import { ToolDefinition } from './types.js';

// ============================================================================
// Tool definitions organized by category (mirrors C# McpUnityServer categories)
// Only core tools + meta-tools are returned as fallback when Unity is offline.
// ============================================================================

// === META-TOOLS (category management) ===
const metaTools: ToolDefinition[] = [
  {
    name: 'unity_list_tool_categories',
    description: 'List all tool categories with their status (enabled/disabled) and tool counts. Call this first to discover available tools.',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'unity_enable_tool_category',
    description: 'Enable a tool category to make its tools available. Categories: asset, material, ui, animator, terrain, physics, audio, rendering, build, settings, input, advanced',
    inputSchema: {
      type: 'object',
      properties: {
        category: { type: 'string', description: 'Category name to enable' },
        enabled: { type: 'boolean', description: 'true to enable, false to disable (default: true)' },
      },
      required: ['category'],
    },
  },
];

// === CORE TOOLS (always loaded) ===
const coreTools: ToolDefinition[] = [
  // -- Editor State & Selection --
  {
    name: 'unity_get_editor_state',
    description: 'Get editor state (play mode, selection, scene)',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'unity_get_console_logs',
    description: 'EDITOR: Get console logs',
    inputSchema: {
      type: 'object',
      properties: {
        logType: { type: 'string', enum: ['All', 'Error', 'Warning', 'Log', 'Exception', 'Assert'] },
        count: { type: 'integer' },
        includeStackTrace: { type: 'boolean' },
      },
    },
  },
  {
    name: 'unity_clear_console',
    description: 'EDITOR: Clear console',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'unity_get_selection',
    description: 'EDITOR: Get selection',
    inputSchema: { type: 'object', properties: { includeAssets: { type: 'boolean' } } },
  },
  {
    name: 'unity_set_selection',
    description: 'EDITOR: Set selection',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPaths: { type: 'array', items: { type: 'string' } },
        assetPaths: { type: 'array', items: { type: 'string' } },
        clear: { type: 'boolean' },
      },
    },
  },
  {
    name: 'unity_take_screenshot',
    description: 'EDITOR: Screenshot (returnBase64=false saves tokens)',
    inputSchema: {
      type: 'object',
      properties: {
        view: { type: 'string', enum: ['Scene', 'Game'] },
        format: { type: 'string', enum: ['jpg', 'png'] },
        jpgQuality: { type: 'integer' },
        width: { type: 'integer' },
        height: { type: 'integer' },
        savePath: { type: 'string' },
        returnBase64: { type: 'boolean', description: 'Default: false' },
      },
    },
  },
  {
    name: 'unity_execute_menu_item',
    description: 'EDITOR: Execute menu item',
    inputSchema: {
      type: 'object',
      properties: { menuPath: { type: 'string' } },
      required: ['menuPath'],
    },
  },
  {
    name: 'unity_undo',
    description: 'EDITOR: Undo/redo',
    inputSchema: {
      type: 'object',
      properties: { steps: { type: 'integer' }, redo: { type: 'boolean' } },
    },
  },
  {
    name: 'unity_play_mode',
    description: 'EDITOR: Control Play Mode — play, stop, pause, resume, or step one frame.',
    inputSchema: {
      type: 'object',
      properties: {
        action: { type: 'string', enum: ['play', 'stop', 'pause', 'resume', 'step'], description: 'Action to perform' },
      },
      required: ['action'],
    },
  },
  {
    name: 'unity_refresh_and_compile',
    description: 'EDITOR: Refresh assets and recompile scripts. Use after modifying C# files.',
    inputSchema: {
      type: 'object',
      properties: {
        recompile: { type: 'boolean', description: 'Request script recompilation (default: true)' },
        cleanBuild: { type: 'boolean', description: 'Force a clean rebuild clearing build cache (default: false). Only used when recompile is true.' },
      },
    },
  },
  {
    name: 'unity_run_tests',
    description: 'EDITOR: Run Unity Test Framework tests (EditMode/PlayMode) and return results',
    inputSchema: {
      type: 'object',
      properties: {
        testMode: { type: 'string', enum: ['EditMode', 'PlayMode'] },
        filter: { type: 'string' },
        timeoutSeconds: { type: 'integer' },
      },
    },
  },
  // -- GameObject --
  {
    name: 'unity_list_gameobjects',
    description: "List GameObjects. Use outputMode='tree' (90% smaller)",
    inputSchema: {
      type: 'object',
      properties: {
        outputMode: { type: 'string', enum: ['names', 'tree', 'summary', 'full'], description: 'Format: tree|names|summary|full (default: tree)' },
        maxDepth: { type: 'integer', description: 'Max depth (default: 3)' },
        includeInactive: { type: 'boolean' },
        rootOnly: { type: 'boolean' },
        nameFilter: { type: 'string', description: 'Name pattern (*=wildcard)' },
        componentFilter: { type: 'string', description: 'Filter by component' },
        tagFilter: { type: 'string' },
        includeTransform: { type: 'boolean' },
      },
    },
  },
  {
    name: 'unity_create_gameobject',
    description: 'CORE: Create a GameObject with name, type, position, rotation, scale, and components in one call.',
    inputSchema: {
      type: 'object',
      properties: {
        name: { type: 'string', description: 'Name of the GameObject' },
        primitiveType: { type: 'string', enum: ['Empty', 'Cube', 'Sphere', 'Capsule', 'Cylinder', 'Plane', 'Quad'], description: 'Mesh type (default: Empty)' },
        parentPath: { type: 'string', description: 'Parent GameObject path' },
        position: { type: 'object', description: 'World position {x, y, z}' },
        rotation: { type: 'object', description: 'Rotation in euler angles {x, y, z}' },
        scale: { type: 'object', description: 'Local scale {x, y, z} (default: {1,1,1})' },
        components: { type: 'array', description: 'Components to add: [{ type, properties? }]', items: { type: 'object' } },
      },
      required: ['name'],
    },
  },
  {
    name: 'unity_create_gameobject_batch',
    description: 'Create multiple GameObjects (2+) in one call. Same params per object as unity_create_gameobject. One Undo step.',
    inputSchema: {
      type: 'object',
      properties: {
        objects: {
          type: 'array',
          description: 'Array of { name (required), primitiveType, parentPath, position {x,y,z}, rotation {x,y,z}, scale {x,y,z}, components [{type, properties?}] }',
          items: { type: 'object' },
        },
        stopOnError: { type: 'boolean' },
      },
      required: ['objects'],
    },
  },
  {
    name: 'unity_delete_gameobject',
    description: 'CORE: Delete GameObject',
    inputSchema: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
  },
  {
    name: 'unity_rename_gameobject',
    description: 'CORE: Rename GameObject',
    inputSchema: {
      type: 'object',
      properties: { gameObjectPath: { type: 'string' }, newName: { type: 'string' } },
      required: ['gameObjectPath', 'newName'],
    },
  },
  {
    name: 'unity_set_parent',
    description: 'CORE: Set parent',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath: { type: 'string' },
        parentPath: { type: 'string' },
        worldPositionStays: { type: 'boolean' },
      },
      required: ['gameObjectPath'],
    },
  },
  {
    name: 'unity_duplicate_gameobject',
    description: 'HIERARCHY: Duplicate a GameObject',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string' },
        newName: { type: 'string' },
      },
      required: ['path'],
    },
  },
  {
    name: 'unity_move_gameobject',
    description: 'HIERARCHY: Reorder sibling index. NOT for spatial movement — use unity_set_transform.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string' },
        siblingIndex: { type: 'integer' },
      },
      required: ['path', 'siblingIndex'],
    },
  },
  {
    name: 'unity_set_transform',
    description: 'CORE: Move, rotate, and/or scale a GameObject. Supports world and local space. Primary tool for positioning objects.',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string', description: 'Path or name of the GameObject' },
        position: { type: 'object', description: 'World position {x, y, z}' },
        rotation: { type: 'object', description: 'World rotation in euler angles {x, y, z}' },
        localPosition: { type: 'object', description: 'Local position {x, y, z}' },
        localRotation: { type: 'object', description: 'Local euler rotation {x, y, z}' },
        localScale: { type: 'object', description: 'Local scale {x, y, z}' },
      },
      required: ['path'],
    },
  },
  {
    name: 'unity_set_gameobject_active',
    description: 'CORE: Activate or deactivate a GameObject',
    inputSchema: {
      type: 'object',
      properties: {
        path: { type: 'string' },
        active: { type: 'boolean' },
      },
      required: ['path', 'active'],
    },
  },
  {
    name: 'unity_find_gameobjects_by_component',
    description: 'HIERARCHY: Find GameObjects with a specific component',
    inputSchema: {
      type: 'object',
      properties: {
        componentType: { type: 'string' },
        includeInactive: { type: 'boolean' },
      },
      required: ['componentType'],
    },
  },
  // -- Component --
  {
    name: 'unity_get_component',
    description: 'Get component properties',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath: { type: 'string' },
        componentType: { type: 'string' },
      },
      required: ['gameObjectPath', 'componentType'],
    },
  },
  {
    name: 'unity_modify_component_batch',
    description: 'Modify components on multiple GameObjects in one call. One Undo step.',
    inputSchema: {
      type: 'object',
      properties: {
        modifications: {
          type: 'array',
          description: 'Array of { gameObjectPath, componentType, properties }',
          items: { type: 'object' },
        },
        stopOnError: { type: 'boolean' },
      },
      required: ['modifications'],
    },
  },
  {
    name: 'unity_add_component',
    description: 'CORE: Add component (built-in or custom). Transform is always present — never add it, use unity_set_transform instead.',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath: { type: 'string' },
        componentType: { type: 'string' },
        initialProperties: { type: 'object' },
      },
      required: ['gameObjectPath', 'componentType'],
    },
  },
  {
    name: 'unity_list_project_scripts',
    description: 'SCRIPT: List all MonoBehaviour scripts in project',
    inputSchema: {
      type: 'object',
      properties: {
        nameFilter: { type: 'string' },
        includeNamespace: { type: 'boolean' },
      },
    },
  },
  // -- Scene --
  {
    name: 'unity_get_scene_info',
    description: 'SCENE: Get scene info',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'unity_load_scene',
    description: 'SCENE: Load scene',
    inputSchema: {
      type: 'object',
      properties: {
        scenePath: { type: 'string' },
        mode: { type: 'string', enum: ['Single', 'Additive'] },
      },
      required: ['scenePath'],
    },
  },
  {
    name: 'unity_save_scene',
    description: 'SCENE: Save scene',
    inputSchema: {
      type: 'object',
      properties: { scenePath: { type: 'string' }, saveAll: { type: 'boolean' } },
    },
  },
  {
    name: 'unity_create_scene',
    description: 'SCENE: Create scene',
    inputSchema: {
      type: 'object',
      properties: {
        sceneName: { type: 'string' },
        savePath: { type: 'string' },
        setup: { type: 'string', enum: ['default', 'empty'] },
        mode: { type: 'string', enum: ['single', 'additive'] },
      },
      required: ['sceneName'],
    },
  },
  // -- Script --
  {
    name: 'unity_create_script',
    description: 'SCRIPT: Create C# script from template',
    inputSchema: {
      type: 'object',
      properties: {
        scriptName: { type: 'string' },
        savePath: { type: 'string' },
        scriptType: { type: 'string', enum: ['MonoBehaviour', 'ScriptableObject', 'EditorWindow'] },
        namespace: { type: 'string' },
        methods: { type: 'array', items: { type: 'string' } },
      },
      required: ['scriptName', 'savePath'],
    },
  },
  {
    name: 'unity_read_script',
    description: 'SCRIPT: Read .cs file content',
    inputSchema: {
      type: 'object',
      properties: { scriptPath: { type: 'string' } },
      required: ['scriptPath'],
    },
  },
  {
    name: 'unity_get_script_info',
    description: 'SCRIPT: Get public API of a type via reflection',
    inputSchema: {
      type: 'object',
      properties: { typeName: { type: 'string' } },
      required: ['typeName'],
    },
  },
  {
    name: 'unity_write_script',
    description: 'SCRIPT: Write complete C# file with backup',
    inputSchema: {
      type: 'object',
      properties: {
        filePath: { type: 'string' },
        content: { type: 'string' },
        overwrite: { type: 'boolean' },
        dryRun: { type: 'boolean' },
        createBackup: { type: 'boolean' },
      },
      required: ['filePath', 'content'],
    },
  },
  {
    name: 'unity_update_script',
    description: 'SCRIPT: Find-and-replace in .cs file',
    inputSchema: {
      type: 'object',
      properties: {
        filePath: { type: 'string' },
        oldContent: { type: 'string' },
        newContent: { type: 'string' },
        dryRun: { type: 'boolean' },
        createBackup: { type: 'boolean' },
      },
      required: ['filePath', 'oldContent', 'newContent'],
    },
  },
  // -- Focus / Overview / Diagnostics --
  {
    name: 'unity_focus_gameobject',
    description: 'Select and frame a GameObject in the Scene view (press F equivalent). Use after creating objects.',
    inputSchema: {
      type: 'object',
      properties: { path: { type: 'string', description: 'Path or name of the GameObject' } },
      required: ['path'],
    },
  },
  {
    name: 'unity_get_project_overview',
    description: 'Compact project summary: render pipeline, asset counts, build scenes, packages. Call first in a new session.',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'unity_find_missing_references',
    description: 'Scan the scene for null/missing object references on all components.',
    inputSchema: {
      type: 'object',
      properties: { includeInactive: { type: 'boolean', description: 'Include inactive GameObjects (default: true)' } },
    },
  },
  {
    name: 'unity_set_component_enabled',
    description: 'Enable or disable a Behaviour component (script, AudioSource, Renderer, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath: { type: 'string' },
        componentType:  { type: 'string' },
        enabled:        { type: 'boolean' },
      },
      required: ['gameObjectPath', 'componentType', 'enabled'],
    },
  },
  {
    name: 'unity_remove_component',
    description: 'Remove a component from a GameObject (Transform is protected and cannot be removed)',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath: { type: 'string', description: 'Path or name of the GameObject' },
        componentType:  { type: 'string', description: 'Component type to remove' },
      },
      required: ['gameObjectPath', 'componentType'],
    },
  },
  // -- Get single GameObject --
  {
    name: 'unity_get_gameobject',
    description: 'Get full details of ONE GameObject: world/local transform, all components with properties, children list',
    inputSchema: {
      type: 'object',
      properties: {
        path:               { type: 'string',  description: 'Path or name (finds inactive objects too)' },
        includeProperties:  { type: 'boolean', description: 'Include component properties (default: true)' },
        includeChildren:    { type: 'boolean', description: 'Include direct children (default: true)' },
      },
      required: ['path'],
    },
  },

  {
    name: 'unity_get_gameobject_components',
    description: 'List all component types on a GameObject. Lightweight alternative to unity_get_gameobject.',
    inputSchema: {
      type: 'object',
      properties: {
        gameObjectPath:    { type: 'string',  description: 'Path or name of the GameObject' },
        includeProperties: { type: 'boolean', description: 'Include component properties (default: false)' },
      },
      required: ['gameObjectPath'],
    },
  },
  {
    name: 'unity_list_scenes_in_project',
    description: 'List all .unity scene files in the project (not just open ones)',
    inputSchema: {
      type: 'object',
      properties: {
        searchFolder: { type: 'string', description: 'Limit search to a folder (default: entire Assets/)' },
      },
    },
  },
  // -- Memory --
  {
    name: 'unity_memory_get',
    description: 'MEMORY: Get cached data',
    inputSchema: {
      type: 'object',
      properties: {
        section: { type: 'string', enum: ['assets', 'scenes', 'hierarchy', 'operations', 'all'] },
      },
    },
  },
  {
    name: 'unity_memory_refresh',
    description: 'MEMORY: Refresh cache',
    inputSchema: {
      type: 'object',
      properties: { section: { type: 'string', enum: ['assets', 'scenes', 'hierarchy', 'all'] } },
      required: ['section'],
    },
  },
  {
    name: 'unity_memory_clear',
    description: 'MEMORY: Clear cache',
    inputSchema: {
      type: 'object',
      properties: {
        section: { type: 'string', enum: ['assets', 'scenes', 'hierarchy', 'operations', 'all'] },
      },
    },
  },
];

// ============================================================================
// Default fallback: core + meta-tools only (matches C# default behavior)
// When Unity is connected, tools/list returns the actual enabled tools from C#.
// ============================================================================
export const defaultTools: ToolDefinition[] = [...metaTools, ...coreTools];
