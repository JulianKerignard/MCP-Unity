using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace McpUnity.Server
{
    /// <summary>
    /// Auto-invalidates the Unity-side memory cache (assets/scenes/hierarchy)
    /// when corresponding Editor events fire. Replaces the 5-minute TTL window
    /// with event-driven freshness — users no longer need to call memory_refresh
    /// manually after most edits.
    ///
    /// The invalidated set is in-memory so we avoid disk I/O on every event.
    /// IsSectionStale() consults it via TryConsume().
    /// </summary>
    [InitializeOnLoad]
    public static class MemoryAutoInvalidator
    {
        private static readonly HashSet<string> _invalidated = new HashSet<string>();
        private static readonly object _lock = new object();

        static MemoryAutoInvalidator()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => MarkStale("scenes", "hierarchy");
            EditorSceneManager.sceneOpened += (_, _) => MarkStale("scenes", "hierarchy");
        }

        private static void OnHierarchyChanged()
        {
            MarkStale("hierarchy");
        }

        /// <summary>
        /// Mark one or more sections as stale. Cheap (no I/O).
        /// </summary>
        public static void MarkStale(params string[] sections)
        {
            lock (_lock)
            {
                foreach (var s in sections) _invalidated.Add(s);
            }
        }

        /// <summary>
        /// Returns true if the section was marked stale, and clears the flag.
        /// </summary>
        public static bool TryConsume(string section)
        {
            lock (_lock)
            {
                return _invalidated.Remove(section);
            }
        }
    }

    /// <summary>
    /// AssetPostprocessor hook — every asset import/move/delete invalidates the
    /// assets section. Runs in its own type because AssetPostprocessor requires
    /// it.
    /// </summary>
    internal class MemoryAssetsInvalidator : AssetPostprocessor
    {
        // Static method invoked by Unity after any asset DB change.
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && movedAssets.Length == 0)
                return;
            MemoryAutoInvalidator.MarkStale("assets");
        }
    }
}
