// MCP Unity — Batch Workflow Sample
// McpWorkflowRunner.cs
//
// Editor utility demonstrating common batch operations in C#
// that you can also accomplish via AI prompts using MCP tools.
//
// Menu: Tools > MCP Unity Samples > [operation]

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Samples.BatchWorkflow
{
    public static class McpWorkflowRunner
    {
        // ----------------------------------------------------------------
        // Scene Audit
        // ----------------------------------------------------------------

        [MenuItem("Tools/MCP Unity Samples/Audit — Find Renderers Without Colliders")]
        public static void AuditRenderersWithoutColliders()
        {
            var results = new List<string>();
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (var renderer in allRenderers)
            {
                if (renderer.GetComponent<Collider>() == null)
                {
                    results.Add($"  {renderer.gameObject.name} ({renderer.GetType().Name})");
                }
            }

            if (results.Count == 0)
            {
                Debug.Log("[MCP Sample] All Renderers have a Collider. ✓");
            }
            else
            {
                Debug.LogWarning($"[MCP Sample] {results.Count} Renderer(s) without Collider:\n" +
                                 string.Join("\n", results));
            }
        }

        [MenuItem("Tools/MCP Unity Samples/Audit — Find Missing Scripts")]
        public static void AuditMissingScripts()
        {
            var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int totalMissing = 0;

            foreach (var go in allGOs)
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missingCount > 0)
                {
                    Debug.LogWarning($"[MCP Sample] Missing script(s) on: {GetFullPath(go)} ({missingCount} missing)",
                                     go);
                    totalMissing += missingCount;
                }
            }

            if (totalMissing == 0)
                Debug.Log("[MCP Sample] No missing scripts found. ✓");
            else
                Debug.LogWarning($"[MCP Sample] Total missing scripts: {totalMissing}");
        }

        // ----------------------------------------------------------------
        // Spawn Circle Generator
        // ----------------------------------------------------------------

        [MenuItem("Tools/MCP Unity Samples/Generate — Spawn Circle (10 points, r=20)")]
        public static void GenerateSpawnCircle()
        {
            const int count = 10;
            const float radius = 20f;
            const float yPos = 0.1f;

            // Ensure tag exists
            EnsureTagExists("SpawnPoint");

            // Create root
            var root = new GameObject("SpawnSystem");
            Undo.RegisterCreatedObjectUndo(root, "Create SpawnSystem");

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                var pos = new Vector3(
                    Mathf.Sin(angle) * radius,
                    yPos,
                    Mathf.Cos(angle) * radius
                );

                var spawnGO = new GameObject($"Spawn_{(i + 1):D2}");
                // SEC-#441: register the created object FIRST so subsequent parenting,
                // positioning, tagging, and AddComponent are all captured by the same undo
                // group. Registering after the fact leaves the intermediate operations
                // outside the undo history.
                Undo.RegisterCreatedObjectUndo(spawnGO, $"Create Spawn_{i + 1:D2}");
                spawnGO.transform.SetParent(root.transform);
                spawnGO.transform.position = pos;
                spawnGO.tag = "SpawnPoint";

                var col = Undo.AddComponent<SphereCollider>(spawnGO);
                col.radius = 0.5f;
                col.isTrigger = true;
            }

            Debug.Log($"[MCP Sample] Created {count} spawn points in a circle of radius {radius}.");
            Selection.activeGameObject = root;
        }

        // ----------------------------------------------------------------
        // Level Layout
        // ----------------------------------------------------------------

        [MenuItem("Tools/MCP Unity Samples/Generate — Simple Level Layout")]
        public static void GenerateSimpleLevel()
        {
            var root = new GameObject("Level_01");
            Undo.RegisterCreatedObjectUndo(root, "Create Level_01");

            // Floor
            CreatePrimitive("Floor", PrimitiveType.Plane, Vector3.zero, new Vector3(2, 1, 2), root);

            // Walls
            float wallLen = 20f;
            float wallH = 2f;
            float wallThk = 0.2f;
            float wallDist = 10f;

            CreateBox("Wall_N", new Vector3(0, wallH / 2f, wallDist), new Vector3(wallLen, wallH, wallThk), root);
            CreateBox("Wall_S", new Vector3(0, wallH / 2f, -wallDist), new Vector3(wallLen, wallH, wallThk), root);
            CreateBox("Wall_E", new Vector3(wallDist, wallH / 2f, 0), new Vector3(wallThk, wallH, wallLen), root);
            CreateBox("Wall_W", new Vector3(-wallDist, wallH / 2f, 0), new Vector3(wallThk, wallH, wallLen), root);

            // Obstacles
            Vector3[] obstaclePositions = {
                new Vector3(3, 0.5f, 3), new Vector3(-3, 0.5f, 3),
                new Vector3(3, 0.5f, -3), new Vector3(-3, 0.5f, -3),
                new Vector3(6, 0.5f, 0), new Vector3(-6, 0.5f, 0),
                new Vector3(0, 0.5f, 6), new Vector3(0, 0.5f, -6),
            };

            for (int i = 0; i < obstaclePositions.Length; i++)
            {
                CreateBox($"Obstacle_{(i + 1):D2}", obstaclePositions[i], Vector3.one, root);
            }

            Debug.Log("[MCP Sample] Simple level layout created. ✓");
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 pos,
                                                   Vector3 scale, GameObject parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, GameObject parent)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, pos, scale, parent);
        }

        private static string GetFullPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static void EnsureTagExists(string tag)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            }

            int idx = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(idx);
            tagsProp.GetArrayElementAtIndex(idx).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif
