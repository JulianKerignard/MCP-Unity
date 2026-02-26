---
name: "Unity Story"
description: "Creer une story d'implementation Unity avec sequences d'outils MCP. Utiliser quand l'utilisateur dit '/unity-story', 'unity story', 'implementer dans unity', 'creer un feature unity', 'story implementation', ou veut une story detaillee avec les outils MCP Unity a executer."
---

# Unity Story — Implementation Story

Genere des stories d'implementation avec des sequences concretes d'outils MCP Unity.

## Prerequisites

- TDD et/ou GDD existants (pour contexte)
- MCP Unity connecte et fonctionnel
- Skill `mcp-unity` installe pour reference des outils

## Step-by-Step Guide

### Step 1 : Identifier la feature

Demander a l'utilisateur : "Quel systeme/feature veux-tu implementer ?"
Croiser avec le GDD et le TDD si disponibles.

### Step 2 : Generer la story

Structure :
- **Titre :** description courte
- **Objectif :** ce que le joueur/systeme doit faire
- **Criteres d'acceptation :** conditions testables
- **Sequence MCP :** outils a executer dans l'ordre
- **Scripts a creer :** noms de classes et responsabilites
- **Components a ajouter :** liste avec proprietes
- **Prefabs a creer :** si applicable
- **Plan de test :** quoi verifier

### Step 3 : Definir la sequence MCP

Exemples de recipes dans `~/.claude/skills/unity-planner/references/workflows/mcp-unity-workflows.md` :
- Recipe A : Script + Component
- Recipe B : Physics Object
- Recipe C : Prefab Workflow
- Recipe D : Batch Scene Setup
- Recipe E : Material Setup
- Recipe F : Terrain
- Recipe G : UI Setup
- Recipe H : Lighting Bake
- Recipe I : Audio Setup
- Recipe J : Input System

### Step 4 : Output

La story peut etre :
1. **Executee directement** avec MCP Unity (copier-coller les sequences)
2. **Donnee a `/team`** pour paralleliser l'implementation
3. **Integree a `/start`** comme feature branch

## Agent

**Unity Developer** — Expert des 164 outils MCP Unity.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 5)
Reference complete des outils : `~/.claude/skills/mcp-unity/references/tool-reference-complete.md`

## Regles

- Les sequences MCP doivent etre **directement executables**
- **Toujours** inclure `unity_save_scene` apres les modifications
- **Toujours** inclure `unity_refresh_and_compile` apres creation de scripts
- Suivre les **conventions de nommage** du TDD si disponible
