using System;
using System.Diagnostics;
using UnityEditor;

namespace McpUnity.Editor
{
    /// <summary>
    /// Checks for Node.js at Editor startup and warns the user if it is missing.
    /// Runs once per Editor session (not every domain reload).
    /// </summary>
    [InitializeOnLoad]
    public static class McpNodeCheck
    {
        private const string SessionKey = "McpUnity_NodeCheckDone";

        static McpNodeCheck()
        {
            // Run only once per Editor session
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            EditorApplication.delayCall += RunCheck;
        }

        private static void RunCheck()
        {
            if (!IsNodeInstalled())
            {
                McpDebug.LogWarning(
                    "[MCP Unity] Node.js not found in PATH. " +
                    "The MCP bridge requires Node.js 18+. " +
                    "Install it from https://nodejs.org — then restart Unity.\n" +
                    "Open the Setup Wizard: Tools > MCP Unity > Setup Wizard");
            }
        }

        /// <summary>Returns true if 'node --version' exits with code 0.</summary>
        public static bool IsNodeInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo("node", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
                return p?.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
