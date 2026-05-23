using System;
using System.Collections.Generic;
using McpUnity.Editor;
using McpUnity.Helpers;
using McpUnity.Protocol;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Placement / level-design tools for MCP Unity Server.
    /// Contains 6 tools: raycast_place, align_to_surface, scatter_on_surface,
    /// snap_to_grid, replace_with_prefab, array_3d.
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterPlacementTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_raycast_place",
                description = "Cast a ray DOWN from a XZ position (skyHeight above) and snap a GameObject onto the first collider hit. Use to drop props onto terrain/floor.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject to move" },
                        ["x"] = new McpPropertySchema { type = "number", description = "World X to project from" },
                        ["z"] = new McpPropertySchema { type = "number", description = "World Z to project from" },
                        ["skyHeight"] = new McpPropertySchema { type = "number", description = "Y altitude to cast from (default 1000)" },
                        ["maxDistance"] = new McpPropertySchema { type = "number", description = "Max ray distance (default 2000)" },
                        ["alignToNormal"] = new McpPropertySchema { type = "boolean", description = "Also rotate so up matches surface normal (default false)" },
                        ["yOffset"] = new McpPropertySchema { type = "number", description = "Extra Y offset added after hit (default 0)" },
                        ["layerNames"] = new McpPropertySchema { type = "array", description = "Layer names to consider (default: all)" }
                    },
                    required = new List<string> { "gameObjectPath", "x", "z" }
                }
            }, RaycastPlace);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_align_to_surface",
                description = "Cast a ray DOWN from the GameObject's position and rotate it so its up axis matches the surface normal at the hit point. Position is preserved.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject to align" },
                        ["skyOffset"] = new McpPropertySchema { type = "number", description = "Cast from current.y + skyOffset (default 10)" },
                        ["maxDistance"] = new McpPropertySchema { type = "number", description = "Max ray distance (default 100)" },
                        ["preserveYaw"] = new McpPropertySchema { type = "boolean", description = "Keep current Y rotation (default true)" },
                        ["layerNames"] = new McpPropertySchema { type = "array", description = "Layer names to consider (default: all)" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, AlignToSurface);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_scatter_on_surface",
                description = "Instantiate N copies of a prefab scattered on collider surfaces inside a bounding box. Random rotation Y + uniform scale jitter. Drops via raycast-down.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["prefabPath"] = new McpPropertySchema { type = "string", description = "Asset path to the prefab (e.g. 'Assets/Prefabs/Tree.prefab')" },
                        ["centerX"] = new McpPropertySchema { type = "number", description = "Bounds center X" },
                        ["centerZ"] = new McpPropertySchema { type = "number", description = "Bounds center Z" },
                        ["sizeX"] = new McpPropertySchema { type = "number", description = "Bounds size X" },
                        ["sizeZ"] = new McpPropertySchema { type = "number", description = "Bounds size Z" },
                        ["count"] = new McpPropertySchema { type = "integer", description = "Number of instances to attempt (max 1000)" },
                        ["seed"] = new McpPropertySchema { type = "integer", description = "Random seed (default 0 = random)" },
                        ["skyHeight"] = new McpPropertySchema { type = "number", description = "Y altitude to cast from (default 1000)" },
                        ["maxDistance"] = new McpPropertySchema { type = "number", description = "Max ray distance (default 2000)" },
                        ["alignToNormal"] = new McpPropertySchema { type = "boolean", description = "Rotate each instance to surface normal (default false)" },
                        ["randomYaw"] = new McpPropertySchema { type = "boolean", description = "Apply random Y rotation 0-360 (default true)" },
                        ["minScale"] = new McpPropertySchema { type = "number", description = "Min uniform scale (default 1.0)" },
                        ["maxScale"] = new McpPropertySchema { type = "number", description = "Max uniform scale (default 1.0)" },
                        ["parentPath"] = new McpPropertySchema { type = "string", description = "Optional parent GameObject path (default: scene root)" },
                        ["layerNames"] = new McpPropertySchema { type = "array", description = "Layer names to consider (default: all)" }
                    },
                    required = new List<string> { "prefabPath", "centerX", "centerZ", "sizeX", "sizeZ", "count" }
                }
            }, ScatterOnSurface);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_snap_to_grid",
                description = "Snap one or more GameObjects' positions to a grid (each axis snapped to nearest multiple of resolution).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPaths"] = new McpPropertySchema { type = "array", description = "Paths to the GameObjects to snap" },
                        ["resolution"] = new McpPropertySchema { type = "number", description = "Grid cell size in world units (default 1.0)" },
                        ["snapX"] = new McpPropertySchema { type = "boolean", description = "Snap X axis (default true)" },
                        ["snapY"] = new McpPropertySchema { type = "boolean", description = "Snap Y axis (default false)" },
                        ["snapZ"] = new McpPropertySchema { type = "boolean", description = "Snap Z axis (default true)" }
                    },
                    required = new List<string> { "gameObjectPaths" }
                }
            }, SnapToGrid);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_replace_with_prefab",
                description = "Replace each listed GameObject with an instance of the given prefab, preserving world transform, parent, and sibling index. Originals are deleted.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPaths"] = new McpPropertySchema { type = "array", description = "Paths to the GameObjects to replace" },
                        ["prefabPath"] = new McpPropertySchema { type = "string", description = "Asset path to the replacement prefab" },
                        ["keepName"] = new McpPropertySchema { type = "boolean", description = "Keep the original name on the replacement (default false → prefab name)" }
                    },
                    required = new List<string> { "gameObjectPaths", "prefabPath" }
                }
            }, ReplaceWithPrefab);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_array_3d",
                description = "Duplicate a GameObject in an X×Y×Z grid. Source instance is kept at index (0,0,0). Total cells must be ≤ 4096.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the source GameObject" },
                        ["countX"] = new McpPropertySchema { type = "integer", description = "Number of copies along X (≥1)" },
                        ["countY"] = new McpPropertySchema { type = "integer", description = "Number of copies along Y (≥1, default 1)" },
                        ["countZ"] = new McpPropertySchema { type = "integer", description = "Number of copies along Z (≥1)" },
                        ["offsetX"] = new McpPropertySchema { type = "number", description = "World-space spacing X" },
                        ["offsetY"] = new McpPropertySchema { type = "number", description = "World-space spacing Y (default 0)" },
                        ["offsetZ"] = new McpPropertySchema { type = "number", description = "World-space spacing Z" },
                        ["parentPath"] = new McpPropertySchema { type = "string", description = "Optional parent for the new copies (default: same as source)" }
                    },
                    required = new List<string> { "gameObjectPath", "countX", "countZ", "offsetX", "offsetZ" }
                }
            }, Array3D);
        }

        #region Placement Handlers

        private static int BuildLayerMask(string[] layerNames)
        {
            if (layerNames == null || layerNames.Length == 0) return ~0;
            int mask = 0;
            foreach (var name in layerNames)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0) mask |= 1 << layer;
            }
            return mask == 0 ? ~0 : mask;
        }

        private static McpToolResult RaycastPlace(Dictionary<string, object> args)
        {
            try
            {
                var (go, goPath, goErr) = RequireGameObject(args);
                if (goErr != null) return goErr;

                float x = ArgumentParser.GetFloat(args, "x", 0f);
                float z = ArgumentParser.GetFloat(args, "z", 0f);
                float skyHeight = ArgumentParser.GetFloat(args, "skyHeight", 1000f);
                float maxDistance = ArgumentParser.GetFloat(args, "maxDistance", 2000f);
                bool alignToNormal = ArgumentParser.GetBool(args, "alignToNormal", false);
                float yOffset = ArgumentParser.GetFloat(args, "yOffset", 0f);
                int mask = BuildLayerMask(ArgumentParser.GetStringArray(args, "layerNames"));

                var origin = new Vector3(x, skyHeight, z);
                if (!Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    return McpToolResult.Error($"No collider hit when casting from ({x:F2}, {skyHeight:F2}, {z:F2}) downward (maxDistance {maxDistance}). Ensure target surface has a collider.");
                }

                Undo.RecordObject(go.transform, "MCP Raycast Place");
                go.transform.position = hit.point + new Vector3(0, yOffset, 0);
                if (alignToNormal)
                {
                    Vector3 yaw = go.transform.forward;
                    yaw = Vector3.ProjectOnPlane(yaw, hit.normal);
                    if (yaw.sqrMagnitude < 1e-6f) yaw = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                    go.transform.rotation = Quaternion.LookRotation(yaw.normalized, hit.normal);
                }
                EditorUtility.SetDirty(go);

                return McpResponse.Success("Placed via raycast", new Dictionary<string, object>
                {
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["position"] = new Dictionary<string, object> { ["x"] = hit.point.x, ["y"] = hit.point.y + yOffset, ["z"] = hit.point.z },
                    ["surface"] = hit.collider != null ? hit.collider.gameObject.name : null,
                    ["normal"] = new Dictionary<string, object> { ["x"] = hit.normal.x, ["y"] = hit.normal.y, ["z"] = hit.normal.z },
                    ["alignedToNormal"] = alignToNormal
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to raycast-place: {ex.Message}");
            }
        }

        private static McpToolResult AlignToSurface(Dictionary<string, object> args)
        {
            try
            {
                var (go, goPath, goErr) = RequireGameObject(args);
                if (goErr != null) return goErr;

                float skyOffset = ArgumentParser.GetFloat(args, "skyOffset", 10f);
                float maxDistance = ArgumentParser.GetFloat(args, "maxDistance", 100f);
                bool preserveYaw = ArgumentParser.GetBool(args, "preserveYaw", true);
                int mask = BuildLayerMask(ArgumentParser.GetStringArray(args, "layerNames"));

                var origin = go.transform.position + Vector3.up * skyOffset;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    return McpToolResult.Error($"No collider hit below '{goPath}' within {maxDistance}m.");
                }

                Undo.RecordObject(go.transform, "MCP Align To Surface");
                Vector3 forward = preserveYaw ? go.transform.forward : Vector3.forward;
                forward = Vector3.ProjectOnPlane(forward, hit.normal);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                go.transform.rotation = Quaternion.LookRotation(forward.normalized, hit.normal);
                EditorUtility.SetDirty(go);

                return McpResponse.Success("Aligned to surface normal", new Dictionary<string, object>
                {
                    ["gameObjectPath"] = GetGameObjectPath(go),
                    ["normal"] = new Dictionary<string, object> { ["x"] = hit.normal.x, ["y"] = hit.normal.y, ["z"] = hit.normal.z },
                    ["surface"] = hit.collider != null ? hit.collider.gameObject.name : null
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to align: {ex.Message}");
            }
        }

        private static McpToolResult ScatterOnSurface(Dictionary<string, object> args)
        {
            try
            {
                string prefabPath = ArgumentParser.RequireString(args, "prefabPath", out var prefabErr);
                if (prefabPath == null) return McpToolResult.Error(prefabErr);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) return McpToolResult.Error($"Prefab not found at '{prefabPath}'");

                float cx = ArgumentParser.GetFloat(args, "centerX", 0f);
                float cz = ArgumentParser.GetFloat(args, "centerZ", 0f);
                float sx = Mathf.Max(0.01f, ArgumentParser.GetFloat(args, "sizeX", 10f));
                float sz = Mathf.Max(0.01f, ArgumentParser.GetFloat(args, "sizeZ", 10f));
                int count = Mathf.Clamp(ArgumentParser.GetInt(args, "count", 0), 1, 1000);
                int seed = ArgumentParser.GetInt(args, "seed", 0);
                float skyHeight = ArgumentParser.GetFloat(args, "skyHeight", 1000f);
                float maxDistance = ArgumentParser.GetFloat(args, "maxDistance", 2000f);
                bool alignToNormal = ArgumentParser.GetBool(args, "alignToNormal", false);
                bool randomYaw = ArgumentParser.GetBool(args, "randomYaw", true);
                float minScale = Mathf.Max(0.0001f, ArgumentParser.GetFloat(args, "minScale", 1f));
                float maxScale = Mathf.Max(minScale, ArgumentParser.GetFloat(args, "maxScale", 1f));
                string parentPath = ArgumentParser.GetString(args, "parentPath", null);
                int mask = BuildLayerMask(ArgumentParser.GetStringArray(args, "layerNames"));

                Transform parent = null;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentGo = GameObjectHelpers.FindGameObject(parentPath);
                    if (parentGo == null) return McpToolResult.Error($"Parent GameObject not found: {parentPath}");
                    parent = parentGo.transform;
                }

                var rng = seed == 0 ? new System.Random() : new System.Random(seed);
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"MCP Scatter ({count} × {prefab.name})");

                int placed = 0, missed = 0;
                var placedNames = new List<string>();
                float xMin = cx - sx * 0.5f, xMax = cx + sx * 0.5f;
                float zMin = cz - sz * 0.5f, zMax = cz + sz * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    float rx = (float)(rng.NextDouble() * (xMax - xMin) + xMin);
                    float rz = (float)(rng.NextDouble() * (zMax - zMin) + zMin);
                    var origin = new Vector3(rx, skyHeight, rz);
                    if (!Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
                    {
                        missed++;
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    if (instance == null) { missed++; continue; }

                    instance.transform.position = hit.point;
                    if (alignToNormal)
                    {
                        Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.right, hit.normal);
                        instance.transform.rotation = Quaternion.LookRotation(fwd.normalized, hit.normal);
                    }
                    if (randomYaw)
                    {
                        float yaw = (float)(rng.NextDouble() * 360.0);
                        instance.transform.Rotate(0f, yaw, 0f, Space.World);
                    }
                    if (maxScale > minScale || !Mathf.Approximately(minScale, 1f))
                    {
                        float s = (float)(rng.NextDouble() * (maxScale - minScale) + minScale);
                        instance.transform.localScale *= s;
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "MCP Scatter Instance");
                    placedNames.Add(GetGameObjectPath(instance));
                    placed++;
                }

                Undo.CollapseUndoOperations(undoGroup);

                return McpResponse.Success($"Scattered {placed} / {count} (missed {missed})", new Dictionary<string, object>
                {
                    ["placed"] = placed,
                    ["missed"] = missed,
                    ["prefab"] = prefab.name,
                    ["hint"] = missed > 0 ? "Misses usually mean no collider under that XZ. Ensure your terrain/floor has a collider." : null
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to scatter: {ex.Message}");
            }
        }

        private static McpToolResult SnapToGrid(Dictionary<string, object> args)
        {
            try
            {
                var paths = ArgumentParser.GetStringArray(args, "gameObjectPaths");
                if (paths == null || paths.Length == 0) return McpToolResult.Error("gameObjectPaths is required (non-empty array)");

                float res = Mathf.Max(0.0001f, ArgumentParser.GetFloat(args, "resolution", 1f));
                bool sx = ArgumentParser.GetBool(args, "snapX", true);
                bool sy = ArgumentParser.GetBool(args, "snapY", false);
                bool sz = ArgumentParser.GetBool(args, "snapZ", true);

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"MCP Snap To Grid ({paths.Length})");

                int snapped = 0;
                var notFound = new List<string>();
                foreach (var path in paths)
                {
                    var go = GameObjectHelpers.FindGameObject(path);
                    if (go == null) { notFound.Add(path); continue; }

                    Undo.RecordObject(go.transform, "MCP Snap");
                    Vector3 p = go.transform.position;
                    if (sx) p.x = Mathf.Round(p.x / res) * res;
                    if (sy) p.y = Mathf.Round(p.y / res) * res;
                    if (sz) p.z = Mathf.Round(p.z / res) * res;
                    go.transform.position = p;
                    EditorUtility.SetDirty(go);
                    snapped++;
                }

                Undo.CollapseUndoOperations(undoGroup);

                return McpResponse.Success($"Snapped {snapped}/{paths.Length} to grid {res:F3}", new Dictionary<string, object>
                {
                    ["snapped"] = snapped,
                    ["resolution"] = res,
                    ["notFound"] = notFound
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to snap: {ex.Message}");
            }
        }

        private static McpToolResult ReplaceWithPrefab(Dictionary<string, object> args)
        {
            try
            {
                var paths = ArgumentParser.GetStringArray(args, "gameObjectPaths");
                if (paths == null || paths.Length == 0) return McpToolResult.Error("gameObjectPaths is required (non-empty array)");

                string prefabPath = ArgumentParser.RequireString(args, "prefabPath", out var prefabErr);
                if (prefabPath == null) return McpToolResult.Error(prefabErr);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) return McpToolResult.Error($"Prefab not found at '{prefabPath}'");

                bool keepName = ArgumentParser.GetBool(args, "keepName", false);

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"MCP Replace With Prefab ({paths.Length})");

                int replaced = 0;
                var notFound = new List<string>();
                var newPaths = new List<string>();

                foreach (var path in paths)
                {
                    var old = GameObjectHelpers.FindGameObject(path);
                    if (old == null) { notFound.Add(path); continue; }

                    var t = old.transform;
                    Vector3 worldPos = t.position;
                    Quaternion worldRot = t.rotation;
                    Vector3 lossy = t.lossyScale;
                    Transform parent = t.parent;
                    int siblingIndex = t.GetSiblingIndex();
                    string originalName = old.name;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    if (instance == null) { notFound.Add(path); continue; }

                    instance.transform.SetPositionAndRotation(worldPos, worldRot);
                    // Best-effort lossy-scale match (works perfectly when parent has uniform scale).
                    if (parent != null)
                    {
                        var ps = parent.lossyScale;
                        instance.transform.localScale = new Vector3(
                            ps.x != 0 ? lossy.x / ps.x : lossy.x,
                            ps.y != 0 ? lossy.y / ps.y : lossy.y,
                            ps.z != 0 ? lossy.z / ps.z : lossy.z);
                    }
                    else
                    {
                        instance.transform.localScale = lossy;
                    }
                    instance.transform.SetSiblingIndex(siblingIndex);
                    if (keepName) instance.name = originalName;

                    Undo.RegisterCreatedObjectUndo(instance, "MCP Replace Instance");
                    Undo.DestroyObjectImmediate(old);
                    newPaths.Add(GetGameObjectPath(instance));
                    replaced++;
                }

                Undo.CollapseUndoOperations(undoGroup);

                return McpResponse.Success($"Replaced {replaced}/{paths.Length} with '{prefab.name}'", new Dictionary<string, object>
                {
                    ["replaced"] = replaced,
                    ["prefab"] = prefab.name,
                    ["newPaths"] = newPaths,
                    ["notFound"] = notFound
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to replace: {ex.Message}");
            }
        }

        private static McpToolResult Array3D(Dictionary<string, object> args)
        {
            try
            {
                var (src, srcPath, goErr) = RequireGameObject(args);
                if (goErr != null) return goErr;

                int cx = Mathf.Max(1, ArgumentParser.GetInt(args, "countX", 1));
                int cy = Mathf.Max(1, ArgumentParser.GetInt(args, "countY", 1));
                int cz = Mathf.Max(1, ArgumentParser.GetInt(args, "countZ", 1));
                int total = cx * cy * cz;
                if (total > 4096) return McpToolResult.Error($"Total cells {total} exceeds 4096. Reduce countX/countY/countZ.");

                float ox = ArgumentParser.GetFloat(args, "offsetX", 0f);
                float oy = ArgumentParser.GetFloat(args, "offsetY", 0f);
                float oz = ArgumentParser.GetFloat(args, "offsetZ", 0f);
                string parentPath = ArgumentParser.GetString(args, "parentPath", null);

                Transform parent = src.transform.parent;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var pGo = GameObjectHelpers.FindGameObject(parentPath);
                    if (pGo == null) return McpToolResult.Error($"Parent not found: {parentPath}");
                    parent = pGo.transform;
                }

                Vector3 basePos = src.transform.position;
                bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(src);

                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"MCP Array 3D ({cx}×{cy}×{cz})");

                int created = 0;
                for (int ix = 0; ix < cx; ix++)
                for (int iy = 0; iy < cy; iy++)
                for (int iz = 0; iz < cz; iz++)
                {
                    if (ix == 0 && iy == 0 && iz == 0) continue; // skip source

                    GameObject copy;
                    if (isPrefabInstance)
                    {
                        var root = PrefabUtility.GetCorrespondingObjectFromOriginalSource(src);
                        copy = (GameObject)PrefabUtility.InstantiatePrefab(root, parent);
                    }
                    else
                    {
                        copy = UnityEngine.Object.Instantiate(src, parent);
                    }
                    if (copy == null) continue;

                    copy.transform.position = basePos + new Vector3(ix * ox, iy * oy, iz * oz);
                    copy.transform.rotation = src.transform.rotation;
                    copy.transform.localScale = src.transform.localScale;
                    copy.name = $"{src.name}_{ix}_{iy}_{iz}";
                    Undo.RegisterCreatedObjectUndo(copy, "MCP Array 3D Instance");
                    created++;
                }

                Undo.CollapseUndoOperations(undoGroup);

                return McpResponse.Success($"Created {created} copies in {cx}×{cy}×{cz} grid", new Dictionary<string, object>
                {
                    ["created"] = created,
                    ["grid"] = new Dictionary<string, object> { ["x"] = cx, ["y"] = cy, ["z"] = cz },
                    ["offset"] = new Dictionary<string, object> { ["x"] = ox, ["y"] = oy, ["z"] = oz },
                    ["source"] = GetGameObjectPath(src)
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to array: {ex.Message}");
            }
        }

        #endregion
    }
}
