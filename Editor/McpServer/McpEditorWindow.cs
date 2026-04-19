using System;
using UnityEditor;
using UnityEngine;
using McpUnity.Chat;
using McpUnity.Chat.Providers;
using McpUnity.Server;

namespace McpUnity.Editor
{
    /// <summary>
    /// Main Editor Window for MCP Unity — consolidated 4-tab layout.
    /// Tab 0: Chat (embedded McpChatPanel)           → McpEditorWindow.ChatTab.cs
    /// Tab 1: Settings (Provider, Tools, Server, Advanced) → McpEditorWindow.SettingsTab.cs
    /// Tab 2: Diagnostics (Monitor, Logs, Claude Config)   → McpEditorWindow.DiagnosticsTab.cs
    /// Tab 3: Setup (Editor configs, Claude Desktop)       → McpEditorWindow.SetupTab.cs
    /// Accessible via Tools > MCP Unity > Server Window
    /// </summary>
    public partial class McpEditorWindow : EditorWindow
    {
        // ====================================================================
        // Tab Management
        // ====================================================================
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Chat", "Server", "Logs", "Setup" };

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

        [MenuItem("Tools/Conductor MCP")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpEditorWindow>("Conductor MCP");
            window.minSize = new Vector2(420, 500);
            window.Show();
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
                font = GetMonoFont()
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

        // SEC-#428: cache the dynamic Font once per Editor session — Font.CreateDynamicFontFromOSFont
        // creates a native object that Unity does not GC, so re-creating it on every InitStyles()
        // leaks native memory across window open/close cycles.
        private static Font _cachedMonoFont;
        private static Font GetMonoFont()
        {
            if (_cachedMonoFont == null)
                _cachedMonoFont = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            return _cachedMonoFont;
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
                    DrawServerTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 2:
                    _diagnosticsScrollPosition = EditorGUILayout.BeginScrollView(_diagnosticsScrollPosition);
                    DrawLogsTab();
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
            EditorGUILayout.LabelField("Conductor MCP", _headerStyle);
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
        // Shared Helper Methods
        // ====================================================================

        private static void DrawToggleSetting(string label, bool currentValue, Action<bool> setter)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            bool newValue = EditorGUILayout.Toggle(currentValue);
            if (newValue != currentValue) setter(newValue);
            EditorGUILayout.EndHorizontal();
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
    }
}
