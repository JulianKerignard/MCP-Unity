# MCP Unity

A [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) plugin for Unity Editor that connects AI assistants like Claude to your Unity projects. Exposes 164 tools for full Unity Editor control through natural language, plus an integrated multi-provider AI chat panel.

## How It Works

```
Claude  <-->  MCP (stdio)  <-->  Node.js Bridge  <-->  WebSocket  <-->  Unity Editor
```

The plugin has two components:

1. **Unity Editor Plugin** (`Editor/`) — A C# WebSocket server running inside the Unity Editor. It receives JSON-RPC 2.0 commands and executes them on Unity's main thread (required for Unity API access).

2. **Node.js Bridge** (`Server~/`) — A TypeScript MCP server that communicates with Claude via stdio. It forwards MCP tool calls to Unity over WebSocket and includes a server-side cache with TTL-based invalidation.

When Claude calls a tool (e.g., `unity_create_gameobject`), the request flows through the bridge to Unity, gets executed, and the result is sent back.

## Requirements

- **Unity 6** (6000.0+)
- **Node.js 18+**
- **Claude Desktop** or **Claude Code CLI**

## Installation

### 1. Add the Unity Package

#### Option A: Unity Package Manager (Git URL)

In Unity, go to **Window > Package Manager > + > Add package from git URL** and enter:

```
https://github.com/juliankerignard/mcp-unity.git
```

#### Option B: Local Installation

Clone or download this repository, then in Unity go to **Window > Package Manager > + > Add package from disk** and select the `package.json` at the root of this repo.

#### Option C: Manual Copy

Copy the entire package into your project's `Packages/` folder:

```
YourProject/
├── Packages/
│   └── com.juliank.mcp-unity/    # This package
│       ├── Editor/
│       ├── Plugins/
│       ├── Server~/
│       └── package.json
```

### 2. Build the Node.js Bridge

```bash
cd Server~
npm install
npm run build
```

This compiles TypeScript to `Server~/build/index.js`.

### 3. Configure Claude

#### Claude Desktop

Add to your `claude_desktop_config.json`:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**Linux**: `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["/absolute/path/to/Server~/build/index.js"],
      "env": {
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

> **Tip**: In Unity, go to **Tools > MCP Unity > Server Window > Claude Config** tab to auto-generate this configuration or copy it to clipboard.

#### Claude Code CLI

```bash
claude mcp add mcp-unity -- node /absolute/path/to/Server~/build/index.js
```

### 4. Start the Server

In Unity, go to **Tools > MCP Unity > Server Window** and click **Start Server** (or enable **Auto-start on Editor load**).

The status indicator turns green when the server is running. Claude can now interact with your Unity project.

## Unity Editor Window

Access via **Tools > MCP Unity > Server Window**. Three tabs:

| Tab | Description |
|-----|-------------|
| **Chat** | Multi-provider AI chat with streaming, tool execution, drag & drop references, Markdown rendering |
| **Settings** | Provider config, tool categories, server settings (port, auto-start, timeout), advanced options |
| **Diagnostics** | Request monitor, server logs with level filtering, Claude config generator |

Quick start shortcut: **Tools > MCP Unity > Quick Start Server**

### Integrated Chat Panel

The Chat tab provides a full AI chat experience inside Unity — no external client needed. It supports 9 providers (Claude, GPT-4, Gemini, DeepSeek, Groq, Mistral, Ollama, LM Studio, Custom) with real-time streaming, automatic tool execution (up to 10 iterations per response), and drag & drop of assets/GameObjects as context.

The chat runs independently from the Node.js bridge — it calls tools directly via the in-process `McpToolRegistry`.

### Destructive Operation Confirmation

When using the chat panel, destructive or long-running operations (e.g. `delete_gameobject`, `write_script`, `switch_platform`, `bake_lighting`) require user confirmation via a dialog before execution. This prevents accidental data loss.

## Available Tools (164)

Tools are organized in **13 categories**. Core tools are always available; other categories are loaded on demand to save tokens.

| Category | Count | Examples |
|----------|-------|---------|
| **Core** | 47 | `unity_list_gameobjects`, `unity_create_gameobject`, `unity_get_component`, `unity_take_screenshot` |
| **Asset** | 16 | `unity_search_assets`, `unity_create_prefab`, `unity_delete_asset`, `unity_move_asset` |
| **Material** | 3 | `unity_get_material`, `unity_set_material`, `unity_create_material` |
| **UI** | 9 | `unity_create_canvas`, `unity_create_ui_element`, `unity_set_rect_transform` |
| **Animator** | 23 | `unity_get_animator_controller`, `unity_create_animator_controller`, `unity_create_blend_tree` |
| **Terrain** | 17 | `unity_create_terrain`, `unity_paint_terrain_texture_batch`, `unity_paint_terrain_path` |
| **Physics** | 8 | `unity_raycast`, `unity_setup_rigidbody`, `unity_bake_navmesh` |
| **Audio** | 3 | `unity_setup_audio_source`, `unity_create_audio_mixer` |
| **Rendering** | 13 | `unity_bake_lighting`, `unity_bake_occlusion`, `unity_configure_camera` |
| **Build** | 6 | `unity_get_build_settings`, `unity_switch_platform`, `unity_add_package` |
| **Settings** | 11 | `unity_get_project_settings`, `unity_set_quality_level`, `unity_create_tag` |
| **Input** | 3 | `unity_get_input_actions`, `unity_add_input_action` |
| **Advanced** | 5 | `unity_set_reference`, `unity_create_scriptable_object` |

The **47 core tools** (including 2 meta-tools: `unity_list_tool_categories` and `unity_enable_tool_category`) are always loaded and cover GameObjects, Components, Scenes, Scripts, Editor state, Selection, Memory, and more.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_PORT` | `8090` | WebSocket port to connect to Unity |
| `UNITY_HOST` | `localhost` | Unity host address |
| `UNITY_SECRET` | — | Shared secret for WebSocket authentication (optional) |
| `DEBUG` | `false` | Enable debug logging (`true` or `1`) |
| `REQUEST_TIMEOUT` | `10000` | Request timeout in ms |
| `RECONNECT_INTERVAL` | `3000` | Reconnect interval in ms |
| `MAX_RECONNECT_ATTEMPTS` | `3` | Max reconnect attempts before giving up |

