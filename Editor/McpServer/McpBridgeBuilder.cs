using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Editor
{
    /// <summary>
    /// Builds the Node.js bridge (npm install + npm run build).
    /// Shared by the Setup Wizard and the Setup Tab so a single click is enough.
    /// </summary>
    public static class McpBridgeBuilder
    {
        public struct BuildResult
        {
            public bool Success;
            public string Output;
        }

        public static bool IsBuilt()
        {
            return File.Exists(McpSettings.Instance.EffectiveServerPath);
        }

        /// <summary>
        /// Run npm install + npm run build synchronously with a progress bar.
        /// Returns success + combined stdout/stderr.
        /// </summary>
        public static BuildResult BuildBridgeWithProgress()
        {
            var result = new BuildResult { Success = false, Output = "" };
            string bridgePath = McpSettings.GetServerSourcePath();

            if (!Directory.Exists(bridgePath))
            {
                result.Output = $"Bridge directory not found:\n{bridgePath}";
                return result;
            }

            if (!McpNodeCheck.IsNodeInstalled())
            {
                result.Output = "Node.js not found in PATH. Install Node.js 18+ from https://nodejs.org then retry.";
                return result;
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Building MCP Bridge",
                    "Running npm install && npm run build...",
                    0.5f);

                bool isWin = Application.platform == RuntimePlatform.WindowsEditor;
                string shell = isWin ? "cmd.exe" : "/bin/sh";
                string args = isWin
                    ? $"/c cd /d \"{bridgePath}\" && npm install && npm run build"
                    : $"-c \"cd '{bridgePath}' && npm install && npm run build\"";

                var psi = new ProcessStartInfo(shell, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                {
                    result.Output = "Failed to start build process.";
                    return result;
                }

                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                result.Output = stdout + stderr;
                result.Success = p.ExitCode == 0 && IsBuilt();
            }
            catch (Exception ex)
            {
                result.Output = $"Exception: {ex.Message}";
                result.Success = false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }
    }
}
