import WebSocket from 'ws';
import { EventEmitter } from 'events';
import {
  BridgeConfig,
  ConnectionState,
  DEFAULT_BRIDGE_CONFIG,
  JsonRpcNotificationSchema,
  JsonRpcRequest,
  JsonRpcResponse,
  JsonRpcResponseSchema,
  McpError,
  McpErrorCode,
} from './types.js';

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
  timeout: NodeJS.Timeout;
}

/**
 * UnityBridge - WebSocket client for communicating with Unity MCP Server
 *
 * Handles:
 * - WebSocket connection management with auto-reconnect
 * - JSON-RPC 2.0 request/response handling
 * - Request timeout management
 * - Connection state tracking
 */
export class UnityBridge extends EventEmitter {
  private ws: WebSocket | null = null;
  private requestId = 0;
  private pendingRequests = new Map<number | string, PendingRequest>();
  private reconnectAttempts = 0;
  private reconnectTimer: NodeJS.Timeout | null = null;
  private _state: ConnectionState = ConnectionState.Disconnected;
  private config: BridgeConfig;
  // SEC-#422: share a single in-flight connect() promise across concurrent callers so we
  // don't spawn multiple WebSockets when several MCP handlers race to connect.
  private connectPromise: Promise<void> | null = null;
  // SEC-#422: reject of the in-flight connect promise so handleDisconnect can unblock it.
  private connectReject: ((err: Error) => void) | null = null;

  constructor(config: Partial<BridgeConfig> = {}) {
    super();
    this.setMaxListeners(20);
    this.config = { ...DEFAULT_BRIDGE_CONFIG, ...config };
  }

  /**
   * Current connection state
   */
  get state(): ConnectionState {
    return this._state;
  }

  /**
   * Whether the bridge is currently connected
   */
  get isConnected(): boolean {
    return this._state === ConnectionState.Connected && this.ws?.readyState === WebSocket.OPEN;
  }

  /**
   * WebSocket URL for Unity connection (includes shared secret as query param if configured)
   */
  private get wsUrl(): string {
    const base = `ws://${this.config.unityHost}:${this.config.unityPort}`;
    if (this.config.unitySecret) {
      return `${base}/?secret=${encodeURIComponent(this.config.unitySecret)}`;
    }
    return base;
  }

  /**
   * Log message if debug is enabled
   */
  private log(message: string, ...args: unknown[]): void {
    if (this.config.debug) {
      console.error(`[MCP Unity Bridge] ${message}`, ...args);
    }
  }

  /**
   * Truncate a payload string for safe debug logging.
   * Avoids leaking large or sensitive message contents into log output.
   */
  private truncateForLog(payload: string, max: number = 500): string {
    if (payload.length <= max) return payload;
    return `${payload.substring(0, max)}... [truncated, total ${payload.length} bytes]`;
  }

  /**
   * Update connection state and emit event
   */
  private setState(state: ConnectionState): void {
    const previousState = this._state;
    this._state = state;
    this.emit('stateChange', state, previousState);
    this.log(`State changed: ${previousState} -> ${state}`);
  }

  /**
   * Connect to Unity WebSocket server.
   *
   * SEC-#422:
   *  - Concurrent callers share the same in-flight promise (no double WebSocket).
   *  - Connection timeout, error during handshake, and unexpected close all transition
   *    state to Failed (or Disconnected on user-requested disconnect) so a future
   *    connect() can succeed instead of being rejected as "already in progress".
   *  - Old socket listeners are removed before swapping in a new socket.
   */
  async connect(): Promise<void> {
    if (this._state === ConnectionState.Connected) return;
    if (this.connectPromise) return this.connectPromise;

    this.connectPromise = this.doConnect().finally(() => {
      this.connectPromise = null;
      this.connectReject = null;
    });
    return this.connectPromise;
  }

