using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using McpUnity.Editor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Editor workflow tools: ExecuteMenuItem, RunTests, Undo
    /// </summary>
    public partial class McpUnityServer
    {
        // SECURITY: Allowlist of safe menu items to prevent arbitrary code execution
        private static readonly HashSet<string> AllowedMenuPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // File operations
            "File/Save",
            "File/Save Project",
            "File/New Scene",

            // Edit operations (safe)
            "Edit/Undo",
            "Edit/Redo",
            "Edit/Select All",
            "Edit/Deselect All",
            "Edit/Play",
            "Edit/Pause",
            "Edit/Step",

            // GameObject operations
            "GameObject/Create Empty",
            "GameObject/Create Empty Child",
            "GameObject/3D Object/Cube",
            "GameObject/3D Object/Sphere",
            "GameObject/3D Object/Capsule",
            "GameObject/3D Object/Cylinder",
            "GameObject/3D Object/Plane",
            "GameObject/3D Object/Quad",
            "GameObject/3D Object/Terrain",
            "GameObject/2D Object/Sprite",
            "GameObject/Light/Directional Light",
            "GameObject/Light/Point Light",
            "GameObject/Light/Spotlight",
            "GameObject/Camera",
            "GameObject/UI/Canvas",
            "GameObject/UI/Panel",
            "GameObject/UI/Button",
            "GameObject/UI/Text",
            "GameObject/UI/Image",
            "GameObject/UI/Raw Image",
            "GameObject/UI/Slider",
            "GameObject/UI/Toggle",
            "GameObject/UI/Input Field",

            // Component operations
            "Component/Physics/Rigidbody",
            "Component/Physics/Box Collider",
            "Component/Physics/Sphere Collider",
            "Component/Physics/Capsule Collider",
            "Component/Audio/Audio Source",
            "Component/Audio/Audio Listener",

            // Window operations (safe)
            "Window/General/Game",
            "Window/General/Scene",
            "Window/General/Inspector",
            "Window/General/Hierarchy",
            "Window/General/Project",
            "Window/General/Console"
        };

        static partial void RegisterEditorWorkflowTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_execute_menu_item",
                description = "Execute a Unity Editor menu item (only safe, allowlisted items)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["menuPath"] = new McpPropertySchema { type = "string", description = "Menu path (e.g., 'GameObject/Create Empty')" }
                    },
                    required = new List<string> { "menuPath" }
                }
            }, ExecuteMenuItem);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_run_tests",
                description = "Run Unity Test Framework tests (EditMode or PlayMode) and return results",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["testMode"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Test mode: 'EditMode' or 'PlayMode' (default: 'EditMode')",
                            @enum = new List<string> { "EditMode", "PlayMode" }
                        },
                        ["filter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional test name filter (substring match, e.g., 'MyTest' or 'MyNamespace')"
                        },
                        ["timeoutSeconds"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Timeout in seconds to wait for test results (default: 30)"
                        }
                    },
                    required = new List<string>()
                }
            }, RunTests);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_undo",
                description = "Perform undo or redo operations",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["steps"] = new McpPropertySchema { type = "integer", description = "Number of steps to undo/redo (default: 1)" },
                        ["redo"] = new McpPropertySchema { type = "boolean", description = "If true, perform redo instead of undo" }
                    },
                    required = new List<string>()
                }
            }, PerformUndo);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_refresh_and_compile",
                description = "Refresh AssetDatabase and request script recompilation. Use after modifying C# scripts to trigger Unity domain reload.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["recompile"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Request script recompilation (default: true). Set false to only refresh assets."
                        },
                        ["cleanBuild"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Force a clean rebuild clearing build cache (default: false). Only used when recompile is true."
                        }
                    },
                    required = new List<string>()
                }
            }, RefreshAndCompile);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_find_missing_references",
                description = "Scan the current scene for missing (null) object references on all components. Returns a list of affected GameObjects and fields.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["includeInactive"] = new McpPropertySchema { type = "boolean", description = "Include inactive GameObjects (default: true)" }
                    },
                    required = new List<string>()
                }
            }, FindMissingReferences);
        }

        private static McpToolResult ExecuteMenuItem(Dictionary<string, object> args)
        {
            var (menuPath, menuPathErr) = RequireArg(args, "menuPath");
            if (menuPathErr != null) return menuPathErr;

            // SECURITY: Validate menu path against allowlist
            if (!AllowedMenuPaths.Contains(menuPath))
            {
                McpDebug.LogWarning($"[MCP Unity] Blocked menu item execution (not in allowlist): {menuPath}");
                return McpToolResult.Error($"Menu item not allowed for security reasons: {menuPath}. Use allowed menu paths only.");
            }

            bool success = false;
            string error = null;

            try
            {
                success = EditorApplication.ExecuteMenuItem(menuPath);
                if (!success)
                {
                    error = $"Menu item not found: {menuPath}";
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (!string.IsNullOrEmpty(error))
            {
                return McpToolResult.Error(error);
            }

            return McpToolResult.Success($"Executed menu item: {menuPath}");
        }

        private static McpToolResult RunTests(Dictionary<string, object> args)
        {
            try
            {
                string testModeStr = ArgumentParser.GetString(args, "testMode", "EditMode");
                string filter = ArgumentParser.GetString(args, "filter", null);
                int timeoutSeconds = ArgumentParser.GetIntClamped(args, "timeoutSeconds", 30, 5, 120);

                TestMode testMode = testModeStr.Equals("PlayMode", StringComparison.OrdinalIgnoreCase)
                    ? TestMode.PlayMode
                    : TestMode.EditMode;

                // Results collected via callbacks
                var results = new List<object>();
                bool finished = false;
                string runError = null;

                var api = ScriptableObject.CreateInstance<TestRunnerApi>();

                var callbacks = new McpTestCallbacks(
                    onFinished: (report) =>
                    {
                        foreach (var result in report.Results)
                        {
                            results.Add(new
                            {
                                name = result.Name,
                                fullName = result.FullName,
                                status = result.TestStatus.ToString(),
                                duration = result.Duration,
                                message = result.Message,
                                stackTrace = result.StackTrace
                            });
                        }
                        finished = true;
                    },
                    onError: (error) =>
                    {
                        runError = error;
                        finished = true;
                    }
                );

                api.RegisterCallbacks(callbacks);

                var executeArgs = new ExecutionSettings(new Filter
                {
                    testMode = testMode,
                    testNames = !string.IsNullOrEmpty(filter) ? new[] { filter } : null
                });

                api.Execute(executeArgs);

                // Poll via EditorApplication.update to avoid blocking the main thread.
                // Thread.Sleep would freeze Unity's update loop and prevent test callbacks from firing.
                double startTime = EditorApplication.timeSinceStartup;
                bool timedOut = false;

                EditorApplication.CallbackFunction pollCallback = null;
                pollCallback = () =>
                {
                    if (finished || EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
                    {
                        EditorApplication.update -= pollCallback;
                        if (!finished) timedOut = true;
                        finished = true;
                    }
                };
                EditorApplication.update += pollCallback;

                // Spin-wait without sleeping — the update callback will set finished
                while (!finished)
                {
                    System.Threading.Thread.Yield();
                }

                if (timedOut)
                {
                    api.UnregisterCallbacks(callbacks);
                    UnityEngine.Object.DestroyImmediate(api);
                    return McpToolResult.Error($"Test run timed out after {timeoutSeconds}s. Use unity_get_console_logs to check for partial output.");
                }

                api.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(api);

                if (runError != null)
                    return McpToolResult.Error($"Test run failed: {runError}");

                int passed = 0, failed = 0, skipped = 0;
                foreach (var r in results)
                {
                    var status = ((dynamic)r).status as string ?? "";
                    if (status == "Passed") passed++;
                    else if (status == "Failed") failed++;
                    else skipped++;
                }

                return McpResponse.Success(new
                {
                    testMode = testModeStr,
                    filter = filter,
                    total = results.Count,
                    passed = passed,
                    failed = failed,
                    skipped = skipped,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to run tests: {ex.Message}");
            }
        }

        /// <summary>Callback handler for TestRunnerApi results</summary>
        private class McpTestCallbacks : ICallbacks
        {
            private readonly Action<ITestReportResult> _onFinished;
            private readonly Action<string> _onError;

            public McpTestCallbacks(Action<ITestReportResult> onFinished, Action<string> onError)
            {
                _onFinished = onFinished;
                _onError = onError;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                _onFinished?.Invoke(new McpTestReport(result));
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }
        }

        /// <summary>Wraps ITestResultAdaptor to produce a flat list of leaf results</summary>
        private class McpTestReport : ITestReportResult
        {
            public List<ITestResultAdaptor> Results { get; } = new List<ITestResultAdaptor>();

            public McpTestReport(ITestResultAdaptor root)
            {
                CollectLeaves(root);
            }

            private void CollectLeaves(ITestResultAdaptor node)
            {
                if (node.HasChildren)
                {
                    foreach (var child in node.Children)
                        CollectLeaves(child);
                }
                else
                {
                    Results.Add(node);
                }
            }
        }

        private interface ITestReportResult
        {
            List<ITestResultAdaptor> Results { get; }
        }

        private static McpToolResult PerformUndo(Dictionary<string, object> args)
        {
            int steps = ArgumentParser.GetIntClamped(args, "steps", 1, 1, 100);
            bool redo = ArgumentParser.GetBool(args, "redo", false);

            var actionsPerformed = new List<string>();

            for (int i = 0; i < steps; i++)
            {
                string currentAction = Undo.GetCurrentGroupName();
                if (string.IsNullOrEmpty(currentAction))
                {
                    currentAction = redo ? "(redo action)" : "(undo action)";
                }

                if (redo)
                {
                    Undo.PerformRedo();
                }
                else
                {
                    Undo.PerformUndo();
                }

                actionsPerformed.Add(currentAction);
            }

            return McpResponse.Success($"{(redo ? "Redo" : "Undo")} performed ({actionsPerformed.Count} steps)", new
            {
                operation = redo ? "Redo" : "Undo",
                stepsPerformed = actionsPerformed.Count,
                actions = actionsPerformed
            });
        }
        private static McpToolResult FindMissingReferences(Dictionary<string, object> args)
        {
            bool includeInactive = ArgumentParser.GetBool(args, "includeInactive", true);

            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var missing = new List<object>();

            foreach (var go in allObjects)
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null)
                    {
                        // Destroyed/missing component script
                        missing.Add(new
                        {
                            gameObject = GetGameObjectPath(go),
                            componentType = "(Missing Script)",
                            field = "(component itself)"
                        });
                        continue;
                    }

                    try
                    {
                        using (var so = new UnityEditor.SerializedObject(comp))
                        {
                            var prop = so.GetIterator();
                            while (prop.NextVisible(true))
                            {
                                if (prop.propertyType == UnityEditor.SerializedPropertyType.ObjectReference
                                    && prop.objectReferenceValue == null
                                    && prop.objectReferenceInstanceIDValue != 0)
                                {
                                    missing.Add(new
                                    {
                                        gameObject    = GetGameObjectPath(go),
                                        componentType = comp.GetType().Name,
                                        field         = prop.propertyPath
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception) { /* Skip components that throw during SerializedObject iteration */ }
                }
            }

            return McpResponse.Success(new
            {
                scannedObjects = allObjects.Length,
                missingCount   = missing.Count,
                missing        = missing,
                hint           = missing.Count == 0
                    ? "No missing references found."
                    : $"Found {missing.Count} missing reference(s). Fix them before using unity_set_reference."
            });
        }

        private static McpToolResult RefreshAndCompile(Dictionary<string, object> args)
        {
            bool recompile = ArgumentParser.GetBool(args, "recompile", true);
            bool wasCompiling = EditorApplication.isCompiling;

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.Default);

                if (recompile)
                {
                    bool cleanBuild = ArgumentParser.GetBool(args, "cleanBuild", false);
                    CompilationPipeline.RequestScriptCompilation(
                        cleanBuild ? RequestScriptCompilationOptions.CleanBuildCache : RequestScriptCompilationOptions.None
                    );
                }

                return McpResponse.Success(
                    recompile ? "Asset refresh and script recompilation requested" : "Asset refresh completed",
                    new
                    {
                        refreshed = true,
                        recompileRequested = recompile,
                        wasAlreadyCompiling = wasCompiling,
                        note = recompile ? "Compilation is async — check unity_get_editor_state for isCompiling status" : null
                    }
                );
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to refresh/compile: {ex.Message}");
            }
        }
    }
}
