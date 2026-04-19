# Unity Planner — Status Tracking Schema

YAML status file persists project planning state between sessions.

## File Location

`.claude/context/unity-planner/unity-planner-status.yaml`

Created by `/unity-plan` (helpers.md#H3). Updated by every other command.

---

## Full Schema

```yaml
# Project metadata (set by /unity-plan)
project:
  name: "My Game"
  level: 2                      # 0-4
  level_name: "indie"           # prototype / jam / indie / aa / aaa
  unity_version: "6000.1.0"
  render_pipeline: "URP"        # URP / HDRP / Built-in
  started: "2026-02-26"
  last_updated: "2026-02-26"

# Document generation status (updated by each command)
documents:
  gdd:
    status: "draft"             # none | draft | review | approved
    path: ".claude/context/unity-planner/gdd-my-game.md"
    last_updated: "2026-02-26"
  tdd:
    status: "none"
    path: null
    last_updated: null
  level_design:
    status: "none"
    levels: []                  # Array of { name, scene, status, path }
  art_bible:
    status: "none"
    path: null
    last_updated: null
  milestones:
    status: "none"
    path: null
    last_updated: null

# Milestone tracking (set by /milestone, updated by /unity-story)
milestones:
  current: "vertical_slice"     # Which milestone is active
  progress:
    prototype:
      status: "completed"       # planned | in_progress | completed
      completion: 100           # 0-100
    vertical_slice:
      status: "in_progress"
      completion: 35
    alpha:
      status: "planned"
      completion: 0
    beta:
      status: "planned"
      completion: 0
    release_candidate:
      status: "planned"
      completion: 0
    gold:
      status: "planned"
      completion: 0

# Feature pillars (from GDD, tracked through milestones)
feature_pillars:
  - name: "Core Loop"
    priority: "P0"              # P0 (must) | P1 (should) | P2 (nice) | P3 (cut)
    status: "in_progress"       # planned | in_progress | blocked | completed
    milestone: "vertical_slice" # Target milestone
    completion: 60              # 0-100
    stories: []                 # Array of story references
  - name: "Progression"
    priority: "P1"
    status: "planned"
    milestone: "alpha"
    completion: 0
    stories: []

# Architecture decisions (from TDD)
architecture:
  pattern: "MonoBehaviour"      # ECS | MonoBehaviour | Hybrid | DOTS
  state_management: "ScriptableObject Events"
  input_system: "New"           # New | Legacy
  scene_strategy: "Multi"       # Single | Multi | Additive
  decided: true                 # false if not yet decided
```

---

## Status Values Reference

| Field | Values | Meaning |
|-------|--------|---------|
| `documents.*.status` | `none` | Not yet created |
| | `draft` | First version created |
| | `review` | Under review / revision |
| | `approved` | Finalized, ready for use |
| `milestones.*.status` | `planned` | Not yet started |
| | `in_progress` | Currently active |
| | `completed` | Done |
| `feature_pillars.*.status` | `planned` | Not yet started |
| | `in_progress` | Being implemented |
| | `blocked` | Blocked by dependency |
| | `completed` | Fully implemented |
| `feature_pillars.*.priority` | `P0` | Must-have for MVP |
| | `P1` | Should-have for launch |
| | `P2` | Nice-to-have |
| | `P3` | Cut if needed |

---

## Update Rules

1. **`/unity-plan`** creates the file with project metadata
2. **`/gdd`** updates `documents.gdd` and `feature_pillars`
3. **`/tdd-unity`** updates `documents.tdd` and `architecture`
4. **`/level-design`** adds to `documents.level_design.levels[]`
5. **`/art-direction`** updates `documents.art_bible`
6. **`/milestone`** updates `milestones` and maps `feature_pillars` to milestones
7. **`/unity-story`** adds stories to `feature_pillars[].stories[]` and updates completion
8. **`/unity-review`** updates `last_updated` and may flag issues

Each command reads the YAML first to understand current state before making changes.

---

## Example: Empty Initial State

```yaml
project:
  name: ""
  level: -1
  level_name: "unknown"
  unity_version: ""
  render_pipeline: ""
  started: ""
  last_updated: ""
documents:
  gdd: { status: "none", path: null }
  tdd: { status: "none", path: null }
  level_design: { status: "none", levels: [] }
  art_bible: { status: "none", path: null }
  milestones: { status: "none", path: null }
milestones:
  current: null
  progress: {}
feature_pillars: []
architecture:
  pattern: null
  state_management: null
  input_system: null
  scene_strategy: null
  decided: false
```
