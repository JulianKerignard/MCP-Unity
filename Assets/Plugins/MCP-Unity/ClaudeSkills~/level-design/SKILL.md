---
name: "Level Design"
description: "Plan the design of a Unity level or scene. Use when the user says '/level-design', 'level design', 'design a level', 'plan a scene', 'scene structure', or wants to organize the hierarchy, terrain, lighting, and navigation of a level."
---

# Level Design — Level Design Document

Plans a level's structure: layout, scene hierarchy, terrain, lighting, navigation, audio, pacing.

## Prerequisites

- GDD and/or TDD available (for gameplay and technical context)
- MCP Unity connected (to inspect the current scene)

## Step-by-Step Guide

### Step 1: Inspect Scene via MCP Unity

```
unity_get_scene_info → scene metadata
unity_list_gameobjects { outputMode: "tree" } → current hierarchy
unity_get_terrain_info → terrain config (if present)
unity_get_navmesh_settings → navigation settings
unity_get_lightmap_settings → lighting config
unity_get_render_pipeline_info → pipeline features available
```

### Step 2: Define with User

Key questions to ask:
1. **Purpose:** What role does this level play in the game flow? (tutorial, hub, combat, boss, etc.)
2. **Layout Type:** What spatial structure? (see Layout Patterns below)
3. **Key Zones:** What are the main areas the player visits?
4. **Critical Path:** What is the shortest path from start to goal?
5. **Environment:** Terrain-based or mesh-based environment?
6. **Mood:** Lighting and audio atmosphere?
7. **Duration:** How long should the player spend here?

### Step 3: Choose a Layout Pattern

#### Linear
```
[Start] ──► [Area A] ──► [Area B] ──► [Area C] ──► [Goal]
```
**Use for:** Tutorials, story-driven levels, corridors
**Pros:** Easy to pace, clear direction
**Cons:** Low replayability

#### Branching
```
                ┌──► [Path A] ──┐
[Start] ──►─┤                    ├──► [Goal]
                └──► [Path B] ──┘
```
**Use for:** Exploration, choices-matter, stealth vs action
**Pros:** Player agency, replayability
**Cons:** More content needed, balancing difficulty

#### Hub and Spoke
```
            [Zone A]
               ▲
               │
[Zone D] ◄── [HUB] ──► [Zone B]
               │
               ▼
            [Zone C]
```
**Use for:** RPGs, adventure games, town areas
**Pros:** Player freedom, non-linear exploration
**Cons:** Risk of aimlessness without clear objectives

#### Arena
```
    ┌─────────────────┐
    │     ┌───┐       │
    │     │ C │       │
    │  ┌──┴───┴──┐    │
    │  │ ARENA   │    │
    │  └─────────┘    │
    │  [Spawn] [Spawn]│
    └─────────────────┘
```
**Use for:** Combat encounters, boss fights, PvP
**Pros:** Intense gameplay, good for arena shooters
**Cons:** Limited exploration, repetitive

#### Open World Grid
```
    [A1] [A2] [A3]
    [B1] [B2] [B3]
    [C1] [C2] [C3]
```
**Use for:** Sandbox, survival, open-world
**Pros:** Maximum freedom, emergent gameplay
**Cons:** Hard to pace, expensive to fill with content

#### Metroidvania / Interconnected
```
    [A] ──── [B] ──── [C]
     │                  │
    [D] ──── [E] ──── [F]
              │
             [G]
```
**Use for:** Exploration with gating, ability-based progression
**Pros:** Deep exploration, satisfying backtracking
**Cons:** Complex navigation, easy to get lost

### Step 4: Scene Hierarchy Convention

Recommended hierarchy structure:

```
Scene Root
├── --- ENVIRONMENT ---
│   ├── Terrain
│   ├── Static/
│   │   ├── Ground/
│   │   ├── Buildings/
│   │   └── Props/
│   └── Dynamic/
│       └── Destructibles/
├── --- GAMEPLAY ---
│   ├── SpawnPoints/
│   ├── Triggers/
│   ├── Interactables/
│   └── Waypoints/
├── --- CHARACTERS ---
│   ├── Player
│   └── NPCs/
├── --- LIGHTING ---
│   ├── DirectionalLight
│   ├── PointLights/
│   └── LightProbes/
├── --- AUDIO ---
│   ├── AmbientSources/
│   └── MusicTriggers/
├── --- UI ---
│   └── WorldSpaceCanvas/
└── --- CAMERAS ---
    └── MainCamera
```

Use separators (`--- NAME ---`) for visual clarity in the editor.

### Step 5: Pacing and Flow

Plan the player's emotional journey through the level:

```
Tension
  ▲
  │    ╱╲         ╱╲
  │   ╱  ╲   ╱╲ ╱  ╲     ╱╲
  │  ╱    ╲ ╱  ╳    ╲   ╱  ╲ BOSS
  │ ╱      ╳   ╱╲    ╲ ╱    ╲
  │╱      ╱ ╲ ╱  ╲    ╳      ╲
  ├──────────────────────────────► Time
  Start  Explore  Combat  Rest  Climax  End
```

Guidelines:
- **Introduce** → **Teach** → **Test** → **Reward** → **Escalate**
- Alternate high-tension (combat, puzzle) and low-tension (explore, rest) zones
- Place rewards after challenges, not before
- Give visual landmarks for navigation (tall structures, lights, unique props)

### Step 6: Generate the Document

Template: see `unity-planner/references/templates/level-design-template.md`

Include MCP implementation sequences:
- Terrain: `unity_create_terrain` → `unity_set_terrain_heights_batch` → `unity_add_terrain_layer` → `unity_paint_terrain_texture_batch`
- Trees/Details: `unity_add_terrain_trees` → `unity_add_terrain_detail` → `unity_paint_terrain_detail`
- Lighting: `unity_set_lightmap_settings` → `unity_bake_lighting`
- Navigation: `unity_set_navigation_static` → `unity_bake_navmesh`

### Step 7: Save

`.claude/context/unity-planner/level-{name}.md`

## Performance Considerations by Layout

| Layout | Key Concern | Mitigation |
|--------|------------|-----------|
| Linear | Corridor loading | Additive scene loading at transitions |
| Hub | All doors visible | LOD groups, occlusion culling |
| Arena | Many active enemies | Object pooling, LOD |
| Open World | Draw distance | Terrain LOD, streaming, Addressables |
| Metroidvania | Large interconnected space | Room-based loading, occlusion |

## Agent

**Level Designer** — Expert in spatial design, terrain, lighting, navigation.
Details: see `unity-planner/references/agents.md` (Agent 3)
MCP workflows: W3 in `mcp-unity-workflows.md`
