import { describe, it, expect } from 'vitest';
import {
  ToolCallSchema,
  ToolResultContentSchema,
  ToolResultSchema,
  ToolInputSchemaSchema,
  ResourceDefinitionSchema,
  ResourceReadSchema,
  ResourceContentSchema,
  ResourceReadResultSchema,
  PromptArgumentSchema,
  PromptDefinitionSchema,
  PromptMessageSchema,
  PromptGetResultSchema,
  UnitySceneObjectSchema,
  UnitySceneInfoSchema,
  UnityProjectInfoSchema,
  BridgeConfigSchema,
} from '../types.js';

// ============================================================================
// ToolCallSchema
// ============================================================================

describe('ToolCallSchema', () => {
  it('should parse minimal tool call (name only)', () => {
    const result = ToolCallSchema.parse({ name: 'unity_get_editor_state' });
    expect(result.name).toBe('unity_get_editor_state');
    expect(result.arguments).toBeUndefined();
  });

  it('should parse tool call with arguments', () => {
    const result = ToolCallSchema.parse({
      name: 'unity_create_gameobject',
      arguments: { name: 'Player', primitiveType: 'Cube' },
    });
    expect(result.arguments).toEqual({ name: 'Player', primitiveType: 'Cube' });
  });

  it('should reject missing name', () => {
    expect(() => ToolCallSchema.parse({ arguments: {} })).toThrow();
  });

  it('should reject non-string name', () => {
    expect(() => ToolCallSchema.parse({ name: 123 })).toThrow();
  });
});

// ============================================================================
// ToolResultContentSchema
// ============================================================================

describe('ToolResultContentSchema', () => {
  it('should parse text content', () => {
    const result = ToolResultContentSchema.parse({ type: 'text', text: 'Hello' });
    expect(result.type).toBe('text');
    expect(result.text).toBe('Hello');
  });

  it('should parse image content with data and mimeType', () => {
    const result = ToolResultContentSchema.parse({
      type: 'image',
      data: 'base64data==',
      mimeType: 'image/png',
    });
    expect(result.type).toBe('image');
    expect(result.data).toBe('base64data==');
  });

  it('should parse resource content', () => {
    const result = ToolResultContentSchema.parse({
      type: 'resource',
      resource: { uri: 'unity://scene', text: '{}' },
    });
    expect(result.type).toBe('resource');
  });

  it('should reject invalid type', () => {
    expect(() => ToolResultContentSchema.parse({ type: 'video', text: 'bad' })).toThrow();
  });
});

// ============================================================================
// ToolResultSchema
// ============================================================================

describe('ToolResultSchema', () => {
  it('should parse success result', () => {
    const result = ToolResultSchema.parse({
      content: [{ type: 'text', text: '{"success":true}' }],
    });
    expect(result.content).toHaveLength(1);
    expect(result.isError).toBeUndefined();
  });

  it('should parse error result', () => {
    const result = ToolResultSchema.parse({
      content: [{ type: 'text', text: 'Error: not found' }],
      isError: true,
    });
    expect(result.isError).toBe(true);
  });

  it('should parse multi-content result', () => {
    const result = ToolResultSchema.parse({
      content: [
        { type: 'text', text: 'Screenshot saved' },
        { type: 'image', data: 'base64==', mimeType: 'image/jpg' },
      ],
    });
    expect(result.content).toHaveLength(2);
  });

  it('should accept empty content array (no minItems constraint)', () => {
    // SEC-#439: the test previously claimed to "reject" empty arrays but asserted success.
    // Zod z.array() has no minItems constraint here, so empty is valid; name updated to
    // match what the test actually verifies.
    const result = ToolResultSchema.safeParse({ content: [] });
    expect(result.success).toBe(true);
  });

  it('should reject missing content', () => {
    expect(() => ToolResultSchema.parse({})).toThrow();
  });
});

// ============================================================================
// ToolInputSchemaSchema
// ============================================================================

describe('ToolInputSchemaSchema', () => {
  it('should parse minimal schema', () => {
    const result = ToolInputSchemaSchema.parse({ type: 'object' });
    expect(result.type).toBe('object');
  });

  it('should parse with properties and required', () => {
    const result = ToolInputSchemaSchema.parse({
      type: 'object',
      properties: { name: { type: 'string' } },
      required: ['name'],
    });
    expect(result.required).toEqual(['name']);
  });

  it('should reject non-object type', () => {
    expect(() => ToolInputSchemaSchema.parse({ type: 'array' })).toThrow();
  });
});

// ============================================================================
// Resource Schemas
// ============================================================================

describe('ResourceDefinitionSchema', () => {
  it('should parse minimal resource', () => {
    const result = ResourceDefinitionSchema.parse({ uri: 'unity://scene', name: 'Scene' });
    expect(result.uri).toBe('unity://scene');
  });

  it('should parse full resource', () => {
    const result = ResourceDefinitionSchema.parse({
      uri: 'unity://project',
      name: 'Project Info',
      description: 'Current project info',
      mimeType: 'application/json',
    });
    expect(result.mimeType).toBe('application/json');
  });

  it('should reject missing uri', () => {
    expect(() => ResourceDefinitionSchema.parse({ name: 'test' })).toThrow();
  });

  it('should reject missing name', () => {
    expect(() => ResourceDefinitionSchema.parse({ uri: 'test://' })).toThrow();
  });
});

describe('ResourceReadSchema', () => {
  it('should parse valid URI', () => {
    const result = ResourceReadSchema.parse({ uri: 'unity://scene' });
    expect(result.uri).toBe('unity://scene');
  });

  it('should reject missing URI', () => {
    expect(() => ResourceReadSchema.parse({})).toThrow();
  });
});

