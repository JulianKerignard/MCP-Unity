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
        private McpChatToolBridge _toolBridge;
        private List<ChatMessage> _conversation = new List<ChatMessage>();
        private List<ChatDisplayEntry> _displayEntries = new List<ChatDisplayEntry>();
        private EditorWindow _hostWindow;

        // UI state
        private string _inputText = "";
        private Vector2 _chatScrollPos;
        private bool _autoScroll = true;

        // Asset references (drag & drop)
        private readonly List<AssetReference> _referencedAssets = new List<AssetReference>();
        private const int MaxReferencedAssets = 8;
        private GUIStyle _chipStyle;
        private GUIStyle _chipRemoveStyle;
        private GUIStyle _dropHintStyle;
        private bool _isDragHovering;

        // Token tracking — cumulative (for export/billing display)
        private int _totalInputTokens;
        private int _totalOutputTokens;
        // Token tracking — last API response (for real context window usage)
        private int _lastInputTokens;
        private int _lastOutputTokens;

        // Styles (lazy-init)
        private GUIStyle _userBubbleStyle;
        private GUIStyle _assistantBubbleStyle;
        private GUIStyle _toolBubbleStyle;
        private GUIStyle _errorBubbleStyle;
        private GUIStyle _systemBubbleStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _codeBlockStyle;
        private GUIStyle _h1Style;
        private GUIStyle _h2Style;
        private GUIStyle _h3Style;
        private GUIStyle _copyButtonStyle;
        private GUIStyle _langLabelStyle;
        private GUIStyle _copiedLabelStyle;
        private GUIStyle _roleLabelStyle;
        private GUIStyle _tableHeaderStyle;
        private GUIStyle _tableCellStyle;
        private GUIStyle _placeholderStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _blockquoteStyle;
        private GUIStyle _timestampStyle;     // UX-07
        private GUIStyle _thinkingStyle;      // UX-01
        private GUIStyle _welcomeHeaderStyle; // UX-03
        private GUIStyle _welcomeSubStyle;    // UX-03
        private GUIStyle _suggestionStyle;    // UX-03
        private GUIStyle _linkButtonStyle;    // UX-05
        private GUIStyle _toolCompactStyle;   // Compact one-liner for collapsed tool/result
        private bool _stylesInitialized;
        private bool _lastProSkin;            // UX-09: Track theme for style refresh

        // Copy feedback
        private double _copyFeedbackTime;
        private int _copyFeedbackIndex = -1;

        // Prefs
        private const string SystemPromptPref = "McpUnity_ChatSystemPrompt";
        private const string ChatHistorySessionKey = "McpUnity_ChatDisplayHistory";
        private const string ChatTokensSessionKey = "McpUnity_ChatTokens";
        private const int MaxPersistedEntries = 50;
        private const string ConversationStateKey = "McpUnity_ChatConversation";
        private const string InterruptedStateKey = "McpUnity_ChatInterruptedState";
        private const int MaxPersistedConversationMessages = 30;

        // H3: Async tool execution state
        private int _toolExecIndex;
        private List<ToolUseContent> _pendingToolBlocks;
        private ChatMessage _pendingToolResultMessage;
        private bool _toolExecCancelled;
        // Epoch counter — each new execution increments this; stale delayCall lambdas detect mismatch and bail
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

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            RestoreFromSessionState();
            // UX-03: No initial system message needed — welcome state handles empty chat
        }

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            _apiClient?.Dispose();
            _apiClient = null;
        }

        /// <summary>
        /// Called just before Unity performs a domain reload (script compilation).
        /// Persists the current execution state so we can resume after reload.
        /// </summary>
        private void OnBeforeAssemblyReload()
        {
            // Save display entries + conversation first
            SaveToSessionState();

            var state = new Dictionary<string, object>();

            if (_pendingToolBlocks != null && _toolExecIndex < _pendingToolBlocks.Count)
            {
                // Tool execution was in progress
                state["type"] = "tools";
                state["index"] = _toolExecIndex;
                state["total"] = _pendingToolBlocks.Count;

                // Save partial tool results collected so far
                var results = new List<object>();
                if (_pendingToolResultMessage?.content != null)
                {
                    foreach (var block in _pendingToolResultMessage.content)
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
            else if (_apiClient != null && _apiClient.IsProcessing)
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

        private McpChatToolBridge GetToolBridge()
        {
            var registry = McpUnityServer.ToolRegistry;

            // Re-create bridge whenever the registry becomes available or changes.
            // The bridge is cached as null when the server hasn't initialized yet (ToolRegistry is null
            // at first call on some domain reloads). Without this check the bridge keeps a null registry
            // forever and GetToolDefinitions() always returns an empty list → LLM hallucinates tool XML.
            if (_toolBridge == null || (_toolBridge.Registry == null && registry != null))
            {
                _toolBridge = new McpChatToolBridge(registry);
            }

            return _toolBridge;
        }

        // ====================================================================
        // Styles
        // ====================================================================

        private void InitStyles()
        {
            // UX-09: Detect theme change (light/dark mode toggle) and re-initialize styles
            bool isProSkin = EditorGUIUtility.isProSkin;
            if (_stylesInitialized && isProSkin == _lastProSkin) return;
            _lastProSkin = isProSkin;

            _userBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(50, 4, 2, 2),
                wordWrap = true, richText = true, fontSize = 12
            };

            _assistantBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(4, 50, 2, 2),
                wordWrap = true, richText = true, fontSize = 12
            };

            _toolBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(20, 20, 1, 1),
                wordWrap = true, richText = true, fontSize = 11
            };

            _errorBubbleStyle = new GUIStyle(_toolBubbleStyle);

            _systemBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(20, 20, 4, 4),
                wordWrap = true, richText = true, fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            _inputStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true, fontSize = 13,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _codeBlockStyle = new GUIStyle(EditorStyles.label)
            {
                font = Font.CreateDynamicFontFromOSFont(Application.platform == RuntimePlatform.OSXEditor ? "Menlo" : "Consolas", 11),
                richText = true,
                wordWrap = true,
                fontSize = 11,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(4, 4, 2, 2),
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            _h1Style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 6, 4)
            };
            _h2Style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 5, 3)
            };
            _h3Style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13, richText = true, wordWrap = true,
                margin = new RectOffset(4, 4, 4, 2)
            };

            _copyButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                fixedWidth = 40,
                fixedHeight = 16,
                margin = new RectOffset(0, 4, 0, 0)
            };

            _langLabelStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                alignment = TextAnchor.UpperLeft // UX-06: Language label on left side of code block header
            };

            _copiedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true
            };

            _roleLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                richText = true,
                margin = new RectOffset(4, 4, 6, 1)
            };

            _tableHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _tableHeaderStyle.normal.textColor = isProSkin ? new Color(0.9f, 0.9f, 0.95f) : new Color(0.1f, 0.1f, 0.15f);

            _tableCellStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                richText = true,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 2, 2)
            };

            _placeholderStyle = new GUIStyle(_inputStyle);
            _placeholderStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);

            _hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                richText = true,
                fontSize = 9
            };

            _chipStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(6, 4, 2, 2),
                margin = new RectOffset(2, 2, 2, 2),
                richText = true
            };

            _chipRemoveStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                padding = new RectOffset(3, 3, 2, 2),
                margin = new RectOffset(0, 4, 2, 2),
                fixedWidth = 18,
                fixedHeight = 18
            };

            _dropHintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };

            _blockquoteStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                padding = new RectOffset(14, 10, 6, 6),
                margin = new RectOffset(12, 12, 2, 2), // UX-10: Symmetric margins for blockquotes
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            // UX-07: Timestamp style
            _timestampStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                richText = true,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.6f) },
                margin = new RectOffset(4, 4, 0, 2)
            };

            // UX-01: Thinking indicator style — uses helpBox base for visible bubble background
            _thinkingStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(4, 50, 2, 2),
                normal = { textColor = new Color(0.7f, 0.8f, 0.95f) }
            };

            // UX-03: Welcome screen styles
            _welcomeHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = true,
                margin = new RectOffset(20, 20, 40, 4)
            };

            _welcomeSubStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                margin = new RectOffset(40, 40, 0, 20)
            };

            _suggestionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                wordWrap = true,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(40, 40, 2, 2),
                alignment = TextAnchor.MiddleLeft
            };

            // UX-05: Link button style
            _linkButtonStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                richText = true,
                normal = { textColor = new Color(0.38f, 0.69f, 0.94f) },
                hover = { textColor = new Color(0.55f, 0.80f, 1f) },
                margin = new RectOffset(8, 4, 0, 2)
            };

            // Compact tool/result one-liner (clickable foldout-like)
            _toolCompactStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontSize = 10,
                padding = new RectOffset(6, 6, 3, 3),
                margin = new RectOffset(24, 24, 1, 1),
                wordWrap = false
            };

            _stylesInitialized = true;
        }

        // ====================================================================
        // Main Draw (called by host)
        // ====================================================================

        public void Draw()
        {
            if (_apiClient == null) return;
            InitStyles();

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
            DrawInputArea();
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
            // Show cumulative in/out for billing, but context % uses last API response (accurate)
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
            // Use last API response tokens for accurate context usage (not cumulative)
            int totalUsed = _lastInputTokens + _lastOutputTokens;
            if (totalUsed <= 0) return;

            var provider = ProviderRegistry.GetActiveProvider();
            int maxContext = provider.MaxContextTokens;
            if (maxContext <= 0) return;

            float ratio = Mathf.Clamp01((float)totalUsed / maxContext);

            // Thin bar (3px)
            Rect barRect = EditorGUILayout.GetControlRect(false, 3);

            // Background
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));

            // Fill — green → yellow → red as usage increases
            Color fillColor;
            if (ratio < 0.5f)
                fillColor = Color.Lerp(new Color(0.3f, 0.8f, 0.4f), new Color(0.9f, 0.8f, 0.2f), ratio * 2f);
            else
                fillColor = Color.Lerp(new Color(0.9f, 0.8f, 0.2f), new Color(0.9f, 0.3f, 0.2f), (ratio - 0.5f) * 2f);

            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
            EditorGUI.DrawRect(fillRect, fillColor);

            // Tooltip on hover with detailed info
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

                    // Capture for closure
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

        /// <summary>Format token count: 1234 → "1.2k", 12345 → "12k", 123 → "123".</summary>
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

            GUILayout.Label("MCP Unity Chat (Beta)", _welcomeHeaderStyle);
            GUILayout.Label(
                "Ask questions about your project, inspect scenes, modify GameObjects,\nand automate workflows — all powered by AI with 138 tools.",
                _welcomeSubStyle);

            GUILayout.Space(12);

            // Suggested prompts
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
                if (GUILayout.Button(suggestion, _suggestionStyle, GUILayout.MaxWidth(500)))
                {
                    // Strip the prefix icon and send as a message
                    _inputText = suggestion.Substring(3).Trim();
                    SendMessage();
                }
                GUILayout.Space(40);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("<color=#666666>Tip: Drag assets or GameObjects onto the input field to reference them</color>",
                _welcomeSubStyle);

            GUILayout.FlexibleSpace();
        }

        private void DrawChatEntry(ChatDisplayEntry entry, int entryIndex)
        {
            Color oldBg;
            // UX-10: Clamp max bubble width between 300-700px
            float rawWidth = _hostWindow != null ? _hostWindow.position.width * 0.75f : 400f;
            float maxWidth = Mathf.Clamp(rawWidth, 300f, 700f);

            // Turn separator: draw a thin line when transitioning from assistant/tool/error back to user
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
                    // Role label — right-aligned "You" + timestamp
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (timeStr != null)
                        GUILayout.Label(timeStr, _timestampStyle);
                    GUILayout.Label("<color=#6699cc>You</color>", _roleLabelStyle);
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);

                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.25f, 0.45f, 0.7f, 0.5f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(entry.text, _userBubbleStyle, GUILayout.MaxWidth(maxWidth));
                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = oldBg;
                    break;

                case ChatDisplayEntry.EntryType.Assistant:
                    // Role label — left-aligned model name + timestamp
                    {
                        var activeProvider = ProviderRegistry.GetActiveProvider();
                        string currentModel = ProviderRegistry.GetModel(activeProvider.Id);
                        int modelIdx = Array.IndexOf(activeProvider.ModelIds, currentModel);
                        string mName = modelIdx >= 0 && modelIdx < activeProvider.ModelLabels.Length
                            ? activeProvider.ModelLabels[modelIdx] : activeProvider.DisplayName;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label($"<color=#66bb77>{mName}</color>", _roleLabelStyle);
                        if (timeStr != null && !entry.isStreaming)
                            GUILayout.Label(timeStr, _timestampStyle);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2);
                    }

                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.22f, 0.24f, 0.28f, 0.35f);

                    if (entry.isStreaming)
                    {
                        // UX-01: Show "Thinking..." before first text delta
                        if (entry.isWaitingForFirstChunk)
                        {
                            int dotCount = (int)(EditorApplication.timeSinceStartup * 3) % 4;
                            string dots = new string('.', dotCount);
                            EditorGUILayout.LabelField($"Thinking{dots}", _thinkingStyle);
                            _hostWindow?.Repaint();
                        }
                        else
                        {
                            // UX-02: Use full markdown parser during streaming (progressive rendering)
                            string rawText = entry.streamingBuilder != null
                                ? entry.streamingBuilder.ToString()
                                : (entry.text ?? "");

                            if (!string.IsNullOrEmpty(rawText))
                            {
                                // Throttle re-parse to 10Hz max during streaming (text is quadratic otherwise).
                                // IMPORTANT: only re-parse during EventType.Layout — never during Repaint.
                                // Layout and Repaint are separate OnGUI passes; if parsedSegments changes
                                // between them the GUILayout control counts diverge → "Getting control N" crash.
                                double now = EditorApplication.timeSinceStartup;
                                if (entry.parsedSegments == null ||
                                    (Event.current.type == EventType.Layout && now - entry.lastParseTime > 0.1))
                                {
                                    entry.parsedSegments = new List<object>(McpMarkdownRenderer.Parse(rawText));
                                    entry.lastParseTime = now;
                                }

                                // Render with full markdown support (code blocks visible during streaming)
                                DrawMarkdownEntry(entry);

                                // Blinking cursor after the last segment
                                // IMPORTANT: Always emit the label to keep IMGUI control count stable
                                // between Layout and Repaint passes — toggle content, not existence
                                int cursorPhase = (int)(EditorApplication.timeSinceStartup * 2.5) % 2;
                                string cursorText = cursorPhase == 0 ? "<color=#88ccff> \u258C</color>" : " ";
                                GUILayout.Label(cursorText, _roleLabelStyle);
                            }
                            _hostWindow?.Repaint();
                        }
                    }
                    else
                    {
                        DrawMarkdownEntry(entry);
                        // Hide the generic "Copy" button when the message already has code blocks
                        // (each code block has its own ⎘ copy button, avoid duplicate)
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
                        // Compact one-liner: ▶ ⚡ tool_name (click to expand input)
                        string foldIcon = entry.isExpanded ? "\u25BC" : "\u25B6";
                        Rect toolCallRect = EditorGUILayout.GetControlRect(false, 18);
                        // Subtle background line
                        Rect toolBgRect = new Rect(toolCallRect.x + 20, toolCallRect.y, toolCallRect.width - 40, toolCallRect.height);
                        EditorGUI.DrawRect(toolBgRect, new Color(0.4f, 0.35f, 0.15f, 0.12f));
                        // Clickable label
                        string toolCallLabel = $"<color=#887733>{foldIcon}</color>  <color=#ccaa44>\u26A1 {entry.toolName}</color>";
                        if (GUI.Button(toolBgRect, toolCallLabel, _toolCompactStyle))
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
                                _toolBubbleStyle);
                            GUI.backgroundColor = oldBg;
                        }
                    }
                    break;

                case ChatDisplayEntry.EntryType.ToolResult:
                    {
                        // Compact one-liner: ▶ ✓ tool_name (size) — click to expand
                        string full = entry.fullText ?? entry.text ?? "";
                        string foldIcon = entry.isExpanded ? "\u25BC" : "\u25B6";
                        string statusIcon = "\u2713";
                        string statusColor = "#44cc66";
                        string sizeHint = full.Length > 100 ? $"  <color=#666666>({full.Length:N0} chars)</color>" : "";

                        Rect toolResRect = EditorGUILayout.GetControlRect(false, 18);
                        Rect resBgRect = new Rect(toolResRect.x + 20, toolResRect.y, toolResRect.width - 40, toolResRect.height);
                        EditorGUI.DrawRect(resBgRect, new Color(0.15f, 0.35f, 0.2f, 0.12f));

                        string resultLabel = $"<color=#336644>{foldIcon}</color>  <color={statusColor}>{statusIcon} {entry.toolName}</color>{sizeHint}";
                        if (GUI.Button(resBgRect, resultLabel, _toolCompactStyle))
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
                                _toolBubbleStyle);
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
                    EditorGUILayout.LabelField(prefix + entry.text, _errorBubbleStyle);
                    GUI.backgroundColor = oldBg;
                    DrawRetryButton();
                    break;

                case ChatDisplayEntry.EntryType.System:
                    oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.3f, 0.3f, 0.35f, 0.3f);
                    EditorGUILayout.LabelField($"<color=#aaaaaa>{entry.text}</color>", _systemBubbleStyle);
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
                        EditorGUILayout.LabelField(seg.Text, _assistantBubbleStyle);

                        // UX-05: Render clickable link buttons below prose segments that contain links
                        if (seg.Links != null && seg.Links.Count > 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(8);
                            foreach (string url in seg.Links)
                            {
                                // Truncate long URLs for display
                                string displayUrl = url.Length > 50 ? url.Substring(0, 47) + "..." : url;
                                if (GUILayout.Button($"\u2197 {displayUrl}", _linkButtonStyle))
                                    Application.OpenURL(url);
                                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                            }
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                        break;

                    case McpMarkdownRenderer.SegmentType.CodeBlock:
                        // UX-04: Apply syntax highlighting if language is known
                        string displayCode = McpMarkdownRenderer.HighlightCode(seg.Text, seg.Language);
                        var content = new GUIContent(displayCode);
                        float height = _codeBlockStyle.CalcHeight(content, availWidth);
                        height = Mathf.Max(height, 20f);

                        // UX-06: Fixed header bar for language + copy button (no overlap)
                        bool hasLang = !string.IsNullOrEmpty(seg.Language);
                        float headerHeight = hasLang ? 18f : 4f;

                        Rect bgRect = EditorGUILayout.GetControlRect(false, height + headerHeight + 8);
                        EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.12f, 0.95f));

                        if (hasLang)
                        {
                            // UX-06: Language label on left, copy button on right — separate row
                            Rect langRect = new Rect(bgRect.x + 12, bgRect.y + 2, bgRect.width * 0.5f, 14);
                            GUI.Label(langRect, $"<color=#555555><size=9>{seg.Language}</size></color>", _langLabelStyle);
                        }

                        // Copy button — top-right, small clipboard symbol
                        Rect copyBtnRect = new Rect(bgRect.xMax - 24, bgRect.y + 2, 18, 14);
                        if (GUI.Button(copyBtnRect, "\u2398", EditorStyles.miniButton))
                            EditorGUIUtility.systemCopyBuffer = seg.Text; // Copy raw text, not highlighted

                        // Code text below the header
                        Rect textRect = new Rect(bgRect.x + 8, bgRect.y + headerHeight + 2, bgRect.width - 16, height);
                        EditorGUI.SelectableLabel(textRect, displayCode, _codeBlockStyle);
                        break;

                    case McpMarkdownRenderer.SegmentType.Header:
                        var hStyle = seg.HeaderLevel <= 1 ? _h1Style : seg.HeaderLevel == 2 ? _h2Style : _h3Style;
                        EditorGUILayout.LabelField(seg.Text, hStyle);
                        break;

                    case McpMarkdownRenderer.SegmentType.Blockquote:
                        var bqContent = new GUIContent(seg.Text);
                        float bqHeight = _blockquoteStyle.CalcHeight(bqContent, availWidth - 30f);
                        bqHeight = Mathf.Max(bqHeight, 20f);
                        Rect bqRect = EditorGUILayout.GetControlRect(false, bqHeight + 4);
                        // Left accent bar
                        Rect barRect = new Rect(bqRect.x + 8, bqRect.y, 3, bqRect.height);
                        EditorGUI.DrawRect(barRect, new Color(0.4f, 0.6f, 0.9f, 0.7f));
                        // Subtle background
                        Rect bqBg = new Rect(bqRect.x + 12, bqRect.y, bqRect.width - 16, bqRect.height);
                        EditorGUI.DrawRect(bqBg, new Color(0.2f, 0.2f, 0.25f, 0.2f));
                        // Text
                        Rect bqTextRect = new Rect(bqRect.x + 14, bqRect.y + 2, bqRect.width - 24, bqRect.height - 4);
                        EditorGUI.LabelField(bqTextRect, seg.Text, _blockquoteStyle);
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

            // Header row
            Rect headerRect = EditorGUILayout.GetControlRect(false, rowHeight + 4);
            EditorGUI.DrawRect(headerRect, new Color(0.22f, 0.22f, 0.30f, 0.85f));
            for (int c = 0; c < colCount; c++)
            {
                Rect cellRect = new Rect(headerRect.x + 8 + c * colWidth, headerRect.y + 2, colWidth - 4, rowHeight);
                // Vertical separator between columns
                if (c > 0)
                {
                    Rect sepV = new Rect(cellRect.x - 2, headerRect.y + 4, 1, rowHeight - 4);
                    EditorGUI.DrawRect(sepV, new Color(0.45f, 0.45f, 0.55f, 0.5f));
                }
                EditorGUI.LabelField(cellRect, c < headers.Length ? headers[c] : "", _tableHeaderStyle);
            }

            // Separator line
            Rect topSep = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(topSep, new Color(0.4f, 0.4f, 0.55f, 0.7f));

            // Data rows
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
                    EditorGUI.LabelField(cellRect, cellText, _tableCellStyle);
                }
            }

            // Bottom border
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
                GUILayout.Label("<color=#66cc88>Copied!</color>", _copiedLabelStyle);
            }
            else
            {
                if (GUILayout.Button("Copy", _copyButtonStyle))
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
                retryEntry.isWaitingForFirstChunk = true; // UX-01
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
        // Input Area
        // ====================================================================

        private void DrawInputArea()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool shouldSend = false;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && !e.shift
                && GUI.GetNameOfFocusedControl() == "ChatInput")
            {
                shouldSend = true;
                e.Use();
            }

            // Asset reference chips bar (above input)
            DrawAssetChips();

            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName("ChatInput");
            _inputText = EditorGUILayout.TextArea(_inputText, _inputStyle, GUILayout.MinHeight(36), GUILayout.MaxHeight(80));

            // Drag & drop overlay on the TextArea
            Rect inputRect = GUILayoutUtility.GetLastRect();
            HandleDragAndDrop(inputRect);

            // Placeholder text overlay when input is empty and not focused
            if (string.IsNullOrEmpty(_inputText) && _referencedAssets.Count == 0
                && GUI.GetNameOfFocusedControl() != "ChatInput")
            {
                GUI.Label(inputRect, "  Ask about your project... (drag assets here)", _placeholderStyle);
            }

            // Drop hint overlay when dragging
            if (_isDragHovering)
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.15f);
                GUI.Box(inputRect, "", EditorStyles.helpBox);
                GUI.backgroundColor = oldBg;
                GUI.Label(inputRect, "Drop to reference asset", _dropHintStyle);
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(60));

            if (_apiClient.IsProcessing)
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.35f, 0.3f);
                if (GUILayout.Button("Stop", GUILayout.Height(36)))
                {
                    _apiClient.Cancel();
                    FinalizeStreamingEntry();
                }
                GUI.backgroundColor = oldBg;
            }
            else
            {
                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_inputText) || !McpChatApiClient.HasAuth);
                if (GUILayout.Button("Send", GUILayout.Height(36)) || shouldSend)
                    SendMessage();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            // Keyboard hints
            if (_apiClient.IsProcessing)
                GUILayout.Label("Esc to cancel", _hintStyle);
            else if (!string.IsNullOrEmpty(_inputText))
                GUILayout.Label("Enter = send  |  Shift+Enter = newline", _hintStyle);

            if (!McpChatApiClient.HasAuth)
                EditorGUILayout.HelpBox("Authentication required. Click Settings to configure.", MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        // ====================================================================
        // Drag & Drop
        // ====================================================================

        private void HandleDragAndDrop(Rect dropArea)
        {
            _isDragHovering = false;

            if (!dropArea.Contains(Event.current.mousePosition))
                return;

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        _isDragHovering = true;
                        Event.current.Use();
                        _hostWindow?.Repaint();
                    }
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj == null) continue;
                        if (_referencedAssets.Count >= MaxReferencedAssets) break;

                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        bool isSceneObject = false;
                        if (obj is GameObject goCheck)
                            isSceneObject = string.IsNullOrEmpty(assetPath) || goCheck.scene.IsValid();

                        var assetRef = new AssetReference();

                        if (isSceneObject && obj is GameObject sceneGo)
                        {
                            // Scene GameObject from Hierarchy
                            assetRef.displayName = sceneGo.name;
                            assetRef.gameObjectPath = GetGameObjectPath(sceneGo);
                            assetRef.isSceneObject = true;
                            assetRef.typeName = "GameObject";
                        }
                        else if (!string.IsNullOrEmpty(assetPath))
                        {
                            // Project asset
                            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                            assetRef.displayName = obj.name;
                            assetRef.assetPath = assetPath;
                            assetRef.isSceneObject = false;
                            assetRef.typeName = assetType?.Name ?? "Object";
                        }
                        else
                        {
                            continue;
                        }

                        // Avoid duplicates
                        bool exists = false;
                        for (int i = 0; i < _referencedAssets.Count; i++)
                        {
                            var existing = _referencedAssets[i];
                            if (existing.isSceneObject == assetRef.isSceneObject
                                && existing.displayName == assetRef.displayName
                                && existing.assetPath == assetRef.assetPath
                                && existing.gameObjectPath == assetRef.gameObjectPath)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            _referencedAssets.Add(assetRef);
                            // Insert @mention into input text
                            string mention = "@" + assetRef.displayName;
                            if (string.IsNullOrEmpty(_inputText))
                                _inputText = mention + " ";
                            else if (!_inputText.Contains(mention))
                                _inputText = _inputText.TrimEnd() + " " + mention + " ";
                        }
                    }

                    Event.current.Use();
                    _hostWindow?.Repaint();
                    break;

                case EventType.DragExited:
                    _isDragHovering = false;
                    _hostWindow?.Repaint();
                    break;
            }
        }

        private void DrawAssetChips()
        {
            if (_referencedAssets.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Refs:", EditorStyles.miniLabel, GUILayout.Width(30));

            int removeIndex = -1;
            for (int i = 0; i < _referencedAssets.Count; i++)
            {
                var assetRef = _referencedAssets[i];
                string icon = assetRef.isSceneObject ? "\u25CB " : "\u25A0 "; // circle for GO, square for asset
                string label = icon + assetRef.displayName;
                string tooltip = assetRef.isSceneObject
                    ? $"Scene: {assetRef.gameObjectPath}"
                    : $"{assetRef.typeName}: {assetRef.assetPath}";

                GUILayout.Label(new GUIContent(label, tooltip), _chipStyle);
                if (GUILayout.Button("x", _chipRemoveStyle))
                {
                    removeIndex = i;
                }
            }

            if (removeIndex >= 0)
            {
                // Remove @mention from input text
                string mention = "@" + _referencedAssets[removeIndex].displayName;
                _inputText = _inputText.Replace(mention, "").Replace("  ", " ").Trim();
                _referencedAssets.RemoveAt(removeIndex);
                _hostWindow?.Repaint();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ====================================================================
        // Message Sending
        // ====================================================================

        private void SendMessage()
        {
            string text = _inputText.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputText = "";
            GUI.FocusControl(null);

            var bridge = GetToolBridge();

            // Enrich text with asset context for the LLM (invisible to user display)
            string displayText = text;
            string llmText = _referencedAssets.Count > 0
                ? bridge.BuildEnrichedUserText(text, _referencedAssets)
                : text;

            // Clear refs after capturing them
            _referencedAssets.Clear();

            // Conversation stores the enriched text (what the LLM sees)
            var userMsg = new ChatMessage("user", llmText);
            _conversation.Add(userMsg);

            // Display shows the clean text (what the user typed)
            _displayEntries.Add(ChatDisplayEntry.UserMessage(displayText));
            var assistantEntry = ChatDisplayEntry.AssistantMessage("", true);
            assistantEntry.isWaitingForFirstChunk = true; // UX-01: Thinking state until first delta
            _displayEntries.Add(assistantEntry);
            _autoScroll = true;

            string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
            string sys = bridge.BuildSystemPrompt(customPrompt);
            var msgs = bridge.BuildMessagesArray(_conversation);
            var tools = bridge.GetToolDefinitions();

            _apiClient.SendStreamingRequest(sys, msgs, tools);
            SaveToSessionState();
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
                // UX-01: First chunk received — stop showing "Thinking..."
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
                // Track last response for accurate context window calculation
                // (input_tokens already includes full conversation history each call)
                _lastInputTokens = state.usage.input_tokens;
                _lastOutputTokens = state.usage.output_tokens;
            }

            var assistantMsg = new ChatMessage { role = "assistant", content = new List<ContentBlock>(state.contentBlocks) };
            _conversation.Add(assistantMsg);

            // "tool_use" is normalized by all providers (OpenAI provider maps "tool_calls" → "tool_use")
            if (state.stopReason == "tool_use")
            {
                var blocks = state.GetToolUseBlocks();
                if (blocks.Count > 0)
                {
                    // Update ToolCall display entries with the fully-accumulated input JSON.
                    // HandleToolCallStarted fires at content_block_start (input is empty then);
                    // the actual args arrive via input_json_delta into rawJsonBuilder.
                    // Now that streaming is complete we can fill in the expandable summary.
                    UpdateToolCallDisplayEntries(blocks);
                    ExecuteToolsAndContinue(blocks);
                    return;
                }
            }

            SaveToSessionState();
            _hostWindow?.Repaint();
        }

        /// <summary>
        /// Back-fill the text of ToolCall display entries with the fully-streamed input JSON.
        /// Called once streaming is complete, so rawJsonBuilder contains the complete arguments.
        /// </summary>
        private void UpdateToolCallDisplayEntries(List<ToolUseContent> toolBlocks)
        {
            // Walk display entries in reverse to match each tool block from the most recent entries.
            // This handles the rare case of multiple tool calls in one turn correctly.
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
            // Clear partial streaming text from the current entry
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
            int thisEpoch = ++_toolExecEpoch; // New epoch — invalidates any stale callbacks from prior executions

            // Update streaming entry to show tool execution progress
            UpdateToolExecProgress(bridge);

            // Execute first tool on next frame (yields to editor for repaint + input)
            EditorApplication.delayCall += () => ExecuteNextTool(thisEpoch);
        }

        private void ExecuteNextTool(int epoch)
        {
            // Stale callback guard — bail out if a Cancel or new send has started since we were scheduled
            if (epoch != _toolExecEpoch) return;

            if (_pendingToolBlocks == null) return;

            // Check cancellation
            if (_toolExecCancelled || _toolExecIndex >= _pendingToolBlocks.Count)
            {
                FinalizeToolExecution();
                return;
            }

            var bridge = GetToolBridge();
            var tu = _pendingToolBlocks[_toolExecIndex];

            // ── Confirmation gate for destructive/high-impact tools ──
            if (McpChatToolBridge.RequiresConfirmation(tu.name))
            {
                // Build a readable summary of what the tool wants to do
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
                    // User denied — inject a denial result so the LLM knows
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

                    // Continue to next tool or finalize
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
                // User approved — fall through to normal execution
            }

            // Execute one tool synchronously (individual tool still blocks, but we yield between tools)
            var result = bridge.ExecuteToolUse(tu);

            // Store full text, display truncated
            var displayEntry = ChatDisplayEntry.ToolResultEntry(tu.name, Truncate(result.content, 500), result.is_error);
            displayEntry.fullText = result.content;
            _displayEntries.Add(displayEntry);
            _pendingToolResultMessage.content.Add(result);

            _toolExecIndex++;
            _hostWindow?.Repaint();

            // Schedule next tool or finalize — propagate epoch so stale detection works across all rounds
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

        /// <summary>
        /// Build a human-readable summary of tool arguments for the confirmation dialog.
        /// Shows the most relevant parameters without overwhelming the user.
        /// </summary>
        private static string BuildToolArgSummary(string toolName, Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var kvp in args)
            {
                if (shown >= 5) { sb.Append("\n  ..."); break; } // Limit to 5 params

                string val = kvp.Value?.ToString() ?? "null";
                // Truncate long values (e.g., script content)
                if (val.Length > 120) val = val.Substring(0, 117) + "...";

                sb.Append($"\n  {kvp.Key}: {val}");
                shown++;
            }
            return sb.ToString();
        }

        private void UpdateToolExecProgress(McpChatToolBridge bridge)
        {
            // Update the streaming entry to show which tool is being executed
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
                // Cancelled mid-execution — finalize what we have
                FinalizeStreamingEntry();
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                    $"Tool execution cancelled ({_toolExecIndex}/{_pendingToolBlocks.Count} tools completed)."));

                if (_pendingToolResultMessage.content.Count > 0)
                    _conversation.Add(_pendingToolResultMessage);

                _pendingToolBlocks = null;
                _pendingToolResultMessage = null;
                _apiClient.Cancel();
                SaveToSessionState();
                _hostWindow?.Repaint();
                return;
            }

            // All tools executed — continue conversation
            _conversation.Add(_pendingToolResultMessage);

            // Replace the streaming entry with a fresh one for the next response
            FinalizeStreamingEntry();
            var contEntry = ChatDisplayEntry.AssistantMessage("", true);
            contEntry.isWaitingForFirstChunk = true; // UX-01
            _displayEntries.Add(contEntry);
            _autoScroll = true;

            var bridge = GetToolBridge();
            string customPrompt = EditorPrefs.GetString(SystemPromptPref, "");
            string sys = bridge.BuildSystemPrompt(customPrompt);
            var msgs = bridge.BuildMessagesArray(_conversation);
            var tools = bridge.GetToolDefinitions();

            // Check if any tool in this round returned an error
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

                // Materialize streaming builder → text field
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
        // Compact — summarize conversation to free context (like Claude Code)
        // ====================================================================

        private void CompactConversation()
        {
            if (_conversation.Count < 4 || _isCompacting || _apiClient.IsProcessing) return;

            _isCompacting = true;
            _displayEntries.Add(ChatDisplayEntry.SystemMessage("Compacting conversation..."));
            _autoScroll = true;
            _hostWindow?.Repaint();

            // Build a summary request from the full conversation history
            var summaryMessages = new List<object>();

            // Serialize the conversation into a single user message asking for summary
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

            // Create a temporary one-shot API client for the compact request
            var compactClient = new McpChatApiClient();

            compactClient.OnStreamComplete += (state) =>
            {
                string summary = state.CurrentText;
                if (string.IsNullOrWhiteSpace(summary))
                {
                    _isCompacting = false;
                    _displayEntries.Add(ChatDisplayEntry.SystemMessage("Compact failed — empty summary."));
                    _hostWindow?.Repaint();
                    compactClient.Dispose();
                    return;
                }

                // Replace conversation with the summary
                int oldCount = _conversation.Count;
                _conversation.Clear();

                // Insert summary as user→assistant pair so the conversation starts with "user"
                // (required by OpenAI and most providers — a conversation must begin with a user message)
                _conversation.Add(new ChatMessage("user", "[Previous conversation compacted — summary follows]"));
                _conversation.Add(new ChatMessage("assistant", $"[Conversation Summary]\n{summary}"));

                // Replace display entries — keep only the compact notification
                _displayEntries.Clear();
                _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                    $"Conversation compacted: {oldCount} messages \u2192 summary.\nContext freed for new messages."));
                _displayEntries.Add(ChatDisplayEntry.AssistantMessage($"**Conversation Summary**\n\n{summary}"));

                // Reset token counts — the summary is much smaller
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
                SaveToSessionState();
                _autoScroll = true;
                _hostWindow?.Repaint();
                compactClient.Dispose();
            };

            compactClient.OnError += (err) =>
            {
                _isCompacting = false;
                _displayEntries.Add(ChatDisplayEntry.SystemMessage($"Compact failed: {err}"));
                _hostWindow?.Repaint();
                compactClient.Dispose();
            };

            // Send with no tools — pure text summarization
            string compactSystemPrompt = "You are a conversation summarizer. Produce a concise, structured summary of the provided conversation.";
            compactClient.SendStreamingRequest(compactSystemPrompt, summaryMessages, new List<object>());
        }

        /// <summary>Check context usage and suggest compact if above threshold.</summary>
        private void CheckCompactSuggestion()
        {
            if (_compactSuggested || _isCompacting || _conversation.Count < 6) return;

            var provider = ProviderRegistry.GetActiveProvider();
            int maxCtx = provider.MaxContextTokens;
            if (maxCtx <= 0) return;

            // Use last API response for accurate context window usage
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
            _toolExecEpoch++; // Invalidate any delayCall lambdas scheduled before the clear
            // UX-03: No system message after clear — welcome state shows automatically
            SessionState.EraseString(ChatHistorySessionKey);
            SessionState.EraseString(ChatTokensSessionKey);
            SessionState.EraseString(ConversationStateKey);
        }

        // ====================================================================
        // Export
        // ====================================================================

        private void ExportConversation(string format)
        {
            string ext = format;
            string filter = format == "json" ? "JSON files" : format == "md" ? "Markdown files" : "Text files";
            string defaultName = $"chat-export-{DateTime.Now:yyyy-MM-dd-HHmm}.{ext}";

            string path = EditorUtility.SaveFilePanel($"Export Conversation ({format.ToUpper()})", "", defaultName, ext);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string content = BuildExportContent(format);
                System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", $"Failed to export conversation:\n{ex.Message}", "OK");
            }
        }

        private void CopyConversation(string format)
        {
            string content = BuildExportContent(format);
            EditorGUIUtility.systemCopyBuffer = content;
        }

        private string BuildExportContent(string format)
        {
            switch (format)
            {
                case "json": return BuildJsonExport();
                case "md":   return BuildMarkdownExport();
                case "txt":  return BuildTextExport();
                default:     return BuildTextExport();
            }
        }

        private string BuildJsonExport()
        {
            var export = new Dictionary<string, object>();
            var provider = ProviderRegistry.GetActiveProvider();
            string model = ProviderRegistry.GetModel(provider.Id);

            export["exportedAt"] = DateTime.UtcNow.ToString("o");
            export["provider"] = provider.DisplayName;
            export["model"] = model;
            export["totalInputTokens"] = _totalInputTokens;
            export["totalOutputTokens"] = _totalOutputTokens;

            var messages = new List<object>();
            foreach (var entry in _displayEntries)
            {
                if (entry.type == ChatDisplayEntry.EntryType.System && entry.text.StartsWith("MCP Unity Chat"))
                    continue; // Skip welcome message

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

        private string BuildMarkdownExport()
        {
            var sb = new System.Text.StringBuilder();
            var provider = ProviderRegistry.GetActiveProvider();
            string model = ProviderRegistry.GetModel(provider.Id);

            sb.AppendLine($"# Chat Export — {provider.DisplayName} / {model}");
            sb.AppendLine($"*Exported: {DateTime.Now:yyyy-MM-dd HH:mm}*");
            sb.AppendLine($"*Tokens: {_totalInputTokens} in / {_totalOutputTokens} out*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var entry in _displayEntries)
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

        private string BuildTextExport()
        {
            var sb = new System.Text.StringBuilder();
            var provider = ProviderRegistry.GetActiveProvider();
            string model = ProviderRegistry.GetModel(provider.Id);

            sb.AppendLine($"Chat Export — {provider.DisplayName} / {model}");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Tokens: {_totalInputTokens} in / {_totalOutputTokens} out");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            foreach (var entry in _displayEntries)
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
        private static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // Remove <b>, </b>, <i>, </i>, <color=...>, </color>, <size=...>, </size>
            return System.Text.RegularExpressions.Regex.Replace(text, @"<\/?(?:b|i|color|size)[^>]*>", "");
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max) + "...";
        }

        // ====================================================================
        // Domain Reload Resume
        // ====================================================================

        /// <summary>
        /// Check for interrupted execution state saved before domain reload and schedule a resume.
        /// Returns true if a resume was scheduled (caller should skip the generic "Session restored" message).
        /// </summary>
        private bool TryScheduleResume()
        {
            string json = SessionState.GetString(InterruptedStateKey, "");
            SessionState.EraseString(InterruptedStateKey);

            if (string.IsNullOrEmpty(json)) return false;

            var parsed = JsonHelper.ParseJsonObject(json);
            if (!(parsed is Dictionary<string, object> state)) return false;

            string savedType = state.TryGetValue("type", out var tv) ? tv?.ToString() : "none";
            if (savedType == "none") return false;

            // Source of truth: check actual conversation state.
            // If the last message is an assistant message with tool_use blocks and no
            // matching tool_result user message follows, we MUST resume tool execution
            // — not streaming — otherwise the API will reject the orphaned tool_use.
            bool orphanedToolUse = HasOrphanedToolUse();

            if (orphanedToolUse)
            {
                // Use saved partial results if available, otherwise start from 0
                return TryResumeToolExecution(savedType == "tools" ? state : new Dictionary<string, object>());
            }

            // Conversation is clean (no orphaned tool_use) — safe to re-send
            if (savedType == "streaming" || savedType == "tools")
                return TryResumeStreaming();

            return false;
        }

        /// <summary>
        /// Returns true if the last message in the conversation is an assistant message
        /// containing tool_use blocks without a following tool_result user message.
        /// This state is invalid for the API and must be resolved before re-sending.
        /// </summary>
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

        /// <summary>
        /// Resume tool execution from where it was interrupted by domain reload.
        /// Reconstructs pending tool blocks from the last assistant message in conversation,
        /// restores partial results, and continues executing remaining tools.
        /// </summary>
        private bool TryResumeToolExecution(Dictionary<string, object> state)
        {
            int completedIndex = state.TryGetValue("index", out var idx) ? System.Convert.ToInt32(idx) : 0;
            int total = state.TryGetValue("total", out var tot) ? System.Convert.ToInt32(tot) : 0;

            // Find tool_use blocks from the last assistant message in conversation
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

            // Reconstruct partial tool results collected before reload
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

            // Resume from where we left off
            _pendingToolBlocks = toolBlocks;
            _toolExecIndex = completedIndex;
            _toolExecCancelled = false;

            int actualTotal = toolBlocks.Count;
            _displayEntries.Add(ChatDisplayEntry.SystemMessage(
                completedIndex > 0
                    ? $"(Resuming after compilation \u2014 {completedIndex}/{actualTotal} tools done, continuing...)"
                    : $"(Resuming {actualTotal} tool(s) after compilation...)"));

            // Defer 2 frames to let McpUnityServer and McpToolRegistry initialize after domain reload
            int epoch = ++_toolExecEpoch;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () => ExecuteNextTool(epoch);
            };

            return true;
        }

        /// <summary>
        /// Resume a streaming API response that was interrupted by domain reload.
        /// Re-sends the current conversation to the API to get a fresh response.
        /// </summary>
        private bool TryResumeStreaming()
        {
            if (_conversation.Count == 0) return false;

            _displayEntries.Add(ChatDisplayEntry.SystemMessage("(Resuming response after compilation...)"));

            var assistantEntry = ChatDisplayEntry.AssistantMessage("", true);
            assistantEntry.isWaitingForFirstChunk = true;
            _displayEntries.Add(assistantEntry);
            _autoScroll = true;

            // Defer 2 frames to let McpUnityServer + ProviderRegistry initialize after domain reload
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
                    SaveToSessionState();
                    _hostWindow?.Repaint();
                };
            };

            return true;
        }

        // ====================================================================
        // SessionState Persistence
        // ====================================================================

        private void SaveToSessionState()
        {
            try
            {
                // Save display entries
                var entries = new List<object>();
                int start = Math.Max(0, _displayEntries.Count - MaxPersistedEntries);
                for (int i = start; i < _displayEntries.Count; i++)
                {
                    var e = _displayEntries[i];
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
                SessionState.SetString(ChatTokensSessionKey, $"{_totalInputTokens},{_totalOutputTokens}");

                // Save conversation context for API continuity across domain reloads
                SaveConversationToSession();
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to save session state: {ex.Message}");
            }
        }

        private void SaveConversationToSession()
        {
            var serialized = new List<object>();
            int msgStart = Math.Max(0, _conversation.Count - MaxPersistedConversationMessages);

            for (int i = msgStart; i < _conversation.Count; i++)
            {
                var msg = _conversation[i];
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

        private void RestoreFromSessionState()
        {
            try
            {
                // Restore display entries
                string json = SessionState.GetString(ChatHistorySessionKey, "");
                if (string.IsNullOrEmpty(json)) return;

                var parsed = JsonHelper.ParseJsonObject($"{{\"a\":{json}}}");
                if (parsed is Dictionary<string, object> wrapper
                    && wrapper.TryGetValue("a", out var arrObj)
                    && arrObj is List<object> arr)
                {
                    _displayEntries.Clear();
                    foreach (var item in arr)
                    {
                        if (item is Dictionary<string, object> d)
                        {
                            // SimpleJsonParser returns int for whole numbers, not double — use Convert to handle int/long/double uniformly
                            var entry = new ChatDisplayEntry
                            {
                                type = (ChatDisplayEntry.EntryType)(d.TryGetValue("t", out var tv) ? System.Convert.ToInt32(tv) : 0),
                                text = d.TryGetValue("x", out var xv) ? xv?.ToString() ?? "" : "",
                                toolName = d.TryGetValue("n", out var nv) ? nv?.ToString() ?? "" : "",
                                timestamp = d.TryGetValue("ts", out var tsv) ? System.Convert.ToInt64(tsv) : 0
                            };
                            // Restore full text for expandable tool results
                            if (d.TryGetValue("f", out var fv) && fv != null)
                                entry.fullText = fv.ToString();
                            entry.UpdateTokenEstimate();
                            _displayEntries.Add(entry);
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
                        int.TryParse(parts[0], out _totalInputTokens);
                        int.TryParse(parts[1], out _totalOutputTokens);
                    }
                }

                // Restore conversation context (H2 — API continuity across domain reloads)
                bool conversationRestored = RestoreConversationFromSession();

                // Check for interrupted execution (tool exec or streaming) and auto-resume
                bool resumed = conversationRestored && TryScheduleResume();

                if (_displayEntries.Count > 0 && !resumed)
                {
                    string statusMsg = conversationRestored
                        ? $"(Session restored — conversation context preserved ({_conversation.Count} messages))"
                        : "(Session restored after reload — conversation context reset, previous messages shown for reference)";
                    _displayEntries.Add(ChatDisplayEntry.SystemMessage(statusMsg));
                }
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to restore session state: {ex.Message}");
            }
        }

        private bool RestoreConversationFromSession()
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

                _conversation.Clear();
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> msgDict)) continue;

                    var msg = new ChatMessage();
                    msg.role = msgDict.TryGetValue("role", out var rv) ? rv?.ToString() ?? "user" : "user";
                    // Convert handles int/long/double — SimpleJsonParser returns int for whole numbers
                    msg.timestamp = msgDict.TryGetValue("timestamp", out var tsv) ? System.Convert.ToInt64(tsv) : 0;
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

                    _conversation.Add(msg);
                }

                return _conversation.Count > 0;
            }
            catch (Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to restore conversation: {ex.Message}");
                return false;
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static string GetGameObjectPath(GameObject go)
            => GameObjectHelpers.GetGameObjectPath(go);
    }
}
