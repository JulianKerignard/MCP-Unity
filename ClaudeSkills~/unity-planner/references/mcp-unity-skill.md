# MCP Unity Expert Skill

> Complete reference for working with Unity Editor via MCP (Model Context Protocol).
> 164 tools across 13 categories, integrated chat system, server-side caching.

**Version**: 1.0.0 | **MCP Protocol**: 2024-11-05 | **Unity**: 6000.0+ | **Node.js**: 18+

---

## 1. Architecture Overview

### Communication Flow

```
AI Client (Claude Code, Claude Desktop, etc.)
    |  MCP via stdio
    v
Node.js Bridge (Server~/src/)
    |  JSON-RPC 2.0 over WebSocket
    v
Unity Editor Plugin (Editor/McpServer/)
    |
    +-- Tool Execution (main thread, queued)
    +-- Chat Panel --- Direct HTTP/SSE ---> LLM APIs
```

### Components

| Component | Language | Role |
|-----------|----------|------|
| **Node.js Bridge** (`Server~/src/`) | TypeScript | MCP stdio server, WebSocket client, TTL cache |
| **Unity Plugin** (`Editor/McpServer/`) | C# | WebSocket server, tool execution on main thread |
| **Chat System** (`Editor/McpServer/Chat/`) | C# | Multi-provider LLM chat panel (IMGUI) |

### Threading Model

WebSocket messages arrive on background threads. `McpBehavior.OnMessage` queues them to a
`ConcurrentQueue<QueuedMessage>`. `EditorApplication.update` processes up to **10 messages per frame**
on the main thread (required for all Unity API calls).

### Security

- **Path validation**: All asset paths must start with `Assets/`, `..` is blocked (`TrySanitizePath`)
- **WebSocket auth**: Optional shared secret via `UNITY_SECRET` env var (query parameter on connect)
- **Destructive op confirmation**: Chat panel shows dialog before dangerous operations
- **API keys**: Stored in `EditorPrefs` (never committed to version control)
- **OAuth PKCE**: Supported for Anthropic (browser-based flow with auto-refresh)

---

## 2. Setup & Configuration

### Installation

```bash
# Option A: Unity Package Manager (Git URL)
# Window > Package Manager > + > Add package from git URL:
https://github.com/JulianKerignard/mcp-unity.git

# Option B: Local — copy to Packages/com.juliank.mcp-unity/

# Build the bridge
cd Packages/com.juliank.mcp-unity/Server~/
npm install && npm run build
```

### Client Configuration

**Claude Code CLI:**
```bash
claude mcp add mcp-unity -- node /absolute/path/to/Server~/build/index.js
```

**Claude Desktop** (`~/Library/Application Support/Claude/claude_desktop_config.json` macOS):
```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["/absolute/path/to/Server~/build/index.js"],
      "env": { "UNITY_PORT": "8090" }
    }
  }
}
```