  private doConnect(): Promise<void> {
    this.setState(ConnectionState.Connecting);

    return new Promise<void>((resolve, reject) => {
      // Capture the rejector so handleDisconnect() can unblock us if the socket
      // closes during the handshake (was previously a stuck promise).
      this.connectReject = reject;

      // Tear down any leftover socket from a prior failed attempt before opening a new one.
      if (this.ws) {
        try { this.ws.removeAllListeners(); } catch { /* noop */ }
        try { this.ws.close(); } catch { /* noop */ }
        this.ws = null;
      }

      let settled = false;
      const settle = (fn: () => void) => {
        if (settled) return;
        settled = true;
        clearTimeout(connectionTimeout);
        fn();
      };

      try {
        // SEC: cap incoming payload at 10 MB to prevent OOM from a malicious
        // or buggy Unity server. Default in `ws` is 100 MB.
        this.ws = new WebSocket(this.wsUrl, { maxPayload: 10 * 1024 * 1024 });
      } catch (error) {
        this.setState(ConnectionState.Failed);
        reject(
          new McpError(
            McpErrorCode.ConnectionError,
            `Failed to create WebSocket: ${error instanceof Error ? error.message : String(error)}`
          )
        );
        return;
      }

      const connectionTimeout = setTimeout(() => {
        if (this._state === ConnectionState.Connecting) {
          this.setState(ConnectionState.Failed);
          try { this.ws?.removeAllListeners(); } catch { /* noop */ }
          try { this.ws?.close(); } catch { /* noop */ }
          this.ws = null;
          settle(() => reject(
            new McpError(
              McpErrorCode.TimeoutError,
              `Connection timeout after ${this.config.requestTimeout}ms`
            )
          ));
        }
      }, this.config.requestTimeout);

      this.ws.on('open', () => {
        this.reconnectAttempts = 0;
        this.setState(ConnectionState.Connected);
        this.log(`Connected to Unity at ws://${this.config.unityHost}:${this.config.unityPort}`);
        this.emit('connected');
        settle(() => resolve());
      });

      this.ws.on('message', (data: WebSocket.Data) => {
        this.handleMessage(data);
      });

      this.ws.on('error', (error: Error) => {
        this.log(`WebSocket error:`, error);
        this.emit('error', error);

        if (this._state === ConnectionState.Connecting) {
          this.setState(ConnectionState.Failed);
          settle(() => reject(
            new McpError(McpErrorCode.ConnectionError, `Connection failed: ${error.message}`)
          ));
        }
      });

      this.ws.on('close', (code: number, reason: Buffer) => {
        this.log(`WebSocket closed: ${code} - ${reason.toString()}`);
        this.handleDisconnect();
      });
    });
  }

  /**
   * Handle incoming WebSocket message
   */
  private handleMessage(data: WebSocket.Data): void {
    try {
      const message = data.toString();
      this.log(`Received:`, this.truncateForLog(message));

      const json = JSON.parse(message);

      // SEC-#394: validate notification shape before forwarding downstream so a
      // malformed / untrusted Unity response can't feed arbitrary data to handlers.
      if (json.method && json.id === undefined) {
        const notif = JsonRpcNotificationSchema.safeParse(json);
        if (!notif.success) {
          this.log(`Rejected malformed notification:`, notif.error.message);
          return;
        }
        this.log(`Received notification: ${notif.data.method}`);
        this.emit('notification', notif.data);
        return;
      }

      // H-06: Validate message structure at runtime using Zod before accessing any fields
      const parsed = JsonRpcResponseSchema.safeParse(json);
      if (!parsed.success) {
        this.log(`Received malformed JSON-RPC response:`, parsed.error.message);
        this.emit('error', new McpError(McpErrorCode.ParseError, `Malformed response from Unity: ${parsed.error.message}`));
        return;
      }
      const response: JsonRpcResponse = parsed.data;

      if (response.id !== undefined) {
        const pending = this.pendingRequests.get(response.id);
        if (pending) {
          clearTimeout(pending.timeout);
          this.pendingRequests.delete(response.id);

          if (response.error) {
            pending.reject(
              new McpError(
                response.error.code as McpErrorCode,
                response.error.message,
                response.error.data
              )
            );
          } else {
            pending.resolve(response.result);
          }
        } else {
          this.log(`Received response for unknown request ID: ${response.id}`);
        }
      }
    } catch (error) {
      this.log(`Failed to parse message:`, error);
      this.emit(
        'error',
        new McpError(
          McpErrorCode.ParseError,
          `Failed to parse message: ${error instanceof Error ? error.message : String(error)}`
        )
      );
    }
  }

  /**
   * Handle disconnect and attempt reconnection
   */
  private handleDisconnect(): void {
    // Clear all pending requests
    for (const [, pending] of this.pendingRequests) {
      clearTimeout(pending.timeout);
      pending.reject(new McpError(McpErrorCode.ConnectionError, 'Connection closed'));
    }
    this.pendingRequests.clear();

    // SEC-#422: if the socket closes while a connect() is still pending, unblock the
    // caller instead of leaving the promise dangling forever.
    if (this.connectReject) {
      const reject = this.connectReject;
      this.connectReject = null;
      reject(new McpError(McpErrorCode.ConnectionError, 'Connection closed during handshake'));
    }

    const wasConnected = this._state === ConnectionState.Connected;

    if (wasConnected && this.reconnectAttempts < this.config.maxReconnectAttempts) {
      this.setState(ConnectionState.Reconnecting);
      this.scheduleReconnect();
    } else {
      this.setState(ConnectionState.Disconnected);
    }

    this.emit('disconnected');
  }

