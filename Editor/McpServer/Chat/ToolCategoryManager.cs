using System.Collections.Generic;
using UnityEditor;

namespace McpUnity.Chat
{
    /// <summary>
    /// Manages which MCP tool categories are enabled/disabled for the LLM chat.
    /// Categories group all 137 MCP tools into user-friendly buckets.
    /// Enable/disable state is persisted per-machine via EditorPrefs.
    /// </summary>
    public static class ToolCategoryManager
    {
        public struct ToolCategory
        {
            public string id;
            public string displayName;
            public string[] toolNames;

            public int Count => toolNames != null ? toolNames.Length : 0;
        }

        private const string PrefPrefix = "McpUnity_ToolCat_";
        private const string InitializedPref = "McpUnity_ToolCatInitialized";

        // Categories whose default state is enabled under the "core" preset
        private static readonly HashSet<string> CoreCategoryIds = new HashSet<string>
        {
            "gameobject", "scene", "assets", "editor", "scripts", "terrain"
        };

        // Preset definitions: preset name → set of category ids to enable (core always added)
        private static readonly Dictionary<string, HashSet<string>> PresetCategoryIds =
            new Dictionary<string, HashSet<string>>
            {
                {
                    "scene-only", new HashSet<string> { "gameobject", "scene", "editor" }
                },
                {
                    "animation-only", new HashSet<string> { "animator-read" }
                },
                {
                    "level-design", new HashSet<string> { "gameobject", "scene", "terrain", "assets" }
                },
            };

        // Lookup: tool name → category id (built once at static init)
        private static readonly Dictionary<string, string> ToolToCategoryMap;

        // Cached counts — invalidated when category state changes
        private static int _cachedEnabledCount = -1; // -1 = dirty
        private static readonly int _totalToolCount;  // immutable, computed at static init

