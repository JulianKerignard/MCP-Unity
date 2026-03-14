using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Editor
{
    /// <summary>
    /// One-time setup wizard that opens automatically after the package is imported.
    /// Guides the user through: Node.js check → npm build → Claude config generation.
    /// Triggered once per project via EditorPrefs.
    /// </summary>
    [InitializeOnLoad]
    public static class McpSetupWizard
    {
        private const string ShownKey = "McpUnity_SetupWizard_Shown_v1";

        static McpSetupWizard()
        {
            // Open once per project, deferred so Editor is fully loaded
            if (!EditorPrefs.GetBool(ShownKey, false))
            {
                EditorApplication.delayCall += ShowWizard;
            }
        }

        [MenuItem("Tools/MCP Unity/Setup Wizard", priority = 50)]
        public static void ShowWizard()
        {
            McpSetupWizardWindow.Open();
        }
    }

    /// <summary>
    /// The actual wizard window — 3 steps: Node.js check, npm build, Claude config.
    /// </summary>
    public class McpSetupWizardWindow : EditorWindow
    {
        private const string ShownKey  = "McpUnity_SetupWizard_Shown_v1";

        private enum Step { NodeCheck, NpmBuild, ClaudeConfig, Done }
        private Step _currentStep = Step.NodeCheck;

        // Node.js check results
        private bool  _nodeChecked;
        private bool  _nodeFound;
        private string _nodeVersion = "";
        private bool  _npmFound;

        // Build state
        private bool  _buildRunning;
        private bool  _buildDone;
        private bool  _buildSuccess;
        private string _buildOutput = "";

        // Scroll
        private Vector2 _scroll;

        // Styles (lazy)
        private GUIStyle _headerStyle;
        private GUIStyle _stepLabelStyle;
        private GUIStyle _codeStyle;
        private bool     _stylesReady;

        public static void Open()
        {
            var win = GetWindow<McpSetupWizardWindow>(true, "MCP Unity — Setup Wizard", true);
            win.minSize = new Vector2(520, 420);
            win.maxSize = new Vector2(640, 600);
            win.Show();

            EditorPrefs.SetBool(ShownKey, true);
        }

        private void OnGUI()
        {
            InitStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            GUILayout.Space(8);
            DrawStepBar();
            GUILayout.Space(12);

            switch (_currentStep)
            {
                case Step.NodeCheck:   DrawNodeCheckStep();   break;
                case Step.NpmBuild:    DrawNpmBuildStep();    break;
                case Step.ClaudeConfig: DrawClaudeConfigStep(); break;
                case Step.Done:        DrawDoneStep();        break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Header ──────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("MCP Unity — Setup Wizard", _headerStyle);
            EditorGUILayout.LabelField(
                "Connect any AI assistant to your Unity Editor in 3 steps.",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ── Step bar ─────────────────────────────────────────────────────────

        private void DrawStepBar()
        {
            string[] labels = { "1. Node.js", "2. Build Bridge", "3. Claude Config", "✓ Done" };
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Length; i++)
            {
                bool active = (int)_currentStep == i;
                bool done   = (int)_currentStep > i;
                var style = done   ? _stepLabelStyle :
                            active ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                var color  = done   ? Color.green :
                             active ? Color.white : Color.gray;
                var prev = GUI.color;
                GUI.color = color;
                EditorGUILayout.LabelField(labels[i], style, GUILayout.ExpandWidth(true));
                GUI.color = prev;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // ── Step 1 : Node.js check ───────────────────────────────────────────

        private void DrawNodeCheckStep()
        {
            EditorGUILayout.LabelField("Step 1 — Check Node.js Installation", EditorStyles.boldLabel);
            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "MCP Unity requires Node.js 18+ to run the bridge that connects AI clients to Unity.\n" +
                "If Node.js is not installed, visit https://nodejs.org",
                MessageType.Info);
            GUILayout.Space(8);

            if (!_nodeChecked)
            {
                if (GUILayout.Button("Check Node.js", GUILayout.Height(32)))
                    CheckNode();
                return;
            }

            if (_nodeFound && _npmFound)
            {
                EditorGUILayout.HelpBox($"Node.js {_nodeVersion} found. npm found. Ready!", MessageType.None);
                GUILayout.Space(8);
                if (GUILayout.Button("Next →", GUILayout.Height(32)))
                    _currentStep = Step.NpmBuild;
            }
            else
            {
                if (!_nodeFound)
                    EditorGUILayout.HelpBox(
                        "Node.js not found in PATH.\n" +
                        "Install Node.js 18+ from https://nodejs.org then re-open this wizard.",
                        MessageType.Error);
                else
                    EditorGUILayout.HelpBox(
                        "npm not found. Reinstall Node.js to fix this.",
                        MessageType.Warning);

                if (GUILayout.Button("Open nodejs.org", GUILayout.Height(28)))
                    Application.OpenURL("https://nodejs.org");

                GUILayout.Space(4);
                if (GUILayout.Button("Re-check", GUILayout.Height(28)))
                {
                    _nodeChecked = false;
                    CheckNode();
                }
            }
        }

        private void CheckNode()
        {
            _nodeChecked = false;
            _nodeFound   = false;
            _npmFound    = false;
            _nodeVersion = "";

            try
            {
                var psi = new ProcessStartInfo("node", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var p = Process.Start(psi);
                _nodeVersion = p?.StandardOutput.ReadToEnd()?.Trim() ?? "";
                p?.WaitForExit();
                _nodeFound = !string.IsNullOrEmpty(_nodeVersion);
            }
            catch { _nodeFound = false; }

            try
            {
                var psi = new ProcessStartInfo("npm", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
                _npmFound = p?.ExitCode == 0;
            }
            catch { _npmFound = false; }

            _nodeChecked = true;
            Repaint();
        }

        // ── Step 2 : npm build ───────────────────────────────────────────────

        private void DrawNpmBuildStep()
        {
            EditorGUILayout.LabelField("Step 2 — Build the Node.js Bridge", EditorStyles.boldLabel);
            GUILayout.Space(6);

            string fullBridgePath = GetAbsoluteBridgePath();
            bool   buildAlreadyExists = File.Exists(Path.Combine(fullBridgePath, "build", "index.js"));

            if (buildAlreadyExists && !_buildDone)
            {
                EditorGUILayout.HelpBox(
                    "Bridge already built (build/index.js exists).\n" +
                    "You can skip this step or rebuild to get the latest version.",
                    MessageType.None);
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Skip (already built)", GUILayout.Height(32)))
                    _currentStep = Step.ClaudeConfig;
                if (GUILayout.Button("Rebuild", GUILayout.Height(32)))
                    RunNpmBuild(fullBridgePath);
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (!_buildDone)
            {
                EditorGUILayout.HelpBox(
                    $"Bridge directory: {fullBridgePath}\n\n" +
                    "Click 'Build Bridge' to run: npm install && npm run build",
                    MessageType.Info);
                GUILayout.Space(4);

                if (_buildRunning)
                {
                    EditorGUILayout.HelpBox("Building... (this may take 10-20 seconds)", MessageType.None);
                    Repaint();
                }
                else
                {
                    if (GUILayout.Button("Build Bridge", GUILayout.Height(32)))
                        RunNpmBuild(fullBridgePath);
                }
                return;
            }

            // Build finished
            if (_buildSuccess)
            {
                EditorGUILayout.HelpBox("Bridge built successfully!", MessageType.None);
                GUILayout.Space(4);
                if (GUILayout.Button("Next →", GUILayout.Height(32)))
                    _currentStep = Step.ClaudeConfig;
            }
            else
            {
                EditorGUILayout.HelpBox("Build failed. See output below.", MessageType.Error);
                EditorGUILayout.LabelField("Build output:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_buildOutput, _codeStyle, GUILayout.ExpandHeight(true));
                GUILayout.Space(4);
                if (GUILayout.Button("Retry", GUILayout.Height(28)))
                {
                    _buildDone = false;
                    RunNpmBuild(fullBridgePath);
                }
            }
        }

        private void RunNpmBuild(string bridgePath)
        {
            if (!Directory.Exists(bridgePath))
            {
                _buildOutput  = $"Bridge directory not found:\n{bridgePath}";
                _buildSuccess = false;
                _buildDone    = true;
                return;
            }

            _buildRunning = true;
            _buildDone    = false;
            _buildOutput  = "";
            Repaint();

            // Run npm install + npm run build synchronously (Editor-safe, blocks briefly)
            try
            {
                string shell     = Application.platform == RuntimePlatform.WindowsEditor ? "cmd.exe" : "/bin/sh";
                string shellArgs = Application.platform == RuntimePlatform.WindowsEditor
                    ? $"/c cd /d \"{bridgePath}\" && npm install && npm run build"
                    : $"-c \"cd '{bridgePath}' && npm install && npm run build\"";

                var psi = new ProcessStartInfo(shell, shellArgs)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var p = Process.Start(psi);
                _buildOutput  = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                _buildSuccess = p.ExitCode == 0 && File.Exists(Path.Combine(bridgePath, "build", "index.js"));
            }
            catch (Exception ex)
            {
                _buildOutput  = $"Exception: {ex.Message}";
                _buildSuccess = false;
            }

            _buildRunning = false;
            _buildDone    = true;
            Repaint();
        }

        // ── Step 3 : Claude config ───────────────────────────────────────────

        private void DrawClaudeConfigStep()
        {
            EditorGUILayout.LabelField("Step 3 — Configure Your AI Client", EditorStyles.boldLabel);
            GUILayout.Space(6);

            // Use the canonical config generator (includes UNITY_HOST, UNITY_SECRET, etc.)
            string config = McpSettings.Instance.GenerateLocalMcpConfig();
            string buildPath = McpSettings.Instance.EffectiveServerPath.Replace("\\", "/");

            EditorGUILayout.HelpBox(
                "Click 'Setup All Editors' to automatically create config files for Claude Code, Cursor, Windsurf, and VS Code.\n" +
                "Or copy the JSON below to configure manually.",
                MessageType.Info);

            GUILayout.Space(4);
            EditorGUILayout.LabelField(".mcp.json / mcp.json:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(config, _codeStyle, GUILayout.Height(140));
            GUILayout.Space(4);

            // Auto-setup button — writes .mcp.json and editor configs
            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("Setup All Editors (Claude Code, Cursor, Windsurf, VS Code)", GUILayout.Height(32)))
            {
                SetupAllEditorConfigs();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(28)))
            {
                GUIUtility.systemCopyBuffer = config;
                EditorUtility.DisplayDialog("Copied", "Config copied to clipboard.", "OK");
            }
            if (GUILayout.Button("Open Settings Window", GUILayout.Height(28)))
            {
                EditorApplication.ExecuteMenuItem("Tools/MCP Unity/Server Window");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("Finish →", GUILayout.Height(32)))
                _currentStep = Step.Done;
        }

        /// <summary>
        /// Write config files for all supported editors (Claude Code, Cursor, Windsurf, VS Code).
        /// </summary>
        private void SetupAllEditorConfigs()
        {
            var editors = new[]
            {
                ("Claude Code CLI",   McpSettings.GetClaudeCodeConfigPath()),
                ("Cursor",            McpSettings.GetCursorConfigPath()),
                ("Windsurf",          McpSettings.GetWindsurfConfigPath()),
                ("VS Code / Copilot", McpSettings.GetVSCodeConfigPath()),
            };

            string content = McpSettings.Instance.GenerateLocalMcpConfig();
            int created = 0;

            foreach (var (label, path) in editors)
            {
                string err = McpSettings.WriteConfigFile(path, content);
                if (err == null)
                    created++;
                else
                    McpDebug.LogWarning($"[MCP Unity] Failed to write {label} config: {err}");
            }

            EditorUtility.DisplayDialog("Setup Complete",
                $"{created} config file(s) created.\n\n" +
                "Restart your AI editor (Claude Code, Cursor, etc.) to connect to Unity.",
                "OK");
        }

        // ── Step 4 : Done ────────────────────────────────────────────────────

        private void DrawDoneStep()
        {
            EditorGUILayout.LabelField("Setup Complete!", EditorStyles.boldLabel);
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "MCP Unity is ready.\n\n" +
                "1. In Unity: Tools > MCP Unity > Server Window → Start Server\n" +
                "2. In your AI client: restart or reload MCP servers\n" +
                "3. Ask Claude: \"List the GameObjects in my scene\"",
                MessageType.None);
            GUILayout.Space(12);

            if (GUILayout.Button("Open Server Window", GUILayout.Height(36)))
                EditorApplication.ExecuteMenuItem("Tools/MCP Unity/Server Window");

            GUILayout.Space(4);
            if (GUILayout.Button("Close", GUILayout.Height(28)))
                Close();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetAbsoluteBridgePath()
        {
            return McpSettings.GetServerSourcePath();
        }

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold
            };

            _stepLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.green }
            };

            _codeStyle = new GUIStyle(EditorStyles.textArea)
            {
                font     = Font.CreateDynamicFontFromOSFont("Courier New", 11),
                wordWrap = false
            };
        }
    }
}