  /**
   * Schedule a reconnection attempt
   */
  private scheduleReconnect(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
    }

    this.reconnectTimer = setTimeout(async () => {
      this.reconnectAttempts++;
      this.log(
        `Reconnection attempt ${this.reconnectAttempts}/${this.config.maxReconnectAttempts}`
      );

      try {
        await this.connect();
        this.emit('reconnected');
      } catch {
        if (this.reconnectAttempts < this.config.maxReconnectAttempts) {
          this.scheduleReconnect();
        } else {
          this.setState(ConnectionState.Failed);
          this.emit('reconnectFailed');
        }
      }
    }, this.config.reconnectInterval);
  }

  /**
   * Disconnect from Unity
   */
  async disconnect(): Promise<void> {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    // Reject all pending requests before closing the socket
    for (const [, pending] of this.pendingRequests) {
      clearTimeout(pending.timeout);
      pending.reject(new McpError(McpErrorCode.ConnectionError, 'WebSocket disconnected'));
    }
    this.pendingRequests.clear();

    if (this.ws) {
      // SEC-#422: drop listeners before close() so an asynchronous 'close' event
      // can't fire handleDisconnect() and trigger an unwanted reconnect storm.
      try { this.ws.removeAllListeners(); } catch { /* noop */ }
      try { this.ws.close(); } catch { /* noop */ }
      this.ws = null;
    }

    this.setState(ConnectionState.Disconnected);
  }

  /**
   * Send a JSON-RPC request to Unity and wait for response
   */
  async request<T = unknown>(method: string, params?: unknown): Promise<T> {
    if (!this.isConnected) {
      throw new McpError(McpErrorCode.ConnectionError, 'Not connected to Unity');
    }

    // Wrap around well before Number.MAX_SAFE_INTEGER to avoid losing integer precision
    // and routing responses to the wrong pending request.
    this.requestId = (this.requestId + 1) % Number.MAX_SAFE_INTEGER;
    const id = this.requestId;
    const request: JsonRpcRequest = {
      jsonrpc: '2.0',
      id,
      method,
      params,
    };

    return new Promise<T>((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(id);
        reject(
          new McpError(
            McpErrorCode.TimeoutError,
            `Request timeout after ${this.config.requestTimeout}ms`
          )
        );
      }, this.config.requestTimeout);

      this.pendingRequests.set(id, {
        resolve: resolve as (value: unknown) => void,
        reject,
        timeout,
      });

      const message = JSON.stringify(request);
      this.log(`Sending:`, this.truncateForLog(message));

      if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
        clearTimeout(timeout);
        this.pendingRequests.delete(id);
        reject(new McpError(McpErrorCode.ConnectionError, 'WebSocket not open'));
        return;
      }
      this.ws.send(message, (error) => {
        if (error) {
          clearTimeout(timeout);
          this.pendingRequests.delete(id);
          reject(
            new McpError(McpErrorCode.ConnectionError, `Failed to send request: ${error.message}`)
          );
        }
      });
    });
  }

  /**
   * Send a notification (no response expected)
   */
  notify(method: string, params?: unknown): void {
    if (!this.isConnected) {
      throw new McpError(McpErrorCode.ConnectionError, 'Not connected to Unity');
    }

    const notification = {
      jsonrpc: '2.0',
      method,
      params,
    };

    const message = JSON.stringify(notification);
    this.log(`Sending notification:`, message);

    if (!this.ws) {
      throw new McpError(McpErrorCode.ConnectionError, 'WebSocket closed before send');
    }
    this.ws.send(message, (error) => {
      if (error) {
        this.log(`Failed to send notification:`, error);
        this.emit(
          'error',
          new McpError(McpErrorCode.ConnectionError, `Notify failed: ${error.message}`)
        );
      }
    });
  }

  /**
   * Wait for connection to be established
   */
  async waitForConnection(timeout: number = 30000): Promise<void> {
    if (this.isConnected) {
      return;
    }

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.off('connected', onConnect);
        this.off('error', onError);
        reject(new McpError(McpErrorCode.TimeoutError, 'Connection timeout'));
      }, timeout);

      const onConnect = () => {
        clearTimeout(timer);
        this.off('error', onError);
        resolve();
      };

      const onError = (error: Error) => {
        clearTimeout(timer);
        this.off('connected', onConnect);
        reject(error);
      };

      this.once('connected', onConnect);
      this.once('error', onError);
    });
  }
}
