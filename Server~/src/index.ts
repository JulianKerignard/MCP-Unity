#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
  ListPromptsRequestSchema,
  GetPromptRequestSchema,
  ErrorCode,
  McpError as SdkMcpError,
} from '@modelcontextprotocol/sdk/types.js';
import { UnityBridge } from './UnityBridge.js';
import {
  BridgeConfig,
  BridgeConfigSchema,
  ConnectionState,
  ToolDefinition,
  ToolResult,
  ResourceDefinition,
  PromptDefinition,
  PromptGetResult,
  ResourceReadResult,
} from './types.js';
import { defaultTools } from './tools.js';
import { defaultResources, workflowDocs } from './resources.js';
import { ServerCache, cacheableTools, cacheInvalidators } from './cache.js';

// ============================================================================
// Configuration
// ============================================================================

function getConfig(): BridgeConfig {
  // SEC-#439: don't duplicate defaults here — let BridgeConfigSchema.parse() fill them in.
  // Previously index.ts hardcoded reconnectInterval=3000 / requestTimeout=10000 /
  // maxReconnectAttempts=3 which silently overrode the schema defaults (5000/30000/10)
  // and drifted over time. Env vars still override.
  return BridgeConfigSchema.parse({
    unityHost: process.env.UNITY_HOST || undefined,
    unityPort: process.env.UNITY_PORT ? parseInt(process.env.UNITY_PORT, 10) : undefined,
    unitySecret: process.env.UNITY_SECRET || undefined,
    reconnectInterval: process.env.RECONNECT_INTERVAL
      ? parseInt(process.env.RECONNECT_INTERVAL, 10)
      : undefined,
    requestTimeout: process.env.REQUEST_TIMEOUT
      ? parseInt(process.env.REQUEST_TIMEOUT, 10)
      : undefined,
    maxReconnectAttempts: process.env.MAX_RECONNECT_ATTEMPTS
      ? parseInt(process.env.MAX_RECONNECT_ATTEMPTS, 10)
      : undefined,
    debug: process.env.DEBUG === 'true' || process.env.DEBUG === '1',
  });
}

// ============================================================================
// Logging
// ============================================================================

function log(message: string, ...args: unknown[]): void {
  console.error(`[MCP Unity] ${message}`, ...args);
}

// ============================================================================
// Main Application
// ============================================================================

