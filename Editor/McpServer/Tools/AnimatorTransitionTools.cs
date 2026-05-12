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
    /// Animator Transition tools: add, delete, and modify transitions.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorTransitionTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_animator_transition",
                description = "Add a transition between states in an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "AnimatorController asset path" },
                        ["fromState"] = new McpPropertySchema { type = "string", description = "Source state name (use 'Any' for AnyState)" },
                        ["toState"] = new McpPropertySchema { type = "string", description = "Destination state name" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index" },
                        ["hasExitTime"] = new McpPropertySchema { type = "boolean", description = "Transition has exit time" },
                        ["exitTime"] = new McpPropertySchema { type = "number", description = "Exit time" },
                        ["transitionDuration"] = new McpPropertySchema { type = "number", description = "Transition duration" },
                        ["conditions"] = new McpPropertySchema { type = "array", description = "Array of conditions: [{parameter, mode, threshold}]" }
                    },
                    required = new List<string> { "controllerPath", "fromState", "toState" }
                }
            }, AddAnimatorTransition);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_delete_animator_transition",
                description = "Delete a transition from an Animator Controller",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "AnimatorController asset path" },
                        ["fromState"] = new McpPropertySchema { type = "string", description = "Source state name (use 'Any' for AnyState)" },
                        ["toState"] = new McpPropertySchema { type = "string", description = "Destination state name" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index" },
                        ["transitionIndex"] = new McpPropertySchema { type = "integer", description = "Index if multiple transitions exist" }
                    },
                    required = new List<string> { "controllerPath", "fromState", "toState" }
                }
            }, DeleteAnimatorTransition);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_transition_condition",
                description = "Add a condition to an existing Animator transition",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"]  = new McpPropertySchema { type = "string", description = "AnimatorController asset path" },
                        ["fromState"]       = new McpPropertySchema { type = "string", description = "Source state name (or 'Any')" },
                        ["toState"]         = new McpPropertySchema { type = "string", description = "Destination state name" },
                        ["layerIndex"]      = new McpPropertySchema { type = "integer", description = "Layer index" },
                        ["transitionIndex"] = new McpPropertySchema { type = "integer", description = "Index if multiple transitions" },
                        ["parameter"]       = new McpPropertySchema { type = "string",  description = "Parameter name" },
                        ["mode"]            = new McpPropertySchema { type = "string",  description = "Condition mode: Greater, Less, Equals, NotEqual, If, IfNot" },
                        ["threshold"]       = new McpPropertySchema { type = "number",  description = "Threshold value for numeric conditions" }
                    },
                    required = new List<string> { "controllerPath", "fromState", "toState", "parameter" }
                }
            }, AddTransitionCondition);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_remove_transition_condition",
                description = "Remove a condition from an Animator transition by index",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"]    = new McpPropertySchema { type = "string",  description = "AnimatorController asset path" },
                        ["fromState"]         = new McpPropertySchema { type = "string",  description = "Source state name (or 'Any')" },
                        ["toState"]           = new McpPropertySchema { type = "string",  description = "Destination state name" },
                        ["layerIndex"]        = new McpPropertySchema { type = "integer", description = "Layer index" },
                        ["transitionIndex"]   = new McpPropertySchema { type = "integer", description = "Index if multiple transitions" },
                        ["conditionIndex"]    = new McpPropertySchema { type = "integer", description = "Index of the condition to remove" }
                    },
                    required = new List<string> { "controllerPath", "fromState", "toState" }
                }
            }, RemoveTransitionCondition);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_modify_transition",
                description = "Modify properties of an existing Animator transition",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "AnimatorController asset path" },
                        ["fromState"] = new McpPropertySchema { type = "string", description = "Source state name (use 'Any' for AnyState)" },
                        ["toState"] = new McpPropertySchema { type = "string", description = "Destination state name" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index" },
                        ["transitionIndex"] = new McpPropertySchema { type = "integer", description = "Index if multiple transitions exist" },
                        ["hasExitTime"] = new McpPropertySchema { type = "boolean", description = "Transition has exit time" },
                        ["exitTime"] = new McpPropertySchema { type = "number", description = "Exit time" },
                        ["duration"] = new McpPropertySchema { type = "number", description = "Transition duration" },
                        ["offset"] = new McpPropertySchema { type = "number", description = "Transition offset" },
                        ["interruptionSource"] = new McpPropertySchema { type = "string", description = "Interruption source: None, Source, Destination, SourceThenDestination, DestinationThenSource" },
                        ["canTransitionToSelf"] = new McpPropertySchema { type = "boolean", description = "Whether can transition to self" }
                    },
                    required = new List<string> { "controllerPath", "fromState", "toState" }
                }
            }, ModifyTransition);
        }

        #region Transition Helpers

        private static TransitionInterruptionSource ParseInterruptionSource(string source)
        {
            switch (source?.ToLower())
            {
                case "source": return TransitionInterruptionSource.Source;
                case "destination": return TransitionInterruptionSource.Destination;
                case "sourcethendestination": return TransitionInterruptionSource.SourceThenDestination;
                case "destinationthensource": return TransitionInterruptionSource.DestinationThenSource;
                default: return TransitionInterruptionSource.None;
            }
        }

        #endregion

        #region Transition Handlers

        /// <summary>Finds a transition between two states, supporting AnyState.</summary>
        private static AnimatorStateTransition FindTransition(AnimatorStateMachine sm, string fromState, string toState, int transitionIndex)
        {
            bool isAny = fromState.ToLower() == "any" || fromState.ToLower() == "anystate";
            if (isAny)
            {
                var matches = System.Array.FindAll(sm.anyStateTransitions,
                    t => t.destinationState != null && t.destinationState.name == toState);
                return transitionIndex < matches.Length ? matches[transitionIndex] : null;
            }
            var src = FindStateByName(sm, fromState);
            if (src == null) return null;
            int count = 0;
            foreach (var t in src.transitions)
            {
                if (t.destinationState != null && t.destinationState.name == toState)
                {
                    if (count == transitionIndex) return t;
                    count++;
                }
            }
            return null;
        }

        private static McpToolResult AddTransitionCondition(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;
            var (fromState, fromStateErr) = RequireArg(args, "fromState");
            if (fromStateErr != null) return fromStateErr;
            var (toState, toStateErr) = RequireArg(args, "toState");
            if (toStateErr != null) return toStateErr;
            var (parameter, parameterErr) = RequireArg(args, "parameter");
            if (parameterErr != null) return parameterErr;

            int layerIndex      = ArgumentParser.GetInt(args, "layerIndex", 0);
            int transitionIndex = ArgumentParser.GetInt(args, "transitionIndex", 0);
            string modeStr      = ArgumentParser.GetString(args, "mode", "If");
            float threshold     = ArgumentParser.GetFloat(args, "threshold", 0f);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) return McpToolResult.Error($"AnimatorController not found: {controllerPath}");
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var transition = FindTransition(controller.layers[layerIndex].stateMachine, fromState, toState, transitionIndex);
            if (transition == null)
                return McpToolResult.Error($"Transition from '{fromState}' to '{toState}' not found (index {transitionIndex})");

            Undo.RecordObject(transition, "Add Transition Condition");
            transition.AddCondition(ParseConditionMode(modeStr), threshold, parameter);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added condition '{parameter} {modeStr} {threshold}' to transition", new
            {
                parameter      = parameter,
                mode           = modeStr,
                threshold      = threshold,
                conditionCount = transition.conditions.Length
            });
        }

        private static McpToolResult RemoveTransitionCondition(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;
            var (fromState, fromStateErr) = RequireArg(args, "fromState");
            if (fromStateErr != null) return fromStateErr;
            var (toState, toStateErr) = RequireArg(args, "toState");
            if (toStateErr != null) return toStateErr;

            int layerIndex      = ArgumentParser.GetInt(args, "layerIndex", 0);
            int transitionIndex = ArgumentParser.GetInt(args, "transitionIndex", 0);
            int conditionIndex  = ArgumentParser.GetInt(args, "conditionIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) return McpToolResult.Error($"AnimatorController not found: {controllerPath}");
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var transition = FindTransition(controller.layers[layerIndex].stateMachine, fromState, toState, transitionIndex);
            if (transition == null)
                return McpToolResult.Error($"Transition from '{fromState}' to '{toState}' not found");

            var conditions = transition.conditions;
            if (conditionIndex < 0 || conditionIndex >= conditions.Length)
                return McpToolResult.Error($"Condition index {conditionIndex} out of range (transition has {conditions.Length} condition(s))");

            var removedCond = conditions[conditionIndex];

            Undo.RecordObject(transition, "Remove Transition Condition");
            var newConditions = new AnimatorCondition[conditions.Length - 1];
            int j = 0;
            for (int i = 0; i < conditions.Length; i++)
                if (i != conditionIndex) newConditions[j++] = conditions[i];
            transition.conditions = newConditions;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Removed condition '{removedCond.parameter}' from transition", new
            {
                removedParameter  = removedCond.parameter,
                removedMode       = removedCond.mode.ToString(),
                removedThreshold  = removedCond.threshold,
                remainingCount    = transition.conditions.Length
            });
        }

        private static McpToolResult AddAnimatorTransition(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (fromState, fromStateErr) = RequireArg(args, "fromState");
            if (fromStateErr != null) return fromStateErr;

            var (toState, toStateErr) = RequireArg(args, "toState");
            if (toStateErr != null) return toStateErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);
            var conditionsObj = args.GetValueOrDefault("conditions");
            bool hasExitTime = ArgumentParser.GetBool(args, "hasExitTime", true);
            float exitTime = ArgumentParser.GetFloat(args, "exitTime", 1.0f);
            float transitionDuration = ArgumentParser.GetFloat(args, "transitionDuration", 0.25f);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var stateMachine = controller.layers[layerIndex].stateMachine;

            var destState = FindStateByName(stateMachine, toState);
            if (destState == null)
                return McpToolResult.Error($"Destination state '{toState}' not found in layer {layerIndex}");

            AnimatorStateTransition transition;

            Undo.RecordObject(controller, "Add Animator Transition");

            if (fromState.ToLower() == "any" || fromState.ToLower() == "anystate")
            {
                transition = stateMachine.AddAnyStateTransition(destState);
            }
            else
            {
                var srcState = FindStateByName(stateMachine, fromState);
                if (srcState == null)
                    return McpToolResult.Error($"Source state '{fromState}' not found in layer {layerIndex}");

                transition = srcState.AddTransition(destState);
            }

            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.duration = transitionDuration;

            var addedConditions = new List<string>();
            if (conditionsObj is IList<object> conditionsList)
            {
                foreach (var condObj in conditionsList)
                {
                    if (condObj is Dictionary<string, object> cond)
                    {
                        var paramName = ArgumentParser.GetString(cond, "parameter");
                        var mode = ArgumentParser.GetString(cond, "mode", "If");
                        float threshold = 0;
                        if (cond.TryGetValue("threshold", out var threshObj) && threshObj != null)
                            float.TryParse(threshObj.ToString(), out threshold);

                        if (!string.IsNullOrEmpty(paramName))
                        {
                            transition.AddCondition(ParseConditionMode(mode), threshold, paramName);
                            addedConditions.Add($"{paramName} {mode} {threshold}");
                        }
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Added transition from '{fromState}' to '{toState}'", new
            {
                from = fromState,
                to = toState,
                hasExitTime = hasExitTime,
                exitTime = exitTime,
                duration = transitionDuration,
                conditions = addedConditions
            });
        }

        private static McpToolResult DeleteAnimatorTransition(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (fromState, fromStateErr) = RequireArg(args, "fromState");
            if (fromStateErr != null) return fromStateErr;

            var (toState, toStateErr) = RequireArg(args, "toState");
            if (toStateErr != null) return toStateErr;

            int transitionIndex = ArgumentParser.GetInt(args, "transitionIndex", 0);
            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var stateMachine = controller.layers[layerIndex].stateMachine;

            Undo.RecordObject(controller, "Delete Animator Transition");

            bool isAnyState = fromState.ToLower() == "any" || fromState.ToLower() == "anystate";

            if (isAnyState)
            {
                var anyTransitions = stateMachine.anyStateTransitions;
                AnimatorStateTransition toRemove = null;

                foreach (var t in anyTransitions)
                {
                    if (t.destinationState != null && t.destinationState.name == toState)
                    {
                        toRemove = t;
                        break;
                    }
                }

                if (toRemove == null)
                    return McpToolResult.Error($"No AnyState transition to '{toState}' found in layer {layerIndex}");

                Undo.RecordObject(stateMachine, "Delete AnyState Transition");
                stateMachine.RemoveAnyStateTransition(toRemove);
            }
            else
            {
                var srcState = FindStateByName(stateMachine, fromState);
                if (srcState == null)
                    return McpToolResult.Error($"Source state '{fromState}' not found in layer {layerIndex}");

                var transitions = srcState.transitions;
                if (transitions == null || transitions.Length == 0)
                    return McpToolResult.Error($"State '{fromState}' has no transitions");

                AnimatorStateTransition toRemove = null;
                int matchCount = 0;

                for (int i = 0; i < transitions.Length; i++)
                {
                    var t = transitions[i];
                    if (t.destinationState != null && t.destinationState.name == toState)
                    {
                        if (matchCount == transitionIndex)
                        {
                            toRemove = t;
                            break;
                        }
                        matchCount++;
                    }
                }

                if (toRemove == null)
                {
                    if (matchCount == 0)
                        return McpToolResult.Error($"No transition from '{fromState}' to '{toState}' found");
                    else
                        return McpToolResult.Error($"Transition index {transitionIndex} out of range. Found {matchCount} transitions from '{fromState}' to '{toState}'");
                }

                Undo.RecordObject(srcState, "Delete State Transition");
                srcState.RemoveTransition(toRemove);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Deleted transition from '{fromState}' to '{toState}'", new
            {
                fromState = fromState,
                toState = toState,
                transitionIndex = transitionIndex,
                controllerPath = controllerPath
            });
        }

        private static McpToolResult ModifyTransition(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var (fromState, fromStateErr) = RequireArg(args, "fromState");
            if (fromStateErr != null) return fromStateErr;

            var (toState, toStateErr) = RequireArg(args, "toState");
            if (toStateErr != null) return toStateErr;

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);
            int transitionIndex = ArgumentParser.GetInt(args, "transitionIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var stateMachine = controller.layers[layerIndex].stateMachine;

            AnimatorStateTransition transition = null;

            if (fromState.ToLower() == "any" || fromState.ToLower() == "anystate")
            {
                var anyTransitions = stateMachine.anyStateTransitions
                    .Where(t => t.destinationState != null && t.destinationState.name == toState)
                    .ToArray();

                if (anyTransitions.Length == 0)
                    return McpToolResult.Error($"No AnyState transition to '{toState}' found");
                if (transitionIndex >= anyTransitions.Length)
                    return McpToolResult.Error($"Transition index {transitionIndex} out of range (found {anyTransitions.Length})");

                transition = anyTransitions[transitionIndex];
            }
            else
            {
                var srcState = FindStateByName(stateMachine, fromState);
                if (srcState == null)
                    return McpToolResult.Error($"Source state '{fromState}' not found in layer {layerIndex}");

                var stateTransitions = srcState.transitions
                    .Where(t => t.destinationState != null && t.destinationState.name == toState)
                    .ToArray();

                if (stateTransitions.Length == 0)
                    return McpToolResult.Error($"No transition from '{fromState}' to '{toState}' found");
                if (transitionIndex >= stateTransitions.Length)
                    return McpToolResult.Error($"Transition index {transitionIndex} out of range (found {stateTransitions.Length})");

                transition = stateTransitions[transitionIndex];
            }

            Undo.RecordObject(transition, "Modify Transition");

            var modifiedProperties = new List<string>();

            if (args.ContainsKey("hasExitTime"))
            {
                bool hasExitTime = ArgumentParser.GetBool(args, "hasExitTime", transition.hasExitTime);
                transition.hasExitTime = hasExitTime;
                modifiedProperties.Add($"hasExitTime: {hasExitTime}");
            }

            if (args.ContainsKey("exitTime"))
            {
                float exitTime = ArgumentParser.GetFloat(args, "exitTime", transition.exitTime);
                transition.exitTime = exitTime;
                modifiedProperties.Add($"exitTime: {exitTime}");
            }

            if (args.ContainsKey("duration"))
            {
                float duration = ArgumentParser.GetFloat(args, "duration", transition.duration);
                transition.duration = duration;
                modifiedProperties.Add($"duration: {duration}");
            }

            if (args.ContainsKey("offset"))
            {
                float offset = ArgumentParser.GetFloat(args, "offset", transition.offset);
                transition.offset = offset;
                modifiedProperties.Add($"offset: {offset}");
            }

            if (args.ContainsKey("interruptionSource"))
            {
                var source = ParseInterruptionSource(ArgumentParser.GetString(args, "interruptionSource"));
                transition.interruptionSource = source;
                modifiedProperties.Add($"interruptionSource: {source}");
            }

            if (args.ContainsKey("canTransitionToSelf"))
            {
                bool canTransitionToSelf = ArgumentParser.GetBool(args, "canTransitionToSelf", transition.canTransitionToSelf);
                transition.canTransitionToSelf = canTransitionToSelf;
                modifiedProperties.Add($"canTransitionToSelf: {canTransitionToSelf}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return McpResponse.Success($"Modified transition from '{fromState}' to '{toState}'", new
            {
                modifiedProperties  = modifiedProperties,
                hasExitTime         = transition.hasExitTime,
                exitTime            = transition.exitTime,
                duration            = transition.duration,
                offset              = transition.offset,
                interruptionSource  = transition.interruptionSource.ToString(),
                conditions          = SerializeConditions(transition.conditions)
            });
        }

        #endregion
    }
}
