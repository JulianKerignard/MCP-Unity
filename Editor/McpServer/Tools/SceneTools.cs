using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace McpUnity.Server
{
    /// <summary>
    /// Scene management tools for MCP Unity Server.
    /// Contains 4 tools: GetSceneInfo, LoadScene, SaveScene, CreateScene
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all scene-related tools
        /// </summary>
        static partial void RegisterSceneTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_scene_info",
                description = "Get information about the current scene and all loaded scenes",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetSceneInfo);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_load_scene",
                description = "Load a scene in the editor",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["scenePath"] = new McpPropertySchema { type = "string", description = "Path to the scene file (must end with .unity)" },
                        ["mode"] = new McpPropertySchema { type = "string", description = "Load mode: 'Single' (default) or 'Additive'" }
                    },
                    required = new List<string> { "scenePath" }
                }
            }, LoadScene);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_save_scene",
                description = "Save the current scene or all open scenes",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["saveAll"] = new McpPropertySchema { type = "boolean", description = "If true, save all open scenes" },
                        ["scenePath"] = new McpPropertySchema { type = "string", description = "Optional: Save as a new scene at this path (must end with .unity)" }
                    },
                    required = new List<string>()
                }
            }, SaveScene);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_scenes_in_project",
                description = "List all .unity scene files in the project (not just open ones)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["searchFolder"] = new McpPropertySchema { type = "string", description = "Limit search to a folder (default: entire Assets/)" }
                    },
                    required = new List<string>()
                }
            }, ListScenesInProject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_scene",
                description = "Create a new scene",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["sceneName"] = new McpPropertySchema { type = "string", description = "Name for the new scene" },
                        ["setup"] = new McpPropertySchema { type = "string", description = "Setup type: 'default' (with Main Camera and Light) or 'empty'" },
                        ["mode"] = new McpPropertySchema { type = "string", description = "Scene mode: 'single' (replaces current) or 'additive'" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Optional: Path to save the scene (must end with .unity)" }
                    },
                    required = new List<string> { "sceneName" }
                }
            }, CreateScene);
        }

        #region Scene Handlers

        private static McpToolResult ListScenesInProject(Dictionary<string, object> args)
        {
            string searchFolder = ArgumentParser.GetString(args, "searchFolder", "Assets");

            // Validate search folder
            if (!string.IsNullOrEmpty(searchFolder) && !searchFolder.StartsWith("Assets"))
                return McpToolResult.Error("searchFolder must start with 'Assets'");

            string[] guids = string.IsNullOrEmpty(searchFolder)
                ? AssetDatabase.FindAssets("t:Scene")
                : AssetDatabase.FindAssets("t:Scene", new[] { searchFolder });

            var scenes = new List<object>();
            var buildScenePaths = new System.Collections.Generic.HashSet<string>();
            foreach (var buildScene in EditorBuildSettings.scenes)
                buildScenePaths.Add(buildScene.path);

            foreach (var guid in guids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                scenes.Add(new
                {
                    path         = scenePath,
                    name         = System.IO.Path.GetFileNameWithoutExtension(scenePath),
                    guid         = guid,
                    inBuildSettings = buildScenePaths.Contains(scenePath)
                });
            }

            return McpResponse.Success(new
            {
                count        = scenes.Count,
                searchFolder = searchFolder,
                scenes       = scenes
            });
        }

        private static McpToolResult GetSceneInfo(Dictionary<string, object> args)
        {
            var activeScene = SceneManager.GetActiveScene();

            var activeSceneInfo = new Dictionary<string, object>
            {
                ["name"] = activeScene.name,
                ["path"] = activeScene.path,
                ["buildIndex"] = activeScene.buildIndex,
                ["isDirty"] = activeScene.isDirty,
                ["isLoaded"] = activeScene.isLoaded,
                ["rootCount"] = activeScene.rootCount
            };

            var loadedScenes = new List<Dictionary<string, object>>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                loadedScenes.Add(new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["isDirty"] = scene.isDirty,
                    ["isLoaded"] = scene.isLoaded
                });
            }

            return McpResponse.Success(new
            {
                activeScene = activeSceneInfo,
                loadedScenes = loadedScenes,
                totalLoadedScenes = SceneManager.sceneCount
            });
        }

        private static McpToolResult LoadScene(Dictionary<string, object> args)
        {
            var (scenePath, scenePathErr) = RequireArg(args, "scenePath");
            if (scenePathErr != null) return scenePathErr;

            // Security: Validate path to prevent path traversal attacks
            var (sanitizedScenePath, sanitizeErr) = TrySanitizePath(scenePath, "scene path");
            if (sanitizeErr != null) return sanitizeErr;
            scenePath = sanitizedScenePath;

            // Validate extension
            if (!scenePath.EndsWith(".unity"))
            {
                return McpToolResult.Error("Invalid scene path. Must end with .unity");
            }

            if (!System.IO.File.Exists(scenePath))
            {
                return McpToolResult.Error($"Scene not found: {scenePath}");
            }

            // Parse mode
            OpenSceneMode mode = ArgumentParser.GetEnum(args, "mode", OpenSceneMode.Single);

            // Check for unsaved changes that will be lost (Single mode replaces all open scenes)
            bool hadUnsavedChanges = false;
            if (mode == OpenSceneMode.Single)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty) { hadUnsavedChanges = true; break; }
                }
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, mode);

                var resultData = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["sceneName"] = scene.name,
                    ["scenePath"] = scene.path,
                    ["mode"] = mode.ToString()
                };
                if (hadUnsavedChanges)
                    resultData["warning"] = "Previous scene had unsaved changes that were discarded. Use unity_save_scene before loading to prevent data loss.";

                return McpResponse.Success(resultData);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to load scene: {ex.Message}");
            }
        }

        private static McpToolResult SaveScene(Dictionary<string, object> args)
        {
            bool saveAll = ArgumentParser.GetBool(args, "saveAll", false);

            string saveAsPath = ArgumentParser.GetString(args, "scenePath", null);
            if (!string.IsNullOrEmpty(saveAsPath))
            {

                // Security: Validate path to prevent path traversal attacks
                var (sanitizedSavePath, savePathErr) = TrySanitizePath(saveAsPath, "save path");
                if (savePathErr != null) return savePathErr;
                saveAsPath = sanitizedSavePath;
            }

            try
            {
                if (saveAll)
                {
                    bool success = EditorSceneManager.SaveOpenScenes();

                    var savedScenes = new List<string>();
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        savedScenes.Add(SceneManager.GetSceneAt(i).name);
                    }

                    return new McpToolResult
                    {
                        content = new List<McpContent>
                        {
                            McpContent.Json(new
                            {
                                success = success,
                                message = success ? "All scenes saved" : "Failed to save some scenes",
                                savedScenes = savedScenes
                            })
                        },
                        isError = !success
                    };
                }
                else
                {
                    var activeScene = SceneManager.GetActiveScene();
                    bool success;

                    if (!string.IsNullOrEmpty(saveAsPath))
                    {
                        // Validate path
                        if (!saveAsPath.EndsWith(".unity"))
                        {
                            return McpToolResult.Error("Save path must end with .unity");
                        }

                        // Ensure directory exists
                        var directory = System.IO.Path.GetDirectoryName(saveAsPath);
                        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                        {
                            System.IO.Directory.CreateDirectory(directory);
                        }

                        success = EditorSceneManager.SaveScene(activeScene, saveAsPath);
                    }
                    else
                    {
                        success = EditorSceneManager.SaveScene(activeScene);
                    }

                    return new McpToolResult
                    {
                        content = new List<McpContent>
                        {
                            McpContent.Json(new
                            {
                                success = success,
                                sceneName = activeScene.name,
                                scenePath = string.IsNullOrEmpty(saveAsPath) ? activeScene.path : saveAsPath,
                                message = success ? "Scene saved successfully" : "Failed to save scene"
                            })
                        },
                        isError = !success
                    };
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to save scene: {ex.Message}");
            }
        }

        private static McpToolResult CreateScene(Dictionary<string, object> args)
        {
            var (sceneName, nameErr) = RequireArg(args, "sceneName");
            if (nameErr != null) return nameErr;

            // Parse setup mode
            NewSceneSetup sceneSetup = NewSceneSetup.DefaultGameObjects;
            if (args.TryGetValue("setup", out var setupObj) && setupObj != null)
            {
                string setupStr = setupObj.ToString();
                if (setupStr.Equals("empty", StringComparison.OrdinalIgnoreCase))
                {
                    sceneSetup = NewSceneSetup.EmptyScene;
                }
            }

            // Parse scene mode
            NewSceneMode sceneMode = ArgumentParser.GetEnum(args, "mode", NewSceneMode.Single);

            // SEC-#391: Single mode replaces all open scenes and would silently throw away
            // unsaved changes. Mirror the guard from LoadScene and let the user decide.
            bool hadUnsavedChanges = false;
            if (sceneMode == NewSceneMode.Single)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty) { hadUnsavedChanges = true; break; }
                }

                if (hadUnsavedChanges &&
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return McpToolResult.Error("CreateScene cancelled — user declined to save unsaved changes.");
                }
            }

            try
            {
                // Create the new scene
                var scene = EditorSceneManager.NewScene(sceneSetup, sceneMode);

                string savedPath = null;

                // Determine save path: explicit savePath takes priority, otherwise auto-derive from sceneName
                // (Unity cannot name a scene without saving it to a file)
                string savePathArg = ArgumentParser.GetString(args, "savePath", null);
                if (string.IsNullOrEmpty(savePathArg))
                    savePathArg = $"Assets/Scenes/{sceneName}.unity";

                var (finalSavePath, finalSavePathErr) = TrySanitizePath(savePathArg, "save path");
                if (finalSavePathErr != null) return finalSavePathErr;

                if (!finalSavePath.EndsWith(".unity"))
                    return McpToolResult.Error("Save path must end with .unity");

                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(finalSavePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);

                EditorSceneManager.SaveScene(scene, finalSavePath);
                savedPath = finalSavePath;

                return McpResponse.Success(new
                {
                    success = true,
                    sceneName = System.IO.Path.GetFileNameWithoutExtension(savedPath),
                    scenePath = savedPath,
                    setup = sceneSetup.ToString(),
                    mode = sceneMode.ToString(),
                    rootObjectCount = scene.rootCount
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create scene: {ex.Message}");
            }
        }

        #endregion
    }
}
