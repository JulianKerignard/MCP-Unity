---
name: "Unity Story"
description: "Create a Unity implementation story with MCP tool sequences. Use when the user says '/unity-story', 'unity story', 'implement in unity', 'create a unity feature', 'story implementation', or wants a detailed story with MCP Unity tools to execute."
---

# Unity Story — Implementation Story

Generates implementation stories with concrete MCP Unity tool sequences.

## Prerequisites

- TDD and/or GDD available (for context)
- MCP Unity connected and functional
- Skill `mcp-unity` installed for tool reference

## Step-by-Step Guide

### Step 1: Identify the Feature

Ask the user: "What system/feature do you want to implement?"
Cross-reference with GDD and TDD if available.

Inspect current project state:
```
unity_list_project_scripts → existing scripts (avoid duplicates)
unity_list_gameobjects { outputMode: "tree" } → current scene structure
unity_get_project_overview → packages and assets available
```

### Step 2: Generate the Story

Structure:

```markdown
# Story: {Feature Title}

## Objective
{What the player/system should do when complete}

## Acceptance Criteria
- [ ] {Testable condition 1}
- [ ] {Testable condition 2}
- [ ] {Testable condition 3}

## MCP Tool Sequence
{Ordered list of tools to execute — see Step 3}

## Scripts to Create
| Class | Responsibility | Base Class |
|-------|---------------|------------|
| {Name} | {What it does} | MonoBehaviour / ScriptableObject |

## Components to Add
| GameObject | Component | Properties |
|-----------|-----------|-----------|
| {Path} | {Type} | {Key values} |

## Prefabs to Create
| Prefab | Contents | Location |
|--------|---------|----------|
| {Name} | {Components} | Assets/Prefabs/{path} |

## Test Plan
- [ ] Play mode: {manual test}
- [ ] EditMode test: {unit test description}
```

### Step 3: Define the MCP Sequence

Use recipe patterns from `unity-planner/references/workflows/mcp-unity-workflows.md`:

| Recipe | Pattern | Key Tools |
|--------|---------|-----------|
| **A** | Script + Component | `unity_create_script` → `unity_write_script` → `unity_refresh_and_compile` → `unity_add_component` |
| **B** | Physics Object | `unity_create_gameobject` → `unity_setup_rigidbody` → `unity_setup_collider` |
| **C** | Prefab Workflow | Build hierarchy → `unity_create_prefab` → `unity_instantiate_prefab` |
| **D** | Batch Scene Setup | `unity_create_gameobject_batch` → `unity_modify_component_batch` |
| **E** | Material Setup | `unity_create_material` → `unity_set_material` |
| **F** | Terrain | `unity_create_terrain` → heights → layers → trees → details |
| **G** | UI Setup | `unity_create_canvas` → `unity_create_ui_element` → layout |
| **H** | Lighting Bake | `unity_set_lightmap_settings` → `unity_bake_lighting` |
| **I** | Audio Setup | `unity_setup_audio_source` → `unity_create_audio_mixer` |
| **J** | Input System | `unity_get_input_actions` → `unity_add_input_action` → bindings |

### Step 4: Complexity Assessment

Estimate story complexity:

| Complexity | Criteria | Example |
|-----------|---------|---------|
| **S (Small)** | 1 script, 1-2 tools, <30 min | Add a health bar UI |
| **M (Medium)** | 2-3 scripts, 3-5 tools, 1-2 hours | Player movement + camera |
| **L (Large)** | 4+ scripts, 5-10 tools, half day | Inventory system |
| **XL (Extra Large)** | Full system, 10+ tools, 1-2 days | Combat system with AI |

### Step 5: Output

The story can be:
1. **Executed directly** with MCP Unity (run the tool sequences)
2. **Saved** as a task for later implementation
3. **Broken down** into sub-stories if complexity is L or XL

## Agent

**Unity Developer** — Expert in all 164 MCP Unity tools.
Details: see `unity-planner/references/agents.md` (Agent 5)
Full tool reference: see `mcp-unity/references/tool-reference-complete.md`

## Rules

- MCP sequences must be **directly executable**
- **Always** include `unity_save_scene` after scene modifications
- **Always** include `unity_refresh_and_compile` after creating scripts
- Follow **naming conventions** from TDD if available
- Check for existing scripts/prefabs before creating duplicates
