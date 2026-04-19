# MCP Unity — Cache Invalidation Map

Source: `Server~/src/cache.ts`

## Cache Configuration

- **Max entries**: 500
- **Cleanup interval**: 60 seconds (removes expired)
- **Eviction**: When full, removes oldest entry after cleanup

## TTL by Category

| Category | TTL | Rationale |
|----------|-----|-----------|
| `editorState` | 5s | Changes very often (play mode, selection) |
| `hierarchy` | 30s | Changes frequently (object creation/deletion) |
| `components` | 1 min | Moderately stable (property changes) |
| `assets` | 5 min | Rarely changes (files on disk) |
| `scenes` | 5 min | Rarely changes (scene metadata) |

## Cacheable Tools (19 tools)

| Tool | Category |
|------|----------|
| `unity_get_editor_state` | editorState |
| `unity_get_project_overview` | scenes |
| `unity_get_gameobject` | hierarchy |
| `unity_get_gameobject_components` | components |
| `unity_list_gameobjects` | hierarchy |
| `unity_list_scenes_in_project` | scenes |
| `unity_get_component` | components |
| `unity_get_material` | components |
| `unity_get_asset_info` | assets |
| `unity_search_assets` | assets |
| `unity_get_scene_info` | scenes |
| `unity_list_folders` | assets |
| `unity_list_folder_contents` | assets |
| `unity_memory_get` | components |
| `unity_get_audio_mixer` | assets |
| `unity_read_script` | assets |
| `unity_get_script_info` | components |
| `unity_get_build_settings` | scenes |
| `unity_get_render_pipeline_info` | scenes |

## Cache Invalidators (89 tools)

### GameObject tools → `hierarchy`

| Tool | Invalidates |
|------|-------------|
| `unity_create_gameobject` | hierarchy |
| `unity_create_gameobject_batch` | hierarchy |
| `unity_delete_gameobject` | hierarchy |
| `unity_duplicate_gameobject` | hierarchy |
| `unity_move_gameobject` | hierarchy |
| `unity_set_gameobject_active` | hierarchy |
| `unity_set_parent` | hierarchy |
| `unity_rename_gameobject` | hierarchy |
| `unity_set_transform` | hierarchy |

### Component tools → `hierarchy` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_add_component` | hierarchy, components |
| `unity_modify_component_batch` | hierarchy, components |
| `unity_remove_component` | hierarchy, components |
| `unity_set_component_enabled` | components, hierarchy |

### Scene tools → `hierarchy` + `scenes`

| Tool | Invalidates |
|------|-------------|
| `unity_load_scene` | hierarchy, scenes |
| `unity_save_scene` | scenes |
| `unity_create_scene` | hierarchy, scenes |

### Prefab tools → `assets` + `hierarchy`

| Tool | Invalidates |
|------|-------------|
| `unity_instantiate_prefab` | hierarchy |
| `unity_create_prefab` | assets, hierarchy |
| `unity_unpack_prefab` | hierarchy |
| `unity_apply_prefab_overrides` | assets, hierarchy |
| `unity_revert_prefab_overrides` | assets, hierarchy |

### Script tools → `assets`

| Tool | Invalidates |
|------|-------------|
| `unity_create_script` | assets |
| `unity_write_script` | assets |
| `unity_update_script` | assets |

### Asset tools → `assets`

| Tool | Invalidates |
|------|-------------|
| `unity_set_import_settings` | assets |
| `unity_create_folder` | assets |
| `unity_delete_asset` | assets, hierarchy |
| `unity_move_asset` | assets |
| `unity_copy_asset` | assets |

### Material tools → `assets` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_create_material` | assets |
| `unity_set_material` | assets, components |

### UI tools → `hierarchy` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_create_canvas` | hierarchy |
| `unity_create_ui_element` | hierarchy |
| `unity_set_rect_transform` | hierarchy, components |
| `unity_add_layout_group` | hierarchy, components |
| `unity_set_canvas_scaler` | components |
| `unity_modify_ui_element` | hierarchy, components |
| `unity_add_content_size_fitter` | hierarchy, components |
| `unity_add_layout_element` | hierarchy, components |

