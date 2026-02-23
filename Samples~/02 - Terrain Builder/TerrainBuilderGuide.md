# MCP Unity — Terrain Builder Sample

This sample shows how to use MCP Unity's **17 terrain tools** to build a complete game-ready landscape
entirely through AI prompts, without touching the Unity terrain inspector manually.

## Tools covered

| Tool | What it does |
|------|-------------|
| `unity_create_terrain` | Create a new terrain with configurable size and resolution |
| `unity_get_terrain_info` | Read heightmap, layers, trees, details |
| `unity_modify_terrain` | Set terrain properties (draw instanced, pixel error, etc.) |
| `unity_set_terrain_heights_batch` | Sculpt heightmap with raise/lower/flatten/smooth |
| `unity_add_terrain_layer` | Add or replace terrain texture layers |
| `unity_paint_terrain_texture_batch` | Paint texture layers (grass, rock, dirt...) |
| `unity_paint_terrain_path` | Paint a path/road on terrain along waypoints |
| `unity_add_terrain_trees` | Place trees with density/random parameters |
| `unity_list_terrain_trees` | List all tree prototypes and placements |
| `unity_remove_terrain_trees` | Clear trees by prototype index or area |
| `unity_add_terrain_detail` | Add grass/detail prototypes |
| `unity_paint_terrain_detail` | Paint detail density maps |
| `unity_remove_terrain_detail` | Remove detail prototype or clear painted area |
| `unity_import_heightmap` | Import a PNG/RAW heightmap |
| `unity_export_heightmap` | Export terrain heightmap to PNG |
| `unity_set_terrain_neighbors` | Stitch adjacent terrains for seamless borders |
| `unity_get_terrain_holes` | Inspect terrain hole data |

---

## Workflow: Build a landscape in 5 prompts

### Prompt 1 — Create the terrain
```
Create a terrain named "MainLandscape" with:
- Width: 1000, Height: 200, Length: 1000
- heightmapResolution: 513
- Position at (-500, 0, -500) so it's centered on the origin
```

### Prompt 2 — Sculpt the base shape
```
On the terrain "MainLandscape":
1. Raise the center area (normalized region 0.3-0.7 x 0.3-0.7) by height 0.5 to form a plateau
2. Raise a mountain peak at normalized position (0.2, 0.2) with radius 80 and height 0.8
3. Flatten the edges (all 4 borders, width 0.05 normalized) to height 0.02 for coastline
4. Smooth the entire heightmap twice to remove sharp edges
```

### Prompt 3 — Add textures
```
Add these texture layers to "MainLandscape":
1. A grass texture (any green material from project, or use the default terrain layer)
2. A rock texture for steep slopes
3. A sand/dirt texture for low-elevation areas

Then paint:
- Grass everywhere as base
- Rock on slopes > 30 degrees (estimated normalized region 0.15-0.35 on the mountain)
- Sand on the flat border areas
```

### Prompt 4 — Place trees and details
```
On "MainLandscape":
1. Add a tree prototype from any tree prefab in the project
2. Place 200 trees randomly on the plateau area (normalized 0.3-0.7 x 0.3-0.7),
   avoiding the mountain peak
3. Add a grass detail prototype
4. Paint dense grass across the plateau
```

### Prompt 5 — Export and verify
```
Export the "MainLandscape" heightmap to Assets/Terrain/MainLandscape_heightmap.png,
then get the full terrain info to confirm tree count, detail layers, and texture layers.
```

---

## Multi-terrain stitching

If you need a large seamless world with multiple terrain tiles:

```
Create four terrain tiles:
- "Terrain_NW" at position (-500, 0, 0)  
- "Terrain_NE" at position (0, 0, 0)
- "Terrain_SW" at position (-500, 0, -500)
- "Terrain_SE" at position (0, 0, -500)

Then set terrain neighbors:
- Terrain_NW: right=Terrain_NE, bottom=Terrain_SW
- Terrain_NE: left=Terrain_NW, bottom=Terrain_SE
(etc.)

This eliminates seam artifacts at terrain borders.
```

---

## Tips

- Always `unity_get_terrain_info` before modifying to know the current state
- Heightmap coordinates are normalized (0.0 to 1.0), not world units  
- Tree and detail operations use `Undo.RegisterCompleteObjectUndo` — Ctrl+Z works
- For large terrains (2049+ resolution), prefer `unity_import_heightmap` from a PNG
  generated in a DCC tool (Blender, World Creator, Gaea, etc.)