        private static readonly ToolCategory[] _allCategories = new ToolCategory[]
        {
            // ----------------------------------------------------------------
            // GameObject & Components (11 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "gameobject",
                displayName = "GameObject & Components",
                toolNames = new[]
                {
                    "unity_list_gameobjects",
                    "unity_find_gameobjects_by_component",
                    "unity_create_gameobject",
                    "unity_delete_gameobject",
                    "unity_rename_gameobject",
                    "unity_duplicate_gameobject",
                    "unity_move_gameobject",
                    "unity_set_parent",
                    "unity_get_component",
                    "unity_add_component",
                    "unity_modify_component",
                    "unity_set_reference",
                    "unity_set_reference_array",
                    "unity_list_project_scripts",
                }
            },
            // ----------------------------------------------------------------
            // Scene & Prefab (8 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "scene",
                displayName = "Scene & Prefab",
                toolNames = new[]
                {
                    "unity_get_scene_info",
                    "unity_load_scene",
                    "unity_save_scene",
                    "unity_create_scene",
                    "unity_instantiate_prefab",
                    "unity_create_prefab",
                    "unity_unpack_prefab",
                    "unity_apply_prefab_overrides",
                }
            },
            // ----------------------------------------------------------------
            // Assets & Materials (11 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "assets",
                displayName = "Assets & Materials",
                toolNames = new[]
                {
                    "unity_search_assets",
                    "unity_get_asset_info",
                    "unity_list_folders",
                    "unity_list_folder_contents",
                    "unity_get_asset_preview",
                    "unity_get_material",
                    "unity_set_material",
                    "unity_create_material",
                    "unity_create_scriptable_object",
                    "unity_list_scriptable_object_types",
                    "unity_modify_scriptable_object",
                }
            },
            // ----------------------------------------------------------------
            // Animator — Read (4 tools, read-only inspection)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "animator-read",
                displayName = "Animator (Read)",
                toolNames = new[]
                {
                    "unity_get_animator_controller",
                    "unity_get_animator_parameters",
                    "unity_list_animation_clips",
                    "unity_get_animator_flow",
                }
            },
            // ----------------------------------------------------------------
            // Animator — Edit (12 tools, mutations)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "animator-edit",
                displayName = "Animator (Edit)",
                toolNames = new[]
                {
                    "unity_set_animator_parameter",
                    "unity_add_animator_parameter",
                    "unity_add_animator_state",
                    "unity_delete_animator_state",
                    "unity_modify_animator_state",
                    "unity_create_blend_tree",
                    "unity_add_blend_motion",
                    "unity_add_animator_transition",
                    "unity_delete_animator_transition",
                    "unity_modify_transition",
                    "unity_get_clip_info",
                    "unity_validate_animator",
                }
            },
            // ----------------------------------------------------------------
            // UI (9 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "ui",
                displayName = "UI (uGUI)",
                toolNames = new[]
                {
                    "unity_create_canvas",
                    "unity_create_ui_element",
                    "unity_get_ui_hierarchy",
                    "unity_modify_ui_element",
                    "unity_set_rect_transform",
                    "unity_add_layout_group",
                    "unity_add_content_size_fitter",
                    "unity_add_layout_element",
                    "unity_set_canvas_scaler",
                }
            },
            // ----------------------------------------------------------------
            // Editor (10 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "editor",
                displayName = "Editor",
                toolNames = new[]
                {
                    "unity_get_editor_state",
                    "unity_get_selection",
                    "unity_set_selection",
                    "unity_get_console_logs",
                    "unity_clear_console",
                    "unity_execute_menu_item",
                    "unity_run_tests",
                    "unity_undo",
                    "unity_refresh_and_compile",
                    "unity_take_screenshot",
                }
            },
            // ----------------------------------------------------------------
            // Scripts (5 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "scripts",
                displayName = "Scripts",
                toolNames = new[]
                {
                    "unity_create_script",
                    "unity_read_script",
                    "unity_get_script_info",
                    "unity_write_script",
                    "unity_update_script",
                }
            },
            // ----------------------------------------------------------------
            // Physics & Navigation (8 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "physics",
                displayName = "Physics & Navigation",
                toolNames = new[]
                {
                    "unity_raycast",
                    "unity_setup_rigidbody",
                    "unity_setup_collider",
                    "unity_set_physics_material",
                    "unity_bake_navmesh",
                    "unity_clear_navmesh",
                    "unity_get_navmesh_settings",
                    "unity_set_navigation_static",
                }
            },
            // ----------------------------------------------------------------
            // Lighting & Rendering (13 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "lighting",
                displayName = "Lighting & Rendering",
                toolNames = new[]
                {
                    "unity_bake_lighting",
                    "unity_bake_lighting_async",
                    "unity_get_bake_status",
                    "unity_cancel_bake",
                    "unity_clear_baked_data",
                    "unity_get_lightmap_settings",
                    "unity_set_lightmap_settings",
                    "unity_bake_occlusion",
                    "unity_clear_occlusion",
                    "unity_bake_reflection_probes",
                    "unity_configure_camera",
                    "unity_render_camera_to_file",
                    "unity_get_render_pipeline_info",
                }
            },
            // ----------------------------------------------------------------
            // Terrain (15 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "terrain",
                displayName = "Terrain (Beta)",
                toolNames = new[]
                {
                    // Core (4)
                    "unity_create_terrain",
                    "unity_get_terrain_info",
                    "unity_modify_terrain",
                    "unity_set_terrain_heights",
                    // Paint (3)
                    "unity_add_terrain_layer",
                    "unity_paint_terrain_texture",
                    "unity_add_terrain_trees",
                    // Detail (3)
                    "unity_add_terrain_detail",
                    "unity_paint_terrain_detail",
                    "unity_remove_terrain_detail",
                    // Advanced (5)
                    "unity_import_heightmap",
                    "unity_export_heightmap",
                    "unity_set_terrain_neighbors",
                    "unity_remove_terrain_trees",
                    "unity_list_terrain_trees",
                    // Brushes (1)
                    "unity_list_terrain_brushes",
                }
            },
            // ----------------------------------------------------------------
            // Tags & Layers (6 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "tags-layers",
                displayName = "Tags & Layers",
                toolNames = new[]
                {
                    "unity_list_tags",
                    "unity_list_layers",
                    "unity_set_tag",
                    "unity_set_layer",
                    "unity_create_tag",
                    "unity_create_layer",
                }
            },
            // ----------------------------------------------------------------
            // Project Settings (5 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "project-settings",
                displayName = "Project Settings",
                toolNames = new[]
                {
                    "unity_get_project_settings",
                    "unity_set_project_settings",
                    "unity_set_quality_level",
                    "unity_get_physics_layer_collision",
                    "unity_set_physics_layer_collision",
                }
            },
            // ----------------------------------------------------------------
            // Build (3 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "build",
                displayName = "Build",
                toolNames = new[]
                {
                    "unity_get_build_settings",
                    "unity_manage_build_scenes",
                    "unity_switch_platform",
                }
            },
            // ----------------------------------------------------------------
            // Audio (3 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "audio",
                displayName = "Audio",
                toolNames = new[]
                {
                    "unity_setup_audio_source",
                    "unity_create_audio_mixer",
                    "unity_get_audio_mixer",
                }
            },
            // ----------------------------------------------------------------
            // Asset Import (3 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "import",
                displayName = "Asset Import",
                toolNames = new[]
                {
                    "unity_get_import_settings",
                    "unity_set_import_settings",
                    // (unity_add_package / unity_remove_package / unity_list_packages go below)
                }
            },
            // ----------------------------------------------------------------
            // Package Manager (3 tools)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "packages",
                displayName = "Package Manager",
                toolNames = new[]
                {
                    "unity_list_packages",
                    "unity_add_package",
                    "unity_remove_package",
                }
            },
            // ----------------------------------------------------------------
            // Input System (3 tools — optional, requires Input System package)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "input",
                displayName = "Input System",
                toolNames = new[]
                {
                    "unity_get_input_actions",
                    "unity_add_input_action",
                    "unity_add_input_binding",
                }
            },
            // ----------------------------------------------------------------
            // Placement / level design (6 tools — raycast-based placement,
            // grid snap, scatter, prefab swap, 3D array)
            // ----------------------------------------------------------------
            new ToolCategory
            {
                id = "placement",
                displayName = "Placement",
                toolNames = new[]
                {
                    "unity_raycast_place",
                    "unity_align_to_surface",
                    "unity_scatter_on_surface",
                    "unity_snap_to_grid",
                    "unity_replace_with_prefab",
                    "unity_array_3d",
                }
            },
        };

        static ToolCategoryManager()
        {
            // Build tool → category lookup + compute immutable total
            ToolToCategoryMap = new Dictionary<string, string>();
            int total = 0;
            for (int i = 0; i < _allCategories.Length; i++)
            {
                var cat = _allCategories[i];
                if (cat.toolNames == null) continue;
                total += cat.toolNames.Length;
                for (int j = 0; j < cat.toolNames.Length; j++)
                {
                    ToolToCategoryMap[cat.toolNames[j]] = cat.id;
                }
            }
            _totalToolCount = total;

            // Migration v2: split animator and config categories
            if (!EditorPrefs.GetBool("McpUnity_ToolCatMigration_v2", false))
            {
                if (EditorPrefs.HasKey("McpUnity_ToolCat_animator"))
                {
                    bool oldVal = EditorPrefs.GetBool("McpUnity_ToolCat_animator", false);
                    EditorPrefs.SetBool("McpUnity_ToolCat_animator-read", oldVal);
                    EditorPrefs.SetBool("McpUnity_ToolCat_animator-edit", oldVal);
                    EditorPrefs.DeleteKey("McpUnity_ToolCat_animator");
                }
                if (EditorPrefs.HasKey("McpUnity_ToolCat_config"))
                {
                    bool oldVal = EditorPrefs.GetBool("McpUnity_ToolCat_config", false);
                    EditorPrefs.SetBool("McpUnity_ToolCat_tags-layers", oldVal);
                    EditorPrefs.SetBool("McpUnity_ToolCat_project-settings", oldVal);
                    EditorPrefs.SetBool("McpUnity_ToolCat_build", oldVal);
                    EditorPrefs.SetBool("McpUnity_ToolCat_audio", oldVal);
                    EditorPrefs.DeleteKey("McpUnity_ToolCat_config");
                }
                EditorPrefs.SetBool("McpUnity_ToolCatMigration_v2", true);
            }

            // First-run: apply "core" preset defaults
            if (!EditorPrefs.GetBool(InitializedPref, false))
            {
                ApplyPresetValues("core");
                EditorPrefs.SetBool(InitializedPref, true);
            }
        }

        /// <summary>All category definitions.</summary>
        public static ToolCategory[] AllCategories => _allCategories;

        /// <summary>Whether a specific category is enabled.</summary>
        public static bool IsCategoryEnabled(string categoryId)
        {
            return EditorPrefs.GetBool(PrefPrefix + categoryId, CoreCategoryIds.Contains(categoryId));
        }

        /// <summary>Enable or disable a specific category.</summary>
        public static void SetCategoryEnabled(string categoryId, bool enabled)
        {
            EditorPrefs.SetBool(PrefPrefix + categoryId, enabled);
            _cachedEnabledCount = -1; // Invalidate cache
        }

        /// <summary>Whether a specific tool is enabled (checks its parent category).</summary>
        public static bool IsToolEnabled(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return false;
            if (!ToolToCategoryMap.TryGetValue(toolName, out var categoryId)) return false;
            return IsCategoryEnabled(categoryId);
        }

        /// <summary>Count of tools whose category is currently enabled. Cached, O(1) after first call.</summary>
        public static int EnabledToolCount
        {
            get
            {
                if (_cachedEnabledCount < 0)
                {
                    int count = 0;
                    for (int i = 0; i < _allCategories.Length; i++)
                    {
                        if (IsCategoryEnabled(_allCategories[i].id))
                            count += _allCategories[i].Count;
                    }
                    _cachedEnabledCount = count;
                }
                return _cachedEnabledCount;
            }
        }

        /// <summary>Total number of tools across all categories. Computed once at static init, O(1).</summary>
        public static int TotalToolCount => _totalToolCount;

        /// <summary>Apply a named preset: "all", "none", or "core".</summary>
        public static void SetPreset(string preset) => ApplyPresetValues(preset);

        /// <summary>Enable all categories.</summary>
        public static void EnableAll() => ApplyPresetValues("all");

        /// <summary>Disable all categories.</summary>
        public static void DisableAll() => ApplyPresetValues("none");

        private static void ApplyPresetValues(string preset)
        {
            // Resolve the explicit enable set for named presets
            HashSet<string> enabledIds = null;
            if (PresetCategoryIds.TryGetValue(preset, out var presetSet))
            {
                // Named preset: union of preset-specific ids + always-on core ids
                enabledIds = new HashSet<string>(presetSet);
                enabledIds.UnionWith(CoreCategoryIds);
            }

            for (int i = 0; i < _allCategories.Length; i++)
            {
                var id = _allCategories[i].id;
                bool enabled;
                switch (preset)
                {
                    case "all":  enabled = true; break;
                    case "none": enabled = false; break;
                    case "core": enabled = CoreCategoryIds.Contains(id); break;
                    default:     enabled = enabledIds != null && enabledIds.Contains(id); break;
                }
                EditorPrefs.SetBool(PrefPrefix + id, enabled);
            }
            _cachedEnabledCount = -1; // Invalidate cache after bulk change
        }
    }
}
