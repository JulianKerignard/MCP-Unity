import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

// Must use vi.hoisted so the class is available when vi.mock factory runs (hoisted to top)
const { MockWebSocket } = vi.hoisted(() => {
  // Use require() inside hoisted — ESM imports are not available here
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const { EventEmitter } = require('events') as typeof import('events');

  class MockWebSocket extends EventEmitter {
    static OPEN = 1;
    static CLOSED = 3;

    readyState = MockWebSocket.OPEN;
    url: string;

    constructor(url: string) {
      super();
      this.url = url;
      MockWebSocket._lastInstance = this;
      MockWebSocket._instanceCount++;
    }

    send = vi.fn((_data: string, cb?: (err?: Error) => void) => {
      if (cb) cb();
    });

    close = vi.fn(() => {
      this.readyState = MockWebSocket.CLOSED;
    });

    static _lastInstance: MockWebSocket | null = null;
    static _instanceCount = 0;
  }
  return { MockWebSocket };
});

vi.mock('ws', () => ({
  default: MockWebSocket,
}));

// Import after mock is set up
import { UnityBridge } from '../UnityBridge.js';
import { ConnectionState, McpErrorCode, BridgeConfigSchema } from '../types.js';

describe('UnityBridge', () => {
  let bridge: UnityBridge;

  beforeEach(() => {
    vi.useFakeTimers();
    MockWebSocket._lastInstance = null;
    MockWebSocket._instanceCount = 0;
    bridge = new UnityBridge({
      unityHost: 'localhost',
      unityPort: 8090,
      requestTimeout: 5000,
      reconnectInterval: 1000,
      maxReconnectAttempts: 2,
      debug: false,
    });
  });

  afterEach(() => {
    bridge.removeAllListeners();
    vi.useRealTimers();
  });

  describe('construction', () => {
    it('should create with default config', () => {
      const defaultBridge = new UnityBridge();
      expect(defaultBridge.state).toBe(ConnectionState.Disconnected);
      expect(defaultBridge.isConnected).toBe(false);
    });

    it('should create with partial config', () => {
      const customBridge = new UnityBridge({ unityPort: 9999 });
      expect(customBridge.state).toBe(ConnectionState.Disconnected);
    });
  });

  describe('initial state', () => {
    it('should be disconnected initially', () => {
      expect(bridge.state).toBe(ConnectionState.Disconnected);
    });

    it('should report isConnected as false initially', () => {
      expect(bridge.isConnected).toBe(false);
    });
  });

  describe('connect', () => {
    it('should emit connected event when WebSocket opens', async () => {
      const connectedHandler = vi.fn();
      bridge.on('connected', connectedHandler);

      const connectPromise = bridge.connect();

      // Simulate WebSocket open
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');

      await connectPromise;

      expect(connectedHandler).toHaveBeenCalledOnce();
      expect(bridge.state).toBe(ConnectionState.Connected);
    });

    it('should transition to Connecting state during connect', () => {
      const stateChanges: ConnectionState[] = [];
      bridge.on('stateChange', (newState: ConnectionState) => {
        stateChanges.push(newState);
      });

      bridge.connect().catch(() => {});

      expect(stateChanges).toContain(ConnectionState.Connecting);
    });

    it('should reject if connection fails with error', async () => {
      // Prevent unhandled 'error' event from EventEmitter on the bridge
      bridge.on('error', () => {});
      const connectPromise = bridge.connect();

      const ws = MockWebSocket._lastInstance!;
      ws.emit('error', new Error('Connection refused'));

      await expect(connectPromise).rejects.toThrow('Connection failed');
    });

    it('should coalesce concurrent connect() calls into a single in-flight connection', async () => {
      // SEC-#422: previously the second call threw "already in progress", which combined with
      // a stuck Connecting state made the bridge permanently unusable. Now both calls share the
      // same promise and resolve/reject together.
      const first = bridge.connect();
      const second = bridge.connect();

      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');

      await expect(first).resolves.toBeUndefined();
      await expect(second).resolves.toBeUndefined();
      // Only one underlying socket should have been created.
      expect(MockWebSocket._instanceCount).toBe(1);
    });

    it('should return immediately if already connected', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Second connect should resolve immediately
      await bridge.connect();
      expect(bridge.state).toBe(ConnectionState.Connected);
    });
  });

  describe('disconnect', () => {
    it('should emit stateChange to Disconnected', async () => {
      // Connect first
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      const stateChanges: ConnectionState[] = [];
      bridge.on('stateChange', (newState: ConnectionState) => {
        stateChanges.push(newState);
      });

      await bridge.disconnect();

      expect(stateChanges).toContain(ConnectionState.Disconnected);
      expect(bridge.state).toBe(ConnectionState.Disconnected);
    });

    it('should close the WebSocket', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      await bridge.disconnect();

      expect(ws.close).toHaveBeenCalled();
    });
  });

  describe('stateChange events', () => {
    it('should emit stateChange on transitions', async () => {
      const transitions: Array<{ newState: ConnectionState; oldState: ConnectionState }> = [];
      bridge.on('stateChange', (newState: ConnectionState, oldState: ConnectionState) => {
        transitions.push({ newState, oldState });
      });

      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      expect(transitions).toEqual([
        { newState: ConnectionState.Connecting, oldState: ConnectionState.Disconnected },
        { newState: ConnectionState.Connected, oldState: ConnectionState.Connecting },
      ]);
    });
  });

  describe('request', () => {
    it('should reject if not connected', async () => {
      await expect(bridge.request('test/method')).rejects.toThrow('Not connected to Unity');
    });

    it('should reject if not connected with correct error code', async () => {
      try {
        await bridge.request('test/method');
        expect.unreachable('Should have thrown');
      } catch (error: unknown) {
        const mcpError = error as { code: number };
        expect(mcpError.code).toBe(McpErrorCode.ConnectionError);
      }
    });

    it('should send JSON-RPC request when connected', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Don't await - we need to respond first
      const requestPromise = bridge.request('tools/list', { foo: 'bar' });

      expect(ws.send).toHaveBeenCalled();
      const sentData = ws.send.mock.calls[0]![0] as string;
      const parsed = JSON.parse(sentData);
      expect(parsed.jsonrpc).toBe('2.0');
      expect(parsed.method).toBe('tools/list');
      expect(parsed.params).toEqual({ foo: 'bar' });
      expect(parsed.id).toBeDefined();

      // Simulate response
      ws.emit(
        'message',
        JSON.stringify({
          jsonrpc: '2.0',
          id: parsed.id,
          result: { tools: [] },
        })
      );

      const result = await requestPromise;
      expect(result).toEqual({ tools: [] });
    });

    it('should timeout with fake timers', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      const requestPromise = bridge.request('slow/method');

      // Advance time past the request timeout (5000ms)
      vi.advanceTimersByTime(5001);

      await expect(requestPromise).rejects.toThrow('Request timeout');
    });

    it('should reject with error when response contains error', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      const requestPromise = bridge.request('failing/method');

      const sentData = ws.send.mock.calls[0]![0] as string;
      const parsed = JSON.parse(sentData);

      ws.emit(
        'message',
        JSON.stringify({
          jsonrpc: '2.0',
          id: parsed.id,
          error: { code: -32601, message: 'Method not found' },
        })
      );

      await expect(requestPromise).rejects.toThrow('Method not found');
    });
  });

  describe('isConnected', () => {
    it('should return true when connected with OPEN WebSocket', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      expect(bridge.isConnected).toBe(true);
    });

    it('should return false after disconnect', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      await bridge.disconnect();

      expect(bridge.isConnected).toBe(false);
    });
  });

  describe('notify', () => {
    it('should throw if not connected', () => {
      expect(() => bridge.notify('some/event')).toThrow('Not connected to Unity');
    });

    it('should send notification without id', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      bridge.notify('editor/refresh', { force: true });

      expect(ws.send).toHaveBeenCalled();
      const sentData = ws.send.mock.calls[0]![0] as string;
      const parsed = JSON.parse(sentData);
      expect(parsed.jsonrpc).toBe('2.0');
      expect(parsed.method).toBe('editor/refresh');
      expect(parsed.params).toEqual({ force: true });
      expect(parsed.id).toBeUndefined();
    });
  });

  describe('handleDisconnect', () => {
    it('should reject pending requests on disconnect', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      const requestPromise = bridge.request('pending/method');

      // Simulate WebSocket close (triggers handleDisconnect)
      ws.emit('close', 1000, Buffer.from('normal closure'));

      await expect(requestPromise).rejects.toThrow('Connection closed');
    });

    it('should emit disconnected event', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      const disconnectedHandler = vi.fn();
      bridge.on('disconnected', disconnectedHandler);

      ws.emit('close', 1000, Buffer.from(''));

      expect(disconnectedHandler).toHaveBeenCalledOnce();
    });
  });
});

