# Changelog

All notable changes to MCP Unity — AI Editor Assistant will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- **UI EventSystem now works with the new Input System** — `unity_create_canvas` adds `InputSystemUIInputModule` (via reflection) when the new Input System is the active backend, instead of the non-functional `StandaloneInputModule` (UI was not clickable on Input-System-only projects)
- **`ScreenSpaceCamera` canvas auto-assigns a render camera** — `unity_create_canvas` now sets `worldCamera` to `Camera.main` (or warns when none exists) so the canvas actually renders
- **Undo now removes added layout components** — `unity_add_content_size_fitter` and `unity_add_layout_element` use `Undo.AddComponent` so Ctrl+Z removes the component (matching the existing layout-group fix), instead of only reverting properties
- **UI tools report when an operation had no effect** — `unity_create_ui_element` / `unity_modify_ui_element` return `warnings` when a color can't be parsed or a sprite can't be loaded, instead of reporting success silently
- Added `ColorParser.TryParse` so callers can distinguish an unparseable color from a valid one

## [1.1.0] - 2026-03-14

### Added
- **`unity_play_mode` tool** — control Play Mode directly (play, stop, pause, resume, step) without menu item workarounds
- **Dynamic plugin path detection** — Setup Wizard and server config now find the plugin wherever it's installed via asmdef search (no more hardcoded paths)
- **VS Code / Copilot support** — Setup generates correct format (`"servers"` root key) for `.vscode/mcp.json`
- **Setup Wizard writes config files** — "Setup All Editors" button creates `.mcp.json` for Claude Code, Cursor, Windsurf, and VS Code in one click
- **Memory guard for screenshots** — rejects Texture2D allocations over 128 MB before they happen
- **`CLAUDE.md`** — development guidance file for Claude Code contributors
- **`filterMode` / `wrapMode` in SetImportSettings** — TextureImporter properties now applied (were read-only before)
- **Cache invalidators** for `unity_run_tests`, `unity_cancel_bake`, `unity_export_heightmap`

