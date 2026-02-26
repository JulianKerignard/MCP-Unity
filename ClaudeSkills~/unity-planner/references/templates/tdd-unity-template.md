# Technical Design Document: {{PROJECT_NAME}}

**Version:** {{VERSION}} | **Date:** {{DATE}}
**Unity Version:** {{UNITY_VERSION}} | **Render Pipeline:** {{PIPELINE}}

---

## 1. Architecture Overview

### 1.1 Architecture Pattern
<!-- ECS / MonoBehaviour / Hybrid / Custom — with justification -->

### 1.2 Project Folder Structure
```
Assets/
├── Scripts/
│   ├── Core/           # Managers, singletons, events
│   ├── Gameplay/       # Player, enemies, items
│   ├── UI/             # UI controllers
│   ├── Data/           # ScriptableObjects
│   └── Editor/         # Editor tools
├── Prefabs/
├── Scenes/
├── Materials/
├── Textures/
├── Audio/
├── Animations/
└── Resources/          # or Addressables
```

### 1.3 Scene Organization
| Scene | Type | Purpose | Load Mode |
|-------|------|---------|-----------|

### 1.4 Assembly Definitions
| Assembly | Contains | References |
|----------|----------|------------|

---

## 2. Render Pipeline Configuration

### 2.1 Pipeline Choice Justification

### 2.2 Quality Settings per Platform
| Platform | Resolution | Quality Level | Target FPS |
|----------|-----------|--------------|------------|

### 2.3 Shader Strategy
<!-- Standard shaders, Shader Graph, custom shaders -->

### 2.4 Post-Processing Stack
| Effect | Settings | Performance Cost |
|--------|----------|-----------------|

### 2.5 Lighting Strategy
<!-- Realtime / Baked / Mixed — with justification -->

---

## 3. Core Systems Architecture

### 3.1 Game Manager Pattern
<!-- Singleton? ServiceLocator? DI? -->

### 3.2 Input System
<!-- New Input System / Legacy / Custom -->
- Input Actions Asset structure
- Control schemes (keyboard, gamepad, touch)

### 3.3 State Machine Architecture
<!-- Game states, player states, UI states -->
```
Boot → MainMenu → Loading → Gameplay → Pause → Results → MainMenu
```

### 3.4 Event System
<!-- C# events / UnityEvents / ScriptableObject events / custom EventBus -->

### 3.5 Object Pooling Strategy [INDIE+]

### 3.6 Save/Load System [INDIE+]
- Storage format (JSON / Binary / PlayerPrefs)
- What to persist

---

## 4. Component Design

### 4.1 Component Responsibility Map
| Component | Responsibility | Dependencies |
|-----------|---------------|--------------|

### 4.2 ScriptableObject Data Architecture
| SO Type | Purpose | Used By |
|---------|---------|---------|

### 4.3 Prefab Hierarchy Strategy
<!-- Nested prefabs, variants, instantiation patterns -->

### 4.4 Dependency Injection [AA+]

---

## 5. Performance Budget

### 5.1 FPS Targets
| Platform | Target FPS | Min Acceptable |
|----------|-----------|----------------|

### 5.2 Draw Call Budget
| Category | Budget | Notes |
|----------|--------|-------|

### 5.3 Memory Budget
| Category | Budget (MB) |
|----------|------------|

### 5.4 Asset Size Budgets
| Asset Type | Max Size | Format |
|-----------|----------|--------|

### 5.5 Physics Budget
- Max rigidbodies:
- Max colliders:
- Fixed timestep:

---

## 6. Third-Party Packages

| Package | Version | Purpose | Risk Level |
|---------|---------|---------|-----------|

---

## 7. Build & Platform Configuration [INDIE+]

### 7.1 Platform Targets
### 7.2 Build Pipeline
### 7.3 Addressable Assets Strategy [AA+]
### 7.4 CI/CD Pipeline [AA+]

---

## 8. MCP Unity Implementation Notes

### 8.1 Scene Setup Sequence
<!-- Reference helpers.md#H9 for tool sequences -->

### 8.2 Prefab Creation Pipeline

### 8.3 Script Generation Conventions
- Naming: PascalCase for classes, camelCase for fields
- Namespace: `{{PROJECT_NAME}}.{Module}`
- Base classes to extend

### 8.4 Tool Categories to Activate
<!-- Reference helpers.md#H12 -->
