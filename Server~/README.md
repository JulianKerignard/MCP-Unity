# MCP Unity Bridge — Node.js Server

Node.js bridge that connects AI assistants (via MCP — Model Context Protocol) to Unity Editor over WebSocket.

## Architecture

```
AI Assistant <-> MCP (stdio) <-> Node.js Bridge <-> WebSocket <-> Unity Editor
```

## Installation

```bash
cd Server~
npm install
npm run build
```

## Usage

### Direct Execution

```bash
npm start
```

### With Claude Desktop

Add to your Claude Desktop configuration (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["/path/to/Server~/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "UNITY_SECRET": "your-shared-secret"
      }
    }
  }
}
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_HOST` | `localhost` | Unity WebSocket server host |
| `UNITY_PORT` | `8090` | Unity WebSocket server port |
| `UNITY_SECRET` | — | Shared secret for WebSocket authentication (optional) |
| `RECONNECT_INTERVAL` | `3000` | Reconnection interval in ms |
| `REQUEST_TIMEOUT` | `10000` | Request timeout in ms |
| `MAX_RECONNECT_ATTEMPTS` | `3` | Max reconnection attempts before failing |
| `DEBUG` | `false` | Enable debug logging (`true` or `1`) |

## Development

```bash
# Run with hot reload
npm run dev

# Watch mode for TypeScript
npm run watch

# Type checking
npm run typecheck

# Run tests (136 tests)
npm test

# Lint and format
npm run lint
npm run format
```

## Protocol

The bridge uses JSON-RPC 2.0 over WebSocket to communicate with Unity.

### Supported Methods

- `tools/list` — List available tools (47 fallback + dynamic from Unity)
- `tools/call` — Call a tool in Unity
- `resources/list` — List available resources
- `resources/read` — Read a resource
- `prompts/list` — List available prompts
- `prompts/get` — Get a prompt

## Project Structure

```
Server~/
  src/
    index.ts        # Entry point, MCP server setup, request handlers
    UnityBridge.ts  # WebSocket client for Unity (JSON-RPC 2.0)
    tools.ts        # Tool definitions and schemas (47 fallback tools)
    resources.ts    # Resource definitions and workflow documentation
    cache.ts        # Server-side cache with TTL and invalidation
    types.ts        # TypeScript types and Zod schemas
    __tests__/      # Vitest test suite (136 tests)
  build/            # Compiled JavaScript (gitignored)
  package.json
  tsconfig.json
  vitest.config.ts
  eslint.config.js
```

## Cache System

The bridge implements a server-side cache with per-category TTL to reduce Unity round-trips:

| Category | TTL | Examples |
|----------|-----|---------|
| `editorState` | 5s | Editor state, console logs |
| `hierarchy` | 30s | GameObject listings |
| `components` | 60s | Component data |
| `assets` | 5min | Asset search results |
| `scenes` | 5min | Scene info, build settings |

Write tools automatically invalidate the relevant cache categories.
