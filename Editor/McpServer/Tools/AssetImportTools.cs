using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using McpUnity.Protocol;
using McpUnity.Helpers;

namespace McpUnity.Server
{
    /// <summary>
    /// Partial class containing Asset Import Settings tools
    /// Tools: get_import_settings, set_import_settings
    /// </summary>
    public partial class McpUnityServer
    {
        #region Asset Import Tool Registrations

        static partial void RegisterAssetImportTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_import_settings",
                description = "Get import settings for a texture, audio clip, or model asset",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Asset path relative to project (e.g., 'Assets/Textures/Hero.png')"
                        }
                    },
                    required = new List<string> { "assetPath" }
                }
            }, GetImportSettings);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_import_settings",
                description = "Set import settings for a texture, audio clip, or model asset and reimport it",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Asset path relative to project (e.g., 'Assets/Textures/Hero.png')"
                        },
                        ["settings"] = new McpPropertySchema
                        {
                            type = "object",
                            description = "Import settings to apply. Texture: {textureType, maxTextureSize, compression, mipmaps, readable}. Audio: {loadType, compressionFormat, quality, preloadAudioData}. Model: {importMaterials, importAnimation, optimizeMesh, generateColliders}."
                        }
                    },
                    required = new List<string> { "assetPath", "settings" }
                }
            }, SetImportSettings);
        }

        #endregion

        #region Asset Import Handlers

        private static McpToolResult GetImportSettings(Dictionary<string, object> args)
        {
            try
            {
                var (assetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;

                var (sanitizedPath, sanitizeErr) = TrySanitizePath(assetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;
                assetPath = sanitizedPath;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    return McpToolResult.Error($"Asset not found or not importable: '{assetPath}'");

                object settings;

                if (importer is TextureImporter tex)
                {
                    settings = new
                    {
                        importerType = "Texture",
                        textureType = tex.textureType.ToString(),
                        maxTextureSize = tex.maxTextureSize,
                        compression = tex.textureCompression.ToString(),
                        mipmaps = tex.mipmapEnabled,
                        readable = tex.isReadable,
                        sRGBTexture = tex.sRGBTexture,
                        alphaIsTransparency = tex.alphaIsTransparency,
                        filterMode = tex.filterMode.ToString(),
                        wrapMode = tex.wrapMode.ToString(),
                        anisoLevel = tex.anisoLevel
                    };
                }
                else if (importer is AudioImporter audio)
                {
                    var defaultSettings = audio.defaultSampleSettings;
                    settings = new
                    {
                        importerType = "Audio",
                        loadType = defaultSettings.loadType.ToString(),
                        compressionFormat = defaultSettings.compressionFormat.ToString(),
                        quality = defaultSettings.quality,
                        preloadAudioData = defaultSettings.preloadAudioData,
                        loadInBackground = audio.loadInBackground,
                        forceToMono = audio.forceToMono,
                        ambisonic = audio.ambisonic
                    };
                }
                else if (importer is ModelImporter model)
                {
                    settings = new
                    {
                        importerType = "Model",
                        importMaterials = model.materialImportMode.ToString(),
                        importAnimation = model.importAnimation,
                        optimizeMesh = model.optimizeMeshVertices,
                        generateColliders = model.addCollider,
                        globalScale = model.globalScale,
                        importNormals = model.importNormals.ToString(),
                        importBlendShapes = model.importBlendShapes,
                        isReadable = model.isReadable
                    };
                }
                else
                {
                    settings = new
                    {
                        importerType = importer.GetType().Name,
                        assetBundleName = importer.assetBundleName,
                        assetBundleVariant = importer.assetBundleVariant
                    };
                }

                return McpResponse.Success(new
                {
                    assetPath = assetPath,
                    settings = settings
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get import settings: {ex.Message}");
            }
        }

        private static McpToolResult SetImportSettings(Dictionary<string, object> args)
        {
            try
            {
                var (assetPath, assetPathErr) = RequireArg(args, "assetPath");
                if (assetPathErr != null) return assetPathErr;

                var (sanitizedPath, sanitizeErr) = TrySanitizePath(assetPath, "asset path");
                if (sanitizeErr != null) return sanitizeErr;
                assetPath = sanitizedPath;

                if (!args.TryGetValue("settings", out var settingsObj) || !(settingsObj is Dictionary<string, object> settings))
                    return McpToolResult.Error("'settings' must be a JSON object.");

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    return McpToolResult.Error($"Asset not found or not importable: '{assetPath}'");

                var applied = new List<string>();

                if (importer is TextureImporter tex)
                {
                    if (settings.TryGetValue("textureType", out var v) && Enum.TryParse<TextureImporterType>(v?.ToString(), out var tt))
                    { tex.textureType = tt; applied.Add("textureType"); }

                    if (settings.TryGetValue("maxTextureSize", out v) && v != null)
                    { tex.maxTextureSize = Convert.ToInt32(v); applied.Add("maxTextureSize"); }

                    if (settings.TryGetValue("compression", out v) && Enum.TryParse<TextureImporterCompression>(v?.ToString(), out var tc))
                    { tex.textureCompression = tc; applied.Add("compression"); }

                    if (settings.TryGetValue("mipmaps", out v) && v != null)
                    { tex.mipmapEnabled = Convert.ToBoolean(v); applied.Add("mipmaps"); }

                    if (settings.TryGetValue("readable", out v) && v != null)
                    { tex.isReadable = Convert.ToBoolean(v); applied.Add("readable"); }

                    if (settings.TryGetValue("sRGBTexture", out v) && v != null)
                    { tex.sRGBTexture = Convert.ToBoolean(v); applied.Add("sRGBTexture"); }

                    if (settings.TryGetValue("alphaIsTransparency", out v) && v != null)
                    { tex.alphaIsTransparency = Convert.ToBoolean(v); applied.Add("alphaIsTransparency"); }
                }
                else if (importer is AudioImporter audio)
                {
                    var sampleSettings = audio.defaultSampleSettings;
                    bool changed = false;

                    if (settings.TryGetValue("loadType", out var v) && Enum.TryParse<AudioClipLoadType>(v?.ToString(), out var lt))
                    { sampleSettings.loadType = lt; applied.Add("loadType"); changed = true; }

                    if (settings.TryGetValue("compressionFormat", out v) && Enum.TryParse<AudioCompressionFormat>(v?.ToString(), out var cf))
                    { sampleSettings.compressionFormat = cf; applied.Add("compressionFormat"); changed = true; }

                    if (settings.TryGetValue("quality", out v) && v != null)
                    { sampleSettings.quality = Convert.ToSingle(v); applied.Add("quality"); changed = true; }

                    if (settings.TryGetValue("preloadAudioData", out v) && v != null)
                    { sampleSettings.preloadAudioData = Convert.ToBoolean(v); applied.Add("preloadAudioData"); changed = true; }

                    if (changed)
                        audio.defaultSampleSettings = sampleSettings;

                    if (settings.TryGetValue("loadInBackground", out v) && v != null)
                    { audio.loadInBackground = Convert.ToBoolean(v); applied.Add("loadInBackground"); }

                    if (settings.TryGetValue("forceToMono", out v) && v != null)
                    { audio.forceToMono = Convert.ToBoolean(v); applied.Add("forceToMono"); }
                }
                else if (importer is ModelImporter model)
                {
                    if (settings.TryGetValue("importAnimation", out var v) && v != null)
                    { model.importAnimation = Convert.ToBoolean(v); applied.Add("importAnimation"); }

                    if (settings.TryGetValue("optimizeMesh", out v) && v != null)
                    { model.optimizeMeshVertices = Convert.ToBoolean(v); applied.Add("optimizeMesh"); }

                    if (settings.TryGetValue("generateColliders", out v) && v != null)
                    { model.addCollider = Convert.ToBoolean(v); applied.Add("generateColliders"); }

                    if (settings.TryGetValue("globalScale", out v) && v != null)
                    { model.globalScale = Convert.ToSingle(v); applied.Add("globalScale"); }

                    if (settings.TryGetValue("isReadable", out v) && v != null)
                    { model.isReadable = Convert.ToBoolean(v); applied.Add("isReadable"); }
                }
                else
                {
                    return McpToolResult.Error($"Unsupported importer type '{importer.GetType().Name}'. Supported: Texture, Audio, Model.");
                }

                if (applied.Count == 0)
                    return McpToolResult.Error("No recognized settings were applied. Check property names against the schema.");

                importer.SaveAndReimport();

                return McpResponse.Success(new
                {
                    success = true,
                    assetPath = assetPath,
                    appliedSettings = applied,
                    message = $"Applied {applied.Count} setting(s) and reimported '{assetPath}'."
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to set import settings: {ex.Message}");
            }
        }

        #endregion
    }
}
