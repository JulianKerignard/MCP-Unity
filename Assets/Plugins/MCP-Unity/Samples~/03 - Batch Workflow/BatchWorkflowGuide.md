# MCP Unity — Batch Workflow Sample

This sample demonstrates advanced multi-step workflows where the AI chains multiple
MCP tools together to accomplish complex tasks in a single conversation turn.

## What's in this sample

| File | Purpose |
|------|---------|
| `BatchWorkflowGuide.md` | This guide |
| `BatchWorkflowPrompts.md` | Ready-to-use batch prompt templates |
| `McpWorkflowRunner.cs` | Editor utility for common batch operations |

---

## Concept: Chaining tools

MCP tools are designed to be composed. A good AI assistant will:
1. Call a **read tool** to inspect current state
2. Decide what **write tools** to call based on the result
3. **Verify** with another read tool after changes

This mirrors how an experienced Unity developer works.

---

## Example Workflows

### Workflow A — Project Audit
> "Audit my project and report all issues"

The AI will:
1. `unity_list_gameobjects` — map all GameObjects in the scene
2. `unity_find_missing_references` — search for broken references
3. `unity_get_project_settings` — check physics, quality, rendering
4. Report a structured list of issues found

### Workflow B — Prefab Library Generation
> "Create prefab variants for each of these configurations: ..."

The AI will:
1. `unity_search_assets` — find the base prefab
2. `unity_instantiate_prefab` x N — instantiate variants
3. `unity_modify_component_batch` x N — configure each
4. `unity_create_prefab` x N — save as new prefabs

### Workflow C — Scene Population
> "Populate the scene with 50 enemies following these spawn rules"

The AI will:
1. `unity_get_scene_info` — find existing spawn points
2. `unity_instantiate_prefab` x 50 — place enemies
3. `unity_modify_component_batch` — configure AI parameters
4. `unity_take_screenshot` — verify placement visually

### Workflow D — Animation Setup
> "Set up a character animator with Idle, Walk, Run, Attack states"

The AI will:
1. `unity_create_animator_controller` — create the .controller asset
2. `unity_add_animator_state` x 4 — add each state
3. `unity_add_animator_transition` x 6 — connect states
4. `unity_add_animator_parameter` x 2 — add Speed + IsAttacking params
5. `unity_get_animator_controller` — verify the full graph

---

## Prompt Templates

See `BatchWorkflowPrompts.md` for copy-paste versions of these workflows.

---

## Performance Notes

- MCP tools run on the main thread in the Unity Editor
- For 50+ operations, the AI may batch requests; this is normal
- Use `unity_take_screenshot` sparingly (high latency)
- `unity_list_gameobjects` with `outputMode: "tree"` is 90% smaller than `"full"` — prefer it for large scenes
- Use the `maxDepth` parameter to limit scope on deep hierarchies

---

## Extending with Custom Tools

If you need a tool that doesn't exist yet, you can add it as a partial class of `McpUnityServer`:

```csharp
// Assets/Scripts/Editor/MyCustomMcpTools.cs
using System;
using System.Collections.Generic;
using McpUnity.Server;
using McpUnity.Helpers;

namespace McpUnity.Server
{
    public partial class McpUnityServer
    {
        static partial void RegisterMyCustomTools()
        {
            _toolRegistry.RegisterTool(
                new McpToolDefinition(
                    "unity_my_tool",
                    "Does something custom.",
                    new Dictionary<string, McpPropertySchema>
                    {
                        ["param1"] = new McpPropertySchema { type = "string", description = "A parameter" }
                    },
                    new List<string> { "param1" }
                ),
                MyToolHandler
            );
        }

        private static McpToolResult MyToolHandler(Dictionary<string, object> args)
        {
            var (param1, err) = RequireArg(args, "param1");
            if (err != null) return err;
            
            // Your logic here
            return McpResponse.Success($"Done with {param1}");
        }
    }
}
```

Then declare the partial method call in your own initialization flow.
