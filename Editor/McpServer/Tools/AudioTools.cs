using System;
using System.Collections.Generic;
using System.Linq;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace McpUnity.Server
{
    /// <summary>
    /// Audio tools for MCP Unity Server.
    /// Contains 3 tools: SetupAudioSource, CreateAudioMixer, GetAudioMixer
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all audio-related tools
        /// </summary>
        static partial void RegisterAudioTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_setup_audio_source",
                description = "Add or configure an AudioSource on a GameObject with clip, mixer, and spatial settings",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject in the hierarchy" },
                        ["clipPath"] = new McpPropertySchema { type = "string", description = "Asset path to an AudioClip (e.g., 'Assets/Audio/clip.wav')" },
                        ["volume"] = new McpPropertySchema { type = "number", description = "Volume 0-1" },
                        ["pitch"] = new McpPropertySchema { type = "number", description = "Pitch" },
                        ["loop"] = new McpPropertySchema { type = "boolean", description = "Loop playback" },
                        ["playOnAwake"] = new McpPropertySchema { type = "boolean", description = "Play on awake" },
                        ["spatialBlend"] = new McpPropertySchema { type = "number", description = "Spatial blend 0=2D, 1=3D" },
                        ["minDistance"] = new McpPropertySchema { type = "number", description = "3D min distance" },
                        ["maxDistance"] = new McpPropertySchema { type = "number", description = "3D max distance" },
                        ["mixerGroupPath"] = new McpPropertySchema { type = "string", description = "Asset path to an AudioMixer (e.g., 'Assets/Audio/MainMixer.mixer')" },
                        ["mixerGroupName"] = new McpPropertySchema { type = "string", description = "Group name within the mixer (e.g., 'Music')" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetupAudioSource);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_audio_mixer",
                description = "Create a new AudioMixer asset. Note: sub-group creation requires the Unity Editor UI.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Path to save the mixer (e.g., 'Assets/Audio/MainMixer.mixer')" },
                        ["groupNames"] = new McpPropertySchema { type = "array", description = "Additional group names (informational — sub-groups must be added via Unity Editor)" }
                    },
                    required = new List<string> { "savePath" }
                }
            }, CreateAudioMixer);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_audio_mixer",
                description = "Get information about an AudioMixer asset including groups and exposed parameters",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["mixerPath"] = new McpPropertySchema { type = "string", description = "Asset path to the AudioMixer (e.g., 'Assets/Audio/MainMixer.mixer')" }
                    },
                    required = new List<string> { "mixerPath" }
                }
            }, GetAudioMixer);
        }

        #region Audio Handlers

        private static McpToolResult SetupAudioSource(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args, "gameObjectPath");
            if (goErr != null) return goErr;

            try
            {
                var audioSource = go.GetComponent<AudioSource>();
                bool isNew = audioSource == null;

                if (isNew)
                {
                    audioSource = Undo.AddComponent<AudioSource>(go);
                }
                else
                {
                    Undo.RecordObject(audioSource, "MCP Setup AudioSource");
                }

                // Load and assign AudioClip if provided
                string clipPath = ArgumentParser.GetString(args, "clipPath", null);
                string assignedClipName = null;
                if (!string.IsNullOrEmpty(clipPath))
                {
                    var (sanitizedClipPath, clipPathErr) = TrySanitizePath(clipPath, "clip path");
                    if (clipPathErr != null) return clipPathErr;
                    clipPath = sanitizedClipPath;

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip == null)
                        return McpToolResult.Error($"AudioClip not found at: {clipPath}");

                    audioSource.clip = clip;
                    assignedClipName = clip.name;
                }

                // Apply properties
                audioSource.volume = ArgumentParser.GetFloat(args, "volume", isNew ? 1f : audioSource.volume);
                audioSource.pitch = ArgumentParser.GetFloat(args, "pitch", isNew ? 1f : audioSource.pitch);
                audioSource.loop = ArgumentParser.GetBool(args, "loop", isNew ? false : audioSource.loop);
                audioSource.playOnAwake = ArgumentParser.GetBool(args, "playOnAwake", isNew ? true : audioSource.playOnAwake);
                audioSource.spatialBlend = ArgumentParser.GetFloat(args, "spatialBlend", isNew ? 0f : audioSource.spatialBlend);
                audioSource.minDistance = ArgumentParser.GetFloat(args, "minDistance", isNew ? 1f : audioSource.minDistance);
                audioSource.maxDistance = ArgumentParser.GetFloat(args, "maxDistance", isNew ? 500f : audioSource.maxDistance);

                // Assign AudioMixerGroup if mixer info provided
                string mixerGroupPath = ArgumentParser.GetString(args, "mixerGroupPath", null);
                string mixerGroupName = ArgumentParser.GetString(args, "mixerGroupName", null);
                string assignedMixerGroup = null;

                if (!string.IsNullOrEmpty(mixerGroupPath))
                {
                    var (sanitizedMixerGroupPath, mixerGroupPathErr) = TrySanitizePath(mixerGroupPath, "mixer path");
                    if (mixerGroupPathErr != null) return mixerGroupPathErr;
                    mixerGroupPath = sanitizedMixerGroupPath;

                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerGroupPath);
                    if (mixer == null)
                        return McpToolResult.Error($"AudioMixer not found at: {mixerGroupPath}");

                    // Find the matching group
                    string searchName = string.IsNullOrEmpty(mixerGroupName) ? "Master" : mixerGroupName;
                    var groups = mixer.FindMatchingGroups(searchName);

                    if (groups == null || groups.Length == 0)
                    {
                        // Try empty string to get all groups and list them
                        var allGroups = mixer.FindMatchingGroups("");
                        var groupNames = allGroups != null
                            ? string.Join(", ", allGroups.Select(g => g.name))
                            : "none";
                        return McpToolResult.Error(
                            $"AudioMixerGroup '{searchName}' not found in mixer. Available groups: {groupNames}");
                    }

                    audioSource.outputAudioMixerGroup = groups[0];
                    assignedMixerGroup = groups[0].name;
                }

                EditorUtility.SetDirty(go);

                var result = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["isNew"] = isNew,
                    ["volume"] = audioSource.volume,
                    ["pitch"] = audioSource.pitch,
                    ["loop"] = audioSource.loop,
                    ["playOnAwake"] = audioSource.playOnAwake,
                    ["spatialBlend"] = audioSource.spatialBlend,
                    ["minDistance"] = audioSource.minDistance,
                    ["maxDistance"] = audioSource.maxDistance,
                    ["message"] = $"{(isNew ? "Added" : "Updated")} AudioSource on '{go.name}'"
                };

                if (assignedClipName != null)
                    result["clip"] = assignedClipName;

                if (assignedMixerGroup != null)
                    result["mixerGroup"] = assignedMixerGroup;

                return McpResponse.Success(result);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to setup AudioSource: {ex.Message}");
            }
        }

        private static McpToolResult CreateAudioMixer(Dictionary<string, object> args)
        {
            var (rawPath, savePathArgErr) = RequireArg(args, "savePath");
            if (savePathArgErr != null) return savePathArgErr;

            var (savePath, savePathErr) = TrySanitizePath(rawPath, "save path");
            if (savePathErr != null) return savePathErr;

            // Ensure .mixer extension
            if (!savePath.EndsWith(".mixer", StringComparison.OrdinalIgnoreCase))
            {
                savePath = savePath + ".mixer";
            }

            // Check if asset already exists
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(savePath);
            if (existing != null)
                return McpToolResult.Error($"An AudioMixer already exists at: {savePath}");

            try
            {
                // SEC-#434: centralized helper replaces the copy-pasted folder creation loop.
                AssetDatabaseHelpers.EnsureFolderExists(System.IO.Path.GetDirectoryName(savePath));

                // AudioMixer cannot be created via 'new AudioMixer()' — it has no public constructor.
                // Use the ProjectWindowUtil approach to create the asset via Unity's internal API.
                // The most reliable programmatic method is using UnityEditor internal calls.

                // Attempt creation via ObjectFactory (Unity 2020.1+)
                AudioMixer mixer = null;
                try
                {
                    mixer = UnityEditor.ObjectFactory.CreateInstance<AudioMixer>();
                }
                catch (Exception ex)
                {
                    // SEC-#433: ObjectFactory may not support AudioMixer on all Unity versions.
                    // Log the reason so the fallback path isn't completely silent.
                    McpUnity.Editor.McpDebug.LogWarning($"[AudioTools] ObjectFactory.CreateInstance<AudioMixer> failed, falling back: {ex.Message}");
                    mixer = null;
                }

                if (mixer == null)
                {
                    // AudioMixer cannot be created programmatically via ScriptableObject.CreateInstance
                    var groupNames = ArgumentParser.GetStringArray(args, "groupNames");
                    var groupInfo = groupNames.Length > 0
                        ? $" with groups: Master, {string.Join(", ", groupNames)}"
                        : " with default Master group";

                    return McpToolResult.Error(
                        $"AudioMixer cannot be created programmatically in Unity. " +
                        $"Please create it manually: right-click in Project window > Create > Audio Mixer, " +
                        $"then save it to '{savePath}'{groupInfo}. " +
                        $"After creation, use 'unity_get_audio_mixer' to inspect it or " +
                        $"'unity_setup_audio_source' to assign its groups to AudioSources.");
                }

                // If we got here, creation succeeded
                mixer.name = System.IO.Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(mixer, savePath);
                AssetDatabase.SaveAssets();

                // Read back the created mixer to get group info
                var createdMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(savePath);
                var masterGroups = createdMixer?.FindMatchingGroups("Master");

                var groupNames2 = ArgumentParser.GetStringArray(args, "groupNames");
                var notes = new List<string>();
                if (groupNames2.Length > 0)
                {
                    notes.Add($"Sub-groups ({string.Join(", ", groupNames2)}) must be added via Unity Editor: " +
                              "select the mixer asset, then use the AudioMixer window (Window > Audio > Audio Mixer) " +
                              "to add groups under Master.");
                }

                var resultData = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["path"] = savePath,
                    ["name"] = mixer.name,
                    ["hasMasterGroup"] = masterGroups != null && masterGroups.Length > 0,
                    ["message"] = $"Created AudioMixer '{mixer.name}' at {savePath}"
                };

                if (notes.Count > 0)
                    resultData["notes"] = notes;

                return McpResponse.Success(resultData);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create AudioMixer: {ex.Message}");
            }
        }

        private static McpToolResult GetAudioMixer(Dictionary<string, object> args)
        {
            var (rawPath, mixerPathArgErr) = RequireArg(args, "mixerPath");
            if (mixerPathArgErr != null) return mixerPathArgErr;

            var (mixerPath, mixerPathErr) = TrySanitizePath(rawPath, "mixer path");
            if (mixerPathErr != null) return mixerPathErr;

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            if (mixer == null)
                return McpToolResult.Error($"AudioMixer not found at: {mixerPath}");

            try
            {
                // Get all groups using empty string match
                var allGroups = mixer.FindMatchingGroups("");
                var groupInfos = new List<Dictionary<string, object>>();

                if (allGroups != null)
                {
                    foreach (var group in allGroups)
                    {
                        if (group == null) continue;

                        var groupInfo = new Dictionary<string, object>
                        {
                            ["name"] = group.name
                        };

                        // Try to get the AudioMixer reference from the group
                        if (group.audioMixer != null)
                        {
                            groupInfo["mixerName"] = group.audioMixer.name;
                        }

                        groupInfos.Add(groupInfo);
                    }
                }

                // Try to get exposed parameters by reading the mixer's output group
                var outputGroup = mixer.outputAudioMixerGroup;

                // Collect exposed parameter info
                // AudioMixer doesn't expose a direct list of exposed parameters via public API,
                // but we can try to get known common parameter values
                var exposedParams = new List<Dictionary<string, object>>();

                // Try common parameter names that might be exposed
                string[] commonParams = { "MasterVolume", "MusicVolume", "SFXVolume", "Volume", "Pitch" };
                foreach (var paramName in commonParams)
                {
                    float value;
                    if (mixer.GetFloat(paramName, out value))
                    {
                        exposedParams.Add(new Dictionary<string, object>
                        {
                            ["name"] = paramName,
                            ["value"] = value
                        });
                    }
                }

                // Also try parameter names based on group names
                if (allGroups != null)
                {
                    foreach (var group in allGroups)
                    {
                        if (group == null) continue;
                        string[] suffixes = { "Volume", "Pitch", "Attenuation" };
                        foreach (var suffix in suffixes)
                        {
                            string paramName = group.name + suffix;
                            float value;
                            if (mixer.GetFloat(paramName, out value))
                            {
                                // Avoid duplicates
                                if (!exposedParams.Any(p => (string)p["name"] == paramName))
                                {
                                    exposedParams.Add(new Dictionary<string, object>
                                    {
                                        ["name"] = paramName,
                                        ["value"] = value
                                    });
                                }
                            }
                        }
                    }
                }

                var resultData = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["path"] = mixerPath,
                    ["name"] = mixer.name,
                    ["groupCount"] = groupInfos.Count,
                    ["groups"] = groupInfos,
                    ["exposedParameterCount"] = exposedParams.Count,
                    ["exposedParameters"] = exposedParams
                };

                if (outputGroup != null)
                {
                    resultData["outputGroup"] = outputGroup.name;
                }

                return McpResponse.Success(resultData);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to read AudioMixer: {ex.Message}");
            }
        }

        #endregion
    }
}
