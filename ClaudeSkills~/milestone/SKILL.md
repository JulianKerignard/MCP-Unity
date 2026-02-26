---
name: "Milestone"
description: "Planifier les milestones d'un projet Unity. Utiliser quand l'utilisateur dit '/milestone', 'milestones', 'planning du jeu', 'vertical slice', 'alpha beta gold', 'roadmap du jeu', ou veut definir les etapes de developpement du jeu avec des dates et criteres."
---

# Milestone — Milestone Plan

Definit les milestones du jeu : Prototype, Vertical Slice, Alpha, Beta, Gold.

## Prerequisites

- GDD existant (pour les feature pillars)
- Niveau de projet connu

## Step-by-Step Guide

### Step 1 : Charger le contexte

Lire le GDD pour identifier les feature pillars et priorites.
Lire le status YAML pour le niveau du projet.

### Step 2 : Definir les milestones selon le niveau

- **Level 0 :** Prototype seul
- **Level 1 :** Prototype → Submission
- **Level 2 :** Prototype → Vertical Slice → Alpha → Beta → Gold
- **Level 3-4 :** + Release Candidate

### Step 3 : Pour chaque milestone

- Definition of Done (criteres mesurables)
- Feature scope depuis les pillars GDD (P0-P3)
- Matrice de risques (likelihood x impact + mitigation)
- Date cible (discuter avec l'utilisateur)

### Step 4 : Mapper les pillars aux milestones

- P0 (must-have) → Vertical Slice
- P1 (should-have) → Alpha
- P2 (nice-to-have) → Beta
- P3 (cut if needed) → post-launch

### Step 5 : Sauvegarder

`.claude/context/unity-planner/milestones-{projet}.md`
Template : `~/.claude/skills/unity-planner/references/templates/milestone-template.md`

## Agent

**Producer** — Expert en milestones, scope, risques.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 4)
