using System.Threading;

namespace McpUnity.Editor
{
    /// <summary>
    /// Utility class for conditional logging that routes through McpServerLogger.
    /// All logs appear in both the Unity console AND the Diagnostics tab.
    /// Thread-safe: can be called from any thread (WebSocket, background, etc.).
    /// </summary>
    public static class McpDebug
    {
        // Unity main thread ID, captured at static init (domain reload runs on main thread)
        private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// Log info message
        /// </summary>
        public static void Log(string message)
        {
            if (IsMainThread)
                McpServerLogger.Instance.Info(message);
            else
                McpServerLogger.Instance.LogThreadSafe(LogLevel.Info, message);
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            if (IsMainThread)
                McpServerLogger.Instance.Warning(message);
            else
                McpServerLogger.Instance.LogThreadSafe(LogLevel.Warning, message);
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public static void LogError(string message)
        {
            if (IsMainThread)
                McpServerLogger.Instance.Error(message);
            else
                McpServerLogger.Instance.LogThreadSafe(LogLevel.Error, message);
        }

        /// <summary>
        /// Log with format
        /// </summary>
        public static void LogFormat(string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (IsMainThread)
                McpServerLogger.Instance.Info(message);
            else
                McpServerLogger.Instance.LogThreadSafe(LogLevel.Info, message);
        }

        private static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;
    }
}