### Animator tools (23) → `assets` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_create_animator_controller` | assets |
| `unity_set_animator_parameter` | components |
| `unity_add_animator_parameter` | components |
| `unity_remove_animator_parameter` | components |
| `unity_add_animator_layer` | components |
| `unity_add_animator_state` | components |
| `unity_delete_animator_state` | components |
| `unity_modify_animator_state` | components |
| `unity_set_default_state` | components |
| `unity_create_blend_tree` | components |
| `unity_add_blend_motion` | components |
| `unity_add_animator_transition` | components |
| `unity_delete_animator_transition` | components |
| `unity_modify_transition` | components |
| `unity_add_transition_condition` | components |
| `unity_remove_transition_condition` | components |
| `unity_create_animation_clip` | assets |

### Terrain tools → `hierarchy`

| Tool | Invalidates |
|------|-------------|
| `unity_create_terrain` | hierarchy |
| `unity_modify_terrain` | hierarchy |
| `unity_set_terrain_heights_batch` | hierarchy |
| `unity_import_heightmap` | hierarchy |
| `unity_set_terrain_neighbors` | hierarchy |
| `unity_add_terrain_layer` | hierarchy |
| `unity_paint_terrain_texture_batch` | hierarchy |
| `unity_paint_terrain_path` | hierarchy |
| `unity_add_terrain_trees` | hierarchy |
| `unity_remove_terrain_trees` | hierarchy |
| `unity_add_terrain_detail` | hierarchy |
| `unity_paint_terrain_detail` | hierarchy |
| `unity_remove_terrain_detail` | hierarchy |

### Physics tools → `hierarchy` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_setup_rigidbody` | hierarchy, components |
| `unity_setup_collider` | hierarchy, components |
| `unity_set_physics_material` | components |

### Audio tools → `hierarchy` + `assets`

| Tool | Invalidates |
|------|-------------|
| `unity_setup_audio_source` | hierarchy, components |
| `unity_create_audio_mixer` | assets |

### Rendering tools → `assets` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_configure_camera` | components |
| `unity_render_camera_to_file` | assets |

### Baking tools → `assets`

| Tool | Invalidates |
|------|-------------|
| `unity_bake_navmesh` | assets |
| `unity_clear_navmesh` | assets |
| `unity_set_navigation_static` | hierarchy |
| `unity_bake_lighting` | assets |
| `unity_bake_lighting_async` | assets |
| `unity_clear_baked_data` | assets |
| `unity_set_lightmap_settings` | assets |
| `unity_bake_reflection_probes` | assets |
| `unity_bake_occlusion` | assets |
| `unity_clear_occlusion` | assets |

### Build tools → `scenes` + `assets`

| Tool | Invalidates |
|------|-------------|
| `unity_manage_build_scenes` | scenes |
| `unity_switch_platform` | scenes |
| `unity_add_package` | assets |
| `unity_remove_package` | assets |

### Settings tools → `scenes` + `hierarchy` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_set_tag` | hierarchy |
| `unity_set_layer` | hierarchy |
| `unity_create_tag` | hierarchy |
| `unity_create_layer` | hierarchy |
| `unity_set_project_settings` | scenes |
| `unity_set_quality_level` | scenes |
| `unity_set_physics_layer_collision` | components |

### Input tools → `components`

| Tool | Invalidates |
|------|-------------|
| `unity_add_input_action` | components |
| `unity_add_input_binding` | components |

### Advanced tools → `assets` + `components`

| Tool | Invalidates |
|------|-------------|
| `unity_set_reference` | components |
| `unity_set_reference_array` | components |
| `unity_create_scriptable_object` | assets |
| `unity_modify_scriptable_object` | assets |

### Memory tools → `components`

| Tool | Invalidates |
|------|-------------|
| `unity_memory_refresh` | components |
| `unity_memory_clear` | components |

### Editor tools → multiple categories

| Tool | Invalidates |
|------|-------------|
| `unity_set_selection` | editorState |
| `unity_focus_gameobject` | editorState |
| `unity_clear_console` | editorState |
| `unity_execute_menu_item` | hierarchy, components, scenes |
| `unity_undo` | hierarchy, components, assets, scenes |
| `unity_refresh_and_compile` | hierarchy, components, assets, scenes, editorState (ALL) |
