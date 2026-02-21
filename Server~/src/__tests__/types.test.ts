import { describe, it, expect } from 'vitest';
import {
  JsonRpcRequestSchema,
  JsonRpcResponseSchema,
  JsonRpcErrorSchema,
  ToolDefinitionSchema,
  BridgeConfigSchema,
  ConnectionState,
  McpErrorCode,
  McpError,
  DEFAULT_BRIDGE_CONFIG,
} from '../types.js';

// ============================================================================
// JsonRpcRequestSchema
// ============================================================================

describe('JsonRpcRequestSchema', () => {
  it('should parse a valid request', () => {
    const valid = {
      jsonrpc: '2.0',
      id: 1,
      method: 'tools/list',
    };
    expect(() => JsonRpcRequestSchema.parse(valid)).not.toThrow();
  });

  it('should parse a request with params', () => {
    const valid = {
      jsonrpc: '2.0',
      id: 'abc',
      method: 'tools/call',
      params: { name: 'unity_get_editor_state', arguments: {} },
    };
    const result = JsonRpcRequestSchema.parse(valid);
    expect(result.params).toEqual({ name: 'unity_get_editor_state', arguments: {} });
  });

  it('should reject non-2.0 jsonrpc version', () => {
    expect(() =>
      JsonRpcRequestSchema.parse({ jsonrpc: '1.0', id: 1, method: 'test' })
    ).toThrow();
  });

  it('should reject missing method', () => {
    expect(() =>
      JsonRpcRequestSchema.parse({ jsonrpc: '2.0', id: 1 })
    ).toThrow();
  });

  it('should accept string id', () => {
    const result = JsonRpcRequestSchema.parse({ jsonrpc: '2.0', id: 'req-1', method: 'ping' });
    expect(result.id).toBe('req-1');
  });

  it('should accept numeric id', () => {
    const result = JsonRpcRequestSchema.parse({ jsonrpc: '2.0', id: 42, method: 'ping' });
    expect(result.id).toBe(42);
  });
});

// ============================================================================
// JsonRpcResponseSchema
// ============================================================================

describe('JsonRpcResponseSchema', () => {
  it('should parse a success response', () => {
    const valid = {
      jsonrpc: '2.0',
      id: 1,
      result: { tools: [] },
    };
    const result = JsonRpcResponseSchema.parse(valid);
    expect(result.result).toEqual({ tools: [] });
    expect(result.error).toBeUndefined();
  });

  it('should parse an error response', () => {
    const valid = {
      jsonrpc: '2.0',
      id: 1,
      error: { code: -32601, message: 'Method not found' },
    };
    const result = JsonRpcResponseSchema.parse(valid);
    expect(result.error?.code).toBe(-32601);
    expect(result.error?.message).toBe('Method not found');
  });

  it('should parse error with optional data field', () => {
    const valid = {
      jsonrpc: '2.0',
      id: 2,
      error: { code: -32000, message: 'Connection error', data: { detail: 'timeout' } },
    };
    const result = JsonRpcResponseSchema.parse(valid);
    expect(result.error?.data).toEqual({ detail: 'timeout' });
  });

  it('should reject malformed message (missing id)', () => {
    expect(() =>
      JsonRpcResponseSchema.parse({ jsonrpc: '2.0', result: {} })
    ).toThrow();
  });

  it('should reject non-2.0 jsonrpc version', () => {
    expect(() =>
      JsonRpcResponseSchema.parse({ jsonrpc: '1.0', id: 1, result: {} })
    ).toThrow();
  });
});

// ============================================================================
// JsonRpcErrorSchema
// ============================================================================

describe('JsonRpcErrorSchema', () => {
  it('should parse a minimal error', () => {
    const result = JsonRpcErrorSchema.parse({ code: -32700, message: 'Parse error' });
    expect(result.code).toBe(-32700);
    expect(result.message).toBe('Parse error');
  });

  it('should reject missing message', () => {
    expect(() => JsonRpcErrorSchema.parse({ code: -32700 })).toThrow();
  });

  it('should reject missing code', () => {
    expect(() => JsonRpcErrorSchema.parse({ message: 'error' })).toThrow();
  });
});

