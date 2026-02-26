---
name: "Level Design"
description: "Planifier le design d'un niveau ou scene Unity. Utiliser quand l'utilisateur dit '/level-design', 'design de niveau', 'level design', 'planifier une scene', 'scene structure', ou veut organiser la hierarchie, le terrain, l'eclairage et la navigation d'un niveau."
---

# Level Design — Level Design Document

Planifie la structure d'un niveau : layout, hierarchie de scene, terrain, eclairage, navigation, audio.

## Prerequisites

- GDD et/ou TDD existants (pour contexte gameplay et technique)
- MCP Unity connecte (pour inspecter la scene courante)

## Step-by-Step Guide

### Step 1 : Inspecter la scene via MCP Unity

```
unity_get_scene_info → metadonnees
unity_list_gameobjects { outputMode: "tree" } → hierarchie
unity_get_terrain_info → config terrain (si present)
unity_get_navmesh_settings → navigation
unity_get_lightmap_settings → eclairage
```

### Step 2 : Definir avec l'utilisateur

- Objectif du niveau dans le game flow (depuis GDD)
- Layout (lineaire, open world, hub, arena)
- Zones cles et chemin critique
- Terrain vs mesh-based environment
- Ambiance et eclairage

### Step 3 : Generer le document

Template : `~/.claude/skills/unity-planner/references/templates/level-design-template.md`

Inclure les sequences MCP pour implementation :
- Terrain : `unity_create_terrain` → `unity_set_terrain_heights_batch` → `unity_add_terrain_layer`
- Lighting : `unity_bake_lighting` avec `unity_set_lightmap_settings`
- Navigation : `unity_bake_navmesh` apres `unity_set_navigation_static`

### Step 4 : Sauvegarder

`.claude/context/unity-planner/level-{nom}.md`

## Agent

**Level Designer** — Expert en spatial design, terrain, eclairage, navigation.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 3)
MCP workflows : W3 dans `mcp-unity-workflows.md`
