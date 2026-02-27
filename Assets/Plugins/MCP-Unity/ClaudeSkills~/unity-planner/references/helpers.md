# Unity Planner — Helper Sections

Referenced by commands as `helpers.md#HN`. Load only the section needed.

---

## H1: Project State Detection

Run these MCP Unity tools in sequence to understand the current project:

1. `unity_get_editor_state` → compilation status, play mode, Unity version
2. `unity_get_project_overview` → render pipeline, packages, asset counts, scenes list
3. `unity_list_gameobjects { outputMode: "tree" }` → scene hierarchy (token-efficient)
4. `unity_list_scenes_in_project` → all project scenes

**Parse and summarize:**
- Unity version + render pipeline (URP/HDRP/Built-in)
- Scene count + active scene name
- Package count + notable packages (Cinemachine, Input System, Addressables, etc.)
- Asset count estimate
- Is the project new (empty hierarchy) or existing?

**Fallback:** If MCP Unity is not connected, ask the user to provide: Unity version, render pipeline, project description, team size, timeline.

---

## H2: Project Level Detection

After H1, determine project level (0-4):

**Auto-detection heuristic:**

| Signal | 0: Proto | 1: Jam | 2: Indie | 3: AA | 4: AAA |
|--------|----------|--------|----------|-------|--------|
| Scenes | 1 | 1-3 | 3-15 | 15-50 | 50+ |
| Assets | <50 | <200 | <1000 | <5000 | 5000+ |
| Packages | <5 | <10 | <20 | <30 | 30+ |
| Scripts | <5 | <20 | <100 | <500 | 500+ |

**If project is new (empty):** Ask user directly:
- "What type of project? (prototype / game jam / indie / AA / AAA)"
- "Estimated timeline?"
- "Team size?"

**Always confirm** the detected level with the user before proceeding.

**Set level in status YAML** (`unity-planner-status.yaml` → `project.level`).

---

## H3: Orchestrator Workflow (/unity-plan)

Full workflow for the `/unity-plan` command:

1. Run H1 (Project State Detection)
2. Run H2 (Project Level Detection)
3. Create `.claude/context/unity-planner/` directory if needed
4. Create `unity-planner-status.yaml` with project metadata (per schema in status-tracking.md)
5. Present the recommended document sequence based on level:
   - **Level 0-1:** `/gdd` (compact) → `/tdd-unity` (compact) → `/unity-story`
   - **Level 2:** `/gdd` → `/tdd-unity` → `/art-direction` → `/milestone` → `/level-design` → `/unity-story`
   - **Level 3-4:** `/gdd` → `/milestone` → `/tdd-unity` → `/art-direction` → `/level-design` → `/unity-review` → `/unity-story`
6. Ask user which document to generate first
7. Route to the chosen command

---

## H4: GDD Generation Workflow (/gdd)

1. Load status YAML → get project level
2. Load GDD template from `references/templates/gdd-template.md`
3. If project already inspected (H1 done), use cached state. Otherwise run H1.
4. **Interactive generation** — for each section:
   - Present the section outline
   - Ask user for key decisions (genre, mechanics, audience, etc.)
   - Generate content based on responses
   - Adapt depth to project level:
     - **Level 0-1:** Sections 1-2 only (Overview + Core Gameplay)
     - **Level 2:** Sections 1-5, 8 (skip Multiplayer, Monetization)
     - **Level 3-4:** All sections including appendices
5. Save to `.claude/context/unity-planner/gdd-{project-name}.md`
6. Update status YAML: `documents.gdd.status = "draft"`, set path
7. Recommend next command based on level

---

## H5: Technical Design Workflow (/tdd-unity)

1. Load status YAML → get project level and GDD (if exists)
2. Run architecture inspection via MCP Unity:
   - `unity_get_render_pipeline_info` → pipeline details, quality settings
   - `unity_get_build_settings` → target platforms, scripting backend
   - `unity_get_project_settings` → player settings
   - `unity_list_packages` → installed packages
   - `unity_list_project_scripts` → existing scripts
   - `unity_get_script_info { className }` → public API of key scripts (if existing project)
