# MCP Unity — Custom Tool Development Guide

## Overview

Adding a custom tool requires changes in 3 files (+ optional cache config):

1. **C# handler** in `Editor/McpServer/Tools/` (partial class)
2. **TypeScript definition** in `Server~/src/tools.ts`
3. **Cache invalidation** in `Server~/src/cache.ts` (if tool writes state)

## Step 1: C# — Register and Implement

Create or edit a partial class file under `Editor/McpServer/Tools/`:

```csharp
// In Editor/McpServer/Tools/MyFeatureTools.cs
static partial void RegisterMyFeatureTools()
{
    _toolRegistry.RegisterTool(new McpToolDefinition
    {
        name = "unity_my_tool",
        description = "Short description (keep under 80 chars)",
        inputSchema = new McpInputSchema
        {
            type = "object",
            properties = new Dictionary<string, McpPropertySchema>
            {
                ["path"] = new McpPropertySchema
                {
                    type = "string",
                    description = "Path to the target object"
                },
                ["value"] = new McpPropertySchema
                {
                    type = "number",
                    description = "Value to set"
                }
            },
            required = new List<string> { "path" }
        }
    }, MyToolHandler);
}

private static McpToolResult MyToolHandler(Dictionary<string, object> args)
{
    // Required argument extraction
    var (path, pathErr) = RequireArg(args, "path");
    if (pathErr != null) return pathErr;

    // For asset paths — validates Assets/ prefix, blocks ..
    // var (safePath, safeErr) = TrySanitizePath(rawPath, "path");
    // if (safeErr != null) return safeErr;

    // For GameObjects — finds by path or name, searches inactive
    // var (go, goPath, goErr) = RequireGameObject(args, "path");
    // if (goErr != null) return goErr;

    // Optional argument
    float value = args.ContainsKey("value") ? Convert.ToSingle(args["value"]) : 1.0f;

    // Implementation ...

    // Return success
    return McpResponse.Success(new { result = "ok", path = path });
}
```

Then declare and call the partial method in `McpUnityServer.cs`:

```csharp
static partial void RegisterMyFeatureTools();

// In the registration block:
RegisterMyFeatureTools();
```

## Step 2: TypeScript — Add Tool Definition

Add to `Server~/src/tools.ts` in the appropriate category array:

```typescript
{
  name: 'unity_my_tool',
  description: 'Short description (under 80 chars)',
  inputSchema: {
    type: 'object',
    properties: {
      path: { type: 'string', description: 'Path to target' },
      value: { type: 'number', description: 'Value to set' },
    },
    required: ['path'],
  },
  defer_loading: true, // Always true unless core tool
},
```

## Step 3: Cache Configuration (if tool writes state)

In `Server~/src/cache.ts`:

```typescript
// If the tool is read-only and cacheable:
// Add to cacheableTools:
unity_my_tool: 'components',  // pick appropriate category

// If the tool modifies state:
// Add to cacheInvalidators:
unity_my_tool: ['hierarchy', 'components'],  // categories to invalidate
```

## Step 4: Build

```bash
cd Server~/
npm run build
```

## Conventions

| Rule | Detail |
|------|--------|
| **Naming** | `unity_` prefix + `snake_case` |
| **Required args** | `RequireArg(args, "key")` → returns `(value, error)` |
| **Find GameObject** | `RequireGameObject(args, "key")` → `(go, path, error)` |
| **Path security** | `TrySanitizePath(raw, "label")` — blocks `..`, enforces `Assets/` |
| **Serialization** | Always `JsonHelper.ToJson()` — never `JsonUtility.ToJson()` on Dictionaries |
| **Response** | `McpResponse.Success(data)` or `McpToolResult.Error(message)` |
| **Descriptions** | Under 80 chars (total budget ~1200 tokens for all tool descriptions) |
| **Category** | Add to existing category or create new one in `McpToolRegistry.cs` |

## Helper Methods Available

| Method | Returns | Use |
|--------|---------|-----|
| `RequireArg(args, key)` | `(string, McpToolResult?)` | Extract required string arg |
| `RequireGameObject(args, key)` | `(GameObject, string, McpToolResult?)` | Find GO by path/name |
| `TrySanitizePath(raw, label)` | `(string, McpToolResult?)` | Validate asset path |
| `McpResponse.Success(data)` | `McpToolResult` | Successful response |
| `McpToolResult.Error(msg)` | `McpToolResult` | Error response |
| `JsonHelper.ToJson(obj)` | `string` | Safe Dictionary serialization |

## File Structure

```
Editor/McpServer/
├── McpUnityServer.cs        # Main class — register partial methods here
├── McpToolRegistry.cs       # Category management, tool execution
├── Tools/                   # 43 partial class files for 164 tools
│   ├── GameObjectTools.cs
│   ├── ComponentTools.cs
│   ├── SceneTools.cs
│   ├── ScriptTools.cs
│   ├── AssetTools.cs
│   ├── MaterialTools.cs
│   ├── UITools.cs
│   ├── AnimatorTools.cs
│   ├── TerrainTools.cs
│   ├── PhysicsTools.cs
│   ├── AudioTools.cs
│   ├── RenderingTools.cs
│   ├── BuildTools.cs
│   ├── SettingsTools.cs
│   ├── InputTools.cs
│   └── AdvancedTools.cs
├── Helpers/                 # ArgumentParser, ColorParser, etc.
└── Utils/                   # PathValidator, TypeConverter

Server~/src/
├── tools.ts                 # Tool definitions (add yours here)
├── cache.ts                 # Cache config (add invalidation here)
└── types.ts                 # Shared types
```
