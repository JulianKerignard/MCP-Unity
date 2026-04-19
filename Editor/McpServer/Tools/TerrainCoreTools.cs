using System;
using System.Collections.Generic;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Terrain Core Tools - Create, inspect, modify terrains and sculpt heightmaps.
    /// Contains 4 tools: create_terrain, get_terrain_info, modify_terrain, set_terrain_heights
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterTerrainCoreTools()
        {
            // ================================================================
            // unity_create_terrain
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_terrain",
                description = "Create a new Terrain GameObject with TerrainData asset. Configurable size, resolution, and position.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["name"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Name for the terrain GameObject"
                        },
                        ["width"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Terrain width in world units (X axis). Default: 1000"
                        },
                        ["height"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Terrain max height in world units (Y axis). Default: 600"
                        },
                        ["length"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Terrain length in world units (Z axis). Default: 1000"
                        },
                        ["heightmapResolution"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Heightmap resolution. Valid: 33, 65, 129, 257, 513, 1025, 2049, 4097. Default: 513"
                        },
                        ["alphamapResolution"] = new McpPropertySchema
                        {
                            type = "integer",
                            description = "Alphamap (splatmap) resolution, power of 2. Default: 512"
                        },
                        ["position"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "World position {x, y, z}. Default: {0, 0, 0}"
                        },
                        ["parentPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional parent GameObject path"
                        }
                    },
                    required = new List<string> { "name" }
                }
            }, CreateTerrain);

            // ================================================================
            // unity_get_terrain_info
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_terrain_info",
                description = "Get comprehensive info about a Terrain: size, resolution, layers, tree count, rendering settings, neighbors.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Path to terrain GameObject. If omitted, uses the first active Terrain."
                        }
                    },
                    required = new List<string>()
                }
            }, GetTerrainInfo);

            // ================================================================
            // unity_modify_terrain
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_modify_terrain",
                description = "Modify terrain settings: size, pixel error, detail/tree distances, rendering flags. Only provided values are changed.",
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
                        ["size"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Terrain size {x, y, z} in world units"
                        },
                        ["heightmapPixelError"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "LOD pixel error threshold (1-200). Lower = higher quality."
                        },
                        ["detailObjectDistance"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Max distance to render detail objects (grass, etc.)"
                        },
                        ["treeDistance"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Max distance to render trees"
                        },
                        ["treeBillboardDistance"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Distance at which trees become billboards"
                        },
                        ["basemapDistance"] = new McpPropertySchema
                        {
                            type = "number",
                            description = "Distance beyond which low-res basemap is used"
                        },
                        ["drawHeightmap"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Enable/disable terrain mesh rendering"
                        },
                        ["drawTreesAndFoliage"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Enable/disable trees and detail rendering"
                        },
                        ["drawInstanced"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Enable GPU instanced rendering"
                        },
                        ["allowAutoConnect"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Auto-connect neighboring terrain tiles"
                        }
                    },
                    required = new List<string>()
                }
            }, ModifyTerrain);

            // ================================================================
            // unity_set_terrain_heights_batch
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_terrain_heights_batch",
                description = "Sculpt the terrain heightmap with multiple operations in ONE call. Same parameters per stroke as unity_set_terrain_heights. Heightmap is loaded and saved once (union bbox of all strokes). Use instead of calling unity_set_terrain_heights repeatedly (paths, ridges, rivers, craters).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active Terrain)" },
                        ["strokes"]        = new McpPropertySchema
                        {
                            type = "array",
                            description = "Array of stroke objects. Each stroke: { operation (required: flatten|raise|lower|set|noise|smooth), value, opacity, brushCenter {x,z}, brushSize, brushShape, brushFalloff, brushRotation, brushName, region, intensity, seed }"
                        }
                    },
                    required = new List<string> { "strokes" }
                }
            }, SetTerrainHeightsBatch);

            // ================================================================
            // unity_list_terrain_brushes
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_terrain_brushes",
                description = "TERRAIN: List all Texture2D assets available as brush masks for unity_set_terrain_heights and unity_paint_terrain_texture. Use the returned 'name' as the brushName parameter.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["filter"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Optional case-insensitive name filter (e.g. 'smooth', 'cliff')"
                        }
                    }
                }
            }, args =>
            {
                string filter = ArgumentParser.GetString(args, "filter", "");
                var brushes = BrushHelper.ListAvailableBrushes(string.IsNullOrEmpty(filter) ? null : filter);
                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["count"]   = brushes.Count,
                    ["brushes"] = brushes.ConvertAll(b => new Dictionary<string, object>
                    {
                        ["name"] = b.name,
                        ["path"] = b.path
                    })
                });
            });
        }

        // ====================================================================
        // Handler: unity_create_terrain
        // ====================================================================
        private static McpToolResult CreateTerrain(Dictionary<string, object> args)
        {
            var (name, nameErr) = RequireArg(args, "name");
            if (nameErr != null) return nameErr;

            float width = ArgumentParser.GetFloat(args, "width", 1000f);
            float height = ArgumentParser.GetFloat(args, "height", 600f);
            float length = ArgumentParser.GetFloat(args, "length", 1000f);
            int hmRes = ArgumentParser.GetInt(args, "heightmapResolution", 513);
            int amRes = ArgumentParser.GetInt(args, "alphamapResolution", 512);
            string parentPath = ArgumentParser.GetString(args, "parentPath", null);

            // Clamp heightmap resolution to valid values
            hmRes = ClampHeightmapResolution(hmRes);

            // Create TerrainData
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = hmRes;
            terrainData.alphamapResolution = amRes;
            terrainData.size = new Vector3(width, height, length);

            // Ensure directory exists
            string assetDir = "Assets/Terrain";
            if (!AssetDatabase.IsValidFolder(assetDir))
                AssetDatabase.CreateFolder("Assets", "Terrain");

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{assetDir}/{name}_Data.asset");
            AssetDatabase.CreateAsset(terrainData, assetPath);

            // Create Terrain GameObject
            var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrainGO.name = name;

            // Set position
            if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "position", out var posDict))
            {
                float px = ArgumentParser.GetFloat(posDict, "x", 0f);
                float py = ArgumentParser.GetFloat(posDict, "y", 0f);
                float pz = ArgumentParser.GetFloat(posDict, "z", 0f);
                terrainGO.transform.position = new Vector3(px, py, pz);
            }

            // Parent
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObjectHelpers.FindGameObject(parentPath);
                if (parent != null)
                    terrainGO.transform.SetParent(parent.transform, true);
            }

            Undo.RegisterCreatedObjectUndo(terrainGO, $"Create Terrain '{name}'");
            AssetDatabase.SaveAssets();

            return McpResponse.Success(new Dictionary<string, object>
            {
                ["name"] = name,
                ["path"] = GameObjectHelpers.GetGameObjectPath(terrainGO),
                ["assetPath"] = assetPath,
                ["size"] = new Dictionary<string, object> { ["x"] = width, ["y"] = height, ["z"] = length },
                ["heightmapResolution"] = hmRes,
                ["alphamapResolution"] = amRes
            });
        }

        // ====================================================================
        // Handler: unity_get_terrain_info
        // ====================================================================
        private static McpToolResult GetTerrainInfo(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            if (terrain.terrainData == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            return McpResponse.Success(TerrainHelpers.SerializeTerrainInfo(terrain));
        }

        // ====================================================================
        // Handler: unity_modify_terrain
        // ====================================================================
        private static McpToolResult ModifyTerrain(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            Undo.RecordObject(terrain, "Modify Terrain");
            Undo.RecordObject(data, "Modify TerrainData");

            var changed = new List<string>();

            // Size
            if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "size", out var sizeDict))
            {
                float sx = ArgumentParser.GetFloat(sizeDict, "x", data.size.x);
                float sy = ArgumentParser.GetFloat(sizeDict, "y", data.size.y);
                float sz = ArgumentParser.GetFloat(sizeDict, "z", data.size.z);
                data.size = new Vector3(sx, sy, sz);
                changed.Add("size");
            }

            // Rendering properties
            if (ArgumentParser.HasKey(args, "heightmapPixelError"))
            {
                terrain.heightmapPixelError = ArgumentParser.GetFloat(args, "heightmapPixelError", terrain.heightmapPixelError);
                changed.Add("heightmapPixelError");
            }
            if (ArgumentParser.HasKey(args, "detailObjectDistance"))
            {
                terrain.detailObjectDistance = ArgumentParser.GetFloat(args, "detailObjectDistance", terrain.detailObjectDistance);
                changed.Add("detailObjectDistance");
            }
            if (ArgumentParser.HasKey(args, "treeDistance"))
            {
                terrain.treeDistance = ArgumentParser.GetFloat(args, "treeDistance", terrain.treeDistance);
                changed.Add("treeDistance");
            }
            if (ArgumentParser.HasKey(args, "treeBillboardDistance"))
            {
                terrain.treeBillboardDistance = ArgumentParser.GetFloat(args, "treeBillboardDistance", terrain.treeBillboardDistance);
                changed.Add("treeBillboardDistance");
            }
            if (ArgumentParser.HasKey(args, "basemapDistance"))
            {
                terrain.basemapDistance = ArgumentParser.GetFloat(args, "basemapDistance", terrain.basemapDistance);
                changed.Add("basemapDistance");
            }
            if (ArgumentParser.HasKey(args, "drawHeightmap"))
            {
                terrain.drawHeightmap = ArgumentParser.GetBool(args, "drawHeightmap", terrain.drawHeightmap);
                changed.Add("drawHeightmap");
            }
            if (ArgumentParser.HasKey(args, "drawTreesAndFoliage"))
            {
                terrain.drawTreesAndFoliage = ArgumentParser.GetBool(args, "drawTreesAndFoliage", terrain.drawTreesAndFoliage);
                changed.Add("drawTreesAndFoliage");
            }
            if (ArgumentParser.HasKey(args, "drawInstanced"))
            {
                terrain.drawInstanced = ArgumentParser.GetBool(args, "drawInstanced", terrain.drawInstanced);
                changed.Add("drawInstanced");
            }
            if (ArgumentParser.HasKey(args, "allowAutoConnect"))
            {
                terrain.allowAutoConnect = ArgumentParser.GetBool(args, "allowAutoConnect", terrain.allowAutoConnect);
                changed.Add("allowAutoConnect");
            }

            terrain.Flush();
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(data);

            if (changed.Count == 0)
                return McpToolResult.Error("No recognized properties provided. Valid properties: size {x,y,z}, heightmapPixelError, detailObjectDistance, treeDistance, treeBillboardDistance, basemapDistance, drawHeightmap, drawTreesAndFoliage, drawInstanced, allowAutoConnect.");

            return McpResponse.Success($"Modified terrain '{terrain.name}': {string.Join(", ", changed)}",
                TerrainHelpers.SerializeTerrainInfo(terrain));
        }

        // ====================================================================
        // Batch stroke data (used by SetTerrainHeightsBatch)
        // ====================================================================
        private struct ParsedHeightStroke
        {
            public string operation;
            public float  value;
            public float  opacity;
            public float  intensity;
            public int    seed;
            public string brushShape;
            public float  brushFalloff;
            public float  brushRotRad;
            public string resolvedBrushName;
            public bool   useBrush;
            public int    xBase, yBase, xEnd, yEnd;   // absolute heightmap pixel coords
            public float  centerPixX, centerPixY, radiusPix;
        }

        // ====================================================================
        // Handler: unity_set_terrain_heights_batch
        // ====================================================================
        private static McpToolResult SetTerrainHeightsBatch(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            if (!args.TryGetValue("strokes", out var strokesObj) || !(strokesObj is List<object> strokesRaw) || strokesRaw.Count == 0)
                return McpToolResult.Error("'strokes' array is required and must not be empty.");

            int res = data.heightmapResolution;

            // ---- Phase 1: Parse all strokes ----
            var parsed = new List<ParsedHeightStroke>(strokesRaw.Count);
            int unionX1 = res, unionY1 = res, unionX2 = 0, unionY2 = 0;

            var validOps = new[] { "flatten", "raise", "lower", "set", "noise", "smooth" };

            for (int i = 0; i < strokesRaw.Count; i++)
            {
                if (!(strokesRaw[i] is Dictionary<string, object> sd))
                    return McpToolResult.Error($"strokes[{i}] must be an object.");

                string operation = ArgumentParser.GetString(sd, "operation", null);
                if (string.IsNullOrEmpty(operation))
                    return McpToolResult.Error($"strokes[{i}]: 'operation' is required.");

                operation = operation.ToLowerInvariant();
                bool validOp = false;
                foreach (var v in validOps) if (v == operation) { validOp = true; break; }
                if (!validOp)
                    return McpToolResult.Error($"strokes[{i}]: unknown operation '{operation}'. Valid: {string.Join(", ", validOps)}");

                float  value     = ArgumentParser.GetFloat(sd, "value",     0f);
                float  opacity   = Mathf.Clamp01(ArgumentParser.GetFloat(sd, "opacity",   1f));
                float  intensity = ArgumentParser.GetFloat(sd, "intensity", 1f);
                int    seed      = ArgumentParser.GetInt(sd,   "seed",      42);
                string shape     = ArgumentParser.GetString(sd, "brushShape",   "rect");
                float  falloff   = Mathf.Clamp01(ArgumentParser.GetFloat(sd, "brushFalloff", 0.5f));
                float  rotRad    = ArgumentParser.GetFloat(sd, "brushRotation", 0f) * Mathf.Deg2Rad;

                string brushName         = ArgumentParser.GetString(sd, "brushName", "");
                string resolvedBrushName = null;
                if (!string.IsNullOrEmpty(brushName))
                {
                    resolvedBrushName = BrushHelper.TryLoadTextureBrush(brushName);
                    if (resolvedBrushName == null)
                        return McpToolResult.Error($"strokes[{i}]: brush '{brushName}' not found. Use unity_list_terrain_brushes.");
                }

                bool useBrush = !string.IsNullOrEmpty(resolvedBrushName) || BrushHelper.IsBrushActive(sd);

                BrushHelper.GetBrushPixelRect(sd, data, res,
                    out int xBase, out int yBase, out int w, out int h,
                    out float cPixX, out float cPixY, out float radPix);

                int xEnd = xBase + w;
                int yEnd = yBase + h;

                unionX1 = Math.Min(unionX1, xBase);
                unionY1 = Math.Min(unionY1, yBase);
                unionX2 = Math.Max(unionX2, xEnd);
                unionY2 = Math.Max(unionY2, yEnd);

                parsed.Add(new ParsedHeightStroke
                {
                    operation        = operation,
                    value            = value,
                    opacity          = opacity,
                    intensity        = intensity,
                    seed             = seed,
                    brushShape       = shape,
                    brushFalloff     = falloff,
                    brushRotRad      = rotRad,
                    resolvedBrushName = resolvedBrushName,
                    useBrush         = useBrush,
                    xBase = xBase, yBase = yBase, xEnd = xEnd, yEnd = yEnd,
                    centerPixX = cPixX, centerPixY = cPixY, radiusPix = radPix
                });
            }

            // Clamp union bbox
            unionX1 = Mathf.Clamp(unionX1, 0, res - 1);
            unionY1 = Mathf.Clamp(unionY1, 0, res - 1);
            unionX2 = Mathf.Clamp(unionX2, 0, res);
            unionY2 = Mathf.Clamp(unionY2, 0, res);

            int totalW = unionX2 - unionX1;
            int totalH = unionY2 - unionY1;
            if (totalW <= 0 || totalH <= 0)
                return McpToolResult.Error("All strokes have zero pixel area after clamping.");

            // SEC-#432: guard against multi-hundred-MB allocations. A 4097x4097 terrain
            // already allocates ~64MB of float for the full heightmap; refuse anything beyond
            // ~100 MP (== 400 MB of float[,]) to keep memory use bounded.
            const long MaxHeightmapPixels = 100_000_000L;
            long requestedPixels = (long)totalW * totalH;
            if (requestedPixels > MaxHeightmapPixels)
            {
                return McpToolResult.Error(
                    $"Heightmap region too large: {totalW}x{totalH} = {requestedPixels:N0} pixels " +
                    $"(max {MaxHeightmapPixels:N0}). Split the operation into smaller strokes.");
            }

            // ---- Phase 2: Load heightmap once ----
            Undo.RecordObject(data, "Set Terrain Heights Batch");
            float[,] heights = data.GetHeights(unionX1, unionY1, totalW, totalH);

            // ---- Phase 3: Apply each stroke ----
            foreach (var s in parsed)
            {
                int sx1 = Math.Max(s.xBase, unionX1);
                int sy1 = Math.Max(s.yBase, unionY1);
                int sx2 = Math.Min(s.xEnd,  unionX2);
                int sy2 = Math.Min(s.yEnd,  unionY2);
                int sw  = sx2 - sx1;
                int sh  = sy2 - sy1;
                if (sw <= 0 || sh <= 0) continue;

                switch (s.operation)
                {
                    case "flatten":
                    case "set":
                    {
                        float target = Mathf.Clamp01(s.value);
                        for (int absY = sy1; absY < sy2; absY++)
                        {
                            int localY = absY - unionY1;
                            for (int absX = sx1; absX < sx2; absX++)
                            {
                                int   localX = absX - unionX1;
                                float w = s.useBrush
                                    ? BrushHelper.GetBrushWeight(absX, absY, s.centerPixX, s.centerPixY,
                                        s.radiusPix, s.radiusPix, s.brushRotRad, s.brushShape, s.brushFalloff, s.resolvedBrushName)
                                    : 1f;
                                heights[localY, localX] = Mathf.Lerp(heights[localY, localX], target, w * s.opacity);
                            }
                        }
                        break;
                    }

                    case "raise":
                    {
                        for (int absY = sy1; absY < sy2; absY++)
                        {
                            int localY = absY - unionY1;
                            for (int absX = sx1; absX < sx2; absX++)
                            {
                                int   localX = absX - unionX1;
                                float w = s.useBrush
                                    ? BrushHelper.GetBrushWeight(absX, absY, s.centerPixX, s.centerPixY,
                                        s.radiusPix, s.radiusPix, s.brushRotRad, s.brushShape, s.brushFalloff, s.resolvedBrushName)
                                    : 1f;
                                heights[localY, localX] = Mathf.Clamp01(heights[localY, localX] + s.value * w * s.opacity);
                            }
                        }
                        break;
                    }

                    case "lower":
                    {
                        for (int absY = sy1; absY < sy2; absY++)
                        {
                            int localY = absY - unionY1;
                            for (int absX = sx1; absX < sx2; absX++)
                            {
                                int   localX = absX - unionX1;
                                float w = s.useBrush
                                    ? BrushHelper.GetBrushWeight(absX, absY, s.centerPixX, s.centerPixY,
                                        s.radiusPix, s.radiusPix, s.brushRotRad, s.brushShape, s.brushFalloff, s.resolvedBrushName)
                                    : 1f;
                                heights[localY, localX] = Mathf.Clamp01(heights[localY, localX] - s.value * w * s.opacity);
                            }
                        }
                        break;
                    }

                    case "noise":
                    {
                        float amplitude = Mathf.Clamp01(s.value);
                        float frequency = Mathf.Max(0.01f, s.intensity);
                        float offsetX   = s.seed * 17.3f;
                        float offsetY   = s.seed * 31.7f;
                        for (int absY = sy1; absY < sy2; absY++)
                        {
                            int localY = absY - unionY1;
                            for (int absX = sx1; absX < sx2; absX++)
                            {
                                int   localX = absX - unionX1;
                                float w = s.useBrush
                                    ? BrushHelper.GetBrushWeight(absX, absY, s.centerPixX, s.centerPixY,
                                        s.radiusPix, s.radiusPix, s.brushRotRad, s.brushShape, s.brushFalloff, s.resolvedBrushName)
                                    : 1f;
                                float nx = (float)absX / res * frequency + offsetX;
                                float ny = (float)absY / res * frequency + offsetY;
                                float n  = Mathf.PerlinNoise(nx, ny) * amplitude * w * s.opacity;
                                heights[localY, localX] = Mathf.Clamp01(heights[localY, localX] + n);
                            }
                        }
                        break;
                    }

                    case "smooth":
                    {
                        int passes = Mathf.Max(1, Mathf.RoundToInt(s.intensity));
                        for (int p = 0; p < passes; p++)
                        {
                            // Temporary array for the stroke region only
                            float[,] smoothed = new float[sh, sw];
                            for (int absY = sy1; absY < sy2; absY++)
                            {
                                int localY  = absY - unionY1;
                                int strokeY = absY - sy1;
                                for (int absX = sx1; absX < sx2; absX++)
                                {
                                    int   localX  = absX - unionX1;
                                    int   strokeX = absX - sx1;
                                    float w = s.useBrush
                                        ? BrushHelper.GetBrushWeight(absX, absY, s.centerPixX, s.centerPixY,
                                            s.radiusPix, s.radiusPix, s.brushRotRad, s.brushShape, s.brushFalloff, s.resolvedBrushName)
                                        : 1f;

                                    // Box filter — read neighbors from union heights, clamped
                                    float sum  = heights[localY, localX];
                                    int   cnt  = 1;
                                    if (absX > unionX1)     { sum += heights[localY, localX - 1]; cnt++; }
                                    if (absX < unionX2 - 1) { sum += heights[localY, localX + 1]; cnt++; }
                                    if (absY > unionY1)     { sum += heights[localY - 1, localX]; cnt++; }
                                    if (absY < unionY2 - 1) { sum += heights[localY + 1, localX]; cnt++; }

                                    float avg = sum / cnt;
                                    smoothed[strokeY, strokeX] = Mathf.Lerp(heights[localY, localX], avg, w * s.opacity);
                                }
                            }
                            // Write smoothed values back to the shared heights array
                            for (int absY = sy1; absY < sy2; absY++)
                            {
                                int localY  = absY - unionY1;
                                int strokeY = absY - sy1;
                                for (int absX = sx1; absX < sx2; absX++)
                                {
                                    heights[localY, absX - unionX1] = smoothed[strokeY, absX - sx1];
                                }
                            }
                        }
                        break;
                    }
                }
            }

            // ---- Phase 4: Save once ----
            data.SetHeights(unionX1, unionY1, heights);
            terrain.Flush();
            EditorUtility.SetDirty(data);

            return McpResponse.Success(
                $"Applied {parsed.Count} height strokes to terrain '{terrain.name}' in 1 operation (union bbox: {totalW}×{totalH} px)",
                new Dictionary<string, object>
                {
                    ["strokeCount"]           = parsed.Count,
                    ["heightmapResolution"]   = res,
                    ["unionBbox"] = new Dictionary<string, object>
                    {
                        ["x"] = unionX1, ["y"] = unionY1,
                        ["width"] = totalW, ["height"] = totalH
                    }
                });
        }

        // ====================================================================
        // Handler: unity_set_terrain_heights
        // ====================================================================
        private static McpToolResult SetTerrainHeights(Dictionary<string, object> args)
        {
            var terrain = TerrainHelpers.FindTerrain(args);
            if (terrain == null)
                return McpToolResult.Error("No Terrain found. Provide a valid gameObjectPath or ensure a Terrain exists in the scene.");

            var data = terrain.terrainData;
            if (data == null)
                return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData assigned.");

            var (operation, opErr) = RequireArg(args, "operation");
            if (opErr != null) return opErr;

            float value     = ArgumentParser.GetFloat(args, "value", 0f);
            float opacity   = Mathf.Clamp01(ArgumentParser.GetFloat(args, "opacity", 1f));
            float intensity = ArgumentParser.GetFloat(args, "intensity", 1f);
            int   seed      = ArgumentParser.GetInt(args, "seed", 42);
            int   res       = data.heightmapResolution;

            // Brush parameters
            string brushName     = ArgumentParser.GetString(args, "brushName", "");
            string brushShape    = ArgumentParser.GetString(args, "brushShape", "rect");
            float  brushFalloff  = Mathf.Clamp01(ArgumentParser.GetFloat(args, "brushFalloff", 0.5f));
            float  brushRotRad   = ArgumentParser.GetFloat(args, "brushRotation", 0f) * Mathf.Deg2Rad;
            bool   useBrush      = BrushHelper.IsBrushActive(args);

            // Pre-load texture brush if requested
            string resolvedBrushName = null;
            if (!string.IsNullOrEmpty(brushName))
            {
                resolvedBrushName = BrushHelper.TryLoadTextureBrush(brushName);
                if (resolvedBrushName == null)
                    return McpToolResult.Error($"Brush texture '{brushName}' not found. Use unity_list_terrain_brushes to see available brushes.");
                useBrush = true;
            }

            // Get bounding rect + brush center in pixel space
            BrushHelper.GetBrushPixelRect(args, data, res,
                out int xBase, out int yBase, out int width, out int height,
                out float centerPixX, out float centerPixY, out float radiusPix);

            Undo.RecordObject(data, $"Terrain Heights ({operation})");

            float[,] heights = data.GetHeights(xBase, yBase, width, height);

            switch (operation.ToLowerInvariant())
            {
                case "flatten":
                case "set":
                {
                    float target = Mathf.Clamp01(value);
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            float w = useBrush
                                ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                                    radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                                : 1f;
                            heights[y, x] = Mathf.Lerp(heights[y, x], target, w * opacity);
                        }
                    break;
                }

                case "raise":
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            float w = useBrush
                                ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                                    radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                                : 1f;
                            heights[y, x] = Mathf.Clamp01(heights[y, x] + value * w * opacity);
                        }
                    break;
                }

                case "lower":
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            float w = useBrush
                                ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                                    radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                                : 1f;
                            heights[y, x] = Mathf.Clamp01(heights[y, x] - value * w * opacity);
                        }
                    break;
                }

                case "noise":
                {
                    float amplitude = Mathf.Clamp01(value);
                    float frequency = Mathf.Max(0.01f, intensity);
                    float offsetX   = seed * 17.3f;
                    float offsetY   = seed * 31.7f;
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                        {
                            float w = useBrush
                                ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                                    radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                                : 1f;
                            float nx = (float)(xBase + x) / res * frequency + offsetX;
                            float ny = (float)(yBase + y) / res * frequency + offsetY;
                            float n  = Mathf.PerlinNoise(nx, ny) * amplitude * w * opacity;
                            heights[y, x] = Mathf.Clamp01(heights[y, x] + n);
                        }
                    break;
                }

                case "smooth":
                {
                    int passes = Mathf.Max(1, Mathf.RoundToInt(intensity));
                    for (int p = 0; p < passes; p++)
                    {
                        float[,] smoothed = new float[height, width];
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                            {
                                float w = useBrush
                                    ? BrushHelper.GetBrushWeight(xBase + x, yBase + y, centerPixX, centerPixY,
                                        radiusPix, radiusPix, brushRotRad, brushShape, brushFalloff, resolvedBrushName)
                                    : 1f;
                                float sum = heights[y, x];
                                int count = 1;
                                if (x > 0)          { sum += heights[y, x - 1]; count++; }
                                if (x < width - 1)  { sum += heights[y, x + 1]; count++; }
                                if (y > 0)          { sum += heights[y - 1, x]; count++; }
                                if (y < height - 1) { sum += heights[y + 1, x]; count++; }
                                float avg = sum / count;
                                // Blend between original and smoothed by brush weight * opacity
                                smoothed[y, x] = Mathf.Lerp(heights[y, x], avg, w * opacity);
                            }
                        heights = smoothed;
                    }
                    break;
                }

                default:
                    return McpToolResult.Error($"Unknown operation '{operation}'. Valid: flatten, raise, lower, set, noise, smooth");
            }

            data.SetHeights(xBase, yBase, heights);
            terrain.Flush();
            EditorUtility.SetDirty(data);

            return McpResponse.Success($"Applied '{operation}' to terrain '{terrain.name}' (region: {xBase},{yBase} size: {width}x{height})",
                new Dictionary<string, object>
                {
                    ["operation"] = operation,
                    ["regionPixels"] = new Dictionary<string, object>
                    {
                        ["x"] = xBase, ["y"] = yBase, ["width"] = width, ["height"] = height
                    },
                    ["heightmapResolution"] = res
                });
        }

        // ====================================================================
        // Utility: Clamp heightmap resolution to nearest valid value
        // ====================================================================
        private static int ClampHeightmapResolution(int input)
        {
            int[] valid = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            int closest = valid[0];
            int minDist = int.MaxValue;
            for (int i = 0; i < valid.Length; i++)
            {
                int dist = Mathf.Abs(input - valid[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = valid[i];
                }
            }
            return closest;
        }
    }
}
