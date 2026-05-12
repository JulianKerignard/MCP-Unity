using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Animation Clip tools: list clips and get clip info.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorClipTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_animation_clips",
                description = "List all animation clips in the project with optional filtering",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["searchPath"] = new McpPropertySchema { type = "string", description = "Folder to search in" },
                        ["nameFilter"] = new McpPropertySchema { type = "string", description = "Filter clips by name (case-insensitive)" },
                        ["avatarFilter"] = new McpPropertySchema { type = "string", description = "Filter by type: 'humanoid', 'generic', 'legacy'" }
                    }
                }
            }, ListAnimationClips);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_animation_clip",
                description = "Create a new empty AnimationClip asset",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["savePath"]  = new McpPropertySchema { type = "string",  description = "Asset path for the clip (e.g. 'Assets/Animations/Idle.anim')" },
                        ["frameRate"] = new McpPropertySchema { type = "number",  description = "Frame rate" },
                        ["isLooping"] = new McpPropertySchema { type = "boolean", description = "Enable loop time" },
                        ["wrapMode"]  = new McpPropertySchema { type = "string",  description = "Wrap mode: Default, Once, Loop, PingPong, ClampForever" }
                    },
                    required = new List<string> { "savePath" }
                }
            }, CreateAnimationClip);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_clip_info",
                description = "Get detailed information about an animation clip including curves and events",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["clipPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimationClip asset" }
                    },
                    required = new List<string> { "clipPath" }
                }
            }, GetClipInfo);
        }

        #region Clip Handlers

        private static McpToolResult CreateAnimationClip(Dictionary<string, object> args)
        {
            var (rawPath, savePathErr) = RequireArg(args, "savePath");
            if (savePathErr != null) return savePathErr;

            var (savePath, sanitizeErr) = TrySanitizePath(rawPath, "path");
            if (sanitizeErr != null) return sanitizeErr;

            if (!savePath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                savePath += ".anim";

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath) != null)
                return McpToolResult.Error($"AnimationClip already exists at: {savePath}");

            var dir = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var clip = new AnimationClip
            {
                frameRate = ArgumentParser.GetFloat(args, "frameRate", 30f)
            };

            // Wrap mode
            string wrapModeStr = ArgumentParser.GetString(args, "wrapMode", "Default");
            if (System.Enum.TryParse<WrapMode>(wrapModeStr, true, out var wrapMode))
                clip.wrapMode = wrapMode;

            // Loop setting via AnimationClipSettings
            bool isLooping = ArgumentParser.GetBool(args, "isLooping", false);
            if (isLooping)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            AssetDatabase.CreateAsset(clip, savePath);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Created AnimationClip at {savePath}", new
            {
                path      = savePath,
                name      = System.IO.Path.GetFileNameWithoutExtension(savePath),
                frameRate = clip.frameRate,
                wrapMode  = clip.wrapMode.ToString(),
                isLooping = isLooping
            });
        }

        private static McpToolResult ListAnimationClips(Dictionary<string, object> args)
        {
            string searchPath = ArgumentParser.GetString(args, "searchPath", "Assets");
            string nameFilter = ArgumentParser.GetString(args, "nameFilter")?.ToLowerInvariant();
            string avatarFilter = ArgumentParser.GetString(args, "avatarFilter")?.ToLowerInvariant();

            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { searchPath });
            var clips = new List<object>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                if (clip == null) continue;

                if (!string.IsNullOrEmpty(nameFilter))
                {
                    if (!clip.name.ToLowerInvariant().Contains(nameFilter))
                        continue;
                }

                if (!string.IsNullOrEmpty(avatarFilter))
                {
                    bool isHumanoid = clip.isHumanMotion;
                    bool isLegacy = clip.legacy;

                    if (avatarFilter == "humanoid" && !isHumanoid) continue;
                    if (avatarFilter == "generic" && (isHumanoid || isLegacy)) continue;
                    if (avatarFilter == "legacy" && !isLegacy) continue;
                }

                clips.Add(new
                {
                    path = path,
                    name = clip.name,
                    length = clip.length,
                    frameRate = clip.frameRate,
                    isLooping = clip.isLooping,
                    isHumanMotion = clip.isHumanMotion,
                    hasRootMotion = clip.hasMotionCurves,
                    isLegacy = clip.legacy
                });
            }

            return McpResponse.Success(new
            {
                clips = clips,
                totalCount = clips.Count,
                searchPath = searchPath,
                filters = new
                {
                    nameFilter = nameFilter,
                    avatarFilter = avatarFilter
                }
            });
        }

        private static McpToolResult GetClipInfo(Dictionary<string, object> args)
        {
            var (clipPath, clipPathErr) = RequireArg(args, "clipPath");
            if (clipPathErr != null) return clipPathErr;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                return McpToolResult.Error($"AnimationClip not found at: {clipPath}");

            var animationEvents = AnimationUtility.GetAnimationEvents(clip);
            var events = new List<object>();
            foreach (var evt in animationEvents)
            {
                events.Add(new
                {
                    time = evt.time,
                    functionName = evt.functionName,
                    intParameter = evt.intParameter,
                    floatParameter = evt.floatParameter,
                    stringParameter = evt.stringParameter
                });
            }

            var curveBindings = AnimationUtility.GetCurveBindings(clip);
            var curves = new List<object>();
            foreach (var binding in curveBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                curves.Add(new
                {
                    path = binding.path,
                    propertyName = binding.propertyName,
                    type = binding.type.Name,
                    keyCount = curve != null ? curve.length : 0
                });
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                curves.Add(new
                {
                    path = binding.path,
                    propertyName = binding.propertyName,
                    type = binding.type.Name,
                    keyCount = keyframes != null ? keyframes.Length : 0,
                    isObjectReference = true
                });
            }

            return McpResponse.Success(new
            {
                name = clip.name,
                path = clipPath,
                length = clip.length,
                frameRate = clip.frameRate,
                wrapMode = clip.wrapMode.ToString(),
                isLooping = clip.isLooping,
                isHumanMotion = clip.isHumanMotion,
                hasRootMotion = clip.hasMotionCurves,
                isLegacy = clip.legacy,
                localBounds = new
                {
                    center = new { x = clip.localBounds.center.x, y = clip.localBounds.center.y, z = clip.localBounds.center.z },
                    size = new { x = clip.localBounds.size.x, y = clip.localBounds.size.y, z = clip.localBounds.size.z }
                },
                events = events,
                eventCount = events.Count,
                curves = curves,
                curveCount = curves.Count
            });
        }

        #endregion
    }
}
