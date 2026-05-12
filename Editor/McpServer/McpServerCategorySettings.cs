using System.Collections.Generic;
using UnityEditor;
using McpUnity.Server;

namespace McpUnity.Editor
{
    /// <summary>
    /// Persistent enable/disable state for MCP server tool categories.
    /// Controls which categories are visible in tools/list (i.e. what Claude/Cursor see).
    /// State is stored in EditorPrefs per-machine and applied to the running registry.
    /// </summary>
    public static class McpServerCategorySettings
    {
        private const string PrefPrefix = "McpUnity_ServerCat_";

        // Default: every category enabled (matches the registry's default state).
        private const bool DefaultEnabled = true;

        // "core" is always enabled — never persisted, never togglable.
        public static bool IsAlwaysOn(string category)
        {
            return string.Equals(category, "core", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCategoryEnabled(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;
            if (IsAlwaysOn(category)) return true;
            return EditorPrefs.GetBool(PrefPrefix + category, DefaultEnabled);
        }

        /// <summary>
        /// Toggle a category and apply the change to the running registry.
        /// Fires tools/list_changed if connected clients are present.
        /// Returns true if the state actually changed.
        /// </summary>
        public static bool SetCategoryEnabled(string category, bool enabled)
        {
            if (string.IsNullOrEmpty(category) || IsAlwaysOn(category)) return false;
            if (IsCategoryEnabled(category) == enabled) return false;

            EditorPrefs.SetBool(PrefPrefix + category, enabled);

            var reg = McpUnityServer.ToolRegistry;
            bool changed = false;
            if (reg != null)
            {
                changed = enabled ? reg.EnableCategory(category) : reg.DisableCategory(category);
            }

            if (changed && McpUnityServer.IsRunning && McpUnityServer.ConnectedClientCount > 0)
            {
                McpUnityServer.NotifyToolsListChanged();
            }
            return true;
        }

        /// <summary>
        /// Set every non-core category to <paramref name="enabled"/>.
        /// </summary>
        public static void SetAll(bool enabled)
        {
            var reg = McpUnityServer.ToolRegistry;
            if (reg == null) return;

            bool anyChanged = false;
            foreach (var info in reg.GetCategoryInfo())
            {
                if (IsAlwaysOn(info.name)) continue;
                EditorPrefs.SetBool(PrefPrefix + info.name, enabled);
                bool changed = enabled ? reg.EnableCategory(info.name) : reg.DisableCategory(info.name);
                anyChanged |= changed;
            }

            if (anyChanged && McpUnityServer.IsRunning && McpUnityServer.ConnectedClientCount > 0)
            {
                McpUnityServer.NotifyToolsListChanged();
            }
        }

        /// <summary>
        /// Apply persisted state to a freshly-built registry. Called once after RegisterDefaultTools.
        /// Does not fire notifications — connected clients have not yet received tools/list.
        /// </summary>
        public static void ApplyPersistedState(McpToolRegistry registry)
        {
            if (registry == null) return;
            foreach (var info in registry.GetCategoryInfo())
            {
                if (IsAlwaysOn(info.name)) continue;
                if (IsCategoryEnabled(info.name))
                    registry.EnableCategory(info.name);
                else
                    registry.DisableCategory(info.name);
            }
        }

        /// <summary>
        /// All server category names (read from the running registry, sorted, core first).
        /// </summary>
        public static List<string> GetAllCategoryNames()
        {
            var result = new List<string>();
            var reg = McpUnityServer.ToolRegistry;
            if (reg == null) return result;
            foreach (var info in reg.GetCategoryInfo())
                result.Add(info.name);
            return result;
        }
    }
}
