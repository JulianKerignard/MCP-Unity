using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using McpUnity.Protocol;
using McpUnity.Editor;

namespace McpUnity.Server
{
    /// <summary>
    /// Registry for MCP tools with category-based dynamic loading.
    /// Tools are always registered internally, but only tools in enabled categories
    /// are returned by GetAllTools() (used by tools/list).
    /// ExecuteTool() works for ALL registered tools regardless of category state.
    /// </summary>
    public class McpToolRegistry
    {
        private readonly Dictionary<string, McpToolDefinition> _toolDefinitions
            = new Dictionary<string, McpToolDefinition>();

        private readonly Dictionary<string, Func<Dictionary<string, object>, McpToolResult>> _toolHandlers
            = new Dictionary<string, Func<Dictionary<string, object>, McpToolResult>>();

        // Category tracking
        private readonly Dictionary<string, string> _toolCategories
            = new Dictionary<string, string>(); // toolName -> category

        private readonly HashSet<string> _enabledCategories
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "core" };

        private string _currentCategory = "core";

        // Cached tool list — invalidated on register/unregister/category change
        private List<McpToolDefinition> _cachedToolList = null;

        /// <summary>
        /// Set the current category for subsequent RegisterTool calls.
        /// </summary>
        public void SetCurrentCategory(string category)
        {
            _currentCategory = category ?? "core";
        }

        /// <summary>
        /// Register a tool with its definition and handler.
        /// The tool is assigned to the current category set via SetCurrentCategory().
        /// </summary>
        public void RegisterTool(McpToolDefinition definition, Func<Dictionary<string, object>, McpToolResult> handler)
        {
            if (string.IsNullOrEmpty(definition?.name))
            {
                McpDebug.LogError("[MCP Registry] Cannot register tool without a name");
                return;
            }

            if (handler == null)
            {
                McpDebug.LogError($"[MCP Registry] Cannot register tool '{definition.name}' without a handler");
                return;
            }

            _toolDefinitions[definition.name] = definition;
            _toolHandlers[definition.name] = handler;
            _toolCategories[definition.name] = _currentCategory;
            _cachedToolList = null; // invalidate cache

            McpDebug.Log($"[MCP Registry] Registered tool: {definition.name} (category: {_currentCategory})");
        }

        /// <summary>
        /// Unregister a tool by name
        /// </summary>
        public bool UnregisterTool(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            bool removed = _toolDefinitions.Remove(name);
            _toolHandlers.Remove(name);
            _toolCategories.Remove(name);

            if (removed)
            {
                _cachedToolList = null;
                McpDebug.Log($"[MCP Registry] Unregistered tool: {name}");
            }

            return removed;
        }

        /// <summary>
        /// Check if a tool exists
        /// </summary>
        public bool HasTool(string name)
        {
            return !string.IsNullOrEmpty(name) && _toolDefinitions.ContainsKey(name);
        }

        /// <summary>
        /// Get tool definitions for enabled categories only.
        /// This is what tools/list returns to the MCP client.
        /// </summary>
        public List<McpToolDefinition> GetAllTools()
        {
            if (_cachedToolList == null)
            {
                _cachedToolList = _toolDefinitions
                    .Where(kvp => _enabledCategories.Contains(
                        _toolCategories.TryGetValue(kvp.Key, out var cat) ? cat : "core"))
                    .Select(kvp => kvp.Value)
                    .ToList();
            }
            return _cachedToolList;
        }

        /// <summary>
        /// Get a specific tool definition
        /// </summary>
        public McpToolDefinition GetTool(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _toolDefinitions.TryGetValue(name, out var definition);
            return definition;
        }

