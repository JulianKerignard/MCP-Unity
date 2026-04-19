using System;
using System.Collections.Generic;
using System.Text;
using McpUnity.Server;
using UnityEngine.Networking;

namespace McpUnity.Chat.Providers
{
    /// <summary>
    /// Anthropic Claude API provider.
    /// Handles the Messages API with semantic SSE events.
    /// </summary>
    public class AnthropicProvider : IChatProvider
    {
        private readonly ProviderPreset _preset;
        private const string ApiVersion = "2023-06-01";

        public AnthropicProvider(ProviderPreset preset)
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
        public bool SupportsOAuth => true;  // Anthropic-specific capability
        public bool SupportsTools => _preset.supportsTools;

        public AuthResult ResolveAuth(string storedApiKey)
        {
            // Respect the user's auth mode selection
            var authMode = McpChatApiClient.ActiveAuthMode;

            if (authMode == AuthMode.OAuthToken)
            {
                // OAuth-first: try OAuth, then fallback to API key
                var oauthResult = TryOAuth();
                if (oauthResult.IsValid) return oauthResult;

                // Fallback to API key
                return TryApiKey(storedApiKey);
            }
            else
            {
                // API Key-first: try stored/env key, only use OAuth if no key available
                var keyResult = TryApiKey(storedApiKey);
                if (keyResult.IsValid) return keyResult;

                // Fallback to OAuth if available
                return TryOAuth();
            }
        }

        private AuthResult TryOAuth()
        {
            if (McpChatOAuth.HasValidToken)
                return new AuthResult
                {
                    IsValid = true,
                    HeaderName = "Authorization",
                    HeaderValue = "Bearer " + McpChatOAuth.AccessToken,
                    Source = "Claude Account (OAuth)"
                };

            if (McpChatOAuth.HasRefreshToken)
                return new AuthResult
                {
                    IsValid = true,
                    HeaderName = "Authorization",
                    HeaderValue = "",
                    Source = "Claude Account (token expired, will refresh)",
                    NeedsRefresh = true
                };

            return new AuthResult { IsValid = false, Source = "None" };
        }

        private AuthResult TryApiKey(string storedApiKey)
        {
            if (!string.IsNullOrEmpty(storedApiKey))
                return new AuthResult
                {
                    IsValid = true,
                    HeaderName = "x-api-key",
                    HeaderValue = storedApiKey,
                    Source = "API Key (EditorPrefs)"
                };

            string envKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
            if (!string.IsNullOrEmpty(envKey))
                return new AuthResult
                {
                    IsValid = true,
                    HeaderName = "x-api-key",
                    HeaderValue = envKey,
                    Source = $"API Key (env ${ApiKeyEnvVar})"
                };

            return new AuthResult { IsValid = false, Source = "None" };
        }

        public string GetEndpointUrl(string model, string customEndpoint)
        {
            return string.IsNullOrEmpty(customEndpoint) ? _preset.defaultEndpoint : customEndpoint;
        }

        public void ConfigureRequest(UnityWebRequest request, AuthResult auth, string model)
        {
            request.SetRequestHeader(auth.HeaderName, auth.HeaderValue);
            request.SetRequestHeader("anthropic-version", ApiVersion);
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("accept", "text/event-stream");

            // OAuth requires beta header (any Bearer token, not just when refresh token is present)
            if (auth.HeaderName == "Authorization")
                request.SetRequestHeader("anthropic-beta", McpChatOAuth.OAuthBetaHeader);
        }

        public byte[] BuildRequestBody(string systemPrompt, List<object> messages,
                                        List<object> tools, string model, int maxTokens)
        {
            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = maxTokens,
                ["stream"] = true,
                ["messages"] = messages // Anthropic format = native, no conversion needed
            };

            // Temperature (Anthropic supports 0.0 - 1.0)
            float temperature = ProviderRegistry.GetTemperature(_preset.id);
            if (temperature < 1f) // Only include if non-default (Anthropic default = 1.0)
                body["temperature"] = System.Math.Round(temperature, 2);

            if (!string.IsNullOrEmpty(systemPrompt))
                body["system"] = systemPrompt;

            if (tools != null && tools.Count > 0)
                body["tools"] = tools; // MCP format = Anthropic format

            return Encoding.UTF8.GetBytes(JsonHelper.ToJson(body));
        }

        public List<object> ConvertToolDefinitions(List<object> mcpToolDefs)
        {
            // MCP format IS Anthropic format — pass through
            return mcpToolDefs;
        }

        public List<object> ConvertMessages(List<object> anthropicMessages, string systemPrompt)
        {
            // Already in Anthropic format — pass through
            return anthropicMessages;
        }

        // ====================================================================
        // SSE Event Processing (Anthropic semantic events)
        // ====================================================================

