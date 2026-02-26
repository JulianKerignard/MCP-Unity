---
name: "Unity Plan"
description: "Initialiser et orchestrer la planification d'un projet Unity. Utiliser quand l'utilisateur dit '/unity-plan', 'planifier un jeu unity', 'plan unity project', 'initialiser projet unity', ou veut demarrer la planification d'un jeu. Detecte l'etat du projet via MCP Unity, determine le niveau (Prototype/Jam/Indie/AA/AAA), cree le status YAML, et route vers les commandes appropriees."
---

# Unity Plan — Orchestrateur

Initialise la planification d'un projet Unity. Detecte le projet, determine sa taille, et guide vers les bonnes commandes.

## Prerequisites

- Projet Unity ouvert avec MCP Unity connecte
- Skill `mcp-unity` installe (`~/.claude/skills/mcp-unity/`)

## Step-by-Step Guide

### Step 1 : Detecter l'etat du projet

Executer les outils MCP Unity en sequence :
1. `unity_get_editor_state` → version Unity, etat de compilation, mode play
2. `unity_get_project_overview` → render pipeline, packages, assets, scenes
3. `unity_list_gameobjects { outputMode: "tree" }` → hierarchie (format economique)
4. `unity_list_scenes_in_project` → toutes les scenes

Resumer : version Unity, render pipeline, nombre de scenes/assets/packages, projet neuf ou existant.

Si MCP Unity n'est pas connecte, demander a l'utilisateur : version Unity, render pipeline, description du projet, taille d'equipe, timeline.

### Step 2 : Determiner le niveau du projet

Utiliser l'heuristique d'auto-detection :

| Signal | 0: Proto | 1: Jam | 2: Indie | 3: AA | 4: AAA |
|--------|----------|--------|----------|-------|--------|
| Scenes | 1 | 1-3 | 3-15 | 15-50 | 50+ |
| Assets | <50 | <200 | <1000 | <5000 | 5000+ |
| Packages | <5 | <10 | <20 | <30 | 30+ |
| Scripts | <5 | <20 | <100 | <500 | 500+ |

Pour un projet neuf, demander directement le type (prototype/jam/indie/AA/AAA).
**Toujours confirmer** le niveau avec l'utilisateur.

### Step 3 : Creer le status YAML

Creer `.claude/context/unity-planner/unity-planner-status.yaml` avec les metadonnees detectees.
Schema complet : voir `~/.claude/skills/unity-planner/references/schema/status-tracking.md`

### Step 4 : Presenter la sequence recommandee

Selon le niveau :
- **Level 0-1 :** `/gdd` (compact) → `/tdd-unity` (compact) → `/unity-story`
- **Level 2 :** `/gdd` → `/tdd-unity` → `/art-direction` → `/milestone` → `/level-design` → `/unity-story`
- **Level 3-4 :** `/gdd` → `/milestone` → `/tdd-unity` → `/art-direction` → `/level-design` → `/unity-review` → `/unity-story`

### Step 5 : Router vers la premiere commande

Demander a l'utilisateur par quel document commencer, puis lancer la commande correspondante.

## References

- Agents : `~/.claude/skills/unity-planner/references/agents.md`
- Project levels : `~/.claude/skills/unity-planner/references/workflows/project-levels.md`
- Status schema : `~/.claude/skills/unity-planner/references/schema/status-tracking.md`
- MCP Unity workflows : `~/.claude/skills/unity-planner/references/workflows/mcp-unity-workflows.md`

## Regles

- **Read-only** pendant la planification — ne jamais modifier le projet
- **Toujours** utiliser MCP Unity pour inspecter le projet (pas deviner)
- **Sauver** tous les outputs dans `.claude/context/unity-planner/`
- **Confirmer** le niveau avec l'utilisateur avant de continuer