// ============================================================================
// ToolDefinitionSchema
// ============================================================================

describe('ToolDefinitionSchema', () => {
  it('should parse a minimal tool definition', () => {
    const tool = {
      name: 'unity_test_tool',
      inputSchema: { type: 'object' as const, properties: {} },
    };
    expect(() => ToolDefinitionSchema.parse(tool)).not.toThrow();
  });

  it('should parse a full tool definition with required fields', () => {
    const tool = {
      name: 'unity_create_gameobject_batch',
      description: 'Create multiple GameObjects',
      inputSchema: {
        type: 'object' as const,
        properties: {
          objects: { type: 'array', description: 'Array of object descriptors' },
          stopOnError: { type: 'boolean' },
        },
        required: ['objects'],
      },
    };
    const result = ToolDefinitionSchema.parse(tool);
    expect(result.inputSchema.required).toContain('objects');
  });

  it('should reject non-object inputSchema type', () => {
    const tool = {
      name: 'unity_test',
      inputSchema: { type: 'array' },
    };
    expect(() => ToolDefinitionSchema.parse(tool)).toThrow();
  });

  it('should accept defer_loading flag', () => {
    const tool = {
      name: 'unity_bake_navmesh',
      description: 'Bake NavMesh',
      inputSchema: { type: 'object' as const, properties: {} },
      defer_loading: true,
    };
    const result = ToolDefinitionSchema.parse(tool);
    expect(result.defer_loading).toBe(true);
  });
});

// ============================================================================
// BridgeConfigSchema
// ============================================================================

describe('BridgeConfigSchema — edge cases', () => {
  it('should coerce undefined host to localhost', () => {
    const config = BridgeConfigSchema.parse({});
    expect(config.unityHost).toBe('localhost');
  });

  it('should accept port 1024 (minimum)', () => {
    const config = BridgeConfigSchema.parse({ unityPort: 1024 });
    expect(config.unityPort).toBe(1024);
  });

  it('should accept port 65535 (maximum)', () => {
    const config = BridgeConfigSchema.parse({ unityPort: 65535 });
    expect(config.unityPort).toBe(65535);
  });

  it('should reject port 1023 (below minimum)', () => {
    expect(() => BridgeConfigSchema.parse({ unityPort: 1023 })).toThrow();
  });

  it('should reject port 65536 (above maximum)', () => {
    expect(() => BridgeConfigSchema.parse({ unityPort: 65536 })).toThrow();
  });

  it('should accept maxReconnectAttempts 0', () => {
    const config = BridgeConfigSchema.parse({ maxReconnectAttempts: 0 });
    expect(config.maxReconnectAttempts).toBe(0);
  });

  it('should reject maxReconnectAttempts -1', () => {
    expect(() => BridgeConfigSchema.parse({ maxReconnectAttempts: -1 })).toThrow();
  });

  it('should accept reconnectInterval 1000 (minimum)', () => {
    const config = BridgeConfigSchema.parse({ reconnectInterval: 1000 });
    expect(config.reconnectInterval).toBe(1000);
  });

  it('should reject reconnectInterval 999 (below minimum)', () => {
    expect(() => BridgeConfigSchema.parse({ reconnectInterval: 999 })).toThrow();
  });

  it('should accept requestTimeout 1000 (minimum)', () => {
    const config = BridgeConfigSchema.parse({ requestTimeout: 1000 });
    expect(config.requestTimeout).toBe(1000);
  });

  it('should reject requestTimeout 999 (below minimum)', () => {
    expect(() => BridgeConfigSchema.parse({ requestTimeout: 999 })).toThrow();
  });

  it('DEFAULT_BRIDGE_CONFIG should match schema defaults', () => {
    expect(DEFAULT_BRIDGE_CONFIG.unityHost).toBe('localhost');
    expect(DEFAULT_BRIDGE_CONFIG.unityPort).toBe(8090);
    expect(DEFAULT_BRIDGE_CONFIG.reconnectInterval).toBe(5000);
    expect(DEFAULT_BRIDGE_CONFIG.requestTimeout).toBe(30000);
    expect(DEFAULT_BRIDGE_CONFIG.maxReconnectAttempts).toBe(10);
    expect(DEFAULT_BRIDGE_CONFIG.debug).toBe(false);
  });
});

