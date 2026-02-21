using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using McpUnity.Protocol;
using McpUnity.Helpers;

namespace McpUnity.Server
{
    /// <summary>
    /// Partial class containing Unity Package Manager tools
    /// Tools: list_packages, add_package, remove_package
    /// </summary>
    public partial class McpUnityServer
    {
        #region Package Manager Tool Registrations

        static partial void RegisterPackageManagerTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_packages",
                description = "List all installed packages in the Unity project",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["includeBuiltIn"] = new McpPropertySchema
                        {
                            type = "boolean",
                            description = "Include built-in Unity packages (default: false)"
                        }
                    }
                }
            }, ListPackages);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_add_package",
                description = "Add a package to the Unity project via Package Manager",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["packageId"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Package identifier (e.g., 'com.unity.cinemachine', 'com.unity.cinemachine@2.9.7')"
                        }
                    },
                    required = new List<string> { "packageId" }
                }
            }, AddPackage);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_remove_package",
                description = "Remove a package from the Unity project via Package Manager",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["packageId"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Package identifier to remove (e.g., 'com.unity.cinemachine')"
                        }
                    },
                    required = new List<string> { "packageId" }
                }
            }, RemovePackage);
        }

        #endregion

        #region Package Manager Handlers

        private static McpToolResult ListPackages(Dictionary<string, object> args)
        {
            try
            {
                bool includeBuiltIn = ArgumentParser.GetBool(args, "includeBuiltIn", false);

                // Client.List is asynchronous — we poll synchronously with a timeout
                var request = Client.List(offlineMode: false, includeIndirectDependencies: false);

                double startTime = EditorApplication.timeSinceStartup;
                const double timeoutSeconds = 15.0;

                while (!request.IsCompleted)
                {
                    if (EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
                        return McpToolResult.Error("Timed out waiting for Package Manager to respond. Try again.");

                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == StatusCode.Failure)
                    return McpToolResult.Error($"Package Manager error: {request.Error?.message ?? "Unknown error"}");

                var packages = new List<object>();
                foreach (var pkg in request.Result)
                {
                    if (!includeBuiltIn && pkg.source == PackageSource.BuiltIn) continue;

                    packages.Add(new
                    {
                        name = pkg.name,
                        displayName = pkg.displayName,
                        version = pkg.version,
                        source = pkg.source.ToString(),
                        description = pkg.description
                    });
                }

                return McpResponse.Success(new
                {
                    count = packages.Count,
                    packages = packages
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to list packages: {ex.Message}");
            }
        }

        private static McpToolResult AddPackage(Dictionary<string, object> args)
        {
            try
            {
                var (packageId, packageErr) = RequireArg(args, "packageId");
                if (packageErr != null) return packageErr;

                var request = Client.Add(packageId);

                double startTime = EditorApplication.timeSinceStartup;
                const double timeoutSeconds = 60.0;

                while (!request.IsCompleted)
                {
                    if (EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
                        return McpToolResult.Error($"Timed out adding package '{packageId}'. The operation may still complete in the background.");

                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == StatusCode.Failure)
                    return McpToolResult.Error($"Failed to add package '{packageId}': {request.Error?.message ?? "Unknown error"}");

                var pkg = request.Result;
                return McpResponse.Success(new
                {
                    success = true,
                    name = pkg.name,
                    displayName = pkg.displayName,
                    version = pkg.version,
                    message = $"Package '{pkg.displayName}' ({pkg.version}) added successfully."
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to add package: {ex.Message}");
            }
        }

        private static McpToolResult RemovePackage(Dictionary<string, object> args)
        {
            try
            {
                var (packageId, packageErr) = RequireArg(args, "packageId");
                if (packageErr != null) return packageErr;

                var request = Client.Remove(packageId);

                double startTime = EditorApplication.timeSinceStartup;
                const double timeoutSeconds = 30.0;

                while (!request.IsCompleted)
                {
                    if (EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
                        return McpToolResult.Error($"Timed out removing package '{packageId}'. The operation may still complete in the background.");

                    System.Threading.Thread.Sleep(200);
                }

                if (request.Status == StatusCode.Failure)
                    return McpToolResult.Error($"Failed to remove package '{packageId}': {request.Error?.message ?? "Unknown error"}");

                return McpToolResult.Success($"Package '{packageId}' removed successfully.");
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to remove package: {ex.Message}");
            }
        }

        #endregion
    }
}
