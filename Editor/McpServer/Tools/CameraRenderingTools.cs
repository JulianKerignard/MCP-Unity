using System;
using System.Collections.Generic;
using System.Linq;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace McpUnity.Server
{
    /// <summary>
    /// Camera and rendering tools for MCP Unity Server.
    /// Contains 3 tools: ConfigureCamera, RenderCameraToFile, GetRenderPipelineInfo
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all camera and rendering related tools
        /// </summary>
        static partial void RegisterCameraRenderingTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_configure_camera",
                description = "Configure Camera component properties (clearFlags, FOV, clipping, cullingMask, HDR, etc.)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject with a Camera component" },
                        ["clearFlags"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Camera clear flags",
                            @enum = new List<string> { "Skybox", "SolidColor", "Depth", "Nothing" }
                        },
                        ["backgroundColor"] = new McpPropertySchema { type = "string", description = "Background color (hex '#RRGGBB' or named: red, blue, etc.)" },
                        ["fieldOfView"] = new McpPropertySchema { type = "number", description = "Field of view in degrees (default: 60)" },
                        ["nearClipPlane"] = new McpPropertySchema { type = "number", description = "Near clipping plane distance" },
                        ["farClipPlane"] = new McpPropertySchema { type = "number", description = "Far clipping plane distance" },
                        ["orthographic"] = new McpPropertySchema { type = "boolean", description = "Use orthographic projection" },
                        ["orthographicSize"] = new McpPropertySchema { type = "number", description = "Orthographic camera size" },
                        ["cullingMask"] = new McpPropertySchema { type = "array", description = "Array of layer names for culling mask" },
                        ["allowHDR"] = new McpPropertySchema { type = "boolean", description = "Allow HDR rendering" },
                        ["depth"] = new McpPropertySchema { type = "number", description = "Camera render order (depth value)" }
                    },
                    required = new List<string> { "gameObjectPath" }
                }
            }, ConfigureCamera);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_render_camera_to_file",
                description = "Render a Camera's view to an image file (PNG/JPG)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to the GameObject with a Camera component" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Output file path (e.g. 'Assets/Screenshots/render.png')" },
                        ["width"] = new McpPropertySchema { type = "integer", description = "Image width in pixels (default: 1920)" },
                        ["height"] = new McpPropertySchema { type = "integer", description = "Image height in pixels (default: 1080)" },
                        ["format"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Image format: 'png' or 'jpg' (default: 'png')",
                            @enum = new List<string> { "png", "jpg" }
                        },
                        ["jpgQuality"] = new McpPropertySchema { type = "integer", description = "JPG quality 1-100 (default: 75)" }
                    },
                    required = new List<string> { "gameObjectPath", "savePath" }
                }
            }, RenderCameraToFile);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_render_pipeline_info",
                description = "Get current render pipeline, quality settings, and rendering configuration",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, GetRenderPipelineInfo);
        }

        #region Camera/Rendering Handlers

        private static McpToolResult ConfigureCamera(Dictionary<string, object> args)
        {
            var (gameObjectPath, pathErr) = RequireArg(args, "gameObjectPath");
            if (pathErr != null) return pathErr;

            var go = GameObject.Find(gameObjectPath);
            if (go == null)
                return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

            var camera = go.GetComponent<Camera>();
            if (camera == null)
                return McpToolResult.Error($"No Camera component found on '{gameObjectPath}'");

            Undo.RecordObject(camera, "MCP Configure Camera");

            var appliedSettings = new List<string>();

            // clearFlags
            if (ArgumentParser.HasKey(args, "clearFlags"))
            {
                var clearFlags = ArgumentParser.GetEnum<CameraClearFlags>(args, "clearFlags", camera.clearFlags);
                camera.clearFlags = clearFlags;
                appliedSettings.Add($"clearFlags={clearFlags}");
            }

            // backgroundColor
            if (ArgumentParser.HasKey(args, "backgroundColor"))
            {
                var colorStr = ArgumentParser.GetString(args, "backgroundColor");
                if (!string.IsNullOrEmpty(colorStr))
                {
                    camera.backgroundColor = ColorParser.Parse(colorStr, camera.backgroundColor);
                    appliedSettings.Add($"backgroundColor={ColorParser.ToHex(camera.backgroundColor)}");
                }
            }

            // fieldOfView
            if (ArgumentParser.HasKey(args, "fieldOfView"))
            {
                camera.fieldOfView = ArgumentParser.GetFloat(args, "fieldOfView", camera.fieldOfView);
                appliedSettings.Add($"fieldOfView={camera.fieldOfView}");
            }

            // nearClipPlane
            if (ArgumentParser.HasKey(args, "nearClipPlane"))
            {
                camera.nearClipPlane = ArgumentParser.GetFloat(args, "nearClipPlane", camera.nearClipPlane);
                appliedSettings.Add($"nearClipPlane={camera.nearClipPlane}");
            }

            // farClipPlane
            if (ArgumentParser.HasKey(args, "farClipPlane"))
            {
                camera.farClipPlane = ArgumentParser.GetFloat(args, "farClipPlane", camera.farClipPlane);
                appliedSettings.Add($"farClipPlane={camera.farClipPlane}");
            }

            // orthographic
            if (ArgumentParser.HasKey(args, "orthographic"))
            {
                camera.orthographic = ArgumentParser.GetBool(args, "orthographic", camera.orthographic);
                appliedSettings.Add($"orthographic={camera.orthographic}");
            }

            // orthographicSize
            if (ArgumentParser.HasKey(args, "orthographicSize"))
            {
                camera.orthographicSize = ArgumentParser.GetFloat(args, "orthographicSize", camera.orthographicSize);
                appliedSettings.Add($"orthographicSize={camera.orthographicSize}");
            }

            // cullingMask
            if (ArgumentParser.HasKey(args, "cullingMask"))
            {
                var layerNames = ArgumentParser.GetStringArray(args, "cullingMask");
                if (layerNames.Length > 0)
                {
                    camera.cullingMask = LayerMask.GetMask(layerNames);
                    appliedSettings.Add($"cullingMask=[{string.Join(", ", layerNames)}]");
                }
            }

            // allowHDR
            if (ArgumentParser.HasKey(args, "allowHDR"))
            {
                camera.allowHDR = ArgumentParser.GetBool(args, "allowHDR", camera.allowHDR);
                appliedSettings.Add($"allowHDR={camera.allowHDR}");
            }

            // depth
            if (ArgumentParser.HasKey(args, "depth"))
            {
                camera.depth = ArgumentParser.GetFloat(args, "depth", camera.depth);
                appliedSettings.Add($"depth={camera.depth}");
            }

            EditorUtility.SetDirty(camera);

            return McpResponse.Success(new Dictionary<string, object>
            {
                ["success"] = true,
                ["gameObjectPath"] = gameObjectPath,
                ["appliedSettings"] = appliedSettings,
                ["message"] = $"Configured {appliedSettings.Count} camera settings on '{gameObjectPath}'"
            });
        }

        private static McpToolResult RenderCameraToFile(Dictionary<string, object> args)
        {
            var (gameObjectPath, goErr) = RequireArg(args, "gameObjectPath");
            if (goErr != null) return goErr;

            var (rawSavePath, savePathErr) = RequireArg(args, "savePath");
            if (savePathErr != null) return savePathErr;

            // Sanitize save path
            var (savePath, sanitizeErr) = TrySanitizePath(rawSavePath, "save path");
            if (sanitizeErr != null) return sanitizeErr;

            var go = GameObject.Find(gameObjectPath);
            if (go == null)
                return McpToolResult.Error($"GameObject not found: {gameObjectPath}");

            var camera = go.GetComponent<Camera>();
            if (camera == null)
                return McpToolResult.Error($"No Camera component found on '{gameObjectPath}'");

            int width = ArgumentParser.GetInt(args, "width", 1920);
            int height = ArgumentParser.GetInt(args, "height", 1080);
            string format = ArgumentParser.GetString(args, "format", "png").ToLower();
            int jpgQuality = ArgumentParser.GetIntClamped(args, "jpgQuality", 75, 1, 100);

            // Clamp dimensions for safety
            width = Math.Clamp(width, 1, 4096);
            height = Math.Clamp(height, 1, 4096);

            // Ensure correct file extension
            if (format == "jpg" && !savePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !savePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                savePath = System.IO.Path.ChangeExtension(savePath, ".jpg");
            else if (format == "png" && !savePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                savePath = System.IO.Path.ChangeExtension(savePath, ".png");

            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            RenderTexture rt = null;
            Texture2D tex = null;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;

            try
            {
                rt = new RenderTexture(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] bytes;
                if (format == "jpg")
                    bytes = tex.EncodeToJPG(jpgQuality);
                else
                    bytes = tex.EncodeToPNG();

                System.IO.File.WriteAllBytes(savePath, bytes);
                AssetDatabase.Refresh();

                return McpResponse.Success(new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["savedPath"] = savePath,
                    ["width"] = width,
                    ["height"] = height,
                    ["format"] = format,
                    ["fileSizeKB"] = bytes.Length / 1024,
                    ["message"] = $"Rendered camera to {savePath} ({width}x{height} {format}, {bytes.Length / 1024}KB)"
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to render camera: {ex.Message}");
            }
            finally
            {
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static McpToolResult GetRenderPipelineInfo(Dictionary<string, object> args)
        {
            // Detect current render pipeline
            string pipelineName = "Built-in";
            string pipelineAssetType = null;
            var pipelineAsset = GraphicsSettings.currentRenderPipeline;

            if (pipelineAsset != null)
            {
                var typeName = pipelineAsset.GetType().Name;
                pipelineAssetType = typeName;

                if (typeName.Contains("Universal") || typeName.Contains("URP"))
                    pipelineName = "URP";
                else if (typeName.Contains("HDRender") || typeName.Contains("HDRP"))
                    pipelineName = "HDRP";
                else
                    pipelineName = typeName;
            }

            // Quality settings
            var qualityNames = QualitySettings.names;
            int currentQualityLevel = QualitySettings.GetQualityLevel();

            // Resolution info
            var resolution = Screen.currentResolution;

            var result = new Dictionary<string, object>
            {
                ["pipelineName"] = pipelineName,
                ["qualityLevel"] = currentQualityLevel,
                ["qualityLevelName"] = currentQualityLevel < qualityNames.Length ? qualityNames[currentQualityLevel] : "Unknown",
                ["qualityNames"] = qualityNames,
                ["colorSpace"] = QualitySettings.activeColorSpace.ToString(),
                ["resolution"] = new Dictionary<string, object>
                {
                    ["width"] = resolution.width,
                    ["height"] = resolution.height,
                    ["refreshRate"] = resolution.refreshRateRatio.value
                }
            };

            if (pipelineAssetType != null)
                result["pipelineAssetType"] = pipelineAssetType;

            if (pipelineAsset != null)
                result["pipelineAssetName"] = pipelineAsset.name;

            return McpResponse.Success(result);
        }

        #endregion
    }
}
