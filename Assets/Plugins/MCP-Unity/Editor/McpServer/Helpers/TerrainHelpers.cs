using System.Collections.Generic;
using UnityEngine;

namespace McpUnity.Helpers
{
    /// <summary>
    /// Helper methods for Terrain operations in MCP Unity Server.
    /// Provides terrain lookup, coordinate conversion, and serialization utilities.
    /// </summary>
    public static class TerrainHelpers
    {
        /// <summary>
        /// Find a Terrain component by gameObjectPath argument, or return the first active Terrain.
        /// </summary>
        public static Terrain FindTerrain(Dictionary<string, object> args)
        {
            var path = ArgumentParser.GetString(args, "gameObjectPath", null);
            if (!string.IsNullOrEmpty(path))
            {
                var go = GameObjectHelpers.FindGameObject(path);
                if (go == null) return null;
                return go.GetComponent<Terrain>();
            }

            // Auto-find the first active terrain
            return Terrain.activeTerrain;
        }

        /// <summary>
        /// Convert a normalized region (0-1) to heightmap pixel coordinates.
        /// </summary>
        public static void NormalizedRegionToPixels(Dictionary<string, object> args, int resolution,
            out int xBase, out int yBase, out int width, out int height)
        {
            if (ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "region", out var region))
            {
                float rx = ArgumentParser.GetFloat(region, "x", 0f);
                float ry = ArgumentParser.GetFloat(region, "y", 0f);
                float rw = ArgumentParser.GetFloat(region, "width", 1f);
                float rh = ArgumentParser.GetFloat(region, "height", 1f);

                xBase = Mathf.Clamp(Mathf.RoundToInt(rx * (resolution - 1)), 0, resolution - 1);
                yBase = Mathf.Clamp(Mathf.RoundToInt(ry * (resolution - 1)), 0, resolution - 1);
                width = Mathf.Clamp(Mathf.RoundToInt(rw * (resolution - 1)), 1, resolution - xBase);
                height = Mathf.Clamp(Mathf.RoundToInt(rh * (resolution - 1)), 1, resolution - yBase);
            }
            else
            {
                xBase = 0;
                yBase = 0;
                width = resolution;
                height = resolution;
            }
        }

        /// <summary>
        /// Serialize comprehensive terrain info into a dictionary.
        /// </summary>
        public static Dictionary<string, object> SerializeTerrainInfo(Terrain terrain)
        {
            var data = terrain.terrainData;
            var info = new Dictionary<string, object>
            {
                ["gameObjectPath"] = GameObjectHelpers.GetGameObjectPath(terrain.gameObject),
                ["position"] = new Dictionary<string, object>
                {
                    ["x"] = terrain.transform.position.x,
                    ["y"] = terrain.transform.position.y,
                    ["z"] = terrain.transform.position.z
                },
                ["size"] = new Dictionary<string, object>
                {
                    ["x"] = data.size.x,
                    ["y"] = data.size.y,
                    ["z"] = data.size.z
                },
                ["heightmapResolution"] = data.heightmapResolution,
                ["alphamapResolution"] = data.alphamapResolution,
                ["alphamapLayers"] = data.alphamapLayers,
                ["baseMapResolution"] = data.baseMapResolution,
                ["detailResolution"] = data.detailResolution,
                ["treeInstanceCount"] = data.treeInstanceCount,
                ["treePrototypeCount"] = data.treePrototypes.Length,
                ["detailPrototypeCount"] = data.detailPrototypes.Length
            };

            // Terrain layers
            var layers = new List<Dictionary<string, object>>();
            if (data.terrainLayers != null)
            {
                for (int i = 0; i < data.terrainLayers.Length; i++)
                {
                    var layer = data.terrainLayers[i];
                    if (layer == null) continue;
                    layers.Add(SerializeTerrainLayer(layer, i));
                }
            }
            info["terrainLayers"] = layers;

            // Tree prototypes
            var treeProtos = new List<Dictionary<string, object>>();
            for (int i = 0; i < data.treePrototypes.Length; i++)
            {
                var proto = data.treePrototypes[i];
                treeProtos.Add(new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["prefab"] = proto.prefab != null ? proto.prefab.name : "null",
                    ["bendFactor"] = proto.bendFactor
                });
            }
            info["treePrototypes"] = treeProtos;

            // Neighbors
            info["neighbors"] = new Dictionary<string, object>
            {
                ["left"] = terrain.leftNeighbor != null ? GameObjectHelpers.GetGameObjectPath(terrain.leftNeighbor.gameObject) : null,
                ["right"] = terrain.rightNeighbor != null ? GameObjectHelpers.GetGameObjectPath(terrain.rightNeighbor.gameObject) : null,
                ["top"] = terrain.topNeighbor != null ? GameObjectHelpers.GetGameObjectPath(terrain.topNeighbor.gameObject) : null,
                ["bottom"] = terrain.bottomNeighbor != null ? GameObjectHelpers.GetGameObjectPath(terrain.bottomNeighbor.gameObject) : null
            };

            // Rendering settings
            info["rendering"] = new Dictionary<string, object>
            {
                ["heightmapPixelError"] = terrain.heightmapPixelError,
                ["basemapDistance"] = terrain.basemapDistance,
                ["detailObjectDistance"] = terrain.detailObjectDistance,
                ["detailObjectDensity"] = terrain.detailObjectDensity,
                ["treeDistance"] = terrain.treeDistance,
                ["treeBillboardDistance"] = terrain.treeBillboardDistance,
                ["drawHeightmap"] = terrain.drawHeightmap,
                ["drawTreesAndFoliage"] = terrain.drawTreesAndFoliage,
                ["drawInstanced"] = terrain.drawInstanced,
                ["allowAutoConnect"] = terrain.allowAutoConnect
            };

            return info;
        }

        /// <summary>
        /// Serialize a single TerrainLayer into a dictionary.
        /// </summary>
        public static Dictionary<string, object> SerializeTerrainLayer(TerrainLayer layer, int index)
        {
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = layer.name,
                ["diffuseTexture"] = layer.diffuseTexture != null ? layer.diffuseTexture.name : null,
                ["normalMapTexture"] = layer.normalMapTexture != null ? layer.normalMapTexture.name : null,
                ["tileSize"] = new Dictionary<string, object> { ["x"] = layer.tileSize.x, ["y"] = layer.tileSize.y },
                ["tileOffset"] = new Dictionary<string, object> { ["x"] = layer.tileOffset.x, ["y"] = layer.tileOffset.y },
                ["metallic"] = layer.metallic,
                ["smoothness"] = layer.smoothness
            };
        }
    }
}