        public void ProcessSseEvent(SseEvent evt, StreamingState state,
                                    Action<string> onTextDelta,
                                    Action<ToolUseContent> onToolCallStarted,
                                    Action<UsageInfo> onUsageUpdated,
                                    Action<string> onError)
        {
            if (string.IsNullOrEmpty(evt.data) || evt.data == "[DONE]") return;

            try
            {
                var data = JsonHelper.ParseJsonObject(evt.data);
                if (data == null) return;

                switch (evt.eventType)
                {
                    case "message_start":
                        HandleMessageStart(data, state);
                        break;
                    case "content_block_start":
                        HandleContentBlockStart(data, state, onToolCallStarted);
                        break;
                    case "content_block_delta":
                        HandleContentBlockDelta(data, state, onTextDelta);
                        break;
                    case "content_block_stop":
                        break;
                    case "message_delta":
                        HandleMessageDelta(data, state, onUsageUpdated);
                        break;
                    case "message_stop":
                        state.isComplete = true;
                        break;
                    case "error":
                        if (data.TryGetValue("error", out var errObj) && errObj is Dictionary<string, object> errDict)
                            state.error = errDict.TryGetValue("message", out var msg) ? msg?.ToString() : "Unknown streaming error";
                        onError?.Invoke(state.error ?? "Streaming error");
                        break;
                }
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat][Anthropic] SSE parse error: {ex.Message}");
            }
        }

        private void HandleMessageStart(Dictionary<string, object> data, StreamingState state)
        {
            if (!data.TryGetValue("message", out var msgObj) || !(msgObj is Dictionary<string, object> msg))
                return;

            if (msg.TryGetValue("id", out var id)) state.messageId = id?.ToString();
            if (msg.TryGetValue("model", out var model)) state.model = model?.ToString();
            if (msg.TryGetValue("usage", out var usageObj) && usageObj is Dictionary<string, object> usage)
            {
                state.usage = new UsageInfo
                {
                    input_tokens = usage.TryGetValue("input_tokens", out var inp) ? Convert.ToInt32(inp) : 0,
                    output_tokens = usage.TryGetValue("output_tokens", out var outp) ? Convert.ToInt32(outp) : 0
                };
            }
        }

        private void HandleContentBlockStart(Dictionary<string, object> data, StreamingState state,
                                             Action<ToolUseContent> onToolCallStarted)
        {
            if (!data.TryGetValue("content_block", out var blockObj) || !(blockObj is Dictionary<string, object> block))
                return;

            string blockType = block.TryGetValue("type", out var t) ? t?.ToString() : "";
            if (data.TryGetValue("index", out var idx))
                state.currentBlockIndex = Convert.ToInt32(idx);

            switch (blockType)
            {
                case "text":
                    string initialText = block.TryGetValue("text", out var txt) ? txt?.ToString() : "";
                    state.contentBlocks.Add(new TextContent(initialText));
                    break;
                case "tool_use":
                    var toolUse = new ToolUseContent
                    {
                        id = block.TryGetValue("id", out var tid) ? tid?.ToString() : "",
                        name = block.TryGetValue("name", out var name) ? name?.ToString() : ""
                    };
                    state.contentBlocks.Add(toolUse);
                    onToolCallStarted?.Invoke(toolUse);
                    break;
            }
        }

        private void HandleContentBlockDelta(Dictionary<string, object> data, StreamingState state,
                                             Action<string> onTextDelta)
        {
            if (!data.TryGetValue("delta", out var deltaObj) || !(deltaObj is Dictionary<string, object> delta))
                return;

            string deltaType = delta.TryGetValue("type", out var dt) ? dt?.ToString() : "";
            int index = data.TryGetValue("index", out var idx) ? Convert.ToInt32(idx) : state.currentBlockIndex;
            if (index < 0 || index >= state.contentBlocks.Count) return;

            switch (deltaType)
            {
                case "text_delta":
                    if (state.contentBlocks[index] is TextContent textBlock)
                    {
                        string chunk = delta.TryGetValue("text", out var txt) ? txt?.ToString() : "";
                        if (textBlock.textBuilder == null)
                            textBlock.textBuilder = new System.Text.StringBuilder(textBlock.text ?? "");
                        textBlock.textBuilder.Append(chunk);
                        onTextDelta?.Invoke(chunk);
                    }
                    break;
                case "input_json_delta":
                    if (state.contentBlocks[index] is ToolUseContent toolBlock)
                    {
                        string partial = delta.TryGetValue("partial_json", out var pj) ? pj?.ToString() : "";
                        if (toolBlock.rawJsonBuilder == null)
                            toolBlock.rawJsonBuilder = new StringBuilder();
                        toolBlock.rawJsonBuilder.Append(partial);
                    }
                    break;
            }
        }

        private void HandleMessageDelta(Dictionary<string, object> data, StreamingState state,
                                        Action<UsageInfo> onUsageUpdated)
        {
            if (data.TryGetValue("delta", out var deltaObj) && deltaObj is Dictionary<string, object> delta)
            {
                if (delta.TryGetValue("stop_reason", out var sr))
                    state.stopReason = sr?.ToString();
            }

            if (data.TryGetValue("usage", out var usageObj) && usageObj is Dictionary<string, object> usage)
            {
                int outputTokens = usage.TryGetValue("output_tokens", out var outp) ? Convert.ToInt32(outp) : 0;
                if (state.usage != null)
                {
                    state.usage.output_tokens = outputTokens;
                    onUsageUpdated?.Invoke(state.usage);
                }
            }
        }
    }
}
