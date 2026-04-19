# Level Design Document: {{LEVEL_NAME}}

**Scene:** {{SCENE_PATH}} | **Date:** {{DATE}} | **Status:** {{STATUS}}
**Purpose in Game Flow:** {{PURPOSE}}

---

## 1. Level Overview

### 1.1 Player Objectives
### 1.2 Estimated Play Time
### 1.3 Difficulty Curve Position
### 1.4 Mechanics Introduced / Featured

---

## 2. Layout

### 2.1 Top-Down Map
<!-- ASCII art or textual description of key areas -->

### 2.2 Key Areas / Zones
| Zone | Purpose | Enemies | Items | Notes |
|------|---------|---------|-------|-------|

### 2.3 Critical Path
<!-- The main route players must follow -->

### 2.4 Exploration Areas
<!-- Optional areas, secrets, shortcuts -->

### 2.5 Verticality Plan [if applicable]

---

## 3. Scene Hierarchy Plan

```
{{LEVEL_NAME}}/
├── Environment/
│   ├── Terrain/
│   ├── Props_Static/
│   ├── Props_Dynamic/
│   └── Boundaries/
├── Gameplay/
│   ├── SpawnPoints/
│   ├── Enemies/
│   ├── Interactables/
│   ├── Collectibles/
│   └── Triggers/
├── Lighting/
│   ├── Directional/
│   ├── PointLights/
│   └── ReflectionProbes/
├── Audio/
│   ├── AmbientZones/
│   └── MusicTriggers/
├── Cameras/
└── UI/
```

### 3.1 Naming Conventions
### 3.2 Layer & Tag Usage
| Layer | Used For |
|-------|----------|
| Default | Static environment |
| Player | Player character |
| Enemy | Enemy characters |
| Interactable | Pickups, doors, switches |
| Trigger | Invisible trigger zones |

---

## 4. Terrain & Environment [if terrain-based]

### 4.1 Terrain Dimensions
- Size: {{WIDTH}} x {{LENGTH}} x {{HEIGHT}}
- Heightmap resolution: {{RESOLUTION}}

### 4.2 Texture Layers
| Layer | Texture | Usage |
|-------|---------|-------|

### 4.3 Vegetation Plan
### 4.4 Water Plan [if applicable]

---

## 5. Lighting Plan

### 5.1 Time of Day / Mood
### 5.2 Light Sources
| Type | Position | Color | Intensity | Shadows |
|------|----------|-------|-----------|---------|

### 5.3 Lightmap Settings
- Mode: Baked / Mixed / Realtime
- Resolution:
- Bounces:

---

## 6. Navigation

### 6.1 NavMesh Configuration
- Agent types and sizes
- Walkable areas

### 6.2 Off-Mesh Links
| From | To | Purpose |
|------|-----|---------|

---

## 7. Audio Zones

### 7.1 Ambient Sounds
| Zone | Sound | Volume | Loop |
|------|-------|--------|------|

### 7.2 Music Transitions
### 7.3 Trigger-based Audio Events

---

## 8. Gameplay Elements

### 8.1 Spawn Points
| ID | Position | Type | Notes |
|----|----------|------|-------|

### 8.2 Enemies / NPCs
| Enemy | Count | Patrol | Trigger |
|-------|-------|--------|---------|

### 8.3 Interactables
### 8.4 Collectibles / Pickups
### 8.5 Triggers / Events
| Trigger | Condition | Effect |
|---------|-----------|--------|
