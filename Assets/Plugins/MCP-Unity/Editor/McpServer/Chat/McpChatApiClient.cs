using System;
using System.Collections.Generic;
using System.Text;
using McpUnity.Chat.Providers;
using McpUnity.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace McpUnity.Chat
{
    /// <summary>
    /// HTTP client for LLM APIs with SSE streaming support.
    /// Delegates provider-specific logic (request building, SSE parsing) to IChatProvider.
    /// Runs entirely in the Unity Editor via EditorApplication.update coroutine.
    /// </summary>
    public class McpChatApiClient
    {
        private const int MaxConsecutiveErrorRounds = 3;  // Stop after N rounds where all tools failed
        private const int MaxTotalIterations = 50;         // Hard safety cap regardless of success
        public const int MaxRetries = 3;
        private const float BaseRetryDelay = 1.5f;
        private const float MaxRetryDelay = 30f;

        // Legacy EditorPrefs keys (kept for migration, new code uses ProviderRegistry)
        private const string ApiKeyPref = "McpUnity_AnthropicApiKey";
        private const string OAuthTokenPref = "McpUnity_AnthropicOAuthToken";
        private const string AuthModePref = "McpUnity_ChatAuthMode";

        // State
        private UnityWebRequest _activeRequest;
        private SseDownloadHandler _sseHandler;
        private bool _isProcessing;
        private int _toolIterationCount;      // Total rounds (safety cap)
        private int _consecutiveErrorRounds;   // Rounds where at least one tool failed

        // Retry state
        private int _retryCount;
        private string _retrySystemPrompt;
        private List<object> _retryMessages;
        private List<object> _retryTools;
        private double _retryScheduledTime;
        private bool _retryPending;

        // Callbacks
        public Action<string> OnTextDelta;
        public Action<StreamingState> OnStreamComplete;
        public Action<string> OnError;
        public Action<ToolUseContent> OnToolCallStarted;
        public Action<UsageInfo> OnUsageUpdated;
        /// <summary>Fired when a retry is scheduled. Args: (attempt number, delay in seconds).</summary>
        public Action<int, float> OnRetryAttempt;

        public bool IsProcessing => _isProcessing;

        #region Provider

        /// <summary>The active LLM provider. Defaults to Anthropic via ProviderRegistry.</summary>
        public IChatProvider Provider => ProviderRegistry.GetActiveProvider();

        #endregion

        #region Settings (delegated to ProviderRegistry)

        /// <summary>Active provider ID.</summary>
        public static string ActiveProviderId
        {
            get => ProviderRegistry.ActiveProviderId;
            set => ProviderRegistry.ActiveProviderId = value;
        }

        /// <summary>Auth mode — kept for Anthropic backward compat.</summary>
        public static AuthMode ActiveAuthMode
        {
            get => (AuthMode)EditorPrefs.GetInt(AuthModePref, (int)AuthMode.ApiKey);
            set => EditorPrefs.SetInt(AuthModePref, (int)value);
        }

        /// <summary>Legacy API key getter — now reads from ProviderRegistry for active provider.</summary>
        public static string ApiKey
        {
            get => ProviderRegistry.GetApiKey(ProviderRegistry.ActiveProviderId);
            set => ProviderRegistry.SetApiKey(ProviderRegistry.ActiveProviderId, value);
        }

        /// <summary>OAuth token (Anthropic only).</summary>
        public static string OAuthToken
        {
            get => EditorPrefs.GetString(OAuthTokenPref, "");
            set => EditorPrefs.SetString(OAuthTokenPref, value);
        }

        /// <summary>Selected model for active provider.</summary>
        public static string Model
        {
            get => ProviderRegistry.GetModel(ProviderRegistry.ActiveProviderId);
            set => ProviderRegistry.SetModel(ProviderRegistry.ActiveProviderId, value);
        }

        /// <summary>Max tokens for active provider.</summary>
        public static int MaxTokens
        {
            get => ProviderRegistry.GetMaxTokens(ProviderRegistry.ActiveProviderId);
            set => ProviderRegistry.SetMaxTokens(ProviderRegistry.ActiveProviderId, value);
        }

        /// <summary>Custom endpoint override for active provider.</summary>
        public static string CustomEndpoint
        {
            get => ProviderRegistry.GetCustomEndpoint(ProviderRegistry.ActiveProviderId);
            set => ProviderRegistry.SetCustomEndpoint(ProviderRegistry.ActiveProviderId, value);
        }

        /// <summary>True if the active provider has valid auth.</summary>
        public static bool HasAuth => ProviderRegistry.ResolveActiveAuth().IsValid;

        /// <summary>Kept for backward compat.</summary>
        public static bool HasApiKey => HasAuth;

        /// <summary>Resolve auth for the active provider.</summary>
        public static AuthResult ResolveAuth()
        {
            return ProviderRegistry.ResolveActiveAuth();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Send a streaming request to the active provider's API.
        /// Messages and tools should be in Anthropic/MCP format — the provider converts internally.
        /// </summary>
        public void SendStreamingRequest(string systemPrompt, List<object> messages, List<object> tools)
        {
            if (_isProcessing)
            {
                OnError?.Invoke("A request is already in progress.");
                return;
            }

            var auth = ResolveAuth();
            if (!auth.IsValid)
            {
                OnError?.Invoke("No authentication configured. Set an API key in Chat > Settings.");
                return;
            }

            _isProcessing = true;
            _toolIterationCount = 0;
            _consecutiveErrorRounds = 0;
            _retryCount = 0;

            TrimMessagesIfNeeded(messages);
            SendRequest(systemPrompt, messages, tools);
        }

        /// <summary>
        /// Emergency trim: drop oldest messages if above 90% of context window.
        /// This is a safety net — the Compact feature should handle normal overflow at 70%.
        /// </summary>
        private void TrimMessagesIfNeeded(List<object> messages)
        {
            int maxTokens = Provider.MaxContextTokens > 0
                ? (int)(Provider.MaxContextTokens * 0.90f) // 90% hard ceiling
                : 180000;

            int totalChars = 0;
            foreach (var msg in messages)
            {
                string json = JsonHelper.ToJson(msg);
                totalChars += json.Length;
            }

            int estimatedTokens = totalChars / 4;
            if (estimatedTokens <= maxTokens || messages.Count <= 2) return;

            int removed = 0;
            while (messages.Count > 2 && estimatedTokens > maxTokens)
            {
                string removedJson = JsonHelper.ToJson(messages[1]);
                estimatedTokens -= removedJson.Length / 4;
                messages.RemoveAt(1);
                removed++;
            }

            if (removed > 0)
            {
                McpUnity.Editor.McpDebug.Log($"[Chat] Emergency trim: dropped {removed} old messages (~{estimatedTokens} tokens remaining). Use Compact to avoid this.");
            }
        }

        /// <summary>Cancel the current request and any pending retries.</summary>
        public void Cancel()
        {
            if (_retryPending)
            {
                _retryPending = false;
                EditorApplication.update -= RetryPoll;
            }
            _retryCount = 0;
            _consecutiveErrorRounds = 0;
            _retrySystemPrompt = null;
            _retryMessages = null;
            _retryTools = null;

            if (_activeRequest != null)
            {
                _activeRequest.Abort();
                _activeRequest.Dispose();
                _activeRequest = null;
            }
            _sseHandler = null;
            _isProcessing = false;
        }

        /// <summary>Dispose resources.</summary>
        public void Dispose()
        {
            Cancel();
        }

        #endregion

        #region Request Execution

        private void SendRequest(string systemPrompt, List<object> messages, List<object> tools)
        {
            // Save context for potential retry
            _retrySystemPrompt = systemPrompt;
            _retryMessages = messages;
            _retryTools = tools;

            var provider = Provider;
            var auth = ResolveAuth();

            // If auth needs refresh (e.g. Anthropic OAuth expired token), do that first
            if (auth.NeedsRefresh)
            {
                McpChatOAuth.RefreshAccessToken(
                    _ => SendRequest(systemPrompt, messages, tools),
                    err => { _isProcessing = false; OnError?.Invoke($"Token refresh failed: {err}"); }
                );
                return;
            }

            // Let the provider build the request body (handles format conversion)
            string model = Model;
            int maxTokens = MaxTokens;
            byte[] bodyBytes = provider.BuildRequestBody(systemPrompt, messages, tools, model, maxTokens);

            // Build the HTTP request
            string url = provider.GetEndpointUrl(model, CustomEndpoint);
            _activeRequest = new UnityWebRequest(url, "POST");
            _activeRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            _activeRequest.uploadHandler.contentType = "application/json";

            // SSE download handler
            _sseHandler = new SseDownloadHandler();
            _activeRequest.downloadHandler = _sseHandler;

            // Re-resolve auth (may have refreshed) and let provider set headers
            auth = ResolveAuth();
            provider.ConfigureRequest(_activeRequest, auth, model);

            _activeRequest.timeout = 120;
            _activeRequest.SendWebRequest();

            EditorApplication.update += PollRequest;
        }

        private void PollRequest()
        {
            if (_activeRequest == null)
            {
                EditorApplication.update -= PollRequest;
                return;
            }

            // H-04: Wrap in try/finally so PollRequest is always unsubscribed on exception,
            // preventing a permanent EditorApplication.update leak.
            try
            {
                PollRequestInternal();
            }
            catch (Exception ex)
            {
                EditorApplication.update -= PollRequest;
                _activeRequest?.Dispose();
                _activeRequest = null;
                _sseHandler = null;
                _isProcessing = false;
                OnError?.Invoke($"Unexpected error during request polling: {ex.Message}");
            }
        }

        private void PollRequestInternal()
        {
            ProcessSseEvents();

            if (!_activeRequest.isDone) return;

            EditorApplication.update -= PollRequest;

            if (_activeRequest.result == UnityWebRequest.Result.ConnectionError ||
                _activeRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                long statusCode = _activeRequest.responseCode;
                string retryAfterHeader = _activeRequest.GetResponseHeader("Retry-After");
                string errorMsg = _activeRequest.error;

                if (_sseHandler != null && !string.IsNullOrEmpty(_sseHandler.ErrorBody))
                    errorMsg = ParseApiError(_sseHandler.ErrorBody) ?? errorMsg;

                _activeRequest.Dispose();
                _activeRequest = null;
                _sseHandler = null;

                // Retry on transient errors if retries remain
                if (IsRetryable(statusCode) && _retryCount < MaxRetries)
                {
                    _retryCount++;
                    float delay = CalculateRetryDelay(_retryCount, retryAfterHeader);
                    McpUnity.Editor.McpDebug.Log($"[Chat] Retryable error (HTTP {statusCode}). Attempt {_retryCount}/{MaxRetries} in {delay:F1}s");
                    OnRetryAttempt?.Invoke(_retryCount, delay);

                    _retryScheduledTime = EditorApplication.timeSinceStartup + delay;
                    _retryPending = true;
                    EditorApplication.update += RetryPoll;
                    return; // Keep _isProcessing = true
                }

                // Non-retryable or retries exhausted
                _retryCount = 0;
                _isProcessing = false;
                OnError?.Invoke(errorMsg);
                return;
            }

            ProcessSseEvents();

            var state = _sseHandler.State;
            _activeRequest.Dispose();
            _activeRequest = null;
            _sseHandler = null;

            if (state.stopReason == "tool_use")
            {
                OnStreamComplete?.Invoke(state);
            }
            else
            {
                _isProcessing = false;
                OnStreamComplete?.Invoke(state);
            }
        }

        /// <summary>Continue the conversation after tool execution.</summary>
        /// <param name="hasErrors">True if at least one tool in the last round returned is_error=true.</param>
        public void ContinueWithToolResults(string systemPrompt, List<object> messages, List<object> tools, bool hasErrors = false)
        {
            _toolIterationCount++;
            _retryCount = 0; // Each tool continuation is a fresh request for retry purposes

            // Safety hard cap — prevents infinite loops even on all-success runs
            if (_toolIterationCount >= MaxTotalIterations)
            {
                _isProcessing = false;
                OnError?.Invoke($"Tool loop reached safety limit ({MaxTotalIterations} rounds). Stopping.");
                return;
            }

            // Track consecutive error rounds — stop quickly when tools keep failing
            if (hasErrors)
            {
                _consecutiveErrorRounds++;
                if (_consecutiveErrorRounds >= MaxConsecutiveErrorRounds)
                {
                    _isProcessing = false;
                    OnError?.Invoke($"Tool loop stopped after {_consecutiveErrorRounds} consecutive failed rounds. Stopping.");
                    return;
                }
            }
            else
            {
                _consecutiveErrorRounds = 0; // Reset on any successful round
            }

            SendRequest(systemPrompt, messages, tools);
        }

        #endregion

        #region Retry Logic

        private void RetryPoll()
        {
            if (!_retryPending)
            {
                EditorApplication.update -= RetryPoll;
                return;
            }

            if (EditorApplication.timeSinceStartup >= _retryScheduledTime)
            {
                _retryPending = false;
                EditorApplication.update -= RetryPoll;
                SendRequest(_retrySystemPrompt, _retryMessages, _retryTools);
            }
        }

        private static bool IsRetryable(long statusCode)
        {
            // 0 = ConnectionError (no HTTP status received)
            return statusCode == 0 || statusCode == 408 || statusCode == 429 ||
                   statusCode == 500 || statusCode == 502 || statusCode == 503 ||
                   statusCode == 504 || statusCode == 529;
        }

        private static float CalculateRetryDelay(int attempt, string retryAfterHeader)
        {
            float backoff = BaseRetryDelay * Mathf.Pow(2f, attempt - 1);
            // Jitter ±25% to avoid thundering herd
            backoff *= UnityEngine.Random.Range(0.75f, 1.25f);

            // Respect Retry-After header if present
            if (!string.IsNullOrEmpty(retryAfterHeader) && float.TryParse(retryAfterHeader, out float retryAfter))
                backoff = Mathf.Max(backoff, retryAfter);

            return Mathf.Min(backoff, MaxRetryDelay);
        }

        #endregion

        #region SSE Processing (delegated to provider)

        private void ProcessSseEvents()
        {
            if (_sseHandler == null) return;

            while (_sseHandler.TryDequeueEvent(out var sseEvent))
            {
                // Delegate event processing to the active provider
                Provider.ProcessSseEvent(sseEvent, _sseHandler.State,
                    OnTextDelta, OnToolCallStarted, OnUsageUpdated, OnError);
            }
        }

        private string ParseApiError(string responseBody)
        {
            try
            {
                var parsed = JsonHelper.ParseJsonObject(responseBody);
                if (parsed != null && parsed.TryGetValue("error", out var errObj))
                {
                    if (errObj is Dictionary<string, object> err)
                    {
                        string errType = err.TryGetValue("type", out var et) ? et?.ToString() : "error";
                        string errMsg = err.TryGetValue("message", out var em) ? em?.ToString() : "Unknown error";
                        return $"API {errType}: {errMsg}";
                    }
                    // OpenAI format: error.message directly
                    if (errObj is string errStr)
                        return errStr;
                }
            }
            catch (Exception) { /* Malformed error response — return null to use fallback message */ }
            return null;
        }

        #endregion
    }

    // ========================================================================
    // Custom DownloadHandler for SSE (Server-Sent Events) streaming
    // ========================================================================

    /// <summary>
    /// DownloadHandlerScript that parses SSE events in real-time and queues them.
    /// Generic — works with all providers (Anthropic, OpenAI, Gemini).
    /// Uses a stateful UTF-8 Decoder to correctly handle multi-byte characters
    /// split across chunk boundaries. Instance-scoped 64KB buffer.
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private const int BufferSize = 65536; // 64KB — reduces ReceiveData calls, handles large SSE chunks

        private readonly Queue<SseEvent> _eventQueue = new Queue<SseEvent>();
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private string _currentEventType = "";
        private readonly StringBuilder _currentData = new StringBuilder();
        private readonly StringBuilder _errorBuffer = new StringBuilder();
        private readonly object _lock = new object();
        private bool _isErrorBody; // Once set, all subsequent data goes to _errorBuffer

        // Stateful UTF-8 decoder — preserves incomplete multi-byte sequences across ReceiveData calls
        private readonly Decoder _utf8Decoder = Encoding.UTF8.GetDecoder();
        private char[] _charBuffer = new char[2048];

        public StreamingState State { get; } = new StreamingState();
        public string ErrorBody => _errorBuffer.ToString();

        public SseDownloadHandler() : base(new byte[BufferSize]) { }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return true;

            // Decode bytes to chars using stateful decoder (handles split multi-byte UTF-8)
            int charCount = _utf8Decoder.GetCharCount(data, 0, dataLength, false);
            if (_charBuffer.Length < charCount)
                _charBuffer = new char[charCount * 2];
            int decoded = _utf8Decoder.GetChars(data, 0, dataLength, _charBuffer, 0, false);

            // Check if this looks like a JSON error body (not SSE)
            if (!_isErrorBody && _lineBuffer.Length == 0 && _currentData.Length == 0)
            {
                // Scan for first non-whitespace char
                for (int i = 0; i < decoded; i++)
                {
                    if (!char.IsWhiteSpace(_charBuffer[i]))
                    {
                        if (_charBuffer[i] == '{')
                            _isErrorBody = true;
                        break;
                    }
                }
            }

            if (_isErrorBody)
            {
                _errorBuffer.Append(_charBuffer, 0, decoded);
                return true;
            }

            // Parse SSE lines character by character
            for (int i = 0; i < decoded; i++)
            {
                char c = _charBuffer[i];
                if (c == '\n')
                {
                    ProcessLine(_lineBuffer.ToString());
                    _lineBuffer.Clear();
                }
                else if (c != '\r')
                {
                    _lineBuffer.Append(c);
                }
            }

            return true;
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                if (_currentData.Length > 0)
                {
                    var evt = new SseEvent
                    {
                        eventType = string.IsNullOrEmpty(_currentEventType) ? "message" : _currentEventType,
                        data = _currentData.ToString()
                    };

                    lock (_lock)
                    {
                        _eventQueue.Enqueue(evt);
                    }

                    _currentEventType = "";
                    _currentData.Clear();
                }
                return;
            }

            if (line.StartsWith("event:"))
                _currentEventType = line.Substring(6).Trim();
            else if (line.StartsWith("data:"))
            {
                if (_currentData.Length > 0) _currentData.Append("\n");
                _currentData.Append(line.Substring(5).Trim());
            }
            // SSE spec: lines starting with ':' are comments — silently ignored
        }

        public bool TryDequeueEvent(out SseEvent evt)
        {
            lock (_lock)
            {
                if (_eventQueue.Count > 0)
                {
                    evt = _eventQueue.Dequeue();
                    return true;
                }
            }
            evt = null;
            return false;
        }
    }
}
