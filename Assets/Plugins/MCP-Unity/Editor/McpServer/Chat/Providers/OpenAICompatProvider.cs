using System;
using System.Collections.Generic;
using System.Text;
using McpUnity.Server;
using UnityEngine.Networking;

namespace McpUnity.Chat.Providers
{
    /// <summary>
    /// OpenAI-compatible provider. Handles OpenAI, DeepSeek, Groq, Mistral, Ollama, LM Studio,
    /// Together AI, Fireworks, and any endpoint that implements the OpenAI Chat Completions API.
    /// </summary>
    public class OpenAICompatProvider : IChatProvider
    {
        private readonly ProviderPreset _preset;

        public OpenAICompatProvider(ProviderPreset preset)
        {
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        public string Id => _preset.id;
        public string DisplayName => _preset.displayName;
        public string[] ModelIds => _preset.modelIds;
        public string[] ModelLabels => _preset.modelLabels;
        public string DefaultModel => _preset.defaultModel;
        public int DefaultMaxTokens => _preset.defaultMaxTokens;
        public int MaxContextTokens => _preset.maxContextTokens;
        public string ApiKeyEnvVar => _preset.apiKeyEnvVar;
        public bool SupportsOAuth => false;
        public bool SupportsTools => _preset.supportsTools;

        public AuthResult ResolveAuth(string storedApiKey)
        {
            // 1) Stored API key
            if (!string.IsNullOrEmpty(storedApiKey))
                return new AuthResult
                {
                    IsValid = true,
                    HeaderName = "Authorization",
                    HeaderValue = "Bearer " + storedApiKey,
                    Source = $"API Key (EditorPrefs)"
                };

            // 2) Environment variable
            if (!string.IsNullOrEmpty(ApiKeyEnvVar))
            {
                string envKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
                if (!string.IsNullOrEmpty(envKey))
                    return new AuthResult
                    {
                        IsValid = true,
                        HeaderName = "Authorization",
                        HeaderValue = "Bearer " + envKey,
                        Source = $"API Key (env ${ApiKeyEnvVar})"
                    };
            }

            // Local providers (Ollama, LM Studio) don't need auth
            if (_preset.isLocal)
                return new AuthResult { IsValid = true, HeaderName = "", HeaderValue = "", Source = "Local (no auth)" };

            return new AuthResult { IsValid = false, Source = "None" };
        }

        public string GetEndpointUrl(string model, string customEndpoint)
        {
            return string.IsNullOrEmpty(customEndpoint) ? _preset.defaultEndpoint : customEndpoint;
        }

        public void ConfigureRequest(UnityWebRequest request, AuthResult auth, string model)
        {
            // Set auth header (skip for local providers with no auth)
            if (!string.IsNullOrEmpty(auth.HeaderName))
                request.SetRequestHeader(auth.HeaderName, auth.HeaderValue);

            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("accept", "text/event-stream");
        }

        public byte[] BuildRequestBody(string systemPrompt, List<object> messages,
                                        List<object> tools, string model, int maxTokens)
        {
            // Convert messages from Anthropic format to OpenAI format
            var openaiMessages = ConvertMessages(messages, systemPrompt);

            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = maxTokens,
                ["stream"] = true,
                ["messages"] = openaiMessages,
                ["stream_options"] = new Dictionary<string, object> { ["include_usage"] = true }
            };

            // Temperature (OpenAI supports 0.0 - 2.0)
            float temperature = ProviderRegistry.GetTemperature(_preset.id);
            body["temperature"] = System.Math.Round(temperature, 2);

            if (SupportsTools && tools != null && tools.Count > 0)
                body["tools"] = ConvertToolDefinitions(tools);

            return Encoding.UTF8.GetBytes(JsonHelper.ToJson(body));
        }

        // ====================================================================
        // Tool Definition Conversion: MCP → OpenAI format
        // ====================================================================

