---
name: "Unity Plan"
description: "Initialize and orchestrate Unity project planning. Use when the user says '/unity-plan', 'plan a unity game', 'plan unity project', 'initialize unity project', or wants to start planning a game. Detects project state via MCP Unity, determines level (Prototype/Jam/Indie/AA/AAA), creates status YAML, and routes to appropriate commands."
---

# Unity Plan — Orchestrator

Initializes planning for a Unity project. Detects the project state, determines its size, and guides through the right commands.

## Prerequisites

- Unity project open with MCP Unity connected
- Skill `mcp-unity` installed

## Step-by-Step Guide

### Step 1: Detect Project State

Run MCP Unity tools in sequence:
1. `unity_get_editor_state` → Unity version, compilation state, play mode
2. `unity_get_project_overview` → render pipeline, packages, assets, scenes
3. `unity_list_gameobjects { outputMode: "tree" }` → hierarchy (token-efficient format)
4. `unity_list_scenes_in_project` → all scenes

Summarize: Unity version, render pipeline, scene/asset/package counts, new or existing project.

If MCP Unity is not connected, ask the user: Unity version, render pipeline, project description, team size, timeline.

### Step 2: Determine Project Level

Use auto-detection heuristic:

| Signal | 0: Proto | 1: Jam | 2: Indie | 3: AA | 4: AAA |
|--------|----------|--------|----------|-------|--------|
| Scenes | 1 | 1-3 | 3-15 | 15-50 | 50+ |
| Assets | <50 | <200 | <1000 | <5000 | 5000+ |
| Packages | <5 | <10 | <20 | <30 | 30+ |
| Scripts | <5 | <20 | <100 | <500 | 500+ |

For a new project, ask the type directly (prototype / jam / indie / AA / AAA).
**Always confirm** the level with the user.

### Step 3: Create Status YAML

Create `.claude/context/unity-planner/unity-planner-status.yaml`.
Full schema: see `unity-planner/references/schema/status-tracking.md`

Example for a new Indie project:

```yaml
project:
  name: "MyGame"
  unity_version: "6000.3.9f1"
  render_pipeline: "URP"
  level: 2  # Indie
  platforms: [Windows, WebGL]

documents:
  gdd: { status: "not_started" }
  tdd: { status: "not_started" }
  art_bible: { status: "not_started" }
  milestones: { status: "not_started" }
  level_design: { status: "not_started" }

current_milestone: "prototype"
last_updated: "2025-01-15"
```

### Step 4: Present Recommended Sequence

Based on level:

**Level 0-1 (Proto/Jam):**
```
/gdd (compact) → /tdd-unity (compact) → /unity-story
```

**Level 2 (Indie):**
```
/gdd → /tdd-unity → /art-direction → /milestone → /level-design → /unity-story
```

**Level 3-4 (AA/AAA):**
```
/gdd → /milestone → /tdd-unity → /art-direction → /level-design → /unity-review → /unity-story
```

### Step 5: Route to First Command

Ask the user which document to start with, then launch the corresponding command.

## References

- Agents: see `unity-planner/references/agents.md`
- Project levels: see `unity-planner/references/workflows/project-levels.md`
- Status schema: see `unity-planner/references/schema/status-tracking.md`
- MCP Unity workflows: see `unity-planner/references/workflows/mcp-unity-workflows.md`

## Rules

- **Read-only** during planning — never modify the project
- **Always** use MCP Unity to inspect the project (don't guess)
- **Save** all outputs in `.claude/context/unity-planner/`
- **Confirm** the level with the user before continuing
