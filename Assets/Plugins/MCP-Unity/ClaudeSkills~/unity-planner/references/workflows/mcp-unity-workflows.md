# Unity Planner — MCP Unity Tool Workflows

Concrete MCP Unity tool sequences used by each agent during planning and implementation.
Cross-references `mcp-unity/SKILL.md` for full tool documentation.

---

## W1: Project Inspection (all agents)

**Categories needed:** core (always active)

```
1. unity_get_editor_state
   → Unity version, compilation status, play mode, active scene

2. unity_get_project_overview
   → Render pipeline, package count, asset count, scene list

3. unity_list_gameobjects { outputMode: "tree" }
   → Current scene hierarchy (token-efficient format)

4. unity_list_scenes_in_project
   → All scenes in the project
```

**Token note:** Always use `outputMode: "tree"` — saves ~90% tokens vs default.

---

## W2: Architecture Analysis (Technical Director)

**Categories needed:** core, build, settings, rendering

```
1. [W1] Project Inspection (all 4 steps)

2. unity_get_render_pipeline_info
   → URP/HDRP/Built-in details, quality settings, renderer features

3. unity_list_packages
   → Installed packages (Input System, Cinemachine, Addressables, etc.)

4. unity_get_build_settings
   → Target platform, scripting backend, build scenes, compression

5. unity_get_project_settings
   → Player settings, quality levels, physics settings

6. unity_list_project_scripts
   → All MonoBehaviours and ScriptableObjects in project

7. unity_get_script_info { className: "<key-class>" }
   → Public API, fields, methods of specific scripts (repeat for key classes)

8. unity_find_missing_references
   → Broken asset/component references
```

**When to use:** `/tdd-unity` (H5), `/unity-review` (H10)

---

## W3: Scene Analysis (Level Designer)

**Categories needed:** core, terrain, physics, rendering, audio

```
1. unity_get_scene_info
   → Scene name, path, root objects count, lighting settings

2. unity_list_gameobjects { outputMode: "tree" }
   → Full hierarchy tree

3. unity_get_terrain_info  [if terrain exists]
   → Size, heightmap resolution, texture layers, tree/detail counts

4. unity_get_navmesh_settings
   → Agent types, walkable areas, bake settings

5. unity_get_lightmap_settings
   → Lightmap mode, resolution, bounces, ambient settings

6. unity_get_audio_mixer  [if audio mixer exists]
   → Mixer groups, effects, snapshots
```

**When to use:** `/level-design` (H6)

---

## W4: Visual Analysis (Art Director)

**Categories needed:** core, material, rendering, asset

```
1. unity_get_render_pipeline_info
   → Pipeline capabilities, available shader features

2. unity_search_assets { filter: "t:Material" }
   → List existing materials in project

3. unity_get_material { assetPath: "<material-path>" }
   → Properties and shader of specific materials (repeat for key materials)

4. unity_search_assets { filter: "t:Shader" }
   → Available shaders (standard + custom)

5. unity_search_assets { filter: "t:Texture2D" }
   → Existing textures (assess current art quality/style)
```

**When to use:** `/art-direction` (H7)

---

## W5: Implementation Recipes (Unity Developer)

**Categories:** varies per feature — activate via `unity_enable_tool_category`

### Recipe A: Script + Component Setup
```
1. unity_create_script { scriptName: "PlayerController", savePath: "Assets/Scripts/Player/" }
2. unity_write_script { filePath: "Assets/Scripts/Player/PlayerController.cs", content: "..." }
3. unity_refresh_and_compile
4. unity_create_gameobject { name: "Player", primitiveType: "Capsule" }
5. unity_add_component { gameObjectPath: "Player", componentType: "PlayerController" }
6. unity_save_scene
```

### Recipe B: Physics Object
```
1. unity_create_gameobject { name: "PhysicsObj", primitiveType: "Cube" }
2. unity_setup_rigidbody { gameObjectPath: "PhysicsObj", mass: 1, useGravity: true }
3. unity_setup_collider { gameObjectPath: "PhysicsObj", colliderType: "Box" }
4. unity_set_physics_material { gameObjectPath: "PhysicsObj", friction: 0.5, bounciness: 0.3 }
```
**Categories:** physics

### Recipe C: Prefab Workflow
```
1. unity_create_gameobject { name: "EnemyBase" }
   → Configure children, components, materials
2. unity_create_prefab { gameObjectPath: "EnemyBase", savePath: "Assets/Prefabs/Enemies/" }
3. unity_delete_gameobject { gameObjectPath: "EnemyBase" }  # clean up scene instance
4. unity_instantiate_prefab { prefabPath: "Assets/Prefabs/Enemies/EnemyBase.prefab", position: {x,y,z} }
```

