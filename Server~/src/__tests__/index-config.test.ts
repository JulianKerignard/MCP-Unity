import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { BridgeConfigSchema } from '../types.js';

// ============================================================================
// getConfig() logic — we test the same parsing logic used in index.ts
// (index.ts cannot be imported directly because it calls main() on load)
// ============================================================================

/**
 * Replicate the getConfig() logic from index.ts for testability.
 * This mirrors the exact env var parsing used in production.
 */
function getConfig(env: Record<string, string | undefined> = {}) {
  return BridgeConfigSchema.parse({
    unityHost: env.UNITY_HOST || undefined,
    unityPort: env.UNITY_PORT ? parseInt(env.UNITY_PORT, 10) : undefined,
    unitySecret: env.UNITY_SECRET || undefined,
    reconnectInterval: env.RECONNECT_INTERVAL ? parseInt(env.RECONNECT_INTERVAL, 10) : 3000,
    requestTimeout: env.REQUEST_TIMEOUT ? parseInt(env.REQUEST_TIMEOUT, 10) : 10000,
    maxReconnectAttempts: env.MAX_RECONNECT_ATTEMPTS
      ? parseInt(env.MAX_RECONNECT_ATTEMPTS, 10)
      : 3,
    debug: env.DEBUG === 'true' || env.DEBUG === '1',
  });
}

describe('getConfig() env var parsing', () => {
  it('should use runtime defaults when no env vars set', () => {
    const config = getConfig({});
    expect(config.unityHost).toBe('localhost');
    expect(config.unityPort).toBe(8090);
    expect(config.reconnectInterval).toBe(3000);
    expect(config.requestTimeout).toBe(10000);
    expect(config.maxReconnectAttempts).toBe(3);
    expect(config.debug).toBe(false);
    expect(config.unitySecret).toBe('');
  });

  it('should parse all env vars when set', () => {
    const config = getConfig({
      UNITY_HOST: '192.168.1.50',
      UNITY_PORT: '9090',
      UNITY_SECRET: 'my-secret',
      RECONNECT_INTERVAL: '5000',
      REQUEST_TIMEOUT: '30000',
      MAX_RECONNECT_ATTEMPTS: '10',
      DEBUG: 'true',
    });
    expect(config.unityHost).toBe('192.168.1.50');
    expect(config.unityPort).toBe(9090);
    expect(config.unitySecret).toBe('my-secret');
    expect(config.reconnectInterval).toBe(5000);
    expect(config.requestTimeout).toBe(30000);
    expect(config.maxReconnectAttempts).toBe(10);
    expect(config.debug).toBe(true);
  });

  it('should parse partial env vars (mix of defaults and overrides)', () => {
    const config = getConfig({
      UNITY_PORT: '7777',
      DEBUG: '1',
    });
    expect(config.unityHost).toBe('localhost'); // default
    expect(config.unityPort).toBe(7777); // override
    expect(config.reconnectInterval).toBe(3000); // default
    expect(config.debug).toBe(true); // override via "1"
  });

  it('should handle DEBUG=1 as true', () => {
    const config = getConfig({ DEBUG: '1' });
    expect(config.debug).toBe(true);
  });

  it('should handle DEBUG=false as false', () => {
    const config = getConfig({ DEBUG: 'false' });
    expect(config.debug).toBe(false);
  });

  it('should handle DEBUG=0 as false', () => {
    const config = getConfig({ DEBUG: '0' });
    expect(config.debug).toBe(false);
  });

  it('should handle empty UNITY_SECRET as disabled auth', () => {
    const config = getConfig({ UNITY_SECRET: '' });
    expect(config.unitySecret).toBe('');
  });

  it('should reject invalid port via schema', () => {
    expect(() => getConfig({ UNITY_PORT: '80' })).toThrow();
  });

  it('should reject non-numeric port', () => {
    expect(() => getConfig({ UNITY_PORT: 'abc' })).toThrow();
  });

  it('should reject negative reconnect attempts', () => {
    expect(() => getConfig({ MAX_RECONNECT_ATTEMPTS: '-1' })).toThrow();
  });

  it('should accept zero reconnect attempts (disable reconnect)', () => {
    const config = getConfig({ MAX_RECONNECT_ATTEMPTS: '0' });
    expect(config.maxReconnectAttempts).toBe(0);
  });
});
