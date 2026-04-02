# MCP-Unity - Code Review Report

**Date**: 2026-04-02  
**Scope**: Full codebase (35,268 lines C#, 4,246 lines TypeScript)  
**Issues found**: 21

---

## Summary by Severity

| Severity | Count | Categories |
|----------|-------|------------|
| **Critical/High** | 2 | Security, Thread Safety |
| **Medium** | 12 | Bugs, Security, Memory, Error Handling |
| **Low** | 7 | Bugs, Enhancements, Testing |

---

## Critical / High Priority

### 1. [Security] Shared secret exposed in WebSocket URL query string
- **Files**: `McpUnityServer.cs`, `UnityBridge.ts`
- **Risk**: Credentials leak through server logs, debug output
- **Fix**: Move secret to WebSocket header or first-message auth

### 2. [Bug] Race condition between client disconnect and message processing
- **File**: `McpUnityServer.cs:235-255`
- **Risk**: Unhandled exception when client disconnects during `SendMessage()`
- **Fix**: Wrap send in try-catch for connection-related exceptions

---

## Medium Priority

### 3. Hardcoded tool counts in server instructions
- **File**: `index.ts:112` — `"164 tools"` and `"47 core tools"` hardcoded
- **Fix**: Compute dynamically from tool arrays

### 4. Silent error swallowing in background `bridge.connect()`
- **File**: `index.ts:157,241,305` — `.catch(() => {})`
- **Fix**: Log errors at debug level

### 5. Unsafe type assertion on error codes
- **File**: `UnityBridge.ts:194` — `response.error.code as McpErrorCode`
- **Fix**: Validate against known error codes before casting

### 6. WebSocket `notify()` silently drops send errors
- **File**: `UnityBridge.ts:350-368`
- **Fix**: Add error callback to `ws.send()`

### 7. Cache key `JSON.stringify` can throw on circular refs
- **File**: `index.ts:188`
- **Fix**: Wrap in try-catch, skip cache on failure

### 8. Silent exception suppression in JSON reflection serialization
- **File**: `JsonHelper.cs:122` — empty `catch (Exception)`
- **Fix**: Log at debug level

### 9. No rate limiting on tool execution
- **File**: `McpToolRegistry.cs:128-168`
- **Fix**: Add per-client rate limiting and queue depth limit

### 10. `SseDownloadHandler` does not implement `IDisposable`
- **File**: `McpChatApiClient.cs:484-600`
- **Fix**: Implement `IDisposable`, clear buffers

### 11. Background tick timer not disposed on abnormal shutdown
- **File**: `McpUnityServer.cs:782-784`
- **Fix**: Add cleanup in `AssemblyReloadEvents.beforeAssemblyReload`

### 12. `ConcurrentDictionary` iteration not atomic during broadcast
- **File**: `McpUnityServer.cs:849-860`
- **Fix**: Snapshot clients to array before iterating

### 13. Path validation doesn't check URL-encoded traversal sequences
- **File**: `PathValidator.cs:30-51`
- **Fix**: Add `Uri.UnescapeDataString()` before checks

### 14. Unsafe type assertion on cached tool results
- **File**: `index.ts:192` — `cached as { ... }`
- **Fix**: Validate with Zod schema before returning

---

## Low Priority

### 15. `SimpleJsonParser` can crash on truncated unicode at EOF
- **File**: `SimpleJsonParser.cs:124-126`
- **Fix**: Adjust bounds check to `_pos + 5 > _json.Length`

### 16. Boolean parsing uses incorrect `Epsilon` comparison
- **File**: `ArgumentParser.cs:223-226`
- **Fix**: Use `doubleVal != 0` instead

### 17. File logger silently fails without notifying user
- **File**: `McpServerLogger.cs:161-164`
- **Fix**: Fall back to Unity console on file write failure

### 18. Debug mode logs full message content including sensitive data
- **File**: `UnityBridge.ts:165,327,362`
- **Fix**: Truncate messages, redact sensitive fields

### 19. No validation of tool arguments before forwarding to Unity
- **File**: `index.ts:182`
- **Fix**: Optional schema validation before WebSocket send

### 20. `EditorApplication.update` handler may leak across domain reloads
- **File**: `McpUnityServer.cs:111`
- **Fix**: Explicit cleanup in `beforeAssemblyReload`

### 21. Missing test coverage for critical edge cases
- Circular ref in cache key, concurrent connect/disconnect, truncated unicode, URL-encoded paths, etc.

---

## How to Create GitHub Issues

Run the provided script (requires `gh` CLI authenticated):

```bash
gh auth login
bash create-github-issues.sh
```

This will create all 21 issues with proper labels and detailed descriptions.
