# MCP Unity — Complete Tool Reference (164 tools)

## Meta-Tools (2)

| Tool | Description |
|------|-------------|
| `unity_list_tool_categories` | List all categories with tool counts and enabled status |
| `unity_enable_tool_category` | Enable/disable a category to load/unload its tools |

---

## Core — Always Active (45 tools)

### Editor State & Selection (9)

| Tool | Description |
|------|-------------|
| `unity_get_editor_state` | Get editor state (play mode, compilation, selection) |
| `unity_get_selection` | Get currently selected objects |
| `unity_set_selection` | Set editor selection (GameObjects or assets) |
| `unity_focus_gameobject` | Frame a GameObject in Scene view |
| `unity_get_project_overview` | Project overview (stats, scenes, packages) |
| `unity_find_missing_references` | Find missing references in scene |
| `unity_get_console_logs` | Get console logs with filtering |
| `unity_clear_console` | Clear the Unity console |
| `unity_take_screenshot` | Capture Scene/Game view (`returnBase64=false` to save tokens) |

### Editor Workflow (4)

| Tool | Description |
|------|-------------|
| `unity_execute_menu_item` | Execute an allowlisted menu item |
| `unity_run_tests` | Run Test Framework tests (EditMode/PlayMode) |
| `unity_undo` | Undo or redo operations |
| `unity_refresh_and_compile` | Refresh AssetDatabase + recompile scripts |

### GameObject (12)

| Tool | Description |
|------|-------------|
| `unity_list_gameobjects` | List hierarchy — use `outputMode='tree'` (90% fewer tokens) |
| `unity_create_gameobject` | Create a GameObject (optional primitive, components) |
| `unity_create_gameobject_batch` | Create multiple GameObjects in one call (single Undo) |
| `unity_delete_gameobject` | Delete a GameObject (finds inactive too) |
| `unity_rename_gameobject` | Rename a GameObject |
| `unity_set_parent` | Re-parent (`worldPositionStays` supported) |
| `unity_duplicate_gameobject` | Duplicate a GameObject |
| `unity_move_gameobject` | Change sibling index (NOT spatial — use set_transform) |
| `unity_set_transform` | Set position, rotation, scale (world or local) |
| `unity_get_gameobject` | Full details: transform, components, children |
| `unity_find_gameobjects_by_component` | Find all GameObjects with a component |
| `unity_set_gameobject_active` | Activate/deactivate a GameObject |

### Component (7)

| Tool | Description |
|------|-------------|
| `unity_get_component` | Get component properties via reflection |
| `unity_add_component` | Add a component with optional initial properties |
| `unity_modify_component_batch` | Modify components on multiple GameObjects (batch) |
| `unity_get_gameobject_components` | List all components on a GameObject |
| `unity_set_component_enabled` | Enable/disable a Behaviour component |
| `unity_list_project_scripts` | List all MonoBehaviour scripts in project |
| `unity_remove_component` | Remove a component (Transform protected) |

### Scene (5)

| Tool | Description |
|------|-------------|
| `unity_get_scene_info` | Get current scene info |
| `unity_list_scenes_in_project` | List all .unity scene files |
| `unity_load_scene` | Load a scene (Single or Additive) |
| `unity_save_scene` | Save current or all open scenes |
| `unity_create_scene` | Create a new scene (default or empty setup) |

### Script (5)

| Tool | Description |
|------|-------------|
| `unity_create_script` | Create C# from template (MonoBehaviour, SO, EditorWindow) |
| `unity_read_script` | Read .cs file content |
| `unity_get_script_info` | Get public API via reflection (fields, properties, methods) |
| `unity_write_script` | Write complete C# file (with backup) |
| `unity_update_script` | Find-and-replace in .cs file (with backup) |

### Memory Cache (3)

| Tool | Description |
|------|-------------|
| `unity_memory_get` | Get cached data (assets, scenes, hierarchy, operations, all) |
| `unity_memory_refresh` | Refresh a cache section |
| `unity_memory_clear` | Clear cache sections |

---

## Asset (16 tools) — `unity_enable_tool_category { category: "asset" }`

| Tool | Description |
|------|-------------|
| `unity_search_assets` | Search with filter syntax (`t:Texture`, `l:Label`, name) |
| `unity_get_asset_info` | Detailed metadata (GUID, type, size, dependencies) |
| `unity_delete_asset` | Delete an asset |
| `unity_create_folder` | Create a project folder |
| `unity_move_asset` | Move or rename an asset |
| `unity_copy_asset` | Copy an asset to a new path |
| `unity_list_folders` | List project folder structure |
| `unity_list_folder_contents` | List assets in folder with type filtering |
| `unity_get_asset_preview` | Get thumbnail (`size='small'` for fewer tokens) |
| `unity_get_import_settings` | Get import settings |
| `unity_set_import_settings` | Modify import settings |
| `unity_instantiate_prefab` | Instantiate a prefab in scene |
| `unity_create_prefab` | Create prefab from scene GameObject |
| `unity_unpack_prefab` | Unpack a prefab instance |
| `unity_apply_prefab_overrides` | Apply instance overrides to source |
| `unity_revert_prefab_overrides` | Revert instance to source values |

