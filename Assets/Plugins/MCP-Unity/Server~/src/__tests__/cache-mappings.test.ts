import { describe, it, expect } from 'vitest';
import { cacheableTools, cacheInvalidators, CacheCategory } from '../cache.js';
import { defaultTools } from '../tools.js';

// ============================================================================
// cacheableTools mapping validation
// ============================================================================

describe('cacheableTools', () => {
  const validCategories: CacheCategory[] = [
    'hierarchy',
    'editorState',
    'components',
    'assets',
    'scenes',
  ];

  it('should only map to valid CacheCategory values', () => {
    for (const [tool, category] of Object.entries(cacheableTools)) {
      expect(validCategories, `Tool '${tool}' maps to invalid category '${category}'`).toContain(
        category
      );
    }
  });

  it('should have no empty/undefined tool names', () => {
    for (const toolName of Object.keys(cacheableTools)) {
      expect(toolName.length).toBeGreaterThan(0);
    }
  });

  it('should have all tool names prefixed with unity_', () => {
    for (const toolName of Object.keys(cacheableTools)) {
      expect(toolName, `Cacheable tool '${toolName}' missing unity_ prefix`).toMatch(
        /^unity_/
      );
    }
  });

  it('should only cache read-only tools (no write tools in cache)', () => {
    const writeToolNames = Object.keys(cacheInvalidators);
    for (const toolName of Object.keys(cacheableTools)) {
      expect(
        writeToolNames,
        `Tool '${toolName}' is both cacheable AND an invalidator — it should be one or the other`
      ).not.toContain(toolName);
    }
  });
});

// ============================================================================
// cacheInvalidators mapping validation
// ============================================================================

describe('cacheInvalidators', () => {
  const validCategories: CacheCategory[] = [
    'hierarchy',
    'editorState',
    'components',
    'assets',
    'scenes',
  ];

  it('should only invalidate valid CacheCategory values', () => {
    for (const [tool, categories] of Object.entries(cacheInvalidators)) {
      expect(Array.isArray(categories), `Tool '${tool}' invalidators should be an array`).toBe(
        true
      );
      for (const cat of categories) {
        expect(
          validCategories,
          `Tool '${tool}' invalidates unknown category '${cat}'`
        ).toContain(cat);
      }
    }
  });

  it('should have non-empty category arrays for every invalidator', () => {
    for (const [tool, categories] of Object.entries(cacheInvalidators)) {
      expect(
        categories.length,
        `Tool '${tool}' has empty invalidator array`
      ).toBeGreaterThan(0);
    }
  });

  it('should have all tool names prefixed with unity_', () => {
    for (const toolName of Object.keys(cacheInvalidators)) {
      expect(toolName, `Invalidator tool '${toolName}' missing unity_ prefix`).toMatch(
        /^unity_/
      );
    }
  });

  it('should have no duplicate categories per tool', () => {
    for (const [tool, categories] of Object.entries(cacheInvalidators)) {
      const unique = new Set(categories);
      expect(
        unique.size,
        `Tool '${tool}' has duplicate categories in invalidator`
      ).toBe(categories.length);
    }
  });

  // Key write tools that MUST be present
  const criticalWriteTools = [
    'unity_create_gameobject',
    'unity_delete_gameobject',
    'unity_add_component',
    'unity_remove_component',
    'unity_modify_component_batch',
    'unity_load_scene',
    'unity_save_scene',
    'unity_create_script',
    'unity_write_script',
    'unity_delete_asset',
    'unity_move_asset',
    'unity_undo',
    'unity_execute_menu_item',
    'unity_refresh_and_compile',
    'unity_create_terrain',
    'unity_bake_lighting',
    'unity_bake_navmesh',
    'unity_set_selection',
    'unity_clear_console',
  ];

  it.each(criticalWriteTools)(
    'should have invalidator for critical write tool: %s',
    (tool) => {
      expect(
        cacheInvalidators,
        `Critical write tool '${tool}' missing from cacheInvalidators`
      ).toHaveProperty(tool);
    }
  );
});

// ============================================================================
// Cross-validation: defaultTools vs cache mappings
// ============================================================================

describe('cache <-> tools cross-validation', () => {
  const allDefaultToolNames = new Set(defaultTools.map((t) => t.name));

  it('core cacheable tools should exist in defaultTools', () => {
    // Only core tools are in the static defaultTools array.
    // On-demand category tools (asset, material, terrain, etc.) are registered
    // dynamically by Unity at runtime — they won't be in defaultTools.
    const coreCacheableTools = [
      'unity_get_project_overview',
      'unity_list_scenes_in_project',
      'unity_get_editor_state',
      'unity_list_gameobjects',
      'unity_get_component',
      'unity_memory_get',
    ];

    for (const toolName of coreCacheableTools) {
      expect(
        allDefaultToolNames.has(toolName),
        `Core cacheable tool '${toolName}' should be in defaultTools`
      ).toBe(true);
    }
  });

  it('no cacheable tool should also be an invalidator', () => {
    for (const toolName of Object.keys(cacheableTools)) {
      expect(
        cacheInvalidators,
        `Tool '${toolName}' is both cacheable AND an invalidator`
      ).not.toHaveProperty(toolName);
    }
  });
});
