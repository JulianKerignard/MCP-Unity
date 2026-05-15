using System;
using System.Collections.Generic;
using System.Linq;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Physics tools for MCP Unity Server.
    /// Contains 4 tools: Raycast, SetupRigidbody, SetupCollider, SetPhysicsMaterial
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all physics-related tools
        /// </summary>
        static partial void RegisterPhysicsTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_raycast",
                description = "Cast a ray in the physics scene and return all hits",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["origin"] = new McpPropertySchema { type = "object", description = "Ray origin {x, y, z}" },
                        ["direction"] = new McpPropertySchema { type = "object", description = "Ray direction {x, y, z}" },
                        ["maxDistance"] = new McpPropertySchema { type = "number", description = "Maximum ray distance" },
                        ["layerNames"] = new McpPropertySchema { type = "array", description = "Layer names to include (all layers if omitted)" },
                        ["maxHits"] = new McpPropertySchema { type = "integer", description = "Maximum hits to return" }
                    },
                    required = new List<string> { "origin", "direction" }
                }
            }, Raycast);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_setup_rigidbody",
                description = "Add or configure a Rigidbody on a GameObject",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject" },
                        ["mass"] = new McpPropertySchema { type = "number", description = "Mass" },
                        ["drag"] = new McpPropertySchema { type = "number", description = "Drag" },
                        ["angularDrag"] = new McpPropertySchema { type = "number", description = "Angular drag" },
                        ["useGravity"] = new McpPropertySchema { type = "boolean", description = "Use gravity" },
                        ["isKinematic"] = new McpPropertySchema { type = "boolean", description = "Is kinematic" },
                        ["interpolation"] = new McpPropertySchema { type = "string", description = "Interpolation: None, Interpolate, Extrapolate" },
                        ["constraints"] = new McpPropertySchema { type = "array", description = "Constraints: FreezePositionX/Y/Z, FreezeRotationX/Y/Z, FreezePosition, FreezeRotation, FreezeAll" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetupRigidbody);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_setup_collider",
                description = "Add a collider to a GameObject (Box, Sphere, Capsule, Mesh, or auto-detect)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject" },
                        ["colliderType"] = new McpPropertySchema { type = "string", description = "Type: Box, Sphere, Capsule, Mesh, auto" },
                        ["isTrigger"] = new McpPropertySchema { type = "boolean", description = "Is trigger" },
                        ["center"] = new McpPropertySchema { type = "object", description = "Center offset {x, y, z}" },
                        ["size"] = new McpPropertySchema { type = "object", description = "Size for BoxCollider {x, y, z}" },
                        ["radius"] = new McpPropertySchema { type = "number", description = "Radius for Sphere/Capsule" },
                        ["height"] = new McpPropertySchema { type = "number", description = "Height for CapsuleCollider" },
                        ["direction"] = new McpPropertySchema { type = "integer", description = "Capsule direction: 0=X, 1=Y, 2=Z" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetupCollider);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_physics_material",
                description = "Create and assign a PhysicMaterial to a collider",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject with a Collider" },
                        ["dynamicFriction"] = new McpPropertySchema { type = "number", description = "Dynamic friction" },
                        ["staticFriction"] = new McpPropertySchema { type = "number", description = "Static friction" },
                        ["bounciness"] = new McpPropertySchema { type = "number", description = "Bounciness" },
                        ["frictionCombine"] = new McpPropertySchema { type = "string", description = "Friction combine: Average, Minimum, Maximum, Multiply" },
                        ["bounceCombine"] = new McpPropertySchema { type = "string", description = "Bounce combine: Average, Minimum, Maximum, Multiply" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Asset path to save the material" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetPhysicsMaterial);
        }

        #region Physics Handlers

        private static McpToolResult Raycast(Dictionary<string, object> args)
        {
            try
            {
                // Parse origin
                if (!args.TryGetValue("origin", out var originObj) || !(originObj is Dictionary<string, object> originDict))
                    return McpToolResult.Error("origin is required and must be an object with x, y, z");

                var origin = new Vector3(
                    ArgumentParser.GetFloat(originDict, "x", 0f),
                    ArgumentParser.GetFloat(originDict, "y", 0f),
                    ArgumentParser.GetFloat(originDict, "z", 0f)
                );

                // Parse direction
                if (!args.TryGetValue("direction", out var dirObj) || !(dirObj is Dictionary<string, object> dirDict))
                    return McpToolResult.Error("direction is required and must be an object with x, y, z");

                var direction = new Vector3(
                    ArgumentParser.GetFloat(dirDict, "x", 0f),
                    ArgumentParser.GetFloat(dirDict, "y", 0f),
                    ArgumentParser.GetFloat(dirDict, "z", 0f)
                );

                if (direction.sqrMagnitude < 0.0001f)
                    return McpToolResult.Error("direction must be a non-zero vector");

                direction.Normalize();

                float maxDistance = ArgumentParser.GetFloat(args, "maxDistance", 1000f);
                int maxHits = ArgumentParser.GetInt(args, "maxHits", 10);

                // Parse layer mask
                int layerMask = -1; // All layers
                var layerNames = ArgumentParser.GetStringArray(args, "layerNames");
                if (layerNames.Length > 0)
                {
                    layerMask = LayerMask.GetMask(layerNames);
                }

                // Perform raycast
                var hits = Physics.RaycastAll(origin, direction, maxDistance, layerMask);

                // Sort by distance and limit
                var sortedHits = hits
                    .OrderBy(h => h.distance)
                    .Take(maxHits)
                    .Select(h => new Dictionary<string, object>
                    {
                        ["point"] = new { x = h.point.x, y = h.point.y, z = h.point.z },
                        ["normal"] = new { x = h.normal.x, y = h.normal.y, z = h.normal.z },
                        ["distance"] = h.distance,
                        ["colliderName"] = h.collider != null ? h.collider.name : "null",
                        ["gameObjectPath"] = h.collider != null ? GetGameObjectPath(h.collider.gameObject) : "null"
                    })
                    .ToList();

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["origin"] = new { x = origin.x, y = origin.y, z = origin.z },
                    ["direction"] = new { x = direction.x, y = direction.y, z = direction.z },
                    ["maxDistance"] = maxDistance,
                    ["hitCount"] = sortedHits.Count,
                    ["hits"] = sortedHits
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Raycast failed: {ex.Message}");
            }
        }

        private static McpToolResult SetupRigidbody(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            try
            {
                var rb = go.GetComponent<Rigidbody>();
                bool isNew = rb == null;

                if (isNew)
                {
                    rb = Undo.AddComponent<Rigidbody>(go);
                }
                else
                {
                    Undo.RecordObject(rb, "MCP Setup Rigidbody");
                }

                // SEC-#436: clamp physics parameters to physically meaningful ranges to avoid
                // engine instability (negative mass, negative drag).
                rb.mass = Mathf.Max(0.0001f, ArgumentParser.GetFloat(args, "mass", isNew ? 1f : rb.mass));
                rb.linearDamping = Mathf.Max(0f, ArgumentParser.GetFloat(args, "drag", isNew ? 0f : rb.linearDamping));
                rb.angularDamping = Mathf.Max(0f, ArgumentParser.GetFloat(args, "angularDrag", isNew ? 0.05f : rb.angularDamping));
                rb.useGravity = ArgumentParser.GetBool(args, "useGravity", isNew ? true : rb.useGravity);
                rb.isKinematic = ArgumentParser.GetBool(args, "isKinematic", isNew ? false : rb.isKinematic);

                // Parse interpolation
                if (ArgumentParser.HasKey(args, "interpolation"))
                {
                    rb.interpolation = ArgumentParser.GetEnum<RigidbodyInterpolation>(
                        args, "interpolation", RigidbodyInterpolation.None);
                }

                // Parse constraints
                var constraintStrings = ArgumentParser.GetStringArray(args, "constraints");
                if (constraintStrings.Length > 0)
                {
                    RigidbodyConstraints combined = RigidbodyConstraints.None;
                    foreach (var c in constraintStrings)
                    {
                        if (Enum.TryParse<RigidbodyConstraints>(c, ignoreCase: true, out var parsed))
                        {
                            combined |= parsed;
                        }
                        else
                        {
                            McpDebug.LogWarning($"[MCP Physics] Unknown constraint: {c}");
                        }
                    }
                    rb.constraints = combined;
                }

                EditorUtility.SetDirty(go);

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["isNew"] = isNew,
                    ["mass"] = rb.mass,
                    ["drag"] = rb.linearDamping,
                    ["angularDrag"] = rb.angularDamping,
                    ["useGravity"] = rb.useGravity,
                    ["isKinematic"] = rb.isKinematic,
                    ["interpolation"] = rb.interpolation.ToString(),
                    ["constraints"] = rb.constraints.ToString(),
                    ["message"] = $"{(isNew ? "Added" : "Updated")} Rigidbody on '{go.name}'"
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to setup Rigidbody: {ex.Message}");
            }
        }

        private static McpToolResult SetupCollider(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            try
            {
                string colliderType = ArgumentParser.GetString(args, "colliderType", "auto").ToLower();
                bool isTrigger = ArgumentParser.GetBool(args, "isTrigger", false);

                // Auto-detect collider type
                if (colliderType == "auto")
                {
                    colliderType = go.GetComponent<MeshFilter>() != null ? "mesh" : "box";
                }

                Collider collider = null;
                string addedType = "";

                switch (colliderType)
                {
                    case "box":
                        var box = Undo.AddComponent<BoxCollider>(go);
                        box.isTrigger = isTrigger;

                        if (args.TryGetValue("center", out var boxCenterObj) && boxCenterObj is Dictionary<string, object> boxCenterDict)
                        {
                            box.center = new Vector3(
                                ArgumentParser.GetFloat(boxCenterDict, "x", 0f),
                                ArgumentParser.GetFloat(boxCenterDict, "y", 0f),
                                ArgumentParser.GetFloat(boxCenterDict, "z", 0f));
                        }

                        if (args.TryGetValue("size", out var sizeObj) && sizeObj is Dictionary<string, object> sizeDict)
                        {
                            box.size = new Vector3(
                                ArgumentParser.GetFloat(sizeDict, "x", 1f),
                                ArgumentParser.GetFloat(sizeDict, "y", 1f),
                                ArgumentParser.GetFloat(sizeDict, "z", 1f));
                        }

                        collider = box;
                        addedType = "BoxCollider";
                        break;

                    case "sphere":
                        var sphere = Undo.AddComponent<SphereCollider>(go);
                        sphere.isTrigger = isTrigger;

                        if (args.TryGetValue("center", out var sphereCenterObj) && sphereCenterObj is Dictionary<string, object> sphereCenterDict)
                        {
                            sphere.center = new Vector3(
                                ArgumentParser.GetFloat(sphereCenterDict, "x", 0f),
                                ArgumentParser.GetFloat(sphereCenterDict, "y", 0f),
                                ArgumentParser.GetFloat(sphereCenterDict, "z", 0f));
                        }

                        sphere.radius = ArgumentParser.GetFloat(args, "radius", 0.5f);
                        collider = sphere;
                        addedType = "SphereCollider";
                        break;

                    case "capsule":
                        var capsule = Undo.AddComponent<CapsuleCollider>(go);
                        capsule.isTrigger = isTrigger;

                        if (args.TryGetValue("center", out var capsuleCenterObj) && capsuleCenterObj is Dictionary<string, object> capsuleCenterDict)
                        {
                            capsule.center = new Vector3(
                                ArgumentParser.GetFloat(capsuleCenterDict, "x", 0f),
                                ArgumentParser.GetFloat(capsuleCenterDict, "y", 0f),
                                ArgumentParser.GetFloat(capsuleCenterDict, "z", 0f));
                        }

                        capsule.radius = ArgumentParser.GetFloat(args, "radius", 0.5f);
                        capsule.height = ArgumentParser.GetFloat(args, "height", 2f);
                        capsule.direction = ArgumentParser.GetInt(args, "direction", 1); // Y-axis by default
                        collider = capsule;
                        addedType = "CapsuleCollider";
                        break;

                    case "mesh":
                        var meshFilter = go.GetComponent<MeshFilter>();
                        if (meshFilter == null || meshFilter.sharedMesh == null)
                            return McpToolResult.Error("MeshCollider requires a MeshFilter with a valid mesh on the GameObject");

                        var mesh = Undo.AddComponent<MeshCollider>(go);
                        mesh.isTrigger = isTrigger;
                        // MeshCollider must be convex if used as trigger
                        if (isTrigger)
                            mesh.convex = true;

                        collider = mesh;
                        addedType = "MeshCollider";
                        break;

                    default:
                        return McpToolResult.Error($"Unknown collider type: {colliderType}. Use Box, Sphere, Capsule, Mesh, or auto");
                }

                EditorUtility.SetDirty(go);

                var resultData = new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["colliderType"] = addedType,
                    ["isTrigger"] = isTrigger,
                    ["message"] = $"Added {addedType} to '{go.name}'"
                };

                // Add type-specific info
                if (collider is BoxCollider boxResult)
                {
                    resultData["center"] = new { x = boxResult.center.x, y = boxResult.center.y, z = boxResult.center.z };
                    resultData["size"] = new { x = boxResult.size.x, y = boxResult.size.y, z = boxResult.size.z };
                }
                else if (collider is SphereCollider sphereResult)
                {
                    resultData["center"] = new { x = sphereResult.center.x, y = sphereResult.center.y, z = sphereResult.center.z };
                    resultData["radius"] = sphereResult.radius;
                }
                else if (collider is CapsuleCollider capsuleResult)
                {
                    resultData["center"] = new { x = capsuleResult.center.x, y = capsuleResult.center.y, z = capsuleResult.center.z };
                    resultData["radius"] = capsuleResult.radius;
                    resultData["height"] = capsuleResult.height;
                    resultData["direction"] = capsuleResult.direction;
                }

                return McpResponse.Success(resultData);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to setup collider: {ex.Message}");
            }
        }

        private static McpToolResult SetPhysicsMaterial(Dictionary<string, object> args)
        {
            var (go, gameObjectPath, goErr) = RequireGameObject(args);
            if (goErr != null) return goErr;

            var collider = go.GetComponent<Collider>();
            if (collider == null)
                return McpToolResult.Error($"No Collider found on '{go.name}'. Add a collider first.");

            PhysicsMaterial mat = null;
            try
            {
                mat = new PhysicsMaterial();
                mat.dynamicFriction = ArgumentParser.GetFloat(args, "dynamicFriction", 0.6f);
                mat.staticFriction = ArgumentParser.GetFloat(args, "staticFriction", 0.6f);
                mat.bounciness = ArgumentParser.GetFloat(args, "bounciness", 0f);
                mat.frictionCombine = ArgumentParser.GetEnum<PhysicsMaterialCombine>(
                    args, "frictionCombine", PhysicsMaterialCombine.Average);
                mat.bounceCombine = ArgumentParser.GetEnum<PhysicsMaterialCombine>(
                    args, "bounceCombine", PhysicsMaterialCombine.Average);

                // Save as asset if path provided
                string savePath = ArgumentParser.GetString(args, "savePath", null);
                bool savedAsAsset = false;

                if (!string.IsNullOrEmpty(savePath))
                {
                    var (sanitizedPath, pathErr) = TrySanitizePath(savePath, "save path");
                    if (pathErr != null)
                    {
                        // FIX-#140: destroy the orphaned material before returning the error.
                        UnityEngine.Object.DestroyImmediate(mat);
                        return pathErr;
                    }
                    savePath = sanitizedPath;

                    // SEC-#434: centralized helper replaces the copy-pasted folder creation loop.
                    AssetDatabaseHelpers.EnsureFolderExists(System.IO.Path.GetDirectoryName(savePath));

                    AssetDatabase.CreateAsset(mat, savePath);
                    AssetDatabase.SaveAssets();
                    savedAsAsset = true;
                }

                Undo.RecordObject(collider, "MCP Set PhysicMaterial");
                collider.sharedMaterial = mat;
                EditorUtility.SetDirty(collider);
                // Ownership transferred to the collider/asset — null out so the catch handler
                // doesn't double-destroy on a later (unrelated) exception.
                mat = null;

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["colliderType"] = collider.GetType().Name,
                    ["dynamicFriction"] = mat.dynamicFriction,
                    ["staticFriction"] = mat.staticFriction,
                    ["bounciness"] = mat.bounciness,
                    ["frictionCombine"] = mat.frictionCombine.ToString(),
                    ["bounceCombine"] = mat.bounceCombine.ToString(),
                    ["savedAsAsset"] = savedAsAsset,
                    ["savePath"] = savePath ?? "(not saved)",
                    ["message"] = $"Applied PhysicMaterial to '{go.name}' ({collider.GetType().Name})"
                                  + (savedAsAsset ? $" and saved to {savePath}" : "")
                });
            }
            catch (Exception ex)
            {
                // FIX-#140: clean up the orphaned material if anything threw before ownership
                // transferred to the collider / asset database.
                if (mat != null)
                {
                    try { UnityEngine.Object.DestroyImmediate(mat); } catch { /* best-effort */ }
                }
                return McpToolResult.Error($"Failed to set PhysicMaterial: {ex.Message}");
            }
        }

        #endregion
    }
}
