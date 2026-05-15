using System;
using System.Collections.Generic;

namespace McpUnity.Editor
{
    /// <summary>
    /// Lightweight request monitor for MCP JSON-RPC calls.
    /// Tracks recent requests with timing and success/error status.
    /// </summary>
    public static class McpRequestMonitor
    {
        /// <summary>
        /// Maximum number of request entries to keep
        /// </summary>
        private const int MaxEntries = 100;

        // PERF-#325: Queue gives O(1) enqueue/dequeue, avoiding the O(n) shift of
        // List.RemoveAt(0) when the ring trims to MaxEntries on every record after cap.
        private static readonly Queue<RequestEntry> _entries = new Queue<RequestEntry>(MaxEntries);
        private static readonly object _lock = new object();

        // C-03: volatile ensures cross-thread reads see up-to-date values without a lock
        private static volatile int _totalRequests;
        private static volatile int _totalErrors;

        /// <summary>
        /// Event fired when a new request is recorded
        /// </summary>
        public static event Action<RequestEntry> OnRequestRecorded;

        /// <summary>
        /// All recorded entries (newest last)
        /// </summary>
        public static IReadOnlyList<RequestEntry> Entries
        {
            get
            {
                lock (_lock)
                {
                    // SEC-#425: return an immutable snapshot, not _entries.AsReadOnly() which
                    // exposes a live wrapper. A caller iterating the wrapper while another thread
                    // (or the diagnostics tab's Clear button) mutates _entries hits an
                    // IndexOutOfRangeException or sees half-updated state.
                    return _entries.ToArray();
                }
            }
        }

        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }

        public static int TotalRequests => _totalRequests;
        public static int TotalErrors => _totalErrors;

        /// <summary>
        /// Record a completed request
        /// </summary>
        public static void Record(string method, string toolName, double durationMs, bool success, string error = null)
        {
            var entry = new RequestEntry
            {
                Timestamp = DateTime.Now,
                Method = method,
                ToolName = toolName,
                DurationMs = durationMs,
                Success = success,
                Error = error
            };

            lock (_lock)
            {
                _entries.Enqueue(entry);
                _totalRequests++;
                if (!success) _totalErrors++;

                // PERF-#325: O(1) eviction
                while (_entries.Count > MaxEntries)
                {
                    _entries.Dequeue();
                }
            }

            OnRequestRecorded?.Invoke(entry);
        }

        /// <summary>
        /// Clear all entries and counters
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _totalRequests = 0;
                _totalErrors = 0;
            }
        }
    }

    /// <summary>
    /// Single recorded MCP request
    /// </summary>
    public struct RequestEntry
    {
        public DateTime Timestamp;
        public string Method;
        public string ToolName;
        public double DurationMs;
        public bool Success;
        public string Error;

        /// <summary>
        /// Display name: tool name if tools/call, otherwise method
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ToolName) ? ToolName : Method;
    }
}
