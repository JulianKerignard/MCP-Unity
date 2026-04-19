using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using McpUnity.Helpers;

namespace McpUnity.Utils
{
    /// <summary>
    /// Utility class for converting between Unity types and JSON-serializable formats.
    /// Handles Vector3, Quaternion, Color, and other Unity-specific types.
    /// Also provides shared skip-properties set and component serialization helpers.
    /// </summary>
    public static class TypeConverter
    {
        #region Shared Skip Properties

        /// <summary>
        /// Properties to skip during component serialization (allocated once, shared).
        /// Excludes heavy computed values (matrices, bounds) and non-useful Unity internals.
        /// </summary>
        internal static readonly HashSet<string> SkipProperties = new HashSet<string>
        {
            // Asset references (returned as objects, not useful inline)
            "mesh", "material", "materials", "sharedMesh", "sharedMaterial", "sharedMaterials",
            // Unity Object base
            "gameObject", "transform", "tag", "name", "hideFlags", "runInEditMode",
            // Derived / computed (not serializable state)
            "isActiveAndEnabled", "attachedRigidbody", "attachedArticulationBody",
            // 4x4 matrices -- enormous output, never useful to an AI
            "worldToLocalMatrix", "localToWorldMatrix",
            // Render bounds -- large structs, computed from mesh
            "bounds", "localBounds",
            // Internal render state
            "isVisible", "isPartOfStaticBatch", "isReceivingShadows",
            // Low-level renderer internals
            "lightProbeProxyVolumeOverride", "probeAnchor",
            "motionVectorGenerationMode", "allowOcclusionWhenDynamic",
            // Particle system sub-modules (exposed as objects -- each is huge)
            "collision", "colorBySpeed", "colorOverLifetime", "customData",
            "emission", "externalForces", "forceOverLifetime", "inheritVelocity",
            "lights", "limitVelocityOverLifetime", "main", "noise", "rotationBySpeed",
            "rotationOverLifetime", "shape", "sizeBySpeed", "sizeOverLifetime",
            "subEmitters", "textureSheetAnimation", "trails", "trigger", "velocityOverLifetime"
        };

        #endregion

        #region PropertyInfo Cache

        /// <summary>
        /// Cache PropertyInfo[] per component type -- avoids repeated GetProperties() reflection calls.
        /// </summary>
        internal static readonly Dictionary<Type, PropertyInfo[]> PropertyInfoCache
            = new Dictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// Clear all caches (call after domain reload or when new types may have been compiled).
        /// </summary>
        public static void ClearCaches()
        {
            PropertyInfoCache.Clear();
        }

        #endregion

        #region Unity to JSON Conversion

        /// <summary>
        /// Convert a Unity value to a JSON-serializable format.
        /// Handles primitives, Unity structs (Vector2/3/4, Quaternion, Color, Bounds, Rect),
        /// enums, UnityEngine.Object references, arrays, and generic Lists (up to 32 elements).
        /// </summary>
        internal static object ConvertValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            // Primitives
            if (type.IsPrimitive || value is string || value is decimal)
                return value;

