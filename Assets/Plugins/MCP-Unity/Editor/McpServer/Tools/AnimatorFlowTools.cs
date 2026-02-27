using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Animator flow tools: trace paths through an Animator Controller.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterAnimatorFlowTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_animator_flow",
                description = "Trace all possible paths through an Animator Controller from a starting state",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["controllerPath"] = new McpPropertySchema { type = "string", description = "Path to the AnimatorController asset" },
                        ["fromState"] = new McpPropertySchema { type = "string", description = "Starting state name (default: 'Entry' for default state)" },
                        ["maxDepth"] = new McpPropertySchema { type = "integer", description = "Maximum path depth to trace (default: 10)" },
                        ["layerIndex"] = new McpPropertySchema { type = "integer", description = "Layer index (default: 0)" }
                    },
                    required = new List<string> { "controllerPath" }
                }
            }, GetAnimatorFlow);
        }

        #region Animator Flow Helpers

        internal static void TracePaths(AnimatorStateMachine sm, AnimatorState current,
            List<string> currentPath, List<string> currentConditions, int depth, int maxDepth,
            List<object> allPaths, HashSet<string> visited, HashSet<string> reachableStates)
        {
            reachableStates.Add(current.name);

            if (depth >= maxDepth)
            {
                allPaths.Add(new
                {
                    sequence = new List<string>(currentPath),
                    conditions = new List<string>(currentConditions),
                    truncated = true
                });
                return;
            }

            if (current.transitions.Length == 0)
            {
                allPaths.Add(new
                {
                    sequence = new List<string>(currentPath),
                    conditions = new List<string>(currentConditions),
                    truncated = false
                });
                return;
            }

            foreach (var transition in current.transitions)
            {
                var destState = transition.destinationState;
                if (destState == null) continue;

                var conditionStr = BuildConditionString(transition);

                var visitKey = $"{current.name}->{destState.name}";
                if (visited.Contains(visitKey))
                {
                    var cyclePath = new List<string>(currentPath) { $"{destState.name} (cycle)" };
                    var cycleConds = new List<string>(currentConditions) { conditionStr };
                    allPaths.Add(new
                    {
                        sequence = cyclePath,
                        conditions = cycleConds,
                        truncated = false,
                        hasCycle = true
                    });
                    continue;
                }

                visited.Add(visitKey);
                currentPath.Add(destState.name);
                currentConditions.Add(conditionStr);

                TracePaths(sm, destState, currentPath, currentConditions, depth + 1, maxDepth, allPaths, visited, reachableStates);

                currentPath.RemoveAt(currentPath.Count - 1);
                currentConditions.RemoveAt(currentConditions.Count - 1);
                visited.Remove(visitKey);
            }
        }

        internal static string BuildConditionString(AnimatorStateTransition transition)
        {
            if (transition.conditions.Length == 0)
            {
                if (transition.hasExitTime)
                    return $"exitTime >= {transition.exitTime:F2}";
                return "(no condition)";
            }

            var parts = new List<string>();
            foreach (var cond in transition.conditions)
            {
                string op;
                switch (cond.mode)
                {
                    case AnimatorConditionMode.Greater: op = ">"; break;
                    case AnimatorConditionMode.Less: op = "<"; break;
                    case AnimatorConditionMode.Equals: op = "=="; break;
                    case AnimatorConditionMode.NotEqual: op = "!="; break;
                    case AnimatorConditionMode.If: op = "== true"; break;
                    case AnimatorConditionMode.IfNot: op = "== false"; break;
                    default: op = "?"; break;
                }

                if (cond.mode == AnimatorConditionMode.If || cond.mode == AnimatorConditionMode.IfNot)
                    parts.Add($"{cond.parameter} {op}");
                else
                    parts.Add($"{cond.parameter} {op} {cond.threshold}");
            }

            return string.Join(" && ", parts);
        }

        internal static void CollectAllStateNames(AnimatorStateMachine sm, HashSet<string> stateNames)
        {
            foreach (var childState in sm.states)
            {
                stateNames.Add(childState.state.name);
            }

            foreach (var subMachine in sm.stateMachines)
            {
                CollectAllStateNames(subMachine.stateMachine, stateNames);
            }
        }

        internal static int CountStates(AnimatorStateMachine sm)
        {
            int count = sm.states.Length;
            foreach (var subMachine in sm.stateMachines)
            {
                count += CountStates(subMachine.stateMachine);
            }
            return count;
        }

        internal static int CountTransitions(AnimatorStateMachine sm)
        {
            int count = sm.anyStateTransitions.Length;
            foreach (var childState in sm.states)
            {
                count += childState.state.transitions.Length;
            }
            foreach (var subMachine in sm.stateMachines)
            {
                count += CountTransitions(subMachine.stateMachine);
            }
            return count;
        }

        #endregion

        #region Animator Flow Handlers

        private static McpToolResult GetAnimatorFlow(Dictionary<string, object> args)
        {
            var (controllerPath, controllerPathErr) = RequireArg(args, "controllerPath");
            if (controllerPathErr != null) return controllerPathErr;

            var fromState = ArgumentParser.GetString(args, "fromState", "Entry");
            int maxDepth = ArgumentParser.GetIntClamped(args, "maxDepth", 10, 1, 50);
            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                return McpToolResult.Error($"AnimatorController not found: {controllerPath}");

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
                return McpToolResult.Error($"Layer index {layerIndex} out of range");

            var sm = controller.layers[layerIndex].stateMachine;
            var allPaths = new List<object>();
            var reachableStates = new HashSet<string>();
            var anyStateTargets = new List<string>();

            foreach (var transition in sm.anyStateTransitions)
            {
                if (transition.destinationState != null)
                    anyStateTargets.Add(transition.destinationState.name);
            }

            AnimatorState startState = null;
            if (fromState.ToLower() == "entry")
            {
                startState = sm.defaultState;
                if (startState == null)
                    return McpToolResult.Error("No default state defined in this layer");
            }
            else
            {
                startState = FindStateByName(sm, fromState);
                if (startState == null)
                    return McpToolResult.Error($"State '{fromState}' not found in layer {layerIndex}");
            }

            var pathSequence = new List<string> { startState.name };
            var conditionSequence = new List<string> { "(start)" };
            var visited = new HashSet<string>();

            TracePaths(sm, startState, pathSequence, conditionSequence, 0, maxDepth, allPaths, visited, reachableStates);

            var allStates = new HashSet<string>();
            CollectAllStateNames(sm, allStates);

            var unreachableStates = new List<string>();
            foreach (var state in allStates)
            {
                if (!reachableStates.Contains(state) && state != startState.name)
                {
                    if (!anyStateTargets.Contains(state))
                        unreachableStates.Add(state);
                }
            }

            return McpResponse.Success(new
            {
                paths = allPaths,
                reachableStates = reachableStates.ToList(),
                unreachableStates = unreachableStates,
                anyStateTargets = anyStateTargets
            });
        }

        #endregion
    }
}
