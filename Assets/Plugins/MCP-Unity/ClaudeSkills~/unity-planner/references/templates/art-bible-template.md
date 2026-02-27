# Art Bible: {{PROJECT_NAME}}

**Version:** {{VERSION}} | **Date:** {{DATE}}
**Render Pipeline:** {{PIPELINE}} | **Art Style:** {{STYLE}}

---

## 1. Visual Identity

### 1.1 Art Style
<!-- Realistic / Stylized / Pixel Art / Low-Poly / Hand-Painted / etc. -->

### 1.2 Color Palette
| Role | Color | Hex | Usage |
|------|-------|-----|-------|
| Primary | | | |
| Secondary | | | |
| Accent | | | |
| UI Background | | | |
| UI Text | | | |
| Danger | | | |
| Success | | | |

### 1.3 Mood & Atmosphere Keywords

### 1.4 Visual References
| Reference | What to Take | What to Avoid |
|-----------|-------------|---------------|

---

## 2. Materials & Shaders

### 2.1 Material Naming Convention
`M_{Category}_{Name}_{Variant}` (e.g., `M_Environment_Grass_Dry`)

### 2.2 Master Materials
| Material | Shader | Key Properties | Usage |
|----------|--------|---------------|-------|

### 2.3 Texture Specifications
| Type | Resolution | Format | Compression |
|------|-----------|--------|-------------|
| Albedo | | PNG/TGA | |
| Normal | | PNG | |
| Mask | | PNG | |

### 2.4 Shader Strategy
<!-- Standard shaders, Shader Graph custom, special effects -->

---

## 3. Lighting Direction

### 3.1 Global Illumination Strategy
### 3.2 Color Temperature Guidelines
| Context | Temperature | Example |
|---------|------------|---------|

### 3.3 Shadow Settings
### 3.4 Post-Processing Profile
| Effect | Setting | Purpose |
|--------|---------|---------|

---

## 4. UI/UX Style

### 4.1 UI Kit Components
| Component | Style | Notes |
|-----------|-------|-------|

### 4.2 Typography
| Use | Font | Size | Weight |
|-----|------|------|--------|
| Title | | | |
| Body | | | |
| HUD | | | |
| Button | | | |

### 4.3 Icon Style
### 4.4 Animation / Transition Style
### 4.5 Canvas Strategy
<!-- Overlay vs Screen Space Camera vs World Space -->

---

## 5. Character Art Guidelines [if applicable]

### 5.1 Proportions & Style
### 5.2 Poly Budget
| LOD | Triangles | Distance |
|-----|-----------|----------|

### 5.3 Texture Sheets
### 5.4 Animation Style

---

## 6. Environment Art Guidelines

### 6.1 Modular Kit Rules
### 6.2 Prop Categories & Budgets
| Category | Max Tris | Max Texture |
|----------|----------|-------------|

### 6.3 LOD Strategy
### 6.4 Terrain Texturing Rules

---

## 7. VFX Guidelines [INDIE+]

### 7.1 Particle System Standards
### 7.2 Shader Effects
### 7.3 Screen Effects

---

## 8. Asset Pipeline

### 8.1 Import Settings Standards
| Asset Type | Settings |
|-----------|----------|

### 8.2 Folder Structure
### 8.3 Naming Conventions
| Type | Convention | Example |
|------|-----------|---------|
| Texture | `T_{Name}_{Type}` | `T_Grass_Albedo` |
| Material | `M_{Category}_{Name}` | `M_Environment_Grass` |
| Prefab | `PF_{Name}` | `PF_Tree_Oak` |
| Animation | `Anim_{Character}_{Action}` | `Anim_Player_Run` |
| Audio | `SFX_{Category}_{Name}` | `SFX_UI_Click` |