// ============================================================================
// ConnectionState enum
// ============================================================================

describe('ConnectionState', () => {
  it('should have all expected states', () => {
    expect(ConnectionState.Disconnected).toBe('disconnected');
    expect(ConnectionState.Connecting).toBe('connecting');
    expect(ConnectionState.Connected).toBe('connected');
    expect(ConnectionState.Reconnecting).toBe('reconnecting');
    expect(ConnectionState.Failed).toBe('failed');
  });

  it('should have exactly 5 states', () => {
    const states = Object.values(ConnectionState);
    expect(states).toHaveLength(5);
  });
});

// ============================================================================
// McpErrorCode enum
// ============================================================================

describe('McpErrorCode', () => {
  it('standard JSON-RPC codes should match spec', () => {
    expect(McpErrorCode.ParseError).toBe(-32700);
    expect(McpErrorCode.InvalidRequest).toBe(-32600);
    expect(McpErrorCode.MethodNotFound).toBe(-32601);
    expect(McpErrorCode.InvalidParams).toBe(-32602);
    expect(McpErrorCode.InternalError).toBe(-32603);
  });

  it('custom MCP codes should be in the -32000 range', () => {
    const customCodes = [
      McpErrorCode.ConnectionError,
      McpErrorCode.ToolNotFound,
      McpErrorCode.ResourceNotFound,
      McpErrorCode.ExecutionError,
      McpErrorCode.TimeoutError,
      McpErrorCode.UnityError,
    ];
    for (const code of customCodes) {
      expect(code).toBeGreaterThanOrEqual(-32999);
      expect(code).toBeLessThanOrEqual(-32000);
    }
  });

  it('all custom codes should be unique', () => {
    const codes = Object.values(McpErrorCode).filter(v => typeof v === 'number') as number[];
    const unique = new Set(codes);
    expect(unique.size).toBe(codes.length);
  });
});

// ============================================================================
// McpError class
// ============================================================================

describe('McpError', () => {
  it('should create with code and message', () => {
    const err = new McpError(McpErrorCode.ConnectionError, 'Not connected');
    expect(err.code).toBe(McpErrorCode.ConnectionError);
    expect(err.message).toBe('Not connected');
    expect(err.name).toBe('McpError');
    expect(err).toBeInstanceOf(Error);
  });

  it('should store optional data', () => {
    const err = new McpError(McpErrorCode.ExecutionError, 'Execution failed', { detail: 'null ref' });
    expect(err.data).toEqual({ detail: 'null ref' });
  });

  it('toJsonRpcError() should return valid JSON-RPC error object', () => {
    const err = new McpError(McpErrorCode.TimeoutError, 'Timeout after 10s');
    const jsonRpcErr = err.toJsonRpcError();
    expect(jsonRpcErr.code).toBe(McpErrorCode.TimeoutError);
    expect(jsonRpcErr.message).toBe('Timeout after 10s');

    const parsed = JsonRpcErrorSchema.safeParse(jsonRpcErr);
    expect(parsed.success).toBe(true);
  });

  it('should be catchable as Error', () => {
    const err = new McpError(McpErrorCode.ParseError, 'Bad JSON');
    expect(() => { throw err; }).toThrow(Error);
    expect(() => { throw err; }).toThrow('Bad JSON');
  });
});