describe('BridgeConfigSchema', () => {
  it('should parse empty object with defaults', () => {
    const config = BridgeConfigSchema.parse({});
    expect(config.unityHost).toBe('localhost');
    expect(config.unityPort).toBe(8090);
    expect(config.reconnectInterval).toBe(5000);
    expect(config.requestTimeout).toBe(30000);
    expect(config.maxReconnectAttempts).toBe(10);
    expect(config.debug).toBe(false);
  });

  it('should accept valid custom values', () => {
    const config = BridgeConfigSchema.parse({
      unityHost: '192.168.1.100',
      unityPort: 9090,
      reconnectInterval: 2000,
      requestTimeout: 5000,
      maxReconnectAttempts: 5,
      debug: true,
    });
    expect(config.unityHost).toBe('192.168.1.100');
    expect(config.unityPort).toBe(9090);
    expect(config.debug).toBe(true);
  });

  it('should reject port below 1024', () => {
    expect(() => BridgeConfigSchema.parse({ unityPort: 80 })).toThrow();
  });

  it('should reject port above 65535', () => {
    expect(() => BridgeConfigSchema.parse({ unityPort: 70000 })).toThrow();
  });

  it('should reject negative reconnect attempts', () => {
    expect(() => BridgeConfigSchema.parse({ maxReconnectAttempts: -1 })).toThrow();
  });

  it('should reject reconnectInterval below 1000', () => {
    expect(() => BridgeConfigSchema.parse({ reconnectInterval: 500 })).toThrow();
  });

  it('should reject requestTimeout below 1000', () => {
    expect(() => BridgeConfigSchema.parse({ requestTimeout: 100 })).toThrow();
  });
});
