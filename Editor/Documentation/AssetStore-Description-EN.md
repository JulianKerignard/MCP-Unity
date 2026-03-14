# Conductor — MCP AI Toolkit for Unity

# Asset Store Submission — Form Fields

---
---
---

## 1. SUMMARY (10-200 characters)

---

Control Unity with AI: 165 MCP tools, built-in multi-provider chat, zero code. Works with Claude, GPT-4, Gemini, Ollama.

---
---
---

## 2. DESCRIPTION

---

Conductor MCP turns your Unity Editor into an AI-driven development environment. Ask Claude, GPT-4, Gemini, or any LLM to create GameObjects, configure components, sculpt terrain, build animators, set up lighting, manage builds — all in natural language.

The plugin implements the Model Context Protocol (MCP), an open standard by Anthropic. It exposes 165 tools covering nearly the entire Unity Editor API, organized into 13 categories that load on demand to save up to 70% on token usage.

Conductor also includes an AI chat panel built directly into Unity: 9 AI providers (Claude, GPT-4, Gemini, DeepSeek, Groq, Mistral, Ollama, LM Studio, Custom), real-time token-by-token streaming, automatic tool execution loop (up to 10 iterations per response), and drag & drop assets or GameObjects as context — without ever leaving the Editor.

Every operation goes through Unity's Undo system: everything is reversible with Ctrl+Z. Destructive actions (deletion, script overwrite, baking, platform switch) require confirmation before execution.

Compatible with Claude Desktop, Claude Code CLI, Cursor, Windsurf, VS Code / GitHub Copilot, and any MCP-compliant client. A built-in Setup Wizard generates configuration for all editors in one click.

Works offline with Ollama or LM Studio — no data leaves your machine.

**Support**: GitHub Issues (github.com/JulianKerignard/MCP-Unity/issues) | Email: jujukerignard4@gmail.com

---
---
---

## 3. TECHNICAL DETAILS — Key Features

---

- 165 AI-controllable Unity tools across 13 categories: Core (48), Asset (16), Animator (23), Terrain (17), Rendering (13), Settings (11), UI (9), Physics (8), Build (6), Advanced (5), Material (3), Audio (3), Input (3)

- Dynamic category loading: only 47 Core tools loaded by default, others load on demand via unity_enable_tool_category, reducing token consumption by 70%

- Built-in AI chat panel inside Unity Editor with 9 supported providers: Anthropic Claude, OpenAI, Google Gemini, DeepSeek, Groq, Mistral AI, Ollama, LM Studio, Custom endpoint

- Real-time token-by-token SSE streaming with Markdown rendering, exportable history (Markdown, JSON, plain text, clipboard)

- Automatic execution loop: AI calls tools, Conductor executes, returns results, AI continues — up to 10 iterations per response with no user intervention

- Mandatory confirmation for 17 destructive operations (deletion, script overwrite, platform switch, baking) via Unity dialog

- All operations use Unity's Undo system (Ctrl+Z) — every action is reversible

- Two-tier architecture: C# Plugin (WebSocket server in Editor, main-thread execution) + Node.js Bridge (MCP stdio server, TTL cache, JSON-RPC 2.0 translation)

- Smart server-side cache with per-category TTL (editorState: 5s, hierarchy: 30s, components: 1min, assets: 5min) and automatic write invalidation

- Optional WebSocket authentication via shared secret between bridge and Editor

- Compatible with all MCP clients: Claude Desktop, Claude Code CLI, Cursor, Windsurf

- Built-in Setup Wizard: Node.js verification, npm install, automatic build, Claude config generation

- Drag & drop assets and GameObjects into chat as AI context

- OAuth 2.0 + PKCE authentication for Anthropic (no API key required)

- Works offline with Ollama or LM Studio — no data leaves the machine

- Automatic URP/HDRP material mapping (detects active render pipeline)

