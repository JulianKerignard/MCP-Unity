using UnityEditor;

namespace McpUnity.Helpers
{
    /// <summary>
    /// Shared AssetDatabase helpers — consolidates patterns previously duplicated across
    /// PhysicsTools, AudioTools, MaterialTools, ScriptableObjectTools, etc. (SEC-#434).
    /// </summary>
    public static class AssetDatabaseHelpers
    {
        /// <summary>
        /// Recursively ensure every segment of an Assets-relative folder path exists,
        /// creating intermediate folders with AssetDatabase.CreateFolder as needed.
        /// No-ops if the folder already exists or the path is null/empty.
        /// </summary>
        public static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            // Normalize to forward slashes (Unity paths) and drop a trailing slash.
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');

            var parts = folderPath.Split('/');
            if (parts.Length == 0) return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
