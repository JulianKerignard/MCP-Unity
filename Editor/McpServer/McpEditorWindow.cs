using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using McpUnity.Chat;
using McpUnity.Chat.Providers;
using McpUnity.Server;

namespace McpUnity.Editor
{
    /// <summary>
    /// Main Editor Window for MCP Unity — consolidated 3-tab layout.
    /// Tab 0: Chat (embedded McpChatPanel)
    /// Tab 1: Settings (Provider, Tools, Server, Advanced)
    /// Tab 2: Diagnostics (Monitor, Logs, Claude Config)
    /// Accessible via Tools > MCP Unity > Server Window
    /// </summary>
    public class McpEditorWindow : EditorWindow
    {
        // ====================================================================
        // Tab Management
        // ====================================================================
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Chat (Beta)", "Settings", "Diagnostics", "Setup" };

        // ====================================================================
        // Setup Tab State
        // ====================================================================
        private Vector2 _setupScrollPosition;

        // ====================================================================
        // Chat Panel
        // ====================================================================
        private McpChatPanel _chatPanel;

        // ====================================================================
        // Scroll Positions
        // ====================================================================
        private Vector2 _settingsScrollPosition;
        private Vector2 _diagnosticsScrollPosition;
        private Vector2 _logScrollPosition;
        private Vector2 _monitorScrollPosition;

        // ====================================================================
        // Log State
        // ====================================================================
        private string _cachedLogs = "";
        private int _lastLogCount = 0;
        private bool _useColoredLogs = true;
        private LogLevel _logFilterLevel;
        private bool _monitorAutoScroll = true;

        // ====================================================================
        // Provider Settings State (Settings tab, Section 1)
        // ====================================================================
        private int _settingsProviderIndex;
        private int _settingsModelIndex;
        private string _settingsApiKeyInput = "";
        private int _settingsMaxTokens = 4096;
        private float _settingsTemperature = 1f;
        private bool _settingsCredentialVisible;
        private int _settingsAuthMode; // 0=ApiKey, 1=OAuth
        private string _settingsOAuthTokenInput = "";
        private string _settingsOAuthCodeInput = "";
        private string _settingsOAuthStatus = "";
        private string _settingsCustomSystemPrompt = "";

        // API key dirty-write state — avoids writing to EditorPrefs on every frame while typing
        private string _settingsApiKeyCachedProviderId;
        private bool _settingsApiKeyDirty;

        // ====================================================================
        // Foldout Keys (SessionState)
        // ====================================================================
        private const string FoldoutProvider = "McpUnity_Foldout_Provider";
        private const string FoldoutTools = "McpUnity_Foldout_Tools";
        private const string FoldoutServer = "McpUnity_Foldout_Server";
        private const string FoldoutAdvanced = "McpUnity_Foldout_Advanced";
        private const string FoldoutMonitor = "McpUnity_Foldout_Monitor";
        private const string FoldoutLogs = "McpUnity_Foldout_Logs";
        private const string FoldoutClaudeConfig = "McpUnity_Foldout_ClaudeConfig";

        // ====================================================================
        // System Prompt Pref
        // ====================================================================
        private const string SystemPromptPref = "McpUnity_ChatSystemPrompt";

        // ====================================================================
        // Periodic Repaint
        // ====================================================================
        private double _lastRepaintTime;

        // ====================================================================
        // Styles
        // ====================================================================
        private GUIStyle _headerStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _logAreaStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _foldoutStyle;
        private GUIStyle _linkStyle;
        private bool _stylesInitialized = false;

        // Cached arrays
        private static readonly string[] AuthModeLabels = { "API Key", "OAuth (Pro/Max)" };

        // ====================================================================
        // Menu Items
        // ====================================================================

        [MenuItem("Tools/MCP Unity/Server Window", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<McpEditorWindow>("MCP Unity");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        [MenuItem("Tools/MCP Unity/Quick Start Server", priority = 101)]
        public static void QuickStartServer()
        {
            var window = GetWindow<McpEditorWindow>("MCP Unity");
            window.StartServer();
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        private void OnEnable()
        {
            _logFilterLevel = McpSettings.Instance.MinimumLogLevel;
            McpServerLogger.Instance.OnLogAdded += OnLogAdded;
            McpRequestMonitor.OnRequestRecorded += OnRequestRecorded;
            EditorApplication.update += PeriodicRepaint;

            _chatPanel = new McpChatPanel();
            _chatPanel.Initialize(this);

            // When the chat panel's gear button is clicked, switch to Settings tab
            _chatPanel.OnSettingsRequested += OnChatSettingsRequested;

            // Load provider settings for the Settings tab
            _settingsProviderIndex = ProviderRegistry.IndexOf(ProviderRegistry.ActiveProviderId);
            if (_settingsProviderIndex < 0) _settingsProviderIndex = 0;
            _settingsAuthMode = (int)McpChatApiClient.ActiveAuthMode;
            _settingsOAuthTokenInput = McpChatApiClient.OAuthToken;
            _settingsCustomSystemPrompt = EditorPrefs.GetString(SystemPromptPref, "");
            LoadProviderSettings();
        }

        private void OnDisable()
        {
            McpServerLogger.Instance.OnLogAdded -= OnLogAdded;
            McpRequestMonitor.OnRequestRecorded -= OnRequestRecorded;
            EditorApplication.update -= PeriodicRepaint;

            if (_chatPanel != null)
            {
                _chatPanel.OnSettingsRequested -= OnChatSettingsRequested;
                _chatPanel.Dispose();
                _chatPanel = null;
            }
        }

        private void PeriodicRepaint()
        {
            if (McpUnityServer.IsRunning && EditorApplication.timeSinceStartup - _lastRepaintTime >= 1.0)
            {
                _lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnLogAdded(LogEntry entry)
        {
            Repaint();
        }

        private void OnRequestRecorded(RequestEntry entry)
        {
            Repaint();
        }

        private void OnChatSettingsRequested()
        {
            _selectedTab = 1;
            Repaint();
        }

        // ====================================================================
        // Provider Settings Loader
        // ====================================================================

        private void LoadProviderSettings()
        {
            var ids = ProviderRegistry.GetPresetIds();
            if (_settingsProviderIndex < 0 || _settingsProviderIndex >= ids.Length)
                _settingsProviderIndex = 0;

            string providerId = ids[_settingsProviderIndex];
            var provider = ProviderRegistry.GetPreset(providerId);
            if (provider == null) return;

            string model = ProviderRegistry.GetModel(providerId);
            _settingsModelIndex = Array.IndexOf(provider.modelIds, model);
            if (_settingsModelIndex < 0) _settingsModelIndex = 0;
            _settingsMaxTokens = ProviderRegistry.GetMaxTokens(providerId);
            _settingsTemperature = ProviderRegistry.GetTemperature(providerId);
            _settingsApiKeyInput = ProviderRegistry.GetApiKey(providerId);
        }

        // ====================================================================
        // Styles
        // ====================================================================

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };

            _statusLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _logAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = true,
                wordWrap = true,
                fontSize = 11,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11)
            };

            _boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            _foldoutStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 4, 4)
            };

