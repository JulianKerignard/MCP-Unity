using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Utils;

namespace McpUnity.Server
{
    /// <summary>
    /// Partial class containing GameObject management tools
        /// Tools: list_gameobjects, create_gameobject, delete_gameobject, rename_gameobject, set_parent, duplicate_gameobject, move_gameobject, find_gameobjects_by_component, get_selection, set_selection
    /// </summary>
    public partial class McpUnityServer
    {
        #region GameObject Tool Registrations

        static partial void RegisterGameObjectTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_gameobjects",
                description = "List GameObjects in scene. Use outputMode='tree' for compact view (saves 90% tokens)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["outputMode"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Output format: 'tree' (compact ASCII), 'names' (just names), 'summary' (names+components), 'full' (all details). Default: 'summary'",
                            @enum = new List<string> { "names", "tree", "summary", "full" }
                        },
                        ["maxDepth"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Maximum hierarchy depth (default: 3)"
                        },
                        ["includeInactive"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Include inactive GameObjects (default: false)"
                        },
                        ["rootOnly"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Only return root objects, no children (default: false)"
                        },
                        ["nameFilter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Filter by name pattern (supports * wildcard, e.g., 'Enemy*')"
                        },
                        ["componentFilter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Only objects with this component (e.g., 'Rigidbody')"
                        },
                        ["tagFilter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Filter by tag (e.g., 'Player')"
                        },
                        ["includeTransform"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Include position/rotation/scale in 'full' mode (default: false)"
                        },
                        ["sceneName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Target a specific loaded scene by name (default: active scene)"
                        }
                    }
                }
            }, ListGameObjects);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_gameobject",
                description = "Create a GameObject with name, type, position, rotation, and scale in one call. Transform is set at creation — no need to call unity_set_transform after.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["name"]          = new McpPropertySchema { type = "string", description = "Name of the GameObject" },
                        ["primitiveType"] = new McpPropertySchema { type = "string", description = "Mesh type (default: Empty)", @enum = new List<string> { "Empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" } },
                        ["parentPath"]    = new McpPropertySchema { type = "string", description = "Parent GameObject path (optional)" },
                        ["position"]      = new McpPropertySchema { type = "object", description = "World position {x, y, z}" },
                        ["rotation"]      = new McpPropertySchema { type = "object", description = "Rotation in euler angles {x, y, z}" },
                        ["scale"]         = new McpPropertySchema { type = "object", description = "Local scale {x, y, z} (default: {1,1,1})" },
                        ["components"]    = new McpPropertySchema { type = "array", description = "Components to add at creation: [{ type, properties? }] e.g. [{ \"type\": \"Rigidbody\" }, { \"type\": \"BoxCollider\" }]" }
                    },
                    required = new List<string> { "name" }
                }
            }, CreateGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_gameobject_batch",
                description = "Create multiple GameObjects in one call (2+ objects). Same params per object as unity_create_gameobject. One Undo step.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["objects"]     = new McpPropertySchema { type = "array", description = "Array of objects: { name (required), primitiveType, parentPath, position {x,y,z}, rotation {x,y,z}, scale {x,y,z}, components [{type, properties?}] }" },
                        ["stopOnError"] = new McpPropertySchema { type = "boolean", description = "Stop on first error (default: false)" }
                    },
                    required = new List<string> { "objects" }
                }
            }, CreateGameObjectBatch);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_delete_gameobject",
                description = "Delete a GameObject from the scene by name or path",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject to delete (e.g., 'Player' or 'Environment/Props/Tree')"
                        }
                    },
                    required = new List<string> { "path" }
                }
            }, DeleteGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_rename_gameobject",
                description = "Rename a GameObject",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject to rename"
                        },
                        ["newName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "New name for the GameObject"
                        }
                    },
                    required = new List<string> { "gameObjectPath", "newName" }
                }
            }, RenameGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_parent",
                description = "Set or change the parent of a GameObject",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject"
                        },
                        ["parentPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to new parent (null or empty to move to scene root)"
                        },
                        ["worldPositionStays"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Keep world position when reparenting (default: true)"
                        }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetParent);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_duplicate_gameobject",
                description = "Duplicate a GameObject (creates a copy with the same parent)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject to duplicate"
                        },
                        ["newName"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional name for the duplicate (defaults to original name)"
                        }
                    },
                    required = new List<string> { "path" }
                }
            }, DuplicateGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_move_gameobject",
                description = "Reorder a GameObject in the hierarchy (sibling index). NOT for spatial movement — use unity_set_transform to move/rotate/scale.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject to move"
                        },
                        ["siblingIndex"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "New sibling index within parent (0 = first child). Use -1 to move to last."
                        }
                    },
                    required = new List<string> { "path", "siblingIndex" }
                }
            }, MoveGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_find_gameobjects_by_component",
                description = "Find all GameObjects that have a specific component attached",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["componentType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Component type name to search for (e.g., 'Rigidbody', 'Camera', 'MyScript')"
                        },
                        ["includeInactive"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Include inactive GameObjects (default: false)"
                        }
                    },
                    required = new List<string> { "componentType" }
                }
            }, FindGameObjectsByComponent);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_gameobject_active",
                description = "Activate or deactivate a GameObject in the scene (equivalent to the checkbox in the Inspector)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path or name of the GameObject (searches inactive objects too)"
                        },
                        ["active"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Target active state: true to activate, false to deactivate"
                        }
                    },
                    required = new List<string> { "path", "active" }
                }
            }, SetGameObjectActive);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_gameobject",
                description = "Get full details of ONE specific GameObject: world/local transform, all components with properties, children list",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"] = new McpPropertySchema { type = "string", description = "Path or name of the GameObject (finds inactive objects too)" },
                        ["includeProperties"] = new McpPropertySchema { type = "boolean", description = "Include component properties via reflection (default: true)" },
                        ["includeChildren"] = new McpPropertySchema { type = "boolean", description = "Include direct children list (default: true)" }
                    },
                    required = new List<string> { "path" }
                }
            }, GetGameObject);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_transform",
                description = "Move, rotate, and/or scale a GameObject. Set position, rotation and/or scale in one call. Only updates provided fields. Supports world and local space. This is the primary tool for positioning objects in the scene.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["path"]          = new McpPropertySchema { type = "string", description = "Path or name of the GameObject" },
                        ["position"]      = new McpPropertySchema { type = "object", description = "World position {x, y, z}" },
                        ["rotation"]      = new McpPropertySchema { type = "object", description = "World rotation in euler angles {x, y, z}" },
                        ["localPosition"] = new McpPropertySchema { type = "object", description = "Local position {x, y, z}" },
                        ["localRotation"] = new McpPropertySchema { type = "object", description = "Local euler rotation {x, y, z}" },
                        ["localScale"]    = new McpPropertySchema { type = "object", description = "Local scale {x, y, z}" }
                    },
                    required = new List<string> { "path" }
                }
            }, SetTransform);

            // NOTE: unity_get_selection and unity_set_selection are registered in EditorTools.cs
        }

        #endregion

        #region GameObject Handlers

        private static McpToolResult ListGameObjects(Dictionary<string, object> args)
        {
            // Parse parameters with optimized defaults
            string outputMode = ArgumentParser.GetString(args, "outputMode", "summary").ToLower();
            int maxDepth = ArgumentParser.GetIntClamped(args, "maxDepth", 3, 1, 50);
            bool includeInactive = ArgumentParser.GetBool(args, "includeInactive", false);
            bool rootOnly = ArgumentParser.GetBool(args, "rootOnly", false);
            bool includeTransform = ArgumentParser.GetBool(args, "includeTransform", false);
            string nameFilter = ArgumentParser.GetString(args, "nameFilter", null);
            string componentFilter = ArgumentParser.GetString(args, "componentFilter", null);
            string tagFilter = ArgumentParser.GetString(args, "tagFilter", null);
            string sceneName = ArgumentParser.GetString(args, "sceneName", null);

            // Resolve target scene — by name if specified, otherwise active scene
            UnityEngine.SceneManagement.Scene scene;
            if (!string.IsNullOrEmpty(sceneName))
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                    return McpToolResult.Error($"Scene '{sceneName}' is not loaded. Use unity_load_scene first or unity_list_scenes_in_project to see available scenes.");
            }
            else
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            }

            var rootObjects = scene.GetRootGameObjects().ToList();

            // Apply filters if any
            bool hasFilters = !string.IsNullOrEmpty(nameFilter) || !string.IsNullOrEmpty(componentFilter) || !string.IsNullOrEmpty(tagFilter);

            object resultData;

            switch (outputMode)
            {
                case "names":
                    if (hasFilters)
                    {
                        var filtered = HierarchyHelpers.CollectFilteredObjects(rootObjects, nameFilter, componentFilter, tagFilter, null, rootOnly, includeInactive);
                        resultData = filtered.Select(o => o.name).ToList();
                    }
                    else
                    {
                        resultData = HierarchyHelpers.GetObjectNames(rootObjects, includeInactive);
                    }
                    break;

                case "tree":
                    if (hasFilters)
                    {
                        var filtered = HierarchyHelpers.CollectFilteredObjects(rootObjects, nameFilter, componentFilter, tagFilter, null, rootOnly, includeInactive);
                        resultData = HierarchyHelpers.FormatAsTree(filtered, maxDepth, includeInactive);
                    }
                    else
                    {
                        resultData = HierarchyHelpers.FormatAsTree(rootObjects, maxDepth, includeInactive);
                    }
                    break;

                case "summary":
                    if (hasFilters)
                    {
                        var filtered = HierarchyHelpers.CollectFilteredObjects(rootObjects, nameFilter, componentFilter, tagFilter, null, rootOnly, includeInactive);
                        resultData = HierarchyHelpers.GetSummaryInfo(filtered, maxDepth, includeInactive);
                    }
                    else
                    {
                        resultData = HierarchyHelpers.GetSummaryInfo(rootObjects, maxDepth, includeInactive);
                    }
                    break;

                case "full":
                default:
                    var gameObjects = new List<object>();
                    if (hasFilters)
                    {
                        var filtered = HierarchyHelpers.CollectFilteredObjects(rootObjects, nameFilter, componentFilter, tagFilter, null, rootOnly, includeInactive);
                        foreach (var obj in filtered)
                        {
                            gameObjects.Add(GetDetailedGameObjectInfo(obj, 0, maxDepth, includeInactive, includeTransform));
                        }
                    }
                    else
                    {
                        foreach (var obj in rootObjects)
                        {
                            if (!includeInactive && !obj.activeSelf) continue;
                            gameObjects.Add(GetDetailedGameObjectInfo(obj, 0, maxDepth, includeInactive, includeTransform));
                        }
                    }
                    resultData = gameObjects;
                    break;
            }

            var result = new
            {
                sceneName = scene.name,
                outputMode = outputMode,
                totalRootObjects = rootObjects.Count,
                data = resultData
            };

            return McpResponse.Success(result);
        }

        private static McpToolResult CreateGameObject(Dictionary<string, object> args)
        {
            // Wrap single-object params into the batch format and delegate
            var batchArgs = new Dictionary<string, object>
            {
                ["objects"] = new List<object> { args }
            };
            var batchResult = CreateGameObjectBatch(batchArgs);

            // Unwrap: return the single item result directly instead of the batch envelope
            if (batchResult.isError) return batchResult;
            try
            {
                string json = batchResult.content.Count > 0 ? batchResult.content[0].text : null;
                var parsed = !string.IsNullOrEmpty(json) ? JsonHelper.ParseJsonObject(json) : null;
                if (parsed != null
                    && parsed.TryGetValue("results", out var resObj) && resObj is List<object> resList
                    && resList.Count > 0 && resList[0] is Dictionary<string, object> singleResult)
                {
                    if (singleResult.TryGetValue("success", out var s) && s is bool sb && !sb)
                    {
                        string err = singleResult.TryGetValue("error", out var ev) ? ev?.ToString() : "Creation failed";
                        return McpToolResult.Error(err);
                    }
                    return McpResponse.Success(singleResult);
                }
            }
            catch { /* fall through to raw batch result */ }
            return batchResult;
        }

        private static McpToolResult CreateGameObjectBatch(Dictionary<string, object> args)
        {
            if (!args.TryGetValue("objects", out var objectsObj) || !(objectsObj is List<object> objectsRaw) || objectsRaw.Count == 0)
                return McpToolResult.Error("'objects' array is required and must not be empty.");

            bool stopOnError = ArgumentParser.GetBool(args, "stopOnError", false);
            var  results     = new List<object>(objectsRaw.Count);
            int  created     = 0;
            int  failed      = 0;

            Undo.SetCurrentGroupName("Create GameObjects Batch");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < objectsRaw.Count; i++)
            {
                if (!(objectsRaw[i] is Dictionary<string, object> od))
                {
                    results.Add(new Dictionary<string, object> { ["index"] = i, ["success"] = false, ["error"] = $"objects[{i}] must be an object." });
                    failed++;
                    if (stopOnError) break;
                    continue;
                }

                string objName = ArgumentParser.GetString(od, "name", null);
                if (string.IsNullOrEmpty(objName))
                {
                    results.Add(new Dictionary<string, object> { ["index"] = i, ["success"] = false, ["error"] = $"objects[{i}]: 'name' is required." });
                    failed++;
                    if (stopOnError) break;
                    continue;
                }

                try
                {
                    string primitiveType = ArgumentParser.GetString(od, "primitiveType", "Empty");
                    string parentPath    = ArgumentParser.GetString(od, "parentPath", null);

                    GameObject newObj = null;
                    switch (primitiveType)
                    {
                        case "Cube":     newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);     break;
                        case "Sphere":   newObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);   break;
                        case "Capsule":  newObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);  break;
                        case "Cylinder": newObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                        case "Plane":    newObj = GameObject.CreatePrimitive(PrimitiveType.Plane);    break;
                        case "Quad":     newObj = GameObject.CreatePrimitive(PrimitiveType.Quad);     break;
                        default:         newObj = new GameObject();                                    break;
                    }
                    newObj.name = objName;

                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        var parent = GameObjectHelpers.FindGameObject(parentPath);
                        if (parent == null)
                        {
                            UnityEngine.Object.DestroyImmediate(newObj);
                            results.Add(new Dictionary<string, object> { ["index"] = i, ["name"] = objName, ["success"] = false, ["error"] = $"Parent not found: '{parentPath}'" });
                            failed++;
                            if (stopOnError) break;
                            continue;
                        }
                        newObj.transform.SetParent(parent.transform);
                    }

                    if (od.TryGetValue("position", out var posObj) && posObj is Dictionary<string, object> posDict)
                    {
                        newObj.transform.position = new Vector3(
                            ArgumentParser.GetFloat(posDict, "x", 0f),
                            ArgumentParser.GetFloat(posDict, "y", 0f),
                            ArgumentParser.GetFloat(posDict, "z", 0f));
                    }

                    if (od.TryGetValue("rotation", out var rotObj) && rotObj is Dictionary<string, object> rotDict)
                    {
                        newObj.transform.rotation = Quaternion.Euler(
                            ArgumentParser.GetFloat(rotDict, "x", 0f),
                            ArgumentParser.GetFloat(rotDict, "y", 0f),
                            ArgumentParser.GetFloat(rotDict, "z", 0f));
                    }

                    if (od.TryGetValue("scale", out var scaleObj) && scaleObj is Dictionary<string, object> scaleDict)
                    {
                        newObj.transform.localScale = new Vector3(
                            ArgumentParser.GetFloat(scaleDict, "x", 1f),
                            ArgumentParser.GetFloat(scaleDict, "y", 1f),
                            ArgumentParser.GetFloat(scaleDict, "z", 1f));
                    }

                    // Add components if specified
                    var addedComponents = new List<string>();
                    if (od.TryGetValue("components", out var compsObj) && compsObj is List<object> compsList)
                    {
                        foreach (var compItem in compsList)
                        {
                            if (!(compItem is Dictionary<string, object> compDict)) continue;
                            string compType = ArgumentParser.GetString(compDict, "type", null);
                            if (string.IsNullOrEmpty(compType)) continue;
                            // Skip Transform — always present
                            if (compType.Equals("Transform", System.StringComparison.OrdinalIgnoreCase)) continue;

                            var type = FindComponentType(compType);
                            if (type == null) continue;

                            var comp = newObj.AddComponent(type);
                            if (comp != null)
                            {
                                addedComponents.Add(type.Name);
                                if (compDict.TryGetValue("properties", out var propsObj) && propsObj is Dictionary<string, object> props)
                                {
                                    TypeConverter.ApplyComponentProperties(comp, props);
                                }
                            }
                        }
                    }

                    Undo.RegisterCreatedObjectUndo(newObj, $"Create {objName}");

                    var resultEntry = new Dictionary<string, object>
                    {
                        ["index"]   = i,
                        ["name"]    = objName,
                        ["path"]    = GameObjectHelpers.GetGameObjectPath(newObj),
                        ["success"] = true
                    };
                    if (addedComponents.Count > 0)
                        resultEntry["components"] = addedComponents;
                    results.Add(resultEntry);
                    created++;
                }
                catch (Exception ex)
                {
                    results.Add(new Dictionary<string, object> { ["index"] = i, ["name"] = objName, ["success"] = false, ["error"] = ex.Message });
                    failed++;
                    if (stopOnError) break;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            return McpResponse.Success(
                $"Batch create: {created} created, {failed} failed (total: {objectsRaw.Count})",
                new Dictionary<string, object>
                {
                    ["created"] = created,
                    ["failed"]  = failed,
                    ["total"]   = objectsRaw.Count,
                    ["results"] = results
                });
        }

        private static McpToolResult DeleteGameObject(Dictionary<string, object> args)
        {
            var (path, pathErr) = RequireArg(args, "path");
            if (pathErr != null) return pathErr;

            var obj = GameObjectHelpers.FindGameObject(path);

            if (obj == null)
            {
                return McpToolResult.Error($"GameObject not found: {path}");
            }

            string deletedPath = GameObjectHelpers.GetGameObjectPath(obj);
            Undo.DestroyObjectImmediate(obj);

            return McpToolResult.Success($"Deleted GameObject: {deletedPath}");
        }

        private static McpToolResult RenameGameObject(Dictionary<string, object> args)
        {
            var (gameObject, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            var (newName, nameErr) = RequireArg(args, "newName");
            if (nameErr != null) return nameErr;

            var oldName = gameObject.name;

            Undo.RecordObject(gameObject, "Rename GameObject");
            gameObject.name = newName;
            EditorUtility.SetDirty(gameObject);

            return McpResponse.Success(new
            {
                success = true,
                oldName = oldName,
                newName = newName,
                path = GameObjectHelpers.GetGameObjectPath(gameObject)
            });
        }

        private static McpToolResult SetParent(Dictionary<string, object> args)
        {
            var (gameObject, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            GameObject newParent = null;
            string parentName = "(scene root)";
            string parentPath = ArgumentParser.GetString(args, "parentPath", null);

            if (!string.IsNullOrEmpty(parentPath))
            {
                newParent = GameObjectHelpers.FindGameObject(parentPath);
                if (newParent == null)
                {
                    return McpToolResult.Error($"Parent GameObject not found: {parentPath}");
                }

                // Prevent parenting to self or child
                if (newParent == gameObject || newParent.transform.IsChildOf(gameObject.transform))
                {
                    return McpToolResult.Error("Cannot parent an object to itself or its children");
                }

                parentName = newParent.name;
            }

            bool worldPositionStays = ArgumentParser.GetBool(args, "worldPositionStays", true);

            // Pass worldPositionStays to Undo so undo/redo restores the correct local transform
            Undo.SetTransformParent(gameObject.transform, newParent?.transform, worldPositionStays, "Set Parent");
            EditorUtility.SetDirty(gameObject);

            return McpResponse.Success(new
            {
                success = true,
                child = gameObject.name,
                newParent = parentName,
                worldPositionStays = worldPositionStays,
                newPath = GameObjectHelpers.GetGameObjectPath(gameObject)
            });
        }

        private static McpToolResult DuplicateGameObject(Dictionary<string, object> args)
        {
            try
            {
                var (path, pathErr) = RequireArg(args, "path");
                if (pathErr != null) return pathErr;

                var original = GameObjectHelpers.FindGameObject(path);

                if (original == null)
                    return McpToolResult.Error($"GameObject not found: '{path}'");

                string newName = ArgumentParser.GetString(args, "newName", null);

                var clone = UnityEngine.Object.Instantiate(original, original.transform.parent);
                clone.name = !string.IsNullOrEmpty(newName) ? newName : original.name;

                Undo.RegisterCreatedObjectUndo(clone, $"Duplicate {original.name}");
                Selection.activeGameObject = clone;

                return McpResponse.Success(new
                {
                    success = true,
                    originalPath = GameObjectHelpers.GetGameObjectPath(original),
                    duplicatePath = GameObjectHelpers.GetGameObjectPath(clone),
                    duplicateName = clone.name
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to duplicate GameObject: {ex.Message}");
            }
        }

        private static McpToolResult MoveGameObject(Dictionary<string, object> args)
        {
            try
            {
                var (path, pathErr) = RequireArg(args, "path");
                if (pathErr != null) return pathErr;

                var gameObject = GameObjectHelpers.FindGameObject(path);

                if (gameObject == null)
                    return McpToolResult.Error($"GameObject not found: '{path}'");

                int siblingIndex = ArgumentParser.GetInt(args, "siblingIndex", 0);
                int siblingCount = gameObject.transform.parent != null
                    ? gameObject.transform.parent.childCount
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount;

                // -1 means last
                if (siblingIndex < 0)
                    siblingIndex = siblingCount - 1;

                siblingIndex = Mathf.Clamp(siblingIndex, 0, siblingCount - 1);

                Undo.RecordObject(gameObject.transform, $"Move {gameObject.name} to index {siblingIndex}");
                gameObject.transform.SetSiblingIndex(siblingIndex);
                EditorUtility.SetDirty(gameObject);

                return McpResponse.Success(new
                {
                    success = true,
                    path = GameObjectHelpers.GetGameObjectPath(gameObject),
                    newSiblingIndex = gameObject.transform.GetSiblingIndex()
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to move GameObject: {ex.Message}");
            }
        }

        private static McpToolResult FindGameObjectsByComponent(Dictionary<string, object> args)
        {
            try
            {
                var (componentTypeName, compTypeErr) = RequireArg(args, "componentType");
                if (compTypeErr != null) return compTypeErr;

                bool includeInactive = ArgumentParser.GetBool(args, "includeInactive", false);

                // Resolve type — try simple name first, then full namespace scan
                Type componentType = Type.GetType(componentTypeName);
                if (componentType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        // Skip system/Microsoft assemblies for performance
                        var asmName = assembly.GetName().Name;
                        if (asmName.StartsWith("System") || asmName.StartsWith("Microsoft") || asmName.StartsWith("mscorlib"))
                            continue;

                        componentType = assembly.GetType(componentTypeName);
                        if (componentType != null) break;

                        // Try searching by simple name
                        foreach (var t in assembly.GetTypes())
                        {
                            if (t.Name == componentTypeName && typeof(Component).IsAssignableFrom(t))
                            {
                                componentType = t;
                                break;
                            }
                        }
                        if (componentType != null) break;
                    }
                }

                if (componentType == null)
                    return McpToolResult.Error($"Component type not found: '{componentTypeName}'. Use unity_list_project_scripts to find available scripts.");

                var foundObjects = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsSortMode.None);

                var results = new List<object>();
                foreach (var obj in foundObjects)
                {
                    if (obj is Component comp)
                    {
                        if (!includeInactive && !comp.gameObject.activeSelf) continue;
                        results.Add(new
                        {
                            name = comp.gameObject.name,
                            path = GameObjectHelpers.GetGameObjectPath(comp.gameObject),
                            active = comp.gameObject.activeSelf
                        });
                    }
                }

                return McpResponse.Success(new
                {
                    componentType = componentTypeName,
                    count = results.Count,
                    gameObjects = results
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to find GameObjects by component: {ex.Message}");
            }
        }

        private static McpToolResult SetGameObjectActive(Dictionary<string, object> args)
        {
            try
            {
                var (path, pathErr) = RequireArg(args, "path");
                if (pathErr != null) return pathErr;

                bool active = ArgumentParser.GetBool(args, "active", true);

                // FindGameObject handles both active and inactive via scene traversal
                GameObject go = GameObjectHelpers.FindGameObject(path);
                if (go == null)
                {
                    // Last resort: brute-force search including inactive objects by name
                    var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var obj in allObjects)
                    {
                        if (obj.name == path || GameObjectHelpers.GetGameObjectPath(obj) == path)
                        {
                            go = obj;
                            break;
                        }
                    }
                }

                if (go == null)
                    return McpToolResult.Error($"GameObject not found: '{path}'");

                Undo.RecordObject(go, $"{(active ? "Activate" : "Deactivate")} {go.name}");
                go.SetActive(active);
                EditorUtility.SetDirty(go);

                return McpResponse.Success(
                    $"GameObject '{go.name}' set to {(active ? "active" : "inactive")}",
                    new Dictionary<string, object>
                    {
                        ["path"] = GameObjectHelpers.GetGameObjectPath(go),
                        ["active"] = go.activeSelf
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set active state: {ex.Message}");
            }
        }

        #endregion

        private static McpToolResult GetGameObject(Dictionary<string, object> args)
        {
            var (go, path, goErr) = RequireGameObject(args, "path");
            if (goErr != null) return goErr;

            bool includeProperties = ArgumentParser.GetBool(args, "includeProperties", true);
            bool includeChildren   = ArgumentParser.GetBool(args, "includeChildren", true);

            var t = go.transform;

            // Components
            var components = new List<object>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var compInfo = new Dictionary<string, object>
                {
                    ["type"]    = comp.GetType().Name,
                    ["enabled"] = comp is Behaviour b ? (object)b.enabled : true
                };
                if (includeProperties)
                    compInfo["properties"] = TypeConverter.ConvertToSerializable(comp);
                components.Add(compInfo);
            }

            // Direct children
            var children = new List<object>();
            if (includeChildren)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i);
                    children.Add(new
                    {
                        name   = child.name,
                        path   = GameObjectHelpers.GetGameObjectPath(child.gameObject),
                        active = child.gameObject.activeSelf
                    });
                }
            }

            return McpResponse.Success(new
            {
                name              = go.name,
                path              = GameObjectHelpers.GetGameObjectPath(go),
                active            = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                tag               = go.tag,
                layer             = go.layer,
                layerName         = LayerMask.LayerToName(go.layer),
                isStatic          = go.isStatic,
                worldPosition     = new { x = t.position.x,      y = t.position.y,      z = t.position.z },
                worldRotation     = new { x = t.eulerAngles.x,   y = t.eulerAngles.y,   z = t.eulerAngles.z },
                localPosition     = new { x = t.localPosition.x, y = t.localPosition.y, z = t.localPosition.z },
                localRotation     = new { x = t.localEulerAngles.x, y = t.localEulerAngles.y, z = t.localEulerAngles.z },
                localScale        = new { x = t.localScale.x,    y = t.localScale.y,    z = t.localScale.z },
                componentCount    = components.Count,
                components        = components,
                childCount        = t.childCount,
                children          = children
            });
        }

        private static McpToolResult SetTransform(Dictionary<string, object> args)
        {
            var (go, path, goErr) = RequireGameObject(args, "path");
            if (goErr != null) return goErr;

            var t = go.transform;
            Undo.RecordObject(t, "Set Transform");

            var modified = new List<string>();

            if (args.TryGetValue("position", out var posObj) && posObj is Dictionary<string, object> posD)
            {
                t.position = new Vector3(
                    ArgumentParser.GetFloat(posD, "x", t.position.x),
                    ArgumentParser.GetFloat(posD, "y", t.position.y),
                    ArgumentParser.GetFloat(posD, "z", t.position.z));
                modified.Add("position");
            }

            if (args.TryGetValue("rotation", out var rotObj) && rotObj is Dictionary<string, object> rotD)
            {
                t.eulerAngles = new Vector3(
                    ArgumentParser.GetFloat(rotD, "x", t.eulerAngles.x),
                    ArgumentParser.GetFloat(rotD, "y", t.eulerAngles.y),
                    ArgumentParser.GetFloat(rotD, "z", t.eulerAngles.z));
                modified.Add("rotation");
            }

            if (args.TryGetValue("localPosition", out var lposObj) && lposObj is Dictionary<string, object> lposD)
            {
                t.localPosition = new Vector3(
                    ArgumentParser.GetFloat(lposD, "x", t.localPosition.x),
                    ArgumentParser.GetFloat(lposD, "y", t.localPosition.y),
                    ArgumentParser.GetFloat(lposD, "z", t.localPosition.z));
                modified.Add("localPosition");
            }

            if (args.TryGetValue("localRotation", out var lrotObj) && lrotObj is Dictionary<string, object> lrotD)
            {
                t.localEulerAngles = new Vector3(
                    ArgumentParser.GetFloat(lrotD, "x", t.localEulerAngles.x),
                    ArgumentParser.GetFloat(lrotD, "y", t.localEulerAngles.y),
                    ArgumentParser.GetFloat(lrotD, "z", t.localEulerAngles.z));
                modified.Add("localRotation");
            }

            if (args.TryGetValue("localScale", out var scaleObj) && scaleObj is Dictionary<string, object> scaleD)
            {
                t.localScale = new Vector3(
                    ArgumentParser.GetFloat(scaleD, "x", t.localScale.x),
                    ArgumentParser.GetFloat(scaleD, "y", t.localScale.y),
                    ArgumentParser.GetFloat(scaleD, "z", t.localScale.z));
                modified.Add("localScale");
            }

            if (modified.Count == 0)
                return McpToolResult.Error("No transform fields provided. Use: position, rotation, localPosition, localRotation, or localScale.");

            EditorUtility.SetDirty(go);

            return McpResponse.Success(new
            {
                path          = GameObjectHelpers.GetGameObjectPath(go),
                modified      = modified,
                worldPosition = new { x = t.position.x,         y = t.position.y,         z = t.position.z },
                worldRotation = new { x = t.eulerAngles.x,      y = t.eulerAngles.y,      z = t.eulerAngles.z },
                localPosition = new { x = t.localPosition.x,    y = t.localPosition.y,    z = t.localPosition.z },
                localRotation = new { x = t.localEulerAngles.x, y = t.localEulerAngles.y, z = t.localEulerAngles.z },
                localScale    = new { x = t.localScale.x,       y = t.localScale.y,       z = t.localScale.z }
            });
        }

        #region GameObject Helpers

        private static object GetDetailedGameObjectInfo(GameObject obj, int depth, int maxDepth, bool includeInactive, bool includeTransform = false)
        {
            var components = obj.GetComponents<Component>();
            var componentInfos = new List<object>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                componentInfos.Add(new
                {
                    type = comp.GetType().Name,
                    enabled = (comp is Behaviour b) ? b.enabled : true
                });
            }

            var children = new List<object>();
            if (depth < maxDepth)
            {
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    var child = obj.transform.GetChild(i).gameObject;
                    if (!includeInactive && !child.activeSelf) continue;
                    children.Add(GetDetailedGameObjectInfo(child, depth + 1, maxDepth, includeInactive, includeTransform));
                }
            }

            // Build result - only include transform if explicitly requested (saves tokens)
            if (includeTransform)
            {
                return new
                {
                    name = obj.name,
                    path = GameObjectHelpers.GetGameObjectPath(obj),
                    active = obj.activeSelf,
                    layer = LayerMask.LayerToName(obj.layer),
                    tag = obj.tag,
                    position = new { x = obj.transform.position.x, y = obj.transform.position.y, z = obj.transform.position.z },
                    rotation = new { x = obj.transform.eulerAngles.x, y = obj.transform.eulerAngles.y, z = obj.transform.eulerAngles.z },
                    scale = new { x = obj.transform.localScale.x, y = obj.transform.localScale.y, z = obj.transform.localScale.z },
                    components = componentInfos,
                    childCount = obj.transform.childCount,
                    children = children
                };
            }
            else
            {
                // Compact version without transform data
                return new
                {
                    name = obj.name,
                    path = GameObjectHelpers.GetGameObjectPath(obj),
                    active = obj.activeSelf,
                    layer = LayerMask.LayerToName(obj.layer),
                    tag = obj.tag,
                    components = componentInfos,
                    childCount = obj.transform.childCount,
                    children = children
                };
            }
        }

        #endregion
    }
}
