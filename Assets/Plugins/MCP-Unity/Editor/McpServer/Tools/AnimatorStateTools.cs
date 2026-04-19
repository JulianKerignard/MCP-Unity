using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Animator State tools: add, delete, modify states, and blend tree operations.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorStateTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_animator_state",
                description = "Add a new state to an Animator Controller layer",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["stateName"] = new McpPropertySchema { type = "string", description = "Name of the new state" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" },
                        ["position"] = new McpPropertySchema { type = "object", description = "Position {x, y} in the Animator window (optional)" },
                        ["motionClip"] = new McpPropertySchema { type = "string", description = "Path to an AnimationClip to assign (optional)" }
                    },
                    required = new List<string> { "controllerPath", "stateName" }
                }
            }, AddAnimatorState);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_delete_animator_state",
                description = "Delete a state from an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["stateName"] = new McpPropertySchema { type = "string", description = "Name of the state to delete" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" }
                    },
                    required = new List<string> { "controllerPath", "stateName" }
                }
            }, DeleteAnimatorState);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_modify_animator_state",
                description = "Modify properties of an existing Animator state",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["stateName"] = new McpPropertySchema { type = "string", description = "Name of the state to modify" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" },
                        ["newName"] = new McpPropertySchema { type = "string", description = "New name for the state (optional)" },
                        ["motion"] = new McpPropertySchema { type = "string", description = "Path to AnimationClip to assign (optional)" },
                        ["speed"] = new McpPropertySchema { type = "number", description = "Playback speed (optional)" },
                        ["speedParameter"] = new McpPropertySchema { type = "string", description = "Parameter to control speed (optional)" },
                        ["cycleOffset"] = new McpPropertySchema { type = "number", description = "Cycle offset (optional)" },
                        ["mirror"] = new McpPropertySchema { type = "boolean", description = "Mirror animation (optional)" },
                        ["writeDefaultValues"] = new McpPropertySchema { type = "boolean", description = "Write default values (optional)" },
                        ["setAsDefault"] = new McpPropertySchema { type = "boolean", description = "Set this state as the default (Entry) state for the layer (optional)" }
                    },
                    required = new List<string> { "controllerPath", "stateName" }
                }
            }, ModifyAnimatorState);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_default_state",
                description = "Set the default (Entry) state of an Animator Controller layer",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["stateName"]      = new McpPropertySchema { type = "string", description = "Name of the state to set as default" },
                        ["layerIndex"]     = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" }
                    },
                    required = new List<string> { "controllerPath", "stateName" }
                }
            }, SetDefaultState);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_blend_tree",
                description = "Create a new BlendTree state in an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["stateName"] = new McpPropertySchema { type = "string", description = "Name for the BlendTree state" },
                        ["blendType"] = new McpPropertySchema { type = "string", description = "Blend type: 1D, 2DSimpleDirectional, 2DFreeformDirectional, 2DFreeformCartesian, Direct (default: 1D)" },
                        ["blendParameter"] = new McpPropertySchema { type = "string", description = "Parameter name for blending (X-axis for 2D)" },
                        ["blendParameterY"] = new McpPropertySchema { type = "string", description = "Second parameter for 2D blend trees" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" }
                    },
                    required = new List<string> { "controllerPath", "stateName" }
                }
            }, CreateBlendTree);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_blend_motion",
                description = "Add a motion (animation clip) to an existing BlendTree",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["blendTreeState"] = new McpPropertySchema { type = "string", description = "Name of the BlendTree state" },
                        ["motionPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimationClip" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" },
                        ["threshold"] = new McpPropertySchema { type = "number", description = "Threshold value for 1D blend trees" },
                        ["positionX"] = new McpPropertySchema { type = "number", description = "X position for 2D blend trees" },
                        ["positionY"] = new McpPropertySchema { type = "number", description = "Y position for 2D blend trees" }
                    },
                    required = new List<string> { "controllerPath", "blendTreeState", "motionPath" }
                }
            }, AddBlendMotion);
        }

        #region Blend Tree Helpers

        private static BlendTreeType ParseBlendType(string type)
        {
            switch (type?.ToLower())
            {
                case "1d": return BlendTreeType.Simple1D;
                case "2dsimpledirectional": return BlendTreeType.SimpleDirectional2D;
                case "2dfreeformdirectional": return BlendTreeType.FreeformDirectional2D;
                case "2dfreeformcartesian": return BlendTreeType.FreeformCartesian2D;
                case "direct": return BlendTreeType.Direct;
                default: return BlendTreeType.Simple1D;
            }
        }

        #endregion

        #region State Handlers

        private static McpToolResult AddAnimatorState(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (stateName, stateNameErr) = RequireArg(args, "stateName");
            if (stateNameErr != null) return stateNameErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);
            var positionObj = args.GetValueOrDefault("position") as Dictionary<string, object>;
            var motionClip = ArgumentParser.GetString(args, "motionClip");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range. Controller has {controller.layers.Length} layers");

            var stateMachine = controller.layers[layerIndex].stateMachine;

            if (FindStateByName(stateMachine, stateName) != null)
                return McpToolResult.Error($"State '{stateName}' already exists in layer {layerIndex}");

            Vector3 position = new Vector3(300, 100, 0);
            if (positionObj != null)
            {
                if (positionObj.TryGetValue("x", out var xObj) && float.TryParse(xObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var posX))
                    position.x = posX;
                if (positionObj.TryGetValue("y", out var yObj) && float.TryParse(yObj?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var posY))
                    position.y = posY;
            }
            else
            {
                int stateCount = stateMachine.states.Length;
                position = new Vector3(300 + (stateCount % 3) * 200, 100 + (stateCount / 3) * 100, 0);
            }

            Undo.RecordObject(stateMachine, "Add Animator State");
            var newState = stateMachine.AddState(stateName, position);

            if (!string.IsNullOrEmpty(motionClip))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionClip);
                if (clip != null)
                {
                    newState.motion = clip;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added state '{stateName}' to layer {layerIndex}", new
            {
                name = newState.name,
                position = new { x = position.x, y = position.y },
                motion = newState.motion?.name
            });
        }

        private static McpToolResult DeleteAnimatorState(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (stateName, stateNameErr) = RequireArg(args, "stateName");
            if (stateNameErr != null) return stateNameErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range. Controller has {controller.layers.Length} layers");

            var stateMachine = controller.layers[layerIndex].stateMachine;

            var stateToDelete = FindStateByName(stateMachine, stateName);
            if (stateToDelete == null)
                return McpToolResult.Error($"State '{stateName}' not found in layer {layerIndex}");

            bool wasDefaultState = stateMachine.defaultState == stateToDelete;

            Undo.RecordObject(stateMachine, "Delete Animator State");
            stateMachine.RemoveState(stateToDelete);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Deleted state '{stateName}' from layer {layerIndex}", new
            {
                deletedState = stateName,
                wasDefaultState = wasDefaultState,
                controllerPath = controllerPath
            });
        }

        private static McpToolResult ModifyAnimatorState(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (stateName, stateNameErr) = RequireArg(args, "stateName");
            if (stateNameErr != null) return stateNameErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var state = FindStateByName(stateMachine, stateName);
            if (state == null)
                return McpToolResult.Error($"State '{stateName}' not found in layer {layerIndex}");

            Undo.RecordObject(state, "Modify Animator State");

            var modifiedProperties = new List<string>();

            var newName = ArgumentParser.GetString(args, "newName");
            if (!string.IsNullOrEmpty(newName))
            {
                state.name = newName;
                modifiedProperties.Add($"name: {newName}");
            }

            var motionPath = ArgumentParser.GetString(args, "motion");
            if (!string.IsNullOrEmpty(motionPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionPath);
                if (clip != null)
                {
                    state.motion = clip;
                    modifiedProperties.Add($"motion: {motionPath}");
                }
                else
                {
                    return McpToolResult.Error($"AnimationClip not found: {motionPath}");
                }
            }

            if (args.ContainsKey("speed"))
            {
                float speed = ArgumentParser.GetFloat(args, "speed", state.speed);
                state.speed = speed;
                modifiedProperties.Add($"speed: {speed}");
            }

            var speedParam = ArgumentParser.GetString(args, "speedParameter");
            if (!string.IsNullOrEmpty(speedParam))
            {
                state.speedParameterActive = true;
                state.speedParameter = speedParam;
                modifiedProperties.Add($"speedParameter: {speedParam}");
            }

            if (args.ContainsKey("cycleOffset"))
            {
                float cycleOffset = ArgumentParser.GetFloat(args, "cycleOffset", state.cycleOffset);
                state.cycleOffset = cycleOffset;
                modifiedProperties.Add($"cycleOffset: {cycleOffset}");
            }

            if (args.ContainsKey("mirror"))
            {
                bool mirror = ArgumentParser.GetBool(args, "mirror", state.mirror);
                state.mirror = mirror;
                modifiedProperties.Add($"mirror: {mirror}");
            }

            if (args.ContainsKey("writeDefaultValues"))
            {
                bool writeDefaults = ArgumentParser.GetBool(args, "writeDefaultValues", state.writeDefaultValues);
                state.writeDefaultValues = writeDefaults;
                modifiedProperties.Add($"writeDefaultValues: {writeDefaults}");
            }

            if (ArgumentParser.GetBool(args, "setAsDefault", false))
            {
                Undo.RecordObject(stateMachine, "Set Default Animator State");
                stateMachine.defaultState = state;
                modifiedProperties.Add("setAsDefault: true");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Modified state '{stateName}' in layer {layerIndex}", new
            {
                modifiedProperties = modifiedProperties
            });
        }

        private static McpToolResult SetDefaultState(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (stateName, stateNameErr) = RequireArg(args, "stateName");
            if (stateNameErr != null) return stateNameErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var state = FindStateByName(stateMachine, stateName);
            if (state == null)
                return McpToolResult.Error($"State '{stateName}' not found in layer {layerIndex}");

            var previousDefault = stateMachine.defaultState?.name;

            Undo.RecordObject(stateMachine, "Set Default Animator State");
            stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Set '{stateName}' as default state in layer {layerIndex}", new
            {
                stateName       = stateName,
                layerIndex      = layerIndex,
                previousDefault = previousDefault
            });
        }

        #endregion

        #region Blend Tree Handlers

        private static McpToolResult CreateBlendTree(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (stateName, stateNameErr) = RequireArg(args, "stateName");
            if (stateNameErr != null) return stateNameErr;

            var blendTypeStr = ArgumentParser.GetString(args, "blendType", "1D");
            var blendParameter = ArgumentParser.GetString(args, "blendParameter");
            var blendParameterY = ArgumentParser.GetString(args, "blendParameterY");
            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found at: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} is out of range. Controller has {controller.layers.Length} layers.");

            Undo.RecordObject(controller, "Create Blend Tree");

            BlendTree blendTree;
            controller.CreateBlendTreeInController(stateName, out blendTree, layerIndex);

            if (blendTree == null)
                return McpToolResult.Error("Failed to create blend tree");

            blendTree.blendType = ParseBlendType(blendTypeStr);

            if (!string.IsNullOrEmpty(blendParameter))
                blendTree.blendParameter = blendParameter;

            if (!string.IsNullOrEmpty(blendParameterY) && blendTree.blendType != BlendTreeType.Simple1D)
                blendTree.blendParameterY = blendParameterY;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Created blend tree '{stateName}' in layer {layerIndex}", new
            {
                stateName = stateName,
                blendType = blendTree.blendType.ToString(),
                blendParameter = blendTree.blendParameter,
                blendParameterY = blendTree.blendParameterY,
                layerIndex = layerIndex,
                childCount = blendTree.children.Length
            });
        }

        private static McpToolResult AddBlendMotion(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (blendTreeState, blendTreeStateErr) = RequireArg(args, "blendTreeState");
            if (blendTreeStateErr != null) return blendTreeStateErr;

            var (motionPath, motionPathErr) = RequireArg(args, "motionPath");
            if (motionPathErr != null) return motionPathErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);
            float threshold = ArgumentParser.GetFloat(args, "threshold", 0f);
            float positionX = ArgumentParser.GetFloat(args, "positionX", 0f);
            float positionY = ArgumentParser.GetFloat(args, "positionY", 0f);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found at: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} is out of range. Controller has {controller.layers.Length} layers.");

            var layer = controller.layers[layerIndex];
            // Use recursive FindStateByName to also search nested sub-state machines
            var state = FindStateByName(layer.stateMachine, blendTreeState);

            if (state == null)
                return McpToolResult.Error($"State '{blendTreeState}' not found in layer {layerIndex}");

            var blendTree = state.motion as BlendTree;
            if (blendTree == null)
                return McpToolResult.Error($"State '{blendTreeState}' does not contain a BlendTree");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionPath);
            if (clip == null)
                return McpToolResult.Error($"AnimationClip not found at: {motionPath}");

            Undo.RecordObject(blendTree, "Add Blend Motion");

            if (blendTree.blendType == BlendTreeType.Simple1D)
            {
                blendTree.AddChild(clip, threshold);
            }
            else
            {
                blendTree.AddChild(clip, new Vector2(positionX, positionY));
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(blendTree);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added motion '{clip.name}' to blend tree '{blendTreeState}'", new
            {
                clipName = clip.name,
                clipPath = motionPath,
                blendTreeState = blendTreeState,
                blendType = blendTree.blendType.ToString(),
                threshold = blendTree.blendType == BlendTreeType.Simple1D ? threshold : (float?)null,
                position = blendTree.blendType != BlendTreeType.Simple1D
                    ? new { x = positionX, y = positionY }
                    : null,
                totalChildren = blendTree.children.Length
            });
        }

        #endregion
    }
}
