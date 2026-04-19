// MCP Unity — Quick Start Sample
// McpBootstrapValidator.cs
// 
// Editor utility that checks your MCP Unity setup is correct.
// Open via: Tools > MCP Unity > Validate Setup
//
// This script is part of the "Quick Start" sample.
// Safe to delete once your setup is verified.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace McpUnity.Samples.QuickStart
{
    public static class McpBootstrapValidator
    {
        [MenuItem("Tools/MCP Unity/Validate Setup")]
        public static void ValidateSetup()
        {
            bool allGood = true;

            // 1. Check Node.js
            bool nodeFound = CheckNodeJs();
            allGood &= nodeFound;

            // 2. Check MCP settings asset
            bool settingsFound = CheckMcpSettings();
            allGood &= settingsFound;

            // 3. Check Server~ build
            bool serverFound = CheckServerBuild();
            allGood &= serverFound;

            // Summary
            if (allGood)
            {
                Debug.Log("[MCP Unity] ✓ All checks passed! Open Window > MCP Unity and click 'Start Server'.");
                EditorUtility.DisplayDialog(
                    "MCP Unity — Setup Valid",
                    "All checks passed.\n\nNext step: Open Window > MCP Unity → Settings tab → Start Server.",
                    "OK"
                );
            }
            else
            {
                Debug.LogWarning("[MCP Unity] Some checks failed. See Console for details.");
                EditorUtility.DisplayDialog(
                    "MCP Unity — Setup Issues Found",
                    "Some requirements are missing.\nCheck the Console window for details.",
                    "OK"
                );
            }
        }

        private static bool CheckNodeJs()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Debug.Log($"[MCP Unity] ✓ Node.js found: {version}");
                    return true;
                }
            }
            catch { }

            Debug.LogWarning("[MCP Unity] ✗ Node.js not found. Install from https://nodejs.org (v18 or later).");
            return false;
        }

        private static bool CheckMcpSettings()
        {
            // Look for McpSettings asset
            var guids = AssetDatabase.FindAssets("t:ScriptableObject McpSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Debug.Log($"[MCP Unity] ✓ McpSettings found at: {path}");
                return true;
            }

            // Not yet created — will be auto-created when opening the window
            Debug.Log("[MCP Unity] ℹ McpSettings not yet created. Open Window > MCP Unity to initialize.");
            return true; // Not a blocker
        }

        private static bool CheckServerBuild()
        {
            // Server~ is excluded from Unity's asset database (~ suffix), so check filesystem directly
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            string[] searchPaths = {
                System.IO.Path.Combine(projectRoot, "Library", "PackageCache"),
                System.IO.Path.Combine(projectRoot, "Assets", "Plugins", "MCP-Unity-Package", "Server~", "build")
            };

            foreach (string path in searchPaths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    // Look for index.js
                    string[] files = System.IO.Directory.GetFiles(path, "index.js", System.IO.SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        Debug.Log($"[MCP Unity] ✓ Server build found at: {files[0]}");
                        return true;
                    }
                }
            }

            Debug.LogWarning("[MCP Unity] ✗ Server build not found. In MCP Unity window, go to Settings > Build Server.");
            return false;
        }
    }
}
#endif
