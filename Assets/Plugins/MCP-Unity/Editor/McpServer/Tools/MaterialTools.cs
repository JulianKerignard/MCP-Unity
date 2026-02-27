using System;
using System.Collections.Generic;
using McpUnity.Helpers;
using McpUnity.Protocol;
using McpUnity.Editor;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Material management tools for MCP Unity Server.
    /// Contains 3 tools: GetMaterial, SetMaterial, CreateMaterial
    /// </summary>
    public partial class McpUnityServer
    {
        /// <summary>
        /// Register all material-related tools
        /// </summary>
        static partial void RegisterMaterialTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_material",
                description = "Get material properties from an asset path or a GameObject's renderer",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["materialPath"] = new McpPropertySchema { type = "string", description = "Path to the material asset (e.g., 'Assets/Materials/MyMaterial.mat')" },
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to a GameObject to get its material" },
                        ["materialIndex"] = new McpPropertySchema { type = "integer", description = "Index of the material on the renderer (default: 0)" }
                    },
                    required = new List<string>()
                }
            }, GetMaterial);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_set_material",
                description = "Modify material properties (shader, colors, textures, values). Supports automatic property mapping for URP/HDRP. NOTE: To ASSIGN a material to a GameObject's renderer, use unity_set_reference with componentType='MeshRenderer' and fieldName='m_Materials.Array.data[0]' instead.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["materialPath"] = new McpPropertySchema { type = "string", description = "Path to the material asset" },
                        ["gameObjectPath"] = new McpPropertySchema { type = "string", description = "Path to a GameObject to modify its material" },
                        ["materialIndex"] = new McpPropertySchema { type = "integer", description = "Index of the material on the renderer (default: 0)" },
                        ["shader"] = new McpPropertySchema { type = "string", description = "New shader name (auto-converts 'Standard' for URP/HDRP)" },
                        ["renderQueue"] = new McpPropertySchema { type = "integer", description = "Render queue value" },
                        ["properties"] = new McpPropertySchema { type = "object", description = "Properties to set: {_Color: {r,g,b,a}, _MainTex: 'path', _Metallic: 0.5, etc.}" }
                    },
                    required = new List<string>()
                }
            }, SetMaterial);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_material",
                description = "Create a new material with specified shader and properties. Auto-detects render pipeline (URP/HDRP/Built-in).",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["name"] = new McpPropertySchema { type = "string", description = "Name for the new material" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Path to save the material (e.g., 'Assets/Materials/NewMat.mat')" },
                        ["shader"] = new McpPropertySchema { type = "string", description = "Shader name (default: pipeline-appropriate standard shader)" },
                        ["properties"] = new McpPropertySchema { type = "object", description = "Initial properties to set" }
                    },
                    required = new List<string> { "name", "savePath" }
                }
            }, CreateMaterial);
        }

        #region Material Handlers

        private static McpToolResult GetMaterial(Dictionary<string, object> args)
        {
            string materialPath = ArgumentParser.GetString(args, "materialPath", null);
            string gameObjectPath = ArgumentParser.GetString(args, "gameObjectPath", null);
            int materialIndex = ArgumentParser.GetInt(args, "materialIndex", 0);

            // Validate path for security
            if (!string.IsNullOrEmpty(materialPath))
            {
                var (sanitized, sanitizeErr) = TrySanitizePath(materialPath, "material path");
                if (sanitizeErr != null) return sanitizeErr;
                materialPath = sanitized;
            }

            // Need at least one source
            if (string.IsNullOrEmpty(materialPath) && string.IsNullOrEmpty(gameObjectPath))
            {
                return McpToolResult.Error("Either materialPath or gameObjectPath is required");
            }

            var material = MaterialHelpers.FindMaterial(materialPath, gameObjectPath, materialIndex);
            if (material == null)
            {
                return McpToolResult.Error($"Material not found. Path: {materialPath}, GameObject: {gameObjectPath}");
            }

            var result = MaterialHelpers.SerializeMaterial(material);

            // Add asset path if available
            string assetPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(assetPath))
            {
                result["assetPath"] = assetPath;
            }

            return McpResponse.Success(result);
        }

        private static McpToolResult SetMaterial(Dictionary<string, object> args)
        {
            string materialPath = ArgumentParser.GetString(args, "materialPath", null);
            string gameObjectPath = ArgumentParser.GetString(args, "gameObjectPath", null);
            int materialIndex = ArgumentParser.GetInt(args, "materialIndex", 0);

            // Validate path for security
            if (!string.IsNullOrEmpty(materialPath))
            {
                var (sanitized, sanitizeErr) = TrySanitizePath(materialPath, "material path");
                if (sanitizeErr != null) return sanitizeErr;
                materialPath = sanitized;
            }

            if (string.IsNullOrEmpty(materialPath) && string.IsNullOrEmpty(gameObjectPath))
            {
                return McpToolResult.Error("Either materialPath or gameObjectPath is required");
            }

            var material = MaterialHelpers.FindMaterial(materialPath, gameObjectPath, materialIndex);
            if (material == null)
            {
                return McpToolResult.Error($"Material not found. Path: {materialPath}, GameObject: {gameObjectPath}");
            }

            // Record for undo
            Undo.RecordObject(material, "MCP Modify Material");

            var modifiedProperties = new List<string>();

            // Detect render pipeline for shader compatibility
            string currentPipeline = MaterialHelpers.DetectRenderPipeline();

            // Change shader if specified
            var shaderName = ArgumentParser.GetString(args, "shader", null);
            if (shaderName != null)
            {
                // Auto-convert Standard shader for URP/HDRP projects
                if (shaderName == "Standard" && currentPipeline != "BuiltIn")
                {
                    shaderName = MaterialHelpers.GetDefaultShaderName();
                    McpDebug.Log($"[MCP SetMaterial] Auto-converting 'Standard' to '{shaderName}' for {currentPipeline}");
                }

                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    material.shader = shader;
                    modifiedProperties.Add($"shader ({shaderName})");
                }
                else
                {
                    McpDebug.LogWarning($"[MCP Unity] Shader not found: {shaderName}");
                }
            }

            // Set render queue if specified
            if (args.ContainsKey("renderQueue") && args["renderQueue"] != null)
            {
                material.renderQueue = ArgumentParser.GetInt(args, "renderQueue", material.renderQueue);
                modifiedProperties.Add("renderQueue");
            }

            // Set properties (with automatic property name mapping for URP/HDRP)
            if (args.TryGetValue("properties", out var propsObj) && propsObj is Dictionary<string, object> properties)
            {
                foreach (var kvp in properties)
                {
                    // Map property names if needed (e.g., _Color -> _BaseColor for URP)
                    string mappedPropertyName = MaterialHelpers.MapPropertyName(kvp.Key, currentPipeline);

                    if (MaterialHelpers.SetPropertyValue(material, mappedPropertyName, kvp.Value))
                    {
                        modifiedProperties.Add(kvp.Key + (mappedPropertyName != kvp.Key ? $" (mapped to {mappedPropertyName})" : ""));
                    }
                    else if (mappedPropertyName != kvp.Key)
                    {
                        // Try original name if mapping failed
                        if (MaterialHelpers.SetPropertyValue(material, kvp.Key, kvp.Value))
                        {
                            modifiedProperties.Add(kvp.Key);
                        }
                    }
                }
            }

            // Mark as dirty to save changes
            EditorUtility.SetDirty(material);

            return McpResponse.Success(new Dictionary<string, object>
            {
                ["success"] = true,
                ["material"] = material.name,
                ["modifiedProperties"] = modifiedProperties,
                ["message"] = $"Modified {modifiedProperties.Count} properties on {material.name}"
            });
        }

        private static McpToolResult CreateMaterial(Dictionary<string, object> args)
        {
            var (materialName, nameErr) = RequireArg(args, "name");
            if (nameErr != null) return nameErr;

            var (savePathRaw, pathErr) = RequireArg(args, "savePath");
            if (pathErr != null) return pathErr;

            var (savePath, savePathSanitizeErr) = TrySanitizePath(savePathRaw, "save path");
            if (savePathSanitizeErr != null) return savePathSanitizeErr;

            // Ensure path ends with .mat
            if (!savePath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                savePath = savePath + ".mat";
            }

            // Detect render pipeline
            string renderPipeline = MaterialHelpers.DetectRenderPipeline();

            // Get shader name - use pipeline-appropriate default if not specified or "Standard"
            string shaderName = MaterialHelpers.GetDefaultShaderName();
            string requestedShader = ArgumentParser.GetString(args, "shader", null);
            if (requestedShader != null)
            {
                // If user requested "Standard" but we're in URP/HDRP, auto-convert
                if (requestedShader == "Standard" && renderPipeline != "BuiltIn")
                {
                    shaderName = MaterialHelpers.GetDefaultShaderName();
                    McpDebug.Log($"[MCP CreateMaterial] Auto-converting 'Standard' to '{shaderName}' for {renderPipeline}");
                }
                else
                {
                    shaderName = requestedShader;
                }
            }

            // Create the material
            var material = MaterialHelpers.CreateMaterial(shaderName);
            if (material == null)
            {
                return McpToolResult.Error($"Failed to create material: shader '{shaderName}' not found");
            }
            material.name = materialName;

            // Set initial properties if provided (with property name mapping for URP/HDRP)
            var setProperties = new List<string>();
            if (args.TryGetValue("properties", out var propsObj) && propsObj is Dictionary<string, object> properties)
            {
                foreach (var kvp in properties)
                {
                    // Map property names if needed (e.g., _Color -> _BaseColor for URP)
                    string mappedPropertyName = MaterialHelpers.MapPropertyName(kvp.Key, renderPipeline);

                    if (MaterialHelpers.SetPropertyValue(material, mappedPropertyName, kvp.Value))
                    {
                        setProperties.Add(kvp.Key + (mappedPropertyName != kvp.Key ? $" (mapped to {mappedPropertyName})" : ""));
                    }
                    else if (mappedPropertyName != kvp.Key)
                    {
                        // Try original name if mapping failed
                        if (MaterialHelpers.SetPropertyValue(material, kvp.Key, kvp.Value))
                        {
                            setProperties.Add(kvp.Key);
                        }
                    }
                }
            }

            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                // Create directories recursively
                var parts = directory.Split('/');
                var currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var parentPath = currentPath;
                    currentPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(currentPath))
                    {
                        AssetDatabase.CreateFolder(parentPath, parts[i]);
                    }
                }
            }

            // Save as asset
            AssetDatabase.CreateAsset(material, savePath);
            AssetDatabase.SaveAssets();

            return McpResponse.Success(new Dictionary<string, object>
            {
                ["success"] = true,
                ["name"] = materialName,
                ["path"] = savePath,
                ["shader"] = material.shader.name,
                ["renderPipeline"] = renderPipeline,
                ["propertiesSet"] = setProperties,
                ["message"] = $"Created material '{materialName}' with {material.shader.name} shader ({renderPipeline}) at {savePath}"
            });
        }

        #endregion
    }
}
