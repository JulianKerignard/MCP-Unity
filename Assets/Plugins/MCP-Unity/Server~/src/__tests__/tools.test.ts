import { describe, it, expect } from 'vitest';
import { defaultTools } from '../tools.js';
import { ToolDefinitionSchema } from '../types.js';

// ============================================================================
// Tool definition tests
// ============================================================================

describe('defaultTools', () => {
  it('should export a non-empty array', () => {
    expect(defaultTools).toBeInstanceOf(Array);
    expect(defaultTools.length).toBeGreaterThan(0);
  });

  it('every tool should conform to ToolDefinitionSchema', () => {
    for (const tool of defaultTools) {
      const result = ToolDefinitionSchema.safeParse(tool);
      expect(result.success, `Tool '${tool.name}' failed schema validation: ${JSON.stringify(result)}`).toBe(true);
    }
  });

  it('every tool name should start with unity_', () => {
    for (const tool of defaultTools) {
      expect(tool.name, `Tool '${tool.name}' does not start with unity_`).toMatch(/^unity_/);
    }
  });

  it('every tool name should be snake_case', () => {
    for (const tool of defaultTools) {
      expect(tool.name, `Tool '${tool.name}' is not snake_case`).toMatch(/^[a-z][a-z0-9_]*$/);
    }
  });

  it('should have no duplicate tool names', () => {
    const names = defaultTools.map(t => t.name);
    const unique = new Set(names);
    const duplicates = names.filter((n, i) => names.indexOf(n) !== i);
    expect(duplicates, `Duplicate tool names: ${duplicates.join(', ')}`).toHaveLength(0);
    expect(unique.size).toBe(names.length);
  });

  it('every tool should have a non-empty description', () => {
    for (const tool of defaultTools) {
      expect(tool.description, `Tool '${tool.name}' has no description`).toBeTruthy();
      expect(tool.description!.length, `Tool '${tool.name}' has empty description`).toBeGreaterThan(0);
    }
  });

  it('every tool inputSchema should have type object', () => {
    for (const tool of defaultTools) {
      expect(tool.inputSchema.type, `Tool '${tool.name}' inputSchema.type !== 'object'`).toBe('object');
    }
  });

  it('every required field should exist in properties', () => {
    for (const tool of defaultTools) {
      const { required, properties } = tool.inputSchema;
      if (!required || required.length === 0) continue;
      for (const req of required) {
        expect(properties, `Tool '${tool.name}': required field '${req}' not in properties`).toHaveProperty(req);
      }
    }
  });
});

// ============================================================================
// Specific tool presence tests
// ============================================================================

describe('defaultTools — required core tools', () => {
  const toolNames = new Set(defaultTools.map(t => t.name));

  const requiredCoreTools = [
    'unity_get_editor_state',
    'unity_list_gameobjects',
    'unity_get_selection',
    'unity_set_selection',
    'unity_get_console_logs',
    'unity_clear_console',
    'unity_take_screenshot',
    'unity_execute_menu_item',
    'unity_undo',
    'unity_refresh_and_compile',
    'unity_run_tests',
    'unity_list_tool_categories',
    'unity_enable_tool_category',
  ];

  for (const name of requiredCoreTools) {
    it(`should include '${name}'`, () => {
      expect(toolNames.has(name), `Missing core tool: ${name}`).toBe(true);
    });
  }
});

describe('defaultTools — required field validation', () => {
  it('unity_execute_menu_item should require menuPath', () => {
    const tool = defaultTools.find(t => t.name === 'unity_execute_menu_item');
    expect(tool).toBeDefined();
    expect(tool!.inputSchema.required).toContain('menuPath');
  });

  it('unity_enable_tool_category should require category', () => {
    const tool = defaultTools.find(t => t.name === 'unity_enable_tool_category');
    expect(tool).toBeDefined();
    expect(tool!.inputSchema.required).toContain('category');
  });

  it('unity_list_gameobjects should not require any field', () => {
    const tool = defaultTools.find(t => t.name === 'unity_list_gameobjects');
    expect(tool).toBeDefined();
    const required = tool!.inputSchema.required ?? [];
    expect(required).toHaveLength(0);
  });

  it('unity_get_editor_state should not require any field', () => {
    const tool = defaultTools.find(t => t.name === 'unity_get_editor_state');
    expect(tool).toBeDefined();
    const required = tool!.inputSchema.required ?? [];
    expect(required).toHaveLength(0);
  });
});

describe('defaultTools — property type correctness', () => {
  it('unity_get_console_logs.logType should have enum values', () => {
    const tool = defaultTools.find(t => t.name === 'unity_get_console_logs');
    expect(tool).toBeDefined();
    const props = tool!.inputSchema.properties as Record<string, { type: string; enum?: string[] }>;
    expect(props['logType']).toBeDefined();
    expect(props['logType']?.enum).toContain('All');
    expect(props['logType']?.enum).toContain('Error');
    expect(props['logType']?.enum).toContain('Warning');
  });

  it('unity_list_gameobjects.outputMode should have tree in enum', () => {
    const tool = defaultTools.find(t => t.name === 'unity_list_gameobjects');
    expect(tool).toBeDefined();
    const props = tool!.inputSchema.properties as Record<string, { type: string; enum?: string[] }>;
    expect(props['outputMode']?.enum).toContain('tree');
  });

  it('unity_take_screenshot.returnBase64 should be boolean type', () => {
    const tool = defaultTools.find(t => t.name === 'unity_take_screenshot');
    expect(tool).toBeDefined();
    const props = tool!.inputSchema.properties as Record<string, { type: string }>;
    expect(props['returnBase64']?.type).toBe('boolean');
  });

  it('unity_run_tests.testMode should have EditMode and PlayMode', () => {
    const tool = defaultTools.find(t => t.name === 'unity_run_tests');
    expect(tool).toBeDefined();
    const props = tool!.inputSchema.properties as Record<string, { type: string; enum?: string[] }>;
    expect(props['testMode']?.enum).toContain('EditMode');
    expect(props['testMode']?.enum).toContain('PlayMode');
  });
});
