// MCP Unity — Terrain Builder Sample
// TerrainBuilderExample.cs
//
// Demonstrates how to build a complete terrain via C# Editor scripting
// that mirrors what the MCP tools do internally.
// Useful for understanding the data flow or for extending terrain tools.
//
// Run via: Tools > MCP Unity Samples > Build Example Terrain

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace McpUnity.Samples.TerrainBuilder
{
    public static class TerrainBuilderExample
    {
        private const string TerrainName = "SampleLandscape";
        private const int Resolution = 513;
        private const float Width = 500f;
        private const float Height = 100f;
        private const float Length = 500f;

        [MenuItem("Tools/MCP Unity Samples/Build Example Terrain")]
        public static void BuildExampleTerrain()
        {
            // Step 1: Create terrain
            Terrain terrain = CreateTerrain();
            if (terrain == null) return;

            // Step 2: Sculpt a simple hill
            SculptHill(terrain);

            // Step 3: Smooth
            SmoothHeightmap(terrain, passes: 2);

            Debug.Log($"[MCP Unity Sample] Terrain '{TerrainName}' created successfully.\n" +
                      $"Use MCP tools to add textures, trees, and details via AI prompts.");

            Selection.activeGameObject = terrain.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static Terrain CreateTerrain()
        {
            // Remove existing sample terrain
            GameObject existing = GameObject.Find(TerrainName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // Create TerrainData
            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = Resolution,
                size = new Vector3(Width, Height, Length)
            };

            // Create GameObject
            GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrainGO.name = TerrainName;
            terrainGO.transform.position = new Vector3(-Width / 2f, 0f, -Length / 2f);

            Undo.RegisterCreatedObjectUndo(terrainGO, $"Create {TerrainName}");

            Debug.Log($"[MCP Unity Sample] Created terrain '{TerrainName}' ({Resolution}x{Resolution} heightmap).");
            return terrainGO.GetComponent<Terrain>();
        }

        private static void SculptHill(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);

            // Gaussian hill centered at (0.5, 0.5) normalized
            float cx = 0.5f;
            float cz = 0.5f;
            float sigma = 0.15f; // spread
            float peakHeight = 0.6f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float dx = nx - cx;
                    float dz = nz - cz;
                    float distSq = dx * dx + dz * dz;
                    float gaussian = Mathf.Exp(-distSq / (2f * sigma * sigma));
                    heights[z, x] = Mathf.Max(heights[z, x], gaussian * peakHeight);
                }
            }

            Undo.RegisterCompleteObjectUndo(data, "Sculpt Hill");
            data.SetHeights(0, 0, heights);
            Debug.Log("[MCP Unity Sample] Sculpted gaussian hill.");
        }

        private static void SmoothHeightmap(Terrain terrain, int passes)
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;

            for (int pass = 0; pass < passes; pass++)
            {
                float[,] heights = data.GetHeights(0, 0, res, res);
                float[,] smoothed = new float[res, res];

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = Mathf.Clamp(x + dx, 0, res - 1);
                                int nz = Mathf.Clamp(z + dz, 0, res - 1);
                                sum += heights[nz, nx];
                                count++;
                            }
                        }
                        smoothed[z, x] = sum / count;
                    }
                }

                Undo.RegisterCompleteObjectUndo(data, $"Smooth Pass {pass + 1}");
                data.SetHeights(0, 0, smoothed);
            }

            Debug.Log($"[MCP Unity Sample] Applied {passes} smooth pass(es).");
        }
    }
}
#endif