async function main(): Promise<void> {
  const config = getConfig();
  log(`Starting MCP Unity Bridge`);
  log(`Unity endpoint: ws://${config.unityHost}:${config.unityPort}`);

  // Create Unity bridge
  const bridge = new UnityBridge(config);

  // Track whether we've served the fallback list while Unity was unreachable.
  // If we did, we must tell the client to refetch once Unity is online so it sees
  // the real (potentially larger) tool list instead of the 47-tool fallback.
  let servedFallbackList = false;

  const refreshClientTools = (reason: string) => {
    if (!servedFallbackList) return;
    servedFallbackList = false;
    log(`Pushing tools/list_changed to client (${reason})`);
    serverCache.clear();
    try {
      server.notification({ method: 'notifications/tools/list_changed' });
    } catch (err) {
      log('Failed to push tools/list_changed:', err);
    }
  };

  // Setup bridge event handlers
  bridge.on('connected', () => {
    log('Connected to Unity');
    refreshClientTools('initial connection');
  });

  bridge.on('disconnected', () => {
    log('Disconnected from Unity');
  });

  bridge.on('reconnected', () => {
    log('Reconnected to Unity');
    refreshClientTools('reconnected');
  });

  bridge.on('reconnectFailed', () => {
    log('Failed to reconnect to Unity after maximum attempts');
  });

  bridge.on('error', (error: Error) => {
    log('Bridge error:', error.message);
  });

  bridge.on('stateChange', (newState: ConnectionState, oldState: ConnectionState) => {
    log(`Connection state: ${oldState} -> ${newState}`);
  });

  // Forward notifications from Unity to MCP client (e.g. tools/list_changed)
  bridge.on('notification', (message: { method?: string }) => {
    if (message.method === 'notifications/tools/list_changed') {
      log('Forwarding tools/list_changed notification to MCP client');
      serverCache.clear(); // Invalidate cache since tool list changed
      server.notification({ method: 'notifications/tools/list_changed' });
    }
  });

  // ============================================================================
  // Server-side Cache
  // ============================================================================

  const serverCache = new ServerCache();

  // Log cache statistics every 5 minutes
  const statsTimer = setInterval(() => {
    const s = serverCache.stats();
    log(`Cache stats — size: ${s.size}, hits: ${s.hits}, misses: ${s.misses}, evictions: ${s.evictions}, hitRate: ${(s.hitRate * 100).toFixed(1)}%`);
  }, 300_000);
  if (typeof statsTimer.unref === 'function') statsTimer.unref();

  // Server instructions for Claude - Dynamic tool loading
  const serverInstructions = `Unity MCP (164 tools, dynamic loading). 47 core tools (incl. 2 meta-tools) always loaded.
Call unity_enable_tool_category(category) to load more. All tool names use unity_ prefix.

TOKEN RULES: outputMode='tree' for list_gameobjects | returnBase64=false for screenshots | size='small' for previews

TOOL INDEX (enable category first):
asset(16): search_assets, get_asset_info, delete_asset, create_folder, move_asset, copy_asset, list_folders, list_folder_contents, get_asset_preview, get_import_settings, set_import_settings, instantiate_prefab, create_prefab, unpack_prefab, apply_prefab_overrides, revert_prefab_overrides
material(3): get_material, set_material, create_material
ui(9): create_canvas, create_ui_element, modify_ui_element, get_ui_hierarchy, set_rect_transform, add_layout_group, add_layout_element, add_content_size_fitter, set_canvas_scaler
animator(23): get_animator_controller, get_animator_parameters, set_animator_parameter, add_animator_parameter, remove_animator_parameter, create_animator_controller, add_animator_layer, validate_animator, get_animator_flow, add_animator_state, delete_animator_state, modify_animator_state, set_default_state, create_blend_tree, add_blend_motion, add_animator_transition, delete_animator_transition, add_transition_condition, remove_transition_condition, modify_transition, list_animation_clips, create_animation_clip, get_clip_info
terrain(17): create_terrain, get_terrain_info, modify_terrain, set_terrain_heights_batch, list_terrain_brushes, add_terrain_layer, paint_terrain_texture_batch, paint_terrain_path, add_terrain_trees, remove_terrain_trees, list_terrain_trees, add_terrain_detail, paint_terrain_detail, remove_terrain_detail, import_heightmap, export_heightmap, set_terrain_neighbors
physics(8): raycast, setup_rigidbody, setup_collider, set_physics_material, bake_navmesh, clear_navmesh, get_navmesh_settings, set_navigation_static
audio(3): setup_audio_source, create_audio_mixer, get_audio_mixer
rendering(13): configure_camera, render_camera_to_file, get_render_pipeline_info, bake_lighting, bake_lighting_async, get_bake_status, cancel_bake, clear_baked_data, get_lightmap_settings, set_lightmap_settings, bake_occlusion, clear_occlusion, bake_reflection_probes
build(6): get_build_settings, manage_build_scenes, switch_platform, list_packages, add_package, remove_package
settings(11): get_project_settings, set_project_settings, set_quality_level, get_physics_layer_collision, set_physics_layer_collision, list_tags, list_layers, set_tag, set_layer, create_tag, create_layer
input(3): get_input_actions, add_input_action, add_input_binding
advanced(5): set_reference, set_reference_array, create_scriptable_object, list_scriptable_object_types, modify_scriptable_object

RESOURCES: Read workflows://[category] for detailed guides (core, animator, materials, prefabs, assets, terrain).`;

  // Create MCP server
  const server = new Server(
    {
      name: 'mcp-unity',
      version: '1.0.0',
    },
    {
      capabilities: {
        tools: {},
        resources: {},
        prompts: {},
      },
      instructions: serverInstructions,
    }
  );

  // ============================================================================
  // Tool Handlers
  // ============================================================================

  server.setRequestHandler(ListToolsRequestSchema, async () => {
    // Give Unity a short window to connect before falling back. Most MCP clients
    // (incl. Claude Code) cache tools/list aggressively, so getting the right
    // answer on the first call is far more reliable than relying on a follow-up
    // tools/list_changed notification.
    if (!bridge.isConnected) {
      bridge.connect().catch(() => {});
      try {
        await bridge.waitForConnection(2000);
      } catch {
        // Unity unreachable — serve fallback and let the notification path
        // upgrade the list when Unity eventually comes online.
        servedFallbackList = true;
        return { tools: defaultTools };
      }
    }

    try {
      const result = await bridge.request<{ tools: ToolDefinition[] }>('tools/list');
      return { tools: result.tools || defaultTools };
    } catch (error) {
      log('Error listing tools:', error);
      servedFallbackList = true;
      return { tools: defaultTools };
    }
  });

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    // Bridge-local tool: doesn't reach Unity. Returns cache + connection stats
    // so the client can verify the bridge is healthy and the cache is working.
    if (name === 'unity_bridge_stats') {
      const s = serverCache.stats();
      const body = {
        bridge: {
          unityConnected: bridge.isConnected,
          unityEndpoint: `ws://${config.unityHost}:${config.unityPort}`,
        },
        cache: {
          size: s.size,
          hits: s.hits,
          misses: s.misses,
          evictions: s.evictions,
          hitRate: Math.round((s.hitRate || 0) * 1000) / 10, // percent, 1 decimal
        },
      };
      return {
        content: [{ type: 'text', text: JSON.stringify(body, null, 2) }],
        isError: false,
      };
    }

    if (!bridge.isConnected) {
      try {
        await bridge.connect();
      } catch (error) {
        throw new SdkMcpError(
          ErrorCode.InternalError,
          `Not connected to Unity: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    }

    // Check cache for read operations
    const cacheCategory = cacheableTools[name];
    let cacheKey: string | undefined;
    if (cacheCategory) {
      cacheKey = `${name}:${JSON.stringify(args || {})}`;
      const cached = serverCache.get(cacheKey);
      if (cached) {
        log(`Cache hit for ${name}`);
        return cached as { content: Array<{ type: string; text: string }>; isError: boolean };
      }
    }

    try {
      const result = await bridge.request<ToolResult>('tools/call', {
        name,
        arguments: args || {},
      });

      const response = {
        content: result.content || [{ type: 'text', text: 'Tool executed successfully' }],
        isError: result.isError || false,
      };

      // Cache read operations (reuse cacheKey computed above)
      if (cacheCategory && !response.isError && cacheKey) {
        serverCache.set(cacheKey, response, cacheCategory);
      }

      // Invalidate cache for write operations
      const invalidations = cacheInvalidators[name];
      if (invalidations) {
        invalidations.forEach((pattern) => serverCache.invalidate(pattern));
      }

      return response;
    } catch (error) {
      log(`Error calling tool '${name}':`, error);

      return {
        content: [
          {
            type: 'text',
            text: `Error executing tool '${name}': ${error instanceof Error ? error.message : String(error)}`,
          },
        ],
        isError: true,
      };
    }
  });

  // ============================================================================
  // Resource Handlers
  // ============================================================================

  server.setRequestHandler(ListResourcesRequestSchema, async () => {
    // Return default resources immediately - don't block waiting for Unity
    if (!bridge.isConnected) {
      bridge.connect().catch(() => {});
      return { resources: defaultResources };
    }

    try {
      const result = await bridge.request<{ resources: ResourceDefinition[] }>('resources/list');
      return { resources: result.resources || defaultResources };
    } catch (error) {
      log('Error listing resources:', error);
      return { resources: defaultResources };
    }
  });

  server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
    const { uri } = request.params;

    // Serve workflow documentation from memory (no Unity connection needed)
    if (uri.startsWith('workflows://')) {
      const content = workflowDocs[uri];
      if (content) {
        return {
          contents: [
            {
              uri,
              mimeType: 'text/markdown',
              text: content,
            },
          ],
        };
      }
      throw new SdkMcpError(ErrorCode.InvalidRequest, `Unknown workflow: ${uri}`);
    }

    // For Unity resources, require connection
    if (!bridge.isConnected) {
      try {
        await bridge.connect();
      } catch (error) {
        throw new SdkMcpError(
          ErrorCode.InternalError,
          `Not connected to Unity: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    }

    try {
      const result = await bridge.request<ResourceReadResult>('resources/read', { uri });
      return { contents: result.contents || [] };
    } catch (error) {
      log(`Error reading resource '${uri}':`, error);
      throw new SdkMcpError(
        ErrorCode.InternalError,
        `Failed to read resource: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  });

  // ============================================================================
  // Prompt Handlers
  // ============================================================================

  server.setRequestHandler(ListPromptsRequestSchema, async () => {
    // Return empty prompts immediately - don't block waiting for Unity
    if (!bridge.isConnected) {
      bridge.connect().catch(() => {});
      return { prompts: [] };
    }

    try {
      const result = await bridge.request<{ prompts: PromptDefinition[] }>('prompts/list');
      return { prompts: result.prompts || [] };
    } catch (error) {
      log('Error listing prompts:', error);
      return { prompts: [] };
    }
  });

  server.setRequestHandler(GetPromptRequestSchema, async (request) => {
    if (!bridge.isConnected) {
      try {
        await bridge.connect();
      } catch (error) {
        throw new SdkMcpError(
          ErrorCode.InternalError,
          `Not connected to Unity: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    }

    const { name, arguments: args } = request.params;

    try {
      const result = await bridge.request<PromptGetResult>('prompts/get', {
        name,
        arguments: args || {},
      });

      return {
        description: result.description,
        messages: result.messages || [],
      };
    } catch (error) {
      log(`Error getting prompt '${name}':`, error);
      throw new SdkMcpError(
        ErrorCode.InternalError,
        `Failed to get prompt: ${error instanceof Error ? error.message : String(error)}`
      );
    }
  });

  // ============================================================================
  // Server Lifecycle
  // ============================================================================

  // Handle server errors
  server.onerror = (error) => {
    log('Server error:', error);
  };

  // SEC-#442: graceful shutdown — close MCP server transport + disconnect bridge.
  // Signal handlers don't await async functions, so wrap the body in try/catch and
  // use the returned promise to surface rejections instead of leaking unhandledRejection.
  let shuttingDown = false;
  const cleanup = async (signal: string) => {
    if (shuttingDown) return;
    shuttingDown = true;
    log(`Shutting down (${signal})...`);
    try {
      serverCache.destroy();
      await server.close();
      await bridge.disconnect();
      process.exit(0);
    } catch (err) {
      log('Shutdown error:', err instanceof Error ? err.message : String(err));
      process.exit(1);
    }
  };

  process.on('SIGINT', () => { void cleanup('SIGINT'); });
  process.on('SIGTERM', () => { void cleanup('SIGTERM'); });
  process.on('SIGHUP', () => { void cleanup('SIGHUP'); });
  process.on('unhandledRejection', (reason) => {
    log('Unhandled promise rejection:', reason instanceof Error ? reason.message : String(reason));
  });

  // Start MCP server with stdio transport FIRST (don't block on Unity)
  const transport = new StdioServerTransport();
  await server.connect(transport);
  log('MCP server running');

  // Attempt initial connection to Unity in background (non-blocking)
  bridge.connect().catch((error) => {
    log(
      'Initial connection to Unity failed:',
      error instanceof Error ? error.message : String(error)
    );
    log('Will retry when requests are made...');
  });
}

// ============================================================================
// Entry Point
// ============================================================================

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});
