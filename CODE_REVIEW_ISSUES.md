# MCP-Unity Code Review - Issues

> Review date: 2026-04-10
> Reviewer: Claude Code (automated review)
> Each section below = 1 GitHub issue to create on `juliankerignard/mcp-unity`

---

## Issue 1: [Security] SharedSecret stored in plaintext and not excluded from version control

**Labels:** `bug`, `security`
**Severity:** Critical

### Description

The `SharedSecret` is stored in plaintext in `ProjectSettings/McpUnitySettings.json` (line 46 of `McpSettings.cs`), and this file is **not listed in `.gitignore`**. This means the secret can be accidentally committed to the repository.

Additionally, `GenerateMcpJson()` (line 371-392) embeds the secret directly into generated config files using string interpolation.

### Files
- `Editor/McpServer/McpSettings.cs:46` - plaintext storage
- `Editor/McpServer/McpSettings.cs:376-378` - embedded in generated JSON
- `.gitignore` - missing `ProjectSettings/McpUnitySettings.json`

### Suggested Fix
1. Add `ProjectSettings/McpUnitySettings.json` to `.gitignore`
2. Consider using environment variables or Unity's encrypted PlayerPrefs for secret storage
3. At minimum, document that the settings file may contain secrets and should not be committed

---

## Issue 2: [Security] SharedSecret comparison vulnerable to timing attacks

**Labels:** `bug`, `security`
**Severity:** High

### Description

In `McpBehavior.OnOpen()` (line 934 of `McpUnityServer.cs`), the shared secret is compared using standard string inequality:

```csharp
if (clientSecret != settings.SharedSecret)
```

Standard string comparison returns early on the first mismatched character, making it vulnerable to timing attacks that can reveal the secret one character at a time.

### Files
- `Editor/McpServer/McpUnityServer.cs:934`

### Suggested Fix
Use a constant-time comparison:
```csharp
using System.Security.Cryptography;
if (!CryptographicOperations.FixedTimeEquals(
    System.Text.Encoding.UTF8.GetBytes(clientSecret ?? ""),
    System.Text.Encoding.UTF8.GetBytes(settings.SharedSecret)))
```

---

## Issue 3: [Bug] JsonHelper field.GetValue() missing try-catch causes serialization crashes

**Labels:** `bug`
**Severity:** High

### Description

In `JsonHelper.cs`, property access at line 112 is wrapped in a try-catch, but field access at line 129 is NOT:

```csharp
// Line 110-122: Properties - HAS try-catch
try { var value = prop.GetValue(obj); ... }
catch (Exception) { /* Skip */ }

// Line 129: Fields - NO try-catch
var value = field.GetValue(obj);  // Can throw!
```

If a field's getter throws (e.g., accessing a disposed Unity object), the entire JSON serialization fails, breaking the MCP response.

### Files
- `Editor/McpServer/JsonHelper.cs:129`

### Suggested Fix
Wrap `field.GetValue(obj)` in the same try-catch pattern used for properties.

---

## Issue 4: [Bug] Oversized WebSocket messages silently dropped without error response

**Labels:** `bug`
**Severity:** High

### Description

In `McpBehavior.OnMessage()` (line 957-960), messages exceeding `MaxMessageSize` (10MB) are silently dropped with only a log warning. No JSON-RPC error response is sent to the client.

```csharp
if (message.Length > MaxMessageSize)
{
    McpDebug.LogWarning(...);
    return; // Client never gets a response - will timeout
}
```

The client's pending request will hang until it times out, providing no indication of what went wrong.

### Files
- `Editor/McpServer/McpUnityServer.cs:957-960`

### Suggested Fix
Send a JSON-RPC error response before returning:
```csharp
Send(JsonHelper.ToJson(JsonRpcResponse.Error(null, JsonRpcError.InvalidRequest, 
    $"Message too large: {message.Length} bytes (max {MaxMessageSize})")));
```

---

## Issue 5: [Bug] Event handlers accumulate on domain reload - never unregistered

**Labels:** `bug`
**Severity:** Medium

### Description

In the static constructor `McpUnityServer()` (line 157-163), event handlers are registered:

```csharp
EditorApplication.quitting += Stop;
EditorApplication.delayCall += OnEditorLoaded;
Application.logMessageReceived += HandleLogMessage;
```

These are never unregistered in `Stop()` or elsewhere. On Unity domain reload (entering play mode, recompiling), the static constructor runs again, registering duplicate handlers. This causes:
- `HandleLogMessage` called multiple times per log entry
- `Stop` called multiple times on quit

### Files
- `Editor/McpServer/McpUnityServer.cs:157-163`
- `Editor/McpServer/McpUnityServer.cs:744-762` (Stop method - no cleanup)

