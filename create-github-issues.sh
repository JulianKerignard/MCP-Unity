#!/bin/bash
# =============================================================================
# MCP-Unity Code Review - GitHub Issues Creator
# Run: gh auth login && bash create-github-issues.sh
# Requires: gh CLI authenticated with repo access
# =============================================================================

REPO="JulianKerignard/MCP-Unity"

echo "Creating GitHub issues for MCP-Unity code review..."
echo "Repository: $REPO"
echo ""

# ---------------------------------------------------------------------------
# ISSUE 1 - CRITICAL: Security - Shared secret exposed in WebSocket query string
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Security] Shared secret exposed in WebSocket URL query string" \
  --label "bug,security,priority:high" \
  --body "$(cat <<'EOF'
## Description

The shared secret for WebSocket authentication is transmitted via URL query parameters instead of headers. Query parameters are logged in server logs, proxy logs, and potentially browser history.

## Location

- **C# side**: `Editor/McpServer/McpUnityServer.cs` - The WebSocket server reads the secret from `query?["secret"]`
- **TS side**: `Server~/src/UnityBridge.ts` - The secret is appended to the WebSocket URL

## Problem

1. Query string parameters appear in server access logs
2. With `DEBUG=true`, the full WebSocket URL (including secret) is logged to stderr
3. WebSocket connections use `ws://` (unencrypted), not `wss://`

## Suggested Fix

- Move secret from query string to a WebSocket subprotocol header or the first message after connection
- Mask the secret in all log output, even in debug mode
- Document the security implications of using `ws://` vs `wss://`

## Severity

**High** - Credentials can leak through logs
EOF
)"
echo "Issue 1 created: Security - shared secret exposure"

# ---------------------------------------------------------------------------
# ISSUE 2 - HIGH: Thread safety - Race condition in message queue processing
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Race condition between client disconnect and message processing" \
  --label "bug,priority:high" \
  --body "$(cat <<'EOF'
## Description

In `McpUnityServer.cs`, between checking `queued.Sender.IsConnected` (line ~246) and calling `queued.Sender.SendMessage()` (line ~253), the client could disconnect. The sender reference becomes invalid but is still used.

## Location

- `Editor/McpServer/McpUnityServer.cs:235-255` (`ProcessMessageQueue`)

## Code

```csharp
if (queued.Sender == null || !queued.Sender.IsConnected)
{
    McpDebug.LogWarning("Client disconnected...");
}
else
{
    // Race: client can disconnect HERE between the check and the send
    queued.Sender.SendMessage(response);
}
```

## Suggested Fix

Wrap `SendMessage` in a try-catch for connection-related exceptions, or add a lock around the disconnect/send path.

```csharp
try
{
    queued.Sender.SendMessage(response);
}
catch (Exception ex) when (ex is InvalidOperationException || ex is WebSocketException)
{
    McpDebug.LogWarning($"Client disconnected during send: {ex.Message}");
}
```

## Severity

**High** - Can cause unhandled exceptions in multi-client scenarios
EOF
)"
echo "Issue 2 created: Race condition in message queue"