3. Load TDD template from `references/templates/tdd-unity-template.md`
4. **Key decisions to discuss with user:**
   - Architecture pattern: ECS vs MonoBehaviour vs Hybrid
   - State management: Singletons, ScriptableObject events, custom event bus
   - Input system: New Input System vs Legacy
   - Scene organization: Single scene vs multi-scene
   - Asset strategy: Resources vs Addressables
5. Generate each section, referencing GDD requirements if available
6. For **Level 0-1:** Sections 1, 3.1, 3.2, 3.3 only (architecture + core systems)
7. For **Level 2:** Sections 1-6 (skip CI/CD)
8. For **Level 3-4:** All sections
9. Save and update status YAML

---

## H6: Level Design Workflow (/level-design)

1. Load status YAML → identify which level/scene to design
2. Inspect scene via MCP Unity:
   - `unity_get_scene_info` → current scene metadata
   - `unity_list_gameobjects { outputMode: "tree" }` → existing hierarchy
   - `unity_get_terrain_info` → terrain config (if terrain present)
   - `unity_get_navmesh_settings` → navigation setup
   - `unity_get_lightmap_settings` → lighting config
3. Load level design template from `references/templates/level-design-template.md`
4. **Discuss with user:**
   - Level purpose in game flow (from GDD if available)
   - Layout approach (linear, open world, hub, arena)
   - Key areas and critical path
   - Terrain vs mesh-based environment
5. Generate document with scene hierarchy plan
6. Include MCP tool sequences for implementation:
   - Terrain: `unity_create_terrain` → `unity_set_terrain_heights_batch` → `unity_add_terrain_layer`
   - Lighting: `unity_bake_lighting` with settings from `unity_set_lightmap_settings`
   - Navigation: `unity_bake_navmesh` after `unity_set_navigation_static`
7. Save and update status YAML

---

## H7: Art Direction Workflow (/art-direction)

1. Load status YAML + GDD (if exists)
2. Inspect via MCP Unity:
   - `unity_get_render_pipeline_info` → pipeline capabilities
   - `unity_search_assets { filter: "t:Material" }` → existing materials
   - `unity_search_assets { filter: "t:Shader" }` → available shaders
3. Load art bible template from `references/templates/art-bible-template.md`
4. **Discuss with user:**
   - Art style (realistic, stylized, pixel, low-poly, etc.)
   - Color palette and mood
   - Visual references (games, films, art)
   - Target platforms (affects quality budget)
5. Generate sections adapted to pipeline:
   - URP: standard Lit/Unlit shaders, shader graph, 2D renderer option
   - HDRP: PBR materials, volumetric fog, ray tracing options
   - Built-in: standard shader, surface shaders
6. Include material naming conventions and MCP workflows:
   - `unity_create_material { name, shaderName, properties }` patterns
   - `unity_set_material` for applying to objects
7. Save and update status YAML

---

## H8: Milestone Planning Workflow (/milestone)

1. Load status YAML + GDD (for feature pillars)
2. Load milestone template from `references/templates/milestone-template.md`
3. **Define milestones based on project level:**
   - **Level 0:** Prototype only
   - **Level 1:** Prototype → Submission
   - **Level 2:** Prototype → Vertical Slice → Alpha → Beta → Gold
   - **Level 3-4:** Prototype → Vertical Slice → Alpha → Beta → Release Candidate → Gold
4. **For each milestone, define:**
   - Definition of Done (clear, measurable criteria)
   - Feature scope from GDD pillars with priorities (P0-P3)
   - Risk matrix (impact + mitigation)
   - Target date (discuss with user)
5. Map feature pillars to milestones:
   - P0 (must-have) → Vertical Slice
   - P1 (should-have) → Alpha
   - P2 (nice-to-have) → Beta
   - P3 (cut if needed) → post-launch or never
