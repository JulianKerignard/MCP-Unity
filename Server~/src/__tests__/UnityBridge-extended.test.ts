import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

const { MockWebSocket } = vi.hoisted(() => {
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
    }

    send = vi.fn((_data: string, cb?: (err?: Error) => void) => {
      if (cb) cb();
    });

    close = vi.fn(() => {
      this.readyState = MockWebSocket.CLOSED;
    });

    static _lastInstance: MockWebSocket | null = null;
  }
  return { MockWebSocket };
});

vi.mock('ws', () => ({
  default: MockWebSocket,
}));

import { UnityBridge } from '../UnityBridge.js';
import { ConnectionState, McpErrorCode } from '../types.js';

describe('UnityBridge — extended tests', () => {
  let bridge: UnityBridge;

  beforeEach(() => {
    vi.useFakeTimers();
    MockWebSocket._lastInstance = null;
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

  // ========================================================================
  // Message parsing edge cases
  // ========================================================================

  describe('handleMessage edge cases', () => {
    it('should handle malformed JSON gracefully', async () => {
      bridge.on('error', () => {}); // suppress unhandled
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Send garbage — should not crash
      expect(() => ws.emit('message', 'not-json{{')).not.toThrow();
    });

    it('should handle response with unknown id (no pending request)', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Response for a request we never made — should log, not crash
      expect(() =>
        ws.emit(
          'message',
          JSON.stringify({ jsonrpc: '2.0', id: 999999, result: {} })
        )
      ).not.toThrow();
    });

    it('should handle notification messages (no id, has method)', async () => {
      const notificationHandler = vi.fn();
      bridge.on('notification', notificationHandler);

      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      ws.emit(
        'message',
        JSON.stringify({ jsonrpc: '2.0', method: 'tools/list_changed' })
      );

      expect(notificationHandler).toHaveBeenCalledWith(
        expect.objectContaining({ method: 'tools/list_changed' })
      );
    });
  });

  // ========================================================================
  // WebSocket null guard (ws closed before send)
  // ========================================================================

  describe('null WebSocket guards', () => {
    it('notify should throw McpError when ws is null', async () => {
      // Bridge is disconnected — ws is null
      try {
        bridge.notify('test/event');
        expect.unreachable('Should have thrown');
      } catch (error: unknown) {
        const mcpError = error as { code: number };
        expect(mcpError.code).toBe(McpErrorCode.ConnectionError);
      }
    });

    it('request should reject with ConnectionError when disconnected', async () => {
      await expect(bridge.request('test/method')).rejects.toThrow('Not connected');
    });
  });

  // ========================================================================
  // waitForConnection
  // ========================================================================

  describe('waitForConnection', () => {
    it('should resolve immediately if already connected', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Should resolve instantly
      await bridge.waitForConnection(1000);
      expect(bridge.isConnected).toBe(true);
    });

    it('should resolve when connection is established within timeout', async () => {
      // Start waiting before connection
      const waitPromise = bridge.waitForConnection(5000);

      // Now connect
      bridge.connect().catch(() => {});
      const ws = MockWebSocket._lastInstance!;

      // Simulate delayed open
      setTimeout(() => ws.emit('open'), 100);
      vi.advanceTimersByTime(100);

      await waitPromise;
      expect(bridge.isConnected).toBe(true);
    });

    it('should reject on timeout', async () => {
      const waitPromise = bridge.waitForConnection(2000);

      // Never connect — advance past timeout
      vi.advanceTimersByTime(2001);

      await expect(waitPromise).rejects.toThrow();
    });
  });

  // ========================================================================
  // Connection timeout
  // ========================================================================

  describe('connection timeout', () => {
    it('should reject connect() if WebSocket never opens', async () => {
      bridge.on('error', () => {}); // suppress
      const connectPromise = bridge.connect();

      // Advance past request timeout
      vi.advanceTimersByTime(6000);

      await expect(connectPromise).rejects.toThrow();
    });
  });

  // ========================================================================
  // send() error callback
  // ========================================================================

  describe('request send error', () => {
    it('should reject when ws.send() returns error', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Make send call the callback with an error
      ws.send = vi.fn((_data: string, cb?: (err?: Error) => void) => {
        if (cb) cb(new Error('Write failed'));
      });

      await expect(bridge.request('test/method')).rejects.toThrow('Failed to send request');
    });
  });

  // ========================================================================
  // Reconnection logic
  // ========================================================================

  describe('reconnection', () => {
    it('should attempt reconnection after unexpected close', async () => {
      const stateChanges: ConnectionState[] = [];
      bridge.on('stateChange', (s: ConnectionState) => stateChanges.push(s));
      bridge.on('error', () => {}); // suppress

      const connectPromise = bridge.connect();
      const ws1 = MockWebSocket._lastInstance!;
      ws1.emit('open');
      await connectPromise;

      // Simulate unexpected close (not normal closure)
      ws1.emit('close', 1006, Buffer.from('abnormal'));

      // Should transition to Reconnecting
      expect(stateChanges).toContain(ConnectionState.Reconnecting);
    });

    it('should transition to non-Connected state after unexpected close', async () => {
      bridge.on('error', () => {}); // suppress

      const connectPromise = bridge.connect();
      const ws1 = MockWebSocket._lastInstance!;
      ws1.emit('open');
      await connectPromise;

      expect(bridge.state).toBe(ConnectionState.Connected);

      // Unexpected close
      ws1.emit('close', 1006, Buffer.from(''));

      // State should no longer be Connected
      expect(bridge.state).not.toBe(ConnectionState.Connected);
    });
  });

  // ========================================================================
  // Concurrent request handling (SEC-#439)
  // ========================================================================
  describe('concurrent request()', () => {
    it('should route interleaved responses to the correct pending request', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Kick off two requests before answering either.
      const pA = bridge.request<{ v: string }>('tools/list');
      const pB = bridge.request<{ v: string }>('resources/list');

      // Two distinct IDs should be in flight.
      expect(ws.send).toHaveBeenCalledTimes(2);
      const sentA = JSON.parse((ws.send as any).mock.calls[0][0]);
      const sentB = JSON.parse((ws.send as any).mock.calls[1][0]);
      expect(sentA.id).not.toBe(sentB.id);

      // Answer B first, then A — responses must route correctly.
      ws.emit('message', JSON.stringify({ jsonrpc: '2.0', id: sentB.id, result: { v: 'B' } }));
      ws.emit('message', JSON.stringify({ jsonrpc: '2.0', id: sentA.id, result: { v: 'A' } }));

      await expect(pA).resolves.toEqual({ v: 'A' });
      await expect(pB).resolves.toEqual({ v: 'B' });
    });

    it('should reject request() when WebSocket is no longer OPEN', async () => {
      const connectPromise = bridge.connect();
      const ws = MockWebSocket._lastInstance!;
      ws.emit('open');
      await connectPromise;

      // Simulate the socket transitioning to CLOSED between isConnected check and send.
      // bridge.request() re-checks readyState === OPEN before calling ws.send (SEC-#422).
      ws.readyState = (MockWebSocket as any).CLOSED;

      await expect(bridge.request('tools/list')).rejects.toThrow(/WebSocket not open|Not connected/);
    });
  });
});
