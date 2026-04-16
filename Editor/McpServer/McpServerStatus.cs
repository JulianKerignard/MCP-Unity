using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using McpUnity.Server;

namespace McpUnity.Editor
{
    /// <summary>
    /// Static class to manage MCP server state across the Editor.
    /// Bridges the Editor UI with the actual McpUnityServer implementation.
    /// </summary>
    [InitializeOnLoad]
    public static class McpServerStatus
    {
        private static DateTime _startTime;
        private static readonly Stopwatch _uptimeStopwatch = new Stopwatch();

        /// <summary>
        /// Event fired when server state changes
        /// </summary>
        public static event Action<bool> OnServerStateChanged;

        /// <summary>
        /// Event fired when client count changes
        /// </summary>
        public static event Action<int> OnClientCountChanged;

        /// <summary>
        /// Whether the server is currently running
        /// </summary>
        public static bool IsRunning => McpUnityServer.IsRunning;

        /// <summary>
        /// Number of connected clients (always reads from the actual server)
        /// </summary>
        public static int ConnectedClients => McpUnityServer.ConnectedClientCount;

        /// <summary>
        /// Server start time
        /// </summary>
        public static DateTime StartTime => _startTime;

        /// <summary>
        /// Server uptime. Uses a monotonic Stopwatch so it is immune to wall-clock
        /// changes (DST transitions, NTP corrections, manual clock edits).
        /// </summary>
        public static TimeSpan Uptime => IsRunning ? _uptimeStopwatch.Elapsed : TimeSpan.Zero;

        /// <summary>
        /// Current server endpoint
        /// </summary>
        public static string Endpoint => $"ws://127.0.0.1:{McpUnityServer.Port}";

        static McpServerStatus()
        {
            // Unsubscribe first to guard against duplicate handlers across domain reloads
            // ([InitializeOnLoad] re-runs this constructor on every reload).
            McpUnityServer.OnServerStarted -= OnServerStarted;
            McpUnityServer.OnServerStarted += OnServerStarted;
            McpUnityServer.OnServerStopped -= OnServerStopped;
            McpUnityServer.OnServerStopped += OnServerStopped;
            McpUnityServer.OnClientConnected -= OnClientConnected;
            McpUnityServer.OnClientConnected += OnClientConnected;
            McpUnityServer.OnClientDisconnected -= OnClientDisconnected;
            McpUnityServer.OnClientDisconnected += OnClientDisconnected;
        }

        private static void OnServerStarted()
        {
            _startTime = DateTime.UtcNow;
            _uptimeStopwatch.Restart();
            OnServerStateChanged?.Invoke(true);
        }

        private static void OnServerStopped()
        {
            _uptimeStopwatch.Stop();
            OnServerStateChanged?.Invoke(false);
        }

        private static void OnClientConnected(string clientId)
        {
            OnClientCountChanged?.Invoke(ConnectedClients);
        }

        private static void OnClientDisconnected(string clientId)
        {
            OnClientCountChanged?.Invoke(ConnectedClients);
        }

        /// <summary>
        /// Start the MCP server
        /// </summary>
        public static bool Start()
        {
            if (IsRunning)
            {
                return false;
            }

            McpUnityServer.Start();
            return IsRunning;
        }

        /// <summary>
        /// Stop the MCP server
        /// </summary>
        public static bool Stop()
        {
            if (!IsRunning)
            {
                return false;
            }

            McpUnityServer.Stop();
            return true;
        }

        /// <summary>
        /// Restart the MCP server
        /// </summary>
        public static bool Restart()
        {
            McpUnityServer.Restart();
            return true;
        }
    }
}
