using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using McpUnity.Protocol;
using McpUnity.Utils;
using McpUnity.Helpers;
using McpUnity.Models;
using McpUnity.Editor;
using LogEntry = McpUnity.Models.LogEntry;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace McpUnity.Server
{
    /// <summary>
    /// Helper class for creating standardized MCP tool responses
    /// </summary>
    public static class McpResponse
    {
        /// <summary>
        /// Create a success response with structured data
        /// </summary>
        public static McpToolResult Success(object data)
        {
            return new McpToolResult
            {
                content = new List<McpContent>
                {
                    McpContent.Json(new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["data"] = data
                    })
                },
                isError = false
            };
        }

        /// <summary>
        /// Create a success response with message and optional data
        /// </summary>
        public static McpToolResult Success(string message, object data = null)
        {
            var result = new Dictionary<string, object>
            {
                ["success"] = true,
                ["message"] = message
            };
            if (data != null)
                result["data"] = data;

            return new McpToolResult
            {
                content = new List<McpContent> { McpContent.Json(result) },
                isError = false
            };
        }
    }

    /// <summary>
    /// WebSocket server for MCP Unity plugin using websocket-sharp.
    /// Handles multiple client connections and message routing.
    ///
    /// This is a partial class - tool implementations are in separate files under Tools/ folder.
    /// Tools are organized by category (13 categories, loaded dynamically via McpToolRegistry).
    ///
    /// CORE (always active) — 47 tools (45 + 2 meta):
    ///   - GameObjectTools.cs (12) - ComponentTools.cs (7)  - SceneTools.cs (5)
    ///   - EditorTools.cs (dispatcher → Selection 7 + Screenshot 1 + Workflow 5 = 13)
    ///   - ScriptTools.cs (5)     - MemoryTools.cs (3)     - Meta-tools (2)
    ///
    /// ASSET — 16 tools:
    ///   - AssetTools.cs (9)      - AssetImportTools.cs (2) - PrefabTools.cs (5)
    ///
    /// MATERIAL — 3 tools:       MaterialTools.cs (3)
    /// UI — 9 tools:             UICreationTools.cs (4) + UILayoutTools.cs (5)
    /// ANIMATOR — 23 tools:      ControllerInfo (7) + Validation (1) + Flow (1)
    ///                           + StateTools (6) + TransitionTools (5) + ClipTools (3)
    /// TERRAIN — 17 tools:       Core (5) + Paint (4) + Detail (3) + Advanced (5)
    /// PHYSICS — 8 tools:        PhysicsTools (4) + NavMeshTools (4)
    /// AUDIO — 3 tools:          AudioTools.cs (3)
    /// RENDERING — 13 tools:     CameraRenderingTools (3) + BakingTools (10)
    /// BUILD — 6 tools:          BuildTools (3) + PackageManagerTools (3)
    /// SETTINGS — 11 tools:      ProjectSettingsTools (5) + TagLayerTools (6)
    /// INPUT — 3 tools:          InputSystemTools.cs (3)
    /// ADVANCED — 5 tools:       ReferenceTools (2) + ScriptableObjectTools (3)
    ///
    /// Total: 164 tools
    /// </summary>
    [InitializeOnLoad]
    public partial class McpUnityServer
    {
        #region Static Fields

        private static WebSocketServer _wss;
        private static volatile bool _isRunning;
        private static readonly ConcurrentDictionary<string, McpBehavior> _connectedClients
            = new ConcurrentDictionary<string, McpBehavior>();

        private static McpToolRegistry _toolRegistry;
        private static McpResourceRegistry _resourceRegistry;

        // Message queue for main thread processing
        private static readonly ConcurrentQueue<QueuedMessage> _messageQueue
            = new ConcurrentQueue<QueuedMessage>();
        private static volatile bool _updateRegistered = false;
        private static volatile bool _initialized = false;
        private static int _startupFrameCount = 0;

        // Console log capture
        private static readonly ConcurrentQueue<LogEntry> _consoleLogs = new ConcurrentQueue<LogEntry>();
        private static volatile bool _logHandlerRegistered = false;

        // Background tick — forces EditorApplication.update to fire even when Unity is unfocused
        private static System.Threading.Timer _backgroundTickTimer;

        #endregion

        #region Properties

        public static int Port
        {
            get => McpUnity.Editor.McpSettings.Instance.Port;
            set => McpUnity.Editor.McpSettings.Instance.Port = value;
        }

        public static bool AutoStart
        {
            get => McpUnity.Editor.McpSettings.Instance.AutoStartServer;
            set => McpUnity.Editor.McpSettings.Instance.AutoStartServer = value;
        }

        public static bool IsRunning => _isRunning;
        public static int ConnectedClientCount => _connectedClients.Count;
        public static int RegisteredToolCount => _toolRegistry?.Count ?? 0;
        public static McpToolRegistry ToolRegistry => _toolRegistry;

        #endregion

        #region Events

        public static event Action<string> OnMessageReceived;
        public static event Action<string> OnClientConnected;
        public static event Action<string> OnClientDisconnected;
        public static event Action OnServerStarted;
        public static event Action OnServerStopped;

        #endregion

        #region Initialization

        static McpUnityServer()
        {
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall += OnEditorLoaded;
            RegisterUpdateCallback();
            RegisterLogHandler();
        }

        private static void RegisterLogHandler()
        {
            if (!_logHandlerRegistered)
            {
                Application.logMessageReceived += HandleLogMessage;
                _logHandlerRegistered = true;
            }
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = condition,
                StackTrace = stackTrace,
                Type = type.ToString()
            };

            _consoleLogs.Enqueue(entry);

            // SEC-#431: cap dequeue iterations so a log storm can't pin this method.
            // ConcurrentQueue.Count is O(n) on some runtimes, and if producers run faster than
            // we trim, an unbounded while loop can spin forever. Dequeue at most N extras per
            // enqueue — worst case the queue stays close to (maxEntries + batchLimit).
            int maxEntries = McpUnity.Editor.McpSettings.Instance.MaxLogEntries;
            const int batchLimit = 32;
            int drained = 0;
            while (_consoleLogs.Count > maxEntries && drained < batchLimit)
            {
                if (!_consoleLogs.TryDequeue(out _)) break;
                drained++;
            }
        }

        private static void RegisterUpdateCallback()
        {
            if (!_updateRegistered)
            {
                EditorApplication.update += ProcessMessageQueue;
                _updateRegistered = true;
            }
        }

        /// <summary>
        /// Queue a message for processing on the main thread
        /// </summary>
        internal static void QueueMessage(string message, McpBehavior sender)
        {
            _messageQueue.Enqueue(new QueuedMessage(message, sender));
        }

        /// <summary>
        /// Process queued messages on the main thread (called by EditorApplication.update)
        /// </summary>
        private static void ProcessMessageQueue()
        {
            // Fallback initialization check - ensures server starts even if delayCall doesn't fire
            if (!_initialized && !EditorApplication.isCompiling)
            {
                _startupFrameCount++;
                // Wait a few frames after domain reload before attempting to start
                if (_startupFrameCount > McpConstants.StartupFrameDelay)
                {
                    _initialized = true;
                    InitializeRegistries();
                    if (AutoStart && !_isRunning)
                    {
                        McpDebug.Log("[MCP Unity] Fallback auto-start triggered");
                        Start();
                    }
                }
            }

            // Process up to MaxMessagesPerFrame messages per frame to avoid blocking
            int processed = 0;
            while (processed < McpConstants.MaxMessagesPerFrame && _messageQueue.TryDequeue(out var queued))
            {
                processed++;
                try
                {
                    McpDebug.Log("[MCP Unity] Processing message on main thread...");
                    var response = ProcessMessage(queued.Message);

                    if (!string.IsNullOrEmpty(response))
                    {
                        // C-02: Guard — client may have disconnected between enqueue and processing
                        if (queued.Sender == null || !queued.Sender.IsConnected)
                        {
                            McpDebug.LogWarning("[MCP Unity] Client disconnected before response could be sent, dropping response.");
                        }
                        else
                        {
                            McpDebug.Log($"[MCP Unity] Sending response: {(response.Length > 200 ? response.Substring(0, 200) + "..." : response)}");
                            queued.Sender.SendMessage(response);
                            McpDebug.Log("[MCP Unity] Response sent successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    McpDebug.LogError($"[MCP Unity] Error processing message: {ex.Message}\n{ex.StackTrace}");
                    // SEC-#407: mirror the IsConnected guard from the success path above — the
                    // sender may have disconnected between enqueue and the exception, and calling
                    // SendMessage on a closed socket throws.
                    if (queued.Sender == null || !queued.Sender.IsConnected)
                    {
                        McpDebug.LogWarning("[MCP Unity] Client disconnected before error response could be sent, dropping.");
                        continue;
                    }
                    try
                    {
                        queued.Sender.SendMessage(JsonHelper.ToJson(
                            JsonRpcResponse.Error(null, JsonRpcError.InternalError, ex.Message)));
                    }
                    catch (Exception sendEx)
                    {
                        McpDebug.LogWarning($"[MCP Unity] Failed to send error response: {sendEx.Message}");
                    }
                }
            }
        }

        private static void OnEditorLoaded()
        {
            if (_initialized) return; // Prevent double initialization
            _initialized = true;

            InitializeRegistries();

            if (AutoStart)
            {
                Start();
            }
        }

        private static void InitializeRegistries()
        {
            _toolRegistry = new McpToolRegistry();
            _resourceRegistry = new McpResourceRegistry();

            // Register default tools and resources
            RegisterDefaultTools();
            RegisterDefaultResources();

            // Initialize JSON-RPC handler
            McpJsonRpc.Initialize(_toolRegistry, _resourceRegistry);
        }

        /// <summary>
        /// Register all default tools by calling partial class registration methods.
        /// Tool implementations are in separate files under Tools/ folder.
        /// Categories control which tools appear in tools/list (core is always enabled).
        /// </summary>
        private static void RegisterDefaultTools()
        {
            // ── CORE (always visible) ──────────────────────────────────
            _toolRegistry.SetCurrentCategory("core");
            RegisterGameObjectTools();   // list, create_batch, delete, rename, parent, duplicate, move, find_by_component, set_active
            RegisterComponentTools();    // get, add, modify_batch, list_scripts, remove
            RegisterSceneTools();        // get_info, load, save, create
            RegisterEditorTools();       // selection, console, screenshot, undo, tests, refresh, menu
            RegisterScriptTools();       // create, read, info, write, update
            RegisterMemoryTools();       // get, refresh, clear
            RegisterCategoryMetaTools(); // list_tool_categories, enable_tool_category

            // ── ASSET ──────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("asset");
            RegisterAssetTools();        // search, info, list_folders, list_contents, preview
            RegisterAssetImportTools();  // get/set import settings
            RegisterPrefabTools();       // instantiate, create, unpack, apply, revert

            // ── MATERIAL ───────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("material");
            RegisterMaterialTools();     // get, set, create

            // ── UI ─────────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("ui");
            RegisterUITools();           // canvas, create_element, hierarchy, modify, rect, layout, fitter, element, scaler

            // ── ANIMATOR ───────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("animator");
            RegisterAnimatorTools();     // controller, params, states, blend trees, transitions, flow, validate, clips

            // ── TERRAIN ────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("terrain");
            RegisterTerrainTools();      // create, info, modify, heights, brushes, layers, paint, trees, heightmap, details

            // ── PHYSICS ────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("physics");
            RegisterPhysicsTools();      // raycast, rigidbody, collider, physics_material
            RegisterNavMeshTools();      // bake, clear, settings, set_static

            // ── AUDIO ──────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("audio");
            RegisterAudioTools();        // audio_source, create_mixer, get_mixer

            // ── RENDERING ──────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("rendering");
            RegisterCameraRenderingTools(); // configure, render_to_file, pipeline_info
            RegisterBakingTools();          // lighting, async, status, cancel, clear, lightmap, occlusion, probes

            // ── BUILD ──────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("build");
            RegisterBuildTools();        // build_settings, manage_scenes, switch_platform
            RegisterPackageManagerTools(); // list, add, remove

            // ── SETTINGS ───────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("settings");
            RegisterProjectSettingsTools(); // get/set settings, quality, physics collision
            RegisterTagLayerTools();        // list/create/set tags and layers

            // ── INPUT ──────────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("input");
            RegisterInputSystemTools();  // get_actions, add_action, add_binding

            // ── ADVANCED ───────────────────────────────────────────────
            _toolRegistry.SetCurrentCategory("advanced");
            RegisterReferenceTools();         // set_reference, set_reference_array
            RegisterScriptableObjectTools();  // create, list_types, modify

            _toolRegistry.SetCurrentCategory("core"); // reset

            McpDebug.Log($"[MCP Unity] Registered {_toolRegistry.Count} tools ({_toolRegistry.VisibleCount} visible in core). " +
                         $"Use unity_enable_tool_category to load more.");
        }

        /// <summary>
        /// Register meta-tools for dynamic category management
        /// </summary>
        private static void RegisterCategoryMetaTools()
        {
            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_list_tool_categories",
                description = "List all tool categories with tool counts, enabled status, and tool names. Use this to discover available tools before enabling a category.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>(),
                    required = new List<string>()
                }
            }, ListToolCategories);

            _toolRegistry.RegisterTool(new McpToolDefinition
            {
                name = "unity_enable_tool_category",
                description = "Enable one or more tool categories so their tools appear in the tool list. After enabling, the tool list is refreshed automatically. Categories: asset, material, ui, animator, terrain, physics, audio, rendering, build, settings, input, advanced.",
                inputSchema = new McpInputSchema
                {
                    type = "object",
                    properties = new Dictionary<string, McpPropertySchema>
                    {
                        ["category"] = new McpPropertySchema
                        {
                            type = "string",
                            description = "Category to enable (e.g. 'ui', 'animator', 'terrain')"
                        },
                        ["categories"] = new McpPropertySchema
                        {
                            type = "array",
                            description = "Multiple categories to enable at once (e.g. ['ui', 'material', 'asset'])"
                        }
                    },
                    required = new List<string>()
                }
            }, EnableToolCategory);
        }

        private static McpToolResult ListToolCategories(Dictionary<string, object> args)
        {
            var categories = _toolRegistry.GetCategoryInfo();
            var enabledCount = categories.Count(c => c.enabled);
            var visibleTools = _toolRegistry.VisibleCount;
            var totalTools = _toolRegistry.Count;

            return McpResponse.Success($"{categories.Count} categories ({enabledCount} enabled, {visibleTools}/{totalTools} tools visible)", new
            {
                categories = categories,
                totalTools = totalTools,
                visibleTools = visibleTools
            });
        }

        private static McpToolResult EnableToolCategory(Dictionary<string, object> args)
        {
            var enabled = new List<string>();
            var alreadyEnabled = new List<string>();
            var notFound = new List<string>();

            // Single category
            string single = ArgumentParser.GetString(args, "category", null);
            if (!string.IsNullOrEmpty(single))
            {
                if (_toolRegistry.EnableCategory(single)) enabled.Add(single);
                else if (_toolRegistry.IsCategoryEnabled(single)) alreadyEnabled.Add(single);
                else notFound.Add(single);
            }

            // Multiple categories
            if (args.TryGetValue("categories", out var catsObj) && catsObj is IEnumerable<object> catsList)
            {
                foreach (var catObj in catsList)
                {
                    var cat = catObj?.ToString();
                    if (string.IsNullOrEmpty(cat)) continue;
                    if (_toolRegistry.EnableCategory(cat)) enabled.Add(cat);
                    else if (_toolRegistry.IsCategoryEnabled(cat)) alreadyEnabled.Add(cat);
                    else notFound.Add(cat);
                }
            }

            if (enabled.Count == 0 && notFound.Count == 0 && alreadyEnabled.Count == 0)
                return McpToolResult.Error("Provide 'category' (string) or 'categories' (array). Available: asset, material, ui, animator, terrain, physics, audio, rendering, build, settings, input, advanced.");

            // Send tools/list_changed notification if any category was newly enabled
            if (enabled.Count > 0)
            {
                NotifyToolsListChanged();
            }

            return McpResponse.Success(
                enabled.Count > 0
                    ? $"Enabled {enabled.Count} category(s): {string.Join(", ", enabled)}. Tools list refreshed."
                    : "No new categories enabled.",
                new
                {
                    enabled = enabled,
                    alreadyEnabled = alreadyEnabled,
                    notFound = notFound,
                    visibleTools = _toolRegistry.VisibleCount,
                    totalTools = _toolRegistry.Count
                });
        }

        /// <summary>
        /// Send a JSON-RPC notification to all connected clients that the tools list has changed.
        /// The MCP client will re-fetch tools/list to get the updated list.
        /// </summary>
        public static void NotifyToolsListChanged()
        {
            var notification = new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/tools/list_changed"
            };
            Broadcast(JsonHelper.ToJson(notification));
            McpDebug.Log("[MCP Unity] Sent tools/list_changed notification");
        }

        private static void RegisterDefaultResources()
        {
            _resourceRegistry.RegisterResource(new McpResourceDefinition
            {
                uri = "unity://project/settings",
                name = "Project Settings",
                description = "Current Unity project settings",
                mimeType = "application/json"
            }, () => GetProjectSettings());

            _resourceRegistry.RegisterResource(new McpResourceDefinition
            {
                uri = "unity://scene/hierarchy",
                name = "Scene Hierarchy",
                description = "Current scene object hierarchy",
                mimeType = "application/json"
            }, () => GetSceneHierarchy());

            _resourceRegistry.RegisterResource(new McpResourceDefinition
            {
                uri = "unity://console/logs",
                name = "Console Logs",
                description = "Recent Unity console log entries",
                mimeType = "application/json"
            }, () => GetConsoleLogs());
        }

        #endregion

        #region Shared Utilities

        /// <summary>
        /// Sanitize and validate file paths for security (prevent path traversal attacks)
        /// Delegates to PathValidator for the actual implementation.
        /// </summary>
        internal static string SanitizePath(string path, string requiredPrefix = "Assets/")
            => PathValidator.SanitizePath(path, requiredPrefix);

        /// <summary>
        /// Try to sanitize a path. Returns the sanitized path on success, or an McpToolResult error on failure.
        /// Eliminates the repetitive try/catch(ArgumentException) pattern used in 45+ tool handlers.
        /// Usage: var (path, err) = TrySanitizePath(rawPath, "asset path"); if (err != null) return err;
        /// </summary>
        internal static (string path, McpToolResult error) TrySanitizePath(string rawPath, string label = "path", string requiredPrefix = "Assets/")
        {
            try
            {
                return (PathValidator.SanitizePath(rawPath, requiredPrefix), null);
            }
            catch (ArgumentException ex)
            {
                return (null, McpToolResult.Error($"Invalid {label}: {ex.Message}"));
            }
        }

        /// <summary>
        /// Require a string argument and return an error result if missing.
        /// Eliminates the repetitive "RequireString + null check" 2-line pattern used in 144+ tool handlers.
        /// Usage: var (val, err) = RequireArg(args, "key"); if (err != null) return err;
        /// </summary>
        internal static (string value, McpToolResult error) RequireArg(Dictionary<string, object> args, string key)
        {
            var value = ArgumentParser.RequireString(args, key, out var errorMsg);
            if (value == null)
                return (null, McpToolResult.Error(errorMsg));
            return (value, null);
        }

        /// <summary>
        /// Require a string argument, find the GameObject by that path, and return an error if either fails.
        /// Eliminates the 4-6 line "RequireString + FindGameObject + null checks" pattern used in 48+ tool handlers.
        /// Usage: var (go, goPath, err) = RequireGameObject(args); if (err != null) return err;
        /// </summary>
        internal static (GameObject go, string path, McpToolResult error) RequireGameObject(
            Dictionary<string, object> args, string key = "gameObjectPath")
        {
            var path = ArgumentParser.RequireString(args, key, out var errorMsg);
            if (path == null)
                return (null, null, McpToolResult.Error(errorMsg));

            var go = GameObjectHelpers.FindGameObject(path);
            if (go == null)
                return (null, path, McpToolResult.Error($"GameObject not found: {path}"));

            return (go, path, null);
        }

        /// <summary>
        /// Get the full hierarchy path of a GameObject.
        /// Delegates to the canonical implementation in GameObjectHelpers.
        /// </summary>
        internal static string GetGameObjectPath(GameObject obj)
            => GameObjectHelpers.GetGameObjectPath(obj);

        /// <summary>
        /// Get console logs as a list (used by multiple tools)
        /// </summary>
        internal static ConcurrentQueue<LogEntry> GetConsoleLogQueue()
        {
            return _consoleLogs;
        }

        #endregion

        #region Resource Implementations

        private static McpResourceContent GetProjectSettings()
        {
            var settings = new
            {
                productName = PlayerSettings.productName,
                companyName = PlayerSettings.companyName,
                version = PlayerSettings.bundleVersion,
                targetPlatform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString()
            };

            return new McpResourceContent
            {
                uri = "unity://project/settings",
                mimeType = "application/json",
                text = JsonHelper.ToJson(settings)
            };
        }

        private static McpResourceContent GetSceneHierarchy()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var hierarchy = new List<object>();

            foreach (var obj in rootObjects)
            {
                hierarchy.Add(GetGameObjectInfoForResource(obj, 0));
            }

            return new McpResourceContent
            {
                uri = "unity://scene/hierarchy",
                mimeType = "application/json",
                text = JsonHelper.ToJson(new { sceneName = scene.name, rootObjects = hierarchy })
            };
        }

        private static object GetGameObjectInfoForResource(GameObject obj, int depth)
        {
            var children = new List<object>();
            if (depth < 3)
            {
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    children.Add(GetGameObjectInfoForResource(obj.transform.GetChild(i).gameObject, depth + 1));
                }
            }

            return new
            {
                name = obj.name,
                active = obj.activeSelf,
                components = GetComponentNames(obj),
                children = children
            };
        }

        private static string[] GetComponentNames(GameObject obj)
        {
            var components = obj.GetComponents<Component>();
            var names = new string[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                names[i] = components[i]?.GetType().Name ?? "null";
            }
            return names;
        }

        private static McpResourceContent GetConsoleLogs()
        {
            var logEntries = new List<object>();
            foreach (var entry in _consoleLogs)
            {
                logEntries.Add(entry.ToSerializable(includeStackTrace: false));
            }

            return new McpResourceContent
            {
                uri = "unity://console/logs",
                mimeType = "application/json",
                text = JsonHelper.ToJson(new { count = logEntries.Count, logs = logEntries })
            };
        }

        #endregion

        #region Server Lifecycle

        public static void MenuStartServer()
        {
            if (!_initialized)
            {
                _initialized = true;
                InitializeRegistries();
            }
            Start();
        }

        public static void MenuStopServer()
        {
            Stop();
        }

        /// <summary>
        /// Start the WebSocket server using websocket-sharp
        /// </summary>
        public static void Start()
        {
            if (_isRunning)
            {
                McpDebug.LogWarning("[MCP Unity] Server is already running");
                return;
            }

            try
            {
                // SEC-#421: honor the Host setting so AllowRemoteConnections actually works.
                // "localhost"/"127.0.0.1"/"::1" bind loopback; "0.0.0.0"/"*" bind all interfaces.
                // websocket-sharp interprets "0.0.0.0" as any-interface when passed via IPAddress.
                string configuredHost = McpSettings.Instance.Host ?? "localhost";
                string bindHost = NormalizeBindHost(configuredHost);
                bool isRemote = !(bindHost == "127.0.0.1" || bindHost == "::1" || bindHost == "localhost");

                _wss = new WebSocketServer($"ws://{bindHost}:{Port}");
                _wss.AddWebSocketService<McpBehavior>("/");
                _wss.Start();
                _isRunning = true;
                StartBackgroundTick();

                McpDebug.Log($"[MCP Unity] Server started on ws://{bindHost}:{Port}");
                if (isRemote)
                {
                    McpDebug.LogWarning(
                        "[MCP Unity] Server is bound to a non-loopback interface " +
                        $"('{configuredHost}'). Traffic is UNENCRYPTED (ws://). Ensure a shared secret " +
                        "is configured and that only trusted networks can reach this port.");
                }
                OnServerStarted?.Invoke();
            }
            catch (Exception ex)
            {
                McpDebug.LogError($"[MCP Unity] Failed to start server on port {Port}: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// SEC-#421: translate user-friendly host strings into a value websocket-sharp accepts
        /// in its URL. "localhost" stays as-is (resolves to loopback). "0.0.0.0" / "*" / "any"
        /// bind every interface. An empty/whitespace value falls back to loopback.
        /// </summary>
        private static string NormalizeBindHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return "127.0.0.1";
            string h = host.Trim();
            if (h == "*" || h.Equals("any", StringComparison.OrdinalIgnoreCase)) return "0.0.0.0";
            return h;
        }

        /// <summary>
        /// Stop the WebSocket server
        /// </summary>
        public static void Stop()
        {
            if (!_isRunning) return;

            StopBackgroundTick();

            try
            {
                _wss?.Stop();
            }
            catch (Exception ex)
            {
                McpDebug.LogWarning($"[MCP Unity] Error stopping server: {ex.Message}");
            }

            _connectedClients.Clear();
            _isRunning = false;
            McpDebug.Log("[MCP Unity] Server stopped");
            OnServerStopped?.Invoke();
        }

        /// <summary>
        /// Starts a background timer that forces Unity to process editor updates
        /// even when the window is unfocused. Without this, EditorApplication.update
        /// callbacks (including WebSocket message processing) stall when Unity is in
        /// the background, making the MCP server unresponsive.
        /// </summary>
        private static void StartBackgroundTick()
        {
            StopBackgroundTick();
            _backgroundTickTimer = new System.Threading.Timer(_ =>
            {
                if (_isRunning && !EditorApplication.isCompiling)
                    EditorApplication.QueuePlayerLoopUpdate();
            }, null, 1000, 200); // Start after 1s, tick every 200ms (~5 updates/s)
        }

        private static void StopBackgroundTick()
        {
            _backgroundTickTimer?.Dispose();
            _backgroundTickTimer = null;
        }

        /// <summary>
        /// Restart the server
        /// </summary>
        public static void Restart()
        {
            Stop();
            EditorApplication.delayCall += Start;
        }

        /// <summary>
        /// Process an incoming message (called from McpBehavior)
        /// </summary>
        internal static string ProcessMessage(string message)
        {
            OnMessageReceived?.Invoke(message);
            return McpJsonRpc.ProcessMessage(message);
        }

        /// <summary>
        /// Register a client connection
        /// </summary>
        internal static void RegisterClient(string id, McpBehavior behavior)
        {
            _connectedClients[id] = behavior;
            McpDebug.Log($"[MCP Unity] Client connected: {id}");
            OnClientConnected?.Invoke(id);
        }

        /// <summary>
        /// Unregister a client connection
        /// </summary>
        internal static void UnregisterClient(string id)
        {
            _connectedClients.TryRemove(id, out _);
            McpDebug.Log($"[MCP Unity] Client disconnected: {id}");
            OnClientDisconnected?.Invoke(id);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Register a custom tool
        /// </summary>
        public static void RegisterTool(McpToolDefinition definition, Func<Dictionary<string, object>, McpToolResult> handler)
        {
            _toolRegistry?.RegisterTool(definition, handler);
        }

        /// <summary>
        /// Register a custom resource
        /// </summary>
        public static void RegisterResource(McpResourceDefinition definition, Func<McpResourceContent> handler)
        {
            _resourceRegistry?.RegisterResource(definition, handler);
        }

        /// <summary>
        /// Broadcast a message to all connected clients
        /// </summary>
        public static void Broadcast(string message)
        {
            foreach (var kvp in _connectedClients)
            {
                try
                {
                    kvp.Value.SendMessage(message);
                }
                catch (Exception ex)
                {
                    McpDebug.LogWarning($"[MCP Unity] Failed to broadcast to client {kvp.Key}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Partial Class Registration Methods (implemented in Tools/*.cs)

        // These methods are implemented in partial class files under Tools/ folder
        // They register tool definitions and handlers with _toolRegistry

        static partial void RegisterUITools();
        static partial void RegisterUICreationTools();
        static partial void RegisterUILayoutTools();
        static partial void RegisterGameObjectTools();
        static partial void RegisterComponentTools();
        static partial void RegisterAnimatorTools();
        static partial void RegisterAnimatorControllerTools();
        static partial void RegisterAnimatorControllerInfoTools();
        static partial void RegisterAnimatorValidationTools();
        static partial void RegisterAnimatorFlowTools();
        static partial void RegisterAnimatorStateTools();
        static partial void RegisterAnimatorTransitionTools();
        static partial void RegisterAnimatorClipTools();
        static partial void RegisterAssetTools();
        static partial void RegisterSceneTools();
        static partial void RegisterPrefabTools();
        static partial void RegisterMaterialTools();
        static partial void RegisterTagLayerTools();
        static partial void RegisterEditorTools();
        static partial void RegisterEditorWorkflowTools();
        static partial void RegisterEditorScreenshotTools();
        static partial void RegisterEditorSelectionTools();
        static partial void RegisterMemoryTools();
        static partial void RegisterProjectSettingsTools();
        static partial void RegisterReferenceTools();
        static partial void RegisterNavMeshTools();
        static partial void RegisterBakingTools();
        static partial void RegisterScriptableObjectTools();
        static partial void RegisterPhysicsTools();
        static partial void RegisterAudioTools();
        static partial void RegisterScriptTools();
        static partial void RegisterBuildTools();
        static partial void RegisterCameraRenderingTools();
        static partial void RegisterTerrainTools();
        static partial void RegisterTerrainCoreTools();
        static partial void RegisterTerrainPaintTools();
        static partial void RegisterTerrainDetailTools();
        static partial void RegisterTerrainAdvancedTools();
        static partial void RegisterPackageManagerTools();
        static partial void RegisterAssetImportTools();
        static partial void RegisterInputSystemTools();

        #endregion
    }

    /// <summary>
    /// WebSocket behavior for handling MCP client connections
    /// </summary>
    public class McpBehavior : WebSocketBehavior
    {
        /// <summary>
        /// Maximum allowed incoming message size (10 MB).
        /// Protects against OOM from oversized payloads while still allowing
        /// large legitimate messages (e.g. base64-encoded screenshots).
        /// </summary>
        private const int MaxMessageSize = 10 * 1024 * 1024;

        protected override void OnOpen()
        {
            // Shared secret validation: if a secret is configured, require it in the query string
            var settings = McpUnity.Editor.McpSettings.Instance;
            if (settings.IsSecretEnabled)
            {
                var query = Context?.QueryString;
                string clientSecret = query?["secret"];
                // SEC-#412: constant-time comparison prevents a timing-based side channel
                // that could otherwise let an attacker recover the secret one character at
                // a time by measuring response latency (exploitable over the network when
                // AllowRemoteConnections = true).
                if (!FixedTimeEquals(clientSecret, settings.SharedSecret))
                {
                    McpDebug.LogWarning($"[MCP Unity] Client rejected: invalid or missing shared secret");
                    Context.WebSocket.Close(WebSocketSharp.CloseStatusCode.PolicyViolation, "Invalid shared secret");
                    return;
                }
            }

            McpUnityServer.RegisterClient(ID, this);
        }

        /// <summary>
        /// SEC-#412: constant-time string comparison. Returns false immediately only when
        /// lengths differ (the length itself is not secret); otherwise XORs every char so
        /// total time is independent of where the first mismatch occurs.
        /// </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        protected override void OnClose(CloseEventArgs e)
        {
            McpUnityServer.UnregisterClient(ID);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
            {
                var message = e.Data;

                // S3: Reject oversized messages to prevent OOM from malicious/accidental huge payloads
                if (message.Length > MaxMessageSize)
                {
                    McpDebug.LogWarning($"[MCP Unity] Rejected oversized message: {message.Length} bytes (max {MaxMessageSize})");
                    return;
                }

                McpDebug.Log($"[MCP Unity] Received message: {(message.Length > 100 ? message.Substring(0, 100) + "..." : message)}");

                // Queue message for processing on main thread
                McpUnityServer.QueueMessage(message, this);
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            McpDebug.LogError($"[MCP Unity] WebSocket error: {e.Message}");
        }

        /// <summary>C-02: Check if the WebSocket connection is still alive before sending.</summary>
        public bool IsConnected => Context?.WebSocket?.ReadyState == WebSocketSharp.WebSocketState.Open;

        public void SendMessage(string message)
        {
            Send(message);
        }
    }

}
