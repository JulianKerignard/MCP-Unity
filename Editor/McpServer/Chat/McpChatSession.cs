using System;
using System.Collections.Generic;
using McpUnity.Server;
using UnityEditor;

namespace McpUnity.Chat
{
    /// <summary>
    /// Handles persistence of chat state to Unity's SessionState (survives domain reloads).
    /// Manages save/restore of display entries, conversation context, token counts,
    /// and interrupted execution state for resumption after compilation.
    /// </summary>
    internal static class McpChatSession
    {
        // SessionState keys
        private const string ChatHistorySessionKey = "McpUnity_ChatDisplayHistory";
        private const string ChatTokensSessionKey = "McpUnity_ChatTokens";
        private const string ConversationStateKey = "McpUnity_ChatConversation";
        internal const string InterruptedStateKey = "McpUnity_ChatInterruptedState";

        // Limits
        private const int MaxPersistedEntries = 50;
        private const int MaxPersistedConversationMessages = 30;

        // ====================================================================
        // Save
        // ====================================================================

        /// <summary>
        /// Persist display entries, token counts, and conversation to SessionState.
        /// </summary>
        public static void Save(
            List<ChatDisplayEntry> displayEntries,
            List<ChatMessage> conversation,
            int totalInputTokens,
            int totalOutputTokens)
        {
            try
            {
                SaveDisplayEntries(displayEntries);
                SessionState.SetString(ChatTokensSessionKey, $"{totalInputTokens},{totalOutputTokens}");
                SaveConversation(conversation);
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to save session state: {ex.Message}");
            }
        }

        private static void SaveDisplayEntries(List<ChatDisplayEntry> displayEntries)
        {
            var entries = new List<object>();
            int start = Math.Max(0, displayEntries.Count - MaxPersistedEntries);
            for (int i = start; i < displayEntries.Count; i++)
            {
                var e = displayEntries[i];
                if (e.isStreaming) continue;
                var dict = new Dictionary<string, object>
                {
                    ["t"] = (int)e.type,
                    ["x"] = e.text ?? "",
                    ["n"] = e.toolName ?? "",
                    ["ts"] = e.timestamp
                };
                // Persist full tool result text for expand/collapse across domain reloads
                if (!string.IsNullOrEmpty(e.fullText))
                    dict["f"] = e.fullText;
                entries.Add(dict);
            }
            SessionState.SetString(ChatHistorySessionKey, JsonHelper.ToJson(entries));
        }

