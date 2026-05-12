using System;
using System.Collections.Generic;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpUnity.Server
{
    /// <summary>
    /// Baking Tools - Lightmapping, Occlusion Culling, and other baking operations
    /// Contains 6 tools for lighting and occlusion baking
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all baking-related tools
        /// </summary>
        static partial void RegisterBakingTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_bake_lighting",
                description = "Bake lightmaps for the current scene. This is a synchronous operation that may take time depending on scene complexity.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["clearFirst"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Clear existing baked data before baking (default: false)"
                        }
                    },
                    required = new List<string>()
                }
            }, BakeLighting);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_bake_lighting_async",
                description = "Start asynchronous lightmap baking. Returns immediately while baking continues in background. Use unity_get_bake_status to check progress.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["clearFirst"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Clear existing baked data before baking (default: false)"
                        }
                    },
                    required = new List<string>()
                }
            }, BakeLightingAsync);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_bake_status",
                description = "Get the current status of lightmap baking (progress, isRunning, etc.)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetBakeStatus);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_cancel_bake",
                description = "Cancel any running lightmap bake operation",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, CancelBake);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_clear_baked_data",
                description = "Clear all baked lightmap data from the current scene",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["clearDiskCache"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Also clear the GI cache on disk (default: false)"
                        }
                    },
                    required = new List<string>()
                }
            }, ClearBakedData);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_lightmap_settings",
                description = "Get current lightmap baking settings (quality, resolution, bounces, etc.)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetLightmapSettings);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_lightmap_settings",
                description = "Configure lightmap baking settings. Supports resolution, bounces, quality presets, and more.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["lightmapper"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Lightmapper to use: 'Progressive' (GPU, default), 'ProgressiveCPU', 'Enlighten'"
                        },
                        ["directSamples"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Number of direct light samples (default: 32)"
                        },
                        ["indirectSamples"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Number of indirect light samples (default: 512)"
                        },
                        ["bounces"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Number of light bounces (default: 2)"
                        },
                        ["lightmapResolution"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Texels per unit (default: 40)"
                        },
                        ["lightmapPadding"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Padding between lightmap charts in texels (default: 2)"
                        },
                        ["lightmapMaxSize"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Maximum lightmap size: 32, 64, 128, 256, 512, 1024, 2048, 4096 (default: 1024)"
                        },
                        ["compressLightmaps"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Compress lightmap textures (default: true)"
                        },
                        ["ambientOcclusion"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Enable ambient occlusion (default: false)"
                        },
                        ["aoMaxDistance"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "AO max distance (default: 1)"
                        }
                    },
                    required = new List<string>()
                }
            }, SetLightmapSettings);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_bake_occlusion",
                description = "Bake occlusion culling data for the scene. Requires Occluder Static and Occludee Static flags on objects.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["smallestOccluder"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Smallest occluder size (default: 5)"
                        },
                        ["smallestHole"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Smallest hole size (default: 0.25)"
                        },
                        ["backfaceThreshold"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Backface threshold 0-100 (default: 100)"
                        }
                    },
                    required = new List<string>()
                }
            }, BakeOcclusion);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_clear_occlusion",
                description = "Clear occlusion culling data from the scene",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, ClearOcclusion);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_bake_reflection_probes",
                description = "Bake all reflection probes in the scene",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["timeSlice"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Time slicing mode: NoTimeSlicing, AllFacesAtOnce, IndividualFaces (default: AllFacesAtOnce)"
                        }
                    },
                    required = new List<string>()
                }
            }, BakeReflectionProbes);
        }

        #region Baking Tool Handlers

        private static McpToolResult BakeLighting(Dictionary<string, object> args)
        {
            try
            {
                bool clearFirst = ArgumentParser.GetBool(args, "clearFirst", false);

                if (clearFirst)
                {
                    Lightmapping.Clear();
                }

                var startTime = DateTime.Now;
                bool success = Lightmapping.Bake();
                var duration = DateTime.Now - startTime;

                if (success)
                {
                    return McpResponse.Success("Lightmap baking completed", new
                    {
                        success = true,
                        duration = duration.TotalSeconds,
                        lightmapCount = LightmapSettings.lightmaps.Length
                    });
                }
                else
                {
                    return McpToolResult.Error("Lightmap baking failed or was cancelled");
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to bake lighting: {ex.Message}");
            }
        }

        private static McpToolResult BakeLightingAsync(Dictionary<string, object> args)
        {
            try
            {
                bool clearFirst = ArgumentParser.GetBool(args, "clearFirst", false);

                if (clearFirst)
                {
                    Lightmapping.Clear();
                }

                if (Lightmapping.isRunning)
                {
                    return McpToolResult.Error("Lightmap baking is already in progress. Use unity_cancel_bake to stop it first.");
                }

                bool started = Lightmapping.BakeAsync();

                if (started)
                {
                    return McpResponse.Success("Lightmap baking started", new
                    {
                        isRunning = true,
                        message = "Baking started. Use unity_get_bake_status to check progress."
                    });
                }
                else
                {
                    return McpToolResult.Error("Failed to start async baking");
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to start async baking: {ex.Message}");
            }
        }

        private static McpToolResult GetBakeStatus(Dictionary<string, object> args)
        {
            try
            {
                return McpResponse.Success("Bake status", new
                {
                    isRunning = Lightmapping.isRunning,
                    progress = Lightmapping.buildProgress,
                    lightmapCount = LightmapSettings.lightmaps.Length,
                    bakedGI = LightmapSettings.lightmaps.Length > 0
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get bake status: {ex.Message}");
            }
        }

        private static McpToolResult CancelBake(Dictionary<string, object> args)
        {
            try
            {
                if (!Lightmapping.isRunning)
                {
                    return McpResponse.Success("No baking operation in progress", new { wasRunning = false });
                }

                Lightmapping.Cancel();

                return McpResponse.Success("Baking cancelled", new { cancelled = true });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to cancel baking: {ex.Message}");
            }
        }

        private static McpToolResult ClearBakedData(Dictionary<string, object> args)
        {
            try
            {
                bool clearDiskCache = ArgumentParser.GetBool(args, "clearDiskCache", false);

                int previousCount = LightmapSettings.lightmaps.Length;

                Lightmapping.Clear();

                if (clearDiskCache)
                {
                    Lightmapping.ClearDiskCache();
                }

                return McpResponse.Success("Baked data cleared", new
                {
                    lightmapsCleared = previousCount,
                    diskCacheCleared = clearDiskCache
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to clear baked data: {ex.Message}");
            }
        }

        private static McpToolResult GetLightmapSettings(Dictionary<string, object> args)
        {
            try
            {
                var settings = new SerializedObject(Lightmapping.lightingSettings);

                string lightmapper = "Unknown";
                var lightmapperProp = settings.FindProperty("m_Lightmapper");
                if (lightmapperProp != null)
                {
                    switch (lightmapperProp.intValue)
                    {
                        case 0: lightmapper = "Enlighten"; break;
                        case 1: lightmapper = "ProgressiveCPU"; break;
                        case 2: lightmapper = "ProgressiveGPU"; break;
                    }
                }

                return McpResponse.Success("Lightmap settings", new
                {
                    lightmapper = lightmapper,
                    giWorkflowMode = Lightmapping.bakedGI ? "Baked" : "Realtime",
                    realtimeGI = Lightmapping.realtimeGI,
                    bakedGI = Lightmapping.bakedGI,
                    lightmapCount = LightmapSettings.lightmaps.Length,
                    lightmapsMode = LightmapSettings.lightmapsMode.ToString()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get lightmap settings: {ex.Message}");
            }
        }

        private static McpToolResult SetLightmapSettings(Dictionary<string, object> args)
        {
            try
            {
                var lightingSettings = Lightmapping.lightingSettings;
                if (lightingSettings == null)
                {
                    lightingSettings = new LightingSettings();
                    Lightmapping.lightingSettings = lightingSettings;
                }

                Undo.RecordObject(lightingSettings, "Modify Lightmap Settings");
                var modified = new List<string>();

                if (ArgumentParser.HasKey(args, "lightmapper"))
                {
                    string lightmapper = ArgumentParser.GetString(args, "lightmapper", "Progressive");
                    switch (lightmapper.ToLowerInvariant())
                    {
                        case "progressivegpu":
                        case "progressive":
                            lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                            break;
                        case "progressivecpu":
                            lightingSettings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
                            break;
                    }
                    modified.Add("lightmapper");
                }

                if (ArgumentParser.HasKey(args, "directSamples"))
                {
                    lightingSettings.directSampleCount = ArgumentParser.GetInt(args, "directSamples", 32);
                    modified.Add("directSamples");
                }

                if (ArgumentParser.HasKey(args, "indirectSamples"))
                {
                    lightingSettings.indirectSampleCount = ArgumentParser.GetInt(args, "indirectSamples", 512);
                    modified.Add("indirectSamples");
                }

                if (ArgumentParser.HasKey(args, "bounces"))
                {
                    lightingSettings.maxBounces = ArgumentParser.GetInt(args, "bounces", 2);
                    modified.Add("bounces");
                }

                if (ArgumentParser.HasKey(args, "lightmapResolution"))
                {
                    lightingSettings.lightmapResolution = ArgumentParser.GetFloat(args, "lightmapResolution", 40);
                    modified.Add("lightmapResolution");
                }

                if (ArgumentParser.HasKey(args, "lightmapPadding"))
                {
                    lightingSettings.lightmapPadding = ArgumentParser.GetInt(args, "lightmapPadding", 2);
                    modified.Add("lightmapPadding");
                }

                if (ArgumentParser.HasKey(args, "lightmapMaxSize"))
                {
                    lightingSettings.lightmapMaxSize = ArgumentParser.GetInt(args, "lightmapMaxSize", 1024);
                    modified.Add("lightmapMaxSize");
                }

                if (ArgumentParser.HasKey(args, "compressLightmaps"))
                {
                    lightingSettings.lightmapCompression = ArgumentParser.GetBool(args, "compressLightmaps", true)
                        ? LightmapCompression.NormalQuality
                        : LightmapCompression.None;
                    modified.Add("compressLightmaps");
                }

                if (ArgumentParser.HasKey(args, "ambientOcclusion"))
                {
                    lightingSettings.ao = ArgumentParser.GetBool(args, "ambientOcclusion", false);
                    modified.Add("ambientOcclusion");
                }

                if (ArgumentParser.HasKey(args, "aoMaxDistance"))
                {
                    lightingSettings.aoMaxDistance = ArgumentParser.GetFloat(args, "aoMaxDistance", 1f);
                    modified.Add("aoMaxDistance");
                }

                EditorUtility.SetDirty(lightingSettings);

                return McpResponse.Success($"Modified {modified.Count} lightmap settings", new
                {
                    modifiedSettings = modified,
                    lightmapper = lightingSettings.lightmapper.ToString()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set lightmap settings: {ex.Message}");
            }
        }

        private static McpToolResult BakeOcclusion(Dictionary<string, object> args)
        {
            try
            {
                float smallestOccluder = ArgumentParser.GetFloat(args, "smallestOccluder", 5f);
                float smallestHole = ArgumentParser.GetFloat(args, "smallestHole", 0.25f);
                float backfaceThreshold = ArgumentParser.GetFloat(args, "backfaceThreshold", 100f);

                // Set occlusion settings
                StaticOcclusionCulling.smallestOccluder = smallestOccluder;
                StaticOcclusionCulling.smallestHole = smallestHole;
                StaticOcclusionCulling.backfaceThreshold = backfaceThreshold;

                // Start computation
                bool success = StaticOcclusionCulling.Compute();

                if (success)
                {
                    return McpResponse.Success("Occlusion culling baked", new
                    {
                        success = true,
                        smallestOccluder = smallestOccluder,
                        smallestHole = smallestHole,
                        backfaceThreshold = backfaceThreshold,
                        hasData = StaticOcclusionCulling.umbraDataSize > 0,
                        dataSize = StaticOcclusionCulling.umbraDataSize
                    });
                }
                else
                {
                    return McpToolResult.Error("Occlusion culling bake failed. Ensure objects have Occluder Static or Occludee Static flags.");
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to bake occlusion: {ex.Message}");
            }
        }

        private static McpToolResult ClearOcclusion(Dictionary<string, object> args)
        {
            try
            {
                long previousSize = StaticOcclusionCulling.umbraDataSize;

                StaticOcclusionCulling.Clear();

                return McpResponse.Success("Occlusion data cleared", new
                {
                    previousDataSize = previousSize,
                    cleared = true
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to clear occlusion data: {ex.Message}");
            }
        }

        private static McpToolResult BakeReflectionProbes(Dictionary<string, object> args)
        {
            try
            {
                var timeSlice = ArgumentParser.GetEnum(args, "timeSlice", ReflectionProbeTimeSlicingMode.AllFacesAtOnce);

                // M-12: FindObjectsOfType is deprecated in Unity 6 — use FindObjectsByType instead
                var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude);

                if (probes.Length == 0)
                {
                    return McpResponse.Success("No reflection probes found in scene", new
                    {
                        probeCount = 0,
                        baked = false
                    });
                }

                int bakedCount = 0;
                var bakedProbes = new List<string>();

                foreach (var probe in probes)
                {
                    if (probe.mode == ReflectionProbeMode.Baked || probe.mode == ReflectionProbeMode.Custom)
                    {
                        // For baked probes, use Lightmapping.BakeReflectionProbe
                        if (Lightmapping.BakeReflectionProbe(probe, probe.bakedTexture != null ? AssetDatabase.GetAssetPath(probe.bakedTexture) : null))
                        {
                            bakedCount++;
                            bakedProbes.Add(probe.name);
                        }
                    }
                    else if (probe.mode == ReflectionProbeMode.Realtime)
                    {
                        // For realtime probes, render once
                        probe.RenderProbe();
                        bakedCount++;
                        bakedProbes.Add(probe.name + " (realtime)");
                    }
                }

                return McpResponse.Success($"Baked {bakedCount}/{probes.Length} reflection probes", new
                {
                    totalProbes = probes.Length,
                    bakedCount = bakedCount,
                    bakedProbes = bakedProbes,
                    timeSlicingMode = timeSlice.ToString()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to bake reflection probes: {ex.Message}");
            }
        }

        #endregion
    }
}
