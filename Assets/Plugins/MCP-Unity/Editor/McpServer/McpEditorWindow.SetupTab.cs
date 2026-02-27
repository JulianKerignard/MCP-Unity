using System;
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

            EditorGUILayout.Space(10);

            // --- Claude Code Skills ---
            DrawSetupSkillsSection();
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
            var errors = new StringBuilder();

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

        // ====================================================================
        // Claude Code Skills
        // ====================================================================

        private static readonly (string id, string label)[] SkillDefinitions =
        {
            ("mcp-unity",      "MCP Unity (reference)"),
            ("unity-planner",  "Unity Planner (templates)"),
            ("unity-plan",     "/unity-plan"),
            ("gdd",            "/gdd"),
            ("tdd-unity",      "/tdd-unity"),
            ("level-design",   "/level-design"),
            ("art-direction",  "/art-direction"),
            ("milestone",      "/milestone"),
            ("unity-story",    "/unity-story"),
            ("unity-review",   "/unity-review"),
        };

        private static string GetSkillsSourcePath()
        {
            return Path.Combine(Application.dataPath, "Plugins", "MCP-Unity", "ClaudeSkills~");
        }

        private static string GetSkillsTargetPath()
        {
            string projectRoot = McpSettings.GetProjectRootPath();
            return Path.Combine(projectRoot, ".claude", "skills");
        }

        private void DrawSetupSkillsSection()
        {
            EditorGUILayout.LabelField("Claude Code Skills", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.HelpBox(
                "Install slash-command skills for Claude Code (/gdd, /unity-plan, /level-design, etc.).\n" +
                "Skills are copied to .claude/skills/ in the project root (local to this project).",
                MessageType.Info);

            EditorGUILayout.Space(4);

            string sourcePath = GetSkillsSourcePath();
            string targetPath = GetSkillsTargetPath();

            bool sourceExists = Directory.Exists(sourcePath);
            if (!sourceExists)
            {
                EditorGUILayout.HelpBox(
                    $"Skills source folder not found:\n{sourcePath}",
                    MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            // List skills with individual Install/Remove buttons
            int installedCount = 0;
            int totalCount = SkillDefinitions.Length;

            foreach (var (id, label) in SkillDefinitions)
            {
                bool installed = Directory.Exists(Path.Combine(targetPath, id));
                if (installed) installedCount++;

                EditorGUILayout.BeginHorizontal();

                // Status dot
                var origColor = GUI.color;
                GUI.color = installed ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
                GUILayout.Label(installed ? "\u25CF" : "\u25CB", GUILayout.Width(16));
                GUI.color = origColor;

                // Label
                EditorGUILayout.LabelField(label, GUILayout.Width(200));

                // Individual Install / Remove button
                if (installed)
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        RemoveSingleSkill(targetPath, id);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
                    if (GUILayout.Button("Install", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        InstallSingleSkill(sourcePath, targetPath, id);
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);

            // Summary
            EditorGUILayout.LabelField(
                $"{installedCount}/{totalCount} skills installed",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(4);

            // Bulk buttons
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("Install All", GUILayout.Height(26)))
            {
                InstallSkills(sourcePath, targetPath);
            }
            GUI.backgroundColor = Color.white;

            if (installedCount > 0)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Uninstall All", GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("Uninstall All Skills",
                        "Remove all MCP Unity skills from the project's .claude/skills/ folder?", "Uninstall", "Cancel"))
                    {
                        UninstallSkills(targetPath);
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Target: {targetPath}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void InstallSingleSkill(string sourcePath, string targetPath, string skillId)
        {
            try
            {
                string src = Path.Combine(sourcePath, skillId);
                string dest = Path.Combine(targetPath, skillId);

                if (!Directory.Exists(src))
                {
                    McpServerLogger.Instance.Warning($"Skill source not found: {skillId}");
                    return;
                }

                if (!Directory.Exists(targetPath))
                    Directory.CreateDirectory(targetPath);

                if (Directory.Exists(dest))
                    Directory.Delete(dest, true);

                CopyDirectoryRecursive(src, dest);
                McpServerLogger.Instance.Info($"Installed skill: {skillId}");

                if (McpSettings.Instance.ShowNotifications)
                    ShowNotification(new GUIContent($"Skill {skillId} installed"));
            }
            catch (Exception ex)
            {
                McpServerLogger.Instance.Error($"Failed to install skill: {skillId}", ex);
                EditorUtility.DisplayDialog("Error",
                    $"Failed to install skill {skillId}:\n{ex.Message}", "OK");
            }
        }

        private void RemoveSingleSkill(string targetPath, string skillId)
        {
            try
            {
                string dest = Path.Combine(targetPath, skillId);
                if (Directory.Exists(dest))
                {
                    Directory.Delete(dest, true);
                    McpServerLogger.Instance.Info($"Removed skill: {skillId}");

                    if (McpSettings.Instance.ShowNotifications)
                        ShowNotification(new GUIContent($"Skill {skillId} removed"));
                }
            }
            catch (Exception ex)
            {
                McpServerLogger.Instance.Error($"Failed to remove skill: {skillId}", ex);
                EditorUtility.DisplayDialog("Error",
                    $"Failed to remove skill {skillId}:\n{ex.Message}", "OK");
            }
        }

        private void InstallSkills(string sourcePath, string targetPath)
        {
            try
            {
                if (!Directory.Exists(targetPath))
                    Directory.CreateDirectory(targetPath);

                int installed = 0;
                var errors = new StringBuilder();

                foreach (var (id, label) in SkillDefinitions)
                {
                    string src = Path.Combine(sourcePath, id);
                    string dest = Path.Combine(targetPath, id);

                    if (!Directory.Exists(src))
                    {
                        McpServerLogger.Instance.Warning($"Skill source not found, skipping: {id}");
                        continue;
                    }

                    try
                    {
                        if (Directory.Exists(dest))
                            Directory.Delete(dest, true);

                        CopyDirectoryRecursive(src, dest);
                        installed++;
                        McpServerLogger.Instance.Info($"Installed skill: {id}");
                    }
                    catch (Exception ex)
                    {
                        errors.AppendLine($"{id}: {ex.Message}");
                        McpServerLogger.Instance.Error($"Failed to install skill: {id}", ex);
                    }
                }

                if (errors.Length == 0)
                {
                    McpServerLogger.Instance.Info($"All {installed} skills installed successfully");
                    if (McpSettings.Instance.ShowNotifications)
                        ShowNotification(new GUIContent($"{installed} skills installed"));
                }
                else
                {
                    EditorUtility.DisplayDialog("Some skills failed",
                        $"{installed} skill(s) installed.\n\nErrors:\n{errors}", "OK");
                }
            }
            catch (Exception ex)
            {
                McpServerLogger.Instance.Error("Failed to install skills", ex);
                EditorUtility.DisplayDialog("Error",
                    $"Failed to install skills:\n{ex.Message}", "OK");
            }
        }

        private void UninstallSkills(string targetPath)
        {
            int removed = 0;

            foreach (var (id, _) in SkillDefinitions)
            {
                string dest = Path.Combine(targetPath, id);
                if (Directory.Exists(dest))
                {
                    try
                    {
                        Directory.Delete(dest, true);
                        removed++;
                        McpServerLogger.Instance.Info($"Removed skill: {id}");
                    }
                    catch (Exception ex)
                    {
                        McpServerLogger.Instance.Error($"Failed to remove skill: {id}", ex);
                    }
                }
            }

            McpServerLogger.Instance.Info($"Uninstalled {removed} skills");
            if (McpSettings.Instance.ShowNotifications)
                ShowNotification(new GUIContent($"{removed} skills removed"));
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }
    }
}
