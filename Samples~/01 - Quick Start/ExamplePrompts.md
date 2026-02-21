# Example Prompts — Quick Start

Copy any of these prompts directly into your AI editor (Claude Code, Cursor, Windsurf, VS Code Copilot).

---

## Scene Management

```
List all scenes in the project, then open the first one and show me the full hierarchy.
```

```
Create a new empty scene named "GameLevel_01" and save it to Assets/Scenes/.
```

```
Take a screenshot of the current Scene view and describe what you see.
```

---

## GameObjects

```
Create a sphere named "Enemy_01" at position (3, 1, 0), add a Rigidbody component 
with mass 2, and apply a red material from the project.
```

```
Find all GameObjects tagged "Enemy" and disable them.
```

```
Duplicate "Player" three times, place copies at positions (5,0,0), (-5,0,0), (0,0,5), 
and name them "Player_Red", "Player_Blue", "Player_Green".
```

---

## Components & Materials

```
Add a BoxCollider to every MeshRenderer GameObject in the scene that doesn't have one.
```

```
Create a new material named "GlowEffect", set its shader to Universal Render Pipeline/Lit, 
set the base color to (1, 0.5, 0, 1), and enable emission with color (1, 0.3, 0, 1).
```

```
Find all materials using the Standard shader and list them with their texture names.
```

---

## Assets & Project

```
List all prefabs in Assets/Prefabs/, then show the components on the first one.
```

```
Search for all .cs scripts that contain the word "Singleton" and list them.
```

```
Create a ScriptableObject of type PlayerData at Assets/Data/DefaultPlayer.asset.
```

---

## Terrain

```
Create a terrain named "Landscape" sized 500x100x500, add a grass texture layer, 
then raise a hill at the center using gaussian sculpting.
```

```
Export the terrain heightmap to Assets/Terrain/heightmap.png at full resolution.
```

---

## Batch Operations

```
Find all Animator components in the scene, get their controller names, 
and list any states that have no outgoing transitions.
```

```
Create 10 empty GameObjects named "Spawn_01" through "Spawn_10", 
evenly spaced in a circle of radius 15 centered at the origin.
```
