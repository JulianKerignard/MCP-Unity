import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ServerCache, CacheCategory } from '../cache.js';

describe('ServerCache', () => {
  let cache: ServerCache;

  beforeEach(() => {
    vi.useFakeTimers();
    cache = new ServerCache();
  });

  afterEach(() => {
    cache.destroy();
    vi.useRealTimers();
  });

  it('should return null for a non-existent key', () => {
    expect(cache.get('nonexistent')).toBeNull();
  });

  it('should store and retrieve data', () => {
    cache.set('key1', { foo: 'bar' }, 'components');
    expect(cache.get('key1')).toEqual({ foo: 'bar' });
  });

  it('should return null after TTL expiry', () => {
    cache.set('editor', 'data', 'editorState'); // 5s TTL
    expect(cache.get('editor')).toBe('data');

    vi.advanceTimersByTime(5001);
    expect(cache.get('editor')).toBeNull();
  });

  it('should respect different TTL per category', () => {
    cache.set('a', 1, 'editorState'); // 5s
    cache.set('b', 2, 'hierarchy'); // 30s
    cache.set('c', 3, 'components'); // 60s
    cache.set('d', 4, 'assets'); // 300s
    cache.set('e', 5, 'scenes'); // 300s

    // After 5s: editorState expired
    vi.advanceTimersByTime(5001);
    expect(cache.get('a')).toBeNull();
    expect(cache.get('b')).toBe(2);
    expect(cache.get('c')).toBe(3);
    expect(cache.get('d')).toBe(4);
    expect(cache.get('e')).toBe(5);

    // After 30s total: hierarchy expired
    vi.advanceTimersByTime(25000);
    expect(cache.get('b')).toBeNull();
    expect(cache.get('c')).toBe(3);
    expect(cache.get('d')).toBe(4);

    // After 60s total: components expired
    vi.advanceTimersByTime(30000);
    expect(cache.get('c')).toBeNull();
    expect(cache.get('d')).toBe(4);

    // After 300s total: assets and scenes expired
    vi.advanceTimersByTime(240000);
    expect(cache.get('d')).toBeNull();
    expect(cache.get('e')).toBeNull();
  });

  it('should evict entries when exceeding MAX_ENTRIES (500)', () => {
    for (let i = 0; i < 501; i++) {
      cache.set(`key-${i}`, i, 'assets');
    }
    const stats = cache.stats();
    expect(stats.size).toBeLessThanOrEqual(500);
  });

  it('should invalidate keys by category (exact match, no substring)', () => {
    cache.set('unity_list_gameobjects:{}', 'data1', 'hierarchy');
    cache.set('unity_list_gameobjects:{"depth":2}', 'data2', 'hierarchy');
    cache.set('unity_get_editor_state:{}', 'data3', 'editorState');
    // Key whose tool name contains "hierarchy" as substring — must NOT be invalidated
    cache.set('unity_get_scene_hierarchy:{}', 'data4', 'scenes');

    cache.invalidate('hierarchy');

    expect(cache.get('unity_list_gameobjects:{}')).toBeNull();
    expect(cache.get('unity_list_gameobjects:{"depth":2}')).toBeNull();
    expect(cache.get('unity_get_editor_state:{}')).toBe('data3');
    // scenes category entry must survive hierarchy invalidation
    expect(cache.get('unity_get_scene_hierarchy:{}')).toBe('data4');
  });

  it('should clear all entries', () => {
    cache.set('a', 1, 'hierarchy');
    cache.set('b', 2, 'assets');
    cache.set('c', 3, 'components');

    cache.clear();

    expect(cache.stats().size).toBe(0);
    expect(cache.get('a')).toBeNull();
    expect(cache.get('b')).toBeNull();
    expect(cache.get('c')).toBeNull();
  });

  it('should return correct stats', () => {
    cache.set('x', 1, 'hierarchy');
    cache.set('y', 2, 'assets');

    const stats = cache.stats();
    expect(stats.size).toBe(2);
    expect(stats.keys).toContain('x');
    expect(stats.keys).toContain('y');
  });

  it('should have correct TTL values for each category', () => {
    const categories: { category: CacheCategory; ttlMs: number }[] = [
      { category: 'editorState', ttlMs: 5000 },
      { category: 'hierarchy', ttlMs: 30000 },
      { category: 'components', ttlMs: 60000 },
      { category: 'assets', ttlMs: 300000 },
      { category: 'scenes', ttlMs: 300000 },
    ];

    for (const { category, ttlMs } of categories) {
      const testCache = new ServerCache();
      const key = `ttl-test-${category}`;

      testCache.set(key, 'alive', category);

      // Just before expiry: still alive
      vi.advanceTimersByTime(ttlMs - 1);
      expect(testCache.get(key)).toBe('alive');

      // At expiry: gone
      vi.advanceTimersByTime(2);
      expect(testCache.get(key)).toBeNull();

      testCache.destroy();
    }
  });

  it('should run periodic cleanup and remove expired entries', () => {
    cache.set('short', 'data', 'editorState'); // 5s TTL

    // Advance past TTL but before cleanup interval
    vi.advanceTimersByTime(10000);

    // Entry still in map (not yet cleaned up, just expired on access)
    // Trigger cleanup by advancing to 60s
    vi.advanceTimersByTime(50000);

    // After cleanup, stats should reflect removal
    const stats = cache.stats();
    expect(stats.keys).not.toContain('short');
  });
});
