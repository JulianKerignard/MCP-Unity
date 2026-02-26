---
name: "Art Direction"
description: "Definir la direction artistique d'un projet Unity. Utiliser quand l'utilisateur dit '/art-direction', 'art direction', 'art bible', 'style visuel', 'direction artistique', 'style guide du jeu', ou veut definir la palette, les materiaux, l'eclairage et l'UI d'un jeu."
---

# Art Direction — Art Bible

Definit l'identite visuelle du jeu : style, palette, materiaux, eclairage, UI.

## Prerequisites

- GDD et TDD existants (pour contexte)
- Render pipeline connu (URP/HDRP/Built-in)

## Step-by-Step Guide

### Step 1 : Inspecter le pipeline via MCP Unity

```
unity_get_render_pipeline_info → capacites du pipeline
unity_search_assets { filter: "t:Material" } → materiaux existants
unity_search_assets { filter: "t:Shader" } → shaders disponibles
```

### Step 2 : Definir avec l'utilisateur

- Style (realiste, stylise, pixel art, low-poly, etc.)
- Palette de couleurs et ambiance
- References visuelles (jeux, films, art)
- Plateformes cibles (impact sur le budget qualite)

### Step 3 : Generer l'Art Bible

Template : `~/.claude/skills/unity-planner/references/templates/art-bible-template.md`

Adapter les recommandations au pipeline :
- **URP :** Lit/Unlit, Shader Graph, 2D Renderer option
- **HDRP :** PBR, fog volumetrique, ray tracing
- **Built-in :** Standard shader, surface shaders

### Step 4 : Sauvegarder

`.claude/context/unity-planner/art-{projet}.md`

## Agent

**Art Director** — Expert en style visuel, materiaux, UI, post-processing.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 6)
MCP workflows : W4 dans `mcp-unity-workflows.md`
