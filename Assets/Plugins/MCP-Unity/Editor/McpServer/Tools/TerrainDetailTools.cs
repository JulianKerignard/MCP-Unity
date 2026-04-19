using System;
using System.Collections.Generic;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Terrain Detail Tools — vegetation detail layers (grass, ground cover, meshes).
    /// Contains 3 tools: add_terrain_detail, paint_terrain_detail, remove_terrain_detail
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterTerrainDetailTools()
        {
            // ================================================================
            // unity_add_terrain_detail
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_terrain_detail",
                description = "TERRAIN: Add a detail prototype (grass/mesh) to a terrain",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                        ["renderMode"]     = new McpPropertySchema { type = "string", description = "GrassBillboard | Grass | VertexLit (mesh)", @enum = new List<string> { "GrassBillboard", "Grass", "VertexLit" } },
                        ["texturePath"]    = new McpPropertySchema { type = "string", description = "Texture asset path (GrassBillboard / Grass modes)" },
                        ["meshPath"]       = new McpPropertySchema { type = "string", description = "Mesh prefab asset path (VertexLit mode)" },
                        ["minWidth"]       = new McpPropertySchema { type = "number", description = "Min width in world units (default: 1)" },
                        ["maxWidth"]       = new McpPropertySchema { type = "number", description = "Max width in world units (default: 2)" },
                        ["minHeight"]      = new McpPropertySchema { type = "number", description = "Min height in world units (default: 1)" },
                        ["maxHeight"]      = new McpPropertySchema { type = "number", description = "Max height in world units (default: 2)" },
                        ["noiseSpread"]    = new McpPropertySchema { type = "number", description = "Noise spread factor (default: 0.1)" },
                        ["healthyColor"]   = new McpPropertySchema { type = "string", description = "Healthy color hex (default: #67C13B)" },
                        ["dryColor"]       = new McpPropertySchema { type = "string", description = "Dry color hex (default: #AFA35F)" },
                    },
                    required = new List<string> { "renderMode" }
                }
            }, AddTerrainDetail);

            // ================================================================
            // unity_paint_terrain_detail
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_paint_terrain_detail",
                description = "TERRAIN: Paint detail density (grass/mesh) on a terrain region",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"]  = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                        ["prototypeIndex"]  = new McpPropertySchema { type = "integer", description = "Detail prototype index (from add_terrain_detail)" },
                        ["density"]         = new McpPropertySchema { type = "integer", description = "Target density 0–16 (default: 8)" },
                        ["region"]          = new McpPropertySchema { type = "object", description = "Normalized region {x,y,width,height} 0-1 (default: full terrain)" },
                        ["brush"]           = new McpPropertySchema { type = "string", description = "set | add | subtract (default: set)", @enum = new List<string> { "set", "add", "subtract" } },
                    },
                    required = new List<string> { "prototypeIndex" }
                }
            }, PaintTerrainDetail);

            // ================================================================
            // unity_remove_terrain_detail
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_remove_terrain_detail",
                description = "TERRAIN: Remove a detail prototype from a terrain by index",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                        ["prototypeIndex"] = new McpPropertySchema { type = "integer", description = "Detail prototype index to remove" },
                    },
                    required = new List<string> { "prototypeIndex" }
                }
            }, RemoveTerrainDetail);
        }

        // ====================================================================
        // Handler: unity_add_terrain_detail
        // ====================================================================
        private static McpToolResult AddTerrainDetail(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found. Provide gameObjectPath or ensure a Terrain exists in the scene.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                string renderModeStr = ArgumentParser.GetString(args, "renderMode", "GrassBillboard");
                DetailRenderMode renderMode;
                switch (renderModeStr)
                {
                    case "Grass":        renderMode = DetailRenderMode.Grass;        break;
                    case "VertexLit":    renderMode = DetailRenderMode.VertexLit;    break;
                    default:             renderMode = DetailRenderMode.GrassBillboard; break;
                }

                var proto = new DetailPrototype
                {
                    renderMode  = renderMode,
                    minWidth    = ArgumentParser.GetFloat(args, "minWidth",    1f),
                    maxWidth    = ArgumentParser.GetFloat(args, "maxWidth",    2f),
                    minHeight   = ArgumentParser.GetFloat(args, "minHeight",   1f),
                    maxHeight   = ArgumentParser.GetFloat(args, "maxHeight",   2f),
                    noiseSpread = ArgumentParser.GetFloat(args, "noiseSpread", 0.1f),
                    healthyColor = ParseColorArg(args, "healthyColor", new Color(0.404f, 0.757f, 0.231f)),
                    dryColor     = ParseColorArg(args, "dryColor",     new Color(0.686f, 0.639f, 0.373f)),
                };

                // Texture (billboard / grass)
                string texPath = ArgumentParser.GetString(args, "texturePath", null);
                if (!string.IsNullOrEmpty(texPath))
                {
                    var (sanitizedTexPath, texSanitizeErr) = TrySanitizePath(texPath, "texturePath");
                    if (texSanitizeErr != null) return texSanitizeErr;
                    texPath = sanitizedTexPath;
                    proto.prototypeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (proto.prototypeTexture == null)
                        return McpToolResult.Error($"Texture not found at '{texPath}'.");
                }

                // Mesh (VertexLit)
                string meshPath = ArgumentParser.GetString(args, "meshPath", null);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    var (sanitizedMeshPath, meshSanitizeErr) = TrySanitizePath(meshPath, "meshPath");
                    if (meshSanitizeErr != null) return meshSanitizeErr;
                    meshPath = sanitizedMeshPath;
                    proto.prototype = AssetDatabase.LoadAssetAtPath<GameObject>(meshPath);
                    if (proto.prototype == null)
                        return McpToolResult.Error($"Mesh prefab not found at '{meshPath}'.");
                }

                if (renderMode == DetailRenderMode.VertexLit && proto.prototype == null)
                    return McpToolResult.Error("VertexLit mode requires a meshPath.");
                if (renderMode != DetailRenderMode.VertexLit && proto.prototypeTexture == null)
                    return McpToolResult.Error($"{renderModeStr} mode requires a texturePath.");

                Undo.RecordObject(data, "Add Terrain Detail Prototype");

                var protos = data.detailPrototypes;
                var newProtos = new DetailPrototype[protos.Length + 1];
                protos.CopyTo(newProtos, 0);
                newProtos[protos.Length] = proto;
                data.detailPrototypes = newProtos;
                data.RefreshPrototypes();

                EditorUtility.SetDirty(data);

                return McpResponse.Success($"Added detail prototype '{renderModeStr}' at index {protos.Length} on terrain '{terrain.name}'.",
                    new Dictionary<string, object>
                    {
                        ["prototypeIndex"] = protos.Length,
                        ["renderMode"]     = renderModeStr,
                        ["totalPrototypes"] = newProtos.Length
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add terrain detail: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_paint_terrain_detail
        // ====================================================================
        private static McpToolResult PaintTerrainDetail(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                int protoIndex = ArgumentParser.GetInt(args, "prototypeIndex", 0);
                if (protoIndex < 0 || protoIndex >= data.detailPrototypes.Length)
                    return McpToolResult.Error($"prototypeIndex {protoIndex} out of range (0–{data.detailPrototypes.Length - 1}).");

                int density  = Mathf.Clamp(ArgumentParser.GetInt(args, "density", 8), 0, 16);
                string brush = ArgumentParser.GetString(args, "brush", "set");

                // Convert normalized region → detail-resolution pixels
                int dw = data.detailWidth;
                int dh = data.detailHeight;
                int xBase, yBase, width, height;
                TerrainHelpers.NormalizedRegionToPixels(args, dw, out xBase, out yBase, out width, out height);
                // NormalizedRegionToPixels uses a single resolution; clamp height dimension separately
                if (yBase + height > dh) height = dh - yBase;

                Undo.RecordObject(data, "Paint Terrain Detail");

                int[,] layer = data.GetDetailLayer(xBase, yBase, width, height, protoIndex);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        switch (brush)
                        {
                            case "add":      layer[y, x] = Mathf.Clamp(layer[y, x] + density, 0, 16); break;
                            case "subtract": layer[y, x] = Mathf.Clamp(layer[y, x] - density, 0, 16); break;
                            default:         layer[y, x] = density; break;
                        }
                    }
                }

                data.SetDetailLayer(xBase, yBase, protoIndex, layer);
                terrain.Flush();
                EditorUtility.SetDirty(data);

                return McpResponse.Success(
                    $"Painted detail layer {protoIndex} on terrain '{terrain.name}' ({brush}, density={density}).",
                    new Dictionary<string, object>
                    {
                        ["prototypeIndex"] = protoIndex,
                        ["brush"]          = brush,
                        ["density"]        = density,
                        ["regionPixels"]   = new Dictionary<string, object>
                            { ["x"] = xBase, ["y"] = yBase, ["width"] = width, ["height"] = height }
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to paint terrain detail: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_remove_terrain_detail
        // ====================================================================
        private static McpToolResult RemoveTerrainDetail(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                int protoIndex = ArgumentParser.GetInt(args, "prototypeIndex", 0);
                var protos = data.detailPrototypes;

                if (protoIndex < 0 || protoIndex >= protos.Length)
                    return McpToolResult.Error($"prototypeIndex {protoIndex} out of range (0–{protos.Length - 1}).");

                string removedName = protos[protoIndex].prototypeTexture != null
                    ? protos[protoIndex].prototypeTexture.name
                    : protos[protoIndex].prototype != null
                        ? protos[protoIndex].prototype.name
                        : $"index {protoIndex}";

                Undo.RecordObject(data, $"Remove Terrain Detail '{removedName}'");

                data.RemoveDetailPrototype(protoIndex);
                data.RefreshPrototypes();
                EditorUtility.SetDirty(data);

                return McpResponse.Success(
                    $"Removed detail prototype '{removedName}' (was index {protoIndex}) from terrain '{terrain.name}'.",
                    new Dictionary<string, object>
                    {
                        ["removedName"]     = removedName,
                        ["removedIndex"]    = protoIndex,
                        ["remainingCount"]  = data.detailPrototypes.Length
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to remove terrain detail: {ex.Message}");
            }
        }

        // ====================================================================
        // Utility: parse a hex color from args with fallback
        // ====================================================================
        private static Color ParseColorArg(Dictionary<string, object> args, string key, Color fallback)
        {
            string hex = ArgumentParser.GetString(args, key, null);
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return fallback;
        }
    }
}
