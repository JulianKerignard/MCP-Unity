using System;
using System.Collections.Generic;
using System.Linq;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Reference Tools - Set object references and arrays on SerializedFields
    /// Contains 2 tools: SetReference, SetReferenceArray
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all reference-related tools
        /// </summary>
        static partial void RegisterReferenceTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_reference",
                description = "Set an object reference on a SerializedField (e.g., assign a prefab to WaveManager.zombiePrefab, or link a Transform to a field). Works with GameObjects in scene, prefabs, and other assets.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the GameObject that has the component (e.g., 'WaveManager', 'GameManager/Systems')"
                        },
                        ["componentType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Name of the component/script (e.g., 'WaveManager', 'InteractionSystem')"
                        },
                        ["fieldName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Name of the serialized field (e.g., 'zombiePrefab', 'cameraTransform', 'pointsText')"
                        },
                        ["targetPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the target object. For scene objects: 'Player', 'Main Camera'. For assets: 'Assets/Prefabs/Zombie.prefab'"
                        },
                        ["targetComponentType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional: If the field expects a specific component type (e.g., 'Transform', 'Text'), specify it here to get that component from the target GameObject"
                        }
                    },
                    required = new List<string> { "gameObjectPath", "componentType", "fieldName", "targetPath" }
                }
            }, SetReference);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_reference_array",
                description = "Set an array of object references on a SerializedField (e.g., assign spawn points to WaveManager.spawnPoints[], or weapons to a weapon pool array).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the GameObject that has the component"
                        },
                        ["componentType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Name of the component/script"
                        },
                        ["fieldName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Name of the array field (e.g., 'spawnPoints', 'weaponPrefabs')"
                        },
                        ["targetPaths"] = new McpPropertySchema
                        {
                            type = "array",
                            description = "Array of paths to target objects (e.g., ['SpawnPoint1', 'SpawnPoint2', 'SpawnPoint3'])"
                        },
                        ["targetComponentType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional: Component type to get from each target GameObject (e.g., 'Transform')"
                        }
                    },
                    required = new List<string> { "gameObjectPath", "componentType", "fieldName", "targetPaths" }
                }
            }, SetReferenceArray);
        }

        #region Reference Tool Handlers

        private static McpToolResult SetReference(Dictionary<string, object> args)
        {
            try
            {
                // Parse required parameters
                var (sourceGO, gameObjectPath, goErr) = RequireGameObject(args, "gameObjectPath");
                if (goErr != null) return goErr;

                var (componentType, typeErr) = RequireArg(args, "componentType");
                if (typeErr != null) return typeErr;

                var (fieldName, fieldErr) = RequireArg(args, "fieldName");
                if (fieldErr != null) return fieldErr;

                var (targetPath, targetErr) = RequireArg(args, "targetPath");
                if (targetErr != null) return targetErr;

                string targetComponentType = ArgumentParser.GetString(args, "targetComponentType", null);

                var component = FindComponentOnGameObject(sourceGO, componentType);
                if (component == null)
                    return McpToolResult.Error($"Component '{componentType}' not found on '{gameObjectPath}'. Available: {GetComponentList(sourceGO)}");

                // Create SerializedObject
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.FindProperty(fieldName);

                if (property == null)
                    return McpToolResult.Error($"Field '{fieldName}' not found on component '{componentType}'. Check spelling and ensure it's a serialized field.");

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    return McpToolResult.Error($"Field '{fieldName}' is not an object reference field (type: {property.propertyType})");

                // Find the target object
                var targetObject = ResolveTargetObject(targetPath, targetComponentType);
                if (targetObject == null)
                    return McpToolResult.Error($"Target not found: {targetPath}. Check if it's a scene object path (e.g., 'Player') or asset path (e.g., 'Assets/Prefabs/Zombie.prefab')");

                // Validate type compatibility
                var expectedType = GetExpectedType(property);
                if (expectedType != null && !expectedType.IsInstanceOfType(targetObject))
                {
                    return McpToolResult.Error($"Type mismatch: Field '{fieldName}' expects '{expectedType.Name}' but got '{targetObject.GetType().Name}'");
                }

                // Set the reference
                Undo.RecordObject(component, $"Set Reference {fieldName}");
                property.objectReferenceValue = targetObject;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);

                return McpResponse.Success($"Set {componentType}.{fieldName} to {targetObject.name}", new
                {
                    gameObject = gameObjectPath,
                    component = componentType,
                    field = fieldName,
                    target = targetObject.name,
                    targetType = targetObject.GetType().Name
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set reference: {ex.Message}");
            }
        }

        private static McpToolResult SetReferenceArray(Dictionary<string, object> args)
        {
            try
            {
                // Parse required parameters
                var (sourceGO, gameObjectPath, goErr) = RequireGameObject(args, "gameObjectPath");
                if (goErr != null) return goErr;

                var (componentType, typeErr) = RequireArg(args, "componentType");
                if (typeErr != null) return typeErr;

                var (fieldName, fieldErr) = RequireArg(args, "fieldName");
                if (fieldErr != null) return fieldErr;

                // Parse target paths array
                var targetPathsArray = ArgumentParser.GetStringArray(args, "targetPaths");
                if (targetPathsArray.Length == 0)
                    return McpToolResult.Error("Missing or empty required parameter: targetPaths");

                var targetPaths = new List<string>(targetPathsArray);

                string targetComponentType = ArgumentParser.GetString(args, "targetComponentType", null);

                var component = FindComponentOnGameObject(sourceGO, componentType);
                if (component == null)
                    return McpToolResult.Error($"Component '{componentType}' not found on '{gameObjectPath}'. Available: {GetComponentList(sourceGO)}");

                // Create SerializedObject
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.FindProperty(fieldName);

                if (property == null)
                    return McpToolResult.Error($"Field '{fieldName}' not found on component '{componentType}'");

                if (!property.isArray)
                    return McpToolResult.Error($"Field '{fieldName}' is not an array. Use unity_set_reference for single references.");

                // Resolve all target objects
                var targetObjects = new List<UnityEngine.Object>();
                var notFound = new List<string>();

                foreach (var path in targetPaths)
                {
                    var target = ResolveTargetObject(path, targetComponentType);
                    if (target != null)
                    {
                        targetObjects.Add(target);
                    }
                    else
                    {
                        notFound.Add(path);
                    }
                }

                if (notFound.Count > 0 && targetObjects.Count == 0)
                {
                    return McpToolResult.Error($"No targets found. Not found: {string.Join(", ", notFound)}");
                }

                // Set the array
                Undo.RecordObject(component, $"Set Reference Array {fieldName}");

                property.arraySize = targetObjects.Count;
                for (int i = 0; i < targetObjects.Count; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = targetObjects[i];
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);

                var result = new Dictionary<string, object>
                {
                    ["gameObject"] = gameObjectPath,
                    ["component"] = componentType,
                    ["field"] = fieldName,
                    ["count"] = targetObjects.Count,
                    ["assigned"] = targetObjects.Select(t => t.name).ToList()
                };

                if (notFound.Count > 0)
                {
                    result["notFound"] = notFound;
                    result["warning"] = $"{notFound.Count} target(s) not found";
                }

                return McpResponse.Success($"Set {componentType}.{fieldName} with {targetObjects.Count} elements", result);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set reference array: {ex.Message}");
            }
        }

        #endregion

        #region Reference Helpers

        /// <summary>
        /// Find a component on a GameObject by type name
        /// </summary>
        private static Component FindComponentOnGameObject(GameObject go, string componentTypeName)
        {
            // Try exact match first
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.Equals(componentTypeName, StringComparison.OrdinalIgnoreCase))
                    return comp;
            }

            // SEC-#436: prefix match only — substring match made "Box" hit "BoxCollider",
            // "SandboxManager", etc., silently picking the wrong component. Prefix matching
            // requires the user's input to at least start the component type name.
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.StartsWith(componentTypeName, StringComparison.OrdinalIgnoreCase))
                    return comp;
            }

            return null;
        }

        /// <summary>
        /// Get a comma-separated list of component names on a GameObject
        /// </summary>
        private static string GetComponentList(GameObject go)
        {
            var names = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .Where(n => n != "Transform")
                .ToList();

            return names.Count > 0 ? string.Join(", ", names) : "(none)";
        }

        /// <summary>
        /// Resolve a target path to a Unity Object
        /// </summary>
        private static UnityEngine.Object ResolveTargetObject(string path, string componentType = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            UnityEngine.Object result = null;

            // Check if it's an asset path (starts with Assets/)
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                result = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                // If looking for a specific component type from a prefab
                if (result is GameObject prefabGO && !string.IsNullOrEmpty(componentType))
                {
                    var comp = FindComponentOnGameObject(prefabGO, componentType);
                    if (comp != null)
                        result = comp;
                }

                return result;
            }

            // Try as scene GameObject path
            var go = GameObjectHelpers.FindGameObject(path);
            if (go != null)
            {
                // If a specific component type is requested, get that component
                if (!string.IsNullOrEmpty(componentType))
                {
                    var comp = FindComponentOnGameObject(go, componentType);
                    return comp != null ? (UnityEngine.Object)comp : go; // Fall back to GameObject if component not found
                }
                return go;
            }

            // Try as asset path without Assets/ prefix
            string assetPath = "Assets/" + path;
            result = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (result != null)
                return result;

            // Try finding by name in Assets
            string[] guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(path));
            foreach (var guid in guids)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                if (foundPath.EndsWith(path, StringComparison.OrdinalIgnoreCase) ||
                    System.IO.Path.GetFileName(foundPath).Equals(System.IO.Path.GetFileName(path), StringComparison.OrdinalIgnoreCase))
                {
                    result = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(foundPath);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the expected type for a SerializedProperty ObjectReference field
        /// </summary>
        private static Type GetExpectedType(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return null;

            // Try to get the type from the current value
            if (property.objectReferenceValue != null)
                return property.objectReferenceValue.GetType();

            // Try to extract from property path using reflection
            try
            {
                var targetObject = property.serializedObject.targetObject;
                var targetType = targetObject.GetType();
                var fieldInfo = targetType.GetField(property.name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (fieldInfo != null)
                    return fieldInfo.FieldType;
            }
            catch (Exception ex)
            {
                // SEC-#433: log ignored reflection errors so unexpected failures aren't silent.
                McpUnity.Editor.McpDebug.LogWarning($"[ReferenceTools] Ignored reflection error resolving field type: {ex.Message}");
            }

            return typeof(UnityEngine.Object);
        }

        #endregion
    }
}
