using System;
using UnityEditor;
using UnityEngine;
using McpUnity.Chat;
using McpUnity.Chat.Providers;
using McpUnity.Server;

namespace McpUnity.Editor
{
    public partial class McpEditorWindow
    {
        // ====================================================================
        // Tab 1: Server (merged Settings + Diagnostics)
        // ====================================================================

        private void DrawServerTab()
        {
            DrawSettingsServerSection();
            EditorGUILayout.Space(4);
            DrawSettingsServerToolsSection();
            EditorGUILayout.Space(4);
            DrawSettingsProviderSection();
            EditorGUILayout.Space(4);
            DrawSettingsAdvancedSection();
        }

        // ----------------------------------------------------------------
        // Section: MCP Server Tools (controls what AI clients see)
        // ----------------------------------------------------------------

        private const string FoldoutServerTools = "McpUnity_Foldout_ServerTools";

        private void DrawSettingsServerToolsSection()
        {
            bool open = SessionState.GetBool(FoldoutServerTools, true);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, "MCP Tools (visible to AI clients)");
            if (newOpen != open) SessionState.SetBool(FoldoutServerTools, newOpen);

            if (newOpen)
            {
                EditorGUILayout.BeginVertical(_boxStyle);

                var registry = McpUnityServer.ToolRegistry;
                if (registry == null)
                {
                    EditorGUILayout.HelpBox(
                        "Server not initialized. Start the server (above) to manage tool categories.",
                        MessageType.Info);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    return;
                }

                var categories = registry.GetCategoryInfo();

                int total = 0;
                int visible = 0;
                foreach (var c in categories)
                {
                    total += c.toolCount;
                    if (McpServerCategorySettings.IsCategoryEnabled(c.name))
                        visible += c.toolCount;
                }

                // Top row: presets + count
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(40)))
                    McpServerCategorySettings.SetAll(true);
                if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(48)))
                    McpServerCategorySettings.SetAll(false);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"Visible: {visible}/{total}",
                    EditorStyles.miniLabel, GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "Toggling a category updates tools/list. Connected AI clients are notified automatically.",
                    MessageType.None);

                EditorGUILayout.Space(4);

                // 2-column grid of categories
                float colWidth = position.width / 2f - 24f;
                for (int i = 0; i < categories.Count; i += 2)
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawServerCategoryToggle(categories[i], colWidth);
                    if (i + 1 < categories.Count)
                        DrawServerCategoryToggle(categories[i + 1], colWidth);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawServerCategoryToggle(CategoryInfo cat, float width)
        {
            bool alwaysOn = McpServerCategorySettings.IsAlwaysOn(cat.name);
            bool enabled  = McpServerCategorySettings.IsCategoryEnabled(cat.name);

            using (new EditorGUI.DisabledScope(alwaysOn))
            {
                string label = alwaysOn
                    ? $"{cat.name} ({cat.toolCount}) — always on"
                    : $"{cat.name} ({cat.toolCount})";

                bool newEnabled = EditorGUILayout.ToggleLeft(label, enabled, GUILayout.Width(width));
                if (!alwaysOn && newEnabled != enabled)
                    McpServerCategorySettings.SetCategoryEnabled(cat.name, newEnabled);
            }
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

                // --- Provider + Auth Status on same line ---
                var labels = ProviderRegistry.GetPresetLabels();
                var ids = ProviderRegistry.GetPresetIds();
                if (ids == null || ids.Length == 0)
                {
                    EditorGUILayout.HelpBox("No providers registered.", MessageType.Warning);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    return;
                }
                // Defensive clamp: the persisted index may point past the current preset list
                // (e.g. a preset was removed between sessions). Without clamping, the reads below
                // throw IndexOutOfRangeException on every repaint.
                if (_settingsProviderIndex < 0 || _settingsProviderIndex >= ids.Length)
                    _settingsProviderIndex = 0;
                var auth = McpChatApiClient.ResolveAuth();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Provider:", GUILayout.Width(60));
                int newProvider = EditorGUILayout.Popup(_settingsProviderIndex, labels);
                if (newProvider != _settingsProviderIndex && newProvider >= 0 && newProvider < ids.Length)
                {
                    _settingsProviderIndex = newProvider;
                    ProviderRegistry.ActiveProviderId = ids[newProvider];
                    _settingsCredentialVisible = false;
                    _settingsOAuthCodeInput = "";
                    _settingsOAuthStatus = "";
                    LoadProviderSettings();
                }
                var origColor = GUI.color;
                GUI.color = auth.IsValid ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField(auth.IsValid ? "\u25CF" : "\u25CB", GUILayout.Width(16));
                GUI.color = origColor;
                EditorGUILayout.EndHorizontal();

                string providerId = ids[_settingsProviderIndex];
                var preset = ProviderRegistry.GetPreset(providerId);

                EditorGUILayout.Space(4);

                // --- Auth section (compact) ---
                if (providerId == "anthropic")
                {
                    DrawAnthropicAuthSection();
                }
                else if (preset != null && preset.isLocal)
                {
                    EditorGUILayout.LabelField("  No API key required (local provider)", EditorStyles.miniLabel);
                }
                else
                {
                    DrawGenericApiKeySection(providerId, preset);
                }

                EditorGUILayout.Space(6);

                // --- Model ---
                // Clamp the popup to the intersection of modelLabels and modelIds so we never
                // display a label the user can't actually select, and guard against a null
                // modelIds array when modelLabels is non-null.
                if (preset != null && preset.modelLabels != null && preset.modelLabels.Length > 0)
                {
                    int modelCount = Math.Min(preset.modelLabels.Length, preset.modelIds?.Length ?? 0);
                    if (modelCount > 0)
                    {
                        string[] modelLabels;
                        if (modelCount == preset.modelLabels.Length)
                        {
                            modelLabels = preset.modelLabels;
                        }
                        else
                        {
                            modelLabels = new string[modelCount];
                            Array.Copy(preset.modelLabels, modelLabels, modelCount);
                        }
                        int clampedIndex = Mathf.Clamp(_settingsModelIndex, 0, modelCount - 1);
                        int newModel = EditorGUILayout.Popup("Model", clampedIndex, modelLabels);
                        if (newModel != _settingsModelIndex && newModel >= 0 && newModel < modelCount)
                        {
                            _settingsModelIndex = newModel;
                            ProviderRegistry.SetModel(providerId, preset.modelIds[newModel]);
                        }
                    }
                }

                if (providerId == "custom" || providerId == "ollama" || providerId == "lmstudio")
                {
                    string currentModel = ProviderRegistry.GetModel(providerId);
                    string newModelName = EditorGUILayout.TextField("Custom Model", currentModel);
                    if (newModelName != currentModel)
                        ProviderRegistry.SetModel(providerId, newModelName);
                }

                EditorGUILayout.Space(2);

                // --- Max Tokens + Temperature ---
                int newTokens = EditorGUILayout.IntSlider("Max Tokens", _settingsMaxTokens, 256, 64000);
                if (newTokens != _settingsMaxTokens)
                {
                    _settingsMaxTokens = newTokens;
                    ProviderRegistry.SetMaxTokens(providerId, newTokens);
                }

                float maxTemp = providerId == "anthropic" ? 1f : 2f;
                float newTemp = EditorGUILayout.Slider("Temperature", _settingsTemperature, 0f, maxTemp);
                if (Mathf.Abs(newTemp - _settingsTemperature) > 0.001f)
                {
                    _settingsTemperature = newTemp;
                    ProviderRegistry.SetTemperature(providerId, newTemp);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAnthropicAuthSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auth:", GUILayout.Width(38));
            int newAuthMode = GUILayout.Toolbar(_settingsAuthMode, AuthModeLabels);
            if (newAuthMode != _settingsAuthMode)
            {
                _settingsAuthMode = newAuthMode;
                McpChatApiClient.ActiveAuthMode = (AuthMode)newAuthMode;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            if (_settingsAuthMode == 0)
            {
                DrawGenericApiKeySection("anthropic", ProviderRegistry.GetPreset("anthropic"));
            }
            else
            {
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
                EditorGUILayout.LabelField("\u25CF Connected", EditorStyles.boldLabel);
                GUI.color = origC;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Logout", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    McpChatOAuth.Logout();
                    _settingsOAuthStatus = "";
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (McpChatOAuth.IsExchanging)
            {
                EditorGUILayout.LabelField("  Exchanging code...", EditorStyles.miniLabel);
            }
            else
            {
                // Login button
                if (GUILayout.Button("Login with Claude", GUILayout.Height(24)))
                {
                    McpChatOAuth.StartLogin("max");
                    _settingsOAuthStatus = "Browser opened — authorize then paste the code below.";
                }

                // Code input + exchange on same line
                EditorGUILayout.BeginHorizontal();
                _settingsOAuthCodeInput = EditorGUILayout.TextField(_settingsOAuthCodeInput);
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_settingsOAuthCodeInput));
                if (GUILayout.Button("Exchange", GUILayout.Width(70)))
                {
                    McpChatOAuth.ExchangeCode(
                        _settingsOAuthCodeInput,
                        _ => { _settingsOAuthStatus = ""; _settingsOAuthCodeInput = ""; Repaint(); },
                        err => { _settingsOAuthStatus = $"Error: {err}"; Repaint(); }
                    );
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(_settingsOAuthStatus))
            {
                EditorGUILayout.LabelField(_settingsOAuthStatus, EditorStyles.miniLabel);
            }
        }

        private void DrawGenericApiKeySection(string providerId, ProviderPreset preset)
        {
            if (_settingsApiKeyCachedProviderId != providerId)
            {
                _settingsApiKeyCachedProviderId = providerId;
                _settingsApiKeyInput = ProviderRegistry.GetApiKey(providerId);
                _settingsApiKeyDirty = false;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(55));

            EditorGUI.BeginChangeCheck();
            if (_settingsCredentialVisible)
                _settingsApiKeyInput = EditorGUILayout.TextField(_settingsApiKeyInput);
            else
                _settingsApiKeyInput = EditorGUILayout.PasswordField(_settingsApiKeyInput);
            if (EditorGUI.EndChangeCheck()) _settingsApiKeyDirty = true;

            if (GUILayout.Button(_settingsCredentialVisible ? "Hide" : "Show", EditorStyles.miniButton, GUILayout.Width(40)))
                _settingsCredentialVisible = !_settingsCredentialVisible;

            if (preset != null && !string.IsNullOrEmpty(preset.apiKeyUrl))
            {
                if (GUILayout.Button("Get", EditorStyles.miniButton, GUILayout.Width(30)))
                    Application.OpenURL(preset.apiKeyUrl);
            }
            EditorGUILayout.EndHorizontal();

            if (_settingsApiKeyDirty && !EditorGUIUtility.editingTextField)
            {
                ProviderRegistry.SetApiKey(providerId, _settingsApiKeyInput);
                _settingsApiKeyDirty = false;
            }

            if (preset != null && !string.IsNullOrEmpty(preset.apiKeyEnvVar))
            {
                string envValue = Environment.GetEnvironmentVariable(preset.apiKeyEnvVar);
                if (!string.IsNullOrEmpty(envValue))
                    EditorGUILayout.LabelField($"  \u25CF Env {preset.apiKeyEnvVar} detected", EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("MCP logs are hidden from Unity Console. View them in the Logs section below.", MessageType.Info);
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

                // Custom Endpoint
                EditorGUILayout.LabelField("Provider Endpoint", EditorStyles.boldLabel);
                var epIds = ProviderRegistry.GetPresetIds();
                if (epIds == null || epIds.Length == 0)
                {
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    return;
                }
                if (_settingsProviderIndex < 0 || _settingsProviderIndex >= epIds.Length)
                    _settingsProviderIndex = 0;
                string epId = epIds[_settingsProviderIndex];
                var epPreset = ProviderRegistry.GetPreset(epId);
                string currentEndpoint = ProviderRegistry.GetCustomEndpoint(epId);
                string newEndpoint = EditorGUILayout.TextField(currentEndpoint);
                if (newEndpoint != currentEndpoint)
                    ProviderRegistry.SetCustomEndpoint(epId, newEndpoint);
                if (epPreset != null && !string.IsNullOrEmpty(epPreset.defaultEndpoint))
                    EditorGUILayout.LabelField($"  Default: {epPreset.defaultEndpoint}", EditorStyles.miniLabel);

                EditorGUILayout.Space(8);

                // Custom System Prompt
                EditorGUILayout.LabelField("System Prompt Override", EditorStyles.boldLabel);
                string newPrompt = EditorGUILayout.TextArea(_settingsCustomSystemPrompt, GUILayout.MinHeight(40));
                if (newPrompt != _settingsCustomSystemPrompt)
                {
                    _settingsCustomSystemPrompt = newPrompt;
                    EditorPrefs.SetString(SystemPromptPref, newPrompt);
                }

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
    }
}