        public List<object> ConvertToolDefinitions(List<object> mcpToolDefs)
        {
            var result = new List<object>();
            foreach (var toolObj in mcpToolDefs)
            {
                if (!(toolObj is Dictionary<string, object> tool)) continue;

                string name = tool.TryGetValue("name", out var n) ? n?.ToString() : "";
                string desc = tool.TryGetValue("description", out var d) ? d?.ToString() : "";
                object schema = tool.TryGetValue("input_schema", out var s) ? s : new Dictionary<string, object>();

                result.Add(new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["description"] = desc,
                        ["parameters"] = schema
                    }
                });
            }
            return result;
        }

        // ====================================================================
        // Message Conversion: Anthropic → OpenAI format
        // ====================================================================

        public List<object> ConvertMessages(List<object> anthropicMessages, string systemPrompt)
        {
            var result = new List<object>();

            // System prompt becomes first message
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                result.Add(new Dictionary<string, object>
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                });
            }

            foreach (var msgObj in anthropicMessages)
            {
                if (!(msgObj is Dictionary<string, object> msg)) continue;

                string role = msg.TryGetValue("role", out var r) ? r?.ToString() : "user";
                var contentList = msg.TryGetValue("content", out var c) && c is List<object> cl ? cl : null;

                if (contentList == null) continue;

                // Check what types of content blocks we have
                var textParts = new StringBuilder();
                var toolCalls = new List<object>();
                var toolResults = new List<Dictionary<string, object>>();

                foreach (var blockObj in contentList)
                {
                    if (!(blockObj is Dictionary<string, object> block)) continue;
                    string blockType = block.TryGetValue("type", out var bt) ? bt?.ToString() : "";

                    switch (blockType)
                    {
                        case "text":
                            string text = block.TryGetValue("text", out var t) ? t?.ToString() : "";
                            if (textParts.Length > 0) textParts.Append("\n");
                            textParts.Append(text);
                            break;

                        case "tool_use":
                            string tcId = block.TryGetValue("id", out var tid) ? tid?.ToString() : "";
                            string tcName = block.TryGetValue("name", out var tn) ? tn?.ToString() : "";
                            object tcInput = block.TryGetValue("input", out var ti) ? ti : new Dictionary<string, object>();
                            string argsJson = tcInput is Dictionary<string, object> argsDict
                                ? JsonHelper.ToJson(argsDict)
                                : "{}";

                            toolCalls.Add(new Dictionary<string, object>
                            {
                                ["id"] = tcId,
                                ["type"] = "function",
                                ["function"] = new Dictionary<string, object>
                                {
                                    ["name"] = tcName,
                                    ["arguments"] = argsJson
                                }
                            });
                            break;

                        case "tool_result":
                            string trId = block.TryGetValue("tool_use_id", out var trid) ? trid?.ToString() : "";
                            string trContent = block.TryGetValue("content", out var trc) ? trc?.ToString() : "";
                            toolResults.Add(new Dictionary<string, object>
                            {
                                ["role"] = "tool",
                                ["tool_call_id"] = trId,
                                ["content"] = trContent
                            });
                            break;
                    }
                }

                // Emit appropriate OpenAI messages
                if (role == "assistant")
                {
                    var assistantMsg = new Dictionary<string, object>
                    {
                        ["role"] = "assistant"
                    };
                    string textStr = textParts.ToString();
                    assistantMsg["content"] = string.IsNullOrEmpty(textStr) ? null : (object)textStr;

                    if (toolCalls.Count > 0)
                        assistantMsg["tool_calls"] = toolCalls;

                    result.Add(assistantMsg);
                }
                else if (toolResults.Count > 0)
                {
                    // Tool results become separate "tool" role messages in OpenAI
                    foreach (var tr in toolResults)
                        result.Add(tr);
                }
                else
                {
                    // Regular user message — flatten content to string
                    result.Add(new Dictionary<string, object>
                    {
                        ["role"] = role,
                        ["content"] = textParts.ToString()
                    });
                }
            }

            return result;
        }

        // ====================================================================
        // SSE Event Processing (OpenAI flat format)
        // ====================================================================

        public void ProcessSseEvent(SseEvent evt, StreamingState state,
                                    Action<string> onTextDelta,
                                    Action<ToolUseContent> onToolCallStarted,
                                    Action<UsageInfo> onUsageUpdated,
                                    Action<string> onError)
        {
            if (string.IsNullOrEmpty(evt.data)) return;

            // OpenAI signals end with [DONE]
            if (evt.data == "[DONE]")
            {
                state.isComplete = true;
                return;
            }

            Dictionary<string, object> data;
            try
            {
                data = JsonHelper.ParseJsonObject(evt.data);
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat][OpenAI] SSE parse error: {ex.Message}");
                return;
            }
            if (data == null) return;

            // Check for error response
            if (data.TryGetValue("error", out var errObj) && errObj is Dictionary<string, object> errDict)
            {
                string errMsg = errDict.TryGetValue("message", out var em) ? em?.ToString() : "Unknown error";
                state.error = errMsg;
                onError?.Invoke(errMsg);
                return;
            }

            // Extract id and model from first chunk
            if (state.messageId == null && data.TryGetValue("id", out var id))
                state.messageId = id?.ToString();
            if (state.model == null && data.TryGetValue("model", out var model))
                state.model = model?.ToString();

            // Process usage (may appear in final chunk or standalone)
            if (data.TryGetValue("usage", out var usageObj) && usageObj is Dictionary<string, object> usage)
            {
                state.usage = new UsageInfo
                {
                    input_tokens = usage.TryGetValue("prompt_tokens", out var pt) ? Convert.ToInt32(pt) : 0,
                    output_tokens = usage.TryGetValue("completion_tokens", out var ct) ? Convert.ToInt32(ct) : 0
                };
                onUsageUpdated?.Invoke(state.usage);
            }

            // Process choices
            if (!data.TryGetValue("choices", out var choicesObj) || !(choicesObj is List<object> choices) || choices.Count == 0)
                return;

            var choice = choices[0] as Dictionary<string, object>;
            if (choice == null) return;

            // Check finish_reason
            if (choice.TryGetValue("finish_reason", out var fr) && fr != null)
            {
                string reason = fr.ToString();
                // Normalize: OpenAI "tool_calls" → "tool_use" (Anthropic convention used by our code)
                state.stopReason = reason == "tool_calls" ? "tool_use" : reason;
            }

            // Process delta
            if (!choice.TryGetValue("delta", out var deltaObj) || !(deltaObj is Dictionary<string, object> delta))
                return;

            // Text content
            if (delta.TryGetValue("content", out var content) && content != null)
            {
                string text = content.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    // Ensure we have a text content block
                    if (state.contentBlocks.Count == 0 ||
                        !(state.contentBlocks[state.contentBlocks.Count - 1] is TextContent))
                    {
                        state.contentBlocks.Add(new TextContent(""));
                        state.currentBlockIndex = state.contentBlocks.Count - 1;
                    }

                    var textBlock = (TextContent)state.contentBlocks[state.currentBlockIndex];
                    if (textBlock.textBuilder == null)
                        textBlock.textBuilder = new System.Text.StringBuilder(textBlock.text ?? "");
                    textBlock.textBuilder.Append(text);
                    onTextDelta?.Invoke(text);
                }
            }

            // Tool calls
            if (delta.TryGetValue("tool_calls", out var toolCallsObj) && toolCallsObj is List<object> toolCalls)
            {
                foreach (var tcObj in toolCalls)
                {
                    if (!(tcObj is Dictionary<string, object> tc)) continue;

                    int tcIndex = tc.TryGetValue("index", out var tcIdx) ? Convert.ToInt32(tcIdx) : 0;

                    // New tool call (has id)
                    if (tc.TryGetValue("id", out var tcId) && tcId != null)
                    {
                        string fnName = "";
                        if (tc.TryGetValue("function", out var fnObj) && fnObj is Dictionary<string, object> fn)
                            fnName = fn.TryGetValue("name", out var fnN) ? fnN?.ToString() : "";

                        var toolUse = new ToolUseContent
                        {
                            id = tcId.ToString(),
                            name = fnName,
                            rawJsonBuilder = new StringBuilder()
                        };
                        state.contentBlocks.Add(toolUse);
                        state.ToolIndexMap[tcIndex] = state.contentBlocks.Count - 1;
                        onToolCallStarted?.Invoke(toolUse);
                    }

                    // Argument delta
                    if (tc.TryGetValue("function", out var fnObj2) && fnObj2 is Dictionary<string, object> fn2)
                    {
                        if (fn2.TryGetValue("arguments", out var argsChunk) && argsChunk != null)
                        {
                            string chunk = argsChunk.ToString();
                            if (!string.IsNullOrEmpty(chunk) && state.ToolIndexMap.TryGetValue(tcIndex, out int blockIdx))
                            {
                                if (blockIdx < state.contentBlocks.Count &&
                                    state.contentBlocks[blockIdx] is ToolUseContent targetTool &&
                                    targetTool.rawJsonBuilder != null)
                                {
                                    targetTool.rawJsonBuilder.Append(chunk);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