        private static void SaveConversation(List<ChatMessage> conversation)
        {
            var serialized = new List<object>();
            int msgStart = Math.Max(0, conversation.Count - MaxPersistedConversationMessages);

            // Never start on a user message that contains tool_result blocks,
            // as the preceding assistant message with tool_use would be missing.
            // Walk forward to find a clean boundary (a plain user text message).
            while (msgStart < conversation.Count)
            {
                var msg = conversation[msgStart];
                bool hasToolResult = false;
                foreach (var block in msg.content)
                {
                    if (block is ToolResultContent) { hasToolResult = true; break; }
                }
                if (!hasToolResult) break;
                msgStart++;
            }

            // Also ensure we don't start on an assistant message (API requires user first)
            while (msgStart < conversation.Count && conversation[msgStart].role != "user")
                msgStart++;

            for (int i = msgStart; i < conversation.Count; i++)
            {
                var msg = conversation[i];
                var blocks = new List<object>();

                foreach (var block in msg.content)
                {
                    switch (block)
                    {
                        case TextContent tc:
                            blocks.Add(new Dictionary<string, object>
                            {
                                ["type"] = "text",
                                ["text"] = tc.GetText()
                            });
                            break;
                        case ToolUseContent tu:
                            blocks.Add(new Dictionary<string, object>
                            {
                                ["type"] = "tool_use",
                                ["id"] = tu.id ?? "",
                                ["name"] = tu.name ?? "",
                                ["input"] = tu.input ?? new Dictionary<string, object>()
                            });
                            break;
                        case ToolResultContent tr:
                            blocks.Add(new Dictionary<string, object>
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = tr.tool_use_id ?? "",
                                ["content"] = tr.content ?? "",
                                ["is_error"] = tr.is_error
                            });
                            break;
                    }
                }

                serialized.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.role ?? "user",
                    ["content"] = blocks,
                    ["timestamp"] = msg.timestamp
                });
            }

            SessionState.SetString(ConversationStateKey, JsonHelper.ToJson(serialized));
        }

        // ====================================================================
        // Restore
        // ====================================================================

        /// <summary>
        /// Restore display entries, token counts, and conversation from SessionState.
        /// Returns true if display entries were restored.
        /// </summary>
        public static bool Restore(
            List<ChatDisplayEntry> displayEntries,
            List<ChatMessage> conversation,
            out int totalInputTokens,
            out int totalOutputTokens,
            out bool conversationRestored)
        {
            totalInputTokens = 0;
            totalOutputTokens = 0;
            conversationRestored = false;

            try
            {
                // Restore display entries
                string json = SessionState.GetString(ChatHistorySessionKey, "");
                if (string.IsNullOrEmpty(json)) return false;

                var parsed = JsonHelper.ParseJsonObject($"{{\"a\":{json}}}");
                if (parsed is Dictionary<string, object> wrapper
                    && wrapper.TryGetValue("a", out var arrObj)
                    && arrObj is List<object> arr)
                {
                    displayEntries.Clear();
                    foreach (var item in arr)
                    {
                        if (item is Dictionary<string, object> d)
                        {
                            var entry = new ChatDisplayEntry
                            {
                                type = (ChatDisplayEntry.EntryType)(d.TryGetValue("t", out var tv) ? Convert.ToInt32(tv) : 0),
                                text = d.TryGetValue("x", out var xv) ? xv?.ToString() ?? "" : "",
                                toolName = d.TryGetValue("n", out var nv) ? nv?.ToString() ?? "" : "",
                                timestamp = d.TryGetValue("ts", out var tsv) ? Convert.ToInt64(tsv) : 0
                            };
                            if (d.TryGetValue("f", out var fv) && fv != null)
                                entry.fullText = fv.ToString();
                            entry.UpdateTokenEstimate();
                            displayEntries.Add(entry);
                        }
                    }
                }

                // Restore token counts
                string tokens = SessionState.GetString(ChatTokensSessionKey, "");
                if (!string.IsNullOrEmpty(tokens))
                {
                    var parts = tokens.Split(',');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out totalInputTokens);
                        int.TryParse(parts[1], out totalOutputTokens);
                    }
                }

                // Restore conversation context
                conversationRestored = RestoreConversation(conversation);

                return displayEntries.Count > 0;
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to restore session state: {ex.Message}");
                return false;
            }
        }

        private static bool RestoreConversation(List<ChatMessage> conversation)
        {
            try
            {
                string json = SessionState.GetString(ConversationStateKey, "");
                if (string.IsNullOrEmpty(json)) return false;

                var parsed = JsonHelper.ParseJsonObject($"{{\"a\":{json}}}");
                if (!(parsed is Dictionary<string, object> wrapper
                    && wrapper.TryGetValue("a", out var arrObj)
                    && arrObj is List<object> arr))
                    return false;

                conversation.Clear();
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> msgDict)) continue;

                    var msg = new ChatMessage();
                    msg.role = msgDict.TryGetValue("role", out var rv) ? rv?.ToString() ?? "user" : "user";
                    msg.timestamp = msgDict.TryGetValue("timestamp", out var tsv) ? Convert.ToInt64(tsv) : 0;
                    msg.content = new List<ContentBlock>();

                    if (msgDict.TryGetValue("content", out var cv) && cv is List<object> blocks)
                    {
                        foreach (var blockItem in blocks)
                        {
                            if (!(blockItem is Dictionary<string, object> blockDict)) continue;

                            string blockType = blockDict.TryGetValue("type", out var btv) ? btv?.ToString() : "";
                            switch (blockType)
                            {
                                case "text":
                                    msg.content.Add(new TextContent
                                    {
                                        text = blockDict.TryGetValue("text", out var txv) ? txv?.ToString() ?? "" : ""
                                    });
                                    break;

                                case "tool_use":
                                    msg.content.Add(new ToolUseContent
                                    {
                                        id = blockDict.TryGetValue("id", out var idv) ? idv?.ToString() ?? "" : "",
                                        name = blockDict.TryGetValue("name", out var nmv) ? nmv?.ToString() ?? "" : "",
                                        input = blockDict.TryGetValue("input", out var inv) && inv is Dictionary<string, object> inputDict
                                            ? inputDict : new Dictionary<string, object>()
                                    });
                                    break;

                                case "tool_result":
                                    msg.content.Add(new ToolResultContent
                                    {
                                        tool_use_id = blockDict.TryGetValue("tool_use_id", out var tuiv) ? tuiv?.ToString() ?? "" : "",
                                        content = blockDict.TryGetValue("content", out var ctv) ? ctv?.ToString() ?? "" : "",
                                        is_error = blockDict.TryGetValue("is_error", out var iev) && iev is bool ieb && ieb
                                    });
                                    break;
                            }
                        }
                    }

                    conversation.Add(msg);
                }

                return conversation.Count > 0;
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to restore conversation: {ex.Message}");
                return false;
            }
        }

        // ====================================================================
        // Domain Reload / Interrupted State
        // ====================================================================

        /// <summary>
        /// Save the interrupted execution state before domain reload.
        /// Called from AssemblyReloadEvents.beforeAssemblyReload.
        /// </summary>
        public static void SaveInterruptedState(
            List<ToolUseContent> pendingToolBlocks,
            int toolExecIndex,
            ChatMessage pendingToolResultMessage,
            McpChatApiClient apiClient)
        {
            var state = new Dictionary<string, object>();

            if (pendingToolBlocks != null && toolExecIndex < pendingToolBlocks.Count)
            {
                // Tool execution was in progress
                state["type"] = "tools";
                state["index"] = toolExecIndex;
                state["total"] = pendingToolBlocks.Count;

                // Save partial tool results collected so far
                var results = new List<object>();
                if (pendingToolResultMessage?.content != null)
                {
                    foreach (var block in pendingToolResultMessage.content)
                    {
                        if (block is ToolResultContent tr)
                        {
                            results.Add(new Dictionary<string, object>
                            {
                                ["tool_use_id"] = tr.tool_use_id ?? "",
                                ["content"] = tr.content ?? "",
                                ["is_error"] = tr.is_error
                            });
                        }
                    }
                }
                state["results"] = results;
            }
            else if (apiClient != null && apiClient.IsProcessing)
            {
                // Streaming response was in progress
                state["type"] = "streaming";
            }
            else
            {
                state["type"] = "none";
            }

            SessionState.SetString(InterruptedStateKey, JsonHelper.ToJson(state));
        }

        /// <summary>
        /// Load the interrupted state saved before domain reload.
        /// Returns the parsed state dictionary, or null if none was saved.
        /// </summary>
        public static Dictionary<string, object> LoadInterruptedState()
        {
            string json = SessionState.GetString(InterruptedStateKey, "");
            SessionState.EraseString(InterruptedStateKey);

            if (string.IsNullOrEmpty(json)) return null;

            var parsed = JsonHelper.ParseJsonObject(json);
            if (!(parsed is Dictionary<string, object> state)) return null;

            string savedType = state.TryGetValue("type", out var tv) ? tv?.ToString() : "none";
            if (savedType == "none") return null;

            return state;
        }

        /// <summary>
        /// Clear all session data (called on ClearChat).
        /// </summary>
        public static void Clear()
        {
            SessionState.EraseString(ChatHistorySessionKey);
            SessionState.EraseString(ChatTokensSessionKey);
            SessionState.EraseString(ConversationStateKey);
        }

        // ====================================================================
        // Export
        // ====================================================================

        /// <summary>
        /// Export conversation to file in the specified format.
        /// </summary>
        public static void ExportToFile(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens,
            string format)
        {
            string ext = format;
            string defaultName = $"chat-export-{DateTime.Now:yyyy-MM-dd-HHmm}.{ext}";

            string path = EditorUtility.SaveFilePanel($"Export Conversation ({format.ToUpper()})", "", defaultName, ext);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string content = BuildExportContent(displayEntries, totalInputTokens, totalOutputTokens, format);
                System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Failed to export conversation:\n{ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Copy conversation to clipboard in the specified format.
        /// </summary>
        public static void CopyToClipboard(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens,
            string format)
        {
            string content = BuildExportContent(displayEntries, totalInputTokens, totalOutputTokens, format);
            EditorGUIUtility.systemCopyBuffer = content;
        }

        /// <summary>
        /// Build export content in the specified format.
        /// </summary>
        public static string BuildExportContent(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens,
            string format)
        {
            switch (format)
            {
                case "json": return BuildJsonExport(displayEntries, totalInputTokens, totalOutputTokens);
                case "md":   return BuildMarkdownExport(displayEntries, totalInputTokens, totalOutputTokens);
                case "txt":  return BuildTextExport(displayEntries, totalInputTokens, totalOutputTokens);
                default:     return BuildTextExport(displayEntries, totalInputTokens, totalOutputTokens);
            }
        }

        private static string BuildJsonExport(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens)
        {
            var export = new Dictionary<string, object>();
            var provider = Providers.ProviderRegistry.GetActiveProvider();
            // FIX-#102: guard against null provider (no active provider configured).
            string model = provider != null ? Providers.ProviderRegistry.GetModel(provider.Id) : null;

            export["exportedAt"] = DateTime.UtcNow.ToString("o");
            export["provider"] = provider?.DisplayName ?? "(none)";
            export["model"] = model ?? "(unset)";
            export["totalInputTokens"] = totalInputTokens;
            export["totalOutputTokens"] = totalOutputTokens;

            var messages = new List<object>();
            foreach (var entry in displayEntries)
            {
                if (entry.type == ChatDisplayEntry.EntryType.System && entry.text.StartsWith("MCP Unity Chat"))
                    continue;

                var msg = new Dictionary<string, object>
                {
                    ["type"] = entry.type.ToString().ToLower(),
                    ["text"] = entry.fullText ?? entry.text ?? "",
                    ["timestamp"] = entry.timestamp
                };
                if (!string.IsNullOrEmpty(entry.toolName))
                    msg["toolName"] = entry.toolName;
                messages.Add(msg);
            }
            export["messages"] = messages;

            return JsonHelper.ToJson(export);
        }

        private static string BuildMarkdownExport(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens)
        {
            var sb = new System.Text.StringBuilder();
            var provider = Providers.ProviderRegistry.GetActiveProvider();
            // FIX-#102: guard against null provider.
            string model = provider != null ? Providers.ProviderRegistry.GetModel(provider.Id) : "(unset)";
            string providerName = provider?.DisplayName ?? "(none)";

            sb.AppendLine($"# Chat Export — {providerName} / {model}");
            sb.AppendLine($"*Exported: {DateTime.Now:yyyy-MM-dd HH:mm}*");
            sb.AppendLine($"*Tokens: {totalInputTokens} in / {totalOutputTokens} out*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var entry in displayEntries)
            {
                if (entry.type == ChatDisplayEntry.EntryType.System && entry.text.StartsWith("MCP Unity Chat"))
                    continue;

                var ts = DateTimeOffset.FromUnixTimeMilliseconds(entry.timestamp).LocalDateTime;
                string time = ts.ToString("HH:mm:ss");

                switch (entry.type)
                {
                    case ChatDisplayEntry.EntryType.User:
                        sb.AppendLine($"## You ({time})");
                        sb.AppendLine();
                        sb.AppendLine(entry.text);
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.Assistant:
                        sb.AppendLine($"## Assistant ({time})");
                        sb.AppendLine();
                        sb.AppendLine(entry.text);
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.ToolCall:
                        sb.AppendLine($"### Tool Call: `{entry.toolName}` ({time})");
                        sb.AppendLine();
                        sb.AppendLine("```json");
                        sb.AppendLine(entry.text);
                        sb.AppendLine("```");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.ToolResult:
                        sb.AppendLine($"### Result: `{entry.toolName}`");
                        sb.AppendLine();
                        string resultText = entry.fullText ?? entry.text ?? "";
                        if (resultText.Length > 2000)
                            resultText = resultText.Substring(0, 2000) + "\n...(truncated)";
                        sb.AppendLine("```");
                        sb.AppendLine(resultText);
                        sb.AppendLine("```");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.Error:
                        sb.AppendLine($"> **Error** ({time}): {entry.text}");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.System:
                        sb.AppendLine($"> *{entry.text}*");
                        sb.AppendLine();
                        break;
                }
            }

            return sb.ToString();
        }

        private static string BuildTextExport(
            List<ChatDisplayEntry> displayEntries,
            int totalInputTokens,
            int totalOutputTokens)
        {
            var sb = new System.Text.StringBuilder();
            var provider = Providers.ProviderRegistry.GetActiveProvider();
            // FIX-#102: guard against null provider.
            string model = provider != null ? Providers.ProviderRegistry.GetModel(provider.Id) : "(unset)";
            string providerName = provider?.DisplayName ?? "(none)";

            sb.AppendLine($"Chat Export — {providerName} / {model}");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Tokens: {totalInputTokens} in / {totalOutputTokens} out");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            foreach (var entry in displayEntries)
            {
                if (entry.type == ChatDisplayEntry.EntryType.System && entry.text.StartsWith("MCP Unity Chat"))
                    continue;

                var ts = DateTimeOffset.FromUnixTimeMilliseconds(entry.timestamp).LocalDateTime;
                string time = ts.ToString("HH:mm:ss");

                switch (entry.type)
                {
                    case ChatDisplayEntry.EntryType.User:
                        sb.AppendLine($"[{time}] YOU:");
                        sb.AppendLine(entry.text);
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.Assistant:
                        sb.AppendLine($"[{time}] ASSISTANT:");
                        sb.AppendLine(StripRichText(entry.text));
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.ToolCall:
                        sb.AppendLine($"[{time}] TOOL CALL: {entry.toolName}");
                        sb.AppendLine($"  {entry.text}");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.ToolResult:
                        string resultText = entry.fullText ?? entry.text ?? "";
                        if (resultText.Length > 2000)
                            resultText = resultText.Substring(0, 2000) + "...(truncated)";
                        sb.AppendLine($"  RESULT ({entry.toolName}): {resultText}");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.Error:
                        sb.AppendLine($"[{time}] ERROR: {entry.text}");
                        sb.AppendLine();
                        break;

                    case ChatDisplayEntry.EntryType.System:
                        sb.AppendLine($"--- {entry.text} ---");
                        sb.AppendLine();
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>Strip Unity rich text tags for plain text export.</summary>
        internal static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return System.Text.RegularExpressions.Regex.Replace(text, @"<\/?(?:b|i|color|size)[^>]*>", "");
        }
    }
}
