using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpUnity.Server
{
    /// <summary>
    /// Project Settings Tools - 5 tools for manipulating Unity project settings
    /// Categories: quality, graphics, physics, physics2d, time, audio, render, player
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterProjectSettingsTools()
        {
            // Tool 1: Get Project Settings
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_project_settings",
                description = "SETTINGS: Get project settings by category (quality|graphics|physics|physics2d|time|audio|render|player)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["category"] = new McpPropertySchema { type = "string", description = "Settings category: quality, graphics, physics, physics2d, time, audio, render, player" },
                        ["detailed"] = new McpPropertySchema { type = "boolean", description = "Include extended info" }
                    },
                    required = new List<string> { "category" }
                }
            }, HandleGetProjectSettings);

            // Tool 2: Set Project Settings
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_project_settings",
                description = "SETTINGS: Modify project settings. Pass category and settings object",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["category"] = new McpPropertySchema { type = "string", description = "Settings category: quality, physics, physics2d, time, render" },
                        ["settings"] = new McpPropertySchema { type = "object", description = "Key-value pairs to set" }
                    },
                    required = new List<string> { "category", "settings" }
                }
            }, HandleSetProjectSettings);

            // Tool 3: Set Quality Level
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_quality_level",
                description = "SETTINGS: Set quality level by index or name",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["level"] = new McpPropertySchema { type = "integer", description = "Quality level index (0-based)" },
                        ["levelName"] = new McpPropertySchema { type = "string", description = "Or quality level name" },
                        ["applyExpensiveChanges"] = new McpPropertySchema { type = "boolean", description = "Apply AA changes" }
                    },
                    required = new List<string>()
                }
            }, HandleSetQualityLevel);

            // Tool 5: Get Physics Layer Collision
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_physics_layer_collision",
                description = "SETTINGS: Get physics layer collision matrix",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["layer1"] = new McpPropertySchema { type = "string", description = "First layer name (optional, returns full matrix if omitted)" },
                        ["layer2"] = new McpPropertySchema { type = "string", description = "Second layer name" }
                    },
                    required = new List<string>()
                }
            }, HandleGetPhysicsLayerCollision);

            // Tool 6: Set Physics Layer Collision
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_physics_layer_collision",
                description = "SETTINGS: Enable/disable collision between two physics layers",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["layer1"] = new McpPropertySchema { type = "string", description = "First layer name" },
                        ["layer2"] = new McpPropertySchema { type = "string", description = "Second layer name" },
                        ["collide"] = new McpPropertySchema { type = "boolean", description = "Enable collision between layers" }
                    },
                    required = new List<string> { "layer1", "layer2", "collide" }
                }
            }, HandleSetPhysicsLayerCollision);
        }

        #region Settings Readers

        private static Dictionary<string, object> ReadQualitySettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["currentLevel"] = QualitySettings.GetQualityLevel(),
                ["levelName"] = QualitySettings.names[QualitySettings.GetQualityLevel()],
                ["availableLevels"] = QualitySettings.names,
                ["pixelLightCount"] = QualitySettings.pixelLightCount,
                ["shadows"] = QualitySettings.shadows.ToString(),
                ["shadowResolution"] = QualitySettings.shadowResolution.ToString(),
                ["shadowDistance"] = QualitySettings.shadowDistance,
                ["antiAliasing"] = QualitySettings.antiAliasing,
                ["softParticles"] = QualitySettings.softParticles,
                ["vSyncCount"] = QualitySettings.vSyncCount,
                ["lodBias"] = QualitySettings.lodBias,
                ["maximumLODLevel"] = QualitySettings.maximumLODLevel
            };

            if (detailed)
            {
                settings["shadowCascades"] = QualitySettings.shadowCascades;
                settings["shadowProjection"] = QualitySettings.shadowProjection.ToString();
                settings["skinWeights"] = QualitySettings.skinWeights.ToString();
                settings["anisotropicFiltering"] = QualitySettings.anisotropicFiltering.ToString();
                settings["realtimeReflectionProbes"] = QualitySettings.realtimeReflectionProbes;
                settings["billboardsFaceCameraPosition"] = QualitySettings.billboardsFaceCameraPosition;
                settings["resolutionScalingFixedDPIFactor"] = QualitySettings.resolutionScalingFixedDPIFactor;
            }

            return settings;
        }

        private static Dictionary<string, object> ReadGraphicsSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["renderPipelineAsset"] = GraphicsSettings.currentRenderPipeline?.name ?? "Built-in",
                ["transparencySortMode"] = GraphicsSettings.transparencySortMode.ToString(),
                ["lightsUseLinearIntensity"] = GraphicsSettings.lightsUseLinearIntensity,
                ["lightsUseColorTemperature"] = GraphicsSettings.lightsUseColorTemperature
            };

            if (detailed)
            {
                settings["logWhenShaderIsCompiled"] = GraphicsSettings.logWhenShaderIsCompiled;
                // defaultRenderingLayerMask is deprecated in Unity 6 but the replacement
                // (RenderingLayerMask) is SRP-specific and not universally available.
#pragma warning disable 0618
                settings["defaultRenderingLayerMask"] = GraphicsSettings.defaultRenderingLayerMask;
#pragma warning restore 0618
            }

            return settings;
        }

        private static Dictionary<string, object> ReadPhysicsSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["gravity"] = new Dictionary<string, object> { ["x"] = Physics.gravity.x, ["y"] = Physics.gravity.y, ["z"] = Physics.gravity.z },
                ["defaultSolverIterations"] = Physics.defaultSolverIterations,
                ["defaultSolverVelocityIterations"] = Physics.defaultSolverVelocityIterations,
                ["bounceThreshold"] = Physics.bounceThreshold,
                ["defaultContactOffset"] = Physics.defaultContactOffset,
                ["autoSimulation"] = Physics.simulationMode.ToString()
            };

            if (detailed)
            {
                settings["sleepThreshold"] = Physics.sleepThreshold;
                settings["queriesHitTriggers"] = Physics.queriesHitTriggers;
                settings["queriesHitBackfaces"] = Physics.queriesHitBackfaces;
            }

            return settings;
        }

        private static Dictionary<string, object> ReadPhysics2DSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["gravity"] = new Dictionary<string, object> { ["x"] = Physics2D.gravity.x, ["y"] = Physics2D.gravity.y },
                ["defaultContactOffset"] = Physics2D.defaultContactOffset,
                ["velocityIterations"] = Physics2D.velocityIterations,
                ["positionIterations"] = Physics2D.positionIterations,
                ["autoSimulation"] = Physics2D.simulationMode.ToString()
            };

            if (detailed)
            {
                settings["queriesHitTriggers"] = Physics2D.queriesHitTriggers;
                settings["queriesStartInColliders"] = Physics2D.queriesStartInColliders;
                settings["callbacksOnDisable"] = Physics2D.callbacksOnDisable;
            }

            return settings;
        }

        private static Dictionary<string, object> ReadTimeSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["timeScale"] = Time.timeScale,
                ["fixedDeltaTime"] = Time.fixedDeltaTime,
                ["maximumDeltaTime"] = Time.maximumDeltaTime,
                ["maximumParticleDeltaTime"] = Time.maximumParticleDeltaTime
            };

            if (detailed)
            {
                settings["time"] = Time.time;
                settings["realtimeSinceStartup"] = Time.realtimeSinceStartup;
                settings["frameCount"] = Time.frameCount;
                settings["captureDeltaTime"] = Time.captureDeltaTime;
            }

            return settings;
        }

        private static Dictionary<string, object> ReadAudioSettings(bool detailed = false)
        {
            var config = AudioSettings.GetConfiguration();
            var settings = new Dictionary<string, object>
            {
                ["sampleRate"] = config.sampleRate,
                ["speakerMode"] = config.speakerMode.ToString(),
                ["dspBufferSize"] = config.dspBufferSize,
                ["numRealVoices"] = config.numRealVoices,
                ["numVirtualVoices"] = config.numVirtualVoices
            };

            if (detailed)
            {
                settings["outputSampleRate"] = AudioSettings.outputSampleRate;
                settings["driverCapabilities"] = AudioSettings.driverCapabilities.ToString();
            }

            return settings;
        }

        private static Dictionary<string, object> ReadRenderSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["fog"] = RenderSettings.fog,
                ["fogMode"] = RenderSettings.fogMode.ToString(),
                ["fogColor"] = new Dictionary<string, object> { ["r"] = RenderSettings.fogColor.r, ["g"] = RenderSettings.fogColor.g, ["b"] = RenderSettings.fogColor.b, ["a"] = RenderSettings.fogColor.a },
                ["fogDensity"] = RenderSettings.fogDensity,
                ["fogStartDistance"] = RenderSettings.fogStartDistance,
                ["fogEndDistance"] = RenderSettings.fogEndDistance,
                ["ambientMode"] = RenderSettings.ambientMode.ToString(),
                ["ambientIntensity"] = RenderSettings.ambientIntensity
            };

            if (detailed)
            {
                settings["ambientLight"] = new Dictionary<string, object> { ["r"] = RenderSettings.ambientLight.r, ["g"] = RenderSettings.ambientLight.g, ["b"] = RenderSettings.ambientLight.b, ["a"] = RenderSettings.ambientLight.a };
                settings["ambientSkyColor"] = new Dictionary<string, object> { ["r"] = RenderSettings.ambientSkyColor.r, ["g"] = RenderSettings.ambientSkyColor.g, ["b"] = RenderSettings.ambientSkyColor.b, ["a"] = RenderSettings.ambientSkyColor.a };
                settings["subtractiveShadowColor"] = new Dictionary<string, object> { ["r"] = RenderSettings.subtractiveShadowColor.r, ["g"] = RenderSettings.subtractiveShadowColor.g, ["b"] = RenderSettings.subtractiveShadowColor.b, ["a"] = RenderSettings.subtractiveShadowColor.a };
                settings["skybox"] = RenderSettings.skybox?.name ?? "None";
                settings["sun"] = RenderSettings.sun?.name ?? "None";
                settings["reflectionIntensity"] = RenderSettings.reflectionIntensity;
                settings["reflectionBounces"] = RenderSettings.reflectionBounces;
            }

            return settings;
        }

        private static Dictionary<string, object> ReadPlayerSettings(bool detailed = false)
        {
            var settings = new Dictionary<string, object>
            {
                ["companyName"] = PlayerSettings.companyName,
                ["productName"] = PlayerSettings.productName,
                ["bundleVersion"] = PlayerSettings.bundleVersion,
                ["defaultScreenWidth"] = PlayerSettings.defaultScreenWidth,
                ["defaultScreenHeight"] = PlayerSettings.defaultScreenHeight,
                ["fullScreenMode"] = PlayerSettings.fullScreenMode.ToString(),
                ["colorSpace"] = PlayerSettings.colorSpace.ToString()
            };

            if (detailed)
            {
                settings["runInBackground"] = PlayerSettings.runInBackground;
                settings["visibleInBackground"] = PlayerSettings.visibleInBackground;
                settings["resizableWindow"] = PlayerSettings.resizableWindow;
                settings["allowFullscreenSwitch"] = PlayerSettings.allowFullscreenSwitch;
                settings["forceSingleInstance"] = PlayerSettings.forceSingleInstance;
                settings["apiCompatibilityLevel"] = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString();
            }

            return settings;
        }

        #endregion

        #region Settings Writers

        private static bool SetQualitySetting(string key, object value)
        {
            switch (key.ToLower())
            {
                case "pixellightcount":
                    QualitySettings.pixelLightCount = Convert.ToInt32(value);
                    return true;
                case "shadows":
                    QualitySettings.shadows = (ShadowQuality)Enum.Parse(typeof(ShadowQuality), value.ToString(), true);
                    return true;
                case "shadowresolution":
                    QualitySettings.shadowResolution = (ShadowResolution)Enum.Parse(typeof(ShadowResolution), value.ToString(), true);
                    return true;
                case "shadowdistance":
                    QualitySettings.shadowDistance = Convert.ToSingle(value);
                    return true;
                case "antialiasing":
                    QualitySettings.antiAliasing = Convert.ToInt32(value);
                    return true;
                case "softparticles":
                    QualitySettings.softParticles = Convert.ToBoolean(value);
                    return true;
                case "vsynccount":
                    QualitySettings.vSyncCount = Convert.ToInt32(value);
                    return true;
                case "lodbias":
                    QualitySettings.lodBias = Convert.ToSingle(value);
                    return true;
                case "maximumlodlevel":
                    QualitySettings.maximumLODLevel = Convert.ToInt32(value);
                    return true;
                default:
                    return false;
            }
        }

        private static bool SetPhysicsSetting(string key, object value)
        {
            switch (key.ToLower())
            {
                case "gravity":
                    if (value is Dictionary<string, object> grav)
                    {
                        Physics.gravity = new Vector3(
                            grav.ContainsKey("x") ? Convert.ToSingle(grav["x"]) : Physics.gravity.x,
                            grav.ContainsKey("y") ? Convert.ToSingle(grav["y"]) : Physics.gravity.y,
                            grav.ContainsKey("z") ? Convert.ToSingle(grav["z"]) : Physics.gravity.z
                        );
                        return true;
                    }
                    return false;
                case "defaultsolveriterations":
                    Physics.defaultSolverIterations = Convert.ToInt32(value);
                    return true;
                case "defaultsolvervelocityiterations":
                    Physics.defaultSolverVelocityIterations = Convert.ToInt32(value);
                    return true;
                case "bouncethreshold":
                    Physics.bounceThreshold = Convert.ToSingle(value);
                    return true;
                case "defaultcontactoffset":
                    Physics.defaultContactOffset = Convert.ToSingle(value);
                    return true;
                case "autosynctransforms":
                    // Deprecated in Unity 6 — sync is now automatic
                    return true;
                default:
                    return false;
            }
        }

        private static bool SetPhysics2DSetting(string key, object value)
        {
            switch (key.ToLower())
            {
                case "gravity":
                    if (value is Dictionary<string, object> grav)
                    {
                        Physics2D.gravity = new Vector2(
                            grav.ContainsKey("x") ? Convert.ToSingle(grav["x"]) : Physics2D.gravity.x,
                            grav.ContainsKey("y") ? Convert.ToSingle(grav["y"]) : Physics2D.gravity.y
                        );
                        return true;
                    }
                    return false;
                case "defaultcontactoffset":
                    Physics2D.defaultContactOffset = Convert.ToSingle(value);
                    return true;
                case "velocityiterations":
                    Physics2D.velocityIterations = Convert.ToInt32(value);
                    return true;
                case "positioniterations":
                    Physics2D.positionIterations = Convert.ToInt32(value);
                    return true;
                default:
                    return false;
            }
        }

        private static bool SetTimeSetting(string key, object value)
        {
            switch (key.ToLower())
            {
                case "timescale":
                    Time.timeScale = Convert.ToSingle(value);
                    return true;
                case "fixeddeltatime":
                    Time.fixedDeltaTime = Convert.ToSingle(value);
                    return true;
                case "maximumdeltatime":
                    Time.maximumDeltaTime = Convert.ToSingle(value);
                    return true;
                case "maximumparticledeltatime":
                    Time.maximumParticleDeltaTime = Convert.ToSingle(value);
                    return true;
                default:
                    return false;
            }
        }

        private static bool SetRenderSetting(string key, object value)
        {
            switch (key.ToLower())
            {
                case "fog":
                    RenderSettings.fog = Convert.ToBoolean(value);
                    return true;
                case "fogmode":
                    RenderSettings.fogMode = (FogMode)Enum.Parse(typeof(FogMode), value.ToString(), true);
                    return true;
                case "fogcolor":
                    if (value is Dictionary<string, object> col)
                    {
                        RenderSettings.fogColor = new Color(
                            col.ContainsKey("r") ? Convert.ToSingle(col["r"]) : 0.5f,
                            col.ContainsKey("g") ? Convert.ToSingle(col["g"]) : 0.5f,
                            col.ContainsKey("b") ? Convert.ToSingle(col["b"]) : 0.5f,
                            col.ContainsKey("a") ? Convert.ToSingle(col["a"]) : 1f
                        );
                        return true;
                    }
                    return false;
                case "fogdensity":
                    RenderSettings.fogDensity = Convert.ToSingle(value);
                    return true;
                case "fogstartdistance":
                    RenderSettings.fogStartDistance = Convert.ToSingle(value);
                    return true;
                case "fogenddistance":
                    RenderSettings.fogEndDistance = Convert.ToSingle(value);
                    return true;
                case "ambientmode":
                    RenderSettings.ambientMode = (AmbientMode)Enum.Parse(typeof(AmbientMode), value.ToString(), true);
                    return true;
                case "ambientintensity":
                    RenderSettings.ambientIntensity = Convert.ToSingle(value);
                    return true;
                case "ambientlight":
                    if (value is Dictionary<string, object> light)
                    {
                        RenderSettings.ambientLight = new Color(
                            light.ContainsKey("r") ? Convert.ToSingle(light["r"]) : 0.2f,
                            light.ContainsKey("g") ? Convert.ToSingle(light["g"]) : 0.2f,
                            light.ContainsKey("b") ? Convert.ToSingle(light["b"]) : 0.2f,
                            light.ContainsKey("a") ? Convert.ToSingle(light["a"]) : 1f
                        );
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        #region Tool Handlers

        private static McpToolResult HandleGetProjectSettings(Dictionary<string, object> args)
        {
            // SEC-#435: consistent use of ArgumentParser — handles null, missing keys, and
            // type mismatches instead of raw dict access that throws on bad input.
            var category = (ArgumentParser.GetString(args, "category", "quality") ?? "quality").ToLower();
            var detailed = ArgumentParser.GetBool(args, "detailed", false);

            Dictionary<string, object> settings;

            switch (category)
            {
                case "quality":
                    settings = ReadQualitySettings(detailed);
                    break;
                case "graphics":
                    settings = ReadGraphicsSettings(detailed);
                    break;
                case "physics":
                    settings = ReadPhysicsSettings(detailed);
                    break;
                case "physics2d":
                    settings = ReadPhysics2DSettings(detailed);
                    break;
                case "time":
                    settings = ReadTimeSettings(detailed);
                    break;
                case "audio":
                    settings = ReadAudioSettings(detailed);
                    break;
                case "render":
                    settings = ReadRenderSettings(detailed);
                    break;
                case "player":
                    settings = ReadPlayerSettings(detailed);
                    break;
                default:
                    return McpToolResult.Error($"Unknown category: {category}. Valid: quality, graphics, physics, physics2d, time, audio, render, player");
            }

            var result = new Dictionary<string, object>
            {
                ["category"] = category,
                ["settings"] = settings
            };

            if (category == "render")
            {
                result["note"] = "RenderSettings are per-scene";
            }

            return McpResponse.Success(result);
        }

        private static McpToolResult HandleSetProjectSettings(Dictionary<string, object> args)
        {
            // SEC-#435: consistent ArgumentParser usage.
            var category = (ArgumentParser.GetString(args, "category", "") ?? "").ToLower();
            args.TryGetValue("settings", out var settingsObj);

            if (settingsObj == null || !(settingsObj is Dictionary<string, object> settings))
            {
                return McpToolResult.Error("Settings object is required");
            }

            var modified = new List<string>();
            var failed = new List<string>();

            foreach (var kvp in settings)
            {
                bool success = false;

                switch (category)
                {
                    case "quality":
                        success = SetQualitySetting(kvp.Key, kvp.Value);
                        break;
                    case "physics":
                        success = SetPhysicsSetting(kvp.Key, kvp.Value);
                        break;
                    case "physics2d":
                        success = SetPhysics2DSetting(kvp.Key, kvp.Value);
                        break;
                    case "time":
                        success = SetTimeSetting(kvp.Key, kvp.Value);
                        break;
                    case "render":
                        success = SetRenderSetting(kvp.Key, kvp.Value);
                        break;
                    default:
                        return McpToolResult.Error($"Category '{category}' is read-only or not supported for modification");
                }

                if (success)
                    modified.Add(kvp.Key);
                else
                    failed.Add(kvp.Key);
            }

            return McpResponse.Success($"Modified {modified.Count} settings", new Dictionary<string, object>
            {
                ["modified"] = modified,
                ["failed"] = failed
            });
        }

        private static McpToolResult HandleSetQualityLevel(Dictionary<string, object> args)
        {
            // SEC-#435: default true for applyExpensiveChanges when absent, via ArgumentParser.
            var applyExpensive = ArgumentParser.GetBool(args, "applyExpensiveChanges", true);

            if (ArgumentParser.HasKey(args, "level"))
            {
                var level = ArgumentParser.GetInt(args, "level", 0);
                if (level < 0 || level >= QualitySettings.names.Length)
                {
                    return McpToolResult.Error($"Level {level} out of range. Valid: 0-{QualitySettings.names.Length - 1}");
                }

                QualitySettings.SetQualityLevel(level, applyExpensive);
                return McpResponse.Success($"Quality level set to {level}: {QualitySettings.names[level]}");
            }
            else if (ArgumentParser.HasKey(args, "levelName"))
            {
                // FIX-#435: route through ArgumentParser for null/type safety.
                var name = ArgumentParser.GetString(args, "levelName", "") ?? "";
                var names = QualitySettings.names;
                var index = Array.FindIndex(names, n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    return McpToolResult.Error($"Level '{name}' not found. Available: {string.Join(", ", names)}");
                }

                QualitySettings.SetQualityLevel(index, applyExpensive);
                return McpResponse.Success($"Quality level set to {index}: {names[index]}");
            }

            return McpToolResult.Error("Either 'level' (index) or 'levelName' is required");
        }

        private static McpToolResult HandleGetPhysicsLayerCollision(Dictionary<string, object> args)
        {
            // FIX-#435: ArgumentParser handles null entries and type coercion uniformly.
            var layer1Name = ArgumentParser.GetString(args, "layer1", null);
            var layer2Name = ArgumentParser.GetString(args, "layer2", null);

            // If specific layers requested
            if (!string.IsNullOrEmpty(layer1Name) && !string.IsNullOrEmpty(layer2Name))
            {
                var layer1 = LayerMask.NameToLayer(layer1Name);
                var layer2 = LayerMask.NameToLayer(layer2Name);

                if (layer1 < 0) return McpToolResult.Error($"Layer '{layer1Name}' not found");
                if (layer2 < 0) return McpToolResult.Error($"Layer '{layer2Name}' not found");

                var collides = !Physics.GetIgnoreLayerCollision(layer1, layer2);

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["layer1"] = new Dictionary<string, object> { ["name"] = layer1Name, ["index"] = layer1 },
                    ["layer2"] = new Dictionary<string, object> { ["name"] = layer2Name, ["index"] = layer2 },
                    ["collides"] = collides
                });
            }

            // Return full matrix
            var matrix = new Dictionary<string, Dictionary<string, bool>>();
            var layers = new List<Dictionary<string, object>>();

            for (int i = 0; i < 32; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name)) continue;

                layers.Add(new Dictionary<string, object> { ["index"] = i, ["name"] = name });
                matrix[name] = new Dictionary<string, bool>();

                for (int j = 0; j < 32; j++)
                {
                    var name2 = LayerMask.LayerToName(j);
                    if (string.IsNullOrEmpty(name2)) continue;

                    matrix[name][name2] = !Physics.GetIgnoreLayerCollision(i, j);
                }
            }

            return McpResponse.Success(new Dictionary<string, object>
            {
                ["layers"] = layers,
                ["collisionMatrix"] = matrix
            });
        }

        private static McpToolResult HandleSetPhysicsLayerCollision(Dictionary<string, object> args)
        {
            // FIX-#435: consistent ArgumentParser usage.
            var layer1Name = ArgumentParser.GetString(args, "layer1", "") ?? "";
            var layer2Name = ArgumentParser.GetString(args, "layer2", "") ?? "";
            var collide = ArgumentParser.GetBool(args, "collide", true);

            var layer1 = LayerMask.NameToLayer(layer1Name);
            var layer2 = LayerMask.NameToLayer(layer2Name);

            if (layer1 < 0) return McpToolResult.Error($"Layer '{layer1Name}' not found");
            if (layer2 < 0) return McpToolResult.Error($"Layer '{layer2Name}' not found");

            // FIX-#384: record the DynamicsManager asset before mutating Physics layer matrix
            // so Ctrl+Z reverts the change. Physics.IgnoreLayerCollision writes into
            // ProjectSettings/DynamicsManager.asset which is a regular Unity Object.
            try
            {
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
                if (assets != null && assets.Length > 0 && assets[0] != null)
                {
                    UnityEditor.Undo.RecordObject(assets[0], "MCP Set Physics Layer Collision");
                }
            }
            catch { /* best-effort — fall through to the actual change */ }

            // ignoreLayerCollision is the opposite of collide
            Physics.IgnoreLayerCollision(layer1, layer2, !collide);

            var message = collide
                ? $"Enabled collision between '{layer1Name}' and '{layer2Name}'"
                : $"Disabled collision between '{layer1Name}' and '{layer2Name}'";

            return McpResponse.Success(message, new Dictionary<string, object>
            {
                ["layer1"] = new Dictionary<string, object> { ["name"] = layer1Name, ["index"] = layer1 },
                ["layer2"] = new Dictionary<string, object> { ["name"] = layer2Name, ["index"] = layer2 },
                ["collides"] = collide
            });
        }

        #endregion
    }
}
