using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using McpUnity.Editor;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Animator Controller info tools: get controller info, parameters, set/add parameters.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorControllerInfoTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_animator_controller",
                description = "Get Animator Controller info. Use layerIndex to target one layer, compact=true to omit transitions (saves tokens on large controllers).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string",  description = "Path to the AnimatorController asset" },
                        ["gameObjectPath"] = new McpPropertySchema { type = "string",  description = "Path to a GameObject with Animator (alternative to controllerPath)" },
                        ["layerIndex"]     = new McpPropertySchema { type = "integer", description = "Only return this layer (-1 = all layers, default: -1)" },
                        ["compact"]        = new McpPropertySchema { type = "boolean", description = "Omit transitions from output to save tokens (default: false)" }
                    }
                }
            }, GetAnimatorController);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_animator_parameters",
                description = "Get runtime parameter values from an Animator component on a GameObject",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject with Animator component" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, GetAnimatorParameters);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_animator_parameter",
                description = "Set a runtime parameter value on an Animator component",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject with Animator component" },
                        ["parameterName"] = new McpPropertySchema { type = "string", description = "Name of the parameter to set" },
                        ["value"] = new McpPropertySchema { description = "Value to set (type depends on parameter type)" },
                        ["parameterType"] = new McpPropertySchema { type = "string", description = "Parameter type: Float, Int, Bool, or Trigger (optional, auto-detected)" }
                    },
                    required = new List<string> { "gameObjectPath", "parameterName" }
                }
            }, SetAnimatorParameter);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_animator_controller",
                description = "Create a new AnimatorController asset at the specified path",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Asset path for the controller (e.g. 'Assets/Animations/Player.controller')" }
                    },
                    required = new List<string> { "savePath" }
                }
            }, CreateAnimatorController);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_remove_animator_parameter",
                description = "Remove a parameter from an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["parameterName"]  = new McpPropertySchema { type = "string", description = "Name of the parameter to remove" }
                    },
                    required = new List<string> { "controllerPath", "parameterName" }
                }
            }, RemoveAnimatorParameter);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_animator_layer",
                description = "Add a new layer to an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"]  = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["layerName"]       = new McpPropertySchema { type = "string", description = "Name for the new layer" },
                        ["defaultWeight"]   = new McpPropertySchema { type = "number",  description = "Layer weight 0-1 (default: 1.0)" },
                        ["avatarMaskPath"]  = new McpPropertySchema { type = "string", description = "Optional: path to AvatarMask asset" }
                    },
                    required = new List<string> { "controllerPath", "layerName" }
                }
            }, AddAnimatorLayer);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_animator_parameter",
                description = "Add a new parameter to an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["parameterName"] = new McpPropertySchema { type = "string", description = "Name of the new parameter" },
                        ["parameterType"] = new McpPropertySchema { type = "string", description = "Type: Float, Int, Bool, or Trigger" },
                        ["defaultValue"] = new McpPropertySchema { description = "Default value for the parameter (optional)" }
                    },
                    required = new List<string> { "controllerPath", "parameterName", "parameterType" }
                }
            }, AddAnimatorParameter);
        }

        #region Animator Controller Info Helpers

        internal static AnimatorController LoadAnimatorController(string controllerPath, string gameObjectPath)
        {
            if (!string.IsNullOrEmpty(controllerPath))
            {
                try
                {
                    controllerPath = SanitizePath(controllerPath);
                }
                catch (ArgumentException ex)
                {
                    McpUnity.Editor.McpDebug.LogWarning($"[Animator] Invalid controller path '{controllerPath}': {ex.Message}");
                    return null;
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller != null) return controller;
            }

            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                var go = GameObjectHelpers.FindGameObject(gameObjectPath);
                if (go != null)
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        var path = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                        if (!string.IsNullOrEmpty(path))
                        {
                            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                        }
                    }
                }
            }

            return null;
        }

        internal static AnimatorState FindStateByName(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.name == stateName)
                    return state.state;
            }

            foreach (var subMachine in stateMachine.stateMachines)
            {
                var found = FindStateByName(subMachine.stateMachine, stateName);
                if (found != null) return found;
            }

            return null;
        }

        internal static AnimatorConditionMode ParseConditionMode(string mode)
        {
            switch (mode?.ToLower())
            {
                case "greater": return AnimatorConditionMode.Greater;
                case "less": return AnimatorConditionMode.Less;
                case "equals": return AnimatorConditionMode.Equals;
                case "notequal": return AnimatorConditionMode.NotEqual;
                case "if": return AnimatorConditionMode.If;
                case "ifnot": return AnimatorConditionMode.IfNot;
                default: return AnimatorConditionMode.If;
            }
        }

        /// <summary>
        /// Serialize an AnimatorController to a JSON-friendly dictionary.
        /// </summary>
        /// <param name="controller">The controller to serialize.</param>
        /// <param name="targetLayerIndex">-1 returns all layers; ≥0 returns only that layer.</param>
        /// <param name="compact">When true, omits transitions to reduce output size.</param>
        internal static Dictionary<string, object> SerializeAnimatorController(
            AnimatorController controller,
            int  targetLayerIndex = -1,
            bool compact = false)
        {
            var result = new Dictionary<string, object>
            {
                ["name"]       = controller.name,
                ["assetPath"]  = AssetDatabase.GetAssetPath(controller),
                ["layerCount"] = controller.layers.Length
            };

            var parameters = new List<Dictionary<string, object>>();
            foreach (var param in controller.parameters)
            {
                var paramInfo = new Dictionary<string, object>
                {
                    ["name"] = param.name,
                    ["type"] = param.type.ToString()
                };

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float: paramInfo["defaultValue"] = param.defaultFloat; break;
                    case AnimatorControllerParameterType.Int:   paramInfo["defaultValue"] = param.defaultInt;   break;
                    case AnimatorControllerParameterType.Bool:  paramInfo["defaultValue"] = param.defaultBool;  break;
                }
                parameters.Add(paramInfo);
            }
            result["parameters"] = parameters;

            var layers = new List<Dictionary<string, object>>();
            for (int i = 0; i < controller.layers.Length; i++)
            {
                // Skip layers not matching the requested index
                if (targetLayerIndex >= 0 && i != targetLayerIndex) continue;

                var layer = controller.layers[i];
                var layerInfo = new Dictionary<string, object>
                {
                    ["index"]        = i,
                    ["name"]         = layer.name,
                    ["defaultWeight"] = layer.defaultWeight,
                    ["defaultState"] = layer.stateMachine.defaultState?.name
                };

                var states = new List<Dictionary<string, object>>();
                var transitions = compact ? null : new List<Dictionary<string, object>>();

                SerializeStateMachine(layer.stateMachine, states, transitions, layer.stateMachine.defaultState?.name);

                layerInfo["states"]      = states;
                layerInfo["stateCount"]  = states.Count;

                if (!compact)
                {
                    layerInfo["transitions"]         = transitions;
                    layerInfo["transitionCount"]     = transitions.Count;
                    layerInfo["anyStateTransitions"] = SerializeAnyStateTransitions(layer.stateMachine);
                }

                layers.Add(layerInfo);
            }
            result["layers"] = layers;

            if (compact)
                result["note"] = "compact=true — transitions omitted. Use compact=false to include them.";

            return result;
        }

        // transitions may be null when compact=true — guard all additions
        internal static void SerializeStateMachine(AnimatorStateMachine sm, List<Dictionary<string, object>> states,
            List<Dictionary<string, object>> transitions, string defaultStateName)
        {
            foreach (var childState in sm.states)
            {
                var state = childState.state;
                var stateInfo = new Dictionary<string, object>
                {
                    ["name"]      = state.name,
                    ["position"]  = new Dictionary<string, object> { ["x"] = childState.position.x, ["y"] = childState.position.y },
                    ["isDefault"] = state.name == defaultStateName,
                    ["speed"]     = state.speed,
                    ["motion"]    = state.motion?.name
                };
                states.Add(stateInfo);

                if (transitions != null)
                {
                    foreach (var transition in state.transitions)
                    {
                        transitions.Add(new Dictionary<string, object>
                        {
                            ["from"]        = state.name,
                            ["to"]          = transition.destinationState?.name ?? transition.destinationStateMachine?.name ?? "Exit",
                            ["hasExitTime"] = transition.hasExitTime,
                            ["exitTime"]    = transition.exitTime,
                            ["duration"]    = transition.duration,
                            ["conditions"]  = SerializeConditions(transition.conditions)
                        });
                    }
                }
            }

            foreach (var subMachine in sm.stateMachines)
            {
                SerializeStateMachine(subMachine.stateMachine, states, transitions, defaultStateName);
            }
        }

        internal static List<Dictionary<string, object>> SerializeAnyStateTransitions(AnimatorStateMachine sm)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var transition in sm.anyStateTransitions)
            {
                result.Add(new Dictionary<string, object>
                {
                    ["from"] = "Any",
                    ["to"] = transition.destinationState?.name ?? "Unknown",
                    ["hasExitTime"] = transition.hasExitTime,
                    ["duration"] = transition.duration,
                    ["conditions"] = SerializeConditions(transition.conditions)
                });
            }
            return result;
        }

        internal static List<Dictionary<string, object>> SerializeConditions(AnimatorCondition[] conditions)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var cond in conditions)
            {
                result.Add(new Dictionary<string, object>
                {
                    ["parameter"] = cond.parameter,
                    ["mode"] = cond.mode.ToString(),
                    ["threshold"] = cond.threshold
                });
            }
            return result;
        }

        #endregion

        #region Shared Helper

        /// <summary>
        /// Load an AnimatorController and validate a layer index in one call.
        /// Eliminates the identical 3-line boilerplate repeated across all animator tool handlers.
        /// </summary>
        internal static bool TryGetStateMachine(
            string controllerPath,
            int    layerIndex,
            out AnimatorController controller,
            out AnimatorStateMachine stateMachine,
            out string error)
        {
            controller    = null;
            stateMachine  = null;
            error         = null;

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                error = $"AnimatorController not found: '{controllerPath}'";
                return false;
            }

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                error = $"Layer index {layerIndex} out of range — controller has {controller.layers.Length} layer(s)";
                return false;
            }

            stateMachine = controller.layers[layerIndex].stateMachine;
            return true;
        }

        #endregion

        #region Animator Controller Creation / Management Handlers

        private static McpToolResult CreateAnimatorController(Dictionary<string, object> args)
        {
            var (rawPath, savePathErr) = RequireArg(args, "savePath");
            if (savePathErr != null) return savePathErr;

            var (savePath, sanitizeErr) = TrySanitizePath(rawPath, "path");
            if (sanitizeErr != null) return sanitizeErr;

            if (!savePath.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
                savePath += ".controller";

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(savePath) != null)
                return McpToolResult.Error($"AnimatorController already exists at: {savePath}");

            var dir = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(savePath);
            if (controller == null)
                return McpToolResult.Error($"Failed to create AnimatorController at: {savePath}");

            AssetDatabase.Refresh();

            return McpResponse.Success($"Created AnimatorController at {savePath}", new
            {
                path             = savePath,
                name             = controller.name,
                layerCount       = controller.layers.Length,
                defaultLayerName = controller.layers.Length > 0 ? controller.layers[0].name : "Base Layer"
            });
        }

        private static McpToolResult RemoveAnimatorParameter(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (parameterName, parameterNameErr) = RequireArg(args, "parameterName");
            if (parameterNameErr != null) return parameterNameErr;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            var paramList = new List<AnimatorControllerParameter>(controller.parameters);
            int idx = paramList.FindIndex(p => p.name == parameterName);
            if (idx < 0)
                return McpToolResult.Error($"Parameter '{parameterName}' not found in controller '{controller.name}'");

            string removedType = paramList[idx].type.ToString();

            Undo.RecordObject(controller, "Remove Animator Parameter");
            controller.RemoveParameter(idx);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Removed parameter '{parameterName}' ({removedType})", new
            {
                parameterName  = parameterName,
                parameterType  = removedType,
                remainingCount = controller.parameters.Length
            });
        }

        private static McpToolResult AddAnimatorLayer(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (layerName, layerNameErr) = RequireArg(args, "layerName");
            if (layerNameErr != null) return layerNameErr;

            float defaultWeight = ArgumentParser.GetFloatClamped(args, "defaultWeight", 1.0f, 0f, 1f);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            foreach (var l in controller.layers)
                if (l.name == layerName)
                    return McpToolResult.Error($"Layer '{layerName}' already exists in controller");

            Undo.RecordObject(controller, "Add Animator Layer");
            controller.AddLayer(layerName);

            // Apply weight on the newly created last layer
            var layers = controller.layers;
            var newLayer = layers[layers.Length - 1];
            newLayer.defaultWeight = defaultWeight;

            // Optional AvatarMask
            string avatarMaskPath = ArgumentParser.GetString(args, "avatarMaskPath");
            if (!string.IsNullOrEmpty(avatarMaskPath))
            {
                var mask = AssetDatabase.LoadAssetAtPath<UnityEngine.AvatarMask>(avatarMaskPath);
                if (mask != null)
                    newLayer.avatarMask = mask;
                else
                    McpDebug.LogWarning($"[MCP Animator] AvatarMask not found at '{avatarMaskPath}' — layer created without mask.");
            }

            layers[layers.Length - 1] = newLayer;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added layer '{layerName}' to {controller.name}", new
            {
                layerName     = layerName,
                layerIndex    = layers.Length - 1,
                defaultWeight = defaultWeight,
                totalLayers   = layers.Length
            });
        }

        #endregion

        #region Animator Controller Info Handlers

        private static McpToolResult GetAnimatorController(Dictionary<string, object> args)
        {
            var controllerPath = ArgumentParser.GetString(args, "controllerPath");
            var gameObjectPath = ArgumentParser.GetString(args, "gameObjectPath");

            if (string.IsNullOrEmpty(controllerPath) && string.IsNullOrEmpty(gameObjectPath))
                return McpToolResult.Error("Either controllerPath or gameObjectPath is required");

            var controller = LoadAnimatorController(controllerPath, gameObjectPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found. Path: '{controllerPath}', GameObject: '{gameObjectPath}'");

            int  targetLayer = ArgumentParser.GetInt(args,  "layerIndex", -1);
            bool compact     = ArgumentParser.GetBool(args, "compact",    false);

            var serialized = SerializeAnimatorController(controller, targetLayer, compact);

            return McpResponse.Success(serialized);
        }

        private static McpToolResult GetAnimatorParameters(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args, "gameObjectPath");
            if (goErr != null) return goErr;

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return McpToolResult.Error($"No Animator component on: {gameObjectPath}");

            var parameters = new List<Dictionary<string, object>>();
            foreach (var param in animator.parameters)
            {
                var paramInfo = new Dictionary<string, object>
                {
                    ["name"] = param.name,
                    ["type"] = param.type.ToString()
                };

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        paramInfo["value"] = animator.GetFloat(param.name);
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramInfo["value"] = animator.GetInteger(param.name);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        paramInfo["value"] = animator.GetBool(param.name);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        paramInfo["value"] = null;
                        break;
                }

                parameters.Add(paramInfo);
            }

            return McpResponse.Success(new
            {
                gameObject = gameObjectPath,
                parameterCount = parameters.Count,
                parameters = parameters
            });
        }

        private static McpToolResult SetAnimatorParameter(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args, "gameObjectPath");
            if (goErr != null) return goErr;

            var (parameterName, parameterNameErr) = RequireArg(args, "parameterName");
            if (parameterNameErr != null) return parameterNameErr;

            var valueObj = args.GetValueOrDefault("value");
            var parameterType = ArgumentParser.GetString(args, "parameterType");

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return McpToolResult.Error($"No Animator component on: {gameObjectPath}");

            UnityEngine.AnimatorControllerParameter foundParam = null;
            foreach (var param in animator.parameters)
            {
                if (param.name == parameterName)
                {
                    foundParam = param;
                    break;
                }
            }

            if (foundParam == null)
                return McpToolResult.Error($"Parameter '{parameterName}' not found on Animator");

            var type = !string.IsNullOrEmpty(parameterType) ? parameterType : foundParam.type.ToString();

            try
            {
                switch (type.ToLower())
                {
                    case "float":
                        if (!float.TryParse(valueObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatVal))
                            return McpToolResult.Error($"Invalid float value: {valueObj}");
                        animator.SetFloat(parameterName, floatVal);
                        return McpToolResult.Success($"Set {parameterName} = {floatVal} (Float)");

                    case "int":
                    case "integer":
                        if (!int.TryParse(valueObj?.ToString(), out var intVal))
                            return McpToolResult.Error($"Invalid int value: {valueObj}");
                        animator.SetInteger(parameterName, intVal);
                        return McpToolResult.Success($"Set {parameterName} = {intVal} (Int)");

                    case "bool":
                    case "boolean":
                        if (!bool.TryParse(valueObj?.ToString(), out var boolVal))
                            return McpToolResult.Error($"Invalid bool value: {valueObj}");
                        animator.SetBool(parameterName, boolVal);
                        return McpToolResult.Success($"Set {parameterName} = {boolVal} (Bool)");

                    case "trigger":
                        animator.SetTrigger(parameterName);
                        return McpToolResult.Success($"Triggered {parameterName}");

                    default:
                        return McpToolResult.Error($"Unknown parameter type: {type}");
                }
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set parameter: {ex.Message}");
            }
        }

        private static McpToolResult AddAnimatorParameter(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (parameterName, parameterNameErr) = RequireArg(args, "parameterName");
            if (parameterNameErr != null) return parameterNameErr;

            var (parameterType, parameterTypeErr) = RequireArg(args, "parameterType");
            if (parameterTypeErr != null) return parameterTypeErr;

            var defaultValue = args.GetValueOrDefault("defaultValue");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            foreach (var existing in controller.parameters)
            {
                if (existing.name == parameterName)
                    return McpToolResult.Error($"Parameter '{parameterName}' already exists");
            }

            AnimatorControllerParameterType type;
            switch (parameterType.ToLower())
            {
                case "float": type = AnimatorControllerParameterType.Float; break;
                case "int":
                case "integer": type = AnimatorControllerParameterType.Int; break;
                case "bool":
                case "boolean": type = AnimatorControllerParameterType.Bool; break;
                case "trigger": type = AnimatorControllerParameterType.Trigger; break;
                default:
                    return McpToolResult.Error($"Invalid parameter type: {parameterType}. Use Float, Int, Bool, or Trigger");
            }

            Undo.RecordObject(controller, "Add Animator Parameter");
            controller.AddParameter(parameterName, type);

            if (defaultValue != null)
            {
                var parameters = controller.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].name == parameterName)
                    {
                        var param = parameters[i];
                        switch (type)
                        {
                            case AnimatorControllerParameterType.Float:
                                if (float.TryParse(defaultValue?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatDefault))
                                    param.defaultFloat = floatDefault;
                                break;
                            case AnimatorControllerParameterType.Int:
                                if (int.TryParse(defaultValue?.ToString(), out var intDefault))
                                    param.defaultInt = intDefault;
                                break;
                            case AnimatorControllerParameterType.Bool:
                                if (bool.TryParse(defaultValue?.ToString(), out var boolDefault))
                                    param.defaultBool = boolDefault;
                                break;
                        }
                        parameters[i] = param;
                        break;
                    }
                }
                controller.parameters = parameters;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added parameter '{parameterName}' ({parameterType}) to {controller.name}");
        }

        #endregion
    }
}