### Fixed
- 9 missing `.meta` files added (Unity GUIDs were regenerated on every clone)
- `cleanBuild` parameter added to TypeScript schema for `unity_refresh_and_compile` (was C#-only)
- Script template generates lifecycle methods without explicit `private` modifier (Unity convention)
- Silent `catch {}` blocks replaced with logged warnings in `McpChatOAuth` and `GameObjectTools`
- `Physics.autoSyncTransforms` removed (deprecated in Unity 6)
- All `Debug.LogError` calls routed through `McpDebug.LogError` to respect LogToConsole setting
- npm vulnerabilities resolved (6 HIGH, 1 MODERATE, 1 LOW → 0)

### Changed
- **Renamed to Conductor MCP** — menu: `Tools > Conductor MCP` (single entry, opens main window)
- **Console logging disabled by default** — enable via Server > Advanced > Log to Console
- **UI reorganized** — 4 tabs: Chat, Server, Logs, Setup (was: Chat, Settings, Diagnostics, Setup)
- **Provider & Auth UI compacted** — inline auth dot, simplified OAuth flow, moved endpoint/prompt to Advanced
- **Config generator unified** — `GenerateLocalMcpConfig()` and `GenerateVSCodeMcpConfig()` share `GenerateMcpJson()` helper
- Tool count updated: 165 tools (was 164)

## [1.0.1] - 2026-02-24

### Fixed
- Consolidate 4 duplicate `GetGameObjectPath` implementations → 1 delegate to `GameObjectHelpers`
- Replace 8 empty `catch {}` blocks with logged `catch(Exception)` across 7 files
- Fix `RenderTexture` leak in `EditorScreenshotTools` (added `try/finally` release)
- Use fully-qualified `System.Exception` in `BrushHelper` (avoids `Object` ambiguity)
- Standardize 11 `Debug.LogWarning` → `McpDebug.LogWarning` in Helpers and Utils for consistent verbose control
- Add `process.on('unhandledRejection')` handler in Node.js bridge
- Add null guards for WebSocket access in `UnityBridge.ts` (2 locations)
- Redact shared secret from WebSocket connection log
- Add 5 missing cache invalidators (`unity_set_selection`, `unity_clear_console`, `unity_bake_lighting`, `unity_bake_navmesh`, `unity_create_terrain`)
- Use relative path in `.mcp.json` for portability
- Exclude `src/__tests__/` from TypeScript build output

### Changed
- Harmonize "47 core (incl. 2 meta-tools)" count across all documentation (EN, FR, README, serverInstructions)
- Rewrite Quick Start, Terrain Builder, and Batch Workflow sample guides with corrected tool names
- Rewrite `Server~/README.md` with accurate architecture description
- Update `package.json` terrain sample description (11→17 tools)
- Update test count: 230 TypeScript tests (was 136), 120+ C# NUnit tests

### Added
- 4 new TypeScript test files: `cache-mappings`, `types-extended`, `index-config`, `UnityBridge-extended`
- 4 new C# test files: `PathValidatorTests`, `JsonHelperTests`, `ArgumentParserExtendedTests`, `McpToolRegistryExtendedTests`

## [1.0.0] - 2026-02-19

### Added
- **164 Unity Editor tools** across 13 categories exposed via Model Context Protocol (MCP)
  - **Core (47)**: GameObject CRUD, component management, scene operations, editor selection, editor workflow, script tools, memory cache, editor state, screenshots
  - **Asset (16)**: search, info, previews, import settings, prefab operations, folder/file CRUD (`create_folder`, `delete_asset`, `move_asset`, `copy_asset`)
  - **Animator (23)**: controller CRUD, parameters, layers, states, transitions, blend trees, clips, flow diagram, validation
  - **Terrain (17)**: sculpt, texture paint, path paint, trees, details, heightmap import/export, neighbors
  - **Rendering (13)**: camera config, render-to-file, lighting bake (sync/async), occlusion, reflection probes, lightmap settings
  - **Settings (11)**: project settings, quality levels, physics layer collision, tags and layers
  - **Build (6)**: build settings, scene management, platform switch, package manager (add/remove/list)
  - **UI (9)**: canvas, element creation (Button, Slider, Dropdown, ScrollView…), layout groups, RectTransform
  - **Physics (8)**: raycast, Rigidbody, collider, PhysicsMaterial, NavMesh bake/clear/settings/static
  - **Material (3)**: get/set/create with URP/HDRP auto-mapping
  - **Audio (3)**: AudioSource setup, AudioMixer create/inspect
  - **Input (3)**: Input System actions and bindings
  - **Advanced (5)**: SerializedField references (single + array), ScriptableObject CRUD
- **New in V1**: `unity_get_gameobject`, `unity_set_transform`, `unity_get_gameobject_components`, `unity_list_scenes_in_project`, `unity_focus_gameobject`, `unity_get_project_overview`, `unity_find_missing_references`, `unity_set_component_enabled`
- **Dynamic category loading** — only 47 core tools active by default; others loaded on demand via `unity_enable_tool_category` (saves ~70% token usage)
- **Multi-provider AI Chat panel** integrated in Unity Editor
  - 9 providers: Anthropic Claude, OpenAI, Google Gemini, DeepSeek, Groq, Mistral AI, Ollama, LM Studio, Custom
  - Real-time SSE streaming with token-by-token display and Markdown rendering
  - Automatic MCP tool execution loop (max 10 iterations per response)
  - Drag & drop asset/GameObject references into chat
  - OAuth 2.0 + PKCE authentication for Anthropic
  - Conversation export (Markdown, JSON, Plain Text, clipboard)
- **Setup Wizard** (`Tools > MCP Unity > Setup Wizard`) — auto Node.js check, npm build, Claude config generation
- **Server-side TTL cache** per category (editorState: 5s, hierarchy: 30s, components: 1min, assets/scenes: 5min) with automatic write invalidation
- **230 automated tests** (TypeScript vitest): cache, bridge, tools schema, resources, types/schemas, cache-mappings, index-config
- **C# NUnit tests** (`Tests~/Editor/`): McpToolRegistry, ArgumentParser, PathValidator, JsonHelper (120+ tests)
- Documentation in English and French (MD + PDF)
- Compatible with Claude Desktop, Claude Code CLI, Cursor, and any MCP-compliant client

### Fixed
- `GetConsoleLogs` uses in-process buffer instead of Unity internal reflection (more stable across Unity versions)
- `GetAssetPreview` retry loop — waits up to 500ms for async preview generation
- All `GameObject.Find` calls replaced with `GameObjectHelpers.FindGameObject` — now finds inactive objects
- `SetParent` Undo records correct `worldPositionStays` value
- `JsonHelper.AppendEscaped` correctly escapes all control characters U+0000–U+001F
- `GetProjectSettings` uses `JsonHelper.ToJson` instead of `JsonUtility.ToJson` (was always returning `{}`)
- `EnableCategory` is now case-insensitive
- Cache `invalidate()` uses exact category matching instead of substring (prevents false positives)
- `ParseNumber` returns `null` on parse failure instead of silent `0`
- Script cache invalidated after `unity_create_script` (new class visible immediately to component tools)