6. Save and update status YAML (milestones + feature_pillars)

---

## H9: Unity Story Creation Workflow (/unity-story)

1. Load status YAML + TDD + GDD (for context)
2. Ask user: "What feature/system do you want to implement?"
3. **Generate implementation story:**
   - **Title:** Short description
   - **Objective:** What the player/system should do
   - **Acceptance criteria:** Testable conditions
   - **MCP Tool Sequence:** Step-by-step with exact tool calls
   - **Scripts to create:** List with class names and responsibilities
   - **Components to add:** List with properties
   - **Prefabs to create:** If applicable
   - **Testing plan:** What to verify
4. **MCP tool sequence example:**
   ```
   1. unity_create_script { scriptName: "PlayerController", savePath: "Assets/Scripts/Player/" }
   2. unity_write_script { filePath: "...", content: "..." }
   3. unity_refresh_and_compile
   4. unity_create_gameobject { name: "Player", primitiveType: "Capsule" }
   5. unity_add_component { gameObjectPath: "Player", componentType: "PlayerController" }
   6. unity_setup_rigidbody { gameObjectPath: "Player", mass: 1, useGravity: true }
   7. unity_setup_collider { gameObjectPath: "Player", colliderType: "Capsule" }
   8. unity_save_scene
   ```
5. Cross-reference `mcp-unity/SKILL.md` for tool details
6. The story can be directly executed with MCP Unity tools or handed to `/team` for parallel implementation

---

## H10: Architecture Review Workflow (/unity-review)

1. Load status YAML + TDD (if exists)
2. Run comprehensive inspection:
   - `unity_list_project_scripts` → all MonoBehaviours
   - `unity_get_script_info { className }` → public API of each key script
   - `unity_read_script { filePath }` → implementation details of critical scripts
   - `unity_find_gameobjects_by_component { componentType }` → usage patterns
   - `unity_find_missing_references` → broken references
   - `unity_get_console_logs { type: "error" }` → current errors
   - `unity_run_tests` → test results
3. **Generate audit report:**
   - Architecture pattern compliance (vs TDD if exists)
   - Missing references found
   - Script organization quality
   - Component coupling analysis
   - Performance concerns (excess rigidbodies, unoptimized materials, etc.)
   - Test coverage assessment
   - Recommendations with priority
4. Compare against TDD if available, flag deviations
5. Save report and update status YAML

---

## H11: Document Save Convention

**Base directory:** `.claude/context/unity-planner/`

**File naming:**
- Status: `unity-planner-status.yaml`
- GDD: `gdd-{project-name-kebab}.md`
- TDD: `tdd-{project-name-kebab}.md`
- Level: `level-{level-name-kebab}.md`
- Art: `art-{project-name-kebab}.md`
- Milestones: `milestones-{project-name-kebab}.md`
- Review: `review-{project-name-kebab}-{date}.md`

**Before saving:**
1. Create `.claude/context/unity-planner/` if it doesn't exist
2. Check if file already exists — if yes, ask user: overwrite or create new version?
3. Write file
4. Update status YAML with new path and status

---

## H12: MCP Tool Category Activation

Each workflow needs specific MCP Unity tool categories. Enable only what's needed to save tokens.

| Workflow | Categories to Enable |
|----------|---------------------|
| H1: Project Inspection | core (always active) |
| H5: Technical Design | build, settings, rendering |
| H6: Level Design | terrain, physics, rendering, audio |
| H7: Art Direction | material, rendering, asset |
| H9: Unity Story | varies per story — enable based on feature type |
| H10: Architecture Review | build, settings |

**Activation command:** `unity_enable_tool_category { category: "terrain" }`

**Token budget reminder:**
- 47 core tools always loaded
- 117 additional tools loaded on-demand by category
- Always use `outputMode: "tree"` for hierarchy queries
- Never use `returnBase64: true` for screenshots
- Cross-reference `mcp-unity/SKILL.md` Section 4 for optimization rules
