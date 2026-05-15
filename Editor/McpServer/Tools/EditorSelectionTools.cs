using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using McpUnity.Helpers;
using McpUnity.Protocol;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpUnity.Server
{
    /// <summary>
    /// Editor selection and console tools: GetSelection, SetSelection, GetConsoleLogs, ClearConsole
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterEditorSelectionTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_editor_state",
                description = "Get the current Unity Editor state (play mode, compiling, selection, etc.)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetEditorState);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_selection",
                description = "Get the current selection in the Unity Editor",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["includeAssets"] = new McpPropertySchema { type = "boolean", description = "Include selected assets" }
                    },
                    required = new List<string>()
                }
            }, GetSelection);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_selection",
                description = "Set the current selection in the Unity Editor",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPaths"] = new McpPropertySchema { type = "array", description = "Array of GameObject paths to select" },
                        ["assetPaths"] = new McpPropertySchema { type = "array", description = "Array of asset paths to select" },
                        ["clear"] = new McpPropertySchema { type = "boolean", description = "If true, clear the current selection" }
                    },
                    required = new List<string>()
                }
            }, SetSelection);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_console_logs",
                description = "Get console logs from the Unity Editor with filtering options",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["logType"] = new McpPropertySchema { type = "string", description = "Filter: All, Error, Warning, Log, Exception, Assert" },
                        ["count"] = new McpPropertySchema { type = "integer", description = "Number of logs to retrieve" },
                        ["includeStackTrace"] = new McpPropertySchema { type = "boolean", description = "Include stack traces" }
                    },
                    required = new List<string>()
                }
            }, GetConsoleLogs);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_focus_gameobject",
                description = "Select a GameObject and frame it in the Scene view (equivalent to pressing F). Use after creating or modifying objects.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema { type = "string", description = "Path or name of the GameObject to focus" }
                    },
                    required = new List<string> { "path" }
                }
            }, FocusGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_project_overview",
                description = "Get a compact overview of the project: render pipeline, asset counts, build scenes, packages, Unity version. Call at the start of a session to orient yourself.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetProjectOverview);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_clear_console",
                description = "Clear the Unity console",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, ClearConsole);
        }

        private static string[] GetSelectedObjectNames()
        {
            var names = new string[Selection.objects.Length];
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                names[i] = Selection.objects[i].name;
            }
            return names;
        }

        private static string[] ConvertToStringArray(object obj)
        {
            if (obj is string[] strArray)
            {
                return strArray;
            }

            if (obj is IEnumerable<object> enumerable)
            {
                return enumerable.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
            }

            if (obj is System.Collections.IList list)
            {
                var result = new List<string>();
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        result.Add(item.ToString());
                    }
                }
                return result.ToArray();
            }

            return new string[0];
        }

        private static McpToolResult GetEditorState(Dictionary<string, object> args)
        {
            return McpResponse.Success("Editor state", new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                applicationPath = EditorApplication.applicationPath,
                applicationVersion = Application.unityVersion,
                currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                selectedObjectCount = Selection.objects.Length,
                selectedObjectNames = GetSelectedObjectNames()
            });
        }

        private static McpToolResult GetSelection(Dictionary<string, object> args)
        {
            bool includeAssets = ArgumentParser.GetBool(args, "includeAssets", false);

            var result = new Dictionary<string, object>();

            if (Selection.activeGameObject != null)
            {
                result["activeObject"] = new Dictionary<string, object>
                {
                    ["name"] = Selection.activeGameObject.name,
                    ["path"] = GameObjectHelpers.GetGameObjectPath(Selection.activeGameObject)
                };
            }

            var gameObjects = new List<Dictionary<string, object>>();
            foreach (var go in Selection.gameObjects)
            {
                gameObjects.Add(new Dictionary<string, object>
                {
                    ["name"] = go.name,
                    ["path"] = GameObjectHelpers.GetGameObjectPath(go)
                });
            }
            result["gameObjects"] = gameObjects;

            if (includeAssets)
            {
                var assets = new List<string>();
                foreach (var guid in Selection.assetGUIDs)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        assets.Add(path);
                    }
                }
                result["assets"] = assets;
            }

            result["count"] = Selection.count;

            return McpResponse.Success(new { selection = result });
        }

        private static McpToolResult SetSelection(Dictionary<string, object> args)
        {
            bool doClear = ArgumentParser.GetBool(args, "clear", false);
            if (doClear)
            {
                Selection.objects = new UnityEngine.Object[0];
                return McpResponse.Success("Selection cleared", new { selectedCount = 0 });
            }

            var objectsToSelect = new List<UnityEngine.Object>();
            var notFound = new List<string>();

            if (ArgumentParser.HasKey(args, "gameObjectPaths") && args.TryGetValue("gameObjectPaths", out var goPathsObj) && goPathsObj != null)
            {
                var paths = ConvertToStringArray(goPathsObj);
                foreach (var path in paths)
                {
                    var go = GameObjectHelpers.FindGameObject(path);
                    if (go != null)
                    {
                        objectsToSelect.Add(go);
                    }
                    else
                    {
                        notFound.Add(path);
                    }
                }
            }

            if (ArgumentParser.HasKey(args, "assetPaths") && args.TryGetValue("assetPaths", out var assetPathsObj) && assetPathsObj != null)
            {
                var paths = ConvertToStringArray(assetPathsObj);
                foreach (var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        objectsToSelect.Add(asset);
                    }
                    else
                    {
                        notFound.Add(path);
                    }
                }
            }

            if (objectsToSelect.Count == 0 && notFound.Count > 0)
            {
                return McpToolResult.Error($"No objects found. Not found: {string.Join(", ", notFound)}");
            }

            Selection.objects = objectsToSelect.ToArray();

            var resultData = new Dictionary<string, object>
            {
                ["selectedCount"] = objectsToSelect.Count,
                ["selectedObjects"] = objectsToSelect.Select(o => o.name).ToList()
            };

            if (notFound.Count > 0)
            {
                resultData["notFound"] = notFound;
            }

            return McpResponse.Success($"Selected {objectsToSelect.Count} object(s)", resultData);
        }

        private static McpToolResult FocusGameObject(Dictionary<string, object> args)
        {
            var (go, path, goErr) = RequireGameObject(args, "path");
            if (goErr != null) return goErr;

            Selection.activeGameObject = go;

            // Frame in Scene view
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected();
                sceneView.Focus();
            }

            return McpResponse.Success(new
            {
                focused    = go.name,
                path       = GameObjectHelpers.GetGameObjectPath(go),
                sceneFocused = sceneView != null,
                hint = sceneView == null ? "No Scene view open — object was selected but not framed." : null
            });
        }

        private static McpToolResult GetProjectOverview(Dictionary<string, object> args)
        {
            // Render pipeline
            string renderPipeline = "Built-in";
            var pipelineAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (pipelineAsset != null)
            {
                string pipelineType = pipelineAsset.GetType().Name;
                if (pipelineType.Contains("Universal")) renderPipeline = "URP";
                else if (pipelineType.Contains("HighDefinition")) renderPipeline = "HDRP";
                else renderPipeline = pipelineType;
            }

            // Asset counts by type
            var assetExtensions = new Dictionary<string, string>
            {
                { ".cs", "Scripts" }, { ".prefab", "Prefabs" }, { ".unity", "Scenes" },
                { ".mat", "Materials" }, { ".png", "Textures" }, { ".jpg", "Textures" },
                { ".fbx", "Models" }, { ".anim", "Animations" }, { ".asset", "ScriptableObjects" }
            };
            var counts = new Dictionary<string, int>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/")) continue;
                var ext = System.IO.Path.GetExtension(path).ToLower();
                if (assetExtensions.TryGetValue(ext, out var typeName))
                    counts[typeName] = counts.GetValueOrDefault(typeName, 0) + 1;
            }

            // Build scenes
            var buildScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToList();

            // FIX-#427: don't block main thread. Issue the list request and return its
            // synchronous state; if not ready, surface a hint instead of busy-waiting.
            // Subsequent overview calls will see the request completed (cached by Unity).
            var packages = new List<string>();
            try
            {
                var listRequest = UnityEditor.PackageManager.Client.List(true);
                if (listRequest.IsCompleted && listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    foreach (var p in listRequest.Result)
                        if (!p.name.StartsWith("com.unity.modules"))
                            packages.Add($"{p.name}@{p.version}");
                }
                else if (!listRequest.IsCompleted)
                {
                    packages.Add("(package list not ready — call unity_get_project_overview again in a moment)");
                }
            }
            catch (Exception) { /* Package Manager API unavailable — non-critical for project overview */ }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // Health roll-up — surfaces compile errors / pending compilation in
            // the same call so callers don't have to fetch console logs separately.
            int errors = 0, warnings = 0;
            foreach (var entry in _consoleLogs)
            {
                if (entry.Type == "Error" || entry.Type == "Exception" || entry.Type == "Assert")
                    errors++;
                else if (entry.Type == "Warning")
                    warnings++;
            }
            var health = new
            {
                isCompiling    = EditorApplication.isCompiling,
                isPlaying      = EditorApplication.isPlaying,
                recentErrors   = errors,
                recentWarnings = warnings,
                status         = (errors > 0 || EditorApplication.isCompiling) ? "issues" : "ok"
            };

            return McpResponse.Success(new
            {
                productName      = Application.productName,
                companyName      = Application.companyName,
                version          = Application.version,
                unityVersion     = Application.unityVersion,
                platform         = EditorUserBuildSettings.activeBuildTarget.ToString(),
                renderPipeline   = renderPipeline,
                activeScene      = activeScene.name,
                buildScenes      = buildScenes,
                assetCounts      = counts,
                installedPackages = packages,
                health           = health,
                hint = "Use unity_get_editor_state for live state, unity_list_gameobjects for scene content."
            });
        }

        private static McpToolResult ClearConsole(Dictionary<string, object> args)
        {
            try
            {
                var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
                var type = assembly.GetType("UnityEditor.LogEntries");
                var method = type.GetMethod("Clear");
                method.Invoke(null, null);

                while (_consoleLogs.TryDequeue(out _)) { }

                return McpResponse.Success("Console cleared", new { cleared = true });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to clear console: {ex.Message}");
            }
        }

        private static McpToolResult GetConsoleLogs(Dictionary<string, object> args)
        {
            try
            {
                string logTypeFilter = ArgumentParser.GetString(args, "logType", "All");
                int count = ArgumentParser.GetIntClamped(args, "count", 50, 1, 500);
                bool includeStackTrace = ArgumentParser.GetBool(args, "includeStackTrace", false);

                // Read from the in-process buffer populated by Application.logMessageReceived.
                // Avoids reflection on Unity internals (fragile across versions).
                var queue = GetConsoleLogQueue();
                var allEntries = queue.ToArray(); // snapshot — thread-safe copy

                var logs = new List<object>();
                // Queue is FIFO oldest→newest; iterate in reverse for most-recent-first
                for (int i = allEntries.Length - 1; i >= 0 && logs.Count < count; i--)
                {
                    var entry = allEntries[i];
                    if (logTypeFilter != "All" &&
                        !entry.Type.Equals(logTypeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    logs.Add(entry.ToSerializable(includeStackTrace));
                }

                return McpResponse.Success(new
                {
                    logs           = logs,
                    totalCount     = allEntries.Length,
                    retrievedCount = logs.Count,
                    filter         = logTypeFilter
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get console logs: {ex.Message}");
            }
        }

    }
}
