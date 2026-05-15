using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpUnity.Chat
{
    /// <summary>
    /// Background context indexer that maintains compact summaries of the Unity project.
    /// Subscribes to editor callbacks to auto-rebuild when hierarchy or project changes.
    /// Used to inject rich context into the Chat system prompt without tool calls.
    /// </summary>
    [InitializeOnLoad]
    public static class McpContextIndex
    {
        // ====================================================================
        // Cached summaries
        // ====================================================================

        private static string _hierarchySummary;
        private static string _assetSummary;
        private static string _scriptSummary;
        private static bool _hierarchyDirty = true;
        private static bool _assetsDirty = true;
        private static bool _scriptsDirty = true;

        // Debounce
        private static double _hierarchyDirtyTime;
        private static double _assetsDirtyTime;
        private static double _scriptsDirtyTime;
        private const double DebounceDelay = 1.0; // seconds

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>True once at least one rebuild has completed.</summary>
        public static bool IsReady => _hierarchySummary != null && _assetSummary != null;

        /// <summary>
        /// Returns the full context snapshot for system prompt injection.
        /// Typically 500-1000 tokens covering hierarchy, assets, and scripts.
        /// </summary>
        public static string GetContextSnapshot()
        {
            ProcessDirtyFlags();

            var sb = new StringBuilder();

            if (_hierarchySummary != null)
            {
                sb.Append(_hierarchySummary);
                sb.AppendLine();
            }

            if (_assetSummary != null)
            {
                sb.Append(_assetSummary);
                sb.AppendLine();
            }

            if (_scriptSummary != null)
            {
                sb.Append(_scriptSummary);
            }

            return sb.ToString();
        }

        /// <summary>Returns only the hierarchy summary.</summary>
        public static string GetHierarchySummary()
        {
            ProcessDirtyFlags();
            return _hierarchySummary ?? "(indexing...)";
        }

        /// <summary>Returns only the asset summary.</summary>
        public static string GetAssetSummary()
        {
            ProcessDirtyFlags();
            return _assetSummary ?? "(indexing...)";
        }

        /// <summary>Returns only the script/code summary.</summary>
        public static string GetScriptSummary()
        {
            ProcessDirtyFlags();
            return _scriptSummary ?? "(indexing...)";
        }

        /// <summary>Force rebuild all summaries immediately.</summary>
        public static void ForceRefresh()
        {
            RebuildHierarchySummary();
            RebuildAssetSummary();
            RebuildScriptSummary();
            _hierarchyDirty = false;
            _assetsDirty = false;
            _scriptsDirty = false;
        }

        // ====================================================================
        // Initialization & Callbacks
        // ====================================================================

        static McpContextIndex()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.update += OnEditorUpdate;

            // Initial build (deferred to first update to avoid domain reload issues)
            _hierarchyDirty = true;
            _assetsDirty = true;
            _scriptsDirty = true;
            _hierarchyDirtyTime = EditorApplication.timeSinceStartup;
            _assetsDirtyTime = EditorApplication.timeSinceStartup;
        }

        private static void OnHierarchyChanged()
        {
            _hierarchyDirty = true;
            _hierarchyDirtyTime = EditorApplication.timeSinceStartup;
        }

        private static void OnProjectChanged()
        {
            _assetsDirty = true;
            _scriptsDirty = true;
            double now = EditorApplication.timeSinceStartup;
            _assetsDirtyTime = now;
            _scriptsDirtyTime = now;
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            _hierarchyDirty = true;
            _hierarchyDirtyTime = EditorApplication.timeSinceStartup;
        }

        private static void OnEditorUpdate()
        {
            // PERF-#332: fast-path early return. EditorApplication.update fires every frame,
            // so avoid even touching timeSinceStartup when nothing is pending.
            if (!_hierarchyDirty && !_assetsDirty && !_scriptsDirty) return;
            ProcessDirtyFlags();
        }

        private static void ProcessDirtyFlags()
        {
            double now = EditorApplication.timeSinceStartup;

            if (_hierarchyDirty && (now - _hierarchyDirtyTime) >= DebounceDelay)
            {
                _hierarchyDirty = false;
                RebuildHierarchySummary();
            }

            if (_assetsDirty && (now - _assetsDirtyTime) >= DebounceDelay)
            {
                _assetsDirty = false;
                RebuildAssetSummary();
            }

            if (_scriptsDirty && (now - _scriptsDirtyTime) >= DebounceDelay)
            {
                _scriptsDirty = false;
                RebuildScriptSummary();
            }
        }

        // ====================================================================
        // Hierarchy Summary Builder
        // ====================================================================

        private static void RebuildHierarchySummary()
        {
            var sb = new StringBuilder();

            // Multi-scene support
            int sceneCount = SceneManager.sceneCount;
            var activeScene = SceneManager.GetActiveScene();

            if (sceneCount > 1)
            {
                sb.Append("### Scenes loaded: ");
                for (int s = 0; s < sceneCount; s++)
                {
                    var sc = SceneManager.GetSceneAt(s);
                    if (s > 0) sb.Append(", ");
                    sb.Append(sc.name);
                    if (sc == activeScene) sb.Append(" (active)");
                    if (sc.isDirty) sb.Append("*");
                }
                sb.AppendLine();
            }
            else
            {
                sb.Append("### Scene: \"");
                sb.Append(activeScene.name);
                sb.Append("\"");
                if (activeScene.isDirty) sb.Append(" (unsaved changes)");
                sb.AppendLine();
            }

            // Compilation errors — critical context
            if (EditorUtility.scriptCompilationFailed)
                sb.AppendLine("!! COMPILATION ERRORS — some scripts may not work");

            // Gather root objects from all loaded scenes
            var rootObjects = new List<GameObject>();
            for (int s = 0; s < sceneCount; s++)
            {
                var sc = SceneManager.GetSceneAt(s);
                if (sc.isLoaded)
                    sc.GetRootGameObjects(rootObjects);
            }

            // Count totals
            int totalCount = 0;
            int customScriptCount = 0;
            CountGameObjects(rootObjects, ref totalCount, ref customScriptCount);

            sb.Append("Total: ");
            sb.Append(totalCount);
            sb.Append(" GameObjects, ");
            sb.Append(customScriptCount);
            sb.AppendLine(" with custom scripts");

            // Root objects summary with key components (depth 1-2)
            sb.AppendLine("Hierarchy:");
            int maxRoots = 20; // Cap to avoid huge output
            for (int i = 0; i < rootObjects.Count && i < maxRoots; i++)
            {
                var root = rootObjects[i];
                sb.Append("  ");
                sb.Append(root.name);
                if (!root.activeSelf) sb.Append(" [inactive]");

                // Key components on root
                var comps = GetKeyComponentNames(root);
                if (comps.Count > 0)
                {
                    sb.Append(" [");
                    for (int c = 0; c < comps.Count; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        sb.Append(comps[c]);
                    }
                    sb.Append("]");
                }

                int childCount = root.transform.childCount;
                if (childCount > 0)
                {
                    sb.Append(" (");
                    sb.Append(childCount);
                    sb.Append(" children)");
                }

                sb.AppendLine();

                // Show depth-1 children (compact)
                int maxChildren = 5;
                for (int j = 0; j < childCount && j < maxChildren; j++)
                {
                    var child = root.transform.GetChild(j).gameObject;
                    sb.Append("    ");
                    sb.Append(child.name);

                    var childComps = GetKeyComponentNames(child);
                    if (childComps.Count > 0)
                    {
                        sb.Append(" [");
                        for (int c = 0; c < childComps.Count; c++)
                        {
                            if (c > 0) sb.Append(", ");
                            sb.Append(childComps[c]);
                        }
                        sb.Append("]");
                    }

                    int grandChildCount = child.transform.childCount;
                    if (grandChildCount > 0)
                    {
                        sb.Append(" (");
                        sb.Append(grandChildCount);
                        sb.Append(")");
                    }

                    sb.AppendLine();
                }

                if (childCount > maxChildren)
                {
                    sb.Append("    ... +");
                    sb.Append(childCount - maxChildren);
                    sb.AppendLine(" more");
                }
            }

            if (rootObjects.Count > maxRoots)
            {
                sb.Append("  ... +");
                sb.Append(rootObjects.Count - maxRoots);
                sb.AppendLine(" more root objects");
            }

            _hierarchySummary = sb.ToString().TrimEnd();
        }

        private static void CountGameObjects(List<GameObject> roots, ref int total, ref int customScripts)
        {
            foreach (var root in roots)
            {
                CountRecursive(root.transform, ref total, ref customScripts);
            }
        }

        private static void CountRecursive(Transform t, ref int total, ref int customScripts)
        {
            total++;

            var components = t.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                string fullName = comp.GetType().FullName;
                if (fullName != null && !fullName.StartsWith("UnityEngine.") && !fullName.StartsWith("UnityEditor."))
                {
                    customScripts++;
                    break; // Only count the GameObject once
                }
            }

            for (int i = 0; i < t.childCount; i++)
            {
                CountRecursive(t.GetChild(i), ref total, ref customScripts);
            }
        }

        // Key component types (aligned with HierarchyHelpers.KeyComponentTypes)
        private static readonly HashSet<string> _keyComponentTypes = new HashSet<string>
        {
            "MeshRenderer", "SkinnedMeshRenderer", "SpriteRenderer", "Camera", "Light",
            "Rigidbody", "Rigidbody2D", "CharacterController",
            "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider",
            "Canvas", "Image", "TextMeshProUGUI", "Button",
            "Animator", "AudioSource", "NavMeshAgent",
            "Terrain", "ParticleSystem", "Volume"
        };

        private static List<string> GetKeyComponentNames(GameObject go)
        {
            var result = new List<string>();
            var components = go.GetComponents<Component>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                string fullName = comp.GetType().FullName;

                if (_keyComponentTypes.Contains(typeName))
                {
                    result.Add(typeName);
                }
                else if (fullName != null && !fullName.StartsWith("UnityEngine.") && !fullName.StartsWith("UnityEditor."))
                {
                    // Custom script — include it
                    result.Add(typeName);
                }
            }

            return result;
        }

        // ====================================================================
        // Asset Summary Builder
        // ====================================================================

        private static void RebuildAssetSummary()
        {
            var sb = new StringBuilder();

            sb.Append("### Project: \"");
            sb.Append(Application.productName);
            sb.Append("\" | Unity ");
            sb.Append(Application.unityVersion);

            // Detect render pipeline
            var rpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (rpAsset != null)
            {
                string rpName = rpAsset.GetType().Name;
                if (rpName.Contains("Universal") || rpName.Contains("URP"))
                    sb.Append(" | URP");
                else if (rpName.Contains("HD") || rpName.Contains("HDRP"))
                    sb.Append(" | HDRP");
                else
                    sb.Append(" | ").Append(rpName);
            }
            else
            {
                sb.Append(" | Built-in RP");
            }
            sb.AppendLine();

            // Count assets by type using AssetDatabase
            int scriptCount = AssetDatabase.FindAssets("t:Script", new[] { "Assets" }).Length;
            int prefabCount = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Length;
            int materialCount = AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Length;
            int textureCount = AssetDatabase.FindAssets("t:Texture", new[] { "Assets" }).Length;
            int sceneCount = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Length;
            int soCount = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }).Length;
            int audioCount = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }).Length;
            int modelCount = AssetDatabase.FindAssets("t:Model", new[] { "Assets" }).Length;
            int animCount = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }).Length;
            int shaderCount = AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }).Length;

            int total = scriptCount + prefabCount + materialCount + textureCount +
                        sceneCount + soCount + audioCount + modelCount + animCount + shaderCount;

            sb.Append("Assets: ~");
            sb.Append(total);
            sb.AppendLine(" indexed");

            sb.Append("  Scripts: ").Append(scriptCount);
            sb.Append(" | Prefabs: ").Append(prefabCount);
            sb.Append(" | Materials: ").Append(materialCount);
            sb.Append(" | Textures: ").Append(textureCount);
            sb.AppendLine();
            sb.Append("  Scenes: ").Append(sceneCount);
            sb.Append(" | SOs: ").Append(soCount);
            sb.Append(" | Audio: ").Append(audioCount);
            sb.Append(" | Models: ").Append(modelCount);
            if (animCount > 0) sb.Append(" | Anims: ").Append(animCount);
            if (shaderCount > 0) sb.Append(" | Shaders: ").Append(shaderCount);
            sb.AppendLine();

            // Build scenes
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length > 0)
            {
                sb.Append("Build scenes: ");
                int shown = 0;
                for (int i = 0; i < buildScenes.Length && shown < 8; i++)
                {
                    if (!buildScenes[i].enabled) continue;
                    if (shown > 0) sb.Append(", ");
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(buildScenes[i].path);
                    sb.Append(sceneName);
                    sb.Append(" (");
                    sb.Append(i);
                    sb.Append(")");
                    shown++;
                }
                if (shown == 0) sb.Append("(none enabled)");
                sb.AppendLine();
            }

            // Key folders (depth 1 under Assets/)
            sb.Append("Key folders: ");
            var topFolders = AssetDatabase.GetSubFolders("Assets");
            int folderShown = 0;
            for (int i = 0; i < topFolders.Length && folderShown < 10; i++)
            {
                string folderName = System.IO.Path.GetFileName(topFolders[i]);
                // Skip hidden/meta folders
                if (folderName.StartsWith(".") || folderName.StartsWith("_")) continue;

                if (folderShown > 0) sb.Append(", ");
                sb.Append(folderName);
                sb.Append("/");
                folderShown++;
            }
            sb.AppendLine();

            // Installed packages (from manifest.json)
            AppendInstalledPackages(sb);

            _assetSummary = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Reads Packages/manifest.json and appends key (non-Unity-module) packages.
        /// </summary>
        private static void AppendInstalledPackages(StringBuilder sb)
        {
            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!File.Exists(manifestPath)) return;

                string json = File.ReadAllText(manifestPath);
                // Simple parse: find "dependencies" block and extract package names
                var parsed = McpUnity.Server.JsonHelper.ParseJsonObject(json);
                if (parsed is Dictionary<string, object> root
                    && root.TryGetValue("dependencies", out var depsObj)
                    && depsObj is Dictionary<string, object> deps)
                {
                    var packages = new List<string>();
                    foreach (var kvp in deps)
                    {
                        // Skip Unity built-in modules (com.unity.modules.*)
                        if (kvp.Key.StartsWith("com.unity.modules.")) continue;

                        // Extract short name: "com.unity.inputsystem" → "Input System"
                        string shortName = kvp.Key;
                        if (shortName.StartsWith("com.unity."))
                            shortName = shortName.Substring("com.unity.".Length);
                        else if (shortName.StartsWith("com."))
                            shortName = shortName.Substring("com.".Length);

                        // Humanize: replace dots/hyphens, capitalize words
                        shortName = shortName.Replace('.', ' ').Replace('-', ' ');
                        if (shortName.Length > 0)
                            shortName = char.ToUpper(shortName[0]) + shortName.Substring(1);

                        packages.Add(shortName);
                    }

                    if (packages.Count > 0)
                    {
                        sb.Append("Packages (");
                        sb.Append(packages.Count);
                        sb.Append("): ");
                        int maxPkgs = 12;
                        for (int i = 0; i < packages.Count && i < maxPkgs; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(packages[i]);
                        }
                        if (packages.Count > maxPkgs)
                        {
                            sb.Append(", +");
                            sb.Append(packages.Count - maxPkgs);
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch
            {
                // Silently ignore — packages info is not critical
            }
        }

        // ====================================================================
        // Script / Code Summary Builder
        // ====================================================================

        private static void RebuildScriptSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("### Scripts Overview");

            // Find all C# scripts under Assets/
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });

            // Group by top-level folder under Assets/Scripts/ (or just Assets/ if no Scripts folder)
            var folderGroups = new Dictionary<string, List<string>>();
            var namespaces = new HashSet<string>();
            int totalScripts = 0;

            foreach (var guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == null || !path.EndsWith(".cs")) continue;

                // Skip scripts in _-prefixed folders (e.g. _bmad-output, _backup)
                if (IsInUnderscoreFolder(path)) continue;

                totalScripts++;

                // Get the MonoScript to extract class info
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript == null) continue;

                var scriptClass = monoScript.GetClass();
                if (scriptClass != null && !string.IsNullOrEmpty(scriptClass.Namespace))
                {
                    namespaces.Add(scriptClass.Namespace);
                }

                // Group by folder (relative to Assets/)
                string relativePath = path.Substring("Assets/".Length);
                int slashIdx = relativePath.IndexOf('/');
                string topFolder = slashIdx >= 0 ? relativePath.Substring(0, slashIdx) : "(root)";

                if (!folderGroups.ContainsKey(topFolder))
                    folderGroups[topFolder] = new List<string>();

                // Store class name if available, otherwise filename
                string className = scriptClass?.Name ?? System.IO.Path.GetFileNameWithoutExtension(path);
                folderGroups[topFolder].Add(className);
            }

            sb.Append("Total C# scripts: ");
            sb.AppendLine(totalScripts.ToString());

            // Namespaces
            if (namespaces.Count > 0)
            {
                sb.Append("Namespaces: ");
                int nsShown = 0;
                foreach (var ns in namespaces)
                {
                    if (nsShown >= 12) { sb.Append(", ..."); break; }
                    if (nsShown > 0) sb.Append(", ");
                    sb.Append(ns);
                    nsShown++;
                }
                sb.AppendLine();
            }

            // Per-folder class listing (compact)
            foreach (var kvp in folderGroups)
            {
                string folder = kvp.Key;
                var classes = kvp.Value;

                sb.Append("  ");
                sb.Append(folder);
                sb.Append("/ (");
                sb.Append(classes.Count);
                sb.Append("): ");

                int maxClasses = 8;
                for (int i = 0; i < classes.Count && i < maxClasses; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(classes[i]);
                }
                if (classes.Count > maxClasses)
                {
                    sb.Append(", +");
                    sb.Append(classes.Count - maxClasses);
                }
                sb.AppendLine();
            }

            _scriptSummary = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns true if the path contains a folder segment starting with '_'.
        /// E.g. "Assets/_bmad-output/docs/foo.cs" → true, "Assets/Scripts/foo.cs" → false.
        /// </summary>
        private static bool IsInUnderscoreFolder(string assetPath)
        {
            // Check each path segment between '/' separators
            int start = 0;
            for (int i = 0; i < assetPath.Length; i++)
            {
                if (assetPath[i] == '/')
                {
                    if (i > start && assetPath[start] == '_')
                        return true;
                    start = i + 1;
                }
            }
            return false;
        }
    }
}
