using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using McpUnity.Protocol;
using McpUnity.Helpers;
using McpUnity.Utils;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Server
{
    /// <summary>
    /// Script management tools for MCP Unity Server.
    /// Contains 5 tools: CreateScript, ReadScript, GetScriptInfo, WriteScript, UpdateScript
    /// </summary>
    public partial class McpUnityServer
    {
        // SEC-04: Regex for valid C# identifiers — blocks code injection via scriptName/namespace
        private static readonly Regex ValidCSharpIdentifier = new Regex(
            @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$",
            RegexOptions.Compiled);

        // H-03: Cache type lookups to avoid iterating all assemblies on every call (can be very slow).
        // Sentinel: typeof(void) means "looked up and not found" — avoids repeated full scans.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> _typeCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, Type>();
        private static readonly Type _typeCacheNotFound = typeof(void);

        // H-03: Prefixes of assemblies to skip during type search (Unity internals, mscorlib, etc.)
        private static readonly string[] _skipAssemblyPrefixes = {
            "mscorlib", "System", "UnityEngine", "UnityEditor", "Unity.", "Mono.",
            "nunit", "netstandard", "Microsoft.", "JetBrains.", "ExCSS", "Bee.",
            "WebSocketSharp", "Newtonsoft"
        };

        private const int MaxScriptSizeBytes = 100 * 1024; // 100 KB limit

        /// <summary>
        /// Validate a C# identifier (class name, namespace) against injection attacks.
        /// </summary>
        private static string ValidateIdentifier(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return $"{paramName} cannot be empty";
            if (!ValidCSharpIdentifier.IsMatch(value))
                return $"{paramName} '{value}' is not a valid C# identifier (letters, digits, underscores, dots for namespaces only)";
            if (value.Length > 256)
                return $"{paramName} is too long (max 256 characters)";
            return null; // valid
        }

        /// <summary>
        /// Register all script-related tools
        /// </summary>
        static partial void RegisterScriptTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_create_script",
                description = "Create a C# script file from template (MonoBehaviour, ScriptableObject, or EditorWindow)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["scriptName"] = new McpPropertySchema { type = "string", description = "Name of the script class" },
                        ["savePath"] = new McpPropertySchema { type = "string", description = "Save path (e.g. 'Assets/Scripts/MyScript.cs')" },
                        ["scriptType"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Script base type: MonoBehaviour, ScriptableObject, EditorWindow",
                            @enum = new List<string> { "MonoBehaviour", "ScriptableObject", "EditorWindow" },
                            @default = "MonoBehaviour"
                        },
                        ["namespace"] = new McpPropertySchema { type = "string", description = "Optional namespace to wrap the class in" },
                        ["methods"] = new McpPropertySchema { type = "array", description = "Method stubs to include (e.g. Start, Update, Awake, OnEnable, OnDisable, OnDestroy, FixedUpdate, LateUpdate, OnCollisionEnter, OnTriggerEnter)" }
                    },
                    required = new List<string> { "scriptName", "savePath" }
                }
            }, HandleCreateScript);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_read_script",
                description = "Read the contents of a C# script file in the project",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["scriptPath"] = new McpPropertySchema { type = "string", description = "Path to the .cs file (must be within Assets/)" }
                    },
                    required = new List<string> { "scriptPath" }
                }
            }, HandleReadScript);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_get_script_info",
                description = "Get reflection info (fields, properties, methods) for a C# type by name",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["typeName"] = new McpPropertySchema { type = "string", description = "Type name (e.g. 'PlayerController' or 'MyNamespace.PlayerController')" }
                    },
                    required = new List<string> { "typeName" }
                }
            }, HandleGetScriptInfo);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_write_script",
                description = "Write a complete C# script file with arbitrary content (with backup and compilation guard)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["filePath"] = new McpPropertySchema { type = "string", description = "Destination path (must be within Assets/, must end with .cs)" },
                        ["content"] = new McpPropertySchema { type = "string", description = "Full C# script content to write" },
                        ["overwrite"] = new McpPropertySchema { type = "boolean", description = "Allow overwriting existing file (default: false)", @default = false },
                        ["dryRun"] = new McpPropertySchema { type = "boolean", description = "If true, validate only without writing (default: false)", @default = false },
                        ["createBackup"] = new McpPropertySchema { type = "boolean", description = "Create .bak backup before overwriting (default: true)", @default = true }
                    },
                    required = new List<string> { "filePath", "content" }
                }
            }, HandleWriteScript);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_update_script",
                description = "Update a C# script by replacing a specific section (find-and-replace, must match exactly once)",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["filePath"] = new McpPropertySchema { type = "string", description = "Path to existing .cs file (must be within Assets/)" },
                        ["oldContent"] = new McpPropertySchema { type = "string", description = "Exact text to find in the file (must match exactly once)" },
                        ["newContent"] = new McpPropertySchema { type = "string", description = "Replacement text" },
                        ["dryRun"] = new McpPropertySchema { type = "boolean", description = "If true, validate match without writing (default: false)", @default = false },
                        ["createBackup"] = new McpPropertySchema { type = "boolean", description = "Create .bak backup before modifying (default: true)", @default = true }
                    },
                    required = new List<string> { "filePath", "oldContent", "newContent" }
                }
            }, HandleUpdateScript);
        }

        #region Script Handlers

        private static McpToolResult HandleCreateScript(Dictionary<string, object> args)
        {
            try
            {
                var (scriptName, scriptNameErr) = RequireArg(args, "scriptName");
                if (scriptNameErr != null) return scriptNameErr;

                // SEC-04: Validate scriptName is a safe C# identifier
                var nameValidation = ValidateIdentifier(scriptName, "scriptName");
                if (nameValidation != null) return McpToolResult.Error(nameValidation);

                var (rawSavePath, savePathArgErr) = RequireArg(args, "savePath");
                if (savePathArgErr != null) return savePathArgErr;

                var (savePath, savePathErr) = TrySanitizePath(rawSavePath, "save path");
                if (savePathErr != null) return savePathErr;

                // Ensure .cs extension
                if (!savePath.EndsWith(".cs"))
                    savePath += ".cs";

                string scriptType = ArgumentParser.GetString(args, "scriptType", "MonoBehaviour");
                string namespaceName = ArgumentParser.GetString(args, "namespace", null);

                // SEC-04: Validate namespace if provided
                if (!string.IsNullOrEmpty(namespaceName))
                {
                    var nsValidation = ValidateIdentifier(namespaceName, "namespace");
                    if (nsValidation != null) return McpToolResult.Error(nsValidation);
                }
                string[] methods = ArgumentParser.GetStringArray(args, "methods");

                // Generate script content
                string content = GenerateScriptContent(scriptName, scriptType, namespaceName, methods);
                if (content == null)
                {
                    return McpToolResult.Error($"Unknown script type: '{scriptType}'. Valid types: MonoBehaviour, ScriptableObject, EditorWindow");
                }

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Write file
                System.IO.File.WriteAllText(savePath, content);
                AssetDatabase.Refresh();
                // The new script class will now be visible to component tools
                InvalidateProjectScriptsCache();

                return McpResponse.Success($"Created {scriptType} script '{scriptName}' at {savePath}", new
                {
                    scriptName = scriptName,
                    savePath = savePath,
                    scriptType = scriptType,
                    namespaceName = namespaceName,
                    methodCount = methods.Length,
                    lineCount = CountLines(content)
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to create script: {ex.Message}");
            }
        }

        private static McpToolResult HandleReadScript(Dictionary<string, object> args)
        {
            try
            {
                var (rawScriptPath, scriptPathArgErr) = RequireArg(args, "scriptPath");
                if (scriptPathArgErr != null) return scriptPathArgErr;

                var (scriptPath, scriptPathErr) = TrySanitizePath(rawScriptPath, "script path");
                if (scriptPathErr != null) return scriptPathErr;

                if (!scriptPath.EndsWith(".cs"))
                {
                    return McpToolResult.Error("File must be a C# script (.cs extension)");
                }

                if (!System.IO.File.Exists(scriptPath))
                {
                    return McpToolResult.Error($"Script not found: {scriptPath}");
                }

                string content = System.IO.File.ReadAllText(scriptPath);
                var fileInfo = new System.IO.FileInfo(scriptPath);

                return McpResponse.Success(new
                {
                    scriptPath = scriptPath,
                    fileName = System.IO.Path.GetFileName(scriptPath),
                    content = content,
                    lineCount = CountLines(content),
                    fileSize = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to read script: {ex.Message}");
            }
        }

        private static McpToolResult HandleGetScriptInfo(Dictionary<string, object> args)
        {
            try
            {
                var (typeName, typeNameErr) = RequireArg(args, "typeName");
                if (typeNameErr != null) return typeNameErr;

                // H-03: Check cache first to avoid expensive full-assembly scan on repeat calls
                Type foundType = null;
                if (_typeCache.TryGetValue(typeName, out foundType))
                {
                    if (foundType == _typeCacheNotFound)
                        return McpToolResult.Error($"Type '{typeName}' not found in any loaded assembly");
                }
                else
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        // H-03: Skip Unity/system assemblies — user types are never there
                        string asmName = assembly.GetName().Name ?? "";
                        bool skip = false;
                        foreach (var prefix in _skipAssemblyPrefixes)
                        {
                            if (asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            { skip = true; break; }
                        }
                        if (skip) continue;

                        try
                        {
                            // Try exact match first (with namespace)
                            var type = assembly.GetType(typeName);
                            if (type != null)
                            {
                                foundType = type;
                                break;
                            }

                            // Try finding by simple name within user assemblies only
                            foreach (var t in assembly.GetTypes())
                            {
                                if (t.Name == typeName || t.FullName == typeName)
                                {
                                    foundType = t;
                                    break;
                                }
                            }

                            if (foundType != null) break;
                        }
                        catch
                        {
                            // Skip assemblies that fail to load types
                        }
                    }

                    // Cache result — sentinel for not found to avoid repeated full scans
                    _typeCache[typeName] = foundType ?? _typeCacheNotFound;
                }

                if (foundType == null || foundType == _typeCacheNotFound)
                {
                    return McpToolResult.Error($"Type '{typeName}' not found in any loaded assembly");
                }

                // Determine base types to filter out inherited members
                var excludeTypes = new HashSet<Type>
                {
                    typeof(UnityEngine.Object),
                    typeof(Component),
                    typeof(Behaviour),
                    typeof(MonoBehaviour),
                    typeof(ScriptableObject),
                    typeof(object)
                };

                // Collect fields (declared only, exclude inherited from Unity base types)
                var fields = foundType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(f => new Dictionary<string, object>
                    {
                        ["name"] = f.Name,
                        ["type"] = GetFriendlyTypeName(f.FieldType),
                        ["attributes"] = f.GetCustomAttributes(false)
                            .Select(a => a.GetType().Name.Replace("Attribute", ""))
                            .ToArray()
                    })
                    .ToList();

                // Also include private fields with [SerializeField]
                var serializedFields = foundType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(f => f.GetCustomAttribute<SerializeField>() != null)
                    .Select(f => new Dictionary<string, object>
                    {
                        ["name"] = f.Name,
                        ["type"] = GetFriendlyTypeName(f.FieldType),
                        ["isPrivate"] = true,
                        ["attributes"] = f.GetCustomAttributes(false)
                            .Select(a => a.GetType().Name.Replace("Attribute", ""))
                            .ToArray()
                    })
                    .ToList();

                fields.AddRange(serializedFields);

                // Collect public properties (declared only)
                var properties = foundType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(p => new Dictionary<string, object>
                    {
                        ["name"] = p.Name,
                        ["type"] = GetFriendlyTypeName(p.PropertyType),
                        ["canRead"] = p.CanRead,
                        ["canWrite"] = p.CanWrite
                    })
                    .ToList();

                // Collect public methods (declared only, exclude property accessors)
                var methods = foundType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                    .Select(m => new Dictionary<string, object>
                    {
                        ["name"] = m.Name,
                        ["returnType"] = GetFriendlyTypeName(m.ReturnType),
                        ["parameters"] = m.GetParameters()
                            .Select(p => new Dictionary<string, object>
                            {
                                ["name"] = p.Name,
                                ["type"] = GetFriendlyTypeName(p.ParameterType)
                            })
                            .ToList()
                    })
                    .ToList();

                return McpResponse.Success(new
                {
                    typeName = foundType.Name,
                    fullName = foundType.FullName,
                    baseType = foundType.BaseType?.Name,
                    assembly = foundType.Assembly.GetName().Name,
                    isAbstract = foundType.IsAbstract,
                    isSealed = foundType.IsSealed,
                    fields = fields,
                    properties = properties,
                    methods = methods,
                    fieldCount = fields.Count,
                    propertyCount = properties.Count,
                    methodCount = methods.Count
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to get script info: {ex.Message}");
            }
        }

        private static McpToolResult HandleWriteScript(Dictionary<string, object> args)
        {
            try
            {
                var (rawFilePath, filePathArgErr) = RequireArg(args, "filePath");
                if (filePathArgErr != null) return filePathArgErr;

                var (content, contentErr) = RequireArg(args, "content");
                if (contentErr != null) return contentErr;

                bool overwrite = ArgumentParser.GetBool(args, "overwrite", false);
                bool dryRun = ArgumentParser.GetBool(args, "dryRun", false);
                bool createBackup = ArgumentParser.GetBool(args, "createBackup", true);

                // Validate path
                var (filePath, filePathErr) = TrySanitizePath(rawFilePath, "file path");
                if (filePathErr != null) return filePathErr;

                // Must be a .cs file
                if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return McpToolResult.Error("File must be a C# script (.cs extension)");

                // Size guard
                if (System.Text.Encoding.UTF8.GetByteCount(content) > MaxScriptSizeBytes)
                    return McpToolResult.Error($"Content exceeds maximum size limit ({MaxScriptSizeBytes / 1024} KB)");

                bool fileExists = System.IO.File.Exists(filePath);

                // Overwrite guard
                if (fileExists && !overwrite)
                    return McpToolResult.Error($"File already exists: {filePath}. Set overwrite=true to replace it.");

                // Dry run — validate only
                if (dryRun)
                {
                    return McpResponse.Success($"[DRY RUN] Would {(fileExists ? "overwrite" : "create")} {filePath}", new
                    {
                        filePath,
                        wouldOverwrite = fileExists,
                        contentLength = content.Length,
                        lineCount = CountLines(content)
                    });
                }

                // Create backup if overwriting
                string backupPath = null;
                if (fileExists && createBackup)
                {
                    backupPath = filePath + ".bak";
                    System.IO.File.Copy(filePath, backupPath, true);
                }

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);

                // Write file
                System.IO.File.WriteAllText(filePath, content);
                AssetDatabase.Refresh();

                return McpResponse.Success($"{(fileExists ? "Overwrote" : "Created")} script at {filePath}", new
                {
                    filePath,
                    wasOverwrite = fileExists,
                    backupPath,
                    contentLength = content.Length,
                    lineCount = CountLines(content)
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to write script: {ex.Message}");
            }
        }

        private static McpToolResult HandleUpdateScript(Dictionary<string, object> args)
        {
            try
            {
                var (rawFilePath, filePathArgErr) = RequireArg(args, "filePath");
                if (filePathArgErr != null) return filePathArgErr;

                var (oldContent, oldContentErr) = RequireArg(args, "oldContent");
                if (oldContentErr != null) return oldContentErr;

                var (newContent, newContentErr) = RequireArg(args, "newContent");
                if (newContentErr != null) return newContentErr;

                bool dryRun = ArgumentParser.GetBool(args, "dryRun", false);
                bool createBackup = ArgumentParser.GetBool(args, "createBackup", true);

                // Validate path
                var (filePath, filePathErr) = TrySanitizePath(rawFilePath, "file path");
                if (filePathErr != null) return filePathErr;

                // Must be a .cs file
                if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return McpToolResult.Error("File must be a C# script (.cs extension)");

                // File must exist
                if (!System.IO.File.Exists(filePath))
                    return McpToolResult.Error($"Script not found: {filePath}");

                // Read current content
                string currentContent = System.IO.File.ReadAllText(filePath);

                // Find occurrences — must match exactly once
                int firstIndex = currentContent.IndexOf(oldContent, StringComparison.Ordinal);
                if (firstIndex < 0)
                    return McpToolResult.Error("oldContent not found in file. Ensure the text matches exactly (whitespace and newlines matter).");

                int secondIndex = currentContent.IndexOf(oldContent, firstIndex + oldContent.Length, StringComparison.Ordinal);
                if (secondIndex >= 0)
                    return McpToolResult.Error("oldContent matches multiple locations in the file. Provide more surrounding context to make the match unique.");

                // Compute the line number of the match for reporting
                int lineNumber = currentContent.Substring(0, firstIndex).Count(c => c == '\n') + 1;

                // Build updated content
                string updatedContent = currentContent.Substring(0, firstIndex)
                    + newContent
                    + currentContent.Substring(firstIndex + oldContent.Length);

                // Size guard on result
                if (System.Text.Encoding.UTF8.GetByteCount(updatedContent) > MaxScriptSizeBytes)
                    return McpToolResult.Error($"Updated content would exceed maximum size limit ({MaxScriptSizeBytes / 1024} KB)");

                // Dry run
                if (dryRun)
                {
                    return McpResponse.Success($"[DRY RUN] Would replace {oldContent.Length} chars at line {lineNumber} with {newContent.Length} chars in {filePath}", new
                    {
                        filePath,
                        matchLineNumber = lineNumber,
                        oldLength = oldContent.Length,
                        newLength = newContent.Length,
                        resultLineCount = CountLines(updatedContent)
                    });
                }

                // Create backup
                string backupPath = null;
                if (createBackup)
                {
                    backupPath = filePath + ".bak";
                    System.IO.File.Copy(filePath, backupPath, true);
                }

                // Write updated file
                System.IO.File.WriteAllText(filePath, updatedContent);
                AssetDatabase.Refresh();

                return McpResponse.Success($"Updated script at {filePath} (line {lineNumber})", new
                {
                    filePath,
                    backupPath,
                    matchLineNumber = lineNumber,
                    oldLength = oldContent.Length,
                    newLength = newContent.Length,
                        resultLineCount = CountLines(updatedContent)
                });
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to update script: {ex.Message}");
            }
        }

        #endregion

        #region Script Helpers

        /// <summary>
        /// L-01: Count lines without allocating a string array (avoids Split('\n') GC pressure).
        /// </summary>
        private static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int count = 1;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '\n') count++;
            return count;
        }

        private static string GenerateScriptContent(string scriptName, string scriptType, string namespaceName, string[] methods)
        {
            string classBody;

            switch (scriptType)
            {
                case "MonoBehaviour":
                    classBody = GenerateMonoBehaviourBody(scriptName, methods);
                    break;
                case "ScriptableObject":
                    classBody = GenerateScriptableObjectBody(scriptName, methods);
                    break;
                case "EditorWindow":
                    classBody = GenerateEditorWindowBody(scriptName, methods);
                    break;
                default:
                    return null;
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                // Indent the class body inside the namespace
                var indentedBody = string.Join("\n",
                    classBody.Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? line : "    " + line));
                return $"namespace {namespaceName}\n{{\n{indentedBody}\n}}\n";
            }

            return classBody;
        }

        private static string GenerateMonoBehaviourBody(string scriptName, string[] methods)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {scriptName} : MonoBehaviour");
            sb.AppendLine("{");

            if (methods != null && methods.Length > 0)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    if (i > 0) sb.AppendLine();
                    AppendMethodStub(sb, methods[i], "    ");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateScriptableObjectBody(string scriptName, string[] methods)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"[CreateAssetMenu(fileName = \"New{scriptName}\", menuName = \"{scriptName}\")]");
            sb.AppendLine($"public class {scriptName} : ScriptableObject");
            sb.AppendLine("{");

            if (methods != null && methods.Length > 0)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    if (i > 0) sb.AppendLine();
                    AppendMethodStub(sb, methods[i], "    ");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateEditorWindowBody(string scriptName, string[] methods)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine();
            sb.AppendLine($"public class {scriptName} : EditorWindow");
            sb.AppendLine("{");
            sb.AppendLine($"    [MenuItem(\"Tools/{scriptName}\")]");
            sb.AppendLine("    public static void ShowWindow()");
            sb.AppendLine("    {");
            sb.AppendLine($"        GetWindow<{scriptName}>(\"{scriptName}\");");
            sb.AppendLine("    }");

            if (methods != null && methods.Length > 0)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    sb.AppendLine();
                    AppendMethodStub(sb, methods[i], "    ");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendMethodStub(System.Text.StringBuilder sb, string methodName, string indent)
        {
            // Determine method signature based on known Unity methods
            string returnType = "void";
            string parameters = "";

            switch (methodName)
            {
                case "OnCollisionEnter":
                    parameters = "Collision collision";
                    break;
                case "OnCollisionExit":
                    parameters = "Collision collision";
                    break;
                case "OnCollisionStay":
                    parameters = "Collision collision";
                    break;
                case "OnCollisionEnter2D":
                    parameters = "Collision2D collision";
                    break;
                case "OnCollisionExit2D":
                    parameters = "Collision2D collision";
                    break;
                case "OnCollisionStay2D":
                    parameters = "Collision2D collision";
                    break;
                case "OnTriggerEnter":
                    parameters = "Collider other";
                    break;
                case "OnTriggerExit":
                    parameters = "Collider other";
                    break;
                case "OnTriggerStay":
                    parameters = "Collider other";
                    break;
                case "OnTriggerEnter2D":
                    parameters = "Collider2D other";
                    break;
                case "OnTriggerExit2D":
                    parameters = "Collider2D other";
                    break;
                case "OnTriggerStay2D":
                    parameters = "Collider2D other";
                    break;
                case "OnGUI":
                case "OnDrawGizmos":
                case "OnDrawGizmosSelected":
                case "OnValidate":
                case "Reset":
                case "Start":
                case "Awake":
                case "Update":
                case "FixedUpdate":
                case "LateUpdate":
                case "OnEnable":
                case "OnDisable":
                case "OnDestroy":
                case "OnApplicationQuit":
                case "OnApplicationPause":
                case "OnApplicationFocus":
                case "OnBecameVisible":
                case "OnBecameInvisible":
                    break;
                default:
                    // Unknown method — generate a simple void stub
                    break;
            }

            sb.AppendLine($"{indent}{returnType} {methodName}({parameters})");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    // TODO: Implement {methodName}");
            sb.AppendLine($"{indent}}}");
        }

        #endregion
    }
}
