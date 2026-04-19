---
name: "GDD"
description: "Create a Game Design Document for a Unity project. Use when the user says '/gdd', 'game design document', 'GDD', 'design the game', 'design gameplay', or wants to document the mechanics and systems of a game. Generates a structured GDD adapted to the project size."
---

# GDD — Game Design Document

Generates a Game Design Document adapted to the project level (Prototype → AAA).

## Prerequisites

- `/unity-plan` executed (or at minimum: project name and level known)
- Game concept in mind

## Step-by-Step Guide

### Step 1: Load Context

Read `unity-planner-status.yaml` if it exists → retrieve project level.
If no status, ask: game name, genre, platform, project size.

Inspect project via MCP Unity for existing content:
```
unity_get_project_overview → current state, packages, asset counts
unity_list_gameobjects { outputMode: "tree" } → existing scene structure
unity_list_project_scripts → existing gameplay scripts
```

### Step 2: Load Template

Template: see `unity-planner/references/templates/gdd-template.md`

### Step 3: Interactive Generation — Section by Section

For each section, ask the user key questions before writing:

#### Section 1 — Game Overview
Questions to ask:
- What is your game about in one sentence? (elevator pitch)
- Genre? (roguelike, platformer, RPG, FPS, puzzle, simulation...)
- Target audience? (casual, core, hardcore)
- Target platforms? (Mobile, PC, Console, WebGL)
- What makes your game unique? (USPs — Unique Selling Points)

#### Section 2 — Core Gameplay
Questions to ask:
- What does the player DO every 30 seconds? (core loop)
- Primary mechanics? (jump, shoot, build, match, explore...)
- Win/lose conditions?
- Control scheme? (gamepad, keyboard+mouse, touch, motion)

#### Section 3 — Game Systems [INDIE+]
Questions to ask:
- Progression system? (XP, levels, skill tree, unlocks)
- Economy? (currency, shops, loot)
- Combat/interaction system? (real-time, turn-based, physics-based)
- AI behavior? (patrol, chase, state machine, behavior tree)

#### Section 4 — Content [INDIE+]
Questions to ask:
- How many levels/scenes? What's the structure?
- Characters? (player, NPCs, enemies — how many types?)
- Items/collectibles?
- Story/narrative? (linear, branching, emergent)

#### Section 5 — UI/UX Flow [JAM+]
Questions to ask:
- Screen flow? (main menu → gameplay → pause → game over)
- HUD elements during gameplay?
- Menu navigation style?

#### Section 6 — Multiplayer [AA+]
Questions to ask:
- Multiplayer model? (local, online, both)
- Architecture? (client-server, P2P, relay)
- Modes? (co-op, PvP, competitive)

#### Section 7 — Monetization [AA+]
Questions to ask:
- Business model? (premium, F2P, freemium)
- IAP types? (cosmetic, gameplay, season pass)

#### Section 8 — Feature Pillars
Derive from above — organize features by priority:
- **P0 (must-have):** Core loop, essential mechanics
- **P1 (should-have):** Key systems, basic content
- **P2 (nice-to-have):** Polish, extra content
- **P3 (cut if needed):** Stretch goals

### Depth Adaptation by Level

| Level | Sections | Depth |
|-------|----------|-------|
| **0-1 (Proto/Jam)** | 1-2 only | 1-2 pages total, bullet points |
| **2 (Indie)** | 1-5, 8 | 3-6 pages, detailed mechanics |
| **3-4 (AA/AAA)** | All sections + appendices | 8-15 pages, full specifications |

### Step 4: Save

Save to `.claude/context/unity-planner/gdd-{project}.md`
Update `unity-planner-status.yaml`: `documents.gdd.status = "draft"`

### Step 5: Recommend Next

Based on level, suggest: `/tdd-unity`, `/art-direction`, or `/milestone`

## Agent

**Game Designer** — Expert in mechanics, systems, core loop.
Details: see `unity-planner/references/agents.md` (Agent 1)

## Rules

- **Interactive** generation — don't generate everything at once without asking
- Adapt depth to the **project level**
- Sections tagged `[INDIE+]`, `[AA+]` are **conditional**
- Stay coherent with the genre and target audience
- Use MCP Unity inspection results to ground the GDD in reality