**Cursor / Windsurf / other MCP clients**: Same config format as Claude Desktop.

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_HOST` | `localhost` | Unity WebSocket host |
| `UNITY_PORT` | `8090` | Unity WebSocket port |
| `UNITY_SECRET` | — | Shared secret for WebSocket auth |
| `DEBUG` | `false` | Debug logging (`true` or `1`) |
| `REQUEST_TIMEOUT` | `10000` | Request timeout (ms) |
| `RECONNECT_INTERVAL` | `3000` | Reconnect interval (ms) |
| `MAX_RECONNECT_ATTEMPTS` | `3` | Max reconnection attempts |

### McpSettings (Unity-side)

Stored in `ProjectSettings/McpUnitySettings.json`, auto-saved.

| Setting | Default | Description |
|---------|---------|-------------|
| Port | 8090 | WebSocket server port |
| AutoStartServer | true | Start on Editor load |
| ShowNotifications | true | Notification popups |
| RequestTimeoutMs | 30000 | Request timeout |
| LogToConsole | true | Mirror logs to Console |
| LogToFile | false | Write logs to file |
| MinimumLogLevel | Info | Log level filter |
| MaxLogEntries | 500 | Log buffer size |
| UseCustomServerPath | false | Custom bridge path toggle |
| CustomServerPath | — | Path to custom `index.js` |

### Starting the Server

In Unity: **Tools > MCP Unity > Server Window** → click **Start Server**.
Status indicator turns **green** when ready. The Setup Wizard (`Tools > MCP Unity > Setup Wizard`)
guides through initial setup (Node.js check, build, config generation).

---

## 3. Tool Categories & Usage

### Dynamic Loading (Token Optimization)

MCP Unity uses **category-based dynamic loading**:
- **47 core tools** (incl. 2 meta-tools) always available
- **117 additional tools** loaded on demand per category
- Use `unity_list_tool_categories` to discover categories
- Use `unity_enable_tool_category` to load a category

### Meta-Tools

| Tool | Description |
|------|-------------|
| `unity_list_tool_categories` | List categories with status and tool counts |
| `unity_enable_tool_category` | Enable/disable a category (asset, material, ui, animator, terrain, physics, audio, rendering, build, settings, input, advanced) |

### 13 Categories Summary

| Category | Tools | Always Loaded | Key Tools |
|----------|-------|---------------|-----------|
| **Core** | 45 | Yes | `get_editor_state`, `list_gameobjects`, `create_gameobject`, `modify_component_batch` |
| **Asset** | 16 | No | `search_assets`, `instantiate_prefab`, `create_prefab`, `set_import_settings` |
| **Material** | 3 | No | `get_material`, `set_material`, `create_material` |
| **UI** | 9 | No | `create_canvas`, `create_ui_element`, `set_rect_transform`, `add_layout_group` |
| **Animator** | 23 | No | `create_animator_controller`, `add_animator_state`, `create_blend_tree` |
| **Terrain** | 17 | No | `create_terrain`, `set_terrain_heights_batch`, `paint_terrain_texture_batch` |
| **Physics** | 8 | No | `raycast`, `setup_rigidbody`, `setup_collider`, `bake_navmesh` |
| **Audio** | 3 | No | `setup_audio_source`, `create_audio_mixer`, `get_audio_mixer` |
| **Rendering** | 13 | No | `configure_camera`, `bake_lighting`, `set_lightmap_settings` |
| **Build** | 6 | No | `get_build_settings`, `switch_platform`, `add_package` |
| **Settings** | 11 | No | `set_project_settings`, `set_quality_level`, `create_tag`, `create_layer` |
| **Input** | 3 | No | `get_input_actions`, `add_input_action`, `add_input_binding` |
| **Advanced** | 5 | No | `set_reference`, `create_scriptable_object`, `modify_scriptable_object` |

> Full tool reference: [references/tool-reference-complete.md](references/tool-reference-complete.md)

### Essential Workflows

**Session Start (always do this first):**
```
1. unity_get_editor_state              → check play mode, compilation status
2. unity_get_project_overview          → render pipeline, assets, scenes, packages
3. unity_list_gameobjects (tree mode)  → scene hierarchy overview
```

**Scene Modification:**
```
1. unity_list_gameobjects { outputMode: "tree" }   → see hierarchy
2. unity_get_component { gameObjectPath, componentType }   → inspect
3. unity_modify_component_batch { modifications: [...] }   → change (batch!)
4. unity_save_scene                                         → persist
```

**Create Multiple Objects:**
```
unity_create_gameobject_batch {
  objects: [
    { name: "Floor", primitiveType: "Plane", position: {x:0,y:0,z:0}, scale: {x:10,y:1,z:10} },
    { name: "Player", primitiveType: "Capsule", position: {x:0,y:1,z:0},
      components: [{ type: "Rigidbody" }] },
    { name: "Light", primitiveType: "Empty", position: {x:0,y:3,z:0},
      components: [{ type: "Light", properties: { type: 1, intensity: 1.5 } }] }
  ]
}
```

**Script Workflow:**
```
1. unity_create_script { scriptName, savePath, scriptType, methods }
   OR unity_write_script { filePath, content }
