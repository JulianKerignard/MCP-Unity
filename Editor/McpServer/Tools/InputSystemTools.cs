using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using McpUnity.Protocol;
using McpUnity.Helpers;

// Input System tools use reflection so that this file compiles whether or not
// com.unity.inputsystem is installed.  At runtime, the handlers check for the
// types and return a clear error message if the package is absent.

namespace McpUnity.Server
{
    /// <summary>
    /// Partial class containing Input System tools (requires com.unity.inputsystem package).
    /// Tools: unity_get_input_actions, unity_add_input_action, unity_add_input_binding
    /// </summary>
    public partial class McpUnityServer
    {
        #region Input System Tool Registrations

        static partial void RegisterInputSystemTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_input_actions",
                description = "INPUT: List all action maps and actions in an InputActionAsset",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to the .inputactions asset"
                        }
                    },
                    required = new List<string> { "assetPath" }
                }
            }, GetInputActions);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_input_action",
                description = "INPUT: Add a new action to an action map",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"]     = new McpPropertySchema { type = "string", description = "Path to the .inputactions asset" },
                        ["actionMapName"] = new McpPropertySchema { type = "string", description = "Action map name" },
                        ["actionName"]    = new McpPropertySchema { type = "string", description = "New action name" },
                        ["actionType"]    = new McpPropertySchema { type = "string", description = "Action type", @enum = new List<string> { "Button", "Value", "PassThrough" } }
                    },
                    required = new List<string> { "assetPath", "actionMapName", "actionName" }
                }
            }, AddInputAction);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_input_binding",
                description = "INPUT: Add a control-path binding to an existing action",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"]     = new McpPropertySchema { type = "string", description = "Path to the .inputactions asset" },
                        ["actionMapName"] = new McpPropertySchema { type = "string", description = "Action map name" },
                        ["actionName"]    = new McpPropertySchema { type = "string", description = "Action name" },
                        ["controlPath"]   = new McpPropertySchema { type = "string", description = "Control path (e.g. '<Keyboard>/space')" }
                    },
                    required = new List<string> { "assetPath", "actionMapName", "actionName", "controlPath" }
                }
            }, AddInputBinding);
        }

        #endregion

        // ====================================================================
        // Reflection helpers — resolve InputSystem types at runtime
        // ====================================================================

        private static readonly string InputAssetTypeName      = "UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem";
        private static readonly string InputActionTypeTypeName = "UnityEngine.InputSystem.InputActionType, Unity.InputSystem";
        private static readonly string SetupExtTypeName        = "UnityEngine.InputSystem.InputActionSetupExtensions, Unity.InputSystem";

        private static Type _inputActionAssetType;
        private static Type _inputActionTypeEnum;
        private static Type _setupExtType;
        private static bool _inputSystemChecked;
        private static bool _inputSystemAvailable;

        private static bool EnsureInputSystem(out string error)
        {
            if (!_inputSystemChecked)
            {
                _inputSystemChecked  = true;
                _inputActionAssetType = Type.GetType(InputAssetTypeName);
                _inputActionTypeEnum  = Type.GetType(InputActionTypeTypeName);
                _setupExtType         = Type.GetType(SetupExtTypeName);
                _inputSystemAvailable = _inputActionAssetType != null && _inputActionTypeEnum != null && _setupExtType != null;
            }

            if (!_inputSystemAvailable)
            {
                error = "com.unity.inputsystem package is not installed. Install it via Window > Package Manager.";
                return false;
            }

            error = null;
            return true;
        }

        #region Input System Handlers

        private static McpToolResult GetInputActions(Dictionary<string, object> args)
        {
            try
            {
                if (!EnsureInputSystem(out var sysError))
                    return McpToolResult.Error(sysError);

                var (assetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;

                var (sanitizedAssetPath, sanitizeErr) = TrySanitizePath(assetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;
                assetPath = sanitizedAssetPath;

                var asset = AssetDatabase.LoadAssetAtPath(assetPath, _inputActionAssetType);
                if (asset == null)
                    return McpToolResult.Error($"InputActionAsset not found at '{assetPath}'. Ensure the path ends with '.inputactions'.");

                // asset.actionMaps
                var actionMapsProperty = _inputActionAssetType.GetProperty("actionMaps");
                var actionMaps         = actionMapsProperty?.GetValue(asset) as System.Collections.IEnumerable;

                var mapResults = new List<object>();
                if (actionMaps != null)
                {
                    foreach (var map in actionMaps)
                    {
                        var mapType     = map.GetType();
                        var mapName     = mapType.GetProperty("name")?.GetValue(map)?.ToString();
                        var actionsCol  = mapType.GetProperty("actions")?.GetValue(map) as System.Collections.IEnumerable;

                        var actionResults = new List<object>();
                        int actionCount   = 0;

                        if (actionsCol != null)
                        {
                            foreach (var action in actionsCol)
                            {
                                actionCount++;
                                var actType      = action.GetType();
                                var actName      = actType.GetProperty("name")?.GetValue(action)?.ToString();
                                var actTypeVal   = actType.GetProperty("type")?.GetValue(action)?.ToString();
                                var bindingsCol  = actType.GetProperty("bindings")?.GetValue(action) as System.Collections.IEnumerable;

                                var bindingPaths = new List<string>();
                                if (bindingsCol != null)
                                {
                                    foreach (var binding in bindingsCol)
                                    {
                                        var path = binding.GetType().GetProperty("path")?.GetValue(binding)?.ToString();
                                        if (!string.IsNullOrEmpty(path))
                                            bindingPaths.Add(path);
                                    }
                                }

                                actionResults.Add(new { name = actName, type = actTypeVal, bindings = bindingPaths });
                            }
                        }

                        mapResults.Add(new { name = mapName, actionCount, actions = actionResults });
                    }
                }

                var assetName = _inputActionAssetType.GetProperty("name")?.GetValue(asset)?.ToString();

                return McpResponse.Success($"Input actions for '{assetName}'", new { assetPath, assetName, actionMapCount = mapResults.Count, actionMaps = mapResults });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get input actions: {ex.Message}");
            }
        }

        private static McpToolResult AddInputAction(Dictionary<string, object> args)
        {
            try
            {
                if (!EnsureInputSystem(out var sysError))
                    return McpToolResult.Error(sysError);

                var (assetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;
                var (sanitizedAssetPath, sanitizeErr) = TrySanitizePath(assetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;
                assetPath = sanitizedAssetPath;

                var (actionMapName, mapErr) = RequireArg(args, "actionMapName");
                if (mapErr != null) return mapErr;

                var (actionName, actionErr) = RequireArg(args, "actionName");
                if (actionErr != null) return actionErr;

                string actionTypeStr = ArgumentParser.GetString(args, "actionType", "Button");
                object actionTypeVal;
                try   { actionTypeVal = Enum.Parse(_inputActionTypeEnum, actionTypeStr); }
                catch { actionTypeVal = Enum.Parse(_inputActionTypeEnum, "Button"); }

                var asset = AssetDatabase.LoadAssetAtPath(assetPath, _inputActionAssetType);
                if (asset == null)
                    return McpToolResult.Error($"InputActionAsset not found at '{assetPath}'.");

                // asset.FindActionMap(actionMapName, throwIfNotFound: false)
                var findMap = _inputActionAssetType.GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) });
                var map     = findMap?.Invoke(asset, new object[] { actionMapName, false });
                if (map == null)
                    return McpToolResult.Error($"Action map '{actionMapName}' not found in '{assetPath}'.");

                // map.FindAction(actionName)
                var findAction = map.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) });
                var existing   = findAction?.Invoke(map, new object[] { actionName, false });
                if (existing != null)
                    return McpToolResult.Error($"Action '{actionName}' already exists in map '{actionMapName}'.");

                Undo.RecordObject(asset as UnityEngine.Object, $"Add Input Action '{actionName}'");

                // InputActionSetupExtensions.AddAction(map, actionName, actionType)
                var addAction = _setupExtType.GetMethod("AddAction",
                    new[] { map.GetType(), typeof(string), _inputActionTypeEnum, typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (addAction == null)
                {
                    // Fallback: find overload with fewer params
                    foreach (var m in _setupExtType.GetMethods())
                    {
                        if (m.Name != "AddAction") continue;
                        var prms = m.GetParameters();
                        if (prms.Length >= 3 && prms[1].ParameterType == typeof(string))
                        { addAction = m; break; }
                    }
                }

                if (addAction == null)
                    return McpToolResult.Error("Could not locate InputActionSetupExtensions.AddAction via reflection.");

                var paramCount  = addAction.GetParameters().Length;
                var invokeArgs  = new object[paramCount];
                invokeArgs[0]   = map;
                invokeArgs[1]   = actionName;
                if (paramCount > 2) invokeArgs[2] = actionTypeVal;
                // Remaining optional params remain null

                addAction.Invoke(null, invokeArgs);

                EditorUtility.SetDirty(asset as UnityEngine.Object);
                AssetDatabase.SaveAssets();

                return McpToolResult.Success($"Added action '{actionName}' (type: {actionTypeStr}) to map '{actionMapName}' in '{assetPath}'.");
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add input action: {ex.Message}");
            }
        }

        private static McpToolResult AddInputBinding(Dictionary<string, object> args)
        {
            try
            {
                if (!EnsureInputSystem(out var sysError))
                    return McpToolResult.Error(sysError);

                var (assetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;
                var (sanitizedAssetPath, sanitizeErr) = TrySanitizePath(assetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;
                assetPath = sanitizedAssetPath;

                var (actionMapName, mapErr) = RequireArg(args, "actionMapName");
                if (mapErr != null) return mapErr;

                var (actionName, actionErr) = RequireArg(args, "actionName");
                if (actionErr != null) return actionErr;

                var (controlPath, controlErr) = RequireArg(args, "controlPath");
                if (controlErr != null) return controlErr;

                var asset = AssetDatabase.LoadAssetAtPath(assetPath, _inputActionAssetType);
                if (asset == null)
                    return McpToolResult.Error($"InputActionAsset not found at '{assetPath}'.");

                var findMap = _inputActionAssetType.GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) });
                var map     = findMap?.Invoke(asset, new object[] { actionMapName, false });
                if (map == null)
                    return McpToolResult.Error($"Action map '{actionMapName}' not found.");

                var findAction = map.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) });
                var action     = findAction?.Invoke(map, new object[] { actionName, false });
                if (action == null)
                    return McpToolResult.Error($"Action '{actionName}' not found in map '{actionMapName}'.");

                Undo.RecordObject(asset as UnityEngine.Object, $"Add Binding '{controlPath}' to '{actionName}'");

                // InputActionSetupExtensions.AddBinding(action, controlPath)
                MethodInfo addBinding = null;
                foreach (var m in _setupExtType.GetMethods())
                {
                    if (m.Name != "AddBinding") continue;
                    var prms = m.GetParameters();
                    if (prms.Length >= 2 && prms[1].ParameterType == typeof(string))
                    { addBinding = m; break; }
                }

                if (addBinding == null)
                    return McpToolResult.Error("Could not locate InputActionSetupExtensions.AddBinding via reflection.");

                var paramCount = addBinding.GetParameters().Length;
                var invokeArgs = new object[paramCount];
                invokeArgs[0]  = action;
                invokeArgs[1]  = controlPath;

                addBinding.Invoke(null, invokeArgs);

                EditorUtility.SetDirty(asset as UnityEngine.Object);
                AssetDatabase.SaveAssets();

                return McpResponse.Success($"Binding '{controlPath}' added to action '{actionName}'", new { assetPath, actionMapName, actionName, controlPath });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add input binding: {ex.Message}");
            }
        }

        #endregion
    }
}