# ---------------------------------------------------------------------------
# ISSUE 3 - HIGH: Hardcoded tool count in server instructions
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Hardcoded tool counts in server instructions will become stale" \
  --label "bug,maintenance" \
  --body "$(cat <<'EOF'
## Description

Server instructions contain hardcoded tool counts (`164 tools`, `47 core tools`) that will become stale as tools are added or removed.

## Location

- `Server~/src/index.ts:112-131` - `serverInstructions` string literal

## Code

```typescript
const serverInstructions = `Unity MCP (164 tools, dynamic loading). 47 core tools (incl. 2 meta-tools) always loaded.
```

## Suggested Fix

Compute counts dynamically from the tool arrays:

```typescript
const coreToolCount = coreTools.length;
const totalToolCount = /* compute from all categories */;
const serverInstructions = `Unity MCP (${totalToolCount} tools, dynamic loading). ${coreToolCount} core tools...`;
```

## Severity

**Medium** - Misleading information sent to AI clients
EOF
)"
echo "Issue 3 created: Hardcoded tool counts"

# ---------------------------------------------------------------------------
# ISSUE 4 - MEDIUM: Silent error swallowing in bridge.connect()
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Silent error swallowing in background connection attempts" \
  --label "bug" \
  --body "$(cat <<'EOF'
## Description

Multiple instances of `bridge.connect().catch(() => {})` silently swallow connection errors, making debugging difficult when background connections fail.

## Location

- `Server~/src/index.ts:157` - `ListToolsRequestSchema` handler
- `Server~/src/index.ts:241` - `ListResourcesRequestSchema` handler
- `Server~/src/index.ts:305` - `ListPromptsRequestSchema` handler

## Code

```typescript
bridge.connect().catch(() => {}); // Errors silently swallowed
```

## Suggested Fix

At minimum, log errors at debug level:

```typescript
bridge.connect().catch((err) => {
  log('Background connection attempt failed:', err instanceof Error ? err.message : String(err));
});
```

## Severity

**Medium** - Makes debugging connection issues very difficult
EOF
)"
echo "Issue 4 created: Silent error swallowing"

# ---------------------------------------------------------------------------
# ISSUE 5 - MEDIUM: Unsafe type cast of error codes
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Unsafe type assertion on error code from Unity responses" \
  --label "bug,typescript" \
  --body "$(cat <<'EOF'
## Description

Error codes from Unity responses are directly cast to `McpErrorCode` without validation. If Unity sends an unknown error code, this creates an invalid `McpError`.

## Location

- `Server~/src/UnityBridge.ts:194`

## Code

```typescript
new McpError(
  response.error.code as McpErrorCode,  // No validation
  response.error.message,
  response.error.data
)
```

## Suggested Fix

Validate the error code before casting:

```typescript
const validCodes = Object.values(McpErrorCode);
const code = typeof response.error.code === 'number' && validCodes.includes(response.error.code)
  ? (response.error.code as McpErrorCode)
  : McpErrorCode.InternalError;

new McpError(code, response.error.message, response.error.data);
```

## Severity

**Medium** - Could propagate invalid error codes upstream
EOF
)"
echo "Issue 5 created: Unsafe error code cast"

# ---------------------------------------------------------------------------
# ISSUE 6 - MEDIUM: notify() does not handle ws.send() errors
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] WebSocket notify() silently drops send errors" \
  --label "bug,typescript" \
  --body "$(cat <<'EOF'
## Description

The `notify()` method in `UnityBridge.ts` calls `ws.send()` without handling potential send errors. If the send fails (e.g., buffer full, connection closing), the failure is completely silent.

## Location

- `Server~/src/UnityBridge.ts:350-368`

## Code

```typescript
notify(method: string, params?: unknown): void {
  // ...validation...
  this.ws.send(message);  // No error callback
}
```

## Suggested Fix

Add error handling via the send callback:

```typescript
this.ws.send(message, (error) => {
  if (error) {
    this.log(`Failed to send notification: ${error.message}`);
    this.emit('error', new McpError(McpErrorCode.ConnectionError, `Notification send failed: ${error.message}`));
  }
});
```

## Severity

**Medium** - Notifications can fail silently causing state desync
EOF
)"
echo "Issue 6 created: notify() error handling"

# ---------------------------------------------------------------------------
# ISSUE 7 - MEDIUM: Cache key serialization can throw
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Cache key generation via JSON.stringify can throw on circular references" \
  --label "bug,typescript" \
  --body "$(cat <<'EOF'
## Description

Cache key generation uses `JSON.stringify(args || {})` which can throw on circular references or non-serializable values, causing an unhandled exception in the tool call handler.

## Location

- `Server~/src/index.ts:188`

## Code

```typescript
cacheKey = `${name}:${JSON.stringify(args || {})}`;
```

## Suggested Fix

Wrap in try-catch to gracefully skip caching on serialization failure:

```typescript
try {
  cacheKey = `${name}:${JSON.stringify(args || {})}`;
} catch {
  // Skip cache for non-serializable args
  cacheKey = undefined;
}
```

## Severity

**Medium** - Unhandled exception could crash tool call handler
EOF
)"
echo "Issue 7 created: Cache key serialization"

# ---------------------------------------------------------------------------
# ISSUE 8 - MEDIUM: Silent exception suppression in JSON serialization
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Silent exception suppression during reflection-based JSON serialization" \
  --label "bug,csharp" \
  --body "$(cat <<'EOF'
## Description

In `JsonHelper.cs`, exceptions during property reflection serialization are silently caught and ignored. This can hide important errors and cause missing properties in serialized output without any indication.

## Location

- `Editor/McpServer/JsonHelper.cs:122`

## Code

```csharp
catch (Exception) { /* Skip properties that throw during reflection serialization */ }
```

## Suggested Fix

Log at debug level so issues can be diagnosed when needed:

```csharp
catch (Exception ex)
{
    McpDebug.Log($"[JsonHelper] Skipping property '{prop.Name}' on {type.Name}: {ex.Message}");
}
```

## Severity

**Medium** - Causes silent data loss in serialized output, very hard to debug
EOF
)"
echo "Issue 8 created: Silent JSON serialization exceptions"

# ---------------------------------------------------------------------------
# ISSUE 9 - MEDIUM: No rate limiting on tool execution
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Security] No rate limiting on tool execution requests" \
  --label "enhancement,security" \
  --body "$(cat <<'EOF'
## Description

`McpToolRegistry.ExecuteTool` processes all incoming requests immediately without any rate limiting. A malicious or runaway client could send many expensive tool calls simultaneously, causing the Unity Editor to become unresponsive.

## Location

- `Editor/McpServer/McpToolRegistry.cs:128-168` (ExecuteTool method)
- `Editor/McpServer/McpUnityServer.cs:234` (ProcessMessageQueue already has `MaxMessagesPerFrame` limit)

## Details

While `ProcessMessageQueue` limits messages per frame, there's no:
- Per-client rate limiting
- Backpressure mechanism
- Request queue size limit
- Timeout for long-running tool executions

## Suggested Fix

1. Add a configurable max concurrent tool executions
2. Add per-client request rate limiting
3. Add a maximum queue depth with rejection when full

## Severity

**Medium** - Resource exhaustion possible in hostile environments
EOF
)"
echo "Issue 9 created: No rate limiting"

# ---------------------------------------------------------------------------
# ISSUE 10 - MEDIUM: SseDownloadHandler memory leak
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] SseDownloadHandler does not implement IDisposable - potential memory leak" \
  --label "bug,csharp,memory" \
  --body "$(cat <<'EOF'
## Description

`SseDownloadHandler` inherits from `DownloadHandlerScript` but doesn't implement `IDisposable` or clear its internal buffers. On long-running connections or many sequential API calls, the `_charBuffer`, `StringBuilder`, and queued events can accumulate in memory.

## Location

- `Editor/McpServer/Chat/McpChatApiClient.cs:484-600` (SseDownloadHandler class)

## Suggested Fix

1. Implement `IDisposable` on `SseDownloadHandler`
2. Clear `_charBuffer`, `_lineBuffer`, and event queues in `Dispose()`
3. Ensure disposal is called in all code paths in `McpChatApiClient`

## Severity

**Medium** - Memory leak on repeated chat API calls
EOF
)"
echo "Issue 10 created: SseDownloadHandler memory leak"

# ---------------------------------------------------------------------------
# ISSUE 11 - MEDIUM: Background timer not disposed on abnormal stop
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Background tick timer not disposed on abnormal server shutdown" \
  --label "bug,csharp" \
  --body "$(cat <<'EOF'
## Description

The 200ms background tick timer (`_backgroundTickTimer`) is only disposed in `StopBackgroundTick()`, which is called from `Stop()`. If the server stops abnormally (e.g., domain reload without clean shutdown), the timer keeps running indefinitely.

## Location

- `Editor/McpServer/McpUnityServer.cs:782-784`

## Suggested Fix

Add timer disposal to:
1. A static destructor or `AssemblyReloadEvents.beforeAssemblyReload`
2. The `EditorApplication.quitting` handler (already registered but verify timer cleanup)

```csharp
[InitializeOnLoadMethod]
static void RegisterCleanup()
{
    AssemblyReloadEvents.beforeAssemblyReload += () => {
        _backgroundTickTimer?.Dispose();
        _backgroundTickTimer = null;
    };
}
```

## Severity

**Medium** - Leaked timer wastes resources
EOF
)"
echo "Issue 11 created: Background timer leak"

# ---------------------------------------------------------------------------
# ISSUE 12 - MEDIUM: Dictionary iteration during concurrent modification
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] ConcurrentDictionary iteration not atomic during broadcast" \
  --label "bug,csharp,thread-safety" \
  --body "$(cat <<'EOF'
## Description

Broadcasting messages iterates over `_connectedClients` (ConcurrentDictionary) with `foreach`. While ConcurrentDictionary is thread-safe for individual operations, iteration is not atomic with removals. If a client disconnects during broadcast, the enumeration could miss clients or throw.

## Location

- `Editor/McpServer/McpUnityServer.cs:849-860`

## Suggested Fix

Take a snapshot of clients before iterating:

```csharp
var clients = _connectedClients.Values.ToArray();
foreach (var client in clients)
{
    try { client.SendMessage(message); }
    catch (Exception ex) { McpDebug.LogWarning($"Broadcast failed: {ex.Message}"); }
}
```

## Severity

**Medium** - Can cause missed broadcasts or exceptions in multi-client scenarios
EOF
)"
echo "Issue 12 created: Broadcast iteration safety"

# ---------------------------------------------------------------------------
# ISSUE 13 - MEDIUM: Path validation incomplete for encoded sequences
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Security] Path validation does not check for URL-encoded traversal sequences" \
  --label "bug,security" \
  --body "$(cat <<'EOF'
## Description

`PathValidator.SanitizePath()` checks for `..` but does not decode URL-encoded equivalents (e.g., `%2e%2e`, `%252e%252e`) before validation. While `Path.GetFullPath` resolves the final path, the string-level check could be bypassed with encoded input.

## Location

- `Editor/McpServer/Utils/PathValidator.cs:30-51`

## Code

```csharp
path = path.Replace("\\", "/");
if (path.Contains(".."))  // Only checks literal ".."
    throw new ArgumentException("Path traversal (..) is not allowed");
```

The `Path.GetFullPath` check on line 47-49 provides a second layer of defense, but it would be more robust to also decode URL-encoded characters first.

## Suggested Fix

Add URL decoding before string-level checks:

```csharp
path = Uri.UnescapeDataString(path);
path = path.Replace("\\", "/");
```

## Severity

**Medium** - Defense in depth improvement for path traversal prevention
EOF
)"
echo "Issue 13 created: Path validation encoding"

# ---------------------------------------------------------------------------
# ISSUE 14 - MEDIUM: Unsafe cached type assertion
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] Unsafe type assertion on cached tool results" \
  --label "bug,typescript" \
  --body "$(cat <<'EOF'
## Description

Cached tool results are cast directly without validation. If cache data is somehow corrupted or the cache format changes between versions, this could return invalid data to the MCP client.

## Location

- `Server~/src/index.ts:192`

## Code

```typescript
return cached as { content: Array<{ type: string; text: string }>; isError: boolean };
```

## Suggested Fix

Add runtime validation with Zod schema before returning cached data:

```typescript
const parsed = ToolResultSchema.safeParse(cached);
if (parsed.success) {
  return parsed.data;
}
// Cache miss on invalid data
serverCache.delete(cacheKey);
```

## Severity

**Medium** - Could return malformed data from corrupted cache
EOF
)"
echo "Issue 14 created: Unsafe cache type assertion"

# ---------------------------------------------------------------------------
# ISSUE 15 - LOW: SimpleJsonParser substring overflow on truncated unicode
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] SimpleJsonParser can crash on truncated unicode escape at end of input" \
  --label "bug,csharp" \
  --body "$(cat <<'EOF'
## Description

In `SimpleJsonParser.ParseString()`, when a unicode escape sequence (`\uXXXX`) appears near the end of the JSON string, the bounds check may not fully prevent a `Substring` overflow.

## Location

- `Editor/McpServer/SimpleJsonParser.cs:124-126`

## Details

The check validates `_pos + 4` against `_json.Length`, but the `Substring(_pos + 1, 4)` call requires 4 characters after `_pos + 1`. If the escape is at `_json.Length - 4`, the substring starts at `_json.Length - 3` and needs 4 chars, overflowing.

## Suggested Fix

Change bounds check to:
```csharp
if (_pos + 5 > _json.Length)
    throw new FormatException("Truncated unicode escape");
```

## Severity

**Low** - Only affects malformed JSON with truncated unicode escapes at EOF
EOF
)"
echo "Issue 15 created: JSON parser unicode overflow"

# ---------------------------------------------------------------------------
# ISSUE 16 - LOW: Boolean argument parsing uses Epsilon incorrectly
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] ArgumentParser boolean parsing uses incorrect Epsilon comparison" \
  --label "bug,csharp" \
  --body "$(cat <<'EOF'
## Description

When parsing boolean values from floating-point numbers, `ArgumentParser` uses `Math.Abs(doubleVal) > double.Epsilon` which is semantically incorrect. `double.Epsilon` is the smallest representable positive double (~5e-324), so virtually any non-zero value passes this check. The intent seems to be treating 0 as false and non-zero as true, but the Epsilon comparison is misleading.

## Location

- `Editor/McpServer/Helpers/ArgumentParser.cs:223-226`

## Suggested Fix

Use a simple non-zero check instead:

```csharp
return doubleVal != 0;
```

## Severity

**Low** - Works in practice but is technically incorrect and confusing
EOF
)"
echo "Issue 16 created: Boolean parsing epsilon"

# ---------------------------------------------------------------------------
# ISSUE 17 - LOW: Missing error handling in file logger
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] File logger silently fails without notifying user" \
  --label "bug,csharp" \
  --body "$(cat <<'EOF'
## Description

`McpServerLogger` wraps file writes in a try-catch that silently swallows all exceptions. If disk is full, permissions are denied, or the path is invalid, logging silently stops without alerting the user.

## Location

- `Editor/McpServer/McpServerLogger.cs:161-164`

## Code

```csharp
File.AppendAllText(logPath, line);
// ...
catch { /* Silently fail */ }
```

## Suggested Fix

Log to the Unity console as a fallback when file logging fails:

```csharp
catch (Exception ex)
{
    Debug.LogWarning($"[MCP Logger] File logging failed: {ex.Message}. Disabling file logging.");
    _fileLoggingEnabled = false;
}
```

## Severity

**Low** - Users won't know when their logs are being lost
EOF
)"
echo "Issue 17 created: Silent logger failure"

# ---------------------------------------------------------------------------
# ISSUE 18 - LOW: Debug logging can expose sensitive message content
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Security] Debug mode logs full WebSocket message content including sensitive data" \
  --label "security,enhancement" \
  --body "$(cat <<'EOF'
## Description

When `DEBUG=true`, the Unity Bridge logs full WebSocket messages (both sent and received) to stderr. This can expose sensitive data like API keys, file contents, or user code.

## Location

- `Server~/src/UnityBridge.ts:165` - `this.log('Received:', message)`
- `Server~/src/UnityBridge.ts:327` - `this.log('Sending:', message)`
- `Server~/src/UnityBridge.ts:362` - `this.log('Sending notification:', message)`

## Suggested Fix

1. Truncate message content in debug logs (e.g., first 200 chars)
2. Redact known sensitive fields (secret, apiKey, etc.)
3. Add a `VERBOSE_DEBUG` level for full message logging

```typescript
this.log(`Received: [${message.length} chars]`, message.substring(0, 200));
```

## Severity

**Low** - Only affects debug mode, but can leak sensitive data
EOF
)"
echo "Issue 18 created: Debug logging sensitive data"

# ---------------------------------------------------------------------------
# ISSUE 19 - LOW: No validation of tool arguments before sending to Unity
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Enhancement] Validate tool arguments against inputSchema before forwarding to Unity" \
  --label "enhancement,typescript" \
  --body "$(cat <<'EOF'
## Description

The Node.js bridge forwards tool arguments directly to Unity without validating them against the tool's `inputSchema`. While Unity validates on its side, early validation at the bridge level would provide faster, clearer error messages and reduce unnecessary WebSocket round-trips.

## Location

- `Server~/src/index.ts:182` - Tool arguments passed through without schema validation

## Code

```typescript
const { name, arguments: args } = request.params;
// No validation against tool.inputSchema
const result = await bridge.request<ToolResult>('tools/call', { name, arguments: args || {} });
```

## Suggested Fix

Optionally validate against the tool's inputSchema (from `tools.ts`) before forwarding:

```typescript
const tool = defaultTools.find(t => t.name === name);
if (tool?.inputSchema) {
  // Validate args against schema
}
```

## Severity

**Low** - Improves error messages and reduces unnecessary round-trips
EOF
)"
echo "Issue 19 created: Tool argument validation"

# ---------------------------------------------------------------------------
# ISSUE 20 - LOW: Event handlers not cleaned on domain reload
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Bug] EditorApplication.update handler may leak across domain reloads" \
  --label "bug,csharp,unity" \
  --body "$(cat <<'EOF'
## Description

`EditorApplication.update` handlers registered in `RegisterUpdateCallback()` rely on the volatile bool `_updateRegistered` to prevent double-registration, but this flag resets on domain reload while the handler may still be registered (depending on Unity's event persistence).

## Location

- `Editor/McpServer/McpUnityServer.cs:111` - `_updateRegistered` flag
- `Editor/McpServer/McpUnityServer.cs` - `RegisterUpdateCallback()`

## Details

After domain reload:
1. `_updateRegistered` is reset to `false`
2. A new handler is registered via `+=`
3. If the old handler wasn't unregistered, both run on each frame

Unity *does* clear `EditorApplication.update` on domain reload, so this is likely safe in practice, but it's not explicitly documented and relies on Unity internals.

## Suggested Fix

Add explicit cleanup in `AssemblyReloadEvents.beforeAssemblyReload`:

```csharp
AssemblyReloadEvents.beforeAssemblyReload += () => {
    EditorApplication.update -= ProcessMessageQueue;
    _updateRegistered = false;
};
```

## Severity

**Low** - Likely safe in practice due to Unity behavior, but fragile
EOF
)"
echo "Issue 20 created: Domain reload handler cleanup"

# ---------------------------------------------------------------------------
# ISSUE 21 - LOW: Missing test coverage for edge cases
# ---------------------------------------------------------------------------
gh issue create --repo "$REPO" \
  --title "[Testing] Missing test coverage for critical edge cases" \
  --label "enhancement,testing" \
  --body "$(cat <<'EOF'
## Description

Several critical code paths lack test coverage:

### TypeScript (Server~/)
1. **Cache key serialization failure** - No test for circular reference in args
2. **Concurrent connection attempts** - No test for rapid connect/disconnect cycles
3. **Notification send failure** - No test for `notify()` when send fails
4. **Invalid error codes from Unity** - No test for unknown error code values

### C# (Editor/)
1. **Broadcast during client disconnect** - No test for concurrent disconnect/broadcast
2. **Tool execution timeout** - No test for tools that hang indefinitely
3. **SimpleJsonParser** - No test for truncated unicode escapes
4. **PathValidator** - No test for URL-encoded traversal sequences
5. **Large message handling** - No test for messages near size limits

## Suggested Actions

Add targeted unit tests for each of the above scenarios.

## Severity

**Low** - Risk of undetected regressions
EOF
)"
echo "Issue 21 created: Missing test coverage"

echo ""
echo "============================================="
echo "All 21 issues created successfully!"
echo "============================================="
