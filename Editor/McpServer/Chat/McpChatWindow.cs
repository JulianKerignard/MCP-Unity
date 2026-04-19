using System;
using System.Collections.Generic;
using McpUnity.Chat.Providers;
using McpUnity.Helpers;
using McpUnity.Server;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Chat
{
    /// <summary>
    /// Embeddable chat panel for LLMs, rendered via IMGUI.
    /// Supports multiple providers (Anthropic, OpenAI, DeepSeek, Groq, Mistral, Ollama, etc.).
    /// Can be hosted inside McpEditorWindow as a tab, or in a standalone EditorWindow.
    ///
    /// UI styles are in McpChatStyles, session persistence in McpChatSession,
    /// and input handling in McpChatInput.
    /// </summary>
    public class McpChatPanel
    {
        // ====================================================================
        // Events
        // ====================================================================

        /// <summary>Fired when the user clicks the settings button. The host window should switch to the Settings tab.</summary>
        public event Action OnSettingsRequested;

        // ====================================================================
        // State
        // ====================================================================
        private McpChatApiClient _apiClient;
        // SEC-#429: tracked so we can dispose if the panel closes mid-compaction.
        private McpChatApiClient _compactApiClient;
        private McpChatToolBridge _toolBridge;
        private List<ChatMessage> _conversation = new List<ChatMessage>();
        private List<ChatDisplayEntry> _displayEntries = new List<ChatDisplayEntry>();
        private EditorWindow _hostWindow;
        private McpChatInput _inputHandler;

        // UI state
        private Vector2 _chatScrollPos;
        private bool _autoScroll = true;

        // Token tracking -- cumulative (for export/billing display)
        private int _totalInputTokens;
        private int _totalOutputTokens;
        // Token tracking -- last API response (for real context window usage)
        private int _lastInputTokens;
        private int _lastOutputTokens;

        // Copy feedback
        private double _copyFeedbackTime;
        private int _copyFeedbackIndex = -1;

        // Prefs
        private const string SystemPromptPref = "McpUnity_ChatSystemPrompt";

        // H3: Async tool execution state
        private int _toolExecIndex;
        private List<ToolUseContent> _pendingToolBlocks;
        private ChatMessage _pendingToolResultMessage;
        private bool _toolExecCancelled;
        // Epoch counter -- each new execution increments this; stale delayCall lambdas detect mismatch and bail
        private int _toolExecEpoch;

        // Compact state
        private bool _isCompacting;
        private const float CompactAutoThreshold = 0.70f; // Auto-suggest at 70%
        private bool _compactSuggested; // Avoids repeated suggestions in same session

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public void Initialize(EditorWindow host)
        {
            _hostWindow = host;

            _apiClient = new McpChatApiClient();
            _apiClient.OnTextDelta += HandleTextDelta;
            _apiClient.OnStreamComplete += HandleStreamComplete;
            _apiClient.OnError += HandleError;
            _apiClient.OnToolCallStarted += HandleToolCallStarted;
            _apiClient.OnUsageUpdated += HandleUsageUpdated;
            _apiClient.OnRetryAttempt += HandleRetryAttempt;

            _inputHandler = new McpChatInput();
            _inputHandler.Initialize(host);
            _inputHandler.OnSendRequested += HandleSendRequested;
            _inputHandler.OnStopRequested += HandleStopRequested;

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            RestoreFromSessionState();
            // UX-03: No initial system message needed -- welcome state handles empty chat
        }

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            if (_inputHandler != null)
            {
                _inputHandler.OnSendRequested -= HandleSendRequested;
                _inputHandler.OnStopRequested -= HandleStopRequested;
            }

            _apiClient?.Dispose();
            _apiClient = null;

            _compactApiClient?.Dispose();
            _compactApiClient = null;
        }

        /// <summary>
        /// Called just before Unity performs a domain reload (script compilation).
        /// Persists the current execution state so we can resume after reload.
        /// </summary>
        private void OnBeforeAssemblyReload()
        {
            // Save display entries + conversation first
            McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);

            // Save interrupted execution state
            McpChatSession.SaveInterruptedState(
                _pendingToolBlocks, _toolExecIndex,
                _pendingToolResultMessage, _apiClient);
        }

        private McpChatToolBridge GetToolBridge()
        {
            var registry = McpUnityServer.ToolRegistry;

            if (_toolBridge == null || (_toolBridge.Registry == null && registry != null))
            {
                _toolBridge = new McpChatToolBridge(registry);
            }

            return _toolBridge;
        }

        // ====================================================================
        // Main Draw (called by host)
        // ====================================================================

        public void Draw()
        {
            if (_apiClient == null) return;
            McpChatStyles.EnsureInitialized();

            // Escape to cancel streaming
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (_pendingToolBlocks != null)
                {
                    _toolExecCancelled = true;
                    _toolExecEpoch++; // Invalidate any pending delayCall lambdas
                }
                if (_apiClient.IsProcessing)
                {
                    _apiClient.Cancel();
                    FinalizeStreamingEntry();
                }
                Event.current.Use();
            }

            DrawTopBar();
            DrawContextBar();
            DrawChatArea();
            _inputHandler.Draw(_apiClient.IsProcessing, McpChatApiClient.HasAuth);
        }

        // ====================================================================
        // Top Bar
        // ====================================================================

        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // UX-08: Auth indicator dot with tooltip
            var currentAuth = McpChatApiClient.ResolveAuth();
            var oldColor = GUI.contentColor;
            GUI.contentColor = currentAuth.IsValid
                ? new Color(0.3f, 0.9f, 0.3f)
                : new Color(1f, 0.35f, 0.35f);
            string authTooltip = currentAuth.IsValid
                ? $"Authenticated via {currentAuth.Source}"
                : "Not authenticated — click Settings to configure";
            GUILayout.Label(new GUIContent("\u25CF", authTooltip), EditorStyles.miniLabel, GUILayout.Width(12));
            GUI.contentColor = oldColor;

            // Provider + Model dropdown button
            var provider = ProviderRegistry.GetActiveProvider();
            string currentModel = ProviderRegistry.GetModel(provider.Id);
            int modelIdx = Array.IndexOf(provider.ModelIds, currentModel);
            string modelLabel = modelIdx >= 0 && modelIdx < provider.ModelLabels.Length
                ? provider.ModelLabels[modelIdx] : "?";
            string dropdownText = $"{provider.DisplayName} \u25BC {modelLabel}";
            if (GUILayout.Button(dropdownText, EditorStyles.toolbarDropDown, GUILayout.MaxWidth(220)))
                ShowModelPopup();

            // Tool count
            var bridge = GetToolBridge();
            int toolCount = provider.SupportsTools ? bridge.ActiveToolCount : 0;
            if (toolCount > 0)
                GUILayout.Label($"{toolCount}t", EditorStyles.miniLabel, GUILayout.Width(24));

            GUILayout.FlexibleSpace();

            // Compact token counter with context %
            if (_totalInputTokens > 0 || _totalOutputTokens > 0)
            {
                string inTok = FormatTokenCount(_totalInputTokens);
                string outTok = FormatTokenCount(_totalOutputTokens);
                int maxCtx = provider.MaxContextTokens;
                int contextUsed = _lastInputTokens + _lastOutputTokens;
                string pctStr = maxCtx > 0 && contextUsed > 0 ? $" ({(contextUsed * 100f / maxCtx):F0}%)" : "";
                GUILayout.Label($"\u2191{inTok} \u2193{outTok}{pctStr}", EditorStyles.miniLabel);
                GUILayout.Space(4);
            }

            // Animated streaming indicator
            if (_apiClient.IsProcessing)
            {
                int dotCount = (int)(EditorApplication.timeSinceStartup * 2.5) % 4;
                string dots = new string('.', dotCount);
                oldColor = GUI.contentColor;
                GUI.contentColor = new Color(0.3f, 0.8f, 1f);
                GUILayout.Label($"Streaming{dots}", EditorStyles.miniLabel, GUILayout.Width(72));
                GUI.contentColor = oldColor;
                _hostWindow?.Repaint();
            }

            // UX-08: Settings button with tooltip
            if (GUILayout.Button(new GUIContent("\u2699", "Settings — Configure providers, API keys, system prompt"),
                EditorStyles.toolbarButton, GUILayout.Width(24)))
                OnSettingsRequested?.Invoke();

            // UX-08: Overflow menu replaces separate Export/Compact/Clear buttons
            if (GUILayout.Button(new GUIContent("\u22EF", "More actions — Export, Compact, Clear"),
                EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                var menu = new GenericMenu();

                // Export submenu
                if (_displayEntries.Count > 1)
                {
                    menu.AddItem(new GUIContent("Export/Markdown"), false, () => ExportConversation("md"));
                    menu.AddItem(new GUIContent("Export/JSON"), false, () => ExportConversation("json"));
                    menu.AddItem(new GUIContent("Export/Plain Text"), false, () => ExportConversation("txt"));
                    menu.AddItem(new GUIContent("Export/Copy to Clipboard"), false, () => CopyConversation("md"));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Export (no messages)"));
                }

                menu.AddSeparator("");

                // Compact
                if (_conversation.Count >= 4 && !_apiClient.IsProcessing && !_isCompacting)
                    menu.AddItem(new GUIContent("Compact conversation"), false, CompactConversation);
                else
                    menu.AddDisabledItem(new GUIContent("Compact conversation"));

                menu.AddSeparator("");

                // Clear
                menu.AddItem(new GUIContent("Clear chat"), false, ClearChat);

                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Draw a thin context window fill bar below the toolbar.</summary>
        private void DrawContextBar()
        {
            int totalUsed = _lastInputTokens + _lastOutputTokens;
            if (totalUsed <= 0) return;

            var provider = ProviderRegistry.GetActiveProvider();
            int maxContext = provider.MaxContextTokens;
            if (maxContext <= 0) return;

            float ratio = Mathf.Clamp01((float)totalUsed / maxContext);

            Rect barRect = EditorGUILayout.GetControlRect(false, 3);
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));

            Color fillColor;
            if (ratio < 0.5f)
                fillColor = Color.Lerp(new Color(0.3f, 0.8f, 0.4f), new Color(0.9f, 0.8f, 0.2f), ratio * 2f);
            else
                fillColor = Color.Lerp(new Color(0.9f, 0.8f, 0.2f), new Color(0.9f, 0.3f, 0.2f), (ratio - 0.5f) * 2f);

            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
            EditorGUI.DrawRect(fillRect, fillColor);

            string pct = (ratio * 100f).ToString("F1");
            string tooltip = $"Context: {FormatTokenCount(totalUsed)}/{FormatTokenCount(maxContext)} tokens ({pct}%)\nSession total: \u2191{FormatTokenCount(_totalInputTokens)} \u2193{FormatTokenCount(_totalOutputTokens)}";
            EditorGUI.LabelField(barRect, new GUIContent("", tooltip), GUIStyle.none);
        }

        /// <summary>Show a GenericMenu with Provider > Model hierarchy for quick switching.</summary>
        private void ShowModelPopup()
        {
            var menu = new GenericMenu();
            var presetIds = ProviderRegistry.GetPresetIds();
            var presetLabels = ProviderRegistry.GetPresetLabels();

            for (int p = 0; p < presetIds.Length; p++)
            {
                string pid = presetIds[p];
                string pLabel = presetLabels[p];
                var prov = ProviderRegistry.GetProvider(pid);
                if (prov == null) continue;

                for (int m = 0; m < prov.ModelIds.Length; m++)
                {
                    string mid = prov.ModelIds[m];
                    string mLabel = prov.ModelLabels[m];
                    bool isActive = pid == ProviderRegistry.ActiveProviderId
                        && mid == ProviderRegistry.GetModel(pid);

                    string capturedPid = pid;
                    string capturedMid = mid;

                    menu.AddItem(
                        new GUIContent($"{pLabel}/{mLabel}"),
                        isActive,
                        () =>
                        {
                            ProviderRegistry.ActiveProviderId = capturedPid;
                            ProviderRegistry.SetModel(capturedPid, capturedMid);
                            _hostWindow?.Repaint();
                        });
                }
            }
            menu.ShowAsContext();
        }

        /// <summary>Format token count: 1234 -> "1.2k", 12345 -> "12k", 123 -> "123".</summary>
        private static string FormatTokenCount(int count)
        {
            if (count >= 100000) return $"{count / 1000}k";
            if (count >= 1000) return $"{count / 1000f:0.#}k";
            return count.ToString();
        }

        // ====================================================================
        // Chat Area
        // ====================================================================

        private void DrawChatArea()
        {
            _chatScrollPos = EditorGUILayout.BeginScrollView(_chatScrollPos, GUILayout.ExpandHeight(true));

            // UX-03: Show welcome screen when no real messages exist
            bool hasRealMessages = false;
            for (int i = 0; i < _displayEntries.Count; i++)
            {
                if (_displayEntries[i].type != ChatDisplayEntry.EntryType.System)
                {
                    hasRealMessages = true;
                    break;
                }
            }

            if (!hasRealMessages)
            {
                DrawWelcomeState();
            }
            else
            {
                for (int i = 0; i < _displayEntries.Count; i++)
                    DrawChatEntry(_displayEntries[i], i);
            }

            if (_autoScroll)
            {
                GUILayout.Space(1);
                _chatScrollPos.y = float.MaxValue;
            }

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.ScrollWheel)
                _autoScroll = false;

            // Scroll-to-bottom button when user has scrolled up
            if (!_autoScroll && _displayEntries.Count > 1 && hasRealMessages)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("\u25BC Scroll to latest", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    _autoScroll = true;
                    _chatScrollPos.y = float.MaxValue;
                    _hostWindow?.Repaint();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>UX-03: Welcome screen with suggested prompts.</summary>
        private void DrawWelcomeState()
        {
            GUILayout.FlexibleSpace();

            GUILayout.Label("MCP Unity Chat (Beta)", McpChatStyles.WelcomeHeader);
            GUILayout.Label(
                "Ask questions about your project, inspect scenes, modify GameObjects,\nand automate workflows — all powered by AI with 138 tools.",
                McpChatStyles.WelcomeSub);

            GUILayout.Space(12);

            string[] suggestions = new[]
            {
                "\u25B6  List all GameObjects in the current scene",
                "\u25B6  What scripts have compile errors?",
                "\u25B6  Create a new empty GameObject called 'GameManager'",
                "\u25B6  Show me the project's render pipeline settings"
            };

            foreach (string suggestion in suggestions)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                if (GUILayout.Button(suggestion, McpChatStyles.Suggestion, GUILayout.MaxWidth(500)))
                {
                    SendMessage(suggestion.Substring(3).Trim(), new List<AssetReference>());
                }
                GUILayout.Space(40);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("<color=#666666>Tip: Drag assets or GameObjects onto the input field to reference them</color>",
                McpChatStyles.WelcomeSub);

            GUILayout.FlexibleSpace();
        }

        private void DrawChatEntry(ChatDisplayEntry entry, int entryIndex)
        {
            Color oldBg;
            // UX-10: Clamp max bubble width between 300-700px
            float rawWidth = _hostWindow != null ? _hostWindow.position.width * 0.75f : 400f;
            float maxWidth = Mathf.Clamp(rawWidth, 300f, 700f);

            // Turn separator
            if (entry.type == ChatDisplayEntry.EntryType.User && entryIndex > 0)
            {
                var prevType = _displayEntries[entryIndex - 1].type;
                if (prevType != ChatDisplayEntry.EntryType.User && prevType != ChatDisplayEntry.EntryType.System)
                {
                    GUILayout.Space(4);
                    Rect sepRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(sepRect, new Color(0.3f, 0.3f, 0.3f, 0.4f));
                    GUILayout.Space(4);
                }
            }

            // UX-07: Format timestamp for display
            string timeStr = null;
            if (entry.timestamp > 0)
            {
                var ts = DateTimeOffset.FromUnixTimeMilliseconds(entry.timestamp).LocalDateTime;
                timeStr = ts.ToString("HH:mm");
            }

            switch (entry.type)
            {
                case ChatDisplayEntry.EntryType.User:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (timeStr != null)
                        GUILayout.Label(timeStr, McpChatStyles.Timestamp);
                    GUILayout.Label("<color=#6699cc>You</color>", McpChatStyles.RoleLabel);
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);

                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.25f, 0.45f, 0.7f, 0.5f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(entry.text, McpChatStyles.UserBubble, GUILayout.MaxWidth(maxWidth));
                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = oldBg;
                    break;

                case ChatDisplayEntry.EntryType.Assistant:
                    {
                        var activeProvider = ProviderRegistry.GetActiveProvider();
                        string currentModel = ProviderRegistry.GetModel(activeProvider.Id);
                        int mIdx = Array.IndexOf(activeProvider.ModelIds, currentModel);
                        string mName = mIdx >= 0 && mIdx < activeProvider.ModelLabels.Length
                            ? activeProvider.ModelLabels[mIdx] : activeProvider.DisplayName;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label($"<color=#66bb77>{mName}</color>", McpChatStyles.RoleLabel);
                        if (timeStr != null && !entry.isStreaming)
                            GUILayout.Label(timeStr, McpChatStyles.Timestamp);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2);
                    }

                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.22f, 0.24f, 0.28f, 0.35f);

                    if (entry.isStreaming)
                    {
                        if (entry.isWaitingForFirstChunk)
                        {
                            int dotCount = (int)(EditorApplication.timeSinceStartup * 3) % 4;
                            string dots = new string('.', dotCount);
                            EditorGUILayout.LabelField($"Thinking{dots}", McpChatStyles.Thinking);
                            _hostWindow?.Repaint();
                        }
                        else
                        {
                            string rawText = entry.streamingBuilder != null
                                ? entry.streamingBuilder.ToString()
                                : (entry.text ?? "");

                            if (!string.IsNullOrEmpty(rawText))
                            {
                                double now = EditorApplication.timeSinceStartup;
                                if (entry.parsedSegments == null ||
                                    (Event.current.type == EventType.Layout && now - entry.lastParseTime > 0.1))
                                {
                                    entry.parsedSegments = new List<object>(McpMarkdownRenderer.Parse(rawText));
                                    entry.lastParseTime = now;
                                }

                                DrawMarkdownEntry(entry);

                                int cursorPhase = (int)(EditorApplication.timeSinceStartup * 2.5) % 2;
                                string cursorText = cursorPhase == 0 ? "<color=#88ccff> \u258C</color>" : " ";
                                GUILayout.Label(cursorText, McpChatStyles.RoleLabel);
                            }
                            _hostWindow?.Repaint();
                        }
                    }
                    else
                    {
                        DrawMarkdownEntry(entry);
                        bool hasCodeBlocks = entry.parsedSegments != null &&
                            entry.parsedSegments.Exists(obj =>
                                obj is McpMarkdownRenderer.MarkdownSegment s &&
                                s.Type == McpMarkdownRenderer.SegmentType.CodeBlock);
                        if (!hasCodeBlocks)
                            DrawCopyButton(entry, entryIndex);
                    }
                    GUI.backgroundColor = oldBg;
                    break;

                case ChatDisplayEntry.EntryType.ToolCall:
                    {
                        string foldIcon = entry.isExpanded ? "\u25BC" : "\u25B6";
                        Rect toolCallRect = EditorGUILayout.GetControlRect(false, 18);
                        Rect toolBgRect = new Rect(toolCallRect.x + 20, toolCallRect.y, toolCallRect.width - 40, toolCallRect.height);
                        EditorGUI.DrawRect(toolBgRect, new Color(0.4f, 0.35f, 0.15f, 0.12f));
                        string toolCallLabel = $"<color=#887733>{foldIcon}</color>  <color=#ccaa44>\u26A1 {entry.toolName}</color>";
                        if (GUI.Button(toolBgRect, toolCallLabel, McpChatStyles.ToolCompact))
                        {
                            entry.isExpanded = !entry.isExpanded;
                            _hostWindow?.Repaint();
                        }
                        EditorGUIUtility.AddCursorRect(toolBgRect, MouseCursor.Link);

                        if (entry.isExpanded && !string.IsNullOrEmpty(entry.text))
                        {
                            oldBg = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
                            EditorGUILayout.LabelField(
                                $"<color=#888888>{Truncate(entry.text, 500)}</color>",
                                McpChatStyles.ToolBubble);
                            GUI.backgroundColor = oldBg;
                        }
                    }
                    break;

                case ChatDisplayEntry.EntryType.ToolResult:
                    {
                        string full = entry.fullText ?? entry.text ?? "";
                        string foldIcon = entry.isExpanded ? "\u25BC" : "\u25B6";
                        string statusIcon = "\u2713";
                        string statusColor = "#44cc66";
                        string sizeHint = full.Length > 100 ? $"  <color=#666666>({full.Length:N0} chars)</color>" : "";

                        Rect toolResRect = EditorGUILayout.GetControlRect(false, 18);
                        Rect resBgRect = new Rect(toolResRect.x + 20, toolResRect.y, toolResRect.width - 40, toolResRect.height);
                        EditorGUI.DrawRect(resBgRect, new Color(0.15f, 0.35f, 0.2f, 0.12f));

                        string resultLabel = $"<color=#336644>{foldIcon}</color>  <color={statusColor}>{statusIcon} {entry.toolName}</color>{sizeHint}";
                        if (GUI.Button(resBgRect, resultLabel, McpChatStyles.ToolCompact))
                        {
                            entry.isExpanded = !entry.isExpanded;
                            _hostWindow?.Repaint();
                        }
                        EditorGUIUtility.AddCursorRect(resBgRect, MouseCursor.Link);

                        if (entry.isExpanded)
                        {
                            oldBg = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(0.12f, 0.18f, 0.14f, 0.4f);
                            EditorGUILayout.LabelField(
                                $"<color=#999999>{full}</color>",
                                McpChatStyles.ToolBubble);
                            GUI.backgroundColor = oldBg;
                        }
                    }
                    break;

                case ChatDisplayEntry.EntryType.Error:
                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.5f, 0.15f, 0.15f, 0.4f);
                    string prefix = entry.toolName != null
                        ? $"<color=#ff6666>Error:</color> <b>{entry.toolName}</b>\n"
                        : "<color=#ff6666>Error:</color> ";
                    EditorGUILayout.LabelField(prefix + entry.text, McpChatStyles.ErrorBubble);
                    GUI.backgroundColor = oldBg;
                    DrawRetryButton();
                    break;

                case ChatDisplayEntry.EntryType.System:
                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.35f, 0.3f);
                    EditorGUILayout.LabelField($"<color=#aaaaaa>{entry.text}</color>", McpChatStyles.SystemBubble);
                    GUI.backgroundColor = oldBg;
                    break;
            }
        }

        private void DrawMarkdownEntry(ChatDisplayEntry entry)
        {
            if (entry.parsedSegments == null)
                entry.parsedSegments = new List<object>(McpMarkdownRenderer.Parse(entry.text ?? ""));

            float availWidth = _hostWindow != null ? _hostWindow.position.width - 80f : 400f;
            if (availWidth < 100f) availWidth = 400f;

            foreach (var obj in entry.parsedSegments)
            {
                var seg = (McpMarkdownRenderer.MarkdownSegment)obj;

                switch (seg.Type)
                {
                    case McpMarkdownRenderer.SegmentType.Prose:
                        EditorGUILayout.LabelField(seg.Text, McpChatStyles.AssistantBubble);

                        if (seg.Links != null && seg.Links.Count > 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(8);
                            foreach (string url in seg.Links)
                            {
                                string displayUrl = url.Length > 50 ? url.Substring(0, 47) + "..." : url;
                                if (GUILayout.Button($"\u2197 {displayUrl}", McpChatStyles.LinkButton))
                                {
                                    // SEC: only open http(s) URLs — LLM output may contain
                                    // file://, javascript:, or platform URI handlers.
                                    if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var safeUri) &&
                                        (safeUri.Scheme == System.Uri.UriSchemeHttp || safeUri.Scheme == System.Uri.UriSchemeHttps))
                                    {
                                        Application.OpenURL(url);
                                    }
                                    else
                                    {
                                        UnityEngine.Debug.LogWarning($"[MCP-Unity] Blocked link with unsafe scheme: {url}");
                                    }
                                }
                                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                            }
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                        break;

                    case McpMarkdownRenderer.SegmentType.CodeBlock:
                        string displayCode = McpMarkdownRenderer.HighlightCode(seg.Text, seg.Language);
                        var content = new GUIContent(displayCode);
                        float height = McpChatStyles.CodeBlock.CalcHeight(content, availWidth);
                        height = Mathf.Max(height, 20f);

                        bool hasLang = !string.IsNullOrEmpty(seg.Language);
                        float headerHeight = hasLang ? 18f : 4f;

                        Rect bgRect = EditorGUILayout.GetControlRect(false, height + headerHeight + 8);
                        EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.12f, 0.95f));

                        if (hasLang)
                        {
                            Rect langRect = new Rect(bgRect.x + 12, bgRect.y + 2, bgRect.width * 0.5f, 14);
                            GUI.Label(langRect, $"<color=#555555><size=9>{seg.Language}</size></color>", McpChatStyles.LangLabel);
                        }

                        Rect copyBtnRect = new Rect(bgRect.xMax - 24, bgRect.y + 2, 18, 14);
                        if (GUI.Button(copyBtnRect, "\u2398", EditorStyles.miniButton))
                            EditorGUIUtility.systemCopyBuffer = seg.Text;

                        Rect textRect = new Rect(bgRect.x + 8, bgRect.y + headerHeight + 2, bgRect.width - 16, height);
                        EditorGUI.SelectableLabel(textRect, displayCode, McpChatStyles.CodeBlock);
                        break;

                    case McpMarkdownRenderer.SegmentType.Header:
                        var hStyle = seg.HeaderLevel <= 1 ? McpChatStyles.H1 : seg.HeaderLevel == 2 ? McpChatStyles.H2 : McpChatStyles.H3;
                        EditorGUILayout.LabelField(seg.Text, hStyle);
                        break;

                    case McpMarkdownRenderer.SegmentType.Blockquote:
                        var bqContent = new GUIContent(seg.Text);
                        float bqHeight = McpChatStyles.Blockquote.CalcHeight(bqContent, availWidth - 30f);
                        bqHeight = Mathf.Max(bqHeight, 20f);
                        Rect bqRect = EditorGUILayout.GetControlRect(false, bqHeight + 4);
                        Rect barRect = new Rect(bqRect.x + 8, bqRect.y, 3, bqRect.height);
                        EditorGUI.DrawRect(barRect, new Color(0.4f, 0.6f, 0.9f, 0.7f));
                        Rect bqBg = new Rect(bqRect.x + 12, bqRect.y, bqRect.width - 16, bqRect.height);
                        EditorGUI.DrawRect(bqBg, new Color(0.2f, 0.2f, 0.25f, 0.2f));
                        Rect bqTextRect = new Rect(bqRect.x + 14, bqRect.y + 2, bqRect.width - 24, bqRect.height - 4);
                        EditorGUI.LabelField(bqTextRect, seg.Text, McpChatStyles.Blockquote);
                        break;

                    case McpMarkdownRenderer.SegmentType.HorizontalRule:
                        GUILayout.Space(4);
                        Rect hrRect = EditorGUILayout.GetControlRect(false, 1);
                        EditorGUI.DrawRect(hrRect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
                        GUILayout.Space(4);
                        break;

                    case McpMarkdownRenderer.SegmentType.Table:
                        DrawTableSegment(seg, availWidth);
                        break;
                }
            }
        }

        private void DrawTableSegment(McpMarkdownRenderer.MarkdownSegment seg, float availWidth)
        {
            if (seg.TableRows == null || seg.TableRows.Length == 0) return;
            var headers = seg.TableRows[0];
            if (headers == null || headers.Length == 0) return;

            int colCount = headers.Length;
            float colWidth = Mathf.Max((availWidth - 24f) / colCount, 40f);
            const float rowHeight = 20f;

            GUILayout.Space(4);

            Rect headerRect = EditorGUILayout.GetControlRect(false, rowHeight + 4);
            EditorGUI.DrawRect(headerRect, new Color(0.22f, 0.22f, 0.30f, 0.85f));
            for (int c = 0; c < colCount; c++)
            {
                Rect cellRect = new Rect(headerRect.x + 8 + c * colWidth, headerRect.y + 2, colWidth - 4, rowHeight);
                if (c > 0)
                {
                    Rect sepV = new Rect(cellRect.x - 2, headerRect.y + 4, 1, rowHeight - 4);
                    EditorGUI.DrawRect(sepV, new Color(0.45f, 0.45f, 0.55f, 0.5f));
                }
                EditorGUI.LabelField(cellRect, c < headers.Length ? headers[c] : "", McpChatStyles.TableHeader);
            }

            Rect topSep = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(topSep, new Color(0.4f, 0.4f, 0.55f, 0.7f));

            for (int r = 1; r < seg.TableRows.Length; r++)
            {
                var row = seg.TableRows[r];
                bool isEven = r % 2 == 0;
                Rect rowRect = EditorGUILayout.GetControlRect(false, rowHeight + 2);
                if (isEven)
                    EditorGUI.DrawRect(rowRect, new Color(0.18f, 0.18f, 0.22f, 0.35f));

                for (int c = 0; c < colCount; c++)
                {
                    Rect cellRect = new Rect(rowRect.x + 8 + c * colWidth, rowRect.y + 1, colWidth - 4, rowHeight);
                    if (c > 0)
                    {
                        Rect sepV = new Rect(cellRect.x - 2, rowRect.y + 2, 1, rowHeight - 2);
                        EditorGUI.DrawRect(sepV, new Color(0.35f, 0.35f, 0.40f, 0.3f));
                    }
                    string cellText = (row != null && c < row.Length) ? row[c] : "";
                    EditorGUI.LabelField(cellRect, cellText, McpChatStyles.TableCell);
                }
            }

            Rect botSep = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(botSep, new Color(0.35f, 0.35f, 0.45f, 0.5f));
            GUILayout.Space(4);
        }

        private void DrawCopyButton(ChatDisplayEntry entry, int index)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_copyFeedbackIndex == index && EditorApplication.timeSinceStartup - _copyFeedbackTime < 1.5)
            {
                GUILayout.Label("<color=#66cc88>Copied!</color>", McpChatStyles.CopiedLabel);
            }
            else
            {
                if (GUILayout.Button("Copy", McpChatStyles.CopyButton))
                {
                    EditorGUIUtility.systemCopyBuffer = entry.text ?? "";
                    _copyFeedbackIndex = index;
                    _copyFeedbackTime = EditorApplication.timeSinceStartup;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRetryButton()
        {
            if (_apiClient.IsProcessing) return;
            if (_conversation.Count == 0) return;

            string lastUserText = null;
            for (int i = _conversation.Count - 1; i >= 0; i--)
            {
                if (_conversation[i].role == "user")
                {
                    lastUserText = _conversation[i].GetText();
                    break;
                }
            }

            if (lastUserText == null) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Retry", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                while (_displayEntries.Count > 0)
                {
                    var last = _displayEntries[_displayEntries.Count - 1];
                    if (last.type == ChatDisplayEntry.EntryType.User) break;
                    _displayEntries.RemoveAt(_displayEntries.Count - 1);
                }

                if (_conversation.Count > 0 && _conversation[_conversation.Count - 1].role == "assistant")
                    _conversation.RemoveAt(_conversation.Count - 1);

                var retryEntry = ChatDisplayEntry.AssistantMessage("", true);
                retryEntry.isWaitingForFirstChunk = true;
                _displayEntries.Add(retryEntry);
                _autoScroll = true;

                var bridge = GetToolBridge();
                string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
                string sys = bridge.BuildSystemPrompt(customPrompt);
                var msgs = bridge.BuildMessagesArray(_conversation);
                var tools = bridge.GetToolDefinitions();
                _apiClient.SendStreamingRequest(sys, msgs, tools);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ====================================================================
        // Input Handler Callbacks
        // ====================================================================

        private void HandleSendRequested(string text, List<AssetReference> referencedAssets)
        {
            SendMessage(text, referencedAssets);
        }

        private void HandleStopRequested()
        {
            _apiClient.Cancel();
            FinalizeStreamingEntry();
        }

        // ====================================================================
        // Message Sending
        // ====================================================================

        private void SendMessage(string text, List<AssetReference> referencedAssets)
        {
            if (string.IsNullOrEmpty(text)) return;

            var bridge = GetToolBridge();

            string displayText = text;
            string llmText = referencedAssets.Count > 0
                ? bridge.BuildEnrichedUserText(text, referencedAssets)
                : text;

            var userMsg = new ChatMessage("user", llmText);
            _conversation.Add(userMsg);

            _displayEntries.Add(ChatDisplayEntry.UserMessage(displayText));
            var assistantEntry = ChatDisplayEntry.AssistantMessage("", true);
            assistantEntry.isWaitingForFirstChunk = true;
            _displayEntries.Add(assistantEntry);
            _autoScroll = true;

            string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
            string sys = bridge.BuildSystemPrompt(customPrompt);
            var msgs = bridge.BuildMessagesArray(_conversation);
            var tools = bridge.GetToolDefinitions();

            _apiClient.SendStreamingRequest(sys, msgs, tools);
            McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);
            _hostWindow?.Repaint();
        }

        // ====================================================================
        // API Callbacks
        // ====================================================================

        private void HandleTextDelta(string chunk)
        {
            var last = GetLastStreamingEntry();
            if (last != null)
            {
                last.isWaitingForFirstChunk = false;

                if (last.streamingBuilder == null)
                    last.streamingBuilder = new System.Text.StringBuilder(last.text ?? "");
                last.streamingBuilder.Append(chunk);
            }
            _hostWindow?.Repaint();
        }

        private void HandleStreamComplete(StreamingState state)
        {
            FinalizeStreamingEntry();

            string finalText = state.CurrentText;
            if (!string.IsNullOrEmpty(finalText))
            {
                for (int i = _displayEntries.Count - 1; i >= 0; i--)
                {
                    if (_displayEntries[i].type == ChatDisplayEntry.EntryType.Assistant)
                    {
                        _displayEntries[i].text = finalText;
                        break;
                    }
                }
            }

            if (state.usage != null)
            {
                _totalInputTokens += state.usage.input_tokens;
                _totalOutputTokens += state.usage.output_tokens;
                _lastInputTokens = state.usage.input_tokens;
                _lastOutputTokens = state.usage.output_tokens;
            }

            var assistantMsg = new ChatMessage { role = "assistant", content = new List<ContentBlock>(state.contentBlocks) };
            _conversation.Add(assistantMsg);

            if (state.stopReason == "tool_use")
            {
                var blocks = state.GetToolUseBlocks();
                if (blocks.Count > 0)
                {
                    UpdateToolCallDisplayEntries(blocks);
                    ExecuteToolsAndContinue(blocks);
                    return;
                }
            }

            McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);
            _hostWindow?.Repaint();
        }

        private void UpdateToolCallDisplayEntries(List<ToolUseContent> toolBlocks)
        {
            int blockIdx = toolBlocks.Count - 1;
            for (int i = _displayEntries.Count - 1; i >= 0 && blockIdx >= 0; i--)
            {
                var entry = _displayEntries[i];
                if (entry.type != ChatDisplayEntry.EntryType.ToolCall) continue;

                var tu = toolBlocks[blockIdx];
                if (entry.toolName == tu.name)
                {
                    if (tu.rawJsonBuilder != null && tu.rawJsonBuilder.Length > 0)
                        entry.text = tu.rawJsonBuilder.ToString();
                    else if (tu.input != null && tu.input.Count > 0)
                        entry.text = JsonHelper.ToJson(tu.input);
                    blockIdx--;
                }
            }
        }

        private void HandleError(string error)
        {
            FinalizeStreamingEntry();
            _displayEntries.Add(ChatDisplayEntry.ErrorMessage(error));
            _hostWindow?.Repaint();
        }

        private void HandleToolCallStarted(ToolUseContent toolUse)
        {
            string summary = toolUse.input != null && toolUse.input.Count > 0
                ? JsonHelper.ToJson(toolUse.input) : "";
            _displayEntries.Add(ChatDisplayEntry.ToolCall(toolUse.name, summary));
            _hostWindow?.Repaint();
        }

        private void HandleUsageUpdated(UsageInfo usage)
        {
            CheckCompactSuggestion();
            _hostWindow?.Repaint();
        }

        private void HandleRetryAttempt(int attempt, float delay)
        {
            var streaming = GetLastStreamingEntry();
            if (streaming != null && streaming.streamingBuilder != null)
            {
                streaming.streamingBuilder.Clear();
                streaming.streamingBuilder.Append($"<i><color=#aaaaaa>Retrying... (attempt {attempt}/{McpChatApiClient.MaxRetries}, waiting {delay:F1}s)</color></i>");
            }
            _hostWindow?.Repaint();
        }

        // ====================================================================
        // Tool Execution Loop
        // ====================================================================

        private void ExecuteToolsAndContinue(List<ToolUseContent> toolUseBlocks)
        {
            var bridge = GetToolBridge();
            _pendingToolBlocks = toolUseBlocks;
            _pendingToolResultMessage = new ChatMessage { role = "user", content = new List<ContentBlock>() };
            _toolExecIndex = 0;
            _toolExecCancelled = false;
            int thisEpoch = ++_toolExecEpoch;

            UpdateToolExecProgress(bridge);
            EditorApplication.delayCall += () => ExecuteNextTool(thisEpoch);
        }

        private void ExecuteNextTool(int epoch)
        {
            if (epoch != _toolExecEpoch) return;
            if (_pendingToolBlocks == null) return;

            if (_toolExecCancelled || _toolExecIndex >= _pendingToolBlocks.Count)
            {
                FinalizeToolExecution();
                return;
            }

            var bridge = GetToolBridge();
            var tu = _pendingToolBlocks[_toolExecIndex];

            // Confirmation gate for destructive/high-impact tools
            if (McpChatToolBridge.RequiresConfirmation(tu.name))
            {
                var args = bridge.ResolveToolInput(tu);
                string argSummary = BuildToolArgSummary(tu.name, args);

                bool userApproved = EditorUtility.DisplayDialog(
                    "Tool Confirmation Required",
                    $"The AI wants to execute:\n\n{tu.name}\n{argSummary}\n\nAllow this operation?",
                    "Allow",
                    "Deny"
                );

                if (!userApproved)
                {
                    var denialResult = new ToolResultContent
                    {
                        tool_use_id = tu.id,
                        content = $"Operation denied by user. The user did not approve '{tu.name}'. Ask the user what they'd like to do instead, or explain why this operation is needed.",
                        is_error = true
                    };

                    var denialEntry = ChatDisplayEntry.ToolResultEntry(tu.name, "[Denied by user]", true);
                    denialEntry.fullText = denialResult.content;
                    _displayEntries.Add(denialEntry);
                    _pendingToolResultMessage.content.Add(denialResult);

                    _toolExecIndex++;
                    _hostWindow?.Repaint();

                    if (_toolExecIndex < _pendingToolBlocks.Count)
                    {
                        UpdateToolExecProgress(bridge);
                        EditorApplication.delayCall += () => ExecuteNextTool(epoch);
                    }
                    else
                    {
                        EditorApplication.delayCall += FinalizeToolExecution;
                    }
                    return;
                }
            }

            var result = bridge.ExecuteToolUse(tu);

            var displayEntry = ChatDisplayEntry.ToolResultEntry(tu.name, Truncate(result.content, 500), result.is_error);
            displayEntry.fullText = result.content;
            _displayEntries.Add(displayEntry);
            _pendingToolResultMessage.content.Add(result);

            _toolExecIndex++;
            _hostWindow?.Repaint();

            if (_toolExecIndex < _pendingToolBlocks.Count)
            {
                UpdateToolExecProgress(bridge);
                EditorApplication.delayCall += () => ExecuteNextTool(epoch);
            }
            else
            {
                EditorApplication.delayCall += FinalizeToolExecution;
            }
        }

        private static string BuildToolArgSummary(string toolName, Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var kvp in args)
            {
                if (shown >= 5) { sb.Append("\n  ..."); break; }

                string val = kvp.Value?.ToString() ?? "null";
                if (val.Length > 120) val = val.Substring(0, 117) + "...";

                sb.Append($"\n  {kvp.Key}: {val}");
                shown++;
            }
            return sb.ToString();
        }

        private void UpdateToolExecProgress(McpChatToolBridge bridge)
        {
            var streaming = GetLastStreamingEntry();
            if (streaming != null)
            {
                if (streaming.streamingBuilder == null)
                    streaming.streamingBuilder = new System.Text.StringBuilder();
                streaming.streamingBuilder.Clear();

                if (_toolExecIndex < _pendingToolBlocks.Count)
                {
                    string toolName = _pendingToolBlocks[_toolExecIndex].name;
                    streaming.streamingBuilder.Append(
                        $"<i><color=#ccaa44>Executing tool {_toolExecIndex + 1}/{_pendingToolBlocks.Count}: {toolName}...</color></i>");
                }
            }
            _hostWindow?.Repaint();
        }

        private void FinalizeToolExecution()
        {
            if (_pendingToolBlocks == null) return;

            if (_toolExecCancelled)
            {
                FinalizeStreamingEntry();
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                    $"Tool execution cancelled ({_toolExecIndex}/{_pendingToolBlocks.Count} tools completed)."));

                if (_pendingToolResultMessage.content.Count > 0)
                    _conversation.Add(_pendingToolResultMessage);

                _pendingToolBlocks = null;
                _pendingToolResultMessage = null;
                _apiClient.Cancel();
                McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);
                _hostWindow?.Repaint();
                return;
            }

            _conversation.Add(_pendingToolResultMessage);

            FinalizeStreamingEntry();
            var contEntry = ChatDisplayEntry.AssistantMessage("", true);
            contEntry.isWaitingForFirstChunk = true;
            _displayEntries.Add(contEntry);
            _autoScroll = true;

            var bridge = GetToolBridge();
            string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
            string sys = bridge.BuildSystemPrompt(customPrompt);
            var msgs = bridge.BuildMessagesArray(_conversation);
            var tools = bridge.GetToolDefinitions();

            bool anyErrors = false;
            foreach (var block in _pendingToolResultMessage.content)
            {
                if (block is ToolResultContent trc && trc.is_error)
                {
                    anyErrors = true;
                    break;
                }
            }

            _pendingToolBlocks = null;
            _pendingToolResultMessage = null;

            _apiClient.ContinueWithToolResults(sys, msgs, tools, anyErrors);
            _hostWindow?.Repaint();
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private ChatDisplayEntry GetLastStreamingEntry()
        {
            for (int i = _displayEntries.Count - 1; i >= 0; i--)
                if (_displayEntries[i].isStreaming) return _displayEntries[i];
            return null;
        }

        private void FinalizeStreamingEntry()
        {
            for (int i = _displayEntries.Count - 1; i >= 0; i--)
            {
                if (!_displayEntries[i].isStreaming) continue;

                var s = _displayEntries[i];
                s.isStreaming = false;

                if (s.streamingBuilder != null)
                {
                    s.text = s.streamingBuilder.ToString();
                    s.streamingBuilder = null;
                }

                if (string.IsNullOrEmpty(s.text))
                    _displayEntries.RemoveAt(i);

                return;
            }
        }

        // ====================================================================
        // Compact
        // ====================================================================

        private void CompactConversation()
        {
            if (_conversation.Count < 4 || _isCompacting || _apiClient.IsProcessing) return;

            _isCompacting = true;
            _displayEntries.Add(ChatDisplayEntry.SystemMessage("Compacting conversation..."));
            _autoScroll = true;
            _hostWindow?.Repaint();

            var summaryMessages = new List<object>();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Summarize the following conversation concisely. Preserve:");
            sb.AppendLine("- Key decisions and outcomes");
            sb.AppendLine("- Important tool results and state changes made to the project");
            sb.AppendLine("- Current tasks in progress or pending");
            sb.AppendLine("- Any errors encountered and their resolutions");
            sb.AppendLine("Do NOT include greetings, filler, or redundant detail.");
            sb.AppendLine("Format as a structured summary with sections. Be thorough but concise.");
            sb.AppendLine();
            sb.AppendLine("--- CONVERSATION TO SUMMARIZE ---");
            sb.AppendLine();

            foreach (var msg in _conversation)
            {
                sb.Append($"[{msg.role}]: ");
                foreach (var block in msg.content)
                {
                    if (block is TextContent tc)
                        sb.Append(tc.GetText());
                    else if (block is ToolUseContent tu)
                        sb.Append($"[tool_use: {tu.name}({JsonHelper.ToJson(tu.input)})]");
                    else if (block is ToolResultContent tr)
                    {
                        string preview = tr.content?.Length > 200 ? tr.content.Substring(0, 200) + "..." : tr.content;
                        sb.Append($"[tool_result: {(tr.is_error ? "ERROR: " : "")}{preview}]");
                    }
                }
                sb.AppendLine();
            }

            summaryMessages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = sb.ToString()
            });

            // SEC-#429: hold a reference so Dispose() can clean it up if the panel
            // closes while the request is still in flight.
            _compactApiClient?.Dispose();
            var compactClient = new McpChatApiClient();
            _compactApiClient = compactClient;

            compactClient.OnStreamComplete += (state) =>
            {
                string summary = state.CurrentText;
                if (string.IsNullOrWhiteSpace(summary))
                {
                    _isCompacting = false;
                    _displayEntries.Add(ChatDisplayEntry.SystemMessage("Compact failed — empty summary."));
                    _hostWindow?.Repaint();
                    compactClient.Dispose();
                    if (_compactApiClient == compactClient) _compactApiClient = null;
                    return;
                }

                int oldCount = _conversation.Count;
                _conversation.Clear();

                _conversation.Add(new ChatMessage("user", "[Previous conversation compacted — summary follows]"));
                _conversation.Add(new ChatMessage("assistant", $"[Conversation Summary]\n{summary}"));

                _displayEntries.Clear();
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                    $"Conversation compacted: {oldCount} messages \u2192 summary.\nContext freed for new messages."));
                _displayEntries.Add(ChatDisplayEntry.AssistantMessage($"**Conversation Summary**\n\n{summary}"));

                _totalInputTokens = 0;
                _totalOutputTokens = 0;
                _lastInputTokens = 0;
                _lastOutputTokens = 0;
                if (state.usage != null)
                {
                    _totalInputTokens = state.usage.input_tokens;
                    _totalOutputTokens = state.usage.output_tokens;
                    _lastInputTokens = state.usage.input_tokens;
                    _lastOutputTokens = state.usage.output_tokens;
                }

                _isCompacting = false;
                _compactSuggested = false;
                McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);
                _autoScroll = true;
                _hostWindow?.Repaint();
                compactClient.Dispose();
                if (_compactApiClient == compactClient) _compactApiClient = null;
            };

            compactClient.OnError += (err) =>
            {
                _isCompacting = false;
                _displayEntries.Add(ChatDisplayEntry.SystemMessage($"Compact failed: {err}"));
                _hostWindow?.Repaint();
                compactClient.Dispose();
                if (_compactApiClient == compactClient) _compactApiClient = null;
            };

            string compactSystemPrompt = "You are a conversation summarizer. Produce a concise, structured summary of the provided conversation.";
            compactClient.SendStreamingRequest(compactSystemPrompt, summaryMessages, new List<object>());
        }

        private void CheckCompactSuggestion()
        {
            if (_compactSuggested || _isCompacting || _conversation.Count < 6) return;

            var provider = ProviderRegistry.GetActiveProvider();
            int maxCtx = provider.MaxContextTokens;
            if (maxCtx <= 0) return;

            int totalUsed = _lastInputTokens + _lastOutputTokens;
            if (totalUsed <= 0) return;
            float ratio = (float)totalUsed / maxCtx;

            if (ratio >= CompactAutoThreshold)
            {
                _compactSuggested = true;
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                    $"Context usage at {(ratio * 100f):F0}%. Consider pressing Compact to summarize and free space."));
                _autoScroll = true;
                _hostWindow?.Repaint();
            }
        }

        private void ClearChat()
        {
            _conversation.Clear();
            _displayEntries.Clear();
            _totalInputTokens = 0;
            _totalOutputTokens = 0;
            _lastInputTokens = 0;
            _lastOutputTokens = 0;
            _isCompacting = false;
            _compactSuggested = false;
            _apiClient?.Cancel();
            _pendingToolBlocks = null;
            _pendingToolResultMessage = null;
            _toolExecCancelled = false;
            _toolExecEpoch++;
            McpChatSession.Clear();
        }

        // ====================================================================
        // Export (delegates to McpChatSession)
        // ====================================================================

        private void ExportConversation(string format)
        {
            McpChatSession.ExportToFile(_displayEntries, _totalInputTokens, _totalOutputTokens, format);
        }

        private void CopyConversation(string format)
        {
            McpChatSession.CopyToClipboard(_displayEntries, _totalInputTokens, _totalOutputTokens, format);
        }

        // ====================================================================
        // Domain Reload Resume
        // ====================================================================

        private bool TryScheduleResume()
        {
            var state = McpChatSession.LoadInterruptedState();
            if (state == null) return false;

            string savedType = state.TryGetValue("type", out var tv) ? tv?.ToString() : "none";

            bool orphanedToolUse = HasOrphanedToolUse();

            if (orphanedToolUse)
            {
                return TryResumeToolExecution(savedType == "tools" ? state : new Dictionary<string, object>());
            }

            if (savedType == "streaming" || savedType == "tools")
                return TryResumeStreaming();

            return false;
        }

        private bool HasOrphanedToolUse()
        {
            if (_conversation.Count == 0) return false;
            var lastMsg = _conversation[_conversation.Count - 1];
            if (lastMsg.role != "assistant") return false;
            foreach (var block in lastMsg.content)
            {
                if (block is ToolUseContent) return true;
            }
            return false;
        }

        private bool TryResumeToolExecution(Dictionary<string, object> state)
        {
            int completedIndex = state.TryGetValue("index", out var idx) ? System.Convert.ToInt32(idx) : 0;

            List<ToolUseContent> toolBlocks = null;
            for (int i = _conversation.Count - 1; i >= 0; i--)
            {
                if (_conversation[i].role != "assistant") continue;
                toolBlocks = new List<ToolUseContent>();
                foreach (var block in _conversation[i].content)
                {
                    if (block is ToolUseContent tu) toolBlocks.Add(tu);
                }
                if (toolBlocks.Count > 0) break;
                toolBlocks = null;
            }

            if (toolBlocks == null || toolBlocks.Count == 0) return false;

            _pendingToolResultMessage = new ChatMessage { role = "user", content = new List<ContentBlock>() };
            if (state.TryGetValue("results", out var prObj) && prObj is List<object> partials)
            {
                foreach (var item in partials)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        _pendingToolResultMessage.content.Add(new ToolResultContent
                        {
                            tool_use_id = d.TryGetValue("tool_use_id", out var id) ? id?.ToString() : "",
                            content = d.TryGetValue("content", out var c) ? c?.ToString() : "",
                            is_error = d.TryGetValue("is_error", out var e) && e is bool eb && eb
                        });
                    }
                }
            }

            _pendingToolBlocks = toolBlocks;
            _toolExecIndex = completedIndex;
            _toolExecCancelled = false;

            int actualTotal = toolBlocks.Count;
            _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                completedIndex > 0
                    ? $"(Resuming after compilation \u2014 {completedIndex}/{actualTotal} tools done, continuing...)"
                    : $"(Resuming {actualTotal} tool(s) after compilation...)"));

            int epoch = ++_toolExecEpoch;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () => ExecuteNextTool(epoch);
            };

            return true;
        }

        private bool TryResumeStreaming()
        {
            if (_conversation.Count == 0) return false;

            _displayEntries.Add(ChatDisplayEntry.SystemMessage("(Resuming response after compilation...)"));

            var assistantEntry = ChatDisplayEntry.AssistantMessage("", true);
            assistantEntry.isWaitingForFirstChunk = true;
            _displayEntries.Add(assistantEntry);
            _autoScroll = true;

            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    var bridge = GetToolBridge();
                    string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
                    string sys = bridge.BuildSystemPrompt(customPrompt);
                    var msgs = bridge.BuildMessagesArray(_conversation);
                    var tools = bridge.GetToolDefinitions();

                    _apiClient.SendStreamingRequest(sys, msgs, tools);
                    McpChatSession.Save(_displayEntries, _conversation, _totalInputTokens, _totalOutputTokens);
                    _hostWindow?.Repaint();
                };
            };

            return true;
        }

        // ====================================================================
        // SessionState Persistence (delegates to McpChatSession)
        // ====================================================================

        private void RestoreFromSessionState()
        {
            bool hasEntries = McpChatSession.Restore(
                _displayEntries, _conversation,
                out _totalInputTokens, out _totalOutputTokens,
                out bool conversationRestored);

            if (!hasEntries) return;

            bool resumed = conversationRestored && TryScheduleResume();

            if (_displayEntries.Count > 0 && !resumed)
            {
                string statusMsg = conversationRestored
                    ? $"(Session restored — conversation context preserved ({_conversation.Count} messages))"
                    : "(Session restored after reload — conversation context reset, previous messages shown for reference)";
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(statusMsg));
            }
        }

        // ====================================================================
        // Utilities
        // ====================================================================

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max) + "...";
        }
    }
}
