import { ResourceDefinition } from './types.js';

// Default resources - OPTIMIZED with workflow documentation (Phase 5)
export const defaultResources: ResourceDefinition[] = [
  {
    uri: 'unity://project/settings',
    name: 'Project Settings',
    description: 'Unity project settings',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://scene/hierarchy',
    name: 'Scene Hierarchy',
    description: 'Current scene hierarchy',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://console/logs',
    name: 'Console Logs',
    description: 'Recent console logs',
    mimeType: 'application/json',
  },
  // === WORKFLOW DOCUMENTATION RESOURCES (Phase 5) ===
  {
    uri: 'workflows://core',
    name: 'Core Workflows',
    description: 'Essential Unity MCP workflows and best practices',
    mimeType: 'text/markdown',
  },
  {
    uri: 'workflows://animator',
    name: 'Animator Guide',
    description: 'Complete Animator Controller workflow guide',
    mimeType: 'text/markdown',
  },
  {
    uri: 'workflows://materials',
    name: 'Materials Guide',
    description: 'Materials and shaders workflow guide',
    mimeType: 'text/markdown',
  },
  {
    uri: 'workflows://prefabs',
    name: 'Prefabs Guide',
    description: 'Prefab workflow guide',
    mimeType: 'text/markdown',
  },
  {
    uri: 'workflows://assets',
    name: 'Assets Guide',
    description: 'Asset browser and search workflow guide',
    mimeType: 'text/markdown',
  },
  {
    uri: 'workflows://terrain',
    name: 'Terrain Guide',
    description: 'Complete terrain sculpting, painting and brush workflow guide',
    mimeType: 'text/markdown',
  },
];

