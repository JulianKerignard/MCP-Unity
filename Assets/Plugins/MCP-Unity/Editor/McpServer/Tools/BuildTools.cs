using System;
using System.Collections.Generic;
using System.Linq;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Build configuration tools for MCP Unity Server.
    /// Contains 3 tools: GetBuildSettings, ManageBuildScenes, SwitchPlatform
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all build-related tools
        /// </summary>
        static partial void RegisterBuildTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_build_settings",
                description = "Get current build settings: target platform, scenes list, scripting backend, player settings",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, HandleGetBuildSettings);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_manage_build_scenes",
                description = "Add, remove, enable, disable, or reorder scenes in build settings",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["action"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Action to perform",
                            @enum = new List<string> { "add", "remove", "enable", "disable", "reorder" }
                        },
                        ["scenePath"] = new McpPropertySchema { type = "string", description = "Scene path (required for add/remove/enable/disable/reorder)" },
                        ["sceneIndex"] = new McpPropertySchema { type = "integer", description = "Target index for reorder action" }
                    },
                    required = new List<string> { "action", "scenePath" }
                }
            }, HandleManageBuildScenes);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_switch_platform",
                description = "Switch the active build target platform. WARNING: This can be a long operation that triggers recompilation.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["platform"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Target platform",
                            @enum = new List<string> { "Windows", "Mac", "Linux", "iOS", "Android", "WebGL" }
                        }
                    },
                    required = new List<string> { "platform" }
                }
            }, HandleSwitchPlatform);
        }

        #region Build Handlers

        private static McpToolResult HandleGetBuildSettings(Dictionary<string, object> args)
        {
            try
            {
                var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

                // Get scenes in build settings
                var scenes = EditorBuildSettings.scenes.Select(s => new Dictionary<string, object>
                {
                    ["path"] = s.path,
                    ["enabled"] = s.enabled,
                    ["guid"] = s.guid.ToString()
                }).ToList();

                // Get scripting backend
                string scriptingBackend;
                try
                {
                    scriptingBackend = PlayerSettings.GetScriptingBackend(
                        NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup)).ToString();
                }
                catch
                {
                    scriptingBackend = "Unknown";
                }

                // Get API compatibility level
                string apiCompatibility;
                try
                {
                    apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(
                        NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup)).ToString();
                }
                catch
                {
                    apiCompatibility = "Unknown";
                }

                return McpResponse.Success(new
                {
                    activeBuildTarget = buildTarget.ToString(),
                    buildTargetGroup = buildTargetGroup.ToString(),
                    scenes = scenes,
                    sceneCount = scenes.Count,
                    enabledSceneCount = scenes.Count(s => (bool)s["enabled"]),
                    scriptingBackend = scriptingBackend,
                    apiCompatibility = apiCompatibility,
                    productName = PlayerSettings.productName,
                    companyName = PlayerSettings.companyName,
                    bundleVersion = PlayerSettings.bundleVersion
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get build settings: {ex.Message}");
            }
        }

        private static McpToolResult HandleManageBuildScenes(Dictionary<string, object> args)
        {
            try
            {
                var (action, actionErr) = RequireArg(args, "action");
                if (actionErr != null) return actionErr;

                var (rawScenePath, scenePathErr) = RequireArg(args, "scenePath");
                if (scenePathErr != null) return scenePathErr;

                var (scenePath, sanitizeErr) = TrySanitizePath(rawScenePath, "scene path");
                if (sanitizeErr != null) return sanitizeErr;

                var scenesList = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

                switch (action.ToLower())
                {
                    case "add":
                    {
                        // Check if scene already exists
                        if (scenesList.Any(s => s.path == scenePath))
                        {
                            return McpToolResult.Error($"Scene already in build settings: {scenePath}");
                        }

                        // Verify the scene file exists
                        if (!System.IO.File.Exists(scenePath))
                        {
                            return McpToolResult.Error($"Scene file not found: {scenePath}");
                        }

                        if (!scenePath.EndsWith(".unity"))
                        {
                            return McpToolResult.Error("Scene path must end with .unity");
                        }

                        scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
                        EditorBuildSettings.scenes = scenesList.ToArray();

                        return McpResponse.Success($"Added scene to build settings: {scenePath}", GetBuildScenesInfo());
                    }

                    case "remove":
                    {
                        int removed = scenesList.RemoveAll(s => s.path == scenePath);
                        if (removed == 0)
                        {
                            return McpToolResult.Error($"Scene not found in build settings: {scenePath}");
                        }

                        EditorBuildSettings.scenes = scenesList.ToArray();

                        return McpResponse.Success($"Removed scene from build settings: {scenePath}", GetBuildScenesInfo());
                    }

                    case "enable":
                    {
                        bool found = false;
                        for (int i = 0; i < scenesList.Count; i++)
                        {
                            if (scenesList[i].path == scenePath)
                            {
                                var scene = scenesList[i];
                                scene.enabled = true;
                                scenesList[i] = scene;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            return McpToolResult.Error($"Scene not found in build settings: {scenePath}");
                        }

                        EditorBuildSettings.scenes = scenesList.ToArray();

                        return McpResponse.Success($"Enabled scene in build settings: {scenePath}", GetBuildScenesInfo());
                    }

                    case "disable":
                    {
                        bool found = false;
                        for (int i = 0; i < scenesList.Count; i++)
                        {
                            if (scenesList[i].path == scenePath)
                            {
                                var scene = scenesList[i];
                                scene.enabled = false;
                                scenesList[i] = scene;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            return McpToolResult.Error($"Scene not found in build settings: {scenePath}");
                        }

                        EditorBuildSettings.scenes = scenesList.ToArray();

                        return McpResponse.Success($"Disabled scene in build settings: {scenePath}", GetBuildScenesInfo());
                    }

                    case "reorder":
                    {
                        int targetIndex = ArgumentParser.GetInt(args, "sceneIndex", -1);
                        if (targetIndex < 0)
                        {
                            return McpToolResult.Error("sceneIndex is required for reorder action and must be >= 0");
                        }

                        int currentIndex = scenesList.FindIndex(s => s.path == scenePath);
                        if (currentIndex < 0)
                        {
                            return McpToolResult.Error($"Scene not found in build settings: {scenePath}");
                        }

                        if (targetIndex >= scenesList.Count)
                        {
                            targetIndex = scenesList.Count - 1;
                        }

                        var sceneToMove = scenesList[currentIndex];
                        scenesList.RemoveAt(currentIndex);
                        scenesList.Insert(targetIndex, sceneToMove);

                        EditorBuildSettings.scenes = scenesList.ToArray();

                        return McpResponse.Success($"Moved scene from index {currentIndex} to {targetIndex}: {scenePath}", GetBuildScenesInfo());
                    }

                    default:
                        return McpToolResult.Error($"Unknown action: '{action}'. Valid actions: add, remove, enable, disable, reorder");
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to manage build scenes: {ex.Message}");
            }
        }

        private static McpToolResult HandleSwitchPlatform(Dictionary<string, object> args)
        {
            try
            {
                var (platform, platformErr) = RequireArg(args, "platform");
                if (platformErr != null) return platformErr;

                BuildTargetGroup targetGroup;
                BuildTarget buildTarget;

                switch (platform)
                {
                    case "Windows":
                        targetGroup = BuildTargetGroup.Standalone;
                        buildTarget = BuildTarget.StandaloneWindows64;
                        break;
                    case "Mac":
                        targetGroup = BuildTargetGroup.Standalone;
                        buildTarget = BuildTarget.StandaloneOSX;
                        break;
                    case "Linux":
                        targetGroup = BuildTargetGroup.Standalone;
                        buildTarget = BuildTarget.StandaloneLinux64;
                        break;
                    case "iOS":
                        targetGroup = BuildTargetGroup.iOS;
                        buildTarget = BuildTarget.iOS;
                        break;
                    case "Android":
                        targetGroup = BuildTargetGroup.Android;
                        buildTarget = BuildTarget.Android;
                        break;
                    case "WebGL":
                        targetGroup = BuildTargetGroup.WebGL;
                        buildTarget = BuildTarget.WebGL;
                        break;
                    default:
                        return McpToolResult.Error($"Unknown platform: '{platform}'. Valid platforms: Windows, Mac, Linux, iOS, Android, WebGL");
                }

                // Check if already on this platform
                if (EditorUserBuildSettings.activeBuildTarget == buildTarget)
                {
                    return McpResponse.Success($"Already on platform: {platform}", new
                    {
                        platform = platform,
                        buildTarget = buildTarget.ToString(),
                        buildTargetGroup = targetGroup.ToString(),
                        alreadyActive = true
                    });
                }

                string previousTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
                bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, buildTarget);

                if (!success)
                {
                    return McpToolResult.Error($"Failed to switch platform to {platform}. The platform module may not be installed.");
                }

                return McpResponse.Success($"Switched platform from {previousTarget} to {platform}", new
                {
                    platform = platform,
                    previousTarget = previousTarget,
                    buildTarget = buildTarget.ToString(),
                    buildTargetGroup = targetGroup.ToString(),
                    alreadyActive = false
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to switch platform: {ex.Message}");
            }
        }

        #endregion

        #region Build Helpers

        private static object GetBuildScenesInfo()
        {
            var scenes = EditorBuildSettings.scenes.Select((s, index) => new Dictionary<string, object>
            {
                ["index"] = index,
                ["path"] = s.path,
                ["enabled"] = s.enabled,
                ["guid"] = s.guid.ToString()
            }).ToList();

            return new
            {
                scenes = scenes,
                sceneCount = scenes.Count,
                enabledSceneCount = scenes.Count(s => (bool)s["enabled"])
            };
        }

        #endregion
    }
}
