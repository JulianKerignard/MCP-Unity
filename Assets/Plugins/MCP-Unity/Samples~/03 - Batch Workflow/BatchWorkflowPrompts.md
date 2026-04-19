# Batch Workflow — Prompt Templates

Copy these prompts directly into your AI editor. Each prompt chains multiple MCP tools in a single request.

---

## 1. Full Scene Audit

```
Perform a complete audit of the current Unity scene:

1. Get the scene hierarchy (full depth)
2. For each GameObject with a Renderer, check if it has a Collider — list all that don't
3. Find all Light components and list their type, intensity, and shadow mode
4. Find all Cameras and report their culling masks and render textures
5. Check for any GameObjects with missing script components (MissingMonoBehaviour)
6. Take a Scene view screenshot for reference

Return a structured audit report.
```

---

## 2. Prefab Variant Generator

```
I want 4 variants of my "Enemy_Base" prefab with different difficulty settings.
Find the prefab, then create 4 instances, configure them as follows, and save each as a new prefab in Assets/Prefabs/Enemies/:

Variant 1 — "Enemy_Easy": health=50, speed=3, damage=5
Variant 2 — "Enemy_Normal": health=100, speed=5, damage=10  
Variant 3 — "Enemy_Hard": health=200, speed=8, damage=20
Variant 4 — "Enemy_Elite": health=500, speed=12, damage=40, scale=(1.5, 1.5, 1.5)

The component that holds these values is named "EnemyStats".
After creating all prefabs, list them to confirm.
```

---

## 3. Level Layout Generator

```
Create a simple top-down level layout in the current scene:

1. Create a parent empty GameObject named "Level_01"
2. Create a plane named "Floor" (scale 20x1x20) as child, with a grey material
3. Create 4 wall planes (scale 1x2x20) positioned at N/S/E/W edges, named "Wall_N", "Wall_S", "Wall_E", "Wall_W"
4. Create 8 cube obstacles named "Obstacle_01" through "Obstacle_08", randomly arranged in the interior (roughly at positions like (3,0.5,3), (-3,0.5,3), etc.), each with scale (1,1,1)
5. Create 4 empty GameObjects named "SpawnPoint_01" through "SpawnPoint_04" at the 4 quadrant centers (roughly ±6 on X and Z)
6. Parent everything under "Level_01"
7. Take a top-down screenshot (orthographic if possible)
```

---

## 4. Animator Full Setup

```
Set up a complete animator controller for a character named "Player".

1. Create an AnimatorController at Assets/Animation/PlayerController.controller
2. Add these states: "Idle", "Walk", "Run", "Jump", "Attack", "Die"
3. Add parameters: 
   - "Speed" (Float)
   - "IsJumping" (Bool)
   - "IsAttacking" (Bool)
   - "IsDead" (Bool)
4. Add transitions:
   - Idle → Walk: Speed > 0.1
   - Walk → Idle: Speed < 0.1
   - Walk → Run: Speed > 5.0
   - Run → Walk: Speed < 5.0
   - Any State → Jump: IsJumping = true
   - Any State → Attack: IsAttacking = true
   - Any State → Die: IsDead = true
5. Assign the controller to the Animator on the "Player" GameObject in the scene
6. Get the controller info to verify all states and transitions
```

---

## 5. Asset Cleanup & Organization

```
Perform a cleanup of the Assets/Textures/ folder:

1. Find all textures in Assets/Textures/
2. Group them by resolution (< 512px, 512-1024px, > 1024px)
3. For textures larger than 2048px, get their import settings and report which ones don't have compression enabled
4. For any .png textures with "normal" or "Normal" in their name, check if their TextureType is set to Normal Map — list any that aren't
5. List all textures that have no references in any scene or prefab (potential orphans)

Return a report with actionable recommendations.
```

---

## 6. Spawn System Builder

```
Create a spawn system in the scene:

1. Create an empty GameObject named "SpawnSystem" as the root
2. Create 10 child GameObjects named "Spawn_01" through "Spawn_10"
3. Place them evenly distributed in a circle:
   - Radius: 20 units from origin
   - Evenly spaced (every 36 degrees)
   - Y position: 0.1 (slightly above ground)
4. Add a tag "SpawnPoint" to all of them (create the tag if it doesn't exist)
5. Add a SphereCollider (radius 0.5, isTrigger=true) to each spawn point
6. Get all GameObjects tagged "SpawnPoint" to confirm all 10 were created correctly
7. Take a top-down screenshot to visualize the layout
```

---

## 7. Material Audit & Fix

```
Audit all materials in the project:

1. Find all materials using the legacy "Standard" shader
2. For each, check if it has an albedo texture assigned
3. List materials that:
   a. Use Standard shader but have no textures at all (candidates for deletion)
   b. Have "_Metallic" texture assigned but metallic value is 0 (potential misconfiguration)
   c. Have emission enabled but emission color is black (wasted feature)
4. For all materials using "Standard" shader, report how many unique textures they reference

Provide a prioritized list of fixes.
```