- 3 included samples: Quick Start, Terrain Builder, Batch Workflow

- Full documentation in English and French included in the package

- Extensible: add your own tools as partial classes of McpUnityServer

- Requires Unity 6 (6000.0.0f1+) and Node.js 18+

---
---
---

## 4. COMPATIBILITY

---

| Requirement | Version |
|-------------|---------|
| Unity | 6000.0.0f1+ (Unity 6) |
| Node.js | 18+ |
| Platforms | Windows, macOS, Linux (Editor only) |
| Render Pipelines | Built-in, URP, HDRP (auto-detected) |
| MCP Clients | Claude Desktop, Claude Code CLI, Cursor, Windsurf, any MCP-compliant |
| Local AI | Ollama, LM Studio (offline-capable) |

---
---
---

## 5. TAGS (max 15)

---

AI, MCP, Claude, GPT, LLM, Chat, Automation, Editor Tool, Code Generation, Terrain, Animation, Natural Language, Productivity, Level Design, Scripting

---
---
---

## 6. PACKAGE CONTENTS

---

```
com.juliank.mcp-unity/
├── Editor/
│   ├── McpServer/
│   │   ├── McpUnityServer.cs          — Main server + 164 tools (partial classes)
│   │   ├── Chat/                      — Multi-provider AI chat panel (IMGUI)
│   │   ├── Tools/                     — 43 tool implementation files
│   │   ├── Helpers/                   — ArgumentParser, GameObjectHelpers, etc.
│   │   ├── Utils/                     — PathValidator, TypeConverter
│   │   └── Models/                    — Data models
│   └── Documentation/
│       ├── MCP-Unity-Documentation-EN.md / .pdf
│       ├── MCP-Unity-Documentation-FR.md / .pdf
│       ├── AssetStore-Description-EN.md
│       └── AssetStore-Description-FR.md
├── Plugins/
│   └── websocket-sharp.dll            — WebSocket library (Editor-only)
├── Server~/                           — Node.js MCP bridge (not imported by Unity)
│   ├── src/                           — TypeScript source (6 files + tests)
│   ├── build/                         — Compiled JS (auto-built)
│   └── package.json
├── Samples~/
│   ├── 01 - Quick Start/
│   ├── 02 - Terrain Builder/
│   └── 03 - Batch Workflow/
├── Tests~/Editor/                     — C# NUnit tests (120+ tests)
├── LICENSE (MIT)
├── THIRD_PARTY_NOTICES.md
├── CHANGELOG.md
└── package.json
```

---
---
---

## 7. RELEASE NOTES (v1.0.0)

---

Initial release.

- 164 tools across 13 categories with dynamic loading
- Multi-provider AI chat panel (9 providers, SSE streaming)
- Automatic tool execution loop (up to 10 iterations)
- Destructive operation confirmation system
- WebSocket shared-secret authentication
- Server-side TTL cache with write invalidation
- Setup Wizard for one-click configuration
- OAuth 2.0 + PKCE for Anthropic
- Drag & drop asset/GameObject context in chat
- Full Undo support for all operations
- 230 TypeScript + 120 C# automated tests
- Documentation in English and French (MD + PDF)

---
---
---

## 8. SCREENSHOTS / MEDIA (descriptions for submission)

---

1. **Chat Panel** — AI chat inside Unity Editor with tool execution results, Markdown rendering, and token counter
2. **Setup Wizard** — One-click Node.js check, bridge build, and Claude config generation
3. **Tool Categories** — Settings panel showing 13 toggleable categories with tool counts
4. **Terrain Sculpting** — AI sculpting terrain via natural language commands with before/after
5. **Animator Builder** — AI creating an Animator Controller with states, transitions, and blend trees
6. **Diagnostics** — Request monitor showing live tool execution timing and logs
7. **Architecture Diagram** — Two-tier architecture: AI Client ↔ Node.js Bridge ↔ Unity Plugin