            // Unity vectors and types
            if (value is Vector3 v3)
                return new { x = v3.x, y = v3.y, z = v3.z };
            if (value is Vector2 v2)
                return new { x = v2.x, y = v2.y };
            if (value is Vector4 v4)
                return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is Quaternion q)
                return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c)
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is Color32 c32)
                return new { r = c32.r, g = c32.g, b = c32.b, a = c32.a };
            if (value is Bounds b)
                return new { center = ConvertValue(b.center), size = ConvertValue(b.size) };
            if (value is Rect rect)
                return new { x = rect.x, y = rect.y, width = rect.width, height = rect.height };

            // Enum
            if (type.IsEnum)
                return value.ToString();

            // UnityEngine.Object reference
            if (value is UnityEngine.Object uobj)
                return uobj != null ? new { name = uobj.name, type = uobj.GetType().Name } : null;

            // Arrays -- serialize up to 32 elements to avoid huge outputs
            if (type.IsArray)
            {
                var arr = (System.Array)value;
                var items = new List<object>(Math.Min(arr.Length, 32));
                for (int i = 0; i < Math.Min(arr.Length, 32); i++)
                    items.Add(ConvertValue(arr.GetValue(i)));
                if (arr.Length > 32) items.Add($"... ({arr.Length - 32} more)");
                return items;
            }
            // Generic Lists -- serialize up to 32 elements
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (System.Collections.IEnumerable)value;
                var items = new List<object>();
                foreach (var item in list)
                {
                    items.Add(ConvertValue(item));
                    if (items.Count >= 32) { items.Add("... (truncated)"); break; }
                }
                return items;
            }

            return value.ToString();
        }

        /// <summary>
        /// Convert a component's properties to a serializable dictionary.
        /// Uses PropertyInfoCache and SkipProperties for performance.
        /// </summary>
        internal static Dictionary<string, object> ConvertToSerializable(Component component)
        {
            var result = new Dictionary<string, object>();
            if (component == null) return result;

            var type = component.GetType();

            // Cache PropertyInfo[] per type -- GetProperties() via reflection is expensive
            if (!PropertyInfoCache.TryGetValue(type, out var cachedProps))
            {
                cachedProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyInfoCache[type] = cachedProps;
            }

            // Get public properties (from cache)
            foreach (var prop in cachedProps)
            {
                if (!prop.CanRead) continue;

                if (SkipProperties.Contains(prop.Name)) continue;

                try
                {
                    var val = prop.GetValue(component);
                    result[prop.Name] = ConvertValue(val);
                }
                catch (Exception ex)
                {
                    // Log skipped properties for debugging
                    McpUnity.Editor.McpDebug.LogWarning($"[MCP TypeConverter] Cannot serialize property '{prop.Name}': {ex.Message}");
                }
            }

            return result;
        }

        #endregion

        #region JSON to Unity Conversion

        /// <summary>
        /// Convert a JSON value to a Unity type.
        /// Supports Vector2, Vector3, Quaternion, Color, enums, and standard Convert.ChangeType.
        /// </summary>
        internal static object ConvertJsonToUnity(object jsonValue, Type targetType)
        {
            if (jsonValue == null) return null;

            // Handle Dictionary from JSON parser
            var dict = jsonValue as Dictionary<string, object>;

            // Vector3
            if (targetType == typeof(Vector3) && dict != null)
            {
                return new Vector3(
                    ArgumentParser.GetFloat(dict, "x", 0f),
                    ArgumentParser.GetFloat(dict, "y", 0f),
                    ArgumentParser.GetFloat(dict, "z", 0f)
                );
            }

            // Vector2
            if (targetType == typeof(Vector2) && dict != null)
            {
                return new Vector2(
                    ArgumentParser.GetFloat(dict, "x", 0f),
                    ArgumentParser.GetFloat(dict, "y", 0f)
                );
            }

            // Quaternion
            if (targetType == typeof(Quaternion) && dict != null)
            {
                return new Quaternion(
                    ArgumentParser.GetFloat(dict, "x", 0f),
                    ArgumentParser.GetFloat(dict, "y", 0f),
                    ArgumentParser.GetFloat(dict, "z", 0f),
                    ArgumentParser.GetFloat(dict, "w", 1f)
                );
            }

            // Color
            if (targetType == typeof(Color) && dict != null)
            {
                return new Color(
                    ArgumentParser.GetFloat(dict, "r", 1f),
                    ArgumentParser.GetFloat(dict, "g", 1f),
                    ArgumentParser.GetFloat(dict, "b", 1f),
                    ArgumentParser.GetFloat(dict, "a", 1f)
                );
            }

            // Enum — SEC-#436: TryParse so an unknown value doesn't throw.
            if (targetType.IsEnum && jsonValue is string enumStr)
            {
                try
                {
                    return Enum.Parse(targetType, enumStr, ignoreCase: true);
                }
                catch (Exception)
                {
                    throw new ArgumentException(
                        $"Invalid value '{enumStr}' for enum {targetType.Name}. Valid values: {string.Join(", ", Enum.GetNames(targetType))}");
                }
            }

            // Standard conversion
            try
            {
                return Convert.ChangeType(jsonValue, targetType);
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[MCP TypeConverter] Cannot convert value to type '{targetType.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Apply properties from a dictionary to a component.
        /// Tries properties first, then fields. Uses ConvertJsonToUnity for type conversion.
        /// </summary>
        internal static List<string> ApplyComponentProperties(Component component, Dictionary<string, object> properties)
        {
            var modified = new List<string>();
            var type = component.GetType();

            foreach (var kvp in properties)
            {
                // Try property first
                var prop = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        var convertedValue = ConvertJsonToUnity(kvp.Value, prop.PropertyType);
                        if (convertedValue != null)
                        {
                            prop.SetValue(component, convertedValue);
                            modified.Add(kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        McpUnity.Editor.McpDebug.LogWarning($"[MCP TypeConverter] Cannot set property {kvp.Key}: {ex.Message}");
                    }
                    continue;
                }

                // Try field
                var field = type.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    try
                    {
                        var convertedValue = ConvertJsonToUnity(kvp.Value, field.FieldType);
                        if (convertedValue != null)
                        {
                            field.SetValue(component, convertedValue);
                            modified.Add(kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        McpUnity.Editor.McpDebug.LogWarning($"[MCP TypeConverter] Cannot set field {kvp.Key}: {ex.Message}");
                    }
                }
            }

            return modified;
        }

        /// <summary>
        /// Parse a dictionary to Vector3.
        /// </summary>
        public static Vector3 ParseVector3(object obj)
        {
            if (obj is Dictionary<string, object> dict)
            {
                float x = 0f, y = 0f, z = 0f;
                if (dict.TryGetValue("x", out var xVal))
                    float.TryParse(xVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
                if (dict.TryGetValue("y", out var yVal))
                    float.TryParse(yVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
                if (dict.TryGetValue("z", out var zVal))
                    float.TryParse(zVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
                return new Vector3(x, y, z);
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Parse a dictionary to Quaternion (from euler angles).
        /// </summary>
        public static Quaternion ParseQuaternionFromEuler(object obj)
        {
            if (obj is Dictionary<string, object> dict)
            {
                float x = 0f, y = 0f, z = 0f;
                if (dict.TryGetValue("x", out var xVal))
                    float.TryParse(xVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
                if (dict.TryGetValue("y", out var yVal))
                    float.TryParse(yVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
                if (dict.TryGetValue("z", out var zVal))
                    float.TryParse(zVal?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
                return Quaternion.Euler(x, y, z);
            }
            return Quaternion.identity;
        }

        #endregion
    }
}