### Recipe D: Batch Scene Setup
```
1. unity_create_gameobject_batch {
     objects: [
       { name: "Environment", children: [
         { name: "Terrain" },
         { name: "Props_Static" },
         { name: "Props_Dynamic" }
       ]},
       { name: "Gameplay", children: [
         { name: "SpawnPoints" },
         { name: "Enemies" },
         { name: "Interactables" }
       ]},
       { name: "Lighting" },
       { name: "Audio" },
       { name: "Cameras" },
       { name: "UI" }
     ]
   }
2. unity_save_scene
```

### Recipe E: Material Setup
```
1. unity_create_material { name: "M_Environment_Grass", shaderName: "Universal Render Pipeline/Lit" }
2. unity_set_material {
     gameObjectPath: "Environment/Terrain",
     materialPath: "Assets/Materials/M_Environment_Grass.mat",
     properties: { _BaseColor: "#4a7c3f", _Smoothness: 0.3 }
   }
```
**Categories:** material

### Recipe F: Terrain Setup
```
1. unity_create_terrain { width: 500, length: 500, height: 100, resolution: 513 }
2. unity_add_terrain_layer { texturePath: "Assets/Textures/T_Grass_Albedo.png" }
3. unity_set_terrain_heights_batch { heights: [...] }  # or unity_import_heightmap
4. unity_paint_terrain_texture_batch { ... }
5. unity_add_terrain_trees { prefabPath: "Assets/Prefabs/PF_Tree_Oak.prefab", count: 100 }
6. unity_set_navigation_static { gameObjectPath: "Terrain" }
7. unity_bake_navmesh
```
**Categories:** terrain

### Recipe G: UI Setup
```
1. unity_create_canvas { name: "MainCanvas", renderMode: "ScreenSpaceOverlay" }
2. unity_set_canvas_scaler { canvasPath: "MainCanvas", scaleMode: "ScaleWithScreenSize", referenceResolution: { x: 1920, y: 1080 } }
3. unity_create_ui_element { canvasPath: "MainCanvas", elementType: "Panel", name: "HUD" }
4. unity_create_ui_element { parentPath: "MainCanvas/HUD", elementType: "Text", name: "ScoreText" }
5. unity_add_layout_group { gameObjectPath: "MainCanvas/HUD", layoutType: "Horizontal" }
```
**Categories:** ui

### Recipe H: Lighting Bake
```
1. unity_set_lightmap_settings { lightmapper: "Progressive GPU", resolution: 40, bounces: 2 }
2. unity_bake_lighting
   → Wait for completion
3. unity_bake_reflection_probes
```
**Categories:** rendering

### Recipe I: Audio Setup
```
1. unity_create_audio_mixer { name: "MainMixer" }
2. unity_setup_audio_source {
     gameObjectPath: "Audio/AmbientSource",
     clipPath: "Assets/Audio/SFX_Ambient_Forest.wav",
     volume: 0.5, loop: true, spatialBlend: 0
   }
```
**Categories:** audio

### Recipe J: Input System Setup
```
1. unity_add_input_action { actionMapName: "Player", actionName: "Move", controlType: "Vector2" }
2. unity_add_input_binding { actionMapName: "Player", actionName: "Move", path: "<Gamepad>/leftStick" }
3. unity_add_input_binding { actionMapName: "Player", actionName: "Move", path: "<Keyboard>/wasd", isComposite: true }
```
**Categories:** input

---

## W6: Build Verification (Technical Director / Producer)

**Categories needed:** core, build, settings

```
1. unity_get_build_settings
   → Verify all scenes included, correct platform target

2. unity_find_missing_references
   → Check for broken references across all scenes

3. unity_run_tests
   → Run EditMode and PlayMode tests

4. unity_get_console_logs { type: "error" }
   → Check for runtime errors

5. unity_get_project_overview
   → Final asset count and project state check
```

**When to use:** `/unity-review` (H10), Gold milestone verification

---

## W7: Screenshot Documentation (any agent)

**Token-safe pattern — NEVER use returnBase64: true**

```
1. unity_take_screenshot {
     view: "Scene",
     returnBase64: false,
     format: "jpg",
     jpgQuality: 60,
     width: 640,
     height: 360
   }
   → Returns savedPath

2. Read { file_path: "<savedPath>" }
   → Claude sees the image (multimodal)
```

For asset previews:
```
1. unity_get_asset_preview {
     assetPath: "Assets/...",
     size: "small",
     format: "jpg",
     jpgQuality: 50
   }
```

**Rules:**
- Always `returnBase64: false`
- Always `format: "jpg"` with quality < 75
- Prefer `size: "small"` or `"tiny"` for previews
- Screenshots saved to `Assets/Screenshots/`