### Suggested Fix
Unregister handlers in `Stop()`, or use a flag pattern to prevent double-registration (which is partially done for `_logHandlerRegistered` but not for `quitting`).

---

## Issue 6: [Bug] PathValidator StartsWith check can match unrelated directories

**Labels:** `bug`, `security`
**Severity:** Medium

### Description

In `PathValidator.SanitizePath()` (line 48), the symlink resolution check uses `StartsWith`:

```csharp
string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
string resolvedPath = Path.GetFullPath(path);
if (!resolvedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
```

If `projectRoot` is `/home/user/project`, a symlink resolving to `/home/user/project-evil/` would pass this check because `StartsWith` matches the prefix.

### Files
- `Editor/McpServer/Utils/PathValidator.cs:46-49`

### Suggested Fix
Append a directory separator to the project root before comparison:
```csharp
string projectRootWithSep = projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
if (!resolvedPath.StartsWith(projectRootWithSep, StringComparison.OrdinalIgnoreCase)
    && !resolvedPath.Equals(projectRoot, StringComparison.OrdinalIgnoreCase))
```

---

## Issue 7: [Bug] JsonHelper ToJson() reentrancy corrupts shared StringBuilder

**Labels:** `bug`
**Severity:** Medium

### Description

`JsonHelper.ToJson()` uses a `[ThreadStatic]` shared `StringBuilder` (line 15-16):

```csharp
[ThreadStatic] private static System.Text.StringBuilder _sharedSb;

public static string ToJson(object obj)
{
    _sharedSb.Clear();
    AppendJson(_sharedSb, obj, null);
    return _sharedSb.ToString();
}
```

During reflection-based serialization (line 112), `prop.GetValue(obj)` can trigger property getters that internally call `ToJson()`, causing reentrancy. The second call clears the StringBuilder that the first call is still using, corrupting the output.

### Files
- `Editor/McpServer/JsonHelper.cs:15-23`
- `Editor/McpServer/JsonHelper.cs:110-112`

### Suggested Fix
Use a stack-based approach or detect reentrancy:
```csharp
[ThreadStatic] private static int _depth;

public static string ToJson(object obj)
{
    if (_depth > 0) // Reentrant call - use new StringBuilder
        return ToJsonSlow(obj);
    _depth++;
    try { ... }
    finally { _depth--; }
}
```

---

## Issue 8: [Bug] McpResourceRegistry rethrows exceptions losing original stack trace

**Labels:** `bug`
**Severity:** Medium

### Description

In `McpResourceRegistry.ReadResource()` (line 118), exceptions are caught and rethrown with a new `Exception`, losing the original exception type and stack trace:

```csharp
catch (Exception ex)
{
    throw new Exception($"Failed to read resource: {ex.Message}"); // Lost inner exception!
}
```

This makes debugging resource read failures difficult because the original exception details (including the call stack) are discarded.

### Files
- `Editor/McpServer/McpResourceRegistry.cs:115-119`

### Suggested Fix
Preserve the original exception as inner exception:
```csharp
throw new Exception($"Failed to read resource: {ex.Message}", ex);
```

---

## Issue 9: [Bug] MCP config JSON generation vulnerable to injection via string interpolation

**Labels:** `bug`, `security`
**Severity:** Medium

### Description

`McpSettings.GenerateMcpJson()` (line 371-392) builds JSON via raw string interpolation:

```csharp
return $@"{{
  ""{rootKey}"": {{
    ""mcp-unity"": {{
      ""command"": ""node"",
      ""args"": [""{escapedPath}""],
      ""env"": {{
        ""UNITY_PORT"": ""{_port}"",
        ""UNITY_HOST"": ""{_host}""{secretEnv}
      }}
    }}
  }}
}}";
```

While `escapedPath` escapes quotes, `_host` is directly interpolated without any escaping. A host value containing `"` or other JSON special characters would produce invalid or injected JSON.

### Files
- `Editor/McpServer/McpSettings.cs:371-392`
- `Editor/McpServer/McpSettings.cs:512-533` (`GenerateClaudeConfig` has same issue)

### Suggested Fix
Escape all interpolated values, or use a proper JSON serializer to build the config.

---

## Issue 10: [Bug] SimpleJsonParser doesn't handle UTF-16 surrogate pairs in \u escapes

**Labels:** `bug`
**Severity:** Low

### Description

The `ParseString()` method in `SimpleJsonParser.cs` (line 123-141) handles `\uXXXX` escape sequences by converting directly to a `char`:

```csharp
_sb.Append((char)codePoint);
```

