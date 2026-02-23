using System;
using System.Collections.Generic;
using UnityEngine;
using McpUnity.Protocol;
using McpUnity.Editor;

namespace McpUnity.Server
{
    /// <summary>
    /// JSON-RPC 2.0 message handler for MCP protocol
    /// </summary>
    public static class McpJsonRpc
    {
        private static readonly Dictionary<string, Func<JsonRpcRequest, JsonRpcResponse>> _methodHandlers
            = new Dictionary<string, Func<JsonRpcRequest, JsonRpcResponse>>();

        private static McpToolRegistry _toolRegistry;
        private static McpResourceRegistry _resourceRegistry;

        /// <summary>
        /// Initialize the JSON-RPC handler with tool and resource registries
        /// </summary>
        public static void Initialize(McpToolRegistry toolRegistry, McpResourceRegistry resourceRegistry)
        {
            _toolRegistry = toolRegistry;
            _resourceRegistry = resourceRegistry;
            RegisterDefaultHandlers();
        }

        private static void RegisterDefaultHandlers()
        {
            _methodHandlers.Clear();

            // Core MCP methods
            RegisterMethod("initialize", HandleInitialize);
            RegisterMethod("initialized", HandleInitialized);
            RegisterMethod("ping", HandlePing);

            // Tools
            RegisterMethod("tools/list", HandleToolsList);
            RegisterMethod("tools/call", HandleToolsCall);

            // Resources
            RegisterMethod("resources/list", HandleResourcesList);
            RegisterMethod("resources/read", HandleResourcesRead);

            // Prompts (minimal implementation)
            RegisterMethod("prompts/list", HandlePromptsList);
        }

        /// <summary>
        /// Register a custom method handler
        /// </summary>
        public static void RegisterMethod(string method, Func<JsonRpcRequest, JsonRpcResponse> handler)
        {
            _methodHandlers[method] = handler;
        }

        /// <summary>
        /// Process an incoming JSON-RPC message and return the response
        /// </summary>
        public static string ProcessMessage(string jsonMessage)
        {
            object requestId = null;

            try
            {
                // Parse using our custom parser to properly handle nested objects
                var parsed = JsonHelper.ParseJsonObject(jsonMessage);
                if (parsed == null)
                {
                    return SerializeResponse(JsonRpcResponse.Error(
                        null,
                        JsonRpcError.ParseError,
                        "Failed to parse JSON"
                    ));
                }

                parsed.TryGetValue("id", out requestId);
                parsed.TryGetValue("method", out var methodObj);
                parsed.TryGetValue("params", out var paramsObj);

                string method = methodObj?.ToString();

                if (string.IsNullOrEmpty(method))
                {
                    return SerializeResponse(JsonRpcResponse.Error(
                        requestId,
                        JsonRpcError.InvalidRequest,
                        "Invalid JSON-RPC request: method is required"
                    ));
                }

                // Create request object with properly parsed params
                var request = new JsonRpcRequest
                {
                    id = requestId,
                    method = method,
                    @params = paramsObj
                };

                // Check if this is a notification (no id)
                bool isNotification = requestId == null;

                var response = ProcessRequest(request);

                // Notifications don't get responses
                if (isNotification && response.error == null)
                {
                    return null;
                }

                return SerializeResponse(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP JSON-RPC] Error processing message: {ex.Message}\n{ex.StackTrace}");
                return SerializeResponse(JsonRpcResponse.Error(
                    requestId,
                    JsonRpcError.InternalError,
                    ex.Message
                ));
            }
        }

        private static JsonRpcResponse ProcessRequest(JsonRpcRequest request)
        {
            if (!_methodHandlers.TryGetValue(request.method, out var handler))
            {
                McpRequestMonitor.Record(request.method, null, 0, false, $"Method not found: {request.method}");
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.MethodNotFound,
                    $"Method not found: {request.method}"
                );
            }

