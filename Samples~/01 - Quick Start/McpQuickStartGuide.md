# MCP Unity — Quick Start Guide

This sample demonstrates the most common MCP Unity workflows to get you up and running in minutes.

## Prerequisites

1. MCP Unity package installed
2. Node.js 18+ installed
3. An AI assistant configured (Claude Code, Cursor, or Claude Desktop)

## What's in this sample

| File | Purpose |
|------|---------|
| `ExamplePrompts.md` | Copy-paste prompts to try immediately |
| `McpBootstrapValidator.cs` | Editor script to validate your setup |

---

## Step 1 — Open the MCP Unity window

```
Tools > MCP Unity > Server Window
```

Go to the **Settings** tab and click **Start Server**, or use **Tools > MCP Unity > Setup Wizard** to configure your AI editor automatically.

## Step 2 — Start the Node server

In the **Settings** tab, click **Start Server**. The status indicator turns green.

## Step 3 — Try these prompts in your AI editor

### Create a scene from scratch
```
Create a basic game scene with:
- A directional light named "Sun" at rotation (50, -30, 0)
- A plane named "Ground" at scale (10, 1, 10) with a green material
- A cube named "Player" at position (0, 0.5, 0) with a blue material
- A camera named "Main Camera" at position (0, 5, -10) looking at the origin
```

### Inspect and modify
```
Get the current scene hierarchy, then find all GameObjects with a Rigidbody component 
and set their useGravity to true.
```

### Batch rename
```
Find all GameObjects whose name starts with "Cube" and rename them to 
"Block_01", "Block_02", etc. using a consistent naming convention.
```

---

## Troubleshooting

**Server won't start**: Check Node.js is installed: `node --version` in your terminal.

**AI can't connect**: Verify the config file path in the Setup tab matches your editor's config directory.

**Tool not found**: Try `unity_list_tool_categories` to check which categories are enabled. Use `unity_enable_tool_category` to load additional ones.
