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

                // FIX-#397: drain pipes asynchronously to avoid OS pipe-buffer deadlock
                // (npm/tsc can emit > 64KB), and cap WaitForExit at 5 minutes so a hung
                // npm install does not freeze the editor indefinitely.
                var stdoutBuilder = new System.Text.StringBuilder();
                var stderrBuilder = new System.Text.StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                const int TimeoutMs = 5 * 60 * 1000;
                if (!p.WaitForExit(TimeoutMs))
                {
                    try { p.Kill(); } catch { /* best-effort */ }
                    result.Output = $"npm build timed out after {TimeoutMs / 1000}s. Check network / disk and retry from a terminal.";
                    result.Success = false;
                    return result;
                }

                result.Output = stdoutBuilder.ToString() + stderrBuilder.ToString();
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