When `UNITY_SECRET` is set, the bridge appends it as a `?secret=` query parameter on the WebSocket connection. Unity validates it on connect and rejects mismatches. Set the same secret in Unity via **Tools > MCP Unity > Server Window > Settings > Shared Secret**. The auto-generated Claude configs include the `UNITY_SECRET` env var automatically.

## Bridge-Side Caching

The Node.js bridge caches read-only tool results to reduce Unity round-trips:

| Category | TTL | Tools |
|----------|-----|-------|
| `editorState` | 5s | `unity_get_editor_state` |
| `hierarchy` | 30s | `unity_list_gameobjects` |
| `components` | 1min | `unity_memory_get` |
| `assets` | 5min | `unity_search_assets`, `unity_list_folders`, `unity_list_folder_contents` |
| `scenes` | 5min | `unity_get_scene_info` |

Write operations (`create_gameobject`, `modify_component`, etc.) automatically invalidate relevant cache entries.

## Troubleshooting

### Server won't start
- Check that port 8090 (or your configured port) is not already in use
- Verify `websocket-sharp.dll` is in `Plugins/`

### Claude can't connect
- Ensure the Unity server is running (green indicator in Server Window)
- Verify the `Server~/build/index.js` path in your Claude config is correct and absolute
- Run `npm run build` in `Server~/` if `build/` folder is missing
- Check Claude Desktop logs or run with `DEBUG=true`

### Tools return errors
- Unity API calls must run on the main thread — the plugin handles this automatically, but heavy operations may timeout
- Increase `REQUEST_TIMEOUT` if operations are slow (default: 10s)
- Check the Logs tab in the Server Window for detailed error messages

### Rebuilding the bridge
```bash
cd Server~
npm run rebuild   # clean + build
npm run typecheck  # check for type errors without building
```

## Development

### Bridge Commands (from `Server~/`)

```bash
npm run build        # Compile TypeScript
npm run dev          # Run with hot reload (tsx)
npm run typecheck    # Type check only
npm run test         # Run tests (vitest)
npm run lint         # ESLint
npm run format       # Prettier
```

### Architecture

```
Editor/McpServer/
├── McpUnityServer.cs        # Main partial class — WebSocket server, message queue, shared helpers
├── McpJsonRpc.cs            # JSON-RPC 2.0 dispatcher
├── McpToolRegistry.cs       # Tool registration, category management, execution
├── McpResourceRegistry.cs   # Resource registration
├── McpProtocol.cs           # Protocol data types
├── McpSettings.cs           # Persistent settings (incl. shared secret)
├── McpDebug.cs              # Conditional logging
├── McpEditorWindow.cs       # Editor UI (hosts Chat, Settings, Diagnostics tabs)
├── Tools/                   # 164 tool implementations (43 files, partial classes)
├── Chat/                    # Integrated AI chat panel (McpChatWindow, ApiClient, ToolBridge, OAuth)
├── Helpers/                 # GameObjectHelpers, ComponentHelpers, ArgumentParser, ColorParser
├── Models/                  # Data models
└── Utils/                   # McpConstants, PathValidator, TypeConverter

Server~/src/
├── index.ts                 # MCP server setup, handlers, server instructions
├── UnityBridge.ts           # WebSocket client with auto-reconnect + secret auth
├── types.ts                 # Zod schemas, BridgeConfig
├── tools.ts                 # Tool definitions (47 core incl. 2 meta + category fallbacks)
├── resources.ts             # Resource definitions + workflow docs
├── cache.ts                 # TTL cache with invalidation map
└── __tests__/               # vitest tests
```

## License

MIT