            _linkStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.4f, 0.6f, 1f) }
            };

            _stylesInitialized = true;
        }

        // ====================================================================
        // Main OnGUI
        // ====================================================================

        private void OnGUI()
        {
            InitStyles();

            DrawHeader();
            EditorGUILayout.Space(5);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0:
                    DrawChatTab();
                    break;
                case 1:
                    _settingsScrollPosition = EditorGUILayout.BeginScrollView(_settingsScrollPosition);
                    DrawSettingsTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 2:
                    _diagnosticsScrollPosition = EditorGUILayout.BeginScrollView(_diagnosticsScrollPosition);
                    DrawDiagnosticsTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 3:
                    _setupScrollPosition = EditorGUILayout.BeginScrollView(_setupScrollPosition);
                    DrawSetupTab();
                    EditorGUILayout.EndScrollView();
                    break;
            }
        }

        // ====================================================================
        // Header
        // ====================================================================

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("MCP Unity", _headerStyle);
            GUILayout.FlexibleSpace();
            DrawStatusIndicator();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawStatusIndicator()
        {
            EditorGUILayout.BeginHorizontal();

            bool isRunning = McpUnityServer.IsRunning;
            var statusColor = isRunning ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            var statusText = isRunning ? "RUNNING" : "STOPPED";

            var originalColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label("\u25CF", GUILayout.Width(15));
            GUI.color = originalColor;

            EditorGUILayout.LabelField(statusText, _statusLabelStyle, GUILayout.Width(70));

            EditorGUILayout.EndHorizontal();
        }

        // ====================================================================
        // Tab 0: Chat
        // ====================================================================

        private void DrawChatTab()
        {
            _chatPanel?.Draw();
        }

        // ====================================================================
        // Tab 1: Settings (4 foldout sections)
        // ====================================================================

        private void DrawSettingsTab()
        {
            DrawSettingsProviderSection();
            EditorGUILayout.Space(4);
            DrawSettingsToolsSection();
            EditorGUILayout.Space(4);
            DrawSettingsServerSection();
            EditorGUILayout.Space(4);
            DrawSettingsAdvancedSection();
        }

        // ----------------------------------------------------------------
        // Section 1: Provider & Authentication
        // ----------------------------------------------------------------

        private void DrawSettingsProviderSection()
        {
            bool open = SessionState.GetBool(FoldoutProvider, true);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Provider & Authentication");
            if (newOpen != open) SessionState.SetBool(FoldoutProvider, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                // Provider popup
                var labels = ProviderRegistry.GetPresetLabels();
                var ids = ProviderRegistry.GetPresetIds();
                int newProvider = EditorGUILayout.Popup("Provider", _settingsProviderIndex, labels);
                if (newProvider != _settingsProviderIndex)
                {
                    _settingsProviderIndex = newProvider;
                    ProviderRegistry.ActiveProviderId = ids[newProvider];
                    _settingsCredentialVisible = false;
                    _settingsOAuthCodeInput = "";
                    _settingsOAuthStatus = "";
                    LoadProviderSettings();
                }

                string providerId = ids[_settingsProviderIndex];
                var preset = ProviderRegistry.GetPreset(providerId);

                EditorGUILayout.Space(6);

                // Auth status indicator
                var auth = McpChatApiClient.ResolveAuth();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Auth Status:", GUILayout.Width(80));
                var origColor = GUI.color;
                GUI.color = auth.IsValid ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(auth.IsValid ? "\u25CF Authenticated" : "\u25CF Not Authenticated");
                GUI.color = origColor;
                EditorGUILayout.EndHorizontal();

                if (auth.IsValid && !string.IsNullOrEmpty(auth.Source))
                {
                    EditorGUILayout.LabelField($"  Source: {auth.Source}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(6);

                // Provider-specific auth UI
                if (providerId == "anthropic")
                {
                    DrawAnthropicAuthSection();
                }
                else if (preset != null && preset.isLocal)
                {
                    EditorGUILayout.HelpBox("Local provider — no API key required.", MessageType.Info);
                }
                else
                {
                    DrawGenericApiKeySection(providerId, preset);
                }

                EditorGUILayout.Space(8);

                // Model popup
                if (preset != null && preset.modelLabels != null && preset.modelLabels.Length > 0)
                {
                    int newModel = EditorGUILayout.Popup("Model", _settingsModelIndex, preset.modelLabels);
                    if (newModel != _settingsModelIndex && newModel >= 0 && newModel < preset.modelIds.Length)
                    {
                        _settingsModelIndex = newModel;
                        ProviderRegistry.SetModel(providerId, preset.modelIds[newModel]);
                    }
                }

                // Custom model name (for custom/ollama/lmstudio)
                if (providerId == "custom" || providerId == "ollama" || providerId == "lmstudio")
                {
                    EditorGUILayout.Space(2);
                    string currentModel = ProviderRegistry.GetModel(providerId);
                    string newModelName = EditorGUILayout.TextField("Custom Model Name", currentModel);
                    if (newModelName != currentModel)
                    {
                        ProviderRegistry.SetModel(providerId, newModelName);
                    }
                }

                EditorGUILayout.Space(4);

                // Max tokens slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Max Tokens:", GUILayout.Width(80));
                int newTokens = EditorGUILayout.IntSlider(_settingsMaxTokens, 256, 64000);
                if (newTokens != _settingsMaxTokens)
                {
                    _settingsMaxTokens = newTokens;
                    ProviderRegistry.SetMaxTokens(providerId, newTokens);
                }
                EditorGUILayout.EndHorizontal();

                // Temperature slider
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Temperature:", GUILayout.Width(80));
                float maxTemp = providerId == "anthropic" ? 1f : 2f;
                float newTemp = EditorGUILayout.Slider(_settingsTemperature, 0f, maxTemp);
                if (Mathf.Abs(newTemp - _settingsTemperature) > 0.001f)
                {
                    _settingsTemperature = newTemp;
                    ProviderRegistry.SetTemperature(providerId, newTemp);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Custom endpoint
                string currentEndpoint = ProviderRegistry.GetCustomEndpoint(providerId);
                string hint = preset != null ? preset.defaultEndpoint : "";
                EditorGUILayout.LabelField("Custom Endpoint:", EditorStyles.miniLabel);
                string newEndpoint = EditorGUILayout.TextField(currentEndpoint);
                if (newEndpoint != currentEndpoint)
                {
                    ProviderRegistry.SetCustomEndpoint(providerId, newEndpoint);
                }
                if (!string.IsNullOrEmpty(hint))
                {
                    EditorGUILayout.LabelField($"  Default: {hint}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(6);

                // Custom system prompt
                EditorGUILayout.LabelField("Custom System Prompt:", EditorStyles.miniLabel);
                string newPrompt = EditorGUILayout.TextArea(_settingsCustomSystemPrompt, GUILayout.MinHeight(60));
                if (newPrompt != _settingsCustomSystemPrompt)
                {
                    _settingsCustomSystemPrompt = newPrompt;
                    EditorPrefs.SetString(SystemPromptPref, newPrompt);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAnthropicAuthSection()
        {
            // API Key vs OAuth toggle
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auth Method:", GUILayout.Width(90));
            int newAuthMode = GUILayout.Toolbar(_settingsAuthMode, AuthModeLabels);
            if (newAuthMode != _settingsAuthMode)
            {
                _settingsAuthMode = newAuthMode;
                McpChatApiClient.ActiveAuthMode = (AuthMode)newAuthMode;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (_settingsAuthMode == 0)
            {
                // API Key mode
                DrawGenericApiKeySection("anthropic", ProviderRegistry.GetPreset("anthropic"));
            }
            else
            {
                // OAuth mode
                DrawOAuthSection();
            }
        }

        private void DrawOAuthSection()
        {
            bool hasValid = McpChatOAuth.HasValidToken;

            if (hasValid)
            {
                EditorGUILayout.BeginHorizontal();
                var origC = GUI.color;
                GUI.color = new Color(0.3f, 0.9f, 0.3f);
                EditorGUILayout.LabelField("\u25CF OAuth Authenticated", EditorStyles.boldLabel);
                GUI.color = origC;
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Logout", GUILayout.Width(80)))
                {
                    McpChatOAuth.Logout();
                    _settingsOAuthStatus = "Logged out.";
                }
            }
            else if (McpChatOAuth.IsExchanging)
            {
                EditorGUILayout.HelpBox("Exchanging authorization code...", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Login with your Claude Pro/Max/Team subscription.\n" +
                    "1. Click 'Login with Claude' — your browser opens.\n" +
                    "2. Authorize the app on claude.ai.\n" +
                    "3. You land on console.anthropic.com — copy the full code shown (it looks like: abc123...#xyz...).\n" +
                    "4. Paste it below and click 'Exchange Code'.",
                    MessageType.Info);

                // Login buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Login with Claude (Max)", GUILayout.Height(26)))
                {
                    McpChatOAuth.StartLogin("max");
                    _settingsOAuthStatus = "Browser opened. Authorize, then copy the code from console.anthropic.com.";
                }
                if (GUILayout.Button("Console", GUILayout.Height(26), GUILayout.Width(70)))
                {
                    McpChatOAuth.StartLogin("console");
                    _settingsOAuthStatus = "Browser opened (console mode).";
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Code paste
                EditorGUILayout.LabelField("Paste authorization code:", EditorStyles.miniLabel);
                _settingsOAuthCodeInput = EditorGUILayout.TextField(_settingsOAuthCodeInput);

                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_settingsOAuthCodeInput));
                if (GUILayout.Button("Exchange Code"))
                {
                    McpChatOAuth.ExchangeCode(
                        _settingsOAuthCodeInput,
                        _ => { _settingsOAuthStatus = "Login successful!"; _settingsOAuthCodeInput = ""; Repaint(); },
                        err => { _settingsOAuthStatus = $"Error: {err}"; Repaint(); }
                    );
                }
                EditorGUI.EndDisabledGroup();

                // Manual bearer token (last resort)
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Or paste bearer token manually:", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                _settingsOAuthTokenInput = EditorGUILayout.TextField(_settingsOAuthTokenInput);
                if (GUILayout.Button("Set", GUILayout.Width(40)))
                {
                    McpChatOAuth.SetManualToken(_settingsOAuthTokenInput);
                    _settingsOAuthStatus = "Bearer token set (24h).";
                    _settingsOAuthTokenInput = "";
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }

            // Status feedback
            if (!string.IsNullOrEmpty(_settingsOAuthStatus))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(_settingsOAuthStatus, EditorStyles.miniLabel);
            }
        }

        private void DrawGenericApiKeySection(string providerId, ProviderPreset preset)
        {
            // Load stored key once when provider changes — avoids reading EditorPrefs every frame
            if (_settingsApiKeyCachedProviderId != providerId)
            {
                _settingsApiKeyCachedProviderId = providerId;
                _settingsApiKeyInput = ProviderRegistry.GetApiKey(providerId);
                _settingsApiKeyDirty = false;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(60));

            EditorGUI.BeginChangeCheck();
            if (_settingsCredentialVisible)
                _settingsApiKeyInput = EditorGUILayout.TextField(_settingsApiKeyInput);
            else
                _settingsApiKeyInput = EditorGUILayout.PasswordField(_settingsApiKeyInput);
            if (EditorGUI.EndChangeCheck()) _settingsApiKeyDirty = true;

            if (GUILayout.Button(_settingsCredentialVisible ? "Hide" : "Show", GUILayout.Width(45)))
            {
                _settingsCredentialVisible = !_settingsCredentialVisible;
            }
            EditorGUILayout.EndHorizontal();

            // Save key only when the text field loses focus or user presses Enter
            // (not on every frame — EditorPrefs writes are disk I/O)
            if (_settingsApiKeyDirty && !EditorGUIUtility.editingTextField)
            {
                ProviderRegistry.SetApiKey(providerId, _settingsApiKeyInput);
                _settingsApiKeyDirty = false;
            }

            // Env var hint
            if (preset != null && !string.IsNullOrEmpty(preset.apiKeyEnvVar))
            {
                string envValue = Environment.GetEnvironmentVariable(preset.apiKeyEnvVar);
                if (!string.IsNullOrEmpty(envValue))
                {
                    EditorGUILayout.LabelField($"  Env {preset.apiKeyEnvVar} detected", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField($"  Or set env: {preset.apiKeyEnvVar}", EditorStyles.miniLabel);
                }
            }

            // "Get Key" link
            if (preset != null && !string.IsNullOrEmpty(preset.apiKeyUrl))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Get API Key", _linkStyle, GUILayout.Width(80)))
                {
                    Application.OpenURL(preset.apiKeyUrl);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ----------------------------------------------------------------
        // Section 2: Tools
        // ----------------------------------------------------------------

        private void DrawSettingsToolsSection()
        {
            bool open = SessionState.GetBool(FoldoutTools, true);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Tools");
            if (newOpen != open) SessionState.SetBool(FoldoutTools, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                // Preset buttons + active count
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(40)))
                    ToolCategoryManager.EnableAll();
                if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(44)))
                    ToolCategoryManager.DisableAll();
                if (GUILayout.Button("Core", EditorStyles.miniButton, GUILayout.Width(44)))
                    ToolCategoryManager.SetPreset("core");

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"Active: {ToolCategoryManager.EnabledToolCount}/{ToolCategoryManager.TotalToolCount}",
                    EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                // 2-column grid of categories
                var categories = ToolCategoryManager.AllCategories;
                float colWidth = position.width / 2f - 20f;

                for (int i = 0; i < categories.Length; i += 2)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Left column
                    DrawToolCategoryToggle(ref categories[i], colWidth);

                    // Right column (if exists)
                    if (i + 1 < categories.Length)
                    {
                        DrawToolCategoryToggle(ref categories[i + 1], colWidth);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawToolCategoryToggle(ref ToolCategoryManager.ToolCategory cat, float width)
        {
            bool isEnabled = ToolCategoryManager.IsCategoryEnabled(cat.id);
            bool newEnabled = EditorGUILayout.ToggleLeft(
                $"{cat.displayName} ({cat.Count})",
                isEnabled,
                GUILayout.Width(width));
            if (newEnabled != isEnabled)
            {
                ToolCategoryManager.SetCategoryEnabled(cat.id, newEnabled);
            }
        }

        // ----------------------------------------------------------------
        // Section 3: Server
        // ----------------------------------------------------------------

        private void DrawSettingsServerSection()
        {
            bool open = SessionState.GetBool(FoldoutServer, true);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Server");
            if (newOpen != open) SessionState.SetBool(FoldoutServer, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                bool isRunning = McpUnityServer.IsRunning;
                int connectedClients = McpUnityServer.ConnectedClientCount;
                var settings = McpSettings.Instance;

                // Compact status line
                EditorGUILayout.BeginHorizontal();
                var origColor = GUI.color;
                GUI.color = isRunning ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(
                    isRunning ? "\u25CF Running" : "\u25CF Stopped",
                    EditorStyles.boldLabel, GUILayout.Width(80));
                GUI.color = origColor;

                if (isRunning)
                {
                    var uptime = McpServerStatus.Uptime;
                    EditorGUILayout.LabelField(
                        $"Uptime: {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}",
                        EditorStyles.miniLabel, GUILayout.Width(110));

                    EditorGUILayout.LabelField(
                        $"Clients: {connectedClients}",
                        EditorStyles.miniLabel, GUILayout.Width(70));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Port field (disabled when running)
                EditorGUI.BeginDisabledGroup(isRunning);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Port:", GUILayout.Width(40));
                int newPort = EditorGUILayout.IntField(settings.Port);
                if (newPort != settings.Port && newPort >= 1 && newPort <= 65535)
                {
                    settings.Port = newPort;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(4);

                // Start/Stop/Restart buttons
                EditorGUILayout.BeginHorizontal();
                if (isRunning)
                {
                    GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                    if (GUILayout.Button("Stop", GUILayout.Height(28)))
                    {
                        StopServer();
                    }
                    GUI.backgroundColor = Color.white;

                    GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                    if (GUILayout.Button("Restart", GUILayout.Height(28)))
                    {
                        RestartServer();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                    if (GUILayout.Button("Start Server", GUILayout.Height(28)))
                    {
                        StartServer();
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                // Auto-start toggle
                EditorGUILayout.Space(2);
                bool newAutoStart = EditorGUILayout.Toggle("Auto-start on Editor load", settings.AutoStartServer);
                if (newAutoStart != settings.AutoStartServer)
                {
                    settings.AutoStartServer = newAutoStart;
                }

                // Health indicator
                if (isRunning)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Health:", GUILayout.Width(50));

                    origColor = GUI.color;
                    if (connectedClients > 0)
                    {
                        GUI.color = new Color(0.3f, 0.9f, 0.3f);
                        EditorGUILayout.LabelField($"Bridge connected ({connectedClients} client{(connectedClients > 1 ? "s" : "")})");
                    }
                    else
                    {
                        GUI.color = new Color(1f, 0.7f, 0.2f);
                        EditorGUILayout.LabelField("Waiting for bridge connection...");
                    }
                    GUI.color = origColor;

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ----------------------------------------------------------------
        // Section 4: Advanced
        // ----------------------------------------------------------------

        private void DrawSettingsAdvancedSection()
        {
            bool open = SessionState.GetBool(FoldoutAdvanced, false);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Advanced");
            if (newOpen != open) SessionState.SetBool(FoldoutAdvanced, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                var s = McpSettings.Instance;

                // General
                EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
                DrawToggleSetting("Show Notifications:", s.ShowNotifications, v => s.ShowNotifications = v);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Request Timeout (ms):", GUILayout.Width(150));
                int newTimeout = EditorGUILayout.IntField(s.RequestTimeoutMs);
                if (newTimeout != s.RequestTimeoutMs) s.RequestTimeoutMs = newTimeout;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // Logging
                EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
                DrawToggleSetting("Log to Unity Console:", s.LogToConsole, v => s.LogToConsole = v);

                if (!s.LogToConsole)
                {
                    EditorGUILayout.HelpBox("MCP logs are hidden from Unity Console. View them in the Diagnostics > Logs section.", MessageType.Info);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Minimum Log Level:", GUILayout.Width(150));
                var newLevel = (LogLevel)EditorGUILayout.EnumPopup(s.MinimumLogLevel);
                if (newLevel != s.MinimumLogLevel) s.MinimumLogLevel = newLevel;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Max Log Entries:", GUILayout.Width(150));
                int newMax = EditorGUILayout.IntField(s.MaxLogEntries);
                if (newMax != s.MaxLogEntries) s.MaxLogEntries = newMax;
                EditorGUILayout.EndHorizontal();

                DrawToggleSetting("Log to File:", s.LogToFile, v => s.LogToFile = v);

                EditorGUILayout.Space(8);

                // Custom Server Path
                EditorGUILayout.LabelField("Server Path", EditorStyles.boldLabel);
                DrawToggleSetting("Use Custom Server Path:", s.UseCustomServerPath, v => s.UseCustomServerPath = v);

                if (s.UseCustomServerPath)
                {
                    EditorGUILayout.BeginHorizontal();
                    string newPath = EditorGUILayout.TextField(s.CustomServerPath);
                    if (newPath != s.CustomServerPath) s.CustomServerPath = newPath;
                    if (GUILayout.Button("Browse", GUILayout.Width(60)))
                    {
                        string path = EditorUtility.OpenFilePanel("Select Server Script", "", "js");
                        if (!string.IsNullOrEmpty(path))
                        {
                            s.CustomServerPath = path;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(12);

                // Reset to Defaults
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("Settings are saved automatically.", MessageType.None);

                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Reset to Defaults", GUILayout.Height(28), GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog("Reset Settings",
                        "Are you sure you want to reset all settings to defaults?", "Reset", "Cancel"))
                    {
                        McpSettings.Instance.ResetToDefaults();
                        _logFilterLevel = McpSettings.Instance.MinimumLogLevel;
                        McpServerLogger.Instance.Info("Settings reset to defaults");
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ====================================================================
        // Tab 2: Diagnostics (3 foldout sections)
        // ====================================================================

        private void DrawDiagnosticsTab()
        {
            DrawDiagnosticsMonitorSection();
            EditorGUILayout.Space(4);
            DrawDiagnosticsLogsSection();
            EditorGUILayout.Space(4);
            DrawDiagnosticsClaudeConfigSection();
        }

        // ----------------------------------------------------------------
        // Diagnostics Section 1: Request Monitor
        // ----------------------------------------------------------------

        private void DrawDiagnosticsMonitorSection()
        {
            bool open = SessionState.GetBool(FoldoutMonitor, true);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Request Monitor");
            if (newOpen != open) SessionState.SetBool(FoldoutMonitor, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                // Stats bar
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Total:", GUILayout.Width(38));
                EditorGUILayout.LabelField(McpRequestMonitor.TotalRequests.ToString(), GUILayout.Width(40));
                EditorGUILayout.LabelField("Errors:", GUILayout.Width(44));
                var origColor = GUI.color;
                if (McpRequestMonitor.TotalErrors > 0) GUI.color = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(McpRequestMonitor.TotalErrors.ToString(), GUILayout.Width(30));
                GUI.color = origColor;
                GUILayout.FlexibleSpace();
                _monitorAutoScroll = GUILayout.Toggle(_monitorAutoScroll, "Auto-scroll", GUILayout.Width(90));
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    McpRequestMonitor.Clear();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // Column headers
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                EditorGUILayout.LabelField("Time", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.LabelField("Tool / Method", EditorStyles.miniLabel, GUILayout.MinWidth(150));
                EditorGUILayout.LabelField("Duration", EditorStyles.miniLabel, GUILayout.Width(65));
                EditorGUILayout.LabelField("Status", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

                // Request list
                _monitorScrollPosition = EditorGUILayout.BeginScrollView(_monitorScrollPosition, GUILayout.MinHeight(120), GUILayout.MaxHeight(300));

                var entries = McpRequestMonitor.Entries;
                if (entries.Count == 0)
                {
                    EditorGUILayout.LabelField("No requests recorded yet.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    for (int i = entries.Count - 1; i >= 0; i--)
                    {
                        DrawRequestEntry(entries[i]);
                    }
                }

                EditorGUILayout.EndScrollView();

                if (_monitorAutoScroll && entries.Count > 0)
                {
                    _monitorScrollPosition = Vector2.zero;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ----------------------------------------------------------------
        // Diagnostics Section 2: Logs
        // ----------------------------------------------------------------

        private void DrawDiagnosticsLogsSection()
        {
            bool open = SessionState.GetBool(FoldoutLogs, false);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Logs");
            if (newOpen != open) SessionState.SetBool(FoldoutLogs, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                // Controls
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entries: {McpServerLogger.Instance.Count}", GUILayout.Width(80));
                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Min Level:", GUILayout.Width(60));
                _logFilterLevel = (LogLevel)EditorGUILayout.EnumPopup(_logFilterLevel, GUILayout.Width(80));

                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    McpServerLogger.Instance.Clear();
                    _cachedLogs = "";
                }
                if (GUILayout.Button("Export", GUILayout.Width(50)))
                {
                    ExportLogs();
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(55)))
                {
                    RefreshLogs();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                // Log view
                _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.MinHeight(120), GUILayout.MaxHeight(400));

                if (_useColoredLogs)
                {
                    DrawColoredLogs();
                }
                else
                {
                    RefreshLogsIfNeeded();
                    EditorGUILayout.TextArea(_cachedLogs, _logAreaStyle, GUILayout.ExpandHeight(true));
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ----------------------------------------------------------------
        // Diagnostics Section 3: Claude Config
        // ----------------------------------------------------------------

        private void DrawDiagnosticsClaudeConfigSection()
        {
            bool open = SessionState.GetBool(FoldoutClaudeConfig, false);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "Claude Config");
            if (newOpen != open) SessionState.SetBool(FoldoutClaudeConfig, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                // --- Claude Code CLI (local .mcp.json) ---
                EditorGUILayout.LabelField("Claude Code CLI (Recommended)", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);

                EditorGUILayout.HelpBox(
                    "Claude Code CLI uses a local .mcp.json file in your project.",
                    MessageType.Info);

                EditorGUILayout.Space(4);

                // .mcp.json status
                string localMcpPath = Path.Combine(Application.dataPath, ".mcp.json");
                bool localExists = File.Exists(localMcpPath);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Config File:", GUILayout.Width(80));
                var origColor = GUI.color;
                GUI.color = localExists ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                EditorGUILayout.LabelField(localExists ? "OK" : "MISSING", GUILayout.Width(50));
                GUI.color = origColor;
                EditorGUILayout.SelectableLabel("Assets/.mcp.json", GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                // Server script status
                string serverPath = McpSettings.Instance.EffectiveServerPath;
                bool serverExists = File.Exists(serverPath);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Server Script:", GUILayout.Width(80));
                origColor = GUI.color;
                GUI.color = serverExists ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                EditorGUILayout.LabelField(serverExists ? "OK" : "MISSING", GUILayout.Width(50));
                GUI.color = origColor;
                EditorGUILayout.SelectableLabel(serverPath, GUILayout.Height(18));
                EditorGUILayout.EndHorizontal();

                if (!serverExists)
                {
                    EditorGUILayout.HelpBox("Server script not found. Run 'npm run build' in Server~/.", MessageType.Error);
                }

                EditorGUILayout.Space(4);

                // Show local config content or create button
                if (localExists)
                {
                    try
                    {
                        string localContent = File.ReadAllText(localMcpPath);
                        EditorGUILayout.LabelField("Current .mcp.json:", EditorStyles.miniLabel);
                        EditorGUILayout.TextArea(localContent, _logAreaStyle, GUILayout.Height(80));
                    }
                    catch
                    {
                        EditorGUILayout.HelpBox("Could not read .mcp.json", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.Space(3);
                    if (GUILayout.Button("Create .mcp.json for Claude Code", GUILayout.Height(26)))
                    {
                        CreateLocalMcpJson(localMcpPath, serverPath);
                    }
                }

                EditorGUILayout.Space(10);

                // --- Claude Desktop ---
                EditorGUILayout.LabelField("Claude Desktop (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.Space(3);

                EditorGUILayout.HelpBox(
                    "For Claude Desktop app. Only needed if you use Claude Desktop instead of Claude Code CLI.",
                    MessageType.None);

                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
                if (GUILayout.Button("Auto Configure Claude Desktop", GUILayout.Height(26)))
                {
                    ConfigureClaudeDesktop();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Copy Config", GUILayout.Height(26)))
                {
                    CopyConfigToClipboard();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Config Folder", GUILayout.Height(22)))
                {
                    OpenConfigFolder();
                }
                if (GUILayout.Button("View Current Config", GUILayout.Height(22)))
                {
                    ViewCurrentConfig();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ====================================================================
        // Shared Helper Methods
        // ====================================================================

        private void DrawRequestEntry(RequestEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            // Time
            EditorGUILayout.LabelField(entry.Timestamp.ToString("HH:mm:ss"), EditorStyles.miniLabel, GUILayout.Width(60));

            // Tool/Method name
            string displayName = entry.DisplayName;
            if (displayName != null && displayName.StartsWith("unity_"))
                displayName = displayName.Substring(6);

            var origColor = GUI.contentColor;
            if (!entry.Success) GUI.contentColor = new Color(1f, 0.5f, 0.5f);
            EditorGUILayout.LabelField(displayName ?? "?", EditorStyles.miniLabel, GUILayout.MinWidth(150));
            GUI.contentColor = origColor;

            // Duration
            string durationStr = entry.DurationMs < 1
                ? "<1ms"
                : entry.DurationMs < 1000
                    ? $"{entry.DurationMs:F0}ms"
                    : $"{entry.DurationMs / 1000:F1}s";

            origColor = GUI.contentColor;
            if (entry.DurationMs > 1000) GUI.contentColor = new Color(1f, 0.7f, 0.2f);
            EditorGUILayout.LabelField(durationStr, EditorStyles.miniLabel, GUILayout.Width(65));
            GUI.contentColor = origColor;

            // Status
            origColor = GUI.contentColor;
            GUI.contentColor = entry.Success ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(entry.Success ? "OK" : "ERR", EditorStyles.miniLabel, GUILayout.Width(50));
            GUI.contentColor = origColor;

            EditorGUILayout.EndHorizontal();

            // Show error on hover via tooltip
            if (!entry.Success && !string.IsNullOrEmpty(entry.Error))
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                GUI.Label(lastRect, new GUIContent("", entry.Error));
            }
        }

        private static void DrawToggleSetting(string label, bool currentValue, Action<bool> setter)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            bool newValue = EditorGUILayout.Toggle(currentValue);
            if (newValue != currentValue) setter(newValue);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColoredLogs()
        {
            var logs = McpServerLogger.Instance.Logs;
            bool hasEntries = false;

            for (int i = 0; i < logs.Count; i++)
            {
                var entry = logs[i];
                if (entry.Level < _logFilterLevel) continue;

                hasEntries = true;
                var originalColor = GUI.contentColor;
                GUI.contentColor = entry.GetColor();
                EditorGUILayout.LabelField(entry.ToShortString(), _logAreaStyle, GUILayout.Height(16));
                GUI.contentColor = originalColor;
            }

            if (!hasEntries)
            {
                EditorGUILayout.LabelField("No log entries.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void RefreshLogsIfNeeded()
        {
            int currentCount = McpServerLogger.Instance.Count;
            if (currentCount != _lastLogCount)
            {
                RefreshLogs();
            }
        }

        private void RefreshLogs()
        {
            _cachedLogs = McpServerLogger.Instance.GetFormattedLogs(_logFilterLevel);
            _lastLogCount = McpServerLogger.Instance.Count;
        }

        // ====================================================================
        // Server Control
        // ====================================================================

        private void StartServer()
        {
            McpUnityServer.Start();

            McpServerLogger.Instance.Info($"Server started on port {McpUnityServer.Port}");

            if (McpSettings.Instance.ShowNotifications)
            {
                ShowNotification(new GUIContent("MCP Server Started"));
            }

            Repaint();
        }

        private void StopServer()
        {
            McpUnityServer.Stop();

            McpServerLogger.Instance.Info("Server stopped");

            if (McpSettings.Instance.ShowNotifications)
            {
                ShowNotification(new GUIContent("MCP Server Stopped"));
            }

            Repaint();
        }

        private void RestartServer()
        {
            StopServer();
            EditorApplication.delayCall += StartServer;
        }

        // ====================================================================
        // Claude Desktop Config Helpers
        // ====================================================================

        private void ConfigureClaudeDesktop()
        {
            try
            {
                string configPath = McpSettings.GetClaudeConfigPath();
                string directory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string newConfig = McpSettings.Instance.GenerateClaudeConfig();

                if (File.Exists(configPath))
                {
                    try
                    {
                        McpServerLogger.Instance.Warning("Existing Claude config will be backed up and replaced");
                        string backupPath = configPath + ".backup";
                        File.Copy(configPath, backupPath, true);
                    }
                    catch (Exception ex)
                    {
                        McpServerLogger.Instance.Error("Failed to backup existing config", ex);
                    }
                }

                File.WriteAllText(configPath, newConfig);

                McpServerLogger.Instance.Info($"Claude Desktop configured: {configPath}");

                EditorUtility.DisplayDialog("Success",
                    "Claude Desktop configuration updated!\n\n" +
                    "Please restart Claude Desktop for changes to take effect.",
                    "OK");
            }
            catch (Exception ex)
            {
                McpServerLogger.Instance.Error("Failed to configure Claude Desktop", ex);
                EditorUtility.DisplayDialog("Error",
                    $"Failed to configure Claude Desktop:\n{ex.Message}",
                    "OK");
            }
        }

        private void CopyConfigToClipboard()
        {
            string config = McpSettings.Instance.GenerateClaudeConfig();
            EditorGUIUtility.systemCopyBuffer = config;

            McpServerLogger.Instance.Info("Configuration copied to clipboard");

            if (McpSettings.Instance.ShowNotifications)
            {
                ShowNotification(new GUIContent("Config Copied!"));
            }
        }

        private void OpenConfigFolder()
        {
            string configPath = McpSettings.GetClaudeConfigPath();
            string directory = Path.GetDirectoryName(configPath);

            if (Directory.Exists(directory))
            {
                EditorUtility.RevealInFinder(configPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Folder Not Found",
                    $"The Claude configuration folder does not exist yet:\n{directory}",
                    "OK");
            }
        }

        private void ViewCurrentConfig()
        {
            string configPath = McpSettings.GetClaudeConfigPath();

            if (File.Exists(configPath))
            {
                string content = File.ReadAllText(configPath);
                EditorUtility.DisplayDialog("Current Claude Config", content, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Config Found",
                    $"Claude Desktop configuration file not found at:\n{configPath}",
                    "OK");
            }
        }

        // ====================================================================
        // Setup Tab
        // ====================================================================

        private void DrawSetupTab()
        {
            EditorGUILayout.LabelField("AI Editor Setup", _headerStyle);
            EditorGUILayout.HelpBox(
                "Create local MCP config files so your AI editor can connect to Unity.\n" +
                "All files are written to the project root (next to your Assets/ folder).",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // --- Server Status ---
            DrawSetupServerStatus();

            EditorGUILayout.Space(10);

            // --- Quick Setup Buttons ---
            DrawSetupEditorSection();

            EditorGUILayout.Space(10);

            // --- Claude Desktop (global) ---
            DrawSetupClaudeDesktopSection();
        }

        private void DrawSetupServerStatus()
        {
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_boxStyle);

            string serverPath = McpSettings.Instance.EffectiveServerPath;
            bool serverBuilt = File.Exists(serverPath);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Node.js bridge:", GUILayout.Width(110));
            var c = GUI.color;
            GUI.color = serverBuilt ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField(serverBuilt ? "Built" : "Not built", GUILayout.Width(65));
            GUI.color = c;
            EditorGUILayout.SelectableLabel(serverPath, EditorStyles.miniLabel, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();

            if (!serverBuilt)
            {
                EditorGUILayout.HelpBox(
                    "The Node.js bridge is not built yet.\n" +
                    "Open a terminal in the Server~/ folder and run:\n\n" +
                    "    npm install && npm run build",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupEditorSection()
        {
            EditorGUILayout.LabelField("Local Config Files", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_boxStyle);

            string serverPath = McpSettings.Instance.EffectiveServerPath;

            // Build config rows: (label, icon, path)
            var editors = new[]
            {
                ("Claude Code CLI",  McpSettings.GetClaudeCodeConfigPath()),
                ("Cursor",           McpSettings.GetCursorConfigPath()),
                ("Windsurf",         McpSettings.GetWindsurfConfigPath()),
                ("VS Code / Copilot",McpSettings.GetVSCodeConfigPath()),
            };

            bool anyMissing = false;
            foreach (var (label, path) in editors)
            {
                if (!File.Exists(path)) anyMissing = true;
                DrawSetupEditorRow(label, path, serverPath);
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(6);

            if (anyMissing)
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
                if (GUILayout.Button("Setup All Missing", GUILayout.Height(28)))
                {
                    SetupAllEditors(editors, serverPath);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("All config files are present.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupEditorRow(string editorLabel, string configPath, string serverPath)
        {
            bool exists = File.Exists(configPath);
            string relativePath = configPath.Replace(McpSettings.GetProjectRootPath(), "").TrimStart('/', '\\');

            EditorGUILayout.BeginHorizontal();

            // Status dot
            var origColor = GUI.color;
            GUI.color = exists ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
            GUILayout.Label(exists ? "\u25CF" : "\u25CB", GUILayout.Width(16));
            GUI.color = origColor;

            // Label
            EditorGUILayout.LabelField(editorLabel, GUILayout.Width(130));

            // Relative path
            EditorGUILayout.LabelField(relativePath, EditorStyles.miniLabel);

            // Button
            if (exists)
            {
                if (GUILayout.Button("Recreate", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    WriteEditorConfig(configPath, serverPath, editorLabel);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    WriteEditorConfig(configPath, serverPath, editorLabel);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetupClaudeDesktopSection()
        {
            EditorGUILayout.LabelField("Claude Desktop (Global)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.HelpBox(
                "Claude Desktop uses a global config file, not a project-local one.\n" +
                "Only needed if you use the Claude Desktop app instead of Claude Code CLI.",
                MessageType.None);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUILayout.Button("Configure Claude Desktop", GUILayout.Height(26)))
            {
                ConfigureClaudeDesktop();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Copy Config JSON", GUILayout.Height(26)))
            {
                CopyConfigToClipboard();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void WriteEditorConfig(string configPath, string serverPath, string editorLabel)
        {
            string content = McpSettings.Instance.GenerateLocalMcpConfig();
            string error = McpSettings.WriteConfigFile(configPath, content);

            if (error == null)
            {
                McpServerLogger.Instance.Info($"Created {editorLabel} config: {configPath}");
                if (McpSettings.Instance.ShowNotifications)
                    ShowNotification(new GUIContent($"{editorLabel} config created"));
                // Refresh if inside Assets/
                if (configPath.StartsWith(Application.dataPath))
                    AssetDatabase.Refresh();
            }
            else
            {
                McpServerLogger.Instance.Error($"Failed to write {editorLabel} config", null);
                EditorUtility.DisplayDialog("Error",
                    $"Failed to write config for {editorLabel}:\n{error}", "OK");
            }
        }

        private void SetupAllEditors((string label, string path)[] editors, string serverPath)
        {
            int created = 0;
            var errors = new System.Text.StringBuilder();

            foreach (var (label, path) in editors)
            {
                if (!File.Exists(path))
                {
                    string content = McpSettings.Instance.GenerateLocalMcpConfig();
                    string err = McpSettings.WriteConfigFile(path, content);
                    if (err == null)
                    {
                        created++;
                        McpServerLogger.Instance.Info($"Created {label} config: {path}");
                    }
                    else
                    {
                        errors.AppendLine($"{label}: {err}");
                    }
                }
            }

            if (errors.Length == 0)
            {
                if (McpSettings.Instance.ShowNotifications)
                    ShowNotification(new GUIContent($"{created} config(s) created"));
            }
            else
            {
                EditorUtility.DisplayDialog("Some files failed",
                    $"{created} file(s) created.\n\nErrors:\n{errors}", "OK");
            }
        }

        private void CreateLocalMcpJson(string path, string serverPath)
        {
            try
            {
                string escapedServerPath = serverPath.Replace("\\", "/");
                string json = $@"{{
  ""mcpServers"": {{
    ""mcp-unity"": {{
      ""type"": ""stdio"",
      ""command"": ""node"",
      ""args"": [""{escapedServerPath}""],
      ""env"": {{}}
    }}
  }}
}}";
                File.WriteAllText(path, json);
                McpServerLogger.Instance.Info($"Created .mcp.json at {path}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                McpServerLogger.Instance.Error("Failed to create .mcp.json", ex);
                EditorUtility.DisplayDialog("Error", $"Failed to create .mcp.json:\n{ex.Message}", "OK");
            }
        }

        private void ExportLogs()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export MCP Unity Logs",
                "",
                $"mcp_unity_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string content = McpServerLogger.Instance.Export();
                    File.WriteAllText(path, content);

                    McpServerLogger.Instance.Info($"Logs exported to: {path}");
                    EditorUtility.RevealInFinder(path);
                }
                catch (Exception ex)
                {
                    McpServerLogger.Instance.Error("Failed to export logs", ex);
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Failed to export logs:\n{ex.Message}",
                        "OK");
                }
            }
        }
    }
}