---

## Material (3 tools) — `unity_enable_tool_category { category: "material" }`

| Tool | Description |
|------|-------------|
| `unity_get_material` | Get material properties (from path or renderer) |
| `unity_set_material` | Modify properties — auto-maps URP/HDRP/Built-in |
| `unity_create_material` | Create material with pipeline-appropriate shader |

---

## UI (9 tools) — `unity_enable_tool_category { category: "ui" }`

| Tool | Description |
|------|-------------|
| `unity_create_canvas` | Create Canvas + EventSystem (Overlay, Camera, World) |
| `unity_create_ui_element` | Create element (Panel, Button, Text, Image, Slider, Toggle, InputField, Dropdown, ScrollView) |
| `unity_get_ui_hierarchy` | Inspect UI element tree |
| `unity_modify_ui_element` | Modify text, color, fontSize, interactable, value, sprite |
| `unity_set_rect_transform` | Configure anchors, pivot, size |
| `unity_add_layout_group` | Add layout (Vertical, Horizontal, Grid) |
| `unity_add_content_size_fitter` | Add ContentSizeFitter |
| `unity_add_layout_element` | Add LayoutElement |
| `unity_set_canvas_scaler` | Configure CanvasScaler |

---

## Animator (23 tools) — `unity_enable_tool_category { category: "animator" }`

| Tool | Description |
|------|-------------|
| `unity_get_animator_controller` | Get controller info (states, params, layers) |
| `unity_create_animator_controller` | Create new Animator Controller |
| `unity_get_animator_parameters` | List all parameters |
| `unity_set_animator_parameter` | Set parameter value at runtime |
| `unity_add_animator_parameter` | Add parameter (Float, Int, Bool, Trigger) |
| `unity_remove_animator_parameter` | Remove a parameter |
| `unity_add_animator_layer` | Add a layer |
| `unity_validate_animator` | Detect issues (unreachable states, missing clips) |
| `unity_get_animator_flow` | Get state machine flow diagram |
| `unity_add_animator_state` | Add a state to a layer |
| `unity_delete_animator_state` | Remove a state |
| `unity_modify_animator_state` | Edit state properties (speed, tag, motion) |
| `unity_set_default_state` | Set default state of a layer |
| `unity_create_blend_tree` | Create a Blend Tree state |
| `unity_add_blend_motion` | Add motion to a Blend Tree |
| `unity_add_animator_transition` | Add transition with conditions |
| `unity_delete_animator_transition` | Remove a transition |
| `unity_add_transition_condition` | Add condition to transition |
| `unity_remove_transition_condition` | Remove condition from transition |
| `unity_modify_transition` | Edit transition settings |
| `unity_list_animation_clips` | List animation clips in project |
| `unity_create_animation_clip` | Create a new animation clip |
| `unity_get_clip_info` | Get clip details (length, fps, events) |

---

## Terrain (17 tools) — `unity_enable_tool_category { category: "terrain" }`

| Tool | Description |
|------|-------------|
| `unity_create_terrain` | Create Terrain with TerrainData asset |
| `unity_get_terrain_info` | Get info (size, layers, trees, neighbors) |
| `unity_modify_terrain` | Modify settings (pixel error, distances) |
| `unity_set_terrain_heights_batch` | Sculpt: flatten, raise, lower, set, noise, smooth |
| `unity_list_terrain_brushes` | List available brushes |
| `unity_add_terrain_layer` | Add texture layer (diffuse, normal, tile, metallic) |
| `unity_paint_terrain_texture_batch` | Paint texture on region (alphamap) |
| `unity_paint_terrain_path` | Paint along waypoints (roads, rivers) |
| `unity_add_terrain_trees` | Place trees (explicit or random scatter) |
| `unity_remove_terrain_trees` | Remove trees from region |
| `unity_list_terrain_trees` | List tree prototypes |
| `unity_add_terrain_detail` | Add detail (grass, mesh) |
| `unity_paint_terrain_detail` | Paint detail density |
| `unity_remove_terrain_detail` | Remove detail from region |
| `unity_import_heightmap` | Import heightmap (PNG/RAW) |
| `unity_export_heightmap` | Export heightmap (PNG/RAW) |
| `unity_set_terrain_neighbors` | Set neighbors for seamless edges |

