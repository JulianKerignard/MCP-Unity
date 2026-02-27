using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using McpUnity.Utils;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Prefab management tools for MCP Unity Server.
    /// Contains 4 tools: InstantiatePrefab, CreatePrefab, UnpackPrefab, ApplyPrefabOverrides
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all prefab-related tools
        /// </summary>
        static partial void RegisterPrefabTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_instantiate_prefab",
                description = "Instantiate a prefab in the scene with optional position, rotation, and parent",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["prefabPath"] = new McpPropertySchema { type = "string", description = "Path to the prefab asset (e.g., 'Assets/Prefabs/Player.prefab')" },
                        ["position"] = new McpPropertySchema { type = "object", description = "Position as {x, y, z}" },
                        ["rotation"] = new McpPropertySchema { type = "object", description = "Euler rotation as {x, y, z}" },
                        ["parentPath"] = new McpPropertySchema { type = "string", description = "Optional: Path to the parent GameObject" },
                        ["name"] = new McpPropertySchema { type = "string", description = "Optional: Override the instance name" }
                    },
                    required = new List<string> { "prefabPath" }
                }
            }, InstantiatePrefab);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_prefab",
                description = "Create a prefab from an existing GameObject in the scene",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the source GameObject in the hierarchy" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Path to save the prefab (must end with .prefab)" },
                        ["connectInstance"] = new McpPropertySchema { type = "boolean", description = "If true (default), keep the scene instance connected to the prefab" }
                    },
                    required = new List<string> { "gameObjectPath", "savePath" }
                }
            }, CreatePrefab);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_unpack_prefab",
                description = "Unpack a prefab instance, breaking the link to the source prefab",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the prefab instance in the hierarchy" },
                        ["unpackMode"] = new McpPropertySchema { type = "string", description = "Unpack mode: 'completely' (default) or 'root'" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, UnpackPrefab);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_apply_prefab_overrides",
                description = "Apply all overrides from a prefab instance back to the source prefab",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the prefab instance in the hierarchy" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, ApplyPrefabOverrides);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_revert_prefab_overrides",
                description = "Revert all overrides on a prefab instance back to the source prefab values",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the prefab instance in the hierarchy" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, RevertPrefabOverrides);
        }

        #region Prefab Handlers

        private static McpToolResult InstantiatePrefab(Dictionary<string, object> args)
        {
            var (rawPath, prefabPathErr) = RequireArg(args, "prefabPath");
            if (prefabPathErr != null) return prefabPathErr;

            var (prefabPath, sanitizeErr) = TrySanitizePath(rawPath, "prefab path");
            if (sanitizeErr != null) return sanitizeErr;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                return McpToolResult.Error($"Prefab not found at path: {prefabPath}");
            }

            // Check if it's actually a prefab
            if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                return McpToolResult.Error($"Asset is not a prefab: {prefabPath}");
            }

            // Instantiate with prefab link
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            if (instance == null)
            {
                return McpToolResult.Error("Failed to instantiate prefab");
            }

            // Set position
            if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "position", out var posDict))
            {
                instance.transform.position = TypeConverter.ParseVector3(posDict);
            }

            // Set rotation
            if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "rotation", out var rotDict))
            {
                instance.transform.rotation = Quaternion.Euler(TypeConverter.ParseVector3(rotDict));
            }

            // Set parent if specified
            string parentPath = ArgumentParser.GetString(args, "parentPath", null);
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObjectHelpers.FindGameObject(parentPath);
                if (parent == null)
                {
                    // Clean up the instantiated prefab before returning error
                    UnityEngine.Object.DestroyImmediate(instance);
                    return McpToolResult.Error($"Parent GameObject not found: '{parentPath}'. Use unity_list_gameobjects to verify the path.");
                }
                instance.transform.SetParent(parent.transform, true);
            }

            // Override name if specified
            string nameOverride = ArgumentParser.GetString(args, "name", null);
            if (!string.IsNullOrEmpty(nameOverride))
            {
                instance.name = nameOverride;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

            return McpResponse.Success($"Instantiated prefab '{instance.name}'", new
            {
                instanceName = instance.name,
                instancePath = GameObjectHelpers.GetGameObjectPath(instance),
                prefabPath = prefabPath,
                position = new { x = instance.transform.position.x, y = instance.transform.position.y, z = instance.transform.position.z },
                rotation = new { x = instance.transform.eulerAngles.x, y = instance.transform.eulerAngles.y, z = instance.transform.eulerAngles.z }
            });
        }

        private static McpToolResult CreatePrefab(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            var (rawSavePath, savePathErr) = RequireArg(args, "savePath");
            if (savePathErr != null) return savePathErr;

            var (savePath, sanitizeErr) = TrySanitizePath(rawSavePath, "save path");
            if (sanitizeErr != null) return sanitizeErr;

            if (!savePath.EndsWith(".prefab"))
            {
                return McpToolResult.Error("Save path must end with .prefab");
            }

            bool connectInstance = ArgumentParser.GetBool(args, "connectInstance", true);

            try
            {
                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                GameObject prefab;
                if (connectInstance)
                {
                    prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, savePath, InteractionMode.UserAction);
                }
                else
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
                }

                if (prefab == null)
                {
                    return McpToolResult.Error("Failed to create prefab");
                }

                AssetDatabase.Refresh();

                return McpResponse.Success(new
                {
                    success = true,
                    prefabPath = savePath,
                    prefabName = prefab.name,
                    sourceGameObject = gameObjectPath,
                    isConnected = connectInstance
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create prefab: {ex.Message}");
            }
        }

        private static McpToolResult UnpackPrefab(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            // Check if it's a prefab instance
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status == PrefabInstanceStatus.NotAPrefab)
            {
                return McpToolResult.Error($"GameObject is not a prefab instance: {gameObjectPath}");
            }

            // Parse unpack mode (default: completely)
            string modeStr = ArgumentParser.GetString(args, "unpackMode", "completely");
            PrefabUnpackMode unpackMode = modeStr.Equals("root", StringComparison.OrdinalIgnoreCase)
                ? PrefabUnpackMode.OutermostRoot
                : PrefabUnpackMode.Completely;

            try
            {
                // Get prefab path before unpacking for return value
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string prefabPath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : null;

                // Unpack with Undo support
                Undo.RegisterFullObjectHierarchyUndo(go, "Unpack Prefab");
                PrefabUtility.UnpackPrefabInstance(go, unpackMode, InteractionMode.UserAction);

                return McpResponse.Success(new
                {
                    success = true,
                    gameObjectPath = gameObjectPath,
                    previousPrefab = prefabPath,
                    unpackMode = unpackMode.ToString(),
                    message = "Prefab instance unpacked successfully"
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to unpack prefab: {ex.Message}");
            }
        }

        private static McpToolResult ApplyPrefabOverrides(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            // Check if it's a connected prefab instance
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status == PrefabInstanceStatus.NotAPrefab)
            {
                return McpToolResult.Error($"GameObject is not a prefab instance: {gameObjectPath}");
            }

            try
            {
                // Get source prefab path for return value
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string prefabPath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : null;

                // Apply overrides to source prefab
                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);

                return McpResponse.Success(new
                {
                    success = true,
                    instancePath = gameObjectPath,
                    prefabPath = prefabPath,
                    message = "All overrides applied to source prefab"
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to apply prefab overrides: {ex.Message}");
            }
        }

        private static McpToolResult RevertPrefabOverrides(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status == PrefabInstanceStatus.NotAPrefab)
                return McpToolResult.Error($"GameObject is not a prefab instance: {gameObjectPath}");

            try
            {
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string prefabPath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : null;

                PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);

                return McpResponse.Success(new
                {
                    success = true,
                    instancePath = gameObjectPath,
                    prefabPath = prefabPath,
                    message = "All overrides reverted to source prefab values"
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to revert prefab overrides: {ex.Message}");
            }
        }

        #endregion
    }
}
