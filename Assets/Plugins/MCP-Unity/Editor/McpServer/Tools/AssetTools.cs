using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using McpUnity.Helpers;
using McpUnity.Protocol;

namespace McpUnity.Server
{
    /// <summary>
    /// Asset Browser tools for MCP Unity Server.
    /// Provides 5 tools for asset management and inspection.
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all Asset Browser tools.
        /// </summary>
        static partial void RegisterAssetTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_search_assets",
                description = "Search for assets in the project using Unity's search filter syntax",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["filter"] = new McpPropertySchema { type = "string", description = "Search filter (e.g., 't:Texture', 'l:MyLabel', 'name')" },
                        ["maxResults"] = new McpPropertySchema { type = "integer", description = "Maximum results to return (default: 50, max: 200)" },
                        ["searchFolders"] = new McpPropertySchema { type = "array", description = "Specific folders to search in" }
                    },
                    required = new List<string>()
                }
            }, SearchAssets);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_asset_info",
                description = "Get detailed information about a specific asset including dependencies and type-specific data",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema { type = "string", description = "Path to the asset" },
                        ["includeDependencies"] = new McpPropertySchema { type = "boolean", description = "Include asset dependencies (default: false)" },
                        ["includeReferences"] = new McpPropertySchema { type = "boolean", description = "Include references to this asset (can be slow)" },
                        ["maxReferences"] = new McpPropertySchema { type = "integer", description = "Max referencedBy results (default: 20, max: 200)" }
                    },
                    required = new List<string> { "assetPath" }
                }
            }, GetAssetInfo);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_folders",
                description = "List folders in the project hierarchy",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["parentPath"] = new McpPropertySchema { type = "string", description = "Parent folder path (default: 'Assets')" },
                        ["recursive"] = new McpPropertySchema { type = "boolean", description = "Include subfolders recursively (default: true)" },
                        ["maxDepth"] = new McpPropertySchema { type = "integer", description = "Maximum recursion depth (default: 5, max: 20)" }
                    },
                    required = new List<string>()
                }
            }, ListFolders);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_folder_contents",
                description = "List assets in a specific folder with optional type filtering",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["folderPath"] = new McpPropertySchema { type = "string", description = "Path to the folder" },
                        ["typeFilter"] = new McpPropertySchema { type = "string", description = "Filter by asset type (e.g., 'Texture2D', 'Material')" },
                        ["includeSubfolders"] = new McpPropertySchema { type = "boolean", description = "Include assets from subfolders (default: false)" }
                    },
                    required = new List<string> { "folderPath" }
                }
            }, ListFolderContents);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_asset_preview",
                description = "Get a preview image of an asset as base64-encoded data",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"] = new McpPropertySchema { type = "string", description = "Path to the asset" },
                        ["size"] = new McpPropertySchema { type = "string", description = "Preview size: tiny(32), small(64), medium(128), large(256). Default: small" },
                        ["format"] = new McpPropertySchema { type = "string", description = "Image format: png or jpg. Default: jpg" },
                        ["jpgQuality"] = new McpPropertySchema { type = "integer", description = "JPG quality 1-100 (default: 75)" },
                        ["returnBase64"] = new McpPropertySchema { type = "boolean", description = "Return image as base64 data URI in the response (default: false)" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Custom path to save the preview image (default: Assets/Screenshots/preview_{name}_{timestamp}.jpg)" }
                    },
                    required = new List<string> { "assetPath" }
                }
            }, GetAssetPreview);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_folder",
                description = "Create a new folder inside Assets/",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["parentPath"] = new McpPropertySchema { type = "string", description = "Parent folder path (e.g. 'Assets/Art')" },
                        ["folderName"] = new McpPropertySchema { type = "string", description = "Name of the new folder" }
                    },
                    required = new List<string> { "parentPath", "folderName" }
                }
            }, CreateFolder);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_delete_asset",
                description = "Delete an asset file or folder from the project",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["assetPath"]    = new McpPropertySchema { type = "string", description = "Path to the asset to delete (e.g. 'Assets/Materials/Old.mat')" },
                        ["moveToTrash"] = new McpPropertySchema { type = "boolean", description = "Move to OS trash instead of permanent delete (default: true)" }
                    },
                    required = new List<string> { "assetPath" }
                }
            }, DeleteAsset);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_move_asset",
                description = "Move or rename an asset file within the project",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["sourcePath"]      = new McpPropertySchema { type = "string", description = "Current asset path (e.g. 'Assets/Old/Cube.prefab')" },
                        ["destinationPath"] = new McpPropertySchema { type = "string", description = "New asset path (e.g. 'Assets/New/Cube.prefab')" }
                    },
                    required = new List<string> { "sourcePath", "destinationPath" }
                }
            }, MoveAsset);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_copy_asset",
                description = "Duplicate an asset to a new path",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["sourcePath"]      = new McpPropertySchema { type = "string", description = "Asset to copy (e.g. 'Assets/Materials/Base.mat')" },
                        ["destinationPath"] = new McpPropertySchema { type = "string", description = "Destination path for the copy" }
                    },
                    required = new List<string> { "sourcePath", "destinationPath" }
                }
            }, CopyAsset);
        }

        #region Asset Browser Helpers

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static void CollectFolders(string path, List<object> folders, bool recursive, int maxDepth, int currentDepth)
        {
            var subFolders = AssetDatabase.GetSubFolders(path);

            foreach (var folder in subFolders)
            {
                // GetSubFolders is O(1) — avoids the expensive FindAssets("") per folder
                var nestedFolders = AssetDatabase.GetSubFolders(folder);

                folders.Add(new
                {
                    path           = folder,
                    name           = System.IO.Path.GetFileName(folder),
                    depth          = currentDepth + 1,
                    subFolderCount = nestedFolders.Length
                });

                if (recursive && currentDepth < maxDepth - 1)
                {
                    CollectFolders(folder, folders, true, maxDepth, currentDepth + 1);
                }
            }
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;

            // M-01: Use try/finally to guarantee RenderTexture is always released,
            // even if ReadPixels or Apply throw (e.g. GPU context lost).
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

        #endregion

        #region Asset Browser Handlers

        private static McpToolResult SearchAssets(Dictionary<string, object> args)
        {
            string filter = ArgumentParser.GetString(args, "filter", "");
            int maxResults = ArgumentParser.GetIntClamped(args, "maxResults", 50, 1, 200);

            var searchFoldersArray = ArgumentParser.GetStringArray(args, "searchFolders");
            string[] searchFolders = searchFoldersArray.Length > 0 ? searchFoldersArray : null;

            string[] guids;
            if (searchFolders != null && searchFolders.Length > 0)
            {
                guids = AssetDatabase.FindAssets(filter, searchFolders);
            }
            else
            {
                guids = AssetDatabase.FindAssets(filter);
            }

            var results = new List<object>();
            int count = 0;

            foreach (var guid in guids)
            {
                if (count >= maxResults) break;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                if (type != null)
                {
                    results.Add(new
                    {
                        path = path,
                        name = System.IO.Path.GetFileNameWithoutExtension(path),
                        type = type.Name,
                        guid = guid
                    });
                    count++;
                }
            }

            return McpResponse.Success(new
            {
                filter = filter,
                totalFound = guids.Length,
                returned = results.Count,
                assets = results
            });
        }

        private static McpToolResult GetAssetInfo(Dictionary<string, object> args)
        {
            var (rawPath, assetPathArgErr) = RequireArg(args, "assetPath");
            if (assetPathArgErr != null) return assetPathArgErr;

            var (assetPath, assetPathErr) = TrySanitizePath(rawPath, "asset path");
            if (assetPathErr != null) return assetPathErr;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (asset == null)
            {
                return McpToolResult.Error($"Asset not found: {assetPath}");
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var labels = AssetDatabase.GetLabels(asset);

            // Get file info
            var fileInfo = new System.IO.FileInfo(assetPath);
            long fileSize = fileInfo.Exists ? fileInfo.Length : 0;

            var info = new Dictionary<string, object>
            {
                ["path"] = assetPath,
                ["name"] = asset.name,
                ["type"] = type?.Name ?? "Unknown",
                ["guid"] = guid,
                ["labels"] = labels,
                ["fileSize"] = fileSize,
                ["fileSizeFormatted"] = FormatFileSize(fileSize)
            };

            // Include dependencies if requested
            bool includeDeps = ArgumentParser.GetBool(args, "includeDependencies", false);
            if (includeDeps)
            {
                var deps = AssetDatabase.GetDependencies(assetPath, false);
                info["dependencies"] = deps.Where(d => d != assetPath).ToArray();
            }

            // Include references if requested (can be slow)
            bool includeRefs = ArgumentParser.GetBool(args, "includeReferences", false);
            if (includeRefs)
            {
                int maxReferences = ArgumentParser.GetIntClamped(args, "maxReferences", 20, 1, 200);
                var allAssets = AssetDatabase.GetAllAssetPaths();
                var references = new List<string>();
                const int maxAssetsToScan = 5000;
                int scanned = 0;
                bool cappedScan = false;
                bool cappedResults = false;

                foreach (var otherPath in allAssets)
                {
                    if (otherPath == assetPath || !otherPath.StartsWith("Assets/")) continue;
                    if (++scanned > maxAssetsToScan) { cappedScan = true; break; }
                    var deps = AssetDatabase.GetDependencies(otherPath, false);
                    if (System.Array.IndexOf(deps, assetPath) >= 0)
                    {
                        references.Add(otherPath);
                        if (references.Count >= maxReferences) { cappedResults = true; break; }
                    }
                }
                info["referencedBy"] = references.ToArray();
                if (cappedResults)
                    info["referencedByNote"] = $"Capped at {maxReferences} results. Pass maxReferences (max 200) for more.";
                else if (cappedScan)
                    info["referencedByNote"] = $"Scan capped at {maxAssetsToScan} assets. Use search_assets for full results.";
            }

            // Add type-specific info
            if (asset is Texture2D tex)
            {
                info["textureInfo"] = new
                {
                    width = tex.width,
                    height = tex.height,
                    format = tex.format.ToString(),
                    mipmapCount = tex.mipmapCount
                };
            }
            else if (asset is AudioClip audio)
            {
                info["audioInfo"] = new
                {
                    length = audio.length,
                    channels = audio.channels,
                    frequency = audio.frequency,
                    samples = audio.samples
                };
            }
            else if (asset is Mesh mesh)
            {
                info["meshInfo"] = new
                {
                    vertexCount = mesh.vertexCount,
                    triangles = mesh.triangles.Length / 3,
                    subMeshCount = mesh.subMeshCount,
                    bounds = new
                    {
                        center = new { x = mesh.bounds.center.x, y = mesh.bounds.center.y, z = mesh.bounds.center.z },
                        size = new { x = mesh.bounds.size.x, y = mesh.bounds.size.y, z = mesh.bounds.size.z }
                    }
                };
            }

            return McpResponse.Success(info);
        }

        private static McpToolResult ListFolders(Dictionary<string, object> args)
        {
            string rawParentPath = ArgumentParser.GetString(args, "parentPath", "Assets");
            bool recursive = ArgumentParser.GetBool(args, "recursive", true);
            int maxDepth = ArgumentParser.GetIntClamped(args, "maxDepth", 5, 1, 20);

            // H-02: Validate path to prevent traversal outside Assets/
            var (parentPath, parentPathErr) = TrySanitizePath(rawParentPath, "folder path");
            if (parentPathErr != null) return parentPathErr;

            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                return McpToolResult.Error($"Folder not found: {parentPath}");
            }

            var folders = new List<object>();
            CollectFolders(parentPath, folders, recursive, maxDepth, 0);

            return McpResponse.Success(new
            {
                parentPath = parentPath,
                recursive = recursive,
                folderCount = folders.Count,
                folders = folders
            });
        }

        private static McpToolResult ListFolderContents(Dictionary<string, object> args)
        {
            var (rawFolderPath, folderPathArgErr) = RequireArg(args, "folderPath");
            if (folderPathArgErr != null) return folderPathArgErr;

            // H-02: Validate path to prevent traversal outside Assets/
            var (folderPath, folderPathErr) = TrySanitizePath(rawFolderPath, "folder path");
            if (folderPathErr != null) return folderPathErr;

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return McpToolResult.Error($"Folder not found: {folderPath}");
            }

            string typeFilterStr = ArgumentParser.GetString(args, "typeFilter", "");
            string typeFilter = string.IsNullOrEmpty(typeFilterStr) ? "" : "t:" + typeFilterStr;

            bool includeSubfolders = ArgumentParser.GetBool(args, "includeSubfolders", false);

            string[] guids;
            if (includeSubfolders)
            {
                guids = AssetDatabase.FindAssets(typeFilter, new[] { folderPath });
            }
            else
            {
                // Get only direct children
                guids = AssetDatabase.FindAssets(typeFilter, new[] { folderPath });
                guids = guids.Where(g =>
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var parent = System.IO.Path.GetDirectoryName(p).Replace("\\", "/");
                    return parent == folderPath;
                }).ToArray();
            }

            var assets = new List<object>();
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

                // Skip folders
                if (AssetDatabase.IsValidFolder(assetPath)) continue;

                assets.Add(new
                {
                    path = assetPath,
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                    extension = System.IO.Path.GetExtension(assetPath),
                    type = type?.Name ?? "Unknown",
                    guid = guid
                });
            }

            // Also list subfolders
            var subFolders = AssetDatabase.GetSubFolders(folderPath)
                .Select(f => new { path = f, name = System.IO.Path.GetFileName(f), isFolder = true })
                .ToList();

            return McpResponse.Success(new
            {
                folderPath = folderPath,
                assetCount = assets.Count,
                subFolderCount = subFolders.Count,
                assets = assets,
                subFolders = subFolders
            });
        }

        private static McpToolResult GetAssetPreview(Dictionary<string, object> args)
        {
            var (rawPath, assetPathArgErr) = RequireArg(args, "assetPath");
            if (assetPathArgErr != null) return assetPathArgErr;

            var (assetPath, assetPathErr) = TrySanitizePath(rawPath, "asset path");
            if (assetPathErr != null) return assetPathErr;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            if (asset == null)
            {
                return McpToolResult.Error($"Asset not found: {assetPath}");
            }

            // Parse size preset (default: small = 64px for token efficiency)
            int size = 64;
            var sizeStr = ArgumentParser.GetString(args, "size", "small");
            switch (sizeStr?.ToLower())
            {
                case "tiny": size = 32; break;
                case "small": size = 64; break;
                case "medium": size = 128; break;
                case "large": size = 256; break;
            }

            // Parse format (default: jpg for smaller size)
            string format = ArgumentParser.GetString(args, "format", "jpg")?.ToLower();

            // Parse JPG quality
            int jpgQuality = ArgumentParser.GetIntClamped(args, "jpgQuality", 75, 1, 100);

            // Parse returnBase64 (default: false for token efficiency)
            bool returnBase64 = ArgumentParser.GetBool(args, "returnBase64", false);

            // Get the asset preview — Unity generates previews asynchronously.
            // Retry for up to ~500ms to allow the preview texture to be ready.
            Texture2D preview = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                preview = AssetPreview.GetAssetPreview(asset);
                if (preview != null) break;
                if (!AssetPreview.IsLoadingAssetPreviews()) break;
                System.Threading.Thread.Sleep(50);
            }

            // Fallback to mini thumbnail (always available synchronously)
            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(asset);

            if (preview == null)
                return McpToolResult.Error($"Could not generate preview for asset: {assetPath}");

            // Always copy through RenderTexture to ensure the texture is readable.
            // AssetPreview textures are often GPU-only and cannot be encoded directly.
            Texture2D resized = ResizeTexture(preview, size, size);

            // Encode based on format
            byte[] imageData;
            string mimeType;
            if (format == "png")
            {
                imageData = resized.EncodeToPNG();
                mimeType = "image/png";
            }
            else
            {
                imageData = resized.EncodeToJPG(jpgQuality);
                mimeType = "image/jpeg";
            }

            // Clean up temporary readable copy
            UnityEngine.Object.DestroyImmediate(resized);

            // Save to disk (default behavior for token efficiency)
            string extension = format == "png" ? ".png" : ".jpg";
            string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string rawSavePath = ArgumentParser.GetString(args, "savePath", null);
            string savePath;
            if (string.IsNullOrEmpty(rawSavePath))
            {
                savePath = $"Assets/Screenshots/preview_{assetName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
            }
            else
            {
                // H-05: Validate user-supplied savePath to prevent writes outside Assets/
                var (sanitizedSavePath, savePathErr) = TrySanitizePath(rawSavePath, "save path");
                if (savePathErr != null) return savePathErr;
                savePath = sanitizedSavePath;
            }

            try
            {
                var directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                System.IO.File.WriteAllBytes(savePath, imageData);
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to save preview to '{savePath}': {ex.Message}");
            }

            // Build response
            var responseData = new Dictionary<string, object>
            {
                ["assetPath"] = assetPath,
                ["savedPath"] = savePath,
                ["size"] = size,
                ["format"] = format,
                ["fileSizeBytes"] = imageData.Length
            };

            if (returnBase64)
            {
                string base64 = Convert.ToBase64String(imageData);
                responseData["base64"] = base64;
                responseData["dataUri"] = $"data:{mimeType};base64,{base64}";
            }
            else
            {
                responseData["hint"] = "Use returnBase64=true to get image data, or read the saved file";
            }

            return McpResponse.Success($"Preview saved to {savePath}", responseData);
        }

        private static McpToolResult CreateFolder(Dictionary<string, object> args)
        {
            var (rawParent, parentPathArgErr) = RequireArg(args, "parentPath");
            if (parentPathArgErr != null) return parentPathArgErr;

            var (folderName, folderNameArgErr) = RequireArg(args, "folderName");
            if (folderNameArgErr != null) return folderNameArgErr;

            // Validate folder name (no slashes, no illegal chars)
            if (folderName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                return McpToolResult.Error($"Invalid folder name '{folderName}': contains illegal characters.");

            var (parentPath, parentPathErr) = TrySanitizePath(rawParent, "parent path");
            if (parentPathErr != null) return parentPathErr;

            if (!AssetDatabase.IsValidFolder(parentPath))
                return McpToolResult.Error($"Parent folder not found: '{parentPath}'");

            string newFolderPath = parentPath + "/" + folderName;
            if (AssetDatabase.IsValidFolder(newFolderPath))
                return McpToolResult.Error($"Folder already exists: '{newFolderPath}'");

            string guid = AssetDatabase.CreateFolder(parentPath, folderName);
            if (string.IsNullOrEmpty(guid))
                return McpToolResult.Error($"Failed to create folder '{newFolderPath}'");

            return McpResponse.Success(new
            {
                path       = newFolderPath,
                folderName = folderName,
                guid       = guid
            });
        }

        private static McpToolResult DeleteAsset(Dictionary<string, object> args)
        {
            var (rawPath, assetPathArgErr) = RequireArg(args, "assetPath");
            if (assetPathArgErr != null) return assetPathArgErr;

            var (assetPath, assetPathErr) = TrySanitizePath(rawPath, "asset path");
            if (assetPathErr != null) return assetPathErr;

            if (!System.IO.File.Exists(assetPath) && !System.IO.Directory.Exists(assetPath))
                return McpToolResult.Error($"Asset not found: '{assetPath}'");

            bool moveToTrash = ArgumentParser.GetBool(args, "moveToTrash", true);

            bool success = moveToTrash
                ? AssetDatabase.MoveAssetToTrash(assetPath)
                : AssetDatabase.DeleteAsset(assetPath);

            if (!success)
                return McpToolResult.Error($"Failed to delete '{assetPath}'");

            return McpResponse.Success(new
            {
                deletedPath  = assetPath,
                movedToTrash = moveToTrash
            });
        }

        private static McpToolResult MoveAsset(Dictionary<string, object> args)
        {
            var (rawSource, sourceArgErr) = RequireArg(args, "sourcePath");
            if (sourceArgErr != null) return sourceArgErr;

            var (rawDest, destArgErr) = RequireArg(args, "destinationPath");
            if (destArgErr != null) return destArgErr;

            var (sourcePath, sourcePathErr) = TrySanitizePath(rawSource, "source path");
            if (sourcePathErr != null) return sourcePathErr;

            var (destinationPath, destPathErr) = TrySanitizePath(rawDest, "destination path");
            if (destPathErr != null) return destPathErr;

            if (!System.IO.File.Exists(sourcePath) && !System.IO.Directory.Exists(sourcePath))
                return McpToolResult.Error($"Source not found: '{sourcePath}'");

            // Ensure destination folder exists
            var destDir = System.IO.Path.GetDirectoryName(destinationPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(destDir))
                return McpToolResult.Error($"Destination folder does not exist: '{destDir}'. Create it first with unity_create_folder.");

            string moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(moveError))
                return McpToolResult.Error($"Move failed: {moveError}");

            return McpResponse.Success(new
            {
                sourcePath      = sourcePath,
                destinationPath = destinationPath
            });
        }

        private static McpToolResult CopyAsset(Dictionary<string, object> args)
        {
            var (rawSource, sourceArgErr) = RequireArg(args, "sourcePath");
            if (sourceArgErr != null) return sourceArgErr;

            var (rawDest, destArgErr) = RequireArg(args, "destinationPath");
            if (destArgErr != null) return destArgErr;

            var (sourcePath, sourcePathErr) = TrySanitizePath(rawSource, "source path");
            if (sourcePathErr != null) return sourcePathErr;

            var (destinationPath, destPathErr) = TrySanitizePath(rawDest, "destination path");
            if (destPathErr != null) return destPathErr;

            if (!System.IO.File.Exists(sourcePath))
                return McpToolResult.Error($"Source asset not found: '{sourcePath}'");

            // Ensure destination folder exists
            var destDir = System.IO.Path.GetDirectoryName(destinationPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(destDir))
                return McpToolResult.Error($"Destination folder does not exist: '{destDir}'. Create it first with unity_create_folder.");

            bool success = AssetDatabase.CopyAsset(sourcePath, destinationPath);
            if (!success)
                return McpToolResult.Error($"Failed to copy '{sourcePath}' to '{destinationPath}'");

            AssetDatabase.Refresh();

            return McpResponse.Success(new
            {
                sourcePath      = sourcePath,
                destinationPath = destinationPath,
                guid            = AssetDatabase.AssetPathToGUID(destinationPath)
            });
        }

        #endregion
    }
}
