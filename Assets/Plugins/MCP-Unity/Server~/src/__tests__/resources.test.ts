import { describe, it, expect } from 'vitest';
import { defaultResources, workflowDocs } from '../resources.js';
import { ResourceDefinitionSchema } from '../types.js';

// ============================================================================
// Resource definition tests
// ============================================================================

describe('defaultResources', () => {
  it('should export a non-empty array', () => {
    expect(defaultResources).toBeInstanceOf(Array);
    expect(defaultResources.length).toBeGreaterThan(0);
  });

  it('every resource should conform to ResourceDefinitionSchema', () => {
    for (const resource of defaultResources) {
      const result = ResourceDefinitionSchema.safeParse(resource);
      expect(result.success, `Resource '${resource.uri}' failed schema validation`).toBe(true);
    }
  });

  it('should have no duplicate URIs', () => {
    const uris = defaultResources.map(r => r.uri);
    const unique = new Set(uris);
    const duplicates = uris.filter((u, i) => uris.indexOf(u) !== i);
    expect(duplicates, `Duplicate URIs: ${duplicates.join(', ')}`).toHaveLength(0);
    expect(unique.size).toBe(uris.length);
  });

  it('every resource should have a non-empty name', () => {
    for (const resource of defaultResources) {
      expect(resource.name, `Resource '${resource.uri}' has no name`).toBeTruthy();
    }
  });

  it('every resource should have a mimeType', () => {
    for (const resource of defaultResources) {
      expect(resource.mimeType, `Resource '${resource.uri}' has no mimeType`).toBeTruthy();
    }
  });

  it('unity:// resources should have application/json mimeType', () => {
    const unityResources = defaultResources.filter(r => r.uri.startsWith('unity://'));
    for (const resource of unityResources) {
      expect(resource.mimeType, `Resource '${resource.uri}' should be application/json`).toBe('application/json');
    }
  });

  it('workflows:// resources should have text/markdown mimeType', () => {
    const workflowResources = defaultResources.filter(r => r.uri.startsWith('workflows://'));
    for (const resource of workflowResources) {
      expect(resource.mimeType, `Resource '${resource.uri}' should be text/markdown`).toBe('text/markdown');
    }
  });
});

describe('defaultResources — required Unity resources', () => {
  const uris = new Set(defaultResources.map(r => r.uri));

  const requiredResources = [
    'unity://project/settings',
    'unity://scene/hierarchy',
    'unity://console/logs',
    'workflows://core',
    'workflows://animator',
    'workflows://materials',
    'workflows://prefabs',
    'workflows://assets',
    'workflows://terrain',
  ];

  for (const uri of requiredResources) {
    it(`should include '${uri}'`, () => {
      expect(uris.has(uri), `Missing resource: ${uri}`).toBe(true);
    });
  }
});

// ============================================================================
// Workflow documentation tests
// ============================================================================

describe('workflowDocs', () => {
  it('should export a non-empty object', () => {
    expect(typeof workflowDocs).toBe('object');
    expect(Object.keys(workflowDocs).length).toBeGreaterThan(0);
  });

  it('every workflows:// resource should have content in workflowDocs', () => {
    const workflowResources = defaultResources.filter(r => r.uri.startsWith('workflows://'));
    for (const resource of workflowResources) {
      expect(workflowDocs[resource.uri], `Missing workflowDocs content for '${resource.uri}'`).toBeTruthy();
    }
  });

  it('every workflowDocs key should be in defaultResources', () => {
    const uris = new Set(defaultResources.map(r => r.uri));
    for (const key of Object.keys(workflowDocs)) {
      expect(uris.has(key), `workflowDocs key '${key}' has no matching resource`).toBe(true);
    }
  });

  it('every workflowDoc should contain a markdown heading', () => {
    for (const [uri, content] of Object.entries(workflowDocs)) {
      expect(content, `Doc '${uri}' does not start with a markdown heading`).toMatch(/^#\s/m);
    }
  });

  it('workflows://core should mention outputMode=tree', () => {
    expect(workflowDocs['workflows://core']).toContain("outputMode='tree'");
  });

  it('workflows://core should mention returnBase64=false', () => {
    expect(workflowDocs['workflows://core']).toContain('returnBase64=false');
  });

  it('workflows://animator should mention unity_add_animator_parameter', () => {
    expect(workflowDocs['workflows://animator']).toContain('unity_add_animator_parameter');
  });

  it('workflows://materials should mention URP', () => {
    expect(workflowDocs['workflows://materials']).toContain('URP');
  });

  it('workflows://prefabs should mention unity_create_prefab', () => {
    expect(workflowDocs['workflows://prefabs']).toContain('unity_create_prefab');
  });

  it('workflows://terrain should mention gaussian', () => {
    expect(workflowDocs['workflows://terrain']).toContain('gaussian');
  });

  it('workflows://assets should mention unity_search_assets', () => {
    expect(workflowDocs['workflows://assets']).toContain('unity_search_assets');
  });

  it('no workflowDoc should be shorter than 100 characters', () => {
    for (const [uri, content] of Object.entries(workflowDocs)) {
      expect(content.length, `Doc '${uri}' is suspiciously short`).toBeGreaterThan(100);
    }
  });
});