---

## Physics (8 tools) — `unity_enable_tool_category { category: "physics" }`

| Tool | Description |
|------|-------------|
| `unity_raycast` | Physics raycast — all hits sorted by distance |
| `unity_setup_rigidbody` | Add/configure Rigidbody (mass, drag, constraints) |
| `unity_setup_collider` | Add collider (Box, Sphere, Capsule, Mesh, auto) |
| `unity_set_physics_material` | Create and assign PhysicsMaterial |
| `unity_bake_navmesh` | Bake navigation mesh |
| `unity_clear_navmesh` | Clear NavMesh data |
| `unity_get_navmesh_settings` | Get agent types and area info |
| `unity_set_navigation_static` | Mark as Navigation Static |

---

## Audio (3 tools) — `unity_enable_tool_category { category: "audio" }`

| Tool | Description |
|------|-------------|
| `unity_setup_audio_source` | Add/configure AudioSource with clip and mixer |
| `unity_create_audio_mixer` | Create AudioMixer asset |
| `unity_get_audio_mixer` | Get mixer info (groups, exposed params) |

---

## Rendering (13 tools) — `unity_enable_tool_category { category: "rendering" }`

| Tool | Description |
|------|-------------|
| `unity_configure_camera` | Configure camera (FOV, near/far, background) |
| `unity_render_camera_to_file` | Render camera view to PNG/JPG |
| `unity_get_render_pipeline_info` | Get active pipeline (URP/HDRP/Built-in) |
| `unity_bake_lighting` | Bake lightmaps (synchronous) |
| `unity_bake_lighting_async` | Start async lightmap bake |
| `unity_get_bake_status` | Get current bake progress |
| `unity_cancel_bake` | Cancel active bake |
| `unity_clear_baked_data` | Clear all baked lighting data |
| `unity_get_lightmap_settings` | Get lightmap settings |
| `unity_set_lightmap_settings` | Modify lightmap settings |
| `unity_bake_occlusion` | Bake occlusion culling |
| `unity_clear_occlusion` | Clear occlusion data |
| `unity_bake_reflection_probes` | Bake reflection probes |

---

## Build (6 tools) — `unity_enable_tool_category { category: "build" }`

| Tool | Description |
|------|-------------|
| `unity_get_build_settings` | Get build config (target, scenes, backend) |
| `unity_manage_build_scenes` | Add/remove/enable/disable/reorder build scenes |
| `unity_switch_platform` | Switch target (Windows, Mac, Linux, iOS, Android, WebGL) |
| `unity_list_packages` | List installed Unity packages |
| `unity_add_package` | Add a package via UPM |
| `unity_remove_package` | Remove a package via UPM |

---

## Settings (11 tools) — `unity_enable_tool_category { category: "settings" }`

| Tool | Description |
|------|-------------|
| `unity_get_project_settings` | Read project settings |
| `unity_set_project_settings` | Modify project settings |
| `unity_set_quality_level` | Set quality preset |
| `unity_get_physics_layer_collision` | Get collision matrix |
| `unity_set_physics_layer_collision` | Set collision rules |
| `unity_list_tags` | List all tags |
| `unity_list_layers` | List all layers |
| `unity_set_tag` | Assign tag to GameObject |
| `unity_set_layer` | Assign layer to GameObject |
| `unity_create_tag` | Create a new tag |
| `unity_create_layer` | Create a new layer |

---

## Input (3 tools) — `unity_enable_tool_category { category: "input" }`

| Tool | Description |
|------|-------------|
| `unity_get_input_actions` | Get Input Action Asset contents |
| `unity_add_input_action` | Add a new Input Action |
| `unity_add_input_binding` | Add binding to an Input Action |

---

## Advanced (5 tools) — `unity_enable_tool_category { category: "advanced" }`

| Tool | Description |
|------|-------------|
| `unity_set_reference` | Set object reference on SerializedField |
| `unity_set_reference_array` | Set array of object references |
| `unity_create_scriptable_object` | Create ScriptableObject asset |
| `unity_list_scriptable_object_types` | List available SO types in project |
| `unity_modify_scriptable_object` | Modify ScriptableObject properties |

---

## Tool Count Verification

| Category | Count |
|----------|-------|
| Meta | 2 |
| Core | 45 |
| Asset | 16 |
| Material | 3 |
| UI | 9 |
| Animator | 23 |
| Terrain | 17 |
| Physics | 8 |
| Audio | 3 |
| Rendering | 13 |
| Build | 6 |
| Settings | 11 |
| Input | 3 |
| Advanced | 5 |
| **Total** | **164** |
