using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace McpUnity.Editor
{
    /// <summary>
    /// Persistent settings for MCP Unity Server.
    /// Stores configuration in ProjectSettings/McpUnitySettings.json
    /// </summary>
    [Serializable]
    public class McpSettings
    {
        private const string SettingsPath = "ProjectSettings/McpUnitySettings.json";

        private static McpSettings _instance;
        public static McpSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        // Server Configuration
        [SerializeField] private int _port = 8090;
        [SerializeField] private string _host = "localhost";
        [SerializeField] private bool _allowRemoteConnections = false;
        [SerializeField] private int _requestTimeoutMs = 30000;
        [SerializeField] private bool _autoStartServer = false;
        [SerializeField] private bool _showNotifications = true;

        // Logging Configuration
        [SerializeField] private int _maxLogEntries = 500;
        [SerializeField] private bool _logToFile = false;
        [SerializeField] private bool _logToConsole = false;
        [SerializeField] private string _logFilePath = "Logs/McpUnity.log";
        [SerializeField] private LogLevel _minimumLogLevel = LogLevel.Info;

        // Security Configuration
        [SerializeField] private string _sharedSecret = "";

        // Claude Configuration
        [SerializeField] private string _customServerPath = "";
        [SerializeField] private bool _useCustomServerPath = false;

        // Properties
        public int Port
        {
            get => _port;
            set
            {
                if (value < 1 || value > 65535)
                    throw new ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535");
                _port = value;
                Save();
            }
        }

        public string Host
        {
            get => _host;
            set
            {
                _host = value ?? "localhost";
                Save();
            }
        }

        public bool AllowRemoteConnections
        {
            get => _allowRemoteConnections;
            set
            {
                _allowRemoteConnections = value;
                _host = value ? "0.0.0.0" : "localhost";
                Save();
            }
        }

        public int RequestTimeoutMs
        {
            get => _requestTimeoutMs;
            set
            {
                _requestTimeoutMs = Mathf.Max(1000, value);
                Save();
            }
        }

        public bool AutoStartServer
        {
            get => _autoStartServer;
            set
            {
                _autoStartServer = value;
                Save();
            }
        }

        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                _showNotifications = value;
                Save();
            }
        }

        public int MaxLogEntries
        {
            get => _maxLogEntries;
            set
            {
                _maxLogEntries = Mathf.Max(100, value);
                Save();
            }
        }

        public bool LogToFile
        {
            get => _logToFile;
            set
            {
                _logToFile = value;
                Save();
            }
        }

        public bool LogToConsole
        {
            get => _logToConsole;
            set
            {
                _logToConsole = value;
                Save();
            }
        }

        public string LogFilePath
        {
            get => _logFilePath;
            set
            {
                _logFilePath = value ?? "Logs/McpUnity.log";
                Save();
            }
        }

        public LogLevel MinimumLogLevel
        {
            get => _minimumLogLevel;
            set
            {
                _minimumLogLevel = value;
                Save();
            }
        }

        public string CustomServerPath
        {
            get => _customServerPath;
            set
            {
                _customServerPath = value ?? "";
                Save();
            }
        }

        public bool UseCustomServerPath
        {
            get => _useCustomServerPath;
            set
            {
                _useCustomServerPath = value;
                Save();
            }
        }

        /// <summary>
        /// Shared secret for WebSocket handshake authentication.
        /// If set, clients must send this as a query parameter (?secret=...) or header.
        /// Empty string = no authentication (default for local-only use).
        /// </summary>
        public string SharedSecret
        {
            get => _sharedSecret;
            set
            {
                _sharedSecret = value ?? "";
                Save();
            }
        }

        /// <summary>
        /// Whether shared secret authentication is enabled (non-empty secret).
        /// </summary>
        public bool IsSecretEnabled => !string.IsNullOrEmpty(_sharedSecret);

        /// <summary>
        /// Gets the effective server path based on settings
        /// </summary>
        public string EffectiveServerPath
        {
            get
            {
                if (_useCustomServerPath && !string.IsNullOrEmpty(_customServerPath))
                {
                    return _customServerPath;
                }
                return GetDefaultServerPath();
            }
        }

        /// <summary>
        /// Find the plugin root directory by locating the McpUnity.Editor asmdef via AssetDatabase.
        /// Works regardless of where the plugin is installed (Assets/plugin/, Assets/Plugins/, Packages/, etc.).
        /// Returns null if not found.
        /// </summary>
        public static string FindPluginRootPath()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("McpUnity.Editor");
            foreach (var guid in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("McpUnity.Editor.asmdef"))
                {
                    // asmdef is at <plugin-root>/Editor/McpUnity.Editor.asmdef
                    string editorDir = Path.GetDirectoryName(assetPath);
                    string pluginRoot = Path.GetDirectoryName(editorDir);
                    string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    return Path.GetFullPath(Path.Combine(projectRoot, pluginRoot));
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the Server~ source directory (for npm install/build).
        /// Dynamically detects the plugin location via asmdef.
        /// </summary>
        public static string GetServerSourcePath()
        {
            string pluginRoot = FindPluginRootPath();
            if (pluginRoot != null)
            {
                string serverDir = Path.Combine(pluginRoot, "Server~");
                if (Directory.Exists(serverDir))
                    return serverDir;
            }

            // Fallback: well-known locations
            string[] fallbacks = new string[]
            {
                Path.Combine(Application.dataPath, "Plugins", "MCP-Unity-Package", "Server~"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.juliank.mcp-unity", "Server~"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.claudecode.mcp-unity", "Server~"),
            };
            foreach (var path in fallbacks)
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                    return fullPath;
            }

            // Return best guess from dynamic detection even if not present
            if (pluginRoot != null)
                return Path.Combine(pluginRoot, "Server~");

            return Path.GetFullPath(fallbacks[0]);
        }

        /// <summary>
        /// Gets the default server path (build/index.js).
        /// Dynamically detects the plugin location via asmdef, then falls back to well-known paths.
        /// </summary>
        public static string GetDefaultServerPath()
        {
            // 1. Dynamic detection via asmdef — works for any install location
            string pluginRoot = FindPluginRootPath();
            if (pluginRoot != null)
            {
                string serverBuild = Path.Combine(pluginRoot, "Server~", "build", "index.js");
                if (File.Exists(serverBuild))
                    return serverBuild;
            }

            // 2. Fallback: check well-known hardcoded locations
            string[] possiblePaths = new string[]
            {
                Path.Combine(Application.dataPath, "Plugins", "MCP-Unity-Package", "Server~", "build", "index.js"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.juliank.mcp-unity", "Server~", "build", "index.js"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.claudecode.mcp-unity", "Server~", "build", "index.js"),
                Path.Combine(Application.dataPath, "..", "Packages", "com.mcp.unity", "Server~", "build", "index.js"),
                Path.Combine(Application.dataPath, "Server", "build", "index.js"),
            };

            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            // 3. Return best guess from dynamic detection (even if not built yet)
            if (pluginRoot != null)
                return Path.Combine(pluginRoot, "Server~", "build", "index.js");

            return Path.GetFullPath(possiblePaths[0]);
        }

        // ====================================================================
        // Project-local Config Paths (AI Editor Setup)
        // ====================================================================

        /// <summary>
        /// Returns the Unity project root (one level above Assets/).
        /// All local editor config files are placed relative to this path.
        /// </summary>
        public static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        /// <summary>Claude Code CLI — &lt;root&gt;/.mcp.json</summary>
        public static string GetClaudeCodeConfigPath()
        {
            return Path.Combine(GetProjectRootPath(), ".mcp.json");
        }

        /// <summary>Cursor — &lt;root&gt;/.cursor/mcp.json</summary>
        public static string GetCursorConfigPath()
        {
            return Path.Combine(GetProjectRootPath(), ".cursor", "mcp.json");
        }

        /// <summary>Windsurf — &lt;root&gt;/.windsurf/mcp.json</summary>
        public static string GetWindsurfConfigPath()
        {
            return Path.Combine(GetProjectRootPath(), ".windsurf", "mcp.json");
        }

        /// <summary>VS Code / GitHub Copilot — &lt;root&gt;/.vscode/mcp.json</summary>
        public static string GetVSCodeConfigPath()
        {
            return Path.Combine(GetProjectRootPath(), ".vscode", "mcp.json");
        }

        /// <summary>
        /// Generates mcpServers JSON for Claude Code CLI, Cursor, and Windsurf.
        /// </summary>
        public string GenerateLocalMcpConfig()
        {
            return GenerateMcpJson("mcpServers");
        }

        /// <summary>
        /// Generates servers JSON for VS Code / GitHub Copilot (uses "servers" root key).
        /// </summary>
        public string GenerateVSCodeMcpConfig()
        {
            return GenerateMcpJson("servers");
        }

        private string GenerateMcpJson(string rootKey)
        {
            string serverPath = EffectiveServerPath.Replace("\\", "/");
            string escapedPath = serverPath.Replace("\"", "\\\"");

            string secretEnv = IsSecretEnabled
                ? $@",
        ""UNITY_SECRET"": ""{_sharedSecret.Replace("\"", "\\\"")}"""
                : "";

            return $@"{{
  ""{rootKey}"": {{
    ""mcp-unity"": {{
      ""command"": ""node"",
      ""args"": [""{escapedPath}""],
      ""env"": {{
        ""UNITY_PORT"": ""{_port}"",
        ""UNITY_HOST"": ""{_host}""{secretEnv}
      }}
    }}
  }}
}}";
        }

        /// <summary>
        /// Writes (or overwrites) a local editor config file.
        /// Creates parent directories automatically.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public static string WriteConfigFile(string filePath, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(filePath, content);
                return null; // success
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>
        /// Gets the Claude Desktop configuration file path
        /// </summary>
        public static string GetClaudeConfigPath()
        {
#if UNITY_EDITOR_OSX
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Application Support/Claude/claude_desktop_config.json"
            );
#elif UNITY_EDITOR_WIN
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude/claude_desktop_config.json"
            );
#else
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/Claude/claude_desktop_config.json"
            );
#endif
        }

        /// <summary>
        /// Load settings from disk
        /// </summary>
        private static McpSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonUtility.FromJson<McpSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MCP Unity] Failed to load settings: {e.Message}. Using defaults.");
            }

            return new McpSettings();
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCP Unity] Failed to save settings: {e.Message}");
            }
        }

        /// <summary>
        /// Reset all settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            _port = 8090;
            _host = "localhost";
            _allowRemoteConnections = false;
            _requestTimeoutMs = 30000;
            _autoStartServer = false;
            _showNotifications = true;
            _maxLogEntries = 500;
            _logToFile = false;
            _logToConsole = false;
            _logFilePath = "Logs/McpUnity.log";
            _minimumLogLevel = LogLevel.Info;
            _sharedSecret = "";
            _customServerPath = "";
            _useCustomServerPath = false;
            Save();
        }

        /// <summary>
        /// Generates the Claude Desktop configuration JSON
        /// </summary>
        public string GenerateClaudeConfig()
        {
            string serverPath = EffectiveServerPath.Replace("\\", "/");
            string escapedPath = serverPath.Replace("\"", "\\\"");

            string secretEnv = IsSecretEnabled
                ? $@",
        ""UNITY_SECRET"": ""{_sharedSecret.Replace("\"", "\\\"")}"""
                : "";

            return $@"{{
  ""mcpServers"": {{
    ""mcp-unity"": {{
      ""command"": ""node"",
      ""args"": [""{escapedPath}""],
      ""env"": {{
        ""UNITY_PORT"": ""{_port}"",
        ""UNITY_HOST"": ""{_host}""{secretEnv}
      }}
    }}
  }}
}}";
        }
    }

    /// <summary>
    /// Log level enumeration
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}