        /// <summary>
        /// Execute a tool by name. Works for ALL registered tools regardless of category state.
        /// </summary>
        public McpToolResult ExecuteTool(string name, Dictionary<string, object> arguments)
        {
            if (string.IsNullOrEmpty(name))
            {
                return McpToolResult.Error("Tool name is required");
            }

            if (!_toolHandlers.TryGetValue(name, out var handler))
            {
                // If the tool exists but its category is disabled, give a helpful error
                if (_toolDefinitions.ContainsKey(name))
                {
                    var cat = _toolCategories.TryGetValue(name, out var c) ? c : "unknown";
                    return McpToolResult.Error(
                        $"Tool '{name}' exists but its category '{cat}' is not enabled. " +
                        $"Call unity_enable_tool_category with category='{cat}' first.");
                }
                return McpToolResult.Error($"Tool not found: {name}");
            }

            // Validate required parameters
            if (_toolDefinitions.TryGetValue(name, out var definition))
            {
                var validationError = ValidateArguments(definition, arguments);
                if (validationError != null)
                {
                    return McpToolResult.Error(validationError);
                }
            }

            try
            {
                McpDebug.Log($"[MCP Registry] Executing tool: {name}");
                return handler(arguments ?? new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                McpDebug.LogError($"[MCP Registry] Tool execution error for '{name}': {ex.Message}\n{ex.StackTrace}");
                return McpToolResult.Error($"Tool execution failed: {ex.Message}");
            }
        }

        private string ValidateArguments(McpToolDefinition definition, Dictionary<string, object> arguments)
        {
            if (definition.inputSchema?.required == null) return null;

            arguments = arguments ?? new Dictionary<string, object>();

            foreach (var required in definition.inputSchema.required)
            {
                if (!arguments.ContainsKey(required) || arguments[required] == null)
                {
                    return $"Missing required argument: {required}";
                }
            }

            return null;
        }

        #region Category Management

        /// <summary>
        /// Enable a category so its tools appear in tools/list.
        /// Returns true if the category was newly enabled (tools/list changed).
        /// </summary>
        public bool EnableCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;

            // Check category exists (case-insensitive to match _enabledCategories comparer)
            bool exists = false;
            foreach (var cat in _toolCategories.Values)
            {
                if (string.Equals(cat, category, StringComparison.OrdinalIgnoreCase))
                {
                    category = cat; // normalize to registered casing
                    exists = true;
                    break;
                }
            }
            if (!exists) return false;

            if (_enabledCategories.Add(category))
            {
                _cachedToolList = null;
                McpDebug.Log($"[MCP Registry] Enabled category: {category}");
                return true;
            }
            return false; // already enabled
        }

        /// <summary>
        /// Disable a category. "core" cannot be disabled.
        /// Returns true if the category was disabled (tools/list changed).
        /// </summary>
        public bool DisableCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return false;
            if (category.Equals("core", StringComparison.OrdinalIgnoreCase)) return false;

            if (_enabledCategories.Remove(category))
            {
                _cachedToolList = null;
                McpDebug.Log($"[MCP Registry] Disabled category: {category}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a category is enabled
        /// </summary>
        public bool IsCategoryEnabled(string category)
        {
            return _enabledCategories.Contains(category);
        }

        /// <summary>
        /// Get info about all categories: name, tool count, enabled status, tool names.
        /// </summary>
        public List<CategoryInfo> GetCategoryInfo()
        {
            var grouped = _toolCategories
                .GroupBy(kvp => kvp.Value)
                .OrderBy(g => g.Key == "core" ? "" : g.Key); // core first

            var result = new List<CategoryInfo>();
            foreach (var group in grouped)
            {
                result.Add(new CategoryInfo
                {
                    name = group.Key,
                    toolCount = group.Count(),
                    enabled = _enabledCategories.Contains(group.Key),
                    tools = group.Select(kvp => kvp.Key).OrderBy(n => n).ToList()
                });
            }
            return result;
        }

        /// <summary>
        /// Get the set of enabled category names
        /// </summary>
        public HashSet<string> GetEnabledCategories()
        {
            return new HashSet<string>(_enabledCategories);
        }

        #endregion

        /// <summary>
        /// Get the total count of registered tools (all categories)
        /// </summary>
        public int Count => _toolDefinitions.Count;

        /// <summary>
        /// Get the count of tools visible in tools/list (enabled categories only)
        /// </summary>
        public int VisibleCount => GetAllTools().Count;

        /// <summary>
        /// Clear all registered tools
        /// </summary>
        public void Clear()
        {
            _toolDefinitions.Clear();
            _toolHandlers.Clear();
            _toolCategories.Clear();
            _cachedToolList = null;
            _enabledCategories.Clear();
            _enabledCategories.Add("core");
            _currentCategory = "core";
            McpDebug.Log("[MCP Registry] Cleared all tools");
        }

        /// <summary>
        /// Get debug listing of all tools
        /// </summary>
        public string GetDebugList()
        {
            var lines = new List<string> { $"Registered MCP Tools ({Count} total, {VisibleCount} visible):", "" };
            foreach (var tool in _toolDefinitions.Values.OrderBy(t => t.name))
            {
                var cat = _toolCategories.TryGetValue(tool.name, out var c) ? c : "?";
                var enabled = _enabledCategories.Contains(cat) ? "+" : "-";
                lines.Add($"  [{enabled}] {tool.name} ({cat})");
                lines.Add($"      {tool.description}");
                if (tool.inputSchema?.required?.Count > 0)
                {
                    lines.Add($"      Required: {string.Join(", ", tool.inputSchema.required)}");
                }
                lines.Add("");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Info about a tool category
    /// </summary>
    public class CategoryInfo
    {
        public string name;
        public int toolCount;
        public bool enabled;
        public List<string> tools;
    }
}
