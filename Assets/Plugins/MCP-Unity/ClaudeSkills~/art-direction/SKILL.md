---
name: "Art Direction"
description: "Define the art direction of a Unity project. Use when the user says '/art-direction', 'art direction', 'art bible', 'visual style', 'style guide', or wants to define palette, materials, lighting, and UI style for a game."
---

# Art Direction — Art Bible

Defines the visual identity: style, color palette, materials, lighting, post-processing, UI.

## Prerequisites

- GDD and TDD available (for context)
- Render pipeline known (URP/HDRP/Built-in)
- MCP Unity connected

## Step-by-Step Guide

### Step 1: Inspect Pipeline via MCP Unity

```
unity_get_render_pipeline_info → pipeline type, quality levels, features
unity_get_project_overview → platform, packages
unity_search_assets { filter: "t:Material" } → existing materials
unity_search_assets { filter: "t:Shader" } → available shaders
unity_get_lightmap_settings → current lighting setup
```

### Step 2: Define Visual Identity with User

Ask these key questions:

1. **Art Style:** Realistic, stylized, cel-shaded, pixel art, low-poly, hand-painted?
2. **Color Palette:** Warm/cold? Saturated/muted? Primary mood (dark, vibrant, pastel)?
3. **Visual References:** Games, films, art that match the target look
4. **Target Platforms:** Mobile (tight budget) vs PC/Console (more headroom)
5. **UI Style:** Minimal HUD, diegetic, classic panels?

### Step 3: Pipeline-Specific Recommendations

#### URP (Universal Render Pipeline)
| Feature | Available | Notes |
|---------|-----------|-------|
| Lit/Unlit Shaders | Yes | Primary shaders for 3D/2D |
| Shader Graph | Yes | Custom shaders without code |
| 2D Renderer | Yes | Sprite-lit, sprite-unlit |
| Post-Processing | Volume-based | Bloom, color grading, vignette, DOF |
| Realtime Shadows | Limited | Cascaded shadow maps, 1 directional |
| SSAO | Yes (URP 14+) | Screen Space Ambient Occlusion |
| Light Cookies | Yes | Projected texture lights |
| Decals | Yes (URP 12+) | Decal projector system |
| Ray Tracing | No | Not supported |

**URP Best Practices:**
- Use Shader Graph for custom effects — avoid writing shader code
- Limit real-time lights (1 directional + max 4-8 additional per object)
- Bake static lighting for performance: `unity_bake_lighting`
- Use Light Probes for dynamic objects in baked scenes
- SRP Batcher compatible materials for draw call batching

#### HDRP (High Definition Render Pipeline)
| Feature | Available | Notes |
|---------|-----------|-------|
| PBR Shaders | Yes | Full physically-based rendering |
| Shader Graph | Yes | Advanced nodes (eyes, hair, fabric) |
| Volumetric Fog | Yes | Ray-marched fog volumes |
| Ray Tracing | Yes (DX12) | Reflections, GI, shadows, AO |
| Subsurface Scattering | Yes | Skin, wax, leaves |
| Area Lights | Yes | Rectangle, disc, tube |
| Decals | Yes | Full decal system |
| Custom Pass | Yes | Advanced rendering injection |

**HDRP Best Practices:**
- Use Lit shader with material types (Standard, SSS, Anisotropy, etc.)
- Enable volumetric fog for atmosphere — use Density Volumes
- Set up Exposure control (fixed for stylized, auto for realistic)
- Ray tracing only for PC with supported GPUs — always have fallback

#### Built-in Render Pipeline
| Feature | Available | Notes |
|---------|-----------|-------|
| Standard Shader | Yes | PBR, metallic/specular workflow |
| Surface Shaders | Yes | Code-based custom shaders |
| Post-Processing Stack | v2 package | Separate package required |
| Lightmapping | Yes | Progressive CPU/GPU |
| Realtime GI | Yes | Enlighten (deprecated) |

**Built-in Best Practices:**
- Use Standard shader where possible for consistency
- Surface shaders for custom effects
- Post-processing v2 stack for visual polish

### Step 4: Material Strategy

Define naming conventions and material templates:

```
Materials/
├── Environment/
│   ├── M_Ground_Grass.mat
│   ├── M_Ground_Rock.mat
│   └── M_Water_River.mat
├── Characters/
│   ├── M_Player_Body.mat
│   └── M_Player_Eyes.mat
├── Props/
│   ├── M_Prop_Wood.mat
│   └── M_Prop_Metal.mat
└── FX/
    ├── M_FX_Fire.mat
    └── M_FX_Glow.mat
```

**Naming Convention:** `M_{Category}_{Description}` — prefix `M_` for materials.

**Texture Budget by Platform:**
| Platform | Max Texture Size | Format | Atlas |
|----------|-----------------|--------|-------|
| Mobile | 1024x1024 | ASTC / ETC2 | Yes |
| PC | 2048x2048 | BC7 / DXT5 | Optional |
| Console | 2048-4096 | Platform native | Optional |

### Step 5: Lighting Direction

Define the lighting strategy:

| Approach | When to Use | MCP Setup |
|----------|------------|-----------|
| **Fully Baked** | Mobile, static scenes | `unity_set_lightmap_settings` → `unity_bake_lighting` |
| **Mixed** | Most games — baked indirect + realtime direct | `unity_set_lightmap_settings { mixedMode: "ShadowMask" }` |
| **Fully Realtime** | Dynamic time-of-day, destructible environments | Limit light count, use LOD |

### Step 6: Generate the Art Bible

Template: see `unity-planner/references/templates/art-bible-template.md`

Fill in all sections with the pipeline-specific choices made above.

### Step 7: Save

`.claude/context/unity-planner/art-{project}.md`

## Common Art Style Recipes

| Style | Pipeline | Shader | Lighting | Post-Processing |
|-------|----------|--------|----------|----------------|
| **Realistic** | HDRP | Lit (PBR) | Mixed + RT reflections | Color grading, SSAO, bloom |
| **Stylized 3D** | URP | Shader Graph (toon ramp) | Baked + 1 directional | Bloom, vignette |
| **Pixel Art 2D** | URP 2D | Sprite-Lit-Default | 2D lights | Pixelate post-process |
| **Low Poly** | URP | Lit (flat shading) | Baked | Bloom, color grading |
| **Cel-Shaded** | URP | Shader Graph (step ramp) | Realtime directional | Outline post-process |
| **Hand-Painted** | URP/Built-in | Unlit/Lit + painted textures | Baked | Warm color grading |

## Agent

**Art Director** — Expert in visual style, materials, UI, post-processing.
Details: see `unity-planner/references/agents.md` (Agent 6)
MCP workflows: W4 in `mcp-unity-workflows.md`
