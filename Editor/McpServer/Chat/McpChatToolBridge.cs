using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using McpUnity.Helpers;
using McpUnity.Protocol;
using McpUnity.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace McpUnity.Chat
{
    /// <summary>
    /// Bridges LLM tool_use calls to the MCP Tool Registry.
    /// Converts tool definitions, executes tools, and formats results.
    /// </summary>
    public class McpChatToolBridge
    {
        private const int MaxApiResultChars = 24000;
        private readonly McpToolRegistry _registry;

        /// <summary>
        /// Tools that require user confirmation before execution.
        /// These are destructive or high-impact operations where the user
        /// should explicitly approve before the LLM proceeds.
        /// </summary>
        private static readonly HashSet<string> ConfirmationRequiredTools = new HashSet<string>
        {
            // Destructive — data loss risk
            "unity_delete_gameobject",
            "unity_delete_asset",
            "unity_clear_baked_data",
            "unity_clear_navmesh",
            "unity_clear_occlusion",
            "unity_remove_terrain_trees",
            "unity_remove_terrain_detail",

            // Script creation/modification — modifies project files on disk
            "unity_write_script",
            "unity_create_script",
            "unity_update_script",

            // Potentially dangerous editor actions
            "unity_execute_menu_item",
            "unity_unpack_prefab",

            // Build/platform changes — can take a long time or break settings
            "unity_switch_platform",
            "unity_bake_lighting",
            "unity_bake_lighting_async",
            "unity_bake_navmesh",
            "unity_bake_occlusion",
        };

        /// <summary>Check whether a tool requires user confirmation before execution.</summary>
        public static bool RequiresConfirmation(string toolName)
        {
            return ConfirmationRequiredTools.Contains(toolName);
        }

        public McpChatToolBridge(McpToolRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>Exposes the underlying registry for null-check in GetToolBridge().</summary>
        public McpToolRegistry Registry => _registry;

        #region Tool Definitions → API Format

        /// <summary>
        /// Get MCP tool definitions in native (Anthropic) format.
        /// Provider-specific conversion happens inside each provider's BuildRequestBody().
        /// </summary>
        public List<object> GetToolDefinitions()
        {
            if (_registry == null) return new List<object>();

            // The embedded chat sends ALL tools to the LLM regardless of category state.
            // Unlike the MCP server (which filters to save client tokens), the chat panel
            // benefits from having the full tool set available so the LLM never falls back
            // to writing scripts for tasks that tools can handle directly.
            var allTools = _registry.GetAllTools();
            var result = new List<object>(allTools.Count);

            foreach (var tool in allTools)
                result.Add(ConvertToolDefinition(tool));

            return result;
        }

        private object ConvertToolDefinition(McpToolDefinition tool)
        {
            var schema = new Dictionary<string, object> { ["type"] = "object" };

            if (tool.inputSchema?.properties != null && tool.inputSchema.properties.Count > 0)
            {
                var props = new Dictionary<string, object>();
                foreach (var kvp in tool.inputSchema.properties)
                {
                    var propDef = new Dictionary<string, object> { ["type"] = kvp.Value.type ?? "string" };
                    if (!string.IsNullOrEmpty(kvp.Value.description))
                        propDef["description"] = kvp.Value.description;
                    if (kvp.Value.@enum != null && kvp.Value.@enum.Count > 0)
                        propDef["enum"] = kvp.Value.@enum;
                    props[kvp.Key] = propDef;
                }
                schema["properties"] = props;
            }
            else
            {
                schema["properties"] = new Dictionary<string, object>();
            }

            if (tool.inputSchema?.required != null && tool.inputSchema.required.Count > 0)
                schema["required"] = tool.inputSchema.required;

            return new Dictionary<string, object>
            {
                ["name"] = tool.name,
                ["description"] = tool.description ?? "",
                ["input_schema"] = schema
            };
        }

        /// <summary>Number of tools that will be sent to the API (all tools for the chat panel).</summary>
        public int ActiveToolCount
        {
            get
            {
                if (_registry == null) return 0;
                return _registry.Count;
            }
        }

        #endregion

        #region Tool Execution

        /// <summary>
        /// Execute a tool_use block from Claude's response.
        /// Returns a tool_result content block for the next API call.
        /// </summary>
        public ToolResultContent ExecuteToolUse(ToolUseContent toolUse)
        {
            if (_registry == null || toolUse == null)
            {
                return new ToolResultContent
                {
                    tool_use_id = toolUse?.id ?? "unknown",
                    content = "Tool registry not available",
                    is_error = true
                };
            }

            // Parse raw JSON input if accumulated from streaming
            var args = ResolveToolInput(toolUse);

            try
            {
                McpUnity.Editor.McpDebug.Log($"[Chat] Executing tool: {toolUse.name}");
                var result = _registry.ExecuteTool(toolUse.name, args);

                // Extract text content from McpToolResult
                string resultText = ExtractResultText(result);
                bool isError = result?.isError ?? false;

                return new ToolResultContent
                {
                    tool_use_id = toolUse.id,
                    content = resultText,
                    is_error = isError
                };
            }
            catch (Exception ex)
            {
                return new ToolResultContent
                {
                    tool_use_id = toolUse.id,
                    content = $"Tool execution failed: {ex.Message}",
                    is_error = true
                };
            }
        }

        /// <summary>
        /// Resolve the final tool input dictionary for a ToolUseContent.
        /// During SSE streaming Anthropic sends arguments via input_json_delta into rawJsonBuilder.
        /// The toolUse.input dict is populated here from the fully-accumulated JSON.
        /// After this call toolUse.input always contains the parsed arguments (cached for reuse).
        /// </summary>
        public Dictionary<string, object> ResolveToolInput(ToolUseContent toolUse)
        {
            if (toolUse == null)
                return new Dictionary<string, object>();

            // Parse raw JSON once from rawJsonBuilder and cache into toolUse.input
            if (toolUse.rawJsonBuilder != null && toolUse.rawJsonBuilder.Length > 0)
            {
                try
                {
                    var parsed = JsonHelper.ParseJsonObject(toolUse.rawJsonBuilder.ToString());
                    if (parsed is Dictionary<string, object> dict)
                    {
                        // Cache into input so subsequent calls (BuildMessagesArray, SaveSession) get the parsed args
                        toolUse.input = dict;
                        toolUse.rawJsonBuilder = null; // Free memory — no longer needed
                        return dict;
                    }
                }
                catch (Exception ex)
                {
                    McpUnity.Editor.McpDebug.LogWarning($"[Chat] Failed to parse tool input JSON: {ex.Message}");
                }
                toolUse.rawJsonBuilder = null;
            }

            return toolUse.input ?? new Dictionary<string, object>();
        }

        private string ExtractResultText(McpToolResult result)
        {
            if (result?.content == null || result.content.Count == 0)
                return "(no output)";

            var sb = new StringBuilder();
            foreach (var block in result.content)
            {
                if (!string.IsNullOrEmpty(block.text))
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append(block.text);
                }
            }

            return SmartTruncate(sb.ToString(), MaxApiResultChars);
        }

        /// <summary>
        /// Truncate text at the last complete line before the limit.
        /// Preserves structural integrity (won't cut mid-JSON or mid-line).
        /// </summary>
        private static string SmartTruncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;

            // Find the last newline before the limit
            int cutPoint = text.LastIndexOf('\n', maxChars - 1);
            // If no reasonable newline found (less than half the limit), cut at limit
            if (cutPoint < maxChars / 2)
                cutPoint = maxChars;

            return text.Substring(0, cutPoint) + $"\n...(truncated, {text.Length:N0} chars total)";
        }

        #endregion

        #region Context Injection

        /// <summary>
        /// Build a system prompt with rich Unity project context from the background indexer.
        /// Includes hierarchy overview, asset counts, script index, and selected object details.
        /// </summary>
        public string BuildSystemPrompt(string userSystemPrompt = null)
        {
            var sb = new StringBuilder();

            // Identity — provider-agnostic
            sb.AppendLine("You are an AI assistant embedded in the Unity Editor via MCP Unity.");
            sb.AppendLine("You have direct access to the open Unity project through tool calls.");
            sb.AppendLine();

            // Render pipeline context (useful for material/shader/lighting guidance)
            string pipeline = "Built-in";
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                string pipelineName = GraphicsSettings.defaultRenderPipeline.GetType().Name;
                if (pipelineName.Contains("Universal") || pipelineName.Contains("URP"))
                    pipeline = "URP";
                else if (pipelineName.Contains("HDR") || pipelineName.Contains("HDRP"))
                    pipeline = "HDRP";
                else
                    pipeline = pipelineName;
            }

            // Rich project context from background indexer
            sb.AppendLine("## Project Context");
            sb.AppendLine($"- **Unity**: {Application.unityVersion} | **Pipeline**: {pipeline}");
            if (McpContextIndex.IsReady)
            {
                sb.AppendLine(McpContextIndex.GetContextSnapshot());
            }
            else
            {
                // Fallback: minimal context while indexer initializes
                var scene = SceneManager.GetActiveScene();
                sb.AppendLine($"- **Scene**: \"{scene.name}\" ({scene.path})");
                sb.AppendLine("- (Context index initializing...)");
            }
            sb.AppendLine();

            // Current editor state (always live, not cached)
            sb.AppendLine("## Editor State");
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                sb.Append($"- **Selected**: `{selected.name}` (path: `{GetGameObjectPath(selected)}`)");
                var components = selected.GetComponents<Component>();
                if (components.Length > 0)
                {
                    sb.Append(" — [");
                    for (int i = 0; i < components.Length && i < 10; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(components[i]?.GetType().Name ?? "null");
                    }
                    sb.Append("]");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("- **Selected**: (none)");
            }

            sb.AppendLine($"- **Play mode**: {EditorApplication.isPlaying}");
            sb.AppendLine($"- **Compiling**: {EditorApplication.isCompiling}");
            sb.AppendLine();

            // Operating guidelines
            sb.AppendLine("## Guidelines");
            sb.AppendLine("- **Prefer MCP tools** for creating objects, positioning, adding components, configuring properties, managing assets, and all discrete scene operations.");
            sb.AppendLine("- **Use `unity_write_script` when appropriate**: runtime game logic (MonoBehaviours, controllers), procedural generation (terrain painting, noise-based placement, spline paths), and complex algorithms that would require hundreds of tool calls.");
            sb.AppendLine("- **Do NOT write scripts for tasks tools handle directly**: creating/moving/deleting GameObjects, adding components, setting properties, searching assets, creating materials. Use the available tools instead.");
            sb.AppendLine("- If a tool call fails, **fix the parameters and retry** — do not switch to script generation as a workaround.");
            sb.AppendLine("- Read before writing: use `unity_get_component` or `unity_list_gameobjects` to inspect current state before making changes.");
            sb.AppendLine("- Verify paths: confirm a GameObject exists at the expected path before modifying it.");
            sb.AppendLine("- Prefer `unity_undo` over manual revert — all tool operations are recorded on the Unity undo stack.");
            sb.AppendLine("- Asset search supports Unity filter syntax: `t:Prefab`, `t:Material`, `l:Label`, or plain name.");
            sb.AppendLine("- **Destructive operations require user approval**: deleting objects/assets, writing/creating/modifying scripts, unpacking prefabs, baking, and platform switches will prompt the user for confirmation. If denied, explain why the operation was needed or suggest alternatives.");
            if (EditorApplication.isCompiling)
                sb.AppendLine("- **Scripts are compiling** — avoid tool calls that modify scripts or trigger recompilation until compilation finishes.");
            if (EditorApplication.isPlaying)
                sb.AppendLine("- **Play mode is active** — scene changes will be lost when play mode ends. Prefer runtime inspection over structural edits.");

            // User custom system prompt
            if (!string.IsNullOrEmpty(userSystemPrompt))
            {
                sb.AppendLine();
                sb.AppendLine("## Additional Instructions");
                sb.AppendLine(userSystemPrompt);
            }

            return sb.ToString();
        }

        private static string GetGameObjectPath(GameObject go)
            => GameObjectHelpers.GetGameObjectPath(go);

        #endregion

        #region Asset Reference Resolution

        /// <summary>
        /// Build an enriched user message text that includes resolved context
        /// for any referenced assets or GameObjects.
        /// The display text (what the user sees) stays unchanged;
        /// the enriched text is what gets sent to the LLM.
        /// </summary>
        public string BuildEnrichedUserText(string userText, List<AssetReference> references)
        {
            if (references == null || references.Count == 0)
                return userText;

            var sb = new StringBuilder(userText);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("[Referenced Assets/Objects]");

            for (int i = 0; i < references.Count; i++)
            {
                var assetRef = references[i];
                string context = assetRef.isSceneObject
                    ? ResolveGameObjectContext(assetRef)
                    : ResolveAssetContext(assetRef);

                sb.AppendLine(context);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolve context for a project asset (Material, Texture, Prefab, etc.)
        /// </summary>
        private string ResolveAssetContext(AssetReference assetRef)
        {
            var sb = new StringBuilder();
            sb.Append($"@{assetRef.displayName} -> {assetRef.assetPath} ({assetRef.typeName})");

            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetRef.assetPath);
                if (asset == null)
                {
                    sb.Append(" [not found]");
                    return sb.ToString();
                }

                if (asset is Material mat)
                {
                    sb.AppendLine();
                    sb.Append($"  Shader: {(mat.shader != null ? mat.shader.name : "null")}");
                    // List key properties (limited to avoid token bloat)
                    var propCount = mat.shader != null ? mat.shader.GetPropertyCount() : 0;
                    int listed = 0;
                    for (int p = 0; p < propCount && listed < 8; p++)
                    {
                        var propName = mat.shader.GetPropertyName(p);
                        var propType = mat.shader.GetPropertyType(p);
                        string val = "";
                        switch (propType)
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                var c = mat.GetColor(propName);
                                val = $"#{ColorUtility.ToHtmlStringRGBA(c)}";
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                val = mat.GetFloat(propName).ToString("F2");
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                var tex = mat.GetTexture(propName);
                                val = tex != null ? tex.name : "none";
                                break;
                            default:
                                continue;
                        }
                        sb.AppendLine();
                        sb.Append($"  {propName}: {val}");
                        listed++;
                    }
                }
                else if (asset is Texture2D tex)
                {
                    sb.AppendLine();
                    sb.Append($"  {tex.width}x{tex.height}, format: {tex.format}, mipmaps: {tex.mipmapCount}");
                }
                else if (asset is AudioClip audio)
                {
                    sb.AppendLine();
                    sb.Append($"  {audio.length:F1}s, {audio.channels}ch, {audio.frequency}Hz");
                }
                else if (asset is Mesh mesh)
                {
                    sb.AppendLine();
                    sb.Append($"  {mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris, {mesh.subMeshCount} submeshes");
                }
                else if (asset is GameObject prefab)
                {
                    // Prefab asset
                    var components = prefab.GetComponents<Component>();
                    sb.AppendLine();
                    sb.Append("  Components: [");
                    for (int c = 0; c < components.Length && c < 10; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        sb.Append(components[c]?.GetType().Name ?? "null");
                    }
                    sb.Append("]");
                    if (prefab.transform.childCount > 0)
                    {
                        sb.AppendLine();
                        sb.Append($"  Children: {prefab.transform.childCount}");
                    }
                }
                else if (asset is ScriptableObject so)
                {
                    sb.AppendLine();
                    sb.Append($"  ScriptableObject class: {so.GetType().FullName}");
                }
                else if (asset is MonoScript script)
                {
                    var scriptClass = script.GetClass();
                    sb.AppendLine();
                    sb.Append($"  Class: {(scriptClass != null ? scriptClass.FullName : script.name)}");
                }

                // Add direct dependencies (limited)
                var deps = AssetDatabase.GetDependencies(assetRef.assetPath, false);
                var filteredDeps = deps.Where(d => d != assetRef.assetPath).Take(5).ToArray();
                if (filteredDeps.Length > 0)
                {
                    sb.AppendLine();
                    sb.Append("  Deps: " + string.Join(", ", filteredDeps.Select(System.IO.Path.GetFileName)));
                }
            }
            catch (Exception ex)
            {
                sb.Append($" [error: {ex.Message}]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolve context for a scene GameObject (hierarchy path, components, transform).
        /// </summary>
        private string ResolveGameObjectContext(AssetReference assetRef)
        {
            var sb = new StringBuilder();
            sb.Append($"@{assetRef.displayName} -> scene object at \"{assetRef.gameObjectPath}\"");

            try
            {
                var go = GameObjectHelpers.FindGameObject(assetRef.gameObjectPath);
                if (go == null)
                {
                    sb.Append(" [not found in scene]");
                    return sb.ToString();
                }

                // Components
                var components = go.GetComponents<Component>();
                sb.AppendLine();
                sb.Append("  Components: [");
                for (int i = 0; i < components.Length && i < 12; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(components[i]?.GetType().Name ?? "null");
                }
                sb.Append("]");

                // Transform
                var t = go.transform;
                sb.AppendLine();
                sb.Append($"  Position: ({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})");
                if (t.localScale != Vector3.one)
                    sb.Append($"  Scale: ({t.localScale.x:F2}, {t.localScale.y:F2}, {t.localScale.z:F2})");

                // Children summary
                if (t.childCount > 0)
                {
                    sb.AppendLine();
                    sb.Append($"  Children ({t.childCount}): ");
                    for (int i = 0; i < t.childCount && i < 6; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(t.GetChild(i).name);
                    }
                    if (t.childCount > 6) sb.Append("...");
                }

                // Tag/Layer
                if (go.tag != "Untagged" || go.layer != 0)
                {
                    sb.AppendLine();
                    if (go.tag != "Untagged") sb.Append($"  Tag: {go.tag}");
                    if (go.layer != 0) sb.Append($"  Layer: {LayerMask.LayerToName(go.layer)}");
                }
            }
            catch (Exception ex)
            {
                sb.Append($" [error: {ex.Message}]");
            }

            return sb.ToString();
        }

        #endregion

        #region Message Building Helpers

        /// <summary>
        /// Build the messages array for the API call, including tool results.
        /// Validates tool_use/tool_result pairing to prevent API errors after
        /// session restore or conversation truncation.
        /// </summary>
        public List<object> BuildMessagesArray(List<ChatMessage> conversation)
        {
            // Collect all tool_use IDs from assistant messages
            var toolUseIds = new HashSet<string>();
            foreach (var msg in conversation)
            {
                if (msg.role != "assistant") continue;
                foreach (var block in msg.content)
                {
                    if (block is ToolUseContent tu && !string.IsNullOrEmpty(tu.id))
                        toolUseIds.Add(tu.id);
                }
            }

            var messages = new List<object>();
            bool lastWasAssistant = false;

            foreach (var msg in conversation)
            {
                var contentList = new List<object>();
                foreach (var block in msg.content)
                {
                    if (block is TextContent tc)
                    {
                        contentList.Add(new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = tc.text ?? ""
                        });
                    }
                    else if (block is ToolUseContent tu)
                    {
                        var input = ResolveToolInput(tu);
                        contentList.Add(new Dictionary<string, object>
                        {
                            ["type"] = "tool_use",
                            ["id"] = tu.id,
                            ["name"] = tu.name,
                            ["input"] = input
                        });
                    }
                    else if (block is ToolResultContent tr)
                    {
                        // Skip orphaned tool_result blocks whose tool_use was truncated
                        if (!string.IsNullOrEmpty(tr.tool_use_id) && !toolUseIds.Contains(tr.tool_use_id))
                            continue;

                        var resultBlock = new Dictionary<string, object>
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = tr.tool_use_id,
                            ["content"] = tr.content ?? ""
                        };
                        if (tr.is_error)
                            resultBlock["is_error"] = true;
                        contentList.Add(resultBlock);
                    }
                }

                // Skip empty messages (all tool_results were orphaned)
                if (contentList.Count == 0) continue;

                // Anthropic API requires alternating user/assistant roles
                string role = msg.role;
                if (messages.Count == 0 && role != "user")
                {
                    // First message must be user — skip invalid leading assistant/tool messages
                    continue;
                }

                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = role,
                    ["content"] = contentList
                });
                lastWasAssistant = role == "assistant";
            }

            return messages;
        }

        #endregion
    }
}
