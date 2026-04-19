# Unity Project Planner

Plan and architect Unity game projects with 6 specialized agents, 5 document types, and full MCP Unity tool integration. Inspired by the BMad Method, adapted for game development.

## Prerequisites

- Unity project open with MCP Unity connected
- MCP Unity skill installed (in project `.claude/skills/mcp-unity/` or global `mcp-unity/`)

## Slash Commands

| Command | Agent | Produces | Phase |
|---------|-------|----------|-------|
| `/unity-plan` | Producer | Status YAML + routes to other commands | Orchestrator |
| `/gdd` | Game Designer | Game Design Document | Analysis |
| `/tdd-unity` | Technical Director | Technical Design Document | Solutioning |
| `/level-design` | Level Designer | Level Design Document | Solutioning |
| `/art-direction` | Art Director | Art Bible / Style Guide | Solutioning |
| `/milestone` | Producer | Milestone Plan | Planning |
| `/unity-story` | Unity Developer | Implementation stories with MCP sequences | Implementation |
| `/unity-review` | Technical Director | Architecture audit report | Review |

## Project Level System

The skill adapts document depth based on project scale:

| Level | Name | Timeline | Documents | Milestones |
|-------|------|----------|-----------|------------|
| 0 | Prototype | 1-3 days | GDD mini + TDD mini | Prototype only |
| 1 | Game Jam | 2-7 days | GDD compact + TDD compact | Proto + Submission |
| 2 | Indie | 3-18 months | All 5 documents | Proto → VS → Alpha → Beta → Gold |
| 3 | AA | 1-3 years | All detailed | Full + platform cert |
| 4 | AAA | 2-5 years | All exhaustive | Full + post-launch |

Auto-detection via MCP Unity: scene count, asset count, package count, script count.
See [project-levels.md](references/workflows/project-levels.md) for detection heuristics.

## Quick Start: `/unity-plan`

1. **Detect project state** via MCP Unity (`unity_get_editor_state`, `unity_get_project_overview`)
2. **Determine project level** (auto-detect or ask user) — see [helpers.md#H2](references/helpers.md)
3. **Create status YAML** at `.claude/context/unity-planner/unity-planner-status.yaml`
4. **Ask user** which documents to generate first
5. **Route** to the appropriate command (`/gdd`, `/tdd-unity`, etc.)
6. **Save status** after each document generation

## Agents

6 specialized agents, each with defined MCP Unity tool access:

| Agent | Role | Key MCP Tools |
|-------|------|---------------|
| **Game Designer** | GDD, mechanics, systems, core loop | `unity_get_project_overview` (read-only) |
| **Technical Director** | Architecture, ECS vs MonoBehaviour, pipeline | `unity_get_render_pipeline_info`, `unity_get_build_settings`, `unity_list_packages`, `unity_get_script_info` |
| **Level Designer** | Scenes, terrain, lighting, navigation | `unity_get_scene_info`, `unity_list_gameobjects`, `unity_get_terrain_info`, `unity_get_lightmap_settings` |
| **Producer** | Milestones, scope, risks, orchestration | `unity_get_project_overview`, `unity_get_build_settings` |
| **Unity Developer** | Implementation via all 164 MCP tools | ALL tools — cross-ref `mcp-unity/SKILL.md` |
| **Art Director** | Visual style, materials, UI, post-processing | `unity_get_render_pipeline_info`, `unity_get_material`, `unity_search_assets` |

Full agent definitions: [agents.md](references/agents.md)

## Document Outputs

| Document | Command | Template | Save Location |
|----------|---------|----------|---------------|
| Game Design Document | `/gdd` | [gdd-template.md](references/templates/gdd-template.md) | `.claude/context/unity-planner/gdd-{project}.md` |
| Technical Design Doc | `/tdd-unity` | [tdd-unity-template.md](references/templates/tdd-unity-template.md) | `.claude/context/unity-planner/tdd-{project}.md` |
| Level Design Doc | `/level-design` | [level-design-template.md](references/templates/level-design-template.md) | `.claude/context/unity-planner/level-{name}.md` |
| Art Bible | `/art-direction` | [art-bible-template.md](references/templates/art-bible-template.md) | `.claude/context/unity-planner/art-{project}.md` |
| Milestone Plan | `/milestone` | [milestone-template.md](references/templates/milestone-template.md) | `.claude/context/unity-planner/milestones-{project}.md` |

## Integration with MCP Unity

4 layers of integration:

1. **Read-only inspection** (planning phase): All agents use MCP Unity read tools to understand the project before generating documents. No modifications during planning.
2. **Tool workflow recipes** (implementation): `/unity-story` produces step-by-step MCP tool sequences. See [mcp-unity-workflows.md](references/workflows/mcp-unity-workflows.md).
3. **Cross-skill reference**: Points to `mcp-unity/SKILL.md` for tool details. Never duplicates the 164-tool reference.
4. **Tool category activation**: Each workflow specifies which categories to enable via `unity_enable_tool_category`. See [helpers.md#H12](references/helpers.md).

## Status Tracking

YAML status persists between sessions at `.claude/context/unity-planner/unity-planner-status.yaml`.

Tracks: project metadata, document status (none/draft/review/approved), milestone progress (planned/in_progress/completed), feature pillars (P0-P3), architecture decisions.

Schema: [status-tracking.md](references/schema/status-tracking.md)

## Helper Pattern

Detailed procedures are in [helpers.md](references/helpers.md) as numbered sections (H1-H12). Commands reference `helpers.md#HN` instead of embedding full instructions. This achieves ~70-85% token savings following the BMad pattern.

| Helper | Purpose |
|--------|---------|
| H1 | Project State Detection |
| H2 | Project Level Detection |
| H3 | Orchestrator Workflow |
| H4 | GDD Generation |
| H5 | Technical Design |
| H6 | Level Design |
| H7 | Art Direction |
| H8 | Milestone Planning |
| H9 | Unity Story Creation |
| H10 | Architecture Review |
| H11 | Document Save Convention |
| H12 | MCP Tool Category Activation |

## Rules

- **Never modify Unity project files during planning** — planning is read-only
- **Always use MCP Unity tools** for project inspection (never guess project state)
- **Save all outputs** to `.claude/context/unity-planner/`
- **Detect project level** before generating any document
- **Adapt document depth** to project level (sections tagged `[LEVEL+]`)
- **Cross-reference MCP Unity skill** for tool details — never duplicate
- **Update status YAML** after every command execution

## Related Skills

- `mcp-unity/` — MCP Unity tool reference (164 tools)
- `/brainstorm` — Explore game ideas before `/gdd`
- `/decompose` — Break down features into tasks before `/unity-story`
- `/start` — Create feature branch after planning
- `/team` — Orchestrate parallel agent work on implementation