This doesn't handle surrogate pairs (characters above U+FFFF like emojis). JSON encodes these as two `\uXXXX` escapes (e.g., `\uD83D\uDE00` for the grinning face emoji). The parser treats each as an independent character, resulting in invalid/corrupted Unicode output.

### Files
- `Editor/McpServer/SimpleJsonParser.cs:123-141`

### Suggested Fix
After parsing a high surrogate (`\uD800-\uDBFF`), check if the next escape is a low surrogate (`\uDC00-\uDFFF`) and combine them using `char.ConvertFromUtf32()`.

---

## Issue 11: [Performance] TypeScript cache key generation is non-deterministic

**Labels:** `bug`, `performance`
**Severity:** Medium

### Description

In `index.ts` (line 188), cache keys for tool results are generated using:

```typescript
cacheKey = `${name}:${JSON.stringify(args || {})}`;
```

`JSON.stringify()` does not guarantee consistent property ordering. The same arguments `{a:1, b:2}` and `{b:2, a:1}` will produce different cache keys, causing cache misses for semantically identical requests.

### Files
- `Server~/src/index.ts:188`

### Suggested Fix
Sort object keys before stringifying:
```typescript
const sortedArgs = JSON.stringify(args || {}, Object.keys(args || {}).sort());
cacheKey = `${name}:${sortedArgs}`;
```

---

## Issue 12: [Bug] requestId in UnityBridge.ts can overflow Number.MAX_SAFE_INTEGER

**Labels:** `bug`
**Severity:** Low

### Description

In `UnityBridge.ts` (line 301), the request ID is a simple incrementing integer:

```typescript
const id = ++this.requestId;
```

While unlikely in practice, after `Number.MAX_SAFE_INTEGER` (2^53) increments, the integer loses precision, potentially matching existing pending request IDs and causing response routing errors.

### Files
- `Server~/src/UnityBridge.ts:301`

### Suggested Fix
Reset the counter periodically, or use modular arithmetic:
```typescript
this.requestId = (this.requestId + 1) % Number.MAX_SAFE_INTEGER;
```

---

## Issue 13: [Bug] WebSocket notify() in UnityBridge doesn't handle send errors

**Labels:** `bug`
**Severity:** Medium

### Description

The `notify()` method in `UnityBridge.ts` (line 366) calls `ws.send()` without an error callback:

```typescript
this.ws.send(message); // No error callback!
```

Compare with `request()` (line 335) which properly handles send errors:

```typescript
this.ws.send(message, (error) => {
  if (error) { /* cleanup */ }
});
```

Send errors during notifications are silently lost, which could mask connection issues.

### Files
- `Server~/src/UnityBridge.ts:350-368`

### Suggested Fix
Add an error callback:
```typescript
this.ws.send(message, (error) => {
  if (error) {
    this.log(`Failed to send notification:`, error);
    this.emit('error', new McpError(McpErrorCode.ConnectionError, `Notify failed: ${error.message}`));
  }
});
```

---

## Issue 14: [Config] Default config values inconsistent between TypeScript schema and getConfig()

**Labels:** `bug`, `documentation`
**Severity:** Medium

### Description

The Zod schema `BridgeConfigSchema` in `types.ts` defines defaults:
- `reconnectInterval: 5000` (line 219)
- `requestTimeout: 30000` (line 220)
- `maxReconnectAttempts: 10` (line 221)

But `getConfig()` in `index.ts` overrides them with different values:
- `reconnectInterval: 3000` (line 42)
- `requestTimeout: 10000` (line 43)
- `maxReconnectAttempts: 3` (line 45)

This creates confusion about what the actual defaults are, and means `DEFAULT_BRIDGE_CONFIG` (types.ts:227) doesn't match the runtime defaults.

### Files
- `Server~/src/types.ts:216-222`
- `Server~/src/index.ts:36-49`

### Suggested Fix
Align the defaults in one place. Either update the Zod schema to match the intended values, or remove the overrides in `getConfig()`.

---

## Issue 15: [Bug] process.exit(0) in cleanup handler prevents graceful shutdown

**Labels:** `bug`
**Severity:** Low

### Description

The cleanup handler in `index.ts` (line 361-366) calls `process.exit(0)` directly:

```typescript
const cleanup = async () => {
    log('Shutting down...');
    serverCache.destroy();
    await bridge.disconnect();
    process.exit(0); // Forces immediate exit
};
```

`process.exit()` terminates the process immediately, which may not flush all pending I/O (stdout/stderr buffers, pending WebSocket close frames).

### Files
- `Server~/src/index.ts:361-366`

