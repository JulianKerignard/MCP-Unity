using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Animator validation tools: validate animator controllers for common issues.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorValidationTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_validate_animator",
                description = "Validate an Animator Controller for common issues (orphan states, unused parameters, etc.)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "AnimatorController asset path" }
                    },
                    required = new List<string> { "controllerPath" }
                }
            }, ValidateAnimator);
        }

        #region Animator Validation Helpers

        internal static void ValidateStateMachine(AnimatorStateMachine sm, int layerIndex,
            AnimatorController controller, List<object> errors, List<object> warnings,
            HashSet<string> usedParams)
        {
            var statesWithIncomingTransitions = new HashSet<string>();
            if (sm.defaultState != null)
                statesWithIncomingTransitions.Add(sm.defaultState.name);

            foreach (var anyTrans in sm.anyStateTransitions)
            {
                if (anyTrans.destinationState != null)
                    statesWithIncomingTransitions.Add(anyTrans.destinationState.name);
            }

            foreach (var childState in sm.states)
            {
                var state = childState.state;

                if (state.motion == null)
                {
                    warnings.Add(new
                    {
                        type = "MissingMotion",
                        layer = layerIndex,
                        state = state.name,
                        message = $"State '{state.name}' has no motion assigned"
                    });
                }

                bool hasOutgoingTransitions = state.transitions.Length > 0;

                foreach (var transition in state.transitions)
                {
                    if (transition.destinationState != null)
                        statesWithIncomingTransitions.Add(transition.destinationState.name);

                    if (transition.conditions.Length == 0 && !transition.hasExitTime)
                    {
                        warnings.Add(new
                        {
                            type = "TransitionWithoutCondition",
                            layer = layerIndex,
                            state = state.name,
                            to = transition.destinationState?.name ?? "Exit",
                            message = $"Transition from '{state.name}' to '{transition.destinationState?.name ?? "Exit"}' has no conditions and no exit time"
                        });
                    }

                    foreach (var cond in transition.conditions)
                    {
                        usedParams.Add(cond.parameter);
                    }
                }

                if (!hasOutgoingTransitions && state.name != "Exit")
                {
                    warnings.Add(new
                    {
                        type = "DeadEndState",
                        layer = layerIndex,
                        state = state.name,
                        message = $"State '{state.name}' has no outgoing transitions (dead end)"
                    });
                }
            }

            foreach (var childState in sm.states)
            {
                var state = childState.state;
                if (!statesWithIncomingTransitions.Contains(state.name))
                {
                    warnings.Add(new
                    {
                        type = "OrphanState",
                        layer = layerIndex,
                        state = state.name,
                        message = $"State '{state.name}' has no incoming transitions (orphan)"
                    });
                }
            }

            foreach (var subMachine in sm.stateMachines)
            {
                ValidateStateMachine(subMachine.stateMachine, layerIndex, controller, errors, warnings, usedParams);
            }
        }

        internal static void CollectStateNames(AnimatorStateMachine sm, Dictionary<string, int> stateNames)
        {
            foreach (var childState in sm.states)
            {
                var name = childState.state.name;
                if (stateNames.ContainsKey(name))
                    stateNames[name]++;
                else
                    stateNames[name] = 1;
            }

            foreach (var subMachine in sm.stateMachines)
            {
                CollectStateNames(subMachine.stateMachine, stateNames);
            }
        }

        internal static bool IsParameterUsedInTransitions(AnimatorController controller, string paramName)
        {
            foreach (var layer in controller.layers)
            {
                if (IsParameterUsedInStateMachine(layer.stateMachine, paramName))
                    return true;
            }
            return false;
        }

        private static bool IsParameterUsedInStateMachine(AnimatorStateMachine sm, string paramName)
        {
            foreach (var transition in sm.anyStateTransitions)
            {
                foreach (var cond in transition.conditions)
                {
                    if (cond.parameter == paramName)
                        return true;
                }
            }

            foreach (var childState in sm.states)
            {
                foreach (var transition in childState.state.transitions)
                {
                    foreach (var cond in transition.conditions)
                    {
                        if (cond.parameter == paramName)
                            return true;
                    }
                }
            }

            foreach (var subMachine in sm.stateMachines)
            {
                if (IsParameterUsedInStateMachine(subMachine.stateMachine, paramName))
                    return true;
            }

            return false;
        }

        #endregion

        #region Animator Validation Handlers

        private static McpToolResult ValidateAnimator(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            var errors = new List<object>();
            var warnings = new List<object>();
            var usedParams = new HashSet<string>();
            int totalStates = 0;
            int totalTransitions = 0;

            for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
            {
                var layer = controller.layers[layerIndex];
                var sm = layer.stateMachine;

                ValidateStateMachine(sm, layerIndex, controller, errors, warnings, usedParams);
                totalStates += CountStates(sm);
                totalTransitions += CountTransitions(sm);

                var stateNames = new Dictionary<string, int>();
                CollectStateNames(sm, stateNames);
                foreach (var kvp in stateNames)
                {
                    if (kvp.Value > 1)
                    {
                        errors.Add(new
                        {
                            type = "DuplicateStateName",
                            layer = layerIndex,
                            state = kvp.Key,
                            message = $"State name '{kvp.Key}' appears {kvp.Value} times in layer {layerIndex}"
                        });
                    }
                }
            }

            foreach (var param in controller.parameters)
            {
                if (!usedParams.Contains(param.name))
                {
                    if (!IsParameterUsedInTransitions(controller, param.name))
                    {
                        warnings.Add(new
                        {
                            type = "UnusedParameter",
                            parameter = param.name,
                            message = $"Parameter '{param.name}' is never used in any transition condition"
                        });
                    }
                }
            }

            return McpResponse.Success(new
            {
                isValid = errors.Count == 0,
                errors = errors,
                warnings = warnings,
                stats = new
                {
                    totalStates = totalStates,
                    totalTransitions = totalTransitions,
                    totalParameters = controller.parameters.Length,
                    layers = controller.layers.Length
                }
            });
        }

        #endregion
    }
}
