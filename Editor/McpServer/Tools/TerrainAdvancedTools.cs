using System;
using System.Collections.Generic;
using System.IO;
using McpUnity.Protocol;
using McpUnity.Helpers;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Terrain Advanced Tools — heightmap I/O, neighbors, tree management.
    /// Contains 5 tools: import_heightmap, export_heightmap, set_terrain_neighbors,
    ///                    remove_terrain_trees, list_terrain_trees
    /// </summary>
    public partial class McpUnityServer
    {
        static partial void RegisterTerrainAdvancedTools()
        {
            // ================================================================
            // unity_import_heightmap
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_import_heightmap",
                description = "TERRAIN: Import a PNG/EXR texture as the terrain heightmap",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                        ["texturePath"]    = new McpPropertySchema { type = "string", description = "Asset path to the heightmap texture (PNG or EXR, grayscale)" },
                        ["mode"]           = new McpPropertySchema { type = "string", description = "replace | add | multiply", @enum = new List<string> { "replace", "add", "multiply" } },
                        ["scale"]          = new McpPropertySchema { type = "number", description = "Height scale multiplier 0-1" },
                        ["flipX"]          = new McpPropertySchema { type = "boolean", description = "Flip horizontally" },
                        ["flipY"]          = new McpPropertySchema { type = "boolean", description = "Flip vertically" },
                    },
                    required = new List<string> { "texturePath" }
                }
            }, ImportHeightmap);

            // ================================================================
            // unity_export_heightmap
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_export_heightmap",
                description = "TERRAIN: Export the terrain heightmap as a PNG file",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                        ["outputPath"]     = new McpPropertySchema { type = "string", description = "Output asset path (e.g. 'Assets/Terrain/heightmap.png')" },
                    },
                    required = new List<string> { "outputPath" }
                }
            }, ExportHeightmap);

            // ================================================================
            // unity_set_terrain_neighbors
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_terrain_neighbors",
                description = "TERRAIN: Connect neighboring terrain tiles to remove seams",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Center terrain GameObject path" },
                        ["leftPath"]       = new McpPropertySchema { type = "string", description = "Left (-X) neighbor terrain path" },
                        ["topPath"]        = new McpPropertySchema { type = "string", description = "Top (+Z) neighbor terrain path" },
                        ["rightPath"]      = new McpPropertySchema { type = "string", description = "Right (+X) neighbor terrain path" },
                        ["bottomPath"]     = new McpPropertySchema { type = "string", description = "Bottom (-Z) neighbor terrain path" },
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, SetTerrainNeighbors);

            // ================================================================
            // unity_remove_terrain_trees
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_remove_terrain_trees",
                description = "TERRAIN: Remove trees from terrain by prototype index and/or region",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"]  = new McpPropertySchema { type = "string",  description = "Terrain GameObject path (omit = first active terrain)" },
                        ["prototypeIndex"]  = new McpPropertySchema { type = "integer", description = "Remove only trees of this prototype index (omit = all prototypes)" },
                        ["region"]          = new McpPropertySchema { type = "object",  description = "Normalized region {x,y,width,height} 0-1 (omit = full terrain)" },
                    },
                    required = new List<string>()
                }
            }, RemoveTerrainTrees);

            // ================================================================
            // unity_list_terrain_trees
            // ================================================================
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_terrain_trees",
                description = "TERRAIN: List tree prototypes and instance counts on a terrain",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Terrain GameObject path (omit = first active terrain)" },
                    },
                    required = new List<string>()
                }
            }, ListTerrainTrees);
        }

        // ====================================================================
        // Handler: unity_import_heightmap
        // ====================================================================
        private static McpToolResult ImportHeightmap(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                var (texPath, texPathErr) = RequireArg(args, "texturePath");
                if (texPathErr != null) return texPathErr;
                var (sanitizedTexPath, texSanitizeErr) = TrySanitizePath(texPath, "texturePath");
                if (texSanitizeErr != null) return texSanitizeErr;
                texPath = sanitizedTexPath;

                string mode  = ArgumentParser.GetString(args, "mode", "replace");
                float  scale = Mathf.Clamp01(ArgumentParser.GetFloat(args, "scale", 1f));
                bool   flipX = ArgumentParser.GetBool(args, "flipX", false);
                bool   flipY = ArgumentParser.GetBool(args, "flipY", false);

                // Ensure the texture is Read/Write enabled
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer == null)
                    return McpToolResult.Error($"No TextureImporter found for '{texPath}'. Ensure it is a valid texture asset.");

                bool wasReadable = importer.isReadable;
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    if (!wasReadable) { importer.isReadable = false; AssetDatabase.ImportAsset(texPath); }
                    return McpToolResult.Error($"Texture not found at '{texPath}'.");
                }

                // SEC-#441: snapshot before any read/write so Undo captures the original
                // heightmap state. Previously RecordObject ran after GetHeights/modifications,
                // which "worked" only because we mutate a local copy.
                Undo.RecordObject(data, $"Import Heightmap '{texPath}'");

                int res = data.heightmapResolution;
                float[,] heights = data.GetHeights(0, 0, res, res);

                float minH = float.MaxValue, maxH = float.MinValue;

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        // Sample texture — coords mapped to heightmap; flip if requested
                        float u = flipX ? 1f - (float)x / (res - 1) : (float)x / (res - 1);
                        float v = flipY ? 1f - (float)y / (res - 1) : (float)y / (res - 1);

                        // TerrainData heights[y,x] maps to v,u (row=z, col=x)
                        float sample = tex.GetPixelBilinear(u, v).grayscale * scale;

                        switch (mode)
                        {
                            case "add":      heights[y, x] = Mathf.Clamp01(heights[y, x] + sample); break;
                            case "multiply": heights[y, x] = Mathf.Clamp01(heights[y, x] * sample); break;
                            default:         heights[y, x] = sample; break;
                        }

                        if (heights[y, x] < minH) minH = heights[y, x];
                        if (heights[y, x] > maxH) maxH = heights[y, x];
                    }
                }

                // Already recorded above (SEC-#441) — apply the modified heights.
                data.SetHeights(0, 0, heights);
                terrain.Flush();
                EditorUtility.SetDirty(data);

                // Restore readable flag if we changed it
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                }

                return McpResponse.Success(
                    $"Imported heightmap '{texPath}' → terrain '{terrain.name}' (mode: {mode}, scale: {scale}).",
                    new Dictionary<string, object>
                    {
                        ["texturePath"]         = texPath,
                        ["mode"]                = mode,
                        ["scale"]               = scale,
                        ["heightmapResolution"] = res,
                        ["minHeight01"]         = minH,
                        ["maxHeight01"]         = maxH
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to import heightmap: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_export_heightmap
        // ====================================================================
        private static McpToolResult ExportHeightmap(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                var (outputPath, outputPathErr) = RequireArg(args, "outputPath");
                if (outputPathErr != null) return outputPathErr;
                var (sanitizedOutputPath, outputSanitizeErr) = TrySanitizePath(outputPath, "outputPath");
                if (outputSanitizeErr != null) return outputSanitizeErr;
                outputPath = sanitizedOutputPath;

                int res = data.heightmapResolution;
                float[,] heights = data.GetHeights(0, 0, res, res);

                // Build grayscale Texture2D (R16 → R8 PNG for broad compatibility)
                var tex = new Texture2D(res, res, TextureFormat.R8, false);
                Color[] pixels = new Color[res * res];

                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        pixels[y * res + x] = new Color(heights[y, x], heights[y, x], heights[y, x], 1f);

                tex.SetPixels(pixels);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);

                string fullPath = Path.GetFullPath(outputPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(fullPath, png);
                AssetDatabase.Refresh();

                return McpResponse.Success(
                    $"Exported heightmap to '{outputPath}' ({res}x{res} px, {png.Length / 1024} KB).",
                    new Dictionary<string, object>
                    {
                        ["outputPath"]          = outputPath,
                        ["heightmapResolution"] = res,
                        ["fileSizeKB"]          = png.Length / 1024
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to export heightmap: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_set_terrain_neighbors
        // ====================================================================
        private static McpToolResult SetTerrainNeighbors(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found. gameObjectPath is required for set_terrain_neighbors.");

                Terrain ResolveNeighbor(string key)
                {
                    string path = ArgumentParser.GetString(args, key, null);
                    if (string.IsNullOrEmpty(path)) return null;
                    var go = GameObjectHelpers.FindGameObject(path);
                    return go != null ? go.GetComponent<Terrain>() : null;
                }

                var left   = ResolveNeighbor("leftPath");
                var top    = ResolveNeighbor("topPath");
                var right  = ResolveNeighbor("rightPath");
                var bottom = ResolveNeighbor("bottomPath");

                terrain.SetNeighbors(left, top, right, bottom);
                terrain.Flush();

                var connected = new List<string>();
                if (left   != null) connected.Add($"left={left.name}");
                if (top    != null) connected.Add($"top={top.name}");
                if (right  != null) connected.Add($"right={right.name}");
                if (bottom != null) connected.Add($"bottom={bottom.name}");

                if (connected.Count == 0)
                    return McpToolResult.Error("No valid neighbor paths provided. Pass at least one of: leftPath, topPath, rightPath, bottomPath.");

                return McpResponse.Success(
                    $"Connected neighbors for terrain '{terrain.name}': {string.Join(", ", connected)}.",
                    new Dictionary<string, object>
                    {
                        ["terrain"]    = terrain.name,
                        ["left"]       = left   != null ? left.name   : null,
                        ["top"]        = top    != null ? top.name    : null,
                        ["right"]      = right  != null ? right.name  : null,
                        ["bottom"]     = bottom != null ? bottom.name : null,
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set terrain neighbors: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_remove_terrain_trees
        // ====================================================================
        private static McpToolResult RemoveTerrainTrees(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                bool filterProto  = ArgumentParser.HasKey(args, "prototypeIndex");
                int  protoIndex   = ArgumentParser.GetInt(args, "prototypeIndex", -1);
                bool filterRegion = ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "region", out var regionDict);

                float rx = 0f, ry = 0f, rw = 1f, rh = 1f;
                if (filterRegion && regionDict != null)
                {
                    rx = ArgumentParser.GetFloat(regionDict, "x",      0f);
                    ry = ArgumentParser.GetFloat(regionDict, "y",      0f);
                    rw = ArgumentParser.GetFloat(regionDict, "width",  1f);
                    rh = ArgumentParser.GetFloat(regionDict, "height", 1f);
                }

                var instances  = data.treeInstances;
                int before     = instances.Length;
                var kept       = new List<TreeInstance>();

                foreach (var tree in instances)
                {
                    bool removeProto  = filterProto  && tree.prototypeIndex == protoIndex;
                    bool removeRegion = filterRegion &&
                                        tree.position.x >= rx && tree.position.x <= rx + rw &&
                                        tree.position.z >= ry && tree.position.z <= ry + rh;

                    bool shouldRemove = (!filterProto && !filterRegion)  // remove all
                                     || (filterProto  && !filterRegion && removeProto)
                                     || (!filterProto && filterRegion  && removeRegion)
                                     || (filterProto  && filterRegion  && removeProto && removeRegion);

                    if (!shouldRemove) kept.Add(tree);
                }

                Undo.RecordObject(data, "Remove Terrain Trees");
                data.SetTreeInstances(kept.ToArray(), true);
                terrain.Flush();
                EditorUtility.SetDirty(data);

                int removed = before - kept.Count;
                return McpResponse.Success(
                    $"Removed {removed} tree(s) from terrain '{terrain.name}'. {kept.Count} remaining.",
                    new Dictionary<string, object>
                    {
                        ["removed"]   = removed,
                        ["remaining"] = kept.Count
                    });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to remove terrain trees: {ex.Message}");
            }
        }

        // ====================================================================
        // Handler: unity_list_terrain_trees
        // ====================================================================
        private static McpToolResult ListTerrainTrees(Dictionary<string, object> args)
        {
            try
            {
                var terrain = TerrainHelpers.FindTerrain(args);
                if (terrain == null)
                    return McpToolResult.Error("No Terrain found.");

                var data = terrain.terrainData;
                if (data == null)
                    return McpToolResult.Error($"Terrain '{terrain.name}' has no TerrainData.");

                var protos    = data.treePrototypes;
                var instances = data.treeInstances;

                // Count instances per prototype
                int[] counts = new int[protos.Length];
                foreach (var t in instances)
                    if (t.prototypeIndex >= 0 && t.prototypeIndex < counts.Length)
                        counts[t.prototypeIndex]++;

                var protoList = new List<object>();
                for (int i = 0; i < protos.Length; i++)
                {
                    string prefabName = protos[i].prefab != null ? protos[i].prefab.name : "(none)";
                    protoList.Add(new Dictionary<string, object>
                    {
                        ["index"]      = i,
                        ["prefab"]     = prefabName,
                        ["count"]      = counts[i],
                        ["bendFactor"] = protos[i].bendFactor,
                    });
                }

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["terrain"]        = terrain.name,
                    ["prototypeCount"] = protos.Length,
                    ["totalInstances"] = instances.Length,
                    ["prototypes"]     = protoList
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to list terrain trees: {ex.Message}");
            }
        }
    }
}