### Suggested Fix
Let the event loop drain naturally after cleanup:
```typescript
const cleanup = async () => {
    log('Shutting down...');
    serverCache.destroy();
    await bridge.disconnect();
    // Event loop will exit naturally when no more work is pending
};
```

---

## Issue 16: [Quality] Version "1.0.0" hardcoded in 3 separate places with no single source of truth

**Labels:** `enhancement`, `tech-debt`
**Severity:** Low

### Description

The version string `"1.0.0"` is duplicated in three files:
1. `Server~/package.json:3` - `"version": "1.0.0"`
2. `Server~/src/index.ts:136` - `version: '1.0.0'`
3. `Editor/McpServer/McpJsonRpc.cs:184` - `version = "1.0.0"`

When the version is bumped, all three must be updated manually, risking inconsistency.

### Files
- `Server~/package.json:3`
- `Server~/src/index.ts:136`
- `Editor/McpServer/McpJsonRpc.cs:184`

### Suggested Fix
For TypeScript: import version from `package.json`. For C#: consider reading from a single `McpConstants.cs` or embedding it at build time.

---

## Issue 17: [Security] WebSocket server binds to 127.0.0.1 but AllowRemoteConnections changes host without proper validation

**Labels:** `security`, `enhancement`
**Severity:** Medium

### Description

In `McpSettings.cs` (line 75-84), setting `AllowRemoteConnections = true` changes the host to `"0.0.0.0"`:

```csharp
public bool AllowRemoteConnections
{
    get => _allowRemoteConnections;
    set
    {
        _allowRemoteConnections = value;
        _host = value ? "0.0.0.0" : "localhost";
        Save();
    }
}
```

However, the server in `McpUnityServer.Start()` (line 724) always uses `127.0.0.1`:

```csharp
_wss = new WebSocketServer($"ws://127.0.0.1:{Port}");
```

This means:
1. The `AllowRemoteConnections` setting has NO effect - server always binds to localhost
2. If someone "fixes" this to use `_host`, remote connections would be exposed without proper security warnings
3. The settings UI creates a false sense of security

### Files
- `Editor/McpServer/McpSettings.cs:75-84`
- `Editor/McpServer/McpUnityServer.cs:724`

### Suggested Fix
Either:
1. Remove the `AllowRemoteConnections` setting since it doesn't work, OR
2. Make it functional by using the configured host, but add strong warnings and require the shared secret when remote connections are enabled

---

## Issue 18: [Bug] All tool categories enabled by default - dynamic loading system ineffective

**Labels:** `bug`
**Severity:** Medium

### Description

In `McpToolRegistry.cs` (line 28-29), all categories are enabled at initialization:

```csharp
private readonly HashSet<string> _enabledCategories
    = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "core", "asset", "material", "ui", "animator", "terrain",
        "physics", "audio", "rendering", "build", "settings", "input", "advanced"
    };
```

The server instructions in `index.ts` (line 112) say: "Call unity_enable_tool_category(category) to load more", and tools like `unity_enable_tool_category` exist specifically for dynamic loading.

But since ALL categories are already enabled, the entire dynamic category system has no effect. Every `tools/list` call returns all 164 tools, negating the purpose of reducing token usage.

### Files
- `Editor/McpServer/McpToolRegistry.cs:28-29`

### Suggested Fix
Only enable `"core"` by default:
```csharp
private readonly HashSet<string> _enabledCategories
    = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "core" };
```

---

## Issue 19: [Bug] ServerCache cleanup timer keeps Node.js process alive

**Labels:** `bug`
**Severity:** Low

### Description

In `cache.ts` (line 17), the `ServerCache` constructor starts an interval timer:

```typescript
private cleanupTimer = setInterval(() => this.cleanup(), 60_000);
```

This timer keeps the Node.js event loop active, preventing the process from exiting gracefully. While `destroy()` clears it, if `destroy()` is never called (e.g., crash or unhandled exception), the process will hang.

The timer should use `unref()` so it doesn't prevent process exit:

### Files
- `Server~/src/cache.ts:17`

### Suggested Fix
```typescript
private cleanupTimer = (() => {
    const timer = setInterval(() => this.cleanup(), 60_000);
    timer.unref(); // Don't keep process alive just for cache cleanup
    return timer;
})();
```

---

## Issue 20: [Quality] Missing `author` field in package.json

**Labels:** `enhancement`
**Severity:** Low

### Description

The `package.json` has an empty `author` field:

```json
"author": "",
```

This should identify the maintainer for npm publishing and attribution purposes.

### Files
- `Server~/package.json:53`

### Suggested Fix
Set the author field to the repository owner, e.g.:
```json
"author": "juliankerignard"
```
