using System;
using System.Collections.Generic;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Terrain Paint Tools - Add layers, paint textures, paint paths, and place trees on terrains.
    /// Contains 4 tools: add_terrain_layer, paint_terrain_texture_batch, paint_terrain_path, add_terrain_trees
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterTerrainPaintTools()
        {
            // ================================================================
            // unity_add_terrain_layer
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_terrain_layer",
                description = "Add a texture layer to a terrain. Provide a diffuse texture path to create a new layer, or a terrainLayerPath to use an existing TerrainLayer asset.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Terrain path (default: first active)"
                        },
                        ["diffuseTexturePath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Asset path to diffuse texture (e.g. 'Assets/Textures/Grass.png')"
                        },
                        ["normalMapPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Normal map asset path"
                        },
                        ["terrainLayerPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Existing TerrainLayer asset path (overrides diffuseTexturePath)"
                        },
                        ["tileSize"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Texture tile size {x, y}"
                        },
                        ["tileOffset"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Texture tile offset {x, y}"
                        },
                        ["metallic"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Metallic value 0-1"
                        },
                        ["smoothness"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Smoothness value 0-1"
                        }
                    },
                    required = new List<string>()
                }
            }, AddTerrainLayer);

            // ================================================================
            // unity_paint_terrain_texture_batch
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_paint_terrain_texture_batch",
                description = "Paint multiple texture strokes in a single call. Each stroke: {layerIndex, brushCenter {x,z}, brushSize, opacity, brushShape, brushFalloff}. All strokes applied in one alphamap round-trip. For paths/roads, prefer unity_paint_terrain_path (waypoint-based, much simpler). Use this for complex patterns or scattered spots.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Terrain path (default: first active)"
                        },
                        ["strokes"] = new McpPropertySchema
                        {
                            type = "array",
                            description = "Array of strokes (see docs)"
                        }
                    },
                    required = new List<string> { "strokes" }
                }
            }, PaintTerrainTextureBatch);

            // ================================================================
            // unity_paint_terrain_path
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_paint_terrain_path",
                description = "Paint a terrain texture along a path defined by waypoints. Automatically interpolates between waypoints to create continuous paths, roads, or rivers. Much more efficient than manual paint_terrain_texture_batch strokes — provide 3-10 waypoints instead of 50+ strokes.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Terrain path (default: first active)"
                        },
                        ["layerIndex"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Terrain layer index (0-based)"
                        },
                        ["waypoints"] = new McpPropertySchema
                        {
                            type = "array",
                            description = "Array of {x, z} world-space positions (min 2)"
                        },
                        ["width"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Path width in world units"
                        },
                        ["falloff"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Edge falloff 0-1 (0 = hard, 1 = soft)"
                        },
                        ["opacity"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Paint opacity 0-1"
                        },
                        ["spacing"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Stroke spacing in world units (smaller = smoother)"
                        }
                    },
                    required = new List<string> { "layerIndex", "waypoints" }
                }
            }, PaintTerrainPath);

            // ================================================================
            // unity_add_terrain_trees
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_terrain_trees",
                description = "Place trees on a terrain. Provide explicit positions or a count for random scatter placement.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to terrain GameObject. If omitted, uses the first active Terrain."
                        },
                        ["prefabPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Asset path to the tree prefab (e.g. 'Assets/Prefabs/Tree_Pine.prefab')"
                        },
                        ["positions"] = new McpPropertySchema
                        {
                            type = "array",
                            description = "Array of {x, z} positions normalized 0-1 for explicit placement"
                        },
                        ["count"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Number of trees to scatter randomly (used if positions not provided)"
                        },
                        ["minScale"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Minimum tree scale"
                        },
                        ["maxScale"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Maximum tree scale"
                        },
                        ["seed"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Random seed for scatter placement"
                        },
                        ["clearExisting"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Clear all existing trees before placing new ones"
                        }
                    },
                    required = new List<string> { "prefabPath" }
                }
            }, AddTerrainTrees);
        }

        // ====================================================================
        // Handler: unity_add_terrain_layer
        // ====================================================================
        private static McpToolResult AddTerrainLayer(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            string layerPath = ArgumentParser.GetString(args, "terrainLayerPath", null);
            TerrainLayer newLayer;

            if (!string.IsNullOrEmpty(layerPath))
            {
                // Load existing TerrainLayer asset
                newLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (newLayer == null)
                    return McpToolResult.Error($"TerrainLayer not found at path: {layerPath}");
            }
            else
            {
                // Create new TerrainLayer from texture
                string diffusePath = ArgumentParser.GetString(args, "diffuseTexturePath", null);
                if (string.IsNullOrEmpty(diffusePath))
                    return McpToolResult.Error("Provide either 'diffuseTexturePath' to create a new layer or 'terrainLayerPath' to use an existing one.");

                var diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
                if (diffuseTex == null)
                    return McpToolResult.Error($"Texture not found at path: {diffusePath}");

                newLayer = new TerrainLayer();
                newLayer.diffuseTexture = diffuseTex;

                // Optional normal map
                string normalPath = ArgumentParser.GetString(args, "normalMapPath", null);
                if (!string.IsNullOrEmpty(normalPath))
                {
                    var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    if (normalTex != null)
                        newLayer.normalMapTexture = normalTex;
                }

                // Tile size
                if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "tileSize", out var tsDict))
                {
                    float tsx = ArgumentParser.GetFloat(tsDict, "x", 15f);
                    float tsy = ArgumentParser.GetFloat(tsDict, "y", 15f);
                    newLayer.tileSize = new Vector2(tsx, tsy);
                }
                else
                {
                    newLayer.tileSize = new Vector2(15f, 15f);
                }

                // Tile offset
                if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "tileOffset", out var toDict))
                {
                    float tox = ArgumentParser.GetFloat(toDict, "x", 0f);
                    float toy = ArgumentParser.GetFloat(toDict, "y", 0f);
                    newLayer.tileOffset = new Vector2(tox, toy);
                }

                newLayer.metallic = ArgumentParser.GetFloat(args, "metallic", 0f);
                newLayer.smoothness = ArgumentParser.GetFloat(args, "smoothness", 0.5f);

                // Save as asset
                string layerDir = "Assets/Terrain/Layers";
                if (!AssetDatabase.IsValidFolder("Assets/Terrain"))
                    AssetDatabase.CreateFolder("Assets", "Terrain");
                if (!AssetDatabase.IsValidFolder(layerDir))
                    AssetDatabase.CreateFolder("Assets/Terrain", "Layers");

                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{layerDir}/{diffuseTex.name}_Layer.asset");
                AssetDatabase.CreateAsset(newLayer, assetPath);
            }

            // Add layer to terrain
            Undo.RecordObject(data, "Add Terrain Layer");
            var existingLayers = data.terrainLayers;
            var newLayers = new TerrainLayer[existingLayers.Length + 1];
            Array.Copy(existingLayers, newLayers, existingLayers.Length);
            newLayers[existingLayers.Length] = newLayer;
            data.terrainLayers = newLayers;

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            int newIndex = newLayers.Length - 1;
            return McpResponse.Success($"Added terrain layer '{newLayer.name}' at index {newIndex}",
                TerrainHelpers.SerializeTerrainLayer(newLayer, newIndex));
        }

        // ====================================================================
        // Handler: unity_paint_terrain_texture
        // ====================================================================
        private static McpToolResult PaintTerrainTexture(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            int   layerIndex  = ArgumentParser.GetInt(args, "layerIndex", 0);
            float opacity     = Mathf.Clamp01(ArgumentParser.GetFloat(args, "opacity", 1f));
            int   numLayers   = data.alphamapLayers;

            if (layerIndex < 0 || layerIndex >= numLayers)
                return McpToolResult.Error($"layerIndex {layerIndex} out of range. Terrain has {numLayers} layers (0-{numLayers - 1}).");

            // Brush parameters
            string brushShape   = ArgumentParser.GetString(args, "brushShape", "rect");
            float  brushFalloff = Mathf.Clamp01(ArgumentParser.GetFloat(args, "brushFalloff", 0.5f));
            float  brushRotRad  = ArgumentParser.GetFloat(args, "brushRotation", 0f) * Mathf.Deg2Rad;
            bool   useBrush     = BrushHelper.IsBrushActive(args);

            // Texture brush (PNG mask)
            string brushName         = ArgumentParser.GetString(args, "brushName", "");
            string resolvedBrushName = null;
            if (!string.IsNullOrEmpty(brushName))
            {
                resolvedBrushName = BrushHelper.TryLoadTextureBrush(brushName);
                if (resolvedBrushName == null)
                    return McpToolResult.Error($"Brush texture '{brushName}' not found. Use unity_list_terrain_brushes to see available brushes.");
                useBrush = true;
            }

            int amRes = data.alphamapResolution;

            // Get bounding rect + brush center in alphamap pixel space
            BrushHelper.GetBrushPixelRect(args, data, amRes,
                out int xBase, out int yBase, out int width, out int height,
                out float centerPixX, out float centerPixY, out float radiusPix);

            Undo.RecordObject(data, "Paint Terrain Texture");

            float[,,] alphamaps = data.GetAlphamaps(xBase, yBase, width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float brushWeight = useBrush
                        ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                            radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                        : 1f;

                    float effectiveOpacity = brushWeight * opacity;
                    if (effectiveOpacity <= 0f) continue;

                    // Blend toward target layer at effectiveOpacity
                    for (int l = 0; l < numLayers; l++)
                    {
                        float target = (l == layerIndex) ? 1f : 0f;
                        alphamaps[y, x, l] = Mathf.Lerp(alphamaps[y, x, l], target, effectiveOpacity);
                    }

                    // Renormalize weights to sum to 1.0
                    float sum = 0f;
                    for (int l = 0; l < numLayers; l++)
                        sum += alphamaps[y, x, l];

                    if (sum > 0f)
                    {
                        float invSum = 1f / sum;
                        for (int l = 0; l < numLayers; l++)
                            alphamaps[y, x, l] *= invSum;
                    }
                }
            }

            data.SetAlphamaps(xBase, yBase, alphamaps);
            EditorUtility.SetDirty(data);

            var resultData = new Dictionary<string, object>
            {
                ["layerIndex"]    = layerIndex,
                ["opacity"]       = opacity,
                ["brushShape"]    = brushShape,
                ["regionPixels"]  = new Dictionary<string, object>
                {
                    ["x"] = xBase, ["y"] = yBase, ["width"] = width, ["height"] = height
                },
                ["alphamapResolution"] = amRes
            };
            if (resolvedBrushName != null) resultData["brushName"] = resolvedBrushName;

            return McpResponse.Success($"Painted layer {layerIndex} on terrain '{terrain.name}' (brush: {brushShape}, opacity: {opacity})",
                resultData);
        }

        // ====================================================================
        // Handler: unity_paint_terrain_path
        // ====================================================================
        private static McpToolResult PaintTerrainPath(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            int layerIndex = ArgumentParser.GetInt(args, "layerIndex", 0);
            int numLayers = data.alphamapLayers;
            if (layerIndex < 0 || layerIndex >= numLayers)
                return McpToolResult.Error($"layerIndex {layerIndex} out of range. Terrain has {numLayers} layers (0-{numLayers - 1}).");

            // Parse waypoints
            if (!args.TryGetValue("waypoints", out var wpObj) || !(wpObj is List<object> wpRaw) || wpRaw.Count < 2)
                return McpToolResult.Error("'waypoints' array is required and must have at least 2 points. Each point: {x, z} in world space.");

            float pathWidth = ArgumentParser.GetFloat(args, "width", 4f);
            float falloff   = Mathf.Clamp01(ArgumentParser.GetFloat(args, "falloff", 0.3f));
            float opacity   = Mathf.Clamp01(ArgumentParser.GetFloat(args, "opacity", 1f));
            float spacing   = ArgumentParser.GetFloat(args, "spacing", pathWidth * 0.5f);
            if (spacing <= 0.01f) spacing = 0.5f;

            // Parse waypoint world positions
            var waypoints = new List<Vector2>();
            for (int i = 0; i < wpRaw.Count; i++)
            {
                if (!(wpRaw[i] is Dictionary<string, object> wpDict))
                    return McpToolResult.Error($"waypoints[{i}] must be an object with {{x, z}}.");

                float wx = ArgumentParser.GetFloat(wpDict, "x", 0f);
                float wz = ArgumentParser.GetFloat(wpDict, "z", 0f);
                waypoints.Add(new Vector2(wx, wz));
            }

            // Convert world positions to terrain-local normalized coords (0-1)
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = data.size;
            int amRes = data.alphamapResolution;

            // Interpolate waypoints to generate sample points along the path
            var pathPoints = new List<Vector2>();
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector2 a = waypoints[i];
                Vector2 b = waypoints[i + 1];
                float segLength = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLength / spacing));

                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    pathPoints.Add(Vector2.Lerp(a, b, t));
                }
            }
            pathPoints.Add(waypoints[waypoints.Count - 1]); // add last point

            // Compute bounding box in alphamap pixel space
            float halfWidth = pathWidth * 0.5f;
            int unionX1 = amRes, unionY1 = amRes, unionX2 = 0, unionY2 = 0;

            foreach (var pt in pathPoints)
            {
                // World to terrain-normalized
                float nx = (pt.x - terrainPos.x) / terrainSize.x;
                float nz = (pt.y - terrainPos.z) / terrainSize.z;

                // Half-width in normalized coords
                float hwNormX = halfWidth / terrainSize.x;
                float hwNormZ = halfWidth / terrainSize.z;

                int pxMinX = Mathf.FloorToInt((nx - hwNormX) * amRes);
                int pxMinY = Mathf.FloorToInt((nz - hwNormZ) * amRes);
                int pxMaxX = Mathf.CeilToInt((nx + hwNormX) * amRes);
                int pxMaxY = Mathf.CeilToInt((nz + hwNormZ) * amRes);

                unionX1 = Math.Min(unionX1, pxMinX);
                unionY1 = Math.Min(unionY1, pxMinY);
                unionX2 = Math.Max(unionX2, pxMaxX);
                unionY2 = Math.Max(unionY2, pxMaxY);
            }

            // Clamp to alphamap bounds
            unionX1 = Mathf.Clamp(unionX1, 0, amRes - 1);
            unionY1 = Mathf.Clamp(unionY1, 0, amRes - 1);
            unionX2 = Mathf.Clamp(unionX2, 1, amRes);
            unionY2 = Mathf.Clamp(unionY2, 1, amRes);

            int totalW = unionX2 - unionX1;
            int totalH = unionY2 - unionY1;
            if (totalW <= 0 || totalH <= 0)
                return McpToolResult.Error("Path is entirely outside the terrain bounds.");

            // Load alphamaps
            Undo.RecordObject(data, "Paint Terrain Path");
            float[,,] alphamaps = data.GetAlphamaps(unionX1, unionY1, totalW, totalH);

            // For each pixel in the bounding box, compute distance to nearest path segment
            for (int py = 0; py < totalH; py++)
            {
                for (int px = 0; px < totalW; px++)
                {
                    // Pixel center in world space
                    float worldX = terrainPos.x + ((unionX1 + px + 0.5f) / amRes) * terrainSize.x;
                    float worldZ = terrainPos.z + ((unionY1 + py + 0.5f) / amRes) * terrainSize.z;

                    // Find minimum distance to any path segment
                    float minDist = float.MaxValue;
                    for (int i = 0; i < pathPoints.Count - 1; i++)
                    {
                        float d = DistancePointToSegment(worldX, worldZ,
                            pathPoints[i].x, pathPoints[i].y,
                            pathPoints[i + 1].x, pathPoints[i + 1].y);
                        if (d < minDist) minDist = d;
                    }

                    if (minDist > halfWidth) continue;

                    // Compute brush weight based on distance and falloff
                    float weight;
                    float innerRadius = halfWidth * (1f - falloff);
                    if (minDist <= innerRadius)
                    {
                        weight = 1f;
                    }
                    else
                    {
                        // Smooth falloff from inner to outer edge
                        float t = (minDist - innerRadius) / (halfWidth - innerRadius);
                        weight = 1f - t * t; // quadratic falloff
                    }

                    float eff = weight * opacity;
                    if (eff <= 0f) continue;

                    // Blend toward target layer
                    for (int l = 0; l < numLayers; l++)
                    {
                        float target = (l == layerIndex) ? 1f : 0f;
                        alphamaps[py, px, l] = Mathf.Lerp(alphamaps[py, px, l], target, eff);
                    }

                    // Renormalize
                    float sum = 0f;
                    for (int l = 0; l < numLayers; l++) sum += alphamaps[py, px, l];
                    if (sum > 0f)
                    {
                        float inv = 1f / sum;
                        for (int l = 0; l < numLayers; l++) alphamaps[py, px, l] *= inv;
                    }
                }
            }

            // Write back
            data.SetAlphamaps(unionX1, unionY1, alphamaps);
            EditorUtility.SetDirty(data);

            return McpResponse.Success(
                $"Painted path on terrain '{terrain.name}' (layer {layerIndex}, {waypoints.Count} waypoints, {pathPoints.Count} samples, width {pathWidth})",
                new Dictionary<string, object>
                {
                    ["layerIndex"] = layerIndex,
                    ["waypointCount"] = waypoints.Count,
                    ["sampleCount"] = pathPoints.Count,
                    ["width"] = pathWidth,
                    ["falloff"] = falloff,
                    ["opacity"] = opacity,
                    ["regionPixels"] = new Dictionary<string, object>
                    {
                        ["x"] = unionX1, ["y"] = unionY1,
                        ["width"] = totalW, ["height"] = totalH
                    }
                });
        }

        /// <summary>
        /// Compute the minimum distance from point (px, pz) to the line segment (ax, az)-(bx, bz).
        /// </summary>
        private static float DistancePointToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            float lenSq = dx * dx + dz * dz;

            if (lenSq < 1e-8f)
                return Mathf.Sqrt((px - ax) * (px - ax) + (pz - az) * (pz - az));

            float t = Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lenSq);
            float cx = ax + t * dx;
            float cz = az + t * dz;
            return Mathf.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
        }

        // ====================================================================
        // Batch stroke data (used by PaintTerrainTextureBatch)
        // ====================================================================
        private struct ParsedStroke
        {
            public int    layerIndex;
            public float  opacity;
            public string brushShape;
            public float  brushFalloff;
            public float  brushRotRad;
            public string resolvedBrushName;
            public bool   useBrush;
            public int    xBase, yBase, xEnd, yEnd;   // absolute alphamap pixel coords
            public float  centerPixX, centerPixY, radiusPix;
        }

        // ====================================================================
        // Handler: unity_paint_terrain_texture_batch
        // ====================================================================
        private static McpToolResult PaintTerrainTextureBatch(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            // Parse strokes array
            if (!args.TryGetValue("strokes", out var strokesObj) || !(strokesObj is List<object> strokesRaw) || strokesRaw.Count == 0)
                return McpToolResult.Error("'strokes' array is required and must not be empty.");

            int numLayers = data.alphamapLayers;
            int amRes     = data.alphamapResolution;

            // ---- Phase 1: Parse all strokes, resolve brushes, compute union bbox ----
            var parsed = new List<ParsedStroke>(strokesRaw.Count);
            int unionX1 = amRes, unionY1 = amRes, unionX2 = 0, unionY2 = 0;

            for (int i = 0; i < strokesRaw.Count; i++)
            {
                if (!(strokesRaw[i] is Dictionary<string, object> sd))
                    return McpToolResult.Error($"strokes[{i}] must be an object.");

                int layerIndex = ArgumentParser.GetInt(sd, "layerIndex", 0);
                if (layerIndex < 0 || layerIndex >= numLayers)
                    return McpToolResult.Error($"strokes[{i}].layerIndex {layerIndex} out of range (terrain has {numLayers} layers, 0-{numLayers - 1}).");

                float  opacity  = Mathf.Clamp01(ArgumentParser.GetFloat(sd, "opacity", 1f));
                string shape    = ArgumentParser.GetString(sd, "brushShape", "rect");
                float  falloff  = Mathf.Clamp01(ArgumentParser.GetFloat(sd, "brushFalloff", 0.5f));
                float  rotRad   = ArgumentParser.GetFloat(sd, "brushRotation", 0f) * Mathf.Deg2Rad;

                // Resolve texture brush (pre-load into BrushHelper cache)
                string brushName         = ArgumentParser.GetString(sd, "brushName", "");
                string resolvedBrushName = null;
                if (!string.IsNullOrEmpty(brushName))
                {
                    resolvedBrushName = BrushHelper.TryLoadTextureBrush(brushName);
                    if (resolvedBrushName == null)
                        return McpToolResult.Error($"strokes[{i}]: brush texture '{brushName}' not found. Use unity_list_terrain_brushes.");
                }

                bool useBrush = !string.IsNullOrEmpty(resolvedBrushName) || BrushHelper.IsBrushActive(sd);

                BrushHelper.GetBrushPixelRect(sd, data, amRes,
                    out int xBase, out int yBase, out int w, out int h,
                    out float cPixX, out float cPixY, out float radPix);

                int xEnd = xBase + w;
                int yEnd = yBase + h;

                unionX1 = Math.Min(unionX1, xBase);
                unionY1 = Math.Min(unionY1, yBase);
                unionX2 = Math.Max(unionX2, xEnd);
                unionY2 = Math.Max(unionY2, yEnd);

                parsed.Add(new ParsedStroke
                {
                    layerIndex        = layerIndex,
                    opacity           = opacity,
                    brushShape        = shape,
                    brushFalloff      = falloff,
                    brushRotRad       = rotRad,
                    resolvedBrushName = resolvedBrushName,
                    useBrush          = useBrush,
                    xBase = xBase, yBase = yBase, xEnd = xEnd, yEnd = yEnd,
                    centerPixX = cPixX, centerPixY = cPixY, radiusPix = radPix
                });
            }

            // Clamp union bbox to alphamap resolution
            unionX1 = Mathf.Clamp(unionX1, 0, amRes - 1);
            unionY1 = Mathf.Clamp(unionY1, 0, amRes - 1);
            unionX2 = Mathf.Clamp(unionX2, 0, amRes);
            unionY2 = Mathf.Clamp(unionY2, 0, amRes);

            int totalW = unionX2 - unionX1;
            int totalH = unionY2 - unionY1;
            if (totalW <= 0 || totalH <= 0)
                return McpToolResult.Error("All strokes have zero pixel area after clamping.");

            // ---- Phase 2: Load alphamaps once (union bbox) ----
            Undo.RecordObject(data, "Paint Terrain Texture Batch");
            float[,,] alphamaps = data.GetAlphamaps(unionX1, unionY1, totalW, totalH);

            // ---- Phase 3: Apply each stroke to the in-memory array ----
            foreach (var s in parsed)
            {
                // Intersection of stroke bbox with union bbox (should always be the full stroke)
                int sx1 = Math.Max(s.xBase, unionX1);
                int sy1 = Math.Max(s.yBase, unionY1);
                int sx2 = Math.Min(s.xEnd,  unionX2);
                int sy2 = Math.Min(s.yEnd,  unionY2);

                for (int absY = sy1; absY < sy2; absY++)
                {
                    int localY = absY - unionY1;
                    for (int absX = sx1; absX < sx2; absX++)
                    {
                        int localX = absX - unionX1;

                        float brushWeight = s.useBrush
                            ? BrushHelper.GetBrushWeight(
                                absX, absY,
                                s.centerPixX, s.centerPixY,
                                s.radiusPix, s.radiusPix,
                                s.brushRotRad, s.brushShape,
                                s.brushFalloff, s.resolvedBrushName)
                            : 1f;

                        float eff = brushWeight * s.opacity;
                        if (eff <= 0f) continue;

                        // Blend toward target layer
                        for (int l = 0; l < numLayers; l++)
                        {
                            float target = (l == s.layerIndex) ? 1f : 0f;
                            alphamaps[localY, localX, l] = Mathf.Lerp(alphamaps[localY, localX, l], target, eff);
                        }

                        // Renormalize to sum = 1.0
                        float sum = 0f;
                        for (int l = 0; l < numLayers; l++) sum += alphamaps[localY, localX, l];
                        if (sum > 0f)
                        {
                            float inv = 1f / sum;
                            for (int l = 0; l < numLayers; l++) alphamaps[localY, localX, l] *= inv;
                        }
                    }
                }
            }

            // ---- Phase 4: Write back once ----
            data.SetAlphamaps(unionX1, unionY1, alphamaps);
            EditorUtility.SetDirty(data);

            return McpResponse.Success(
                $"Applied {parsed.Count} texture strokes to terrain '{terrain.name}' in 1 operation (union bbox: {totalW}×{totalH} px)",
                new Dictionary<string, object>
                {
                    ["strokeCount"]        = parsed.Count,
                    ["alphamapResolution"] = amRes,
                    ["unionBbox"] = new Dictionary<string, object>
                    {
                        ["x"] = unionX1, ["y"] = unionY1,
                        ["width"] = totalW, ["height"] = totalH
                    }
                });
        }

        // ====================================================================
        // Handler: unity_add_terrain_trees
        // ====================================================================
        private static McpToolResult AddTerrainTrees(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            var (prefabPath, prefabErr) = RequireArg(args, "prefabPath");
            if (prefabErr != null) return prefabErr;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return McpToolResult.Error($"Prefab not found at path: {prefabPath}");

            float minScale = ArgumentParser.GetFloat(args, "minScale", 0.8f);
            float maxScale = ArgumentParser.GetFloat(args, "maxScale", 1.2f);
            int seed = ArgumentParser.GetInt(args, "seed", 42);
            bool clearExisting = ArgumentParser.GetBool(args, "clearExisting", false);

            Undo.RecordObject(data, "Add Terrain Trees");

            // Find or add tree prototype
            int protoIndex = -1;
            var prototypes = data.treePrototypes;
            for (int i = 0; i < prototypes.Length; i++)
            {
                if (prototypes[i].prefab == prefab)
                {
                    protoIndex = i;
                    break;
                }
            }

            if (protoIndex < 0)
            {
                // Add new prototype
                var newProtos = new TreePrototype[prototypes.Length + 1];
                Array.Copy(prototypes, newProtos, prototypes.Length);
                newProtos[prototypes.Length] = new TreePrototype { prefab = prefab };
                data.treePrototypes = newProtos;
                protoIndex = prototypes.Length;
            }

            // Collect existing trees (unless clearing)
            var treeList = new List<TreeInstance>();
            if (!clearExisting)
            {
                treeList.AddRange(data.treeInstances);
            }

            var rng = new System.Random(seed);
            int addedCount = 0;

            // Explicit positions
            List<object> positions = null;
            if (ArgumentParser.HasKey(args, "positions") && args.TryGetValue("positions", out var posObj))
                positions = posObj as List<object>;

            if (positions != null && positions.Count > 0)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    if (positions[i] is Dictionary<string, object> posDict)
                    {
                        float px = ArgumentParser.GetFloat(posDict, "x", 0.5f);
                        float pz = ArgumentParser.GetFloat(posDict, "z", 0.5f);
                        float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);

                        treeList.Add(new TreeInstance
                        {
                            prototypeIndex = protoIndex,
                            position = new Vector3(px, 0f, pz), // Y is set by snap
                            widthScale = scale,
                            heightScale = scale,
                            color = Color.white,
                            lightmapColor = Color.white,
                            rotation = (float)(rng.NextDouble() * Math.PI * 2)
                        });
                        addedCount++;
                    }
                }
            }
            else
            {
                // Random scatter
                int count = ArgumentParser.GetInt(args, "count", 10);
                for (int i = 0; i < count; i++)
                {
                    float px = (float)rng.NextDouble();
                    float pz = (float)rng.NextDouble();
                    float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);

                    treeList.Add(new TreeInstance
                    {
                        prototypeIndex = protoIndex,
                        position = new Vector3(px, 0f, pz),
                        widthScale = scale,
                        heightScale = scale,
                        color = Color.white,
                        lightmapColor = Color.white,
                        rotation = (float)(rng.NextDouble() * Math.PI * 2)
                    });
                    addedCount++;
                }
            }

            data.SetTreeInstances(treeList.ToArray(), true); // snapToSurface = true
            EditorUtility.SetDirty(data);

            return McpResponse.Success($"Added {addedCount} trees to terrain '{terrain.name}' (prototype: {prefab.name}, total: {data.treeInstanceCount})",
                new Dictionary<string, object>
                {
                    ["addedCount"] = addedCount,
                    ["totalTreeCount"] = data.treeInstanceCount,
                    ["prototypeIndex"] = protoIndex,
                    ["prefab"] = prefab.name,
                    ["clearExisting"] = clearExisting
                });
        }
    }
}
