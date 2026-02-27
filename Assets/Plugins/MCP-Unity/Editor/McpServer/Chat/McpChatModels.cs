using System;
using System.Collections.Generic;

namespace McpUnity.Chat
{
    // ========================================================================
    // Anthropic Messages API data models (C# representations)
    // ========================================================================

    #region Conversation Models

    /// <summary>
    /// A single message in the conversation (user or assistant).
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public string role; // "user" or "assistant"
        public List<ContentBlock> content = new List<ContentBlock>();
        public long timestamp;

        public ChatMessage() { }

        public ChatMessage(string role, string text)
        {
            this.role = role;
            this.content.Add(new TextContent(text));
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Get concatenated text from all text content blocks.
        /// </summary>
        public string GetText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var block in content)
            {
                if (block is TextContent tc)
                    sb.Append(tc.GetText());
            }
            return sb.ToString();
        }
    }

    #endregion

    #region Content Blocks

    /// <summary>
    /// Base class for content blocks in messages.
    /// </summary>
    [Serializable]
    public abstract class ContentBlock
    {
        public abstract string type { get; }
    }

    [Serializable]
    public class TextContent : ContentBlock
    {
        public override string type => "text";
        public string text;

        /// <summary>
        /// StringBuilder for accumulating text during SSE streaming.
        /// Avoids O(n²) string concatenation on every delta chunk.
        /// </summary>
        [NonSerialized]
        public System.Text.StringBuilder textBuilder;

        public TextContent() { }
        public TextContent(string text) { this.text = text; }

        /// <summary>Materialize text from builder (call after streaming ends).</summary>
        public string GetText()
        {
            if (textBuilder != null && textBuilder.Length > 0)
            {
                text = textBuilder.ToString();
                textBuilder = null;
            }
            return text ?? "";
        }
    }

    [Serializable]
    public class ToolUseContent : ContentBlock
    {
        public override string type => "tool_use";
        public string id;
        public string name;
        public Dictionary<string, object> input = new Dictionary<string, object>();

        /// <summary>
        /// StringBuilder for accumulating raw JSON tool input during SSE streaming.
        /// Avoids string concatenation GC pressure on every delta chunk.
        /// </summary>
        [NonSerialized]
        public System.Text.StringBuilder rawJsonBuilder;
    }

    [Serializable]
    public class ToolResultContent : ContentBlock
    {
        public override string type => "tool_result";
        public string tool_use_id;
        public string content;
        public bool is_error;
    }

    #endregion

    #region API Types

    [Serializable]
    public class UsageInfo
    {
        public int input_tokens;
        public int output_tokens;
    }

    #endregion

    #region SSE Streaming Events

    /// <summary>
    /// Parsed SSE event from streaming response.
    /// </summary>
    public class SseEvent
    {
        public string eventType; // message_start, content_block_start, content_block_delta, etc.
        public string data;      // Raw JSON data
    }

    /// <summary>
    /// State tracker for an in-progress streaming response.
    /// </summary>
    public class StreamingState
    {
        public string messageId;
        public string model;
        public string stopReason;
        public UsageInfo usage;
        public List<ContentBlock> contentBlocks = new List<ContentBlock>();
        public int currentBlockIndex = -1;
        public bool isComplete;
        public string error;

        /// <summary>
        /// Maps OpenAI tool_calls[].index → contentBlocks index.
        /// Per-stream state (lives with the StreamingState, not the provider instance).
        /// </summary>
        public Dictionary<int, int> ToolIndexMap = new Dictionary<int, int>();

        /// <summary>Current text being built by streaming deltas.</summary>
        public string CurrentText
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var block in contentBlocks)
                {
                    if (block is TextContent tc)
                        sb.Append(tc.GetText());
                }
                return sb.ToString();
            }
        }

        /// <summary>Get all tool_use blocks from completed stream.</summary>
        public List<ToolUseContent> GetToolUseBlocks()
        {
            var result = new List<ToolUseContent>();
            foreach (var block in contentBlocks)
            {
                if (block is ToolUseContent tu)
                    result.Add(tu);
            }
            return result;
        }
    }

    #endregion

    #region Chat UI Models

    /// <summary>
    /// Display entry in the chat UI (can be user message, assistant message, or tool call).
    /// </summary>
    public class ChatDisplayEntry
    {
        public enum EntryType { User, Assistant, ToolCall, ToolResult, Error, System }

        public EntryType type;
        public string text;
        public string toolName;
        public bool isStreaming;
        public bool isWaitingForFirstChunk; // UX-01: true until first text delta arrives
        public long timestamp;

        /// <summary>Estimated token count for this entry (chars / 4 approximation).</summary>
        public int estimatedTokens;

        /// <summary>
        /// StringBuilder for accumulating text during SSE streaming.
        /// Avoids O(n²) string concatenation from += on every delta chunk.
        /// Materialized to 'text' when streaming ends.
        /// </summary>
        [NonSerialized]
        public System.Text.StringBuilder streamingBuilder;

        /// <summary>Cached parsed markdown segments (null until first render).</summary>
        [NonSerialized]
        public List<object> parsedSegments;

        /// <summary>
        /// Timestamp of the last markdown re-parse (EditorApplication.timeSinceStartup).
        /// Used to throttle re-parsing during SSE streaming to max 10Hz.
        /// </summary>
        [NonSerialized]
        public double lastParseTime;

        /// <summary>Whether this tool result is expanded to show full text.</summary>
        public bool isExpanded;

        /// <summary>Full text before display truncation (only set for tool results).</summary>
        public string fullText;

        public static ChatDisplayEntry UserMessage(string text)
        {
            return new ChatDisplayEntry
            {
                type = EntryType.User,
                text = text,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ChatDisplayEntry AssistantMessage(string text, bool streaming = false)
        {
            return new ChatDisplayEntry
            {
                type = EntryType.Assistant,
                text = text,
                isStreaming = streaming,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ChatDisplayEntry ToolCall(string toolName, string inputSummary)
        {
            return new ChatDisplayEntry
            {
                type = EntryType.ToolCall,
                toolName = toolName,
                text = inputSummary,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ChatDisplayEntry ToolResultEntry(string toolName, string result, bool isError)
        {
            return new ChatDisplayEntry
            {
                type = isError ? EntryType.Error : EntryType.ToolResult,
                toolName = toolName,
                text = result,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ChatDisplayEntry ErrorMessage(string text)
        {
            return new ChatDisplayEntry
            {
                type = EntryType.Error,
                text = text,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ChatDisplayEntry SystemMessage(string text)
        {
            return new ChatDisplayEntry
            {
                type = EntryType.System,
                text = text,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        /// <summary>Approximate token count: chars / 4.</summary>
        public static int EstimateTokens(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
        }

        /// <summary>Update the estimated token count from current text.</summary>
        public void UpdateTokenEstimate()
        {
            estimatedTokens = EstimateTokens(text);
        }
    }

    #endregion

    #region Auth

    /// <summary>
    /// Authentication mode for the Anthropic API.
    /// </summary>
    public enum AuthMode
    {
        /// <summary>Standard API key via x-api-key header.</summary>
        ApiKey = 0,
        /// <summary>OAuth bearer token via Authorization header (for subscription-based auth).</summary>
        OAuthToken = 1
    }

    /// <summary>
    /// Resolved authentication result with header info and source.
    /// </summary>
    public struct AuthResult
    {
        public bool IsValid;
        public string HeaderName;  // "x-api-key" or "Authorization"
        public string HeaderValue; // the key/token value (with "Bearer " prefix for OAuth)
        public string Source;      // human-readable source description
        public bool NeedsRefresh;  // true if OAuth token needs refresh before use
    }

    #endregion

    #region Asset References

    /// <summary>
    /// An asset or GameObject dragged into the chat input.
    /// </summary>
    public struct AssetReference
    {
        /// <summary>Display name shown in the chip and @mention.</summary>
        public string displayName;
        /// <summary>Asset path (Assets/...) or null for scene GameObjects.</summary>
        public string assetPath;
        /// <summary>Hierarchy path for scene GameObjects, null for project assets.</summary>
        public string gameObjectPath;
        /// <summary>True if this is a scene GameObject, false if it's a project asset.</summary>
        public bool isSceneObject;
        /// <summary>Asset type name (Material, Texture2D, etc.) or "GameObject".</summary>
        public string typeName;
    }

    #endregion

}
