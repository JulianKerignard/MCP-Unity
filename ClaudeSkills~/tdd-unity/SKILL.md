---
name: "TDD Unity"
description: "Creer un Technical Design Document pour un projet Unity. Utiliser quand l'utilisateur dit '/tdd-unity', 'technical design document', 'architecture unity', 'architecture technique', 'TDD unity', ou veut definir l'architecture technique du jeu. Couvre render pipeline, patterns, performance budgets, packages."
---

# TDD Unity — Technical Design Document

Genere un Technical Design Document avec des decisions d'architecture specifiques a Unity.

## Prerequisites

- `/unity-plan` ou `/gdd` execute (pour contexte)
- MCP Unity connecte (pour inspecter le projet)

## Step-by-Step Guide

### Step 1 : Inspecter le projet via MCP Unity

```
unity_get_render_pipeline_info → details pipeline, quality settings
unity_get_build_settings → plateformes cibles, scripting backend
unity_get_project_settings → player settings
unity_list_packages → packages installes
unity_list_project_scripts → scripts existants
unity_get_script_info { className } → API publique des scripts cles
```

### Step 2 : Charger le template

Template : `~/.claude/skills/unity-planner/references/templates/tdd-unity-template.md`

### Step 3 : Decisions cles a discuter

- **Architecture pattern :** ECS vs MonoBehaviour vs Hybrid — justifier
- **State management :** Singletons, ScriptableObject events, event bus
- **Input system :** New Input System vs Legacy
- **Scene organization :** Single scene vs multi-scene vs additive loading
- **Asset strategy :** Resources vs Addressables

### Step 4 : Generer les sections

Adapter au niveau :
- **Level 0-1 :** Sections 1, 3.1-3.3 (architecture + 3 core systems)
- **Level 2 :** Sections 1-6 (tout sauf CI/CD)
- **Level 3-4 :** Toutes les sections

### Step 5 : Sauvegarder et mettre a jour le status

## Agent

**Technical Director** — Expert en architecture Unity, render pipelines, performance.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 2)
MCP workflows : `~/.claude/skills/unity-planner/references/workflows/mcp-unity-workflows.md` (W2)

## Regles

- **Toujours inspecter** le projet via MCP avant de recommander
- **Justifier** chaque choix technique vs alternatives
- **Inclure** des performance budgets realistes par plateforme
- **Referencer** les packages existants avant d'en recommander de nouveaux