2. unity_refresh_and_compile   → trigger domain reload
3. unity_add_component { gameObjectPath, componentType: "YourScript" }
```

**Prefab Workflow:**
```
1. Build a GameObject in scene (create + configure)
2. unity_create_prefab { gameObjectPath, savePath }
3. unity_instantiate_prefab { prefabPath, position }
4. Modify instance → unity_apply_prefab_overrides { gameObjectPath }
```

### Batch Operations

Always prefer batch tools over individual calls:
- `unity_create_gameobject_batch` — create 2+ objects in one call, single Undo
- `unity_modify_component_batch` — modify components on multiple objects at once
- `unity_set_terrain_heights_batch` — sculpt entire terrain regions
- `unity_paint_terrain_texture_batch` — paint textures on regions

### Resources (Read-Only, No Tool Call Needed)

| URI | Type | Content |
|-----|------|---------|
| `unity://project/settings` | JSON | Project name, version, platform, backend |
| `unity://scene/hierarchy` | JSON | Current scene root objects with components |
| `unity://console/logs` | JSON | Recent console log entries |
| `workflows://core` | Markdown | Core workflow guide + token tips |
| `workflows://animator` | Markdown | Animator Controller workflow |
| `workflows://materials` | Markdown | Materials + shader workflow |
| `workflows://prefabs` | Markdown | Prefab creation/instantiation workflow |
| `workflows://assets` | Markdown | Asset search syntax + browser workflow |
| `workflows://terrain` | Markdown | Terrain sculpting, painting, brush guide |

---

## 4. Performance Optimization

### Token Reduction Rules

| Rule | Savings | How |
|------|---------|-----|
| Use `outputMode='tree'` for lists | ~90% | `unity_list_gameobjects { outputMode: "tree" }` |
| Never return base64 screenshots | ~95% | `unity_take_screenshot { returnBase64: false }` → file saved, use Read tool |
| Use `size='small'` for previews | ~75% | `unity_get_asset_preview { size: "small", format: "jpg", jpgQuality: 50 }` |
| Limit search results | variable | `unity_search_assets { maxResults: 20 }` |
| Keep tool categories disabled | ~70% schema | Only enable categories you need |
| Use batch operations | ~50% overhead | One call vs multiple round-trips |

### Screenshot Workflow (Token-Safe)

```javascript
// Step 1: Capture to file (NO base64)
unity_take_screenshot {
  view: "Scene",
  returnBase64: false,
  format: "jpg",
  jpgQuality: 60,
  width: 640,
  height: 360
}
// Returns { savedPath: "Assets/Screenshots/..." }

// Step 2: View with Read tool if needed
Read { file_path: "Assets/Screenshots/screenshot.jpg" }
```

### Asset Preview Workflow

```javascript
// Step 1: Small preview, jpg format
unity_get_asset_preview {
  assetPath: "Assets/Models/Character.fbx",
  size: "small",     // tiny(32), small(64), medium(128)
  format: "jpg",
  jpgQuality: 50
}
// Returns savedPath in Assets/Screenshots/

// Step 2: Read if needed
Read { file_path: savedPath }
```

### Cache System

The Node.js bridge caches read-only results with TTL:

| Category | TTL | Cached Tools |
|----------|-----|--------------|
| `editorState` | 5s | `get_editor_state` |
| `hierarchy` | 30s | `list_gameobjects`, `get_gameobject` |
| `components` | 1 min | `get_component`, `get_material`, `get_script_info`, `memory_*` |
| `assets` | 5 min | `search_assets`, `get_asset_info`, `list_folders`, `read_script` |
| `scenes` | 5 min | `get_scene_info`, `list_scenes`, `get_build_settings`, `get_render_pipeline_info` |

Write operations auto-invalidate relevant cache entries. Max 500 cached entries.
Cleanup runs every 60 seconds.

> Full invalidation map: [references/cache-invalidation-map.md](references/cache-invalidation-map.md)

### Common Component Names

