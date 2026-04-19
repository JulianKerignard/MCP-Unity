using System.Collections.Generic;
using NUnit.Framework;
using McpUnity.Server;
using McpUnity.Protocol;

namespace McpUnity.Tests
{
    /// <summary>
    /// Edit Mode tests for McpToolRegistry.
    /// Tests tool registration, category management, execution dispatch and validation.
    /// </summary>
    public class McpToolRegistryTests
    {
        private McpToolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new McpToolRegistry();
        }

        // ── Registration ────────────────────────────────────────────────────

        [Test]
        public void RegisterTool_ValidTool_IsAdded()
        {
            _registry.RegisterTool(MakeDef("unity_test_tool"), EchoHandler);
            Assert.AreEqual(1, _registry.Count);
        }

        [Test]
        public void RegisterTool_NullDefinition_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _registry.RegisterTool(null, EchoHandler));
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void RegisterTool_NullHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _registry.RegisterTool(MakeDef("unity_test"), null));
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void RegisterTool_DuplicateName_OverwritesPrevious()
        {
            _registry.RegisterTool(MakeDef("unity_dupe"), EchoHandler);
            _registry.RegisterTool(MakeDef("unity_dupe"), ErrorHandler);
            Assert.AreEqual(1, _registry.Count, "Duplicate should overwrite, not add");

            // The second handler (ErrorHandler) should now be active
            var result = _registry.ExecuteTool("unity_dupe", new Dictionary<string, object>());
            Assert.IsTrue(result.isError);
        }

        [Test]
        public void HasTool_RegisteredTool_ReturnsTrue()
        {
            _registry.RegisterTool(MakeDef("unity_exists"), EchoHandler);
            Assert.IsTrue(_registry.HasTool("unity_exists"));
        }

        [Test]
        public void HasTool_UnknownTool_ReturnsFalse()
        {
            Assert.IsFalse(_registry.HasTool("unity_does_not_exist"));
        }

        [Test]
        public void UnregisterTool_ExistingTool_RemovesIt()
        {
            _registry.RegisterTool(MakeDef("unity_to_remove"), EchoHandler);
            Assert.IsTrue(_registry.UnregisterTool("unity_to_remove"));
            Assert.IsFalse(_registry.HasTool("unity_to_remove"));
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void UnregisterTool_UnknownTool_ReturnsFalse()
        {
            Assert.IsFalse(_registry.UnregisterTool("unity_ghost"));
        }

        // ── Execution ────────────────────────────────────────────────────────

        [Test]
        public void ExecuteTool_RegisteredTool_CallsHandler()
        {
            bool called = false;
            _registry.RegisterTool(MakeDef("unity_call_me"), args =>
            {
                called = true;
                return McpToolResult.Success("ok");
            });

            _registry.ExecuteTool("unity_call_me", new Dictionary<string, object>());
            Assert.IsTrue(called);
        }

        [Test]
        public void ExecuteTool_UnknownTool_ReturnsError()
        {
            var result = _registry.ExecuteTool("unity_unknown", new Dictionary<string, object>());
            Assert.IsTrue(result.isError);
        }

        [Test]
        public void ExecuteTool_NullName_ReturnsError()
        {
            var result = _registry.ExecuteTool(null, new Dictionary<string, object>());
            Assert.IsTrue(result.isError);
        }

        [Test]
        public void ExecuteTool_NullArgs_PassesEmptyDict()
        {
            Dictionary<string, object> receivedArgs = null;
            _registry.RegisterTool(MakeDef("unity_capture_args"), args =>
            {
                receivedArgs = args;
                return McpToolResult.Success("ok");
            });

            _registry.ExecuteTool("unity_capture_args", null);
            Assert.IsNotNull(receivedArgs);
        }

        [Test]
        public void ExecuteTool_HandlerThrows_ReturnsError()
        {
            _registry.RegisterTool(MakeDef("unity_throws"), args =>
            {
                throw new System.Exception("Simulated handler crash");
            });

            var result = _registry.ExecuteTool("unity_throws", new Dictionary<string, object>());
            Assert.IsTrue(result.isError);
        }

        // ── Required field validation ──────────────────────────────────────

        [Test]
        public void ExecuteTool_MissingRequiredArg_ReturnsError()
        {
            var def = new McpToolDefinition
            {
                name = "unity_needs_path",
                description = "Requires path",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema { type = "string" }
                    },
                    required = new System.Collections.Generic.List<string> { "path" }
                }
            };
            _registry.RegisterTool(def, EchoHandler);

            var result = _registry.ExecuteTool("unity_needs_path", new Dictionary<string, object>());
            Assert.IsTrue(result.isError);
        }

        [Test]
        public void ExecuteTool_RequiredArgPresent_Succeeds()
        {
            var def = new McpToolDefinition
            {
                name = "unity_needs_name",
                description = "Requires name",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["name"] = new McpPropertySchema { type = "string" }
                    },
                    required = new System.Collections.Generic.List<string> { "name" }
                }
            };
            _registry.RegisterTool(def, EchoHandler);

            var args = new Dictionary<string, object> { ["name"] = "Player" };
            var result = _registry.ExecuteTool("unity_needs_name", args);
            Assert.IsFalse(result.isError);
        }

        // ── Category management ───────────────────────────────────────────

        [Test]
        public void CoreCategory_EnabledByDefault()
        {
            Assert.IsTrue(_registry.IsCategoryEnabled("core"));
        }

        [Test]
        public void EnableCategory_NewCategory_BecomesEnabled()
        {
            _registry.SetCurrentCategory("physics");
            _registry.RegisterTool(MakeDef("unity_raycast"), EchoHandler);

            bool enabled = _registry.EnableCategory("physics");
            Assert.IsTrue(enabled);
            Assert.IsTrue(_registry.IsCategoryEnabled("physics"));
        }

        [Test]
        public void EnableCategory_AlreadyEnabled_ReturnsFalse()
        {
            Assert.IsFalse(_registry.EnableCategory("core")); // core is default, already enabled
        }

        [Test]
        public void EnableCategory_CaseInsensitive_Succeeds()
        {
            _registry.SetCurrentCategory("audio");
            _registry.RegisterTool(MakeDef("unity_setup_audio_source"), EchoHandler);

            bool enabled = _registry.EnableCategory("AUDIO");
            Assert.IsTrue(enabled);
            Assert.IsTrue(_registry.IsCategoryEnabled("AUDIO"));
        }

        [Test]
        public void EnableCategory_NonExistent_ReturnsFalse()
        {
            bool enabled = _registry.EnableCategory("does_not_exist");
            Assert.IsFalse(enabled);
        }

        [Test]
        public void DisableCategory_ExistingNonCore_Succeeds()
        {
            _registry.SetCurrentCategory("material");
            _registry.RegisterTool(MakeDef("unity_get_material"), EchoHandler);
            _registry.EnableCategory("material");

            bool disabled = _registry.DisableCategory("material");
            Assert.IsTrue(disabled);
            Assert.IsFalse(_registry.IsCategoryEnabled("material"));
        }

        [Test]
        public void DisableCategory_Core_ReturnsFalse()
        {
            bool disabled = _registry.DisableCategory("core");
            Assert.IsFalse(disabled, "Core category must never be disabled");
            Assert.IsTrue(_registry.IsCategoryEnabled("core"));
        }

        [Test]
        public void GetAllTools_OnlyReturnsEnabledCategories()
        {
            // SEC-#438: production defaults enable every known category — explicitly disable
            // 'asset' here so the assertion that the tool is hidden actually exercises the gate.
            _registry.SetCurrentCategory("core");
            _registry.RegisterTool(MakeDef("unity_core_tool"), EchoHandler);

            _registry.SetCurrentCategory("asset");
            _registry.RegisterTool(MakeDef("unity_search_assets"), EchoHandler);
            _registry.DisableCategory("asset");

            var visibleTools = _registry.GetAllTools();
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var t in visibleTools) names.Add(t.name);

            Assert.IsTrue(names.Contains("unity_core_tool"), "Core tool must be visible");
            Assert.IsFalse(names.Contains("unity_search_assets"), "Asset tool must be hidden when category disabled");
        }

        [Test]
        public void ExecuteTool_DisabledCategoryTool_StillExecutes()
        {
            // SEC-#438: production behavior is documented as "ExecuteTool works for ALL registered
            // tools regardless of category state" — only tools/list filters by category. The
            // previous version of this test asserted a helpful-error path that is dead code
            // (handlers and definitions are always set/removed together), so it was testing
            // incorrect behavior. Replace with a test of the documented contract.
            _registry.SetCurrentCategory("terrain");
            _registry.RegisterTool(MakeDef("unity_create_terrain"), EchoHandler);
            _registry.DisableCategory("terrain");

            var result = _registry.ExecuteTool("unity_create_terrain", new Dictionary<string, object>());
            Assert.IsFalse(result.isError, "Disabled category must NOT block execution — only visibility");
        }

        [Test]
        public void Clear_ResetsAllState()
        {
            _registry.SetCurrentCategory("ui");
            _registry.RegisterTool(MakeDef("unity_create_canvas"), EchoHandler);
            _registry.EnableCategory("ui");

            _registry.Clear();

            Assert.AreEqual(0, _registry.Count);
            Assert.IsTrue(_registry.IsCategoryEnabled("core"), "Core should be re-enabled after clear");
            Assert.IsFalse(_registry.IsCategoryEnabled("ui"), "Other categories reset after clear");
        }

        [Test]
        public void VisibleCount_ReflectsEnabledToolsOnly()
        {
            _registry.SetCurrentCategory("core");
            _registry.RegisterTool(MakeDef("unity_core_1"), EchoHandler);
            _registry.RegisterTool(MakeDef("unity_core_2"), EchoHandler);

            _registry.SetCurrentCategory("audio");
            _registry.RegisterTool(MakeDef("unity_audio_1"), EchoHandler);

            Assert.AreEqual(2, _registry.VisibleCount);
            Assert.AreEqual(3, _registry.Count);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static McpToolDefinition MakeDef(string name) =>
            new McpToolDefinition
            {
                name = name,
                description = $"Test tool: {name}",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new System.Collections.Generic.List<string>()
                }
            };

        private static McpToolResult EchoHandler(Dictionary<string, object> args) =>
            McpToolResult.Success("ok");

        private static McpToolResult ErrorHandler(Dictionary<string, object> args) =>
            McpToolResult.Error("deliberate error");
    }
}
