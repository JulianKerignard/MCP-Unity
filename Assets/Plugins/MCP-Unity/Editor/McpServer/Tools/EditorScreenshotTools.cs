using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Editor screenshot tools: TakeScreenshot
    /// </summary>
    public partial class McpUnityServer
    {
        // Maximum screenshot dimension for safety
        private const int MaxScreenshotDimension = 4096;

        static partial void RegisterEditorScreenshotTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_take_screenshot",
                description = "Take a screenshot of the Scene or Game view. Returns saved path, optionally base64 data.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["view"] = new McpPropertySchema { type = "string", description = "'Scene' (default) or 'Game' (requires Play Mode)" },
                        ["format"] = new McpPropertySchema { type = "string", description = "'jpg' (default, smaller) or 'png'" },
                        ["width"] = new McpPropertySchema { type = "integer", description = "Width in pixels (default: 640)" },
                        ["height"] = new McpPropertySchema { type = "integer", description = "Height in pixels (default: 360)" },
                        ["jpgQuality"] = new McpPropertySchema { type = "integer", description = "JPG quality 1-100 (default: 75)" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Custom save path (default: Assets/Screenshots/)" },
                        ["returnBase64"] = new McpPropertySchema { type = "boolean", description = "If true, include base64 data in response (default: false)" }
                    },
                    required = new List<string>()
                }
            }, TakeScreenshot);
        }

        private static Texture2D CaptureSceneView(int width, int height)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return null;
            }

            var svCamera = sceneView.camera;
            if (svCamera == null)
            {
                return null;
            }

            // URP/HDRP: SceneView.camera.Render() produces black output.
            // Instead, find the Main Camera (which has proper URP data),
            // temporarily move it to the SceneView position, render, then restore.
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                return null;
            }

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;

            // Save original state
            var origPos = mainCam.transform.position;
            var origRot = mainCam.transform.rotation;
            var origFOV = mainCam.fieldOfView;
            var origNear = mainCam.nearClipPlane;
            var origFar = mainCam.farClipPlane;
            var origTarget = mainCam.targetTexture;
            var origActive = RenderTexture.active;
            var origOrthographic = mainCam.orthographic;
            var origOrthoSize = mainCam.orthographicSize;

            try
            {
                // Move main camera to SceneView position
                mainCam.transform.position = svCamera.transform.position;
                mainCam.transform.rotation = svCamera.transform.rotation;
                mainCam.fieldOfView = svCamera.fieldOfView;
                mainCam.nearClipPlane = svCamera.nearClipPlane;
                mainCam.farClipPlane = svCamera.farClipPlane;
                mainCam.orthographic = svCamera.orthographic;
                mainCam.orthographicSize = svCamera.orthographicSize;
                mainCam.targetTexture = rt;

                mainCam.Render();

                RenderTexture.active = rt;
                Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();

                return screenshot;
            }
            finally
            {
                // Restore original state
                mainCam.transform.position = origPos;
                mainCam.transform.rotation = origRot;
                mainCam.fieldOfView = origFOV;
                mainCam.nearClipPlane = origNear;
                mainCam.farClipPlane = origFar;
                mainCam.orthographic = origOrthographic;
                mainCam.orthographicSize = origOrthoSize;
                mainCam.targetTexture = origTarget;
                RenderTexture.active = origActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static Texture2D CaptureGameView(int width, int height)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();

            if (screenshot == null)
            {
                return null;
            }

            if (screenshot.width != width || screenshot.height != height)
            {
                var resized = ResizeTextureForScreenshot(screenshot, width, height);
                UnityEngine.Object.DestroyImmediate(screenshot);
                return resized;
            }

            return screenshot;
        }

        private static Texture2D ResizeTextureForScreenshot(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;

            try
            {
                RenderTexture.active = rt;
                Graphics.Blit(source, rt);

                Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                result.Apply();

                return result;
            }
            finally
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static McpToolResult TakeScreenshot(Dictionary<string, object> args)
        {
            string view = ArgumentParser.GetString(args, "view", "Scene");
            string format = ArgumentParser.GetString(args, "format", "jpg").ToLower();
            int jpgQuality = ArgumentParser.GetIntClamped(args, "jpgQuality", 75, 1, 100);
            int width = ArgumentParser.GetIntClamped(args, "width", 640, 1, MaxScreenshotDimension);
            int height = ArgumentParser.GetIntClamped(args, "height", 360, 1, MaxScreenshotDimension);
            bool returnBase64 = ArgumentParser.GetBool(args, "returnBase64", false);

            string extension = format == "png" ? ".png" : ".jpg";
            string savePath = $"Assets/Screenshots/screenshot_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";

            string customPath = ArgumentParser.GetString(args, "savePath", null);
            if (!string.IsNullOrEmpty(customPath))
            {
                savePath = customPath;
                if (format == "jpg" && !savePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    savePath = System.IO.Path.ChangeExtension(savePath, ".jpg");
                else if (format == "png" && !savePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    savePath = System.IO.Path.ChangeExtension(savePath, ".png");
            }

            var (sanitizedSavePath, sanitizeErr) = TrySanitizePath(savePath, "save path");
            if (sanitizeErr != null) return sanitizeErr;
            savePath = sanitizedSavePath;

            Texture2D screenshot = null;

            try
            {
                if (view.Equals("Scene", StringComparison.OrdinalIgnoreCase))
                {
                    screenshot = CaptureSceneView(width, height);
                }
                else if (view.Equals("Game", StringComparison.OrdinalIgnoreCase))
                {
                    if (!EditorApplication.isPlaying)
                    {
                        return McpToolResult.Error("Game View screenshot requires Play Mode. Use 'Scene' view instead.");
                    }
                    screenshot = CaptureGameView(width, height);
                }
                else
                {
                    return McpToolResult.Error($"Invalid view: {view}. Use 'Scene' or 'Game'.");
                }

                if (screenshot == null)
                {
                    return McpToolResult.Error($"Failed to capture {view} view");
                }

                byte[] imageData;
                if (format == "png")
                {
                    imageData = screenshot.EncodeToPNG();
                }
                else
                {
                    imageData = screenshot.EncodeToJPG(jpgQuality);
                }

                var directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllBytes(savePath, imageData);
                AssetDatabase.Refresh();

                var responseData = new Dictionary<string, object>
                {
                    ["view"] = view,
                    ["format"] = format,
                    ["width"] = screenshot.width,
                    ["height"] = screenshot.height,
                    ["savedPath"] = savePath,
                    ["fileSizeKB"] = imageData.Length / 1024
                };

                if (returnBase64)
                {
                    string base64 = Convert.ToBase64String(imageData);
                    responseData["base64"] = base64;
                    responseData["base64Length"] = base64.Length;
                }
                else
                {
                    responseData["hint"] = "Use returnBase64=true to get image data, or read the saved file";
                }

                return McpResponse.Success($"Screenshot saved to '{savePath}'", responseData);
            }
            finally
            {
                if (screenshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }
            }
        }
    }
}