// Workflow documentation content (served from memory, no Unity connection needed)
export const workflowDocs: Record<string, string> = {
  'workflows://core': `# Unity MCP Core Workflows

## Basic Workflow
1. \`unity_get_editor_state\` - Check play mode
2. \`unity_list_gameobjects\` with outputMode='tree' - See hierarchy
3. \`unity_get_component\` - Inspect properties
4. \`unity_modify_component_batch\` - Make changes (supports multiple GameObjects in one call)
5. \`unity_save_scene\` - Persist changes

## Token Optimization
- ALWAYS use outputMode='tree' for lists (90% smaller)
- Use returnBase64=false for screenshots
- Use size='small' for asset previews
- Limit maxResults to 20 for searches

## Common Components
Transform, Rigidbody, BoxCollider, SphereCollider, MeshRenderer, AudioSource, Animator`,

  'workflows://animator': `# Animator Controller Workflow

## Reading Controllers
\`\`\`
unity_get_animator_controller({ controllerPath: "Assets/Animations/Player.controller" })
// OR from GameObject:
unity_get_animator_controller({ gameObjectPath: "Player" })
\`\`\`

## Runtime Control
\`\`\`
unity_set_animator_parameter({ gameObjectPath: "Player", parameterName: "Speed", value: 5.0 })
unity_set_animator_parameter({ gameObjectPath: "Player", parameterName: "Jump", parameterType: "Trigger" })
\`\`\`

## Building State Machines
1. Add parameters: unity_add_animator_parameter
2. Add states: unity_add_animator_state
3. Add transitions: unity_add_animator_transition with conditions
4. Create blend trees: unity_create_blend_tree + unity_add_blend_motion

## Transition Conditions
Modes: Greater, Less, Equals, NotEqual, If (bool true), IfNot (bool false)`,

  'workflows://materials': `# Materials Workflow

## Reading Materials
\`\`\`
unity_get_material({ materialPath: "Assets/Materials/Player.mat" })
// OR from GameObject:
unity_get_material({ gameObjectPath: "Cube" })
\`\`\`

## Modifying Materials
\`\`\`
unity_set_material({
  materialPath: "Assets/Materials/Player.mat",
  properties: { "_Color": {r:1,g:0,b:0,a:1}, "_Metallic": 0.9 }
})
\`\`\`

## Common Properties
- Standard: _Color, _MainTex, _Metallic, _Glossiness, _BumpMap, _EmissionColor
- URP/Lit: _BaseColor, _BaseMap, _Metallic, _Smoothness
- All properties start with underscore (_)

## Render Pipeline Auto-Detection
System auto-detects URP/HDRP/Built-in. "Standard" maps to "Universal Render Pipeline/Lit" in URP.`,

  'workflows://prefabs': `# Prefab Workflow

## Creating Prefabs
\`\`\`
unity_create_prefab({
  gameObjectPath: "Player",
  savePath: "Assets/Prefabs/Player.prefab"
})
\`\`\`

## Instantiating Prefabs
\`\`\`
unity_instantiate_prefab({
  prefabPath: "Assets/Prefabs/Enemy.prefab",
  position: {x: 5, y: 0, z: 0}
})
\`\`\`

## Modifying Prefabs
1. Modify instance in scene
2. Apply changes: unity_apply_prefab_overrides({ gameObjectPath: "Enemy(Clone)" })

## Breaking Prefab Link
\`\`\`
unity_unpack_prefab({ gameObjectPath: "Enemy(Clone)", unpackMode: "completely" })
\`\`\``,

  'workflows://terrain': `# Terrain Workflow Guide

## Full Terrain Setup (start to finish)
\`\`\`
1. unity_create_terrain({ name: "Terrain", width: 1000, length: 1000, height: 600 })
2. unity_set_terrain_heights_batch({ operation: "flatten", value: 0.05 })   // base level
3. unity_set_terrain_heights_batch({ operation: "raise", brushShape: "gaussian", ... })  // sculpt
4. unity_add_terrain_layer({ diffuseTexturePath: "Assets/Textures/Grass.png", tileSize: {x:30,z:30} })
5. unity_paint_terrain_texture_batch({ layerIndex: 0, opacity: 1.0 })       // base texture
6. unity_paint_terrain_path({ layerIndex: 1, waypoints: [...], width: 5 })  // paint roads/rivers
7. unity_add_terrain_trees / unity_add_terrain_detail                       // vegetation
\`\`\`

## Sculpting Operations (unity_set_terrain_heights_batch)
| operation | use when |
|-----------|----------|
| flatten   | Level a zone to a specific height. value = target height 0-1 |
| raise     | Push terrain up (hills, ridges). value = raise amount |
| lower     | Push terrain down (valleys, rivers). value = lower amount |
| set       | Force exact height everywhere in region. value = exact height 0-1 |
| noise     | Add organic randomness. intensity = frequency, value = amplitude |
| smooth    | Soften hard edges. intensity = number of blur passes (1-5) |

## Brush Shapes — When to Use What
| brushShape | best for |
|------------|----------|
| rect       | Filling large flat areas uniformly (roads, platforms, bases) |
| circle     | Precise spots with hard edge (craters, ponds, clearings) |
| gaussian   | Natural organic shapes (hills, mountains, river banks) — USE THIS BY DEFAULT |
| brushName  | Complex/asymmetric masks impossible with geometry (custom erosion, cliff faces, custom stamps) |

## Texture Brush Workflow (brushName)
Use when gaussian/circle/rect are not expressive enough.
\`\`\`
// Step 1: discover what's available
unity_list_terrain_brushes({ filter: "smooth" })
// → returns [{ name: "SmoothHeight", path: "Assets/Brushes/SmoothHeight.png" }, ...]

// Step 2: use the name in sculpt or paint
unity_set_terrain_heights_batch({
  operation: "raise",
  brushName: "SmoothHeight",   // texture drives the brush shape
  brushSize: 150,
  brushCenter: { x: 0.5, z: 0.5 },
  value: 0.08,
  opacity: 0.7
})
\`\`\`
- Any Texture2D in the project can be a brush (grayscale PNGs work best)
- Red channel = brush weight (white = full effect, black = no effect)
- Partial name match, case-insensitive: "smooth" matches "SmoothHeight_01"

## brushSize vs region
- **brushSize** (world units) + **brushCenter** (0-1 normalized) → preferred for sculpting, intuitive
- **region** {x,y,width,height} (normalized 0-1) → preferred for filling rectangular zones

## Key Parameters
- **opacity** 0-1: overall strength of the operation (always apply this, start at 0.5-0.8 for natural results)
- **brushFalloff** 0-1: only for gaussian — 0 = wide flat mound, 1 = tight sharp peak (0.3-0.6 = natural hills)
- **brushRotation** degrees: rotate brush mask (useful with asymmetric texture brushes)
- **value**: meaning depends on operation — height 0-1 for flatten/set, delta for raise/lower, amplitude for noise

## Texture Painting (unity_paint_terrain_texture_batch)
- Always add layers first with unity_add_terrain_layer
- layerIndex 0 = base layer (paint it fully first: opacity 1.0, region entire terrain)
- Use gaussian brush with opacity 0.4-0.7 for natural blending between layers
- Weights are auto-normalized — no need to manually balance all layers

## Common Patterns
\`\`\`
// Natural mountain
unity_set_terrain_heights_batch({ operation: "raise", brushShape: "gaussian", brushFalloff: 0.4,
  brushSize: 300, value: 0.35, opacity: 0.8 })

// Rocky plateau with custom brush
unity_list_terrain_brushes({ filter: "cliff" })
unity_set_terrain_heights_batch({ operation: "raise", brushName: "CliffEdge",
  brushSize: 200, value: 0.2, opacity: 0.6 })

// Smooth pass after noise
unity_set_terrain_heights_batch({ operation: "smooth", brushShape: "rect", intensity: 3, opacity: 1.0 })

// Paint grass base then dirt on slopes
unity_paint_terrain_texture_batch({ layerIndex: 0, opacity: 1.0 })  // grass everywhere
unity_paint_terrain_texture_batch({ layerIndex: 1, brushShape: "gaussian", brushCenter: {x:0.3, z:0.6},
  brushSize: 80, opacity: 0.6 })  // dirt patch

// Paint road along waypoints
unity_paint_terrain_path({ layerIndex: 2, waypoints: [{x:100,z:100}, {x:500,z:300}, {x:800,z:600}],
  width: 8, falloff: 0.3 })
\`\`\``,

  'workflows://assets': `# Asset Browser Workflow

## Search Syntax
- Type: t:Texture2D, t:Prefab, t:Material, t:AnimationClip, t:AudioClip, t:Script
- Label: l:Environment, l:Player
- Name: Player, Enemy*
- Combined: "t:Prefab Player", "t:Texture2D l:UI"

## Workflow
1. List folders: unity_list_folders()
2. Browse: unity_list_folder_contents({ folderPath: "Assets/Prefabs" })
3. Search: unity_search_assets({ filter: "t:Prefab Player", maxResults: 20 })
4. Details: unity_get_asset_info({ assetPath: "...", includeDependencies: true })
5. Preview: unity_get_asset_preview({ assetPath: "...", size: "small" })

## Token Tips
- Use maxResults=20 (default)
- Use size='small' for previews
- Avoid includeReferences (slow)`,
};