Use exact type names (no namespace prefix needed):
- **Transform**: always present, never add — use `unity_set_transform`
- **Physics**: `Rigidbody`, `BoxCollider`, `SphereCollider`, `CapsuleCollider`, `MeshCollider`
- **Rendering**: `MeshRenderer`, `MeshFilter`, `Light`, `Camera`
- **Audio**: `AudioSource`, `AudioListener`
- **Animation**: `Animator`
- **UI**: `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `Image`, `Text`

### Material Property Names

All material properties start with underscore (`_`):
- **Standard/Built-in**: `_Color`, `_MainTex`, `_Metallic`, `_Glossiness`, `_BumpMap`, `_EmissionColor`
- **URP/Lit**: `_BaseColor`, `_BaseMap`, `_Metallic`, `_Smoothness`
- System auto-detects render pipeline. "Standard" maps to "Universal Render Pipeline/Lit" in URP.

---

## 5. Chat Panel

### Overview

The integrated chat panel runs inside Unity Editor (IMGUI). It calls tools directly via
`McpToolRegistry` — no Node.js bridge needed.

### 9 Provider Presets

| Provider | Models | Context | Auth | Local |
|----------|--------|---------|------|-------|
| **Anthropic Claude** | Sonnet 4.6, Opus 4.6, Haiku 4.5, Sonnet 4.5 | 200K | API Key / OAuth PKCE | No |
| **OpenAI** | GPT-4o, GPT-4o Mini, o3 Mini, GPT-4.1, 4.1 Mini, 4.1 Nano | 128K | API Key | No |
| **Google Gemini** | 2.5 Pro, 2.5 Flash, 2.0 Flash | 1M | API Key | No |
| **DeepSeek** | Chat V3.2, Reasoner R1 | 128K | API Key | No |
| **Groq** | Llama 3.3 70B, Llama 3.1 8B, Llama 4 Maverick, Qwen3 32B | 131K | API Key | No |
| **Mistral AI** | Large 3, Small 3.2, Codestral, Magistral Medium | 128K | API Key | No |
| **Ollama** | Llama 3.3, Qwen3, Qwen 2.5 Coder, DeepSeek R1, Gemma 3 | 131K | None | Yes |
| **LM Studio** | Any local model | 131K | None | Yes |
| **Custom** | Any OpenAI-compatible endpoint | 128K | API Key | — |

> Full provider details: [references/provider-configs.md](references/provider-configs.md)

### Tool Execution Loop

1. AI response arrives via SSE streaming
2. Parser detects `tool_use` content blocks
3. Each tool executed on main thread via `McpToolRegistry`
4. Results added to conversation → follow-up request
5. Loop continues until no tool calls or max 10 iterations

### Destructive Operation Confirmation

These tools trigger a confirmation dialog in chat before execution:

- **Destructive**: `delete_gameobject`, `delete_asset`, `clear_baked_data`, `clear_navmesh`, `clear_occlusion`, `remove_terrain_trees`, `remove_terrain_detail`
- **Scripts**: `write_script`, `create_script`, `update_script`
- **Dangerous**: `execute_menu_item`, `unpack_prefab`
- **Long/Irreversible**: `switch_platform`, `bake_lighting`, `bake_lighting_async`, `bake_navmesh`, `bake_occlusion`

User denial returns "Operation denied by user" error; AI continues without that op.

### Features

- **Drag & drop**: Drag assets/GameObjects into input → `@Name` mention + full context on send
- **Export**: Markdown, JSON, Plain Text, or clipboard (toolbar `⇩` button)
- **Context bar**: Shows token usage vs. model context window
- **Markdown rendering**: Code blocks, headers, tables, blockquotes, links, lists

### EditorPrefs Keys

Per-provider settings stored in `EditorPrefs`:
- `McpUnity_ActiveProvider` — active provider ID
- `McpUnity_ProviderKey_{id}` — API key
- `McpUnity_ProviderModel_{id}` — selected model
- `McpUnity_ProviderMaxTokens_{id}` — max tokens
- `McpUnity_ProviderEndpoint_{id}` — custom endpoint
- `McpUnity_ProviderTemp_{id}` — temperature

---

## 6. Troubleshooting

### Connection Issues

| Problem | Solution |
|---------|----------|
| Bridge won't connect | Ensure Unity running + MCP server started (green indicator) |
| Port already in use | Change port in Settings tab → Server Settings |
| Tools return "not connected" | Bridge connects async — wait a few seconds after Editor starts |
| WebSocket timeout | Increase `REQUEST_TIMEOUT` env var (default 10s) |
| Node.js not found | Run Setup Wizard: Tools > MCP Unity > Setup Wizard |

### Tool Issues

| Problem | Solution |
|---------|----------|
| `GameObject not found: X` | Use `unity_list_gameobjects` to verify path; tool searches inactive too |
| `Required parameter 'X' is missing` | Check tool's `inputSchema.required` fields |
| `Tool 'X' category 'Y' not enabled` | Call `unity_enable_tool_category` with category name |
| `Invalid asset path` | Paths must start with `Assets/`, no `..` allowed |
| `JsonUtility` returns `{}` | Use `JsonHelper.ToJson()` for Dictionary serialization |

### Performance Issues

| Problem | Solution |
|---------|----------|
| Compilation errors after edit | `unity_refresh_and_compile` to trigger domain reload |
| Cache returns stale data | Verify tool is in `cacheInvalidators` in `cache.ts` |
| Context fills too fast | Disable unused tool categories; use `outputMode='tree'`; avoid base64 |
| Slow tool responses | Check request monitor in Diagnostics tab for bottlenecks |

### Chat Issues

| Problem | Solution |
|---------|----------|
| "No API key" | Enter key in Settings → Provider Settings |
| Tool not in chat | Check Settings → Tool Categories (may be disabled) |
| Streaming stops | Check network; press Escape then retry; context may be full |
| OAuth login fails | Ensure browser reaches Anthropic auth; disable popup blockers |
| Drag & drop fails | Drop on input field area, not message area |

---

## 7. Advanced Patterns

### Custom Tool Development

> Full guide: [references/custom-tool-guide.md](references/custom-tool-guide.md)

Quick summary:
1. **C#**: Register in `Editor/McpServer/Tools/` (partial class, `McpToolDefinition`)
2. **TypeScript**: Add to `Server~/src/tools.ts` with `defer_loading: true`
3. **Cache**: Add invalidation in `Server~/src/cache.ts` if tool writes state
4. **Build**: `npm run build` in `Server~/`

### Conventions

| Rule | Detail |
|------|--------|
| Tool naming | `unity_` prefix + `snake_case` |
| Required args | `RequireArg(args, "key")` → `(value, error)` |
| Find GameObject | `RequireGameObject(args, "key")` → `(go, path, error)` |
| Path security | `TrySanitizePath(raw, "label")` → blocks `..`, enforces `Assets/` |
| Serialization | `JsonHelper.ToJson()` — never `JsonUtility.ToJson()` on Dictionaries |
| Response | `McpResponse.Success(data)` or `McpToolResult.Error(message)` |
| Descriptions | Keep under 80 chars (token budget: ~1200 total for all descriptions) |

### Key Source Files

```
Editor/McpServer/
├── McpUnityServer.cs        # Main partial class — lifecycle, queue, helpers
├── McpJsonRpc.cs            # JSON-RPC 2.0 dispatcher + JSON parser
├── McpToolRegistry.cs       # Tool registration, categories, execution
├── McpResourceRegistry.cs   # Resource registration
├── McpProtocol.cs           # Protocol data classes
├── McpSettings.cs           # Persistent settings + shared secret
├── Tools/                   # 164 tool implementations (43 files, partial classes)
├── Chat/                    # AI chat panel (McpChatWindow, ApiClient, OAuth)
├── Helpers/                 # ArgumentParser, ColorParser, GameObjectHelpers
├── Models/                  # LogEntry, QueuedMessage, MemoryCacheData
└── Utils/                   # McpConstants, PathValidator, TypeConverter

Server~/src/
├── index.ts                 # MCP stdio server, request handlers
├── UnityBridge.ts           # WebSocket client + secret auth + reconnect
├── tools.ts                 # 47 core tool definitions (meta + core)
├── resources.ts             # Resource definitions + workflow docs
├── cache.ts                 # TTL cache + invalidation map
└── types.ts                 # Zod schemas, error codes, BridgeConfig
```

### Editor Window Tabs

| Tab | Content |
|-----|---------|
| **Chat** | Multi-provider LLM chat with SSE streaming, tool execution, drag & drop |
| **Settings** | Provider config, tool categories, server settings, advanced options |
| **Diagnostics** | Request monitor, logs (color-coded), Claude config generator |
| **Toolbar** | Scene view overlay with server status + start/stop button |
