---
name: "TDD Unity"
description: "Create a Technical Design Document for a Unity project. Use when the user says '/tdd-unity', 'technical design document', 'unity architecture', 'technical architecture', 'TDD unity', or wants to define the game's technical architecture. Covers render pipeline, patterns, performance budgets, packages."
---

# TDD Unity — Technical Design Document

Generates a Technical Design Document with Unity-specific architecture decisions.

## Prerequisites

- `/unity-plan` or `/gdd` executed (for context)
- MCP Unity connected (to inspect the project)

## Step-by-Step Guide

### Step 1: Inspect Project via MCP Unity

```
unity_get_render_pipeline_info → pipeline details, quality settings
unity_get_build_settings → target platforms, scripting backend
unity_get_project_settings → player settings
unity_list_packages → installed packages
unity_list_project_scripts → existing scripts
unity_get_script_info { className } → public API of key scripts
unity_get_project_overview → asset counts, Unity version
```

### Step 2: Load Template

Template: see `unity-planner/references/templates/tdd-unity-template.md`

### Step 3: Key Decisions — Decision Trees

For each decision, inspect the project first, then recommend with justification.

#### Architecture Pattern

```
What type of game?
├── Simple / Prototype / Jam
│   └── ✅ MonoBehaviour + ScriptableObject events
│       (fast to prototype, Unity-native)
├── Medium complexity (Indie)
│   ├── Many interacting systems?
│   │   └── ✅ Service Locator + Event Bus
│   │       (loose coupling, testable)
│   └── Performance-critical (thousands of entities)?
│       └── ✅ ECS (DOTS) or Hybrid
│           (data-oriented, cache-friendly)
└── Large / AA+
    └── ✅ Clean Architecture + Assembly Definitions
        (modular, team-scalable, CI-friendly)
```

#### State Management

```
How many systems share state?
├── 1-2 systems → Direct references (simple)
├── 3-5 systems → ScriptableObject shared variables
├── 5-10 systems → Event Bus (pub/sub)
└── 10+ systems → Full DI framework (VContainer, Zenject)
```

#### Scene Organization

```
How many scenes?
├── 1-3 scenes → Single scene or basic LoadScene
├── 3-15 scenes → Multi-scene with additive loading
│   (shared scene for managers + per-level scenes)
└── 15+ scenes → Addressable scene loading
    (async, memory-managed, downloadable)
```

#### Input System

```
Target platforms?
├── PC only → New Input System (actions + bindings)
├── PC + Console → New Input System (mandatory for gamepad rebinding)
├── Mobile → New Input System + On-Screen controls
└── Legacy project → Old Input Manager (if no budget to migrate)
```

#### Asset Strategy

```
Project size?
├── Small (<500 assets) → Resources/ folder (simple)
├── Medium (<2000 assets) → Addressables (async loading)
└── Large (2000+ or DLC) → Addressables + Remote catalog
    (streaming, patching, DLC support)
```

### Step 4: Performance Budgets

Define budgets per platform:

| Metric | Mobile | PC (min) | PC (rec) | Console |
|--------|--------|----------|----------|---------|
| Frame time | 33ms (30fps) | 16ms (60fps) | 16ms (60fps) | 16ms (60fps) |
| Draw calls | < 100 | < 500 | < 1000 | < 500 |
| Triangles/frame | < 100K | < 1M | < 5M | < 2M |
| Memory | < 1GB | < 4GB | < 8GB | < 5GB |
| Build size | < 200MB | < 2GB | < 10GB | < 20GB |
| Load time | < 5s | < 10s | < 10s | < 15s |

### Step 5: Generate Sections

Adapt to level:
- **Level 0-1:** Sections 1, 3.1-3.3 (architecture + 3 core systems)
- **Level 2:** Sections 1-6 (everything except CI/CD)
- **Level 3-4:** All sections including CI/CD, testing strategy

### Step 6: Save and Update Status

Save to `.claude/context/unity-planner/tdd-{project}.md`
Update `unity-planner-status.yaml`: `documents.tdd.status = "draft"`

## Assembly Definition Recommendations

| Assembly | Contents | References |
|----------|----------|-----------|
| `Game.Core` | Interfaces, events, data types | None (leaf) |
| `Game.Gameplay` | MonoBehaviours, game systems | Game.Core |
| `Game.UI` | UI controllers, views | Game.Core |
| `Game.Audio` | Audio manager, SFX system | Game.Core |
| `Game.Infrastructure` | Save/Load, networking, analytics | Game.Core |
| `Game.Editor` | Custom editors, tools | All (Editor only) |
| `Game.Tests` | Unit and integration tests | All (Test only) |

## Agent

**Technical Director** — Expert in Unity architecture, render pipelines, performance.
Details: see `unity-planner/references/agents.md` (Agent 2)
MCP workflows: see `unity-planner/references/workflows/mcp-unity-workflows.md` (W2)

## Rules

- **Always inspect** the project via MCP before recommending
- **Justify** every technical choice vs alternatives
- **Include** realistic performance budgets per platform
- **Reference** existing packages before recommending new ones
