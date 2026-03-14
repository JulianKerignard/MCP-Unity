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
            DrawSettingsProviderSection();
            EditorGUILayout.Space(4);
            DrawSettingsAdvancedSection();
            EditorGUILayout.Space(4);
            DrawDiagnosticsClaudeConfigSection();
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
