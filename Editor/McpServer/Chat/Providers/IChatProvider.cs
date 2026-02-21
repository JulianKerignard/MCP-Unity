using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace McpUnity.Chat.Providers
{
    /// <summary>
    /// Abstraction for LLM API providers (Anthropic, OpenAI, Gemini, etc.).
    /// Each provider handles request building, SSE event parsing, and tool format conversion.
    /// </summary>
    public interface IChatProvider
    {
        // ---- Identity ----
        string Id { get; }
        string DisplayName { get; }
        string[] ModelIds { get; }
        string[] ModelLabels { get; }
        string DefaultModel { get; }
        int DefaultMaxTokens { get; }
        int MaxContextTokens { get; }

        // ---- Capabilities ----
        string ApiKeyEnvVar { get; }
        bool SupportsOAuth { get; }
        bool SupportsTools { get; }

        /// <summary>Resolve authentication from stored key + env var.</summary>
        AuthResult ResolveAuth(string storedApiKey);

        // ---- Request Building ----

        /// <summary>Get the API endpoint URL, optionally using a custom endpoint override.</summary>
        string GetEndpointUrl(string model, string customEndpoint);

        /// <summary>Set provider-specific request headers (auth, version, content-type, etc.).</summary>
        void ConfigureRequest(UnityWebRequest request, AuthResult auth, string model);

        /// <summary>Build the full JSON request body as bytes, converting messages and tools to provider format.</summary>
        byte[] BuildRequestBody(string systemPrompt, List<object> messages,
                                List<object> tools, string model, int maxTokens);

        // ---- SSE Event Processing ----

        /// <summary>
        /// Process a single SSE event from the streaming response.
        /// Must update StreamingState and invoke callbacks appropriately.
        /// Important: normalize stop_reason to "tool_use" when the provider signals tool calls.
        /// </summary>
        void ProcessSseEvent(SseEvent evt, StreamingState state,
                             Action<string> onTextDelta,
                             Action<ToolUseContent> onToolCallStarted,
                             Action<UsageInfo> onUsageUpdated,
                             Action<string> onError);

        // ---- Tool Format ----

        /// <summary>
        /// Convert MCP-format tool definitions to this provider's format.
        /// Input is List of dicts in {name, description, input_schema} format.
        /// </summary>
        List<object> ConvertToolDefinitions(List<object> mcpToolDefs);

        /// <summary>
        /// Convert Anthropic-format messages to this provider's format.
        /// Input is List of dicts in {role, content: [{type, text/tool_use/tool_result}]} format.
        /// </summary>
        List<object> ConvertMessages(List<object> anthropicMessages, string systemPrompt);
    }
}