            // Extract tool name for tools/call requests
            string toolName = null;
            if (request.method == "tools/call" && request.@params is Dictionary<string, object> p)
            {
                if (p.TryGetValue("name", out var n))
                    toolName = n?.ToString();
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = handler(request);
                sw.Stop();
                bool success = response.error == null;
                McpRequestMonitor.Record(request.method, toolName, sw.Elapsed.TotalMilliseconds, success, response.error?.message);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                McpRequestMonitor.Record(request.method, toolName, sw.Elapsed.TotalMilliseconds, false, ex.Message);
                Debug.LogError($"[MCP JSON-RPC] Handler error for {request.method}: {ex.Message}");
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.InternalError,
                    ex.Message
                );
            }
        }

        #region Default Handlers

        private static JsonRpcResponse HandleInitialize(JsonRpcRequest request)
        {
            var result = new McpInitializeResult
            {
                protocolVersion = "2024-11-05",
                capabilities = new McpServerCapabilities
                {
                    tools = new McpToolsCapability { listChanged = true },
                    resources = new McpResourcesCapability { subscribe = false, listChanged = true },
                    prompts = new McpPromptsCapability { listChanged = false }
                },
                serverInfo = new McpServerInfo
                {
                    name = "mcp-unity",
                    version = "1.0.0"
                }
            };

            return JsonRpcResponse.Success(request.id, result);
        }

        private static JsonRpcResponse HandleInitialized(JsonRpcRequest request)
        {
            // This is a notification, no response needed
            McpDebug.Log("[MCP Unity] Client initialized successfully");
            return JsonRpcResponse.Success(request.id, new { });
        }

        private static JsonRpcResponse HandlePing(JsonRpcRequest request)
        {
            return JsonRpcResponse.Success(request.id, new { });
        }

        private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
        {
            if (_toolRegistry == null)
            {
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.InternalError,
                    "Tool registry not initialized"
                );
            }

            var tools = _toolRegistry.GetAllTools();
            return JsonRpcResponse.Success(request.id, new { tools = tools });
        }

        private static JsonRpcResponse HandleToolsCall(JsonRpcRequest request)
        {
            if (_toolRegistry == null)
            {
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.InternalError,
                    "Tool registry not initialized"
                );
            }

            try
            {
                // Extract tool name and arguments from params (already parsed as Dictionary)
                string toolName = null;
                Dictionary<string, object> arguments = new Dictionary<string, object>();

                if (request.@params is Dictionary<string, object> paramsDict)
                {
                    if (paramsDict.TryGetValue("name", out var nameObj))
                    {
                        toolName = nameObj?.ToString();
                    }

                    if (paramsDict.TryGetValue("arguments", out var argsObj))
                    {
                        if (argsObj is Dictionary<string, object> argsDict)
                        {
                            arguments = argsDict;
                        }
                    }
                }

                if (string.IsNullOrEmpty(toolName))
                {
                    return JsonRpcResponse.Error(
                        request.id,
                        JsonRpcError.InvalidParams,
                        "Tool name is required"
                    );
                }

                McpDebug.Log($"[MCP JSON-RPC] Executing tool: {toolName} with {arguments.Count} arguments");

                var result = _toolRegistry.ExecuteTool(toolName, arguments);
                return JsonRpcResponse.Success(request.id, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP JSON-RPC] Tool execution error: {ex.Message}\n{ex.StackTrace}");
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.ExecutionError,
                    $"Tool execution failed: {ex.Message}"
                );
            }
        }

        private static JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
        {
            if (_resourceRegistry == null)
            {
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.InternalError,
                    "Resource registry not initialized"
                );
            }

            var resources = _resourceRegistry.GetAllResources();
            return JsonRpcResponse.Success(request.id, new { resources = resources });
        }

        private static JsonRpcResponse HandleResourcesRead(JsonRpcRequest request)
        {
            if (_resourceRegistry == null)
            {
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.InternalError,
                    "Resource registry not initialized"
                );
            }

            try
            {
                string uri = null;

                if (request.@params is Dictionary<string, object> paramsDict)
                {
                    if (paramsDict.TryGetValue("uri", out var uriObj))
                    {
                        uri = uriObj?.ToString();
                    }
                }

                if (string.IsNullOrEmpty(uri))
                {
                    return JsonRpcResponse.Error(
                        request.id,
                        JsonRpcError.InvalidParams,
                        "Resource URI is required"
                    );
                }

                var result = _resourceRegistry.ReadResource(uri);
                return JsonRpcResponse.Success(request.id, result);
            }
            catch (Exception ex)
            {
                return JsonRpcResponse.Error(
                    request.id,
                    JsonRpcError.ResourceNotFound,
                    ex.Message
                );
            }
        }

        private static JsonRpcResponse HandlePromptsList(JsonRpcRequest request)
        {
            // Minimal prompts implementation - return empty list
            return JsonRpcResponse.Success(request.id, new { prompts = new List<object>() });
        }

        #endregion

        private static string SerializeResponse(JsonRpcResponse response)
        {
            return JsonHelper.ToJson(response);
        }
    }

    /// <summary>
    /// JSON helper with full Dictionary support for MCP protocol
    /// </summary>
    public static class JsonHelper
    {
        // Thread-local StringBuilder to avoid allocations on the hot path.
        // Each ToJson call uses this single builder from start to finish.
        [ThreadStatic] private static System.Text.StringBuilder _sharedSb;

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

            // Complex objects — reflection-based
            if (visited == null) visited = new HashSet<object>();
            if (!visited.Add(obj)) { sb.Append("\"[circular]\""); return; }

            var type = obj.GetType();
            sb.Append('{');
            bool firstMember = true;
            var serializedNames = new HashSet<string>();

            // 1. Properties first (anonymous types, modern classes)
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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

        // Legacy wrapper kept for any external callers
        private static string EscapeString(string s)
        {
            var esb = new System.Text.StringBuilder(s.Length + 8);
            AppendEscaped(esb, s);
            return esb.ToString();
        }
    }

    /// <summary>
    /// Simple JSON parser that supports Dictionary<string, object>
    /// </summary>
    public class SimpleJsonParser
    {
        private readonly string _json;
        private int _pos;
        private int _depth = 0;
        private const int MaxDepth = 64;
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);

        public SimpleJsonParser(string json)
        {
            _json = json;
            _pos = 0;
        }

        public Dictionary<string, object> ParseObject()
        {
            SkipWhitespace();
            if (_pos >= _json.Length || _json[_pos] != '{') return null;
            _depth++;
            if (_depth > MaxDepth)
                throw new InvalidOperationException($"JSON nesting too deep (max: {MaxDepth})");
            _pos++; // skip '{'

            var result = new Dictionary<string, object>();

            SkipWhitespace();
            if (_pos < _json.Length && _json[_pos] == '}')
            {
                _pos++;
                _depth--;
                return result;
            }

            while (_pos < _json.Length)
            {
                SkipWhitespace();

                // Parse key
                var key = ParseString();
                if (key == null) break;

                SkipWhitespace();
                if (_pos >= _json.Length || _json[_pos] != ':') break;
                _pos++; // skip ':'

                SkipWhitespace();
                var value = ParseValue();
                result[key] = value;

                SkipWhitespace();
                if (_pos >= _json.Length) break;

                if (_json[_pos] == '}')
                {
                    _pos++;
                    _depth--;
                    return result;
                }

                if (_json[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                break;
            }

            _depth--;
            return result;
        }

        private object ParseValue()
        {
            SkipWhitespace();
            if (_pos >= _json.Length) return null;

            char c = _json[_pos];

            if (c == '"') return ParseString();
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == 't' || c == 'f') return ParseBool();
            if (c == 'n') return ParseNull();
            if (c == '-' || char.IsDigit(c)) return ParseNumber();

            return null;
        }

        private string ParseString()
        {
            if (_pos >= _json.Length || _json[_pos] != '"') return null;
            _pos++; // skip opening quote

            _sb.Clear();
            while (_pos < _json.Length)
            {
                char c = _json[_pos];
                if (c == '"')
                {
                    _pos++;
                    return _sb.ToString();
                }
                if (c == '\\' && _pos + 1 < _json.Length)
                {
                    _pos++;
                    char escaped = _json[_pos];
                    switch (escaped)
                    {
                        case 'n': _sb.Append('\n'); break;
                        case 'r': _sb.Append('\r'); break;
                        case 't': _sb.Append('\t'); break;
                        case '"': _sb.Append('"'); break;
                        case '\\': _sb.Append('\\'); break;
                        case 'u':
                            if (_pos + 4 < _json.Length)
                            {
                                var hex = _json.Substring(_pos + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out int codePoint))
                                {
                                    _sb.Append((char)codePoint);
                                    _pos += 4;
                                }
                                else
                                {
                                    _sb.Append('u');
                                }
                            }
                            else
                            {
                                _sb.Append('u');
                            }
                            break;
                        case '/': _sb.Append('/'); break;
                        case 'b': _sb.Append('\b'); break;
                        case 'f': _sb.Append('\f'); break;
                        default: _sb.Append(escaped); break;
                    }
                }
                else
                {
                    _sb.Append(c);
                }
                _pos++;
            }
            return _sb.ToString();
        }

        private List<object> ParseArray()
        {
            if (_pos >= _json.Length || _json[_pos] != '[') return null;
            _depth++;
            if (_depth > MaxDepth)
                throw new InvalidOperationException($"JSON nesting too deep (max: {MaxDepth})");
            _pos++; // skip '['

            var result = new List<object>();

            SkipWhitespace();
            if (_pos < _json.Length && _json[_pos] == ']')
            {
                _pos++;
                _depth--;
                return result;
            }

            while (_pos < _json.Length)
            {
                SkipWhitespace();
                var value = ParseValue();
                result.Add(value);

                SkipWhitespace();
                if (_pos >= _json.Length) break;

                if (_json[_pos] == ']')
                {
                    _pos++;
                    _depth--;
                    return result;
                }

                if (_json[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                break;
            }

            _depth--;
            return result;
        }

        private object ParseNumber()
        {
            int start = _pos;
            if (_json[_pos] == '-') _pos++;

            while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;

            bool isFloat = false;
            if (_pos < _json.Length && _json[_pos] == '.')
            {
                isFloat = true;
                _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }

            if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
            {
                isFloat = true;
                _pos++;
                if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }

            string numStr = _json.Substring(start, _pos - start);

            if (isFloat)
            {
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }
            else
            {
                if (int.TryParse(numStr, out int i)) return i;
                if (long.TryParse(numStr, out long l)) return l;
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }

            return null;
        }

        private bool ParseBool()
        {
            if (_pos + 4 <= _json.Length &&
                _json[_pos] == 't' && _json[_pos + 1] == 'r' &&
                _json[_pos + 2] == 'u' && _json[_pos + 3] == 'e')
            {
                _pos += 4;
                return true;
            }
            if (_pos + 5 <= _json.Length &&
                _json[_pos] == 'f' && _json[_pos + 1] == 'a' &&
                _json[_pos + 2] == 'l' && _json[_pos + 3] == 's' && _json[_pos + 4] == 'e')
            {
                _pos += 5;
                return false;
            }
            return false;
        }

        private object ParseNull()
        {
            if (_pos + 4 <= _json.Length &&
                _json[_pos] == 'n' && _json[_pos + 1] == 'u' &&
                _json[_pos + 2] == 'l' && _json[_pos + 3] == 'l')
            {
                _pos += 4;
                return null;
            }
            return null;
        }

        private void SkipWhitespace()
        {
            while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos]))
                _pos++;
        }
    }
}
