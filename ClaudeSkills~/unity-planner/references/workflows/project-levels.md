# Unity Planner — Project Levels

Scale-adaptive system that adjusts document depth and workflow based on project complexity.

---

## Level 0: Prototype

**Timeline:** 1-3 days | **Team:** 1 person

**Goal:** Validate a single mechanic. Answer: "Is this fun?"

**Documents to generate:**
- GDD: Sections 1-2 only (Overview + Core Gameplay)
- TDD: Sections 1, 3.1-3.3 only (Architecture + 3 core systems)

**Milestones:** Prototype only

**MCP Workflow:** Minimal
- `unity_get_editor_state` → check project
- Direct to `/unity-story` for rapid implementation

---

## Level 1: Game Jam

**Timeline:** 2-7 days | **Team:** 1-4 people

**Goal:** Ship a playable game within a tight deadline.

**Documents to generate:**
- GDD: Sections 1-2, 5 (Overview + Core Gameplay + UI/UX Flow)
- TDD: Sections 1-4 (Architecture through Component Design, skip performance budget)

**Milestones:** Prototype → Submission

**MCP Workflow:** Quick setup
- `unity_get_project_overview` → understand starting point
- Focus on `/gdd` (compact) → `/unity-story` for speed

**Special rules:**
- Skip `/art-direction` (no time for art bible)
- Skip `/level-design` (usually 1-3 scenes)
- Prioritize speed over documentation completeness

---

## Level 2: Indie

**Timeline:** 3-18 months | **Team:** 1-10 people

**Goal:** Ship a polished, commercial-quality game.

**Documents to generate:**
- GDD: Sections 1-5, 8 (all except Multiplayer, Monetization)
- TDD: Sections 1-6 (all except CI/CD)
- Level Design: Yes, for each major level
- Art Bible: Recommended
- Milestones: Prototype → Vertical Slice → Alpha → Beta → Gold

**MCP Workflow:** Full planning
- Complete project inspection (helpers.md#H1)
- All 6 agents active
- Status YAML actively tracked

**Recommended command sequence:**
1. `/unity-plan` → setup
2. `/gdd` → game design
3. `/tdd-unity` → architecture
4. `/art-direction` → visual style
5. `/milestone` → timeline
6. `/level-design` → per level
7. `/unity-story` → per feature
8. `/unity-review` → periodic audits

---

## Level 3: AA

**Timeline:** 1-3 years | **Team:** 10-50 people

**Goal:** High-quality game with broader scope and polish.

**Documents to generate:**
- GDD: ALL sections including Multiplayer, Monetization if applicable + appendices
- TDD: ALL sections including CI/CD, Addressables
- Level Design: Detailed per level with terrain specs
- Art Bible: Required with full pipeline specs
- Milestones: Full including Release Candidate

**MCP Workflow:** Comprehensive
- All workflows including build verification (helpers.md#H10)
- Regular `/unity-review` audits
- Assembly definition strategy required

**Additional considerations:**
- Content pipeline documentation
- Addressable assets strategy
- Platform certification requirements
- Performance profiling plan
- QA process definition

---

## Level 4: AAA

**Timeline:** 2-5 years | **Team:** 50+ people

**Goal:** Top-tier production values across all aspects.

**Documents to generate:**
- GDD: ALL + appendices (Mechanic specs, Economy spreadsheet, Narrative bible, Accessibility plan)
- TDD: ALL + CI/CD + distributed builds + plugin architecture
- Level Design: Exhaustive per level with biome specs
- Art Bible: Comprehensive with LOD budgets, character sheets, VFX guidelines
- Milestones: Full + post-launch roadmap

**MCP Workflow:** Enterprise
- All workflows at maximum depth
- Multiple `/unity-review` passes
- Integration testing plans
- Localization strategy

**Additional considerations:**
- Custom engine extensions
- Streaming/LOD strategy
- Network architecture (if multiplayer)
- Analytics and telemetry plan
- Live operations plan
- Legal/compliance review

---

## Auto-Detection Heuristic

When `/unity-plan` runs H1 (Project State Detection), use these signals:

| Signal | Method | Level 0 | Level 1 | Level 2 | Level 3 | Level 4 |
|--------|--------|---------|---------|---------|---------|---------|
| Scene count | `unity_list_scenes_in_project` | 1 | 1-3 | 3-15 | 15-50 | 50+ |
| Total assets | `unity_get_project_overview` | <50 | <200 | <1000 | <5000 | 5000+ |
| Packages | `unity_list_packages` | <5 | <10 | <20 | <30 | 30+ |
| Scripts | `unity_list_project_scripts` | <5 | <20 | <100 | <500 | 500+ |
| Has Addressables | check packages | No | No | Maybe | Likely | Yes |
| Has CI/CD | check project root | No | No | Maybe | Yes | Yes |

**For new/empty projects:** Ask user directly. The heuristic only works for existing projects.

**Always confirm** detected level with user — the heuristic provides a suggestion, not a mandate.

**Override:** User can set level explicitly in status YAML at any time.
