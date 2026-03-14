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
                McpDebug.LogError($"[MCP JSON-RPC] Error processing message: {ex.Message}\n{ex.StackTrace}");
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
                McpDebug.LogError($"[MCP JSON-RPC] Handler error for {request.method}: {ex.Message}");
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
                McpDebug.LogError($"[MCP JSON-RPC] Tool execution error: {ex.Message}\n{ex.StackTrace}");
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
}