describe('ResourceContentSchema', () => {
  it('should parse text content', () => {
    const result = ResourceContentSchema.parse({ uri: 'unity://scene', text: '{}' });
    expect(result.text).toBe('{}');
  });

  it('should parse blob content', () => {
    const result = ResourceContentSchema.parse({
      uri: 'unity://screenshot',
      blob: 'base64data',
      mimeType: 'image/png',
    });
    expect(result.blob).toBe('base64data');
  });
});

describe('ResourceReadResultSchema', () => {
  it('should parse with contents array', () => {
    const result = ResourceReadResultSchema.parse({
      contents: [{ uri: 'unity://scene', text: '{"name":"MainScene"}' }],
    });
    expect(result.contents).toHaveLength(1);
  });

  it('should reject missing contents', () => {
    expect(() => ResourceReadResultSchema.parse({})).toThrow();
  });
});

// ============================================================================
// Prompt Schemas
// ============================================================================

describe('PromptArgumentSchema', () => {
  it('should parse minimal argument', () => {
    const result = PromptArgumentSchema.parse({ name: 'task' });
    expect(result.name).toBe('task');
  });

  it('should parse full argument', () => {
    const result = PromptArgumentSchema.parse({
      name: 'task',
      description: 'What to do',
      required: true,
    });
    expect(result.required).toBe(true);
  });
});

describe('PromptDefinitionSchema', () => {
  it('should parse minimal prompt', () => {
    const result = PromptDefinitionSchema.parse({ name: 'scene-setup' });
    expect(result.name).toBe('scene-setup');
  });

  it('should parse prompt with arguments', () => {
    const result = PromptDefinitionSchema.parse({
      name: 'create-character',
      description: 'Set up a game character',
      arguments: [
        { name: 'characterName', description: 'Name', required: true },
        { name: 'meshType', description: 'Mesh type' },
      ],
    });
    expect(result.arguments).toHaveLength(2);
  });
});

describe('PromptMessageSchema', () => {
  it('should parse user message', () => {
    const result = PromptMessageSchema.parse({
      role: 'user',
      content: { type: 'text', text: 'Create a cube' },
    });
    expect(result.role).toBe('user');
  });

  it('should parse assistant message', () => {
    const result = PromptMessageSchema.parse({
      role: 'assistant',
      content: { type: 'text', text: 'Done' },
    });
    expect(result.role).toBe('assistant');
  });

  it('should reject invalid role', () => {
    expect(() =>
      PromptMessageSchema.parse({ role: 'system', content: { type: 'text', text: 'x' } })
    ).toThrow();
  });
});

describe('PromptGetResultSchema', () => {
  it('should parse result with messages', () => {
    const result = PromptGetResultSchema.parse({
      messages: [{ role: 'user', content: { type: 'text', text: 'Hello' } }],
    });
    expect(result.messages).toHaveLength(1);
  });

  it('should parse result with description', () => {
    const result = PromptGetResultSchema.parse({
      description: 'A helpful prompt',
      messages: [],
    });
    expect(result.description).toBe('A helpful prompt');
  });
});

// ============================================================================
// Unity-specific Schemas
// ============================================================================

describe('UnitySceneObjectSchema', () => {
  it('should parse minimal scene object', () => {
    const result = UnitySceneObjectSchema.parse({
      instanceId: 12345,
      name: 'Player',
      type: 'GameObject',
      active: true,
      layer: 0,
      tag: 'Player',
    });
    expect(result.name).toBe('Player');
  });

  it('should parse full scene object with transform', () => {
    const result = UnitySceneObjectSchema.parse({
      instanceId: 100,
      name: 'Cube',
      type: 'GameObject',
      active: true,
      layer: 0,
      tag: 'Untagged',
      position: { x: 1, y: 2, z: 3 },
      rotation: { x: 0, y: 0, z: 0, w: 1 },
      scale: { x: 1, y: 1, z: 1 },
      components: ['Transform', 'MeshRenderer'],
      children: [101, 102],
    });
    expect(result.position?.x).toBe(1);
    expect(result.rotation?.w).toBe(1);
    expect(result.components).toHaveLength(2);
  });
});

describe('UnitySceneInfoSchema', () => {
  it('should parse valid scene info', () => {
    const result = UnitySceneInfoSchema.parse({
      name: 'MainScene',
      path: 'Assets/Scenes/Main.unity',
      buildIndex: 0,
      isDirty: false,
      isLoaded: true,
      rootCount: 5,
    });
    expect(result.name).toBe('MainScene');
    expect(result.rootCount).toBe(5);
  });
});

describe('UnityProjectInfoSchema', () => {
  it('should parse valid project info', () => {
    const result = UnityProjectInfoSchema.parse({
      productName: 'MyGame',
      companyName: 'Studio',
      version: '1.0.0',
      unityVersion: '6000.0.45f1',
      platform: 'StandaloneWindows64',
      dataPath: '/path/to/Assets',
      persistentDataPath: '/path/to/persistent',
    });
    expect(result.unityVersion).toBe('6000.0.45f1');
  });
});

// ============================================================================
// BridgeConfigSchema — unitySecret handling
// ============================================================================

describe('BridgeConfigSchema — secret handling', () => {
  it('should default unitySecret to empty string', () => {
    const config = BridgeConfigSchema.parse({});
    expect(config.unitySecret).toBe('');
  });

  it('should accept custom secret', () => {
    const config = BridgeConfigSchema.parse({ unitySecret: 'my-secret-key' });
    expect(config.unitySecret).toBe('my-secret-key');
  });

  it('should accept empty secret (disables auth)', () => {
    const config = BridgeConfigSchema.parse({ unitySecret: '' });
    expect(config.unitySecret).toBe('');
  });
});
