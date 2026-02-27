using System;
using System.Collections.Generic;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace McpUnity.Server
{
    /// <summary>
    /// NavMesh Tools - Bake and manage navigation meshes for AI pathfinding
    /// Contains 3 tools: BakeNavMesh, ClearNavMesh, GetNavMeshSettings
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all NavMesh-related tools
        /// </summary>
        static partial void RegisterNavMeshTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_bake_navmesh",
                description = "Bake the NavMesh for the current scene. This generates navigation data for AI agents to use for pathfinding. Make sure objects have Navigation Static enabled.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["agentTypeId"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Optional: Agent type ID to bake for (default: 0 = Humanoid). Use unity_get_navmesh_settings to see available agent types."
                        }
                    },
                    required = new List<string>()
                }
            }, BakeNavMesh);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_clear_navmesh",
                description = "Clear/remove all NavMesh data from the current scene.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, ClearNavMesh);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_navmesh_settings",
                description = "Get current NavMesh bake settings and available agent types.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetNavMeshSettings);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_navigation_static",
                description = "Set or unset GameObjects as Navigation Static for NavMesh baking. Objects must be static to be included in the NavMesh.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the GameObject (e.g., 'Floor', 'Environment/Ground')"
                        },
                        ["isStatic"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "True to mark as Navigation Static, false to unmark"
                        },
                        ["includeChildren"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Also apply to all children (default: true)"
                        }
                    },
                    required = new List<string> { "gameObjectPath", "isStatic" }
                }
            }, SetNavigationStatic);
        }

        #region NavMesh Tool Handlers

        private static McpToolResult BakeNavMesh(Dictionary<string, object> args)
        {
            try
            {
                int agentTypeId = ArgumentParser.GetInt(args, "agentTypeId", 0);

                // Check if NavMeshBuilder is available (Editor only)
#if UNITY_EDITOR
                // NavMeshBuilder.BuildNavMesh() is deprecated but has no replacement API in Unity 6.
                // The NavMeshSurface component (AI Navigation package) is the modern approach,
                // but it requires scene setup — this legacy API works universally without config.
#pragma warning disable CS0618
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618

                // Get info about what was baked
                var triangulation = NavMesh.CalculateTriangulation();
                int triangleCount = triangulation.indices.Length / 3;
                int vertexCount = triangulation.vertices.Length;

                return McpResponse.Success("NavMesh baked successfully", new
                {
                    agentTypeId = agentTypeId,
                    triangleCount = triangleCount,
                    vertexCount = vertexCount,
                    hasNavMesh = triangleCount > 0,
                    hint = triangleCount == 0
                        ? "No NavMesh generated. Ensure objects are marked as Navigation Static (use unity_set_navigation_static)"
                        : null
                });
#else
                return McpToolResult.Error("NavMesh baking is only available in the Unity Editor");
#endif
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to bake NavMesh: {ex.Message}");
            }
        }

        private static McpToolResult ClearNavMesh(Dictionary<string, object> args)
        {
            try
            {
#if UNITY_EDITOR
                // ClearAllNavMeshes() is deprecated but no replacement exists in Unity 6.
#pragma warning disable 0618
                UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
#pragma warning restore 0618

                return McpResponse.Success("NavMesh cleared successfully", new
                {
                    cleared = true
                });
#else
                return McpToolResult.Error("NavMesh clearing is only available in the Unity Editor");
#endif
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to clear NavMesh: {ex.Message}");
            }
        }

        private static McpToolResult GetNavMeshSettings(Dictionary<string, object> args)
        {
            try
            {
                // Get all agent types
                var agentTypes = new List<object>();
                int agentTypeCount = NavMesh.GetSettingsCount();

                for (int i = 0; i < agentTypeCount; i++)
                {
                    var settings = NavMesh.GetSettingsByIndex(i);
                    string agentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);

                    agentTypes.Add(new
                    {
                        id = settings.agentTypeID,
                        name = agentName,
                        agentRadius = settings.agentRadius,
                        agentHeight = settings.agentHeight,
                        agentSlope = settings.agentSlope,
                        agentClimb = settings.agentClimb
                    });
                }

                // Get current triangulation info
                var triangulation = NavMesh.CalculateTriangulation();
                bool hasNavMesh = triangulation.indices.Length > 0;

                // Get area names (cache array once to avoid repeated allocations)
                var areaNames = new List<object>();
                string[] allAreaNames = NavMesh.GetAreaNames();
                for (int i = 0; i < allAreaNames.Length; i++)
                {
                    areaNames.Add(new
                    {
                        index = i,
                        name = allAreaNames[i],
                        cost = NavMesh.GetAreaCost(i)
                    });
                }

                return McpResponse.Success(new
                {
                    hasNavMesh = hasNavMesh,
                    triangleCount = hasNavMesh ? triangulation.indices.Length / 3 : 0,
                    vertexCount = hasNavMesh ? triangulation.vertices.Length : 0,
                    agentTypes = agentTypes,
                    areas = areaNames,
                    hint = !hasNavMesh
                        ? "No NavMesh exists. Use unity_bake_navmesh to generate one."
                        : null
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get NavMesh settings: {ex.Message}");
            }
        }

        private static McpToolResult SetNavigationStatic(Dictionary<string, object> args)
        {
            try
            {
                var (go, gameObjectPath, goErr) = RequireGameObject(args);
                if (goErr != null) return goErr;

                // isStatic is required - check if it exists
                if (!ArgumentParser.HasKey(args, "isStatic"))
                    return McpToolResult.Error("Required parameter 'isStatic' is missing");

                bool isStatic = ArgumentParser.GetBool(args, "isStatic", false);
                bool includeChildren = ArgumentParser.GetBool(args, "includeChildren", true);

                int modifiedCount = 0;

                // Set navigation static flag
                void SetStaticFlag(GameObject obj)
                {
                    Undo.RecordObject(obj, "Set Navigation Static");

                    var flags = GameObjectUtility.GetStaticEditorFlags(obj);
                    // NavigationStatic is deprecated in Unity 6 but remains the only way to mark
                    // objects for legacy NavMesh baking without the AI Navigation package.
#pragma warning disable 0618
                    if (isStatic)
                        flags |= StaticEditorFlags.NavigationStatic;
                    else
                        flags &= ~StaticEditorFlags.NavigationStatic;
#pragma warning restore 0618
                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                    EditorUtility.SetDirty(obj);
                    modifiedCount++;
                }

                // Apply to the target
                SetStaticFlag(go);

                // Apply to children if requested
                if (includeChildren)
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.gameObject != go)
                        {
                            SetStaticFlag(child.gameObject);
                        }
                    }
                }

                return McpResponse.Success($"Set Navigation Static = {isStatic} on {modifiedCount} object(s)", new
                {
                    gameObject = gameObjectPath,
                    isNavigationStatic = isStatic,
                    modifiedCount = modifiedCount,
                    includeChildren = includeChildren
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set navigation static: {ex.Message}");
            }
        }

        #endregion
    }
}
