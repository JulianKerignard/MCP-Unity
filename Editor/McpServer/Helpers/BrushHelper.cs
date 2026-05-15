using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Helpers
{
    /// <summary>
    /// Brush shape and falloff utilities for terrain sculpt/paint operations.
    /// Provides CPU-side brush weight computation equivalent to Unity's GPU brush system.
    ///
    /// Supported brushShape values:
    ///   "rect"     — uniform rectangle (legacy default)
    ///   "circle"   — hard-edge circle
    ///   "gaussian" — smooth gaussian falloff
    ///   (any other string) — treated as a texture brush name (see ResolveTextureBrush)
    ///
    /// Texture brushes:
    ///   Pass brushName = "SmoothHeight" (or any partial Texture2D name) to use a PNG brush.
    ///   The brush is found by scanning all Texture2D assets in the project and matching
    ///   by name (case-insensitive, partial match). The red channel of the texture is
    ///   sampled and used as the per-pixel weight, resampled to the brush bounding box.
    /// </summary>
    public static class BrushHelper
    {
        // ====================================================================
        // Texture brush cache (cleared on domain reload via static ctor)
        // ====================================================================

        /// <summary>
        /// Maps lower-case brush name → loaded Texture2D pixels (read-only copy).
        /// Populated lazily on first use of a given brush name.
        /// FIX-#125: capped to avoid unbounded growth across long Editor sessions.
        /// </summary>
        private const int MaxCachedBrushes = 128;
        private static readonly Dictionary<string, CachedBrush> _textureCache
            = new Dictionary<string, CachedBrush>();

        /// <summary>
        /// FIX-#125: enforce a soft cap on _textureCache. When the cache is full and a new
        /// brush is being added, clear the oldest half. Brush textures are inexpensive to
        /// reload (just a Texture2D import) so the trade-off favors bounded memory.
        /// </summary>
        private static void EnforceCacheBound()
        {
            if (_textureCache.Count < MaxCachedBrushes) return;
            int drop = _textureCache.Count / 2;
            var keys = new List<string>(_textureCache.Keys);
            for (int i = 0; i < drop && i < keys.Count; i++)
            {
                _textureCache.Remove(keys[i]);
            }
        }

        private struct CachedBrush
        {
            public float[] pixels; // row-major [y * width + x], values 0-1
            public int width;
            public int height;
            public string resolvedName; // full asset name for reporting
        }

        // ====================================================================
        // Public API
        // ====================================================================

        /// <summary>
        /// Compute the brush weight for a single pixel.
        /// Returns 0.0–1.0. Outside the brush = 0.
        ///
        /// If brushName is non-null and non-empty, uses a texture brush (brushShape is ignored).
        /// Otherwise uses brushShape ("rect" / "circle" / "gaussian").
        /// </summary>
        public static float GetBrushWeight(
            int px, int py,
            float centerX, float centerY,
            float radiusX, float radiusY,
            float rotationRad,
            string brushShape,
            float falloff,
            string brushName = null)
        {
            // Texture brush path
            if (!string.IsNullOrEmpty(brushName))
            {
                if (_textureCache.TryGetValue(brushName.ToLowerInvariant(), out var cached))
                    return SampleTextureBrush(cached, px, py, centerX, centerY, radiusX, radiusY, rotationRad);
                // Not loaded yet — fall through to math brush (caller should pre-load via TryLoadTextureBrush)
            }

            float dx = px - centerX;
            float dy = py - centerY;

            // Apply inverse rotation to get coordinates in brush-local space
            if (Mathf.Abs(rotationRad) > 0.001f)
            {
                float cos = Mathf.Cos(-rotationRad);
                float sin = Mathf.Sin(-rotationRad);
                float rx = dx * cos - dy * sin;
                float ry = dx * sin + dy * cos;
                dx = rx;
                dy = ry;
            }

            // Normalize to brush UV space [0..1 at edge]
            float nx = (radiusX > 0f) ? dx / radiusX : 0f;
            float ny = (radiusY > 0f) ? dy / radiusY : 0f;

            switch (brushShape?.ToLowerInvariant())
            {
                case "circle":
                    return CircleWeight(nx, ny);

                case "gaussian":
                    return GaussianWeight(nx, ny, falloff);

                default: // "rect" and anything else
                    return RectWeight(nx, ny);
            }
        }

        /// <summary>
        /// Try to load a texture brush by name into the cache.
        /// Scans all Texture2D assets in the project (Assets/ + Packages/).
        /// Match is case-insensitive; partial name match is accepted.
        /// Returns the resolved full asset name, or null if not found.
        /// </summary>
        public static string TryLoadTextureBrush(string brushName)
        {
            if (string.IsNullOrEmpty(brushName)) return null;
            string key = brushName.ToLowerInvariant();
            if (_textureCache.ContainsKey(key)) return _textureCache[key].resolvedName;

            // Search project assets
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {brushName}");

            Texture2D found = null;
            string foundName = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (assetName.IndexOf(brushName, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                found = tex;
                foundName = assetName;
                break; // First match wins
            }

            // 2. Fall back to built-in Unity terrain brushes
            if (found == null)
            {
                foreach (var (bName, bPath) in GetBuiltinBrushes())
                {
                    if (bName.IndexOf(brushName, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var tex = LoadBuiltinBrushTexture(bPath);
                    if (tex == null) continue;

                    var px = ReadTexturePixels(tex);
                    if (px == null) continue;

                    _textureCache[key] = new CachedBrush
                    {
                        pixels = px,
                        width  = tex.width,
                        height = tex.height,
                        resolvedName = bName
                    };
                    return bName;
                }
                return null;
            }

            // Make the texture readable temporarily if needed
            var pixels = ReadTexturePixels(found);
            if (pixels == null) return null;

            // FIX-#125: enforce soft cap before inserting.
            EnforceCacheBound();
            _textureCache[key] = new CachedBrush
            {
                pixels = pixels,
                width  = found.width,
                height = found.height,
                resolvedName = foundName
            };
            return foundName;
        }

        /// <summary>
        /// List all available brush textures: Unity built-in brushes first, then project Texture2D assets.
        /// Returns list of (name, path) pairs. path is "builtin-brush:N" or "builtin-png:N" for built-in brushes.
        /// Optionally filter by a search term (case-insensitive, partial match).
        /// </summary>
        public static List<(string name, string path)> ListAvailableBrushes(string filter = null)
        {
            var result = new List<(string, string)>();

            // 1. Built-in Unity terrain brushes (via reflection on TerrainInspectorUtil)
            foreach (var entry in GetBuiltinBrushes())
            {
                if (filter == null || entry.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Add(entry);
            }

            // 2. Project Texture2D assets (Assets/ and Packages/)
            string query = string.IsNullOrEmpty(filter) ? "t:Texture2D" : $"t:Texture2D {filter}";
            string[] guids = AssetDatabase.FindAssets(query);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/")) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (filter != null && name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add((name, path));
            }

            return result;
        }

        /// <summary>
        /// Compute brush bounding box (pixel coords, clamped to resolution) from world-space parameters.
        /// Falls back to full-terrain rect if brushCenter/brushSize are not provided.
        /// </summary>
        public static void GetBrushPixelRect(
            Dictionary<string, object> args,
            TerrainData terrainData,
            int resolution,
            out int xBase, out int yBase, out int width, out int height,
            out float centerPixX, out float centerPixY, out float radiusPix)
        {
            bool hasBrushCenter = ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "brushCenter", out var centerDict);
            bool hasBrushSize   = ArgumentParser.HasKey(args, "brushSize");

            if (hasBrushSize)
            {
                float brushSizeWorld = Mathf.Max(1f, ArgumentParser.GetFloat(args, "brushSize", 50f));

                // Center in normalized terrain UV (default: 0.5, 0.5)
                float cnx = hasBrushCenter ? ArgumentParser.GetFloat(centerDict, "x", 0.5f) : 0.5f;
                float cnz = hasBrushCenter ? ArgumentParser.GetFloat(centerDict, "z", 0.5f) : 0.5f;

                // Convert world-space brush size to pixels
                float pixelsPerWorldUnitX = (resolution - 1f) / terrainData.size.x;
                float pixelsPerWorldUnitZ = (resolution - 1f) / terrainData.size.z;

                float radiusPixX = brushSizeWorld * 0.5f * pixelsPerWorldUnitX;
                float radiusPixZ = brushSizeWorld * 0.5f * pixelsPerWorldUnitZ;
                radiusPix = (radiusPixX + radiusPixZ) * 0.5f;

                centerPixX = cnx * (resolution - 1f);
                centerPixY = cnz * (resolution - 1f);

                int margin = 1;
                xBase  = Mathf.Clamp(Mathf.FloorToInt(centerPixX - radiusPixX) - margin, 0, resolution - 1);
                yBase  = Mathf.Clamp(Mathf.FloorToInt(centerPixY - radiusPixZ) - margin, 0, resolution - 1);
                int x2 = Mathf.Clamp(Mathf.CeilToInt(centerPixX  + radiusPixX) + margin, 0, resolution - 1);
                int y2 = Mathf.Clamp(Mathf.CeilToInt(centerPixY  + radiusPixZ) + margin, 0, resolution - 1);
                width  = Mathf.Max(1, x2 - xBase);
                height = Mathf.Max(1, y2 - yBase);
            }
            else if (hasBrushCenter && ArgumentParser.TryGetValue<Dictionary<string, object>>(args, "region", out _))
            {
                TerrainHelpers.NormalizedRegionToPixels(args, resolution, out xBase, out yBase, out width, out height);
                float cnx = ArgumentParser.GetFloat(centerDict, "x", 0.5f);
                float cnz = ArgumentParser.GetFloat(centerDict, "z", 0.5f);
                centerPixX = cnx * (resolution - 1f);
                centerPixY = cnz * (resolution - 1f);
                radiusPix  = Mathf.Min(width, height) * 0.5f;
            }
            else
            {
                TerrainHelpers.NormalizedRegionToPixels(args, resolution, out xBase, out yBase, out width, out height);
                centerPixX = xBase + width  * 0.5f;
                centerPixY = yBase + height * 0.5f;
                radiusPix  = Mathf.Min(width, height) * 0.5f;
            }
        }

        /// <summary>
        /// Returns true if brush parameters are present — triggers the brush-weighted path.
        /// </summary>
        public static bool IsBrushActive(Dictionary<string, object> args)
        {
            if (ArgumentParser.HasKey(args, "brushSize"))   return true;
            if (ArgumentParser.HasKey(args, "brushCenter")) return true;
            if (!string.IsNullOrEmpty(ArgumentParser.GetString(args, "brushName", ""))) return true;
            string shape = ArgumentParser.GetString(args, "brushShape", "rect");
            return shape != null && shape.ToLowerInvariant() != "rect";
        }

        // ====================================================================
        // Texture brush sampling
        // ====================================================================

        private static float SampleTextureBrush(
            CachedBrush brush,
            int px, int py,
            float centerX, float centerY,
            float radiusX, float radiusY,
            float rotationRad)
        {
            float dx = px - centerX;
            float dy = py - centerY;

            if (Mathf.Abs(rotationRad) > 0.001f)
            {
                float cos = Mathf.Cos(-rotationRad);
                float sin = Mathf.Sin(-rotationRad);
                float rx = dx * cos - dy * sin;
                float ry = dx * sin + dy * cos;
                dx = rx;
                dy = ry;
            }

            // Map to brush UV [-1..1]
            float nx = (radiusX > 0f) ? dx / radiusX : 0f;
            float ny = (radiusY > 0f) ? dy / radiusY : 0f;

            // Outside brush circle — zero weight
            if (nx * nx + ny * ny > 1f) return 0f;

            // Remap to texture UV [0..1]
            float u = (nx + 1f) * 0.5f;
            float v = (ny + 1f) * 0.5f;

            // Bilinear sample from cached pixels
            float tx = u * (brush.width  - 1);
            float ty = v * (brush.height - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(tx), 0, brush.width  - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(ty), 0, brush.height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, brush.width  - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, brush.height - 1);
            float fx = tx - x0;
            float fy = ty - y0;

            float p00 = brush.pixels[y0 * brush.width + x0];
            float p10 = brush.pixels[y0 * brush.width + x1];
            float p01 = brush.pixels[y1 * brush.width + x0];
            float p11 = brush.pixels[y1 * brush.width + x1];

            return Mathf.Lerp(Mathf.Lerp(p00, p10, fx), Mathf.Lerp(p01, p11, fx), fy);
        }

        /// <summary>
        /// Read texture pixels as a float array (red channel, 0-1).
        /// Handles non-readable textures by creating a temporary RenderTexture copy.
        /// </summary>
        private static float[] ReadTexturePixels(Texture2D tex)
        {
            try
            {
                // Try direct read first (works if texture is marked Read/Write)
                Color[] colors;
                try
                {
                    colors = tex.GetPixels();
                }
                catch
                {
                    // Texture not readable — blit to a RenderTexture and read back
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    colors = readable.GetPixels();
                    Object.DestroyImmediate(readable);
                }

                float[] result = new float[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                    result[i] = colors[i].r; // use red channel (grayscale brushes = r=g=b)
                return result;
            }
            catch (System.Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[BrushHelper] Failed to read texture pixels: {ex.Message}");
                return null;
            }
        }

        // ====================================================================
        // Built-in Unity terrain brush enumeration
        // Replicates the same loading logic as UnityEditor.BrushList.LoadBrushes():
        //   1. EditorGUIUtility.Load(brushesPath + "builtin_brush_N.brush") — .brush ScriptableObjects
        //   2. EditorGUIUtility.FindTexture("brush_N.png")                  — legacy PNG fallback
        // ====================================================================

        private static List<(string name, string path)> _builtinBrushList = null;

        // Reflection handle for Brush.m_Mask (Texture2D mask field on Brush ScriptableObject)
        private static FieldInfo _brushMaskField = null;
        private static bool      _brushMaskFieldInit = false;

        private static FieldInfo GetBrushMaskField(System.Type brushType)
        {
            if (_brushMaskFieldInit) return _brushMaskField;
            _brushMaskFieldInit = true;
            _brushMaskField = brushType?.GetField("m_Mask", BindingFlags.NonPublic | BindingFlags.Instance);
            return _brushMaskField;
        }

        /// <summary>
        /// Returns all Unity built-in terrain brushes.
        /// Uses the same two-phase loading logic as Unity's internal BrushList.LoadBrushes().
        /// Cached after first call.
        /// </summary>
        private static List<(string name, string path)> GetBuiltinBrushes()
        {
            if (_builtinBrushList != null) return _builtinBrushList;
            _builtinBrushList = new List<(string, string)>();

            try
            {
                // Phase 1: Load .brush ScriptableObjects via EditorResources.brushesPath
                // e.g. "builtin_brush_1.brush", "builtin_brush_2.brush", ...
                string brushesPath = GetEditorBrushesPath();
                if (!string.IsNullOrEmpty(brushesPath))
                {
                    for (int i = 1; ; i++)
                    {
                        string assetPath = $"{brushesPath}builtin_brush_{i}.brush";
                        var obj = EditorGUIUtility.Load(assetPath);
                        if (obj == null) break;

                        string brushName = obj.name;
                        _builtinBrushList.Add((brushName, $"builtin-brush:{i}"));
                    }
                }

                // Phase 2: Legacy PNG brushes via EditorGUIUtility.FindTexture("brush_N.png")
                // Used when .brush assets are not available
                if (_builtinBrushList.Count == 0)
                {
                    for (int i = 0; ; i++)
                    {
                        var tex = EditorGUIUtility.FindTexture($"brush_{i}.png");
                        if (tex == null) break;
                        _builtinBrushList.Add((tex.name, $"builtin-png:{i}"));
                    }
                }
            }
            catch (System.Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[BrushHelper] GetBuiltinBrushes failed: {ex.Message}");
            }

            return _builtinBrushList;
        }

        /// <summary>
        /// Load the Texture2D mask for a built-in brush (encoded as "builtin-brush:N" or "builtin-png:N").
        /// </summary>
        private static Texture2D LoadBuiltinBrushTexture(string builtinPath)
        {
            try
            {
                if (builtinPath.StartsWith("builtin-brush:"))
                {
                    if (!int.TryParse(builtinPath.Substring(14), out int idx)) return null;

                    string brushesPath = GetEditorBrushesPath();
                    if (string.IsNullOrEmpty(brushesPath)) return null;

                    var obj = EditorGUIUtility.Load($"{brushesPath}builtin_brush_{idx}.brush");
                    if (obj == null) return null;

                    // Get the m_Mask texture via reflection (same field Unity's BrushList reads)
                    var maskField = GetBrushMaskField(obj.GetType());
                    if (maskField != null)
                    {
                        var mask = maskField.GetValue(obj) as Texture2D;
                        if (mask != null) return mask;
                    }

                    // Fallback: if m_Mask is null, try m_Texture field
                    var texField = obj.GetType().GetField("m_Texture", BindingFlags.NonPublic | BindingFlags.Instance);
                    return texField?.GetValue(obj) as Texture2D;
                }
                else if (builtinPath.StartsWith("builtin-png:"))
                {
                    if (!int.TryParse(builtinPath.Substring(12), out int idx)) return null;
                    return EditorGUIUtility.FindTexture($"brush_{idx}.png");
                }
            }
            catch (System.Exception ex)
            {
                McpUnity.Editor.McpDebug.LogWarning($"[BrushHelper] LoadBuiltinBrushTexture failed: {ex.Message}");
            }
            return null;
        }

        private static string _cachedBrushesPath = null;
        private static bool   _brushesPathInit   = false;

        private static string GetEditorBrushesPath()
        {
            if (_brushesPathInit) return _cachedBrushesPath;
            _brushesPathInit = true;
            try
            {
                // UnityEditor.Experimental.EditorResources.brushesPath (internal property)
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Experimental.EditorResources");
                var prop = type?.GetProperty("brushesPath", BindingFlags.Public | BindingFlags.Static)
                        ?? type?.GetProperty("brushesPath", BindingFlags.NonPublic | BindingFlags.Static);
                _cachedBrushesPath = prop?.GetValue(null) as string;
            }
            catch (System.Exception) { /* EditorResources.brushesPath not available in this Unity version */ }
            return _cachedBrushesPath;
        }

        // ====================================================================
        // Math brush weight functions
        // ====================================================================

        private static float RectWeight(float nx, float ny)
            => (Mathf.Abs(nx) <= 1f && Mathf.Abs(ny) <= 1f) ? 1f : 0f;

        private static float CircleWeight(float nx, float ny)
        {
            float dist = Mathf.Sqrt(nx * nx + ny * ny);
            return dist <= 1f ? 1f : 0f;
        }

        // falloff = 0 → wide flat mound   falloff = 1 → tight sharp peak
        private static float GaussianWeight(float nx, float ny, float falloff)
        {
            float dist2 = nx * nx + ny * ny;
            if (dist2 > 1f) return 0f;
            float sigma  = Mathf.Lerp(0.8f, 0.15f, Mathf.Clamp01(falloff));
            float sigma2 = sigma * sigma;
            return Mathf.Clamp01(Mathf.Exp(-dist2 / (2f * sigma2)));
        }
    }
}
