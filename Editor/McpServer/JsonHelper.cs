using System;
using System.Collections.Generic;
using UnityEngine;
using McpUnity.Editor;

namespace McpUnity.Server
{
    /// <summary>
    /// JSON helper with full Dictionary support for MCP protocol
    /// </summary>
    public static class JsonHelper
    {
        // Thread-local StringBuilder to avoid allocations on the hot path.
        // Each ToJson call uses this single builder from start to finish.
        [ThreadStatic] private static System.Text.StringBuilder _sharedSb;

        // PERF-#20: cache reflection results per type. GetProperties/GetFields enumerate
        // metadata every call; for a serializer on the hot path this is a measurable cost.
        // ConcurrentDictionary because JsonHelper is callable from worker threads.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]> _propsCache
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]>();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]> _fieldsCache
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]>();

        private static System.Reflection.PropertyInfo[] GetCachedProperties(Type type)
            => _propsCache.GetOrAdd(type, t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));

        private static System.Reflection.FieldInfo[] GetCachedFields(Type type)
            => _fieldsCache.GetOrAdd(type, t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));

        // FIX-#fc1: reference-equality comparer for the circular-reference visited set.
        // Mono / .NET Standard 2.1 (used by Unity) don't ship ReferenceEqualityComparer, so
        // we use this lightweight equivalent.
        private static readonly RefEqualityComparer _referenceEqualityComparer = new RefEqualityComparer();
        private sealed class RefEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => object.ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public static string ToJson(object obj)
        {
            if (_sharedSb == null) _sharedSb = new System.Text.StringBuilder(4096);
            _sharedSb.Clear();
            AppendJson(_sharedSb, obj, null);
            return _sharedSb.ToString();
        }

        /// <summary>
        /// Core recursive serializer — appends JSON to the provided StringBuilder.
        /// Zero intermediate string allocations for structure (braces, commas, colons).
        /// </summary>
        private static void AppendJson(System.Text.StringBuilder sb, object obj, HashSet<object> visited)
        {
            if (obj == null) { sb.Append("null"); return; }

            if (obj is string s) { sb.Append('"'); AppendEscaped(sb, s); sb.Append('"'); return; }
            if (obj is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (obj is int i) { sb.Append(i); return; }
            if (obj is long l) { sb.Append(l); return; }
            if (obj is float f)
            {
                if (float.IsNaN(f) || float.IsInfinity(f)) sb.Append("null");
                else sb.Append(f.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (obj is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                else sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // Dictionary<string, object>
            if (obj is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kvp in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"'); AppendEscaped(sb, kvp.Key); sb.Append("\":");
                    AppendJson(sb, kvp.Value, visited);
                }
                sb.Append('}');
                return;
            }

            // Non-generic dictionaries (Dictionary<string, McpPropertySchema> etc.)
            if (obj is System.Collections.IDictionary nonGenericDict)
            {
                sb.Append('{');
                bool first = true;
                foreach (System.Collections.DictionaryEntry entry in nonGenericDict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    var key = entry.Key?.ToString() ?? "";
                    sb.Append('"'); AppendEscaped(sb, key); sb.Append("\":");
                    AppendJson(sb, entry.Value, visited);
                }
                sb.Append('}');
                return;
            }

            // Enums — serialize by name (string) so clients get a human-readable value instead
            // of the internal field layout reflection would produce.
            if (obj is Enum e)
            {
                sb.Append('"');
                AppendEscaped(sb, e.ToString());
                sb.Append('"');
                return;
            }

            // Lists / arrays
            if (obj is System.Collections.IList list)
            {
                sb.Append('[');
                for (int idx = 0; idx < list.Count; idx++)
                {
                    if (idx > 0) sb.Append(',');
                    AppendJson(sb, list[idx], visited);
                }
                sb.Append(']');
                return;
            }

            // Complex objects — reflection-based.
            // FIX-#fc1: use reference equality so siblings with identical content (e.g. two
            // Dictionary<string,object> with same {name, path} keys) aren't flagged as circular.
            // Some .NET types override Equals to do value comparison — default HashSet<object>
            // would then collapse equivalent-but-distinct instances. ReferenceEqualityComparer
            // is .NET 5+; we use a hand-rolled equivalent for Mono / NetStandard 2.1 builds.
            if (visited == null) visited = new HashSet<object>(_referenceEqualityComparer);
            if (!visited.Add(obj)) { sb.Append("\"[circular]\""); return; }

            var type = obj.GetType();
            sb.Append('{');
            bool firstMember = true;
            var serializedNames = new HashSet<string>();

            // 1. Properties first (anonymous types, modern classes)
            var properties = GetCachedProperties(type);
            foreach (var prop in properties)
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var value = prop.GetValue(obj);
                    if (value != null)
                    {
                        if (!firstMember) sb.Append(',');
                        firstMember = false;
                        sb.Append('"'); AppendEscaped(sb, prop.Name); sb.Append("\":");
                        AppendJson(sb, value, visited);
                        serializedNames.Add(prop.Name);
                    }
                }
                catch (Exception) { /* Skip properties that throw during reflection serialization */ }
            }

            // 2. Fields (Unity [Serializable] classes)
            var fields = GetCachedFields(type);
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value != null)
                {
                    var name = field.Name;
                    if (name.StartsWith("@")) name = name.Substring(1);
                    if (!serializedNames.Contains(name))
                    {
                        if (!firstMember) sb.Append(',');
                        firstMember = false;
                        sb.Append('"'); AppendEscaped(sb, name); sb.Append("\":");
                        AppendJson(sb, value, visited);
                    }
                }
            }

            sb.Append('}');
        }

        /// <summary>
        /// Append a JSON-escaped string to StringBuilder.
        /// Fast-path: scan for chars that need escaping. If none found, append the whole string in one call.
        /// </summary>
        private static void AppendEscaped(System.Text.StringBuilder sb, string s)
        {
            // Fast path: check if any escaping is needed
            bool needsEscape = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' || c == '"' || c < 0x20)
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                sb.Append(s);
                return;
            }

            // Slow path: escape character by character
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
        }

        public static T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                McpDebug.LogWarning($"[JsonHelper] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse JSON string to a Dictionary (supports nested objects)
        /// </summary>
        public static Dictionary<string, object> ParseJsonObject(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            json = json.Trim();
            if (!json.StartsWith("{")) return null;

            var parser = new SimpleJsonParser(json);
            return parser.ParseObject();
        }
        // SEC-#444: removed dead `EscapeString` (private + unused) — callers go through
        // AppendEscaped directly.
    }
}
