using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Server;

namespace McpUnity.Tests
{
    /// <summary>
    /// Extended Edit Mode tests for McpToolRegistry — covering methods missing from the original test file.
    /// </summary>
    public class McpToolRegistryExtendedTests
    {
        private McpToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new McpToolRegistry();
        }

        private McpToolDefinition MakeTool(string name, string category = "core")
        {
            return new McpToolDefinition(
                name,
                $"Test tool: {name}",
                new Dictionary<string, McpPropertySchema>(),
                new List<string>(),
                category
            );
        }

        // ── GetTool ─────────────────────────────────────────────────────────

        [Test]
        public void GetTool_RegisteredTool_ReturnsDefinition()
        {
            var def = MakeTool("unity_test_get");
            _registry.RegisterTool(def, args => McpToolResult.Error("stub"));
            var result = _registry.GetTool("unity_test_get");
            Assert.IsNotNull(result);
            Assert.AreEqual("unity_test_get", result.name);
        }

        [Test]
        public void GetTool_UnknownTool_ReturnsNull()
        {
            var result = _registry.GetTool("unity_nonexistent");
            Assert.IsNull(result);
        }

        // ── GetEnabledCategories ────────────────────────────────────────────

        [Test]
        public void GetEnabledCategories_CoreAlwaysPresent()
        {
            var enabled = _registry.GetEnabledCategories();
            Assert.IsTrue(enabled.Contains("core"));
        }

        [Test]
        public void GetEnabledCategories_AfterEnabling_ContainsNew()
        {
            var def = MakeTool("unity_test_cat", "terrain");
            _registry.RegisterTool(def, args => McpToolResult.Error("stub"));

            _registry.EnableCategory("terrain");
            var enabled = _registry.GetEnabledCategories();
            Assert.IsTrue(enabled.Contains("terrain"));
        }

        // ── GetCategoryInfo ─────────────────────────────────────────────────

        [Test]
        public void GetCategoryInfo_ReturnsRegisteredCategories()
        {
            var def1 = MakeTool("unity_core_a", "core");
            var def2 = MakeTool("unity_terrain_a", "terrain");
            _registry.RegisterTool(def1, args => McpToolResult.Error("stub"));
            _registry.RegisterTool(def2, args => McpToolResult.Error("stub"));

            var info = _registry.GetCategoryInfo();
            Assert.IsNotNull(info);
            // Should contain at least core and terrain
            bool hasCore = false;
            bool hasTerrain = false;
            foreach (var cat in info)
            {
                if (cat.name == "core") hasCore = true;
                if (cat.name == "terrain") hasTerrain = true;
            }
            Assert.IsTrue(hasCore, "Should have 'core' category");
            Assert.IsTrue(hasTerrain, "Should have 'terrain' category");
        }

        // ── Multiple required fields ────────────────────────────────────────

        [Test]
        public void ExecuteTool_MultipleRequiredFields_MissingSome_ReturnsError()
        {
            var props = new Dictionary<string, McpPropertySchema>
            {
                ["name"] = new McpPropertySchema { type = "string", description = "Name" },
                ["path"] = new McpPropertySchema { type = "string", description = "Path" },
                ["value"] = new McpPropertySchema { type = "number", description = "Value" }
            };
            var required = new List<string> { "name", "path", "value" };
            var def = new McpToolDefinition("unity_multi_req", "Test", props, required);
            _registry.RegisterTool(def, args => McpToolResult.Error("should not reach"));

            // Only provide "name", missing "path" and "value"
            var args = new Dictionary<string, object> { ["name"] = "test" };
            var result = _registry.ExecuteTool("unity_multi_req", args);

            // Should return error for missing required fields
            Assert.IsNotNull(result);
            Assert.IsTrue(result.isError);
        }

        // ── Cached tool list invalidation ───────────────────────────────────

        [Test]
        public void GetAllTools_AfterNewRegistration_ReturnsUpdatedList()
        {
            var def1 = MakeTool("unity_first", "core");
            _registry.RegisterTool(def1, args => McpToolResult.Error("stub"));

            var list1 = _registry.GetAllTools();
            int count1 = 0;
            foreach (var _ in list1) count1++;

            // Register another tool
            var def2 = MakeTool("unity_second", "core");
            _registry.RegisterTool(def2, args => McpToolResult.Error("stub"));

            var list2 = _registry.GetAllTools();
            int count2 = 0;
            foreach (var _ in list2) count2++;

            Assert.AreEqual(count1 + 1, count2);
        }

        // ── DisableCategory ─────────────────────────────────────────────────

        [Test]
        public void DisableCategory_NonCore_RemovesFromEnabled()
        {
            var def = MakeTool("unity_anim_a", "animator");
            _registry.RegisterTool(def, args => McpToolResult.Error("stub"));

            _registry.EnableCategory("animator");
            Assert.IsTrue(_registry.GetEnabledCategories().Contains("animator"));

            _registry.DisableCategory("animator");
            Assert.IsFalse(_registry.GetEnabledCategories().Contains("animator"));
        }

        [Test]
        public void DisableCategory_Core_StaysEnabled()
        {
            _registry.DisableCategory("core");
            Assert.IsTrue(_registry.GetEnabledCategories().Contains("core"),
                "Core category should not be disableable");
        }

        // ── VisibleCount ────────────────────────────────────────────────────

        [Test]
        public void VisibleCount_OnlyCoreEnabled_CountsCoreOnly()
        {
            var def1 = MakeTool("unity_core_vis", "core");
            var def2 = MakeTool("unity_terrain_vis", "terrain");
            _registry.RegisterTool(def1, args => McpToolResult.Error("stub"));
            _registry.RegisterTool(def2, args => McpToolResult.Error("stub"));

            int count = _registry.VisibleCount;
            // terrain is not enabled, so only core tools should be visible
            Assert.AreEqual(1, count);
        }

        [Test]
        public void VisibleCount_AfterEnablingCategory_IncludesNew()
        {
            var def1 = MakeTool("unity_core_vis2", "core");
            var def2 = MakeTool("unity_terrain_vis2", "terrain");
            _registry.RegisterTool(def1, args => McpToolResult.Error("stub"));
            _registry.RegisterTool(def2, args => McpToolResult.Error("stub"));

            _registry.EnableCategory("terrain");
            int count = _registry.VisibleCount;
            Assert.AreEqual(2, count);
        }

        // ── Clear ───────────────────────────────────────────────────────────

        [Test]
        public void Clear_ResetsEverything()
        {
            var def = MakeTool("unity_clear_test", "core");
            _registry.RegisterTool(def, args => McpToolResult.Error("stub"));

            Assert.IsTrue(_registry.HasTool("unity_clear_test"));

            _registry.Clear();

            Assert.IsFalse(_registry.HasTool("unity_clear_test"));
            Assert.AreEqual(0, _registry.VisibleCount);
        }
    }
}
