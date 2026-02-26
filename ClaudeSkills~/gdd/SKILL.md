---
name: "GDD"
description: "Creer un Game Design Document pour un projet Unity. Utiliser quand l'utilisateur dit '/gdd', 'game design document', 'GDD', 'design du jeu', 'concevoir le gameplay', ou veut documenter les mecaniques et systemes d'un jeu. Genere un GDD structure adapte a la taille du projet."
---

# GDD — Game Design Document

Genere un Game Design Document adapte au niveau du projet (Prototype → AAA).

## Prerequisites

- `/unity-plan` execute (ou au minimum : nom du projet et niveau connus)
- Concept de jeu en tete

## Step-by-Step Guide

### Step 1 : Charger le contexte

Lire `unity-planner-status.yaml` si existant → recuperer le niveau du projet.
Si pas de status, demander : nom du jeu, genre, plateforme, taille du projet.

### Step 2 : Charger le template

Template : `~/.claude/skills/unity-planner/references/templates/gdd-template.md`

### Step 3 : Generation interactive section par section

Pour chaque section, demander les choix cles a l'utilisateur :

**Section 1 — Game Overview :** concept, genre, audience, plateformes, USPs
**Section 2 — Core Gameplay :** core loop, mecaniques, goals, controles
**Section 3 — Game Systems [INDIE+] :** progression, economie, combat, IA
**Section 4 — Content [INDIE+] :** niveaux, personnages, items, narration
**Section 5 — UI/UX Flow [JAM+] :** screen flow, HUD, menus
**Section 6 — Multiplayer [AA+] :** architecture reseau, modes, matchmaking
**Section 7 — Monetization [AA+] :** modele economique, IAP, pubs
**Section 8 — Feature Pillars :** priorites P0-P3 par milestone

Adapter la profondeur au niveau :
- **Level 0-1 :** Sections 1-2 seulement
- **Level 2 :** Sections 1-5, 8
- **Level 3-4 :** Toutes les sections + appendices

### Step 4 : Sauvegarder

Sauver dans `.claude/context/unity-planner/gdd-{projet}.md`
Mettre a jour `unity-planner-status.yaml` : `documents.gdd.status = "draft"`

### Step 5 : Recommander la suite

Selon le niveau, proposer : `/tdd-unity`, `/art-direction`, ou `/milestone`

## Agent

**Game Designer** — Expert en mecaniques, systemes, core loop.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 1)

## Regles

- Generation **interactive** — ne pas generer tout d'un coup sans demander
- Adapter la profondeur au **niveau du projet**
- Les sections tagguees `[INDIE+]`, `[AA+]` sont **conditionelles**
- Rester coherent avec le genre et l'audience cible
