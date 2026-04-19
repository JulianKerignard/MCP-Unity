using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// ScriptableObject Tools - Create and modify ScriptableObject assets
    /// Contains 3 tools: CreateScriptableObject, ListScriptableObjectTypes, ModifyScriptableObject
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all ScriptableObject-related tools
        /// </summary>
        static partial void RegisterScriptableObjectTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_scriptable_object",
                description = "Create a new ScriptableObject asset. The type must be a valid ScriptableObject class in your project.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["typeName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Full type name of the ScriptableObject (e.g., 'WeaponData', 'MyNamespace.EnemyConfig')"
                        },
                        ["savePath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path where to save the asset (e.g., 'Assets/Data/Weapons/Pistol.asset')"
                        },
                        ["properties"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Optional: Initial property values to set (e.g., { damage: 25, fireRate: 0.5 })"
                        }
                    },
                    required = new List<string> { "typeName", "savePath" }
                }
            }, CreateScriptableObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_scriptable_object_types",
                description = "List all ScriptableObject types available in the project.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["nameFilter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional: filter types by name (supports * wildcard)"
                        },
                        ["includeUnity"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Include Unity built-in ScriptableObject types (default: false)"
                        }
                    },
                    required = new List<string>()
                }
            }, ListScriptableObjectTypes);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_modify_scriptable_object",
                description = "Modify properties of an existing ScriptableObject asset.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the ScriptableObject asset (e.g., 'Assets/Data/Weapons/Pistol.asset')"
                        },
                        ["properties"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Property values to set (e.g., { damage: 50, fireRate: 0.3 })"
                        }
                    },
                    required = new List<string> { "assetPath", "properties" }
                }
            }, ModifyScriptableObject);
        }

        #region ScriptableObject Tool Handlers

        private static McpToolResult CreateScriptableObject(Dictionary<string, object> args)
        {
            try
            {
                var (typeName, typeErr) = RequireArg(args, "typeName");
                if (typeErr != null) return typeErr;

                var (rawSavePath, savePathErr) = RequireArg(args, "savePath");
                if (savePathErr != null) return savePathErr;

                var (savePath, sanitizeErr) = TrySanitizePath(rawSavePath, "save path");
                if (sanitizeErr != null) return sanitizeErr;

                if (!savePath.EndsWith(".asset"))
                    savePath += ".asset";

                // Find the type
                Type soType = FindScriptableObjectType(typeName);
                if (soType == null)
                    return McpToolResult.Error($"ScriptableObject type '{typeName}' not found. Use unity_list_scriptable_object_types to see available types.");

                // Create the instance
                var instance = ScriptableObject.CreateInstance(soType);
                if (instance == null)
                    return McpToolResult.Error($"Failed to create instance of '{typeName}'");

                // Set properties if provided
                if (args.TryGetValue("properties", out var propsObj) && propsObj is Dictionary<string, object> properties)
                {
                    SetScriptableObjectProperties(instance, properties);
                }

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    CreateAssetFolder(directory);
                }

                // Save the asset
                AssetDatabase.CreateAsset(instance, savePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Get the created asset info
                var createdAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(savePath);

                return McpResponse.Success($"Created ScriptableObject '{typeName}' at {savePath}", new
                {
                    assetPath = savePath,
                    typeName = soType.FullName,
                    instanceId = createdAsset?.GetInstanceID()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create ScriptableObject: {ex.Message}");
            }
        }

        private static McpToolResult ListScriptableObjectTypes(Dictionary<string, object> args)
        {
            try
            {
                string nameFilter = ArgumentParser.GetString(args, "nameFilter", "");
                bool includeUnity = ArgumentParser.GetBool(args, "includeUnity", false);

                var types = new List<object>();

                // Get all types that inherit from ScriptableObject
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Skip Unity internals unless requested
                        string assemblyName = assembly.GetName().Name;
                        bool isUnityAssembly = assemblyName.StartsWith("Unity") ||
                                               assemblyName.StartsWith("UnityEngine") ||
                                               assemblyName.StartsWith("UnityEditor");

                        if (isUnityAssembly && !includeUnity)
                            continue;

                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsAbstract &&
                                typeof(ScriptableObject).IsAssignableFrom(type) &&
                                type != typeof(ScriptableObject))
                            {
                                // Apply name filter
                                if (!string.IsNullOrEmpty(nameFilter))
                                {
                                    string pattern = nameFilter.Replace("*", "");
                                    bool matches = nameFilter.StartsWith("*") && nameFilter.EndsWith("*")
                                        ? type.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                                        : nameFilter.StartsWith("*")
                                            ? type.Name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)
                                            : nameFilter.EndsWith("*")
                                                ? type.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)
                                                : type.Name.Equals(nameFilter, StringComparison.OrdinalIgnoreCase);

                                    if (!matches) continue;
                                }

                                // Get serialized fields
                                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                    .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                                    .Select(f => new
                                    {
                                        name = f.Name,
                                        type = GetFriendlyTypeName(f.FieldType)
                                    })
                                    .ToList();

                                types.Add(new
                                {
                                    name = type.Name,
                                    fullName = type.FullName,
                                    assembly = assemblyName,
                                    isUnityType = isUnityAssembly,
                                    fieldCount = fields.Count,
                                    fields = fields.Take(10).ToList() // Limit fields for readability
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                {
                    McpUnity.Editor.McpDebug.LogWarning($"[ScriptableObject] Skipping assembly during type listing: {ex.Message}");
                }
                }

                // SEC-#440: dynamic dispatch on anonymous types incurs DLR overhead and breaks
                // under AOT (IL2CPP). Use reflection to extract the string `name` field once.
                types = types.OrderBy(t =>
                {
                    var prop = t.GetType().GetProperty("name");
                    return prop != null ? prop.GetValue(t) as string ?? "" : "";
                }, StringComparer.Ordinal).ToList();

                return McpResponse.Success(new
                {
                    types = types,
                    count = types.Count,
                    includeUnity = includeUnity,
                    nameFilter = string.IsNullOrEmpty(nameFilter) ? null : nameFilter
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to list ScriptableObject types: {ex.Message}");
            }
        }

        private static McpToolResult ModifyScriptableObject(Dictionary<string, object> args)
        {
            try
            {
                var (rawAssetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;

                var (assetPath, sanitizeErr) = TrySanitizePath(rawAssetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;

                if (!ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "properties", out var properties))
                    return McpToolResult.Error("Missing required parameter: properties");

                // Load the asset
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null)
                    return McpToolResult.Error($"ScriptableObject not found at: {assetPath}");

                // Record for undo
                Undo.RecordObject(asset, "Modify ScriptableObject");

                // Set properties
                var modifiedProperties = SetScriptableObjectProperties(asset, properties);

                // Save changes
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return McpResponse.Success($"Modified {modifiedProperties.Count} properties on '{asset.name}'", new
                {
                    assetPath = assetPath,
                    typeName = asset.GetType().FullName,
                    modifiedProperties = modifiedProperties
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to modify ScriptableObject: {ex.Message}");
            }
        }

        #endregion

        #region ScriptableObject Helpers

        private static Type FindScriptableObjectType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Try exact match first
                    var type = assembly.GetType(typeName);
                    if (type != null && typeof(ScriptableObject).IsAssignableFrom(type))
                        return type;

                    // Try finding by name only
                    foreach (var t in assembly.GetTypes())
                    {
                        if ((t.Name == typeName || t.FullName == typeName) &&
                            typeof(ScriptableObject).IsAssignableFrom(t) &&
                            !t.IsAbstract)
                        {
                            return t;
                        }
                    }
                }
                catch (Exception ex)
                {
                    McpUnity.Editor.McpDebug.LogWarning($"[ScriptableObject] Skipping assembly during type search: {ex.Message}");
                }
            }
            return null;
        }

        private static List<string> SetScriptableObjectProperties(ScriptableObject instance, Dictionary<string, object> properties)
        {
            var modified = new List<string>();
            var type = instance.GetType();

            foreach (var kvp in properties)
            {
                var field = type.GetField(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    // Try property
                    var prop = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        try
                        {
                            var convertedValue = ConvertValue(kvp.Value, prop.PropertyType);
                            prop.SetValue(instance, convertedValue);
                            modified.Add(kvp.Key);
                        }
                        catch (Exception ex)
                        {
                            McpUnity.Editor.McpDebug.LogWarning($"[ScriptableObject] Failed to set property '{kvp.Key}': {ex.Message}");
                        }
                    }
                    continue;
                }

                try
                {
                    var convertedValue = ConvertValue(kvp.Value, field.FieldType);
                    field.SetValue(instance, convertedValue);
                    modified.Add(kvp.Key);
                }
                catch (Exception ex)
                {
                    McpUnity.Editor.McpDebug.LogWarning($"[ScriptableObject] Failed to set field '{kvp.Key}': {ex.Message}");
                }
            }

            return modified;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            // Handle nullable types
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Direct assignment if types match
            if (underlyingType.IsAssignableFrom(value.GetType()))
                return value;

            // Convert from JSON number types
            if (underlyingType == typeof(int))
                return Convert.ToInt32(value);
            if (underlyingType == typeof(float))
                return Convert.ToSingle(value);
            if (underlyingType == typeof(double))
                return Convert.ToDouble(value);
            if (underlyingType == typeof(long))
                return Convert.ToInt64(value);
            if (underlyingType == typeof(bool))
                return Convert.ToBoolean(value);
            if (underlyingType == typeof(string))
                return value.ToString();

            // Handle enums
            if (underlyingType.IsEnum)
            {
                if (value is string strValue)
                    return Enum.Parse(underlyingType, strValue, true);
                return Enum.ToObject(underlyingType, Convert.ToInt32(value));
            }

            // Handle Vector types
            if (underlyingType == typeof(Vector2) && value is Dictionary<string, object> v2)
            {
                return new Vector2(
                    Convert.ToSingle(v2.GetValueOrDefault("x", 0)),
                    Convert.ToSingle(v2.GetValueOrDefault("y", 0))
                );
            }
            if (underlyingType == typeof(Vector3) && value is Dictionary<string, object> v3)
            {
                return new Vector3(
                    Convert.ToSingle(v3.GetValueOrDefault("x", 0)),
                    Convert.ToSingle(v3.GetValueOrDefault("y", 0)),
                    Convert.ToSingle(v3.GetValueOrDefault("z", 0))
                );
            }

            // Handle Color
            if (underlyingType == typeof(Color) && value is string colorStr)
            {
                return ColorParser.Parse(colorStr, Color.white);
            }

            // Fallback: try ChangeType
            return Convert.ChangeType(value, underlyingType);
        }

        private static void CreateAssetFolder(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string parentFolder = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string newFolder = parentFolder + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newFolder))
                {
                    AssetDatabase.CreateFolder(parentFolder, folders[i]);
                }
                parentFolder = newFolder;
            }
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Color)) return "Color";
            if (type == typeof(GameObject)) return "GameObject";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return $"List<{GetFriendlyTypeName(type.GetGenericArguments()[0])}>";
            if (type.IsArray)
                return $"{GetFriendlyTypeName(type.GetElementType())}[]";
            return type.Name;
        }

        #endregion
    }
}
