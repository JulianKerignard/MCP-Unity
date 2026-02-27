using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Editor
{
    public partial class McpEditorWindow
    {
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

                if (GUILayout.Button("Copy All", GUILayout.Width(60)))
                {
                    CopyLogsToClipboard();
                }
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
                DrawColoredLogs();
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
        // Diagnostics Helper Methods
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

        private void DrawColoredLogs()
        {
            var logs = McpServerLogger.Instance.Logs;
            bool hasEntries = false;

            for (int i = 0; i < logs.Count; i++)
            {
                var entry = logs[i];
                if (entry.Level < _logFilterLevel) continue;

                hasEntries = true;
                string text = entry.ToShortString();

                var originalColor = GUI.contentColor;
                GUI.contentColor = entry.GetColor();

                // SelectableLabel allows text selection and copy (Ctrl+C)
                EditorGUILayout.SelectableLabel(text, _logAreaStyle, GUILayout.Height(16));

                // Right-click context menu
                var lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.ContextClick && lastRect.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    string capturedText = text;
                    string fullText = entry.ToString();
                    menu.AddItem(new GUIContent("Copy Line"), false, () => EditorGUIUtility.systemCopyBuffer = capturedText);
                    menu.AddItem(new GUIContent("Copy Full (with timestamp)"), false, () => EditorGUIUtility.systemCopyBuffer = fullText);
                    menu.ShowAsContext();
                    Event.current.Use();
                }

                GUI.contentColor = originalColor;
            }

            if (!hasEntries)
            {
                EditorGUILayout.LabelField("No log entries.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void CopyLogsToClipboard()
        {
            string logs = McpServerLogger.Instance.GetFormattedLogs(_logFilterLevel);
            if (string.IsNullOrEmpty(logs))
            {
                if (McpSettings.Instance.ShowNotifications)
                    ShowNotification(new GUIContent("No logs to copy"));
                return;
            }
            EditorGUIUtility.systemCopyBuffer = logs;
            if (McpSettings.Instance.ShowNotifications)
                ShowNotification(new GUIContent("Logs copied!"));
        }

        private void RefreshLogs()
        {
            _cachedLogs = McpServerLogger.Instance.GetFormattedLogs(_logFilterLevel);
            _lastLogCount = McpServerLogger.Instance.Count;
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
    }
}
