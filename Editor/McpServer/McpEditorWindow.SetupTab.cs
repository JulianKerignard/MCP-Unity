using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Editor
{
    public partial class McpEditorWindow
    {
        // ====================================================================
        // Tab 3: Setup
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
                    "The Node.js bridge is not built yet. Click 'Build Bridge Now' to run " +
                    "npm install && npm run build automatically.",
                    MessageType.Warning);

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
                if (GUILayout.Button("Build Bridge Now", GUILayout.Height(28)))
                {
                    BuildBridgeFromSetupTab();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void BuildBridgeFromSetupTab()
        {
            var result = McpBridgeBuilder.BuildBridgeWithProgress();
            if (result.Success)
            {
                if (McpSettings.Instance.ShowNotifications)
                    ShowNotification(new GUIContent("Bridge built successfully"));
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Build failed",
                    "Failed to build the Node.js bridge.\n\nOutput:\n" + result.Output,
                    "OK");
            }
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
            // Bridge must be built — otherwise the AI client will fail to connect.
            if (!McpBridgeBuilder.IsBuilt())
            {
                bool doBuild = EditorUtility.DisplayDialog("Bridge not built",
                    "The Node.js bridge is not built yet. Build it now?\n\n" +
                    "(Runs npm install && npm run build in Server~/)",
                    "Build now", "Cancel");
                if (!doBuild) return;

                var build = McpBridgeBuilder.BuildBridgeWithProgress();
                if (!build.Success)
                {
                    EditorUtility.DisplayDialog("Bridge build failed",
                        "Output:\n" + build.Output, "OK");
                    return;
                }
                AssetDatabase.Refresh();
            }

            // VS Code uses "servers" root key, others use "mcpServers"
            bool isVSCode = configPath.Contains(".vscode");
            string content = isVSCode
                ? McpSettings.Instance.GenerateVSCodeMcpConfig()
                : McpSettings.Instance.GenerateLocalMcpConfig();
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
            // Build the bridge first if missing — config files alone are useless without it.
            if (!McpBridgeBuilder.IsBuilt())
            {
                var build = McpBridgeBuilder.BuildBridgeWithProgress();
                if (!build.Success)
                {
                    EditorUtility.DisplayDialog("Bridge build failed",
                        "Cannot create config files because the Node.js bridge build failed.\n\n" +
                        "Output:\n" + build.Output,
                        "OK");
                    return;
                }
                AssetDatabase.Refresh();
            }

            int created = 0;
            var errors = new StringBuilder();

            foreach (var (label, path) in editors)
            {
                if (!File.Exists(path))
                {
                    bool isVSCode = path.Contains(".vscode");
                    string content = isVSCode
                        ? McpSettings.Instance.GenerateVSCodeMcpConfig()
                        : McpSettings.Instance.GenerateLocalMcpConfig();
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
    }
}
