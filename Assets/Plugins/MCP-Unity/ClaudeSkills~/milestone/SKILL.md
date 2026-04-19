---
name: "Milestone"
description: "Plan milestones for a Unity project. Use when the user says '/milestone', 'milestones', 'game planning', 'vertical slice', 'alpha beta gold', 'game roadmap', or wants to define development stages with dates and criteria."
---

# Milestone — Milestone Plan

Defines the game's milestones: Prototype, Vertical Slice, Alpha, Beta, Gold.

## Prerequisites

- GDD available (for feature pillars)
- Project level known

## Step-by-Step Guide

### Step 1: Load Context

Read the GDD to identify feature pillars and priorities.
Read `unity-planner-status.yaml` for the project level.

Optionally inspect project state:
```
unity_get_project_overview → current asset/script counts
unity_list_scenes_in_project → scene count
unity_run_tests → current test pass rate
```

### Step 2: Define Milestones by Level

| Level | Milestones |
|-------|-----------|
| **0 (Proto)** | Prototype only |
| **1 (Jam)** | Prototype → Submission |
| **2 (Indie)** | Prototype → Vertical Slice → Alpha → Beta → Gold |
| **3-4 (AA/AAA)** | + Pre-production, Release Candidate |

### Step 3: For Each Milestone — Define Content

#### 3a. Definition of Done (Measurable Criteria)

Each milestone needs concrete, testable criteria. Examples:

**Prototype:**
- [ ] Core loop playable (move, interact, win/lose)
- [ ] Single test scene
- [ ] No art needed — placeholder cubes/capsules OK
- [ ] Runs without crashes for 5 minutes

**Vertical Slice:**
- [ ] One complete level from start to finish
- [ ] Core mechanics polished (feels good to play)
- [ ] Placeholder UI for all screens
- [ ] Target framerate on target platform
- [ ] Basic audio (SFX + placeholder music)

**Alpha:**
- [ ] All core features implemented (P0 + P1)
- [ ] All levels blocked out (graybox)
- [ ] Save/load working
- [ ] All UI screens functional
- [ ] Test coverage > 60%

**Beta:**
- [ ] All content integrated (final art, audio, levels)
- [ ] Feature complete — no new features
- [ ] Performance optimized for all target platforms
- [ ] Localization ready
- [ ] All known critical bugs fixed

**Gold:**
- [ ] Zero critical/high bugs
- [ ] Platform certification requirements met
- [ ] Build size within limits
- [ ] All achievements/trophies working
- [ ] Launch build signed and tested

#### 3b. Feature Scope from GDD Pillars

Map priorities to milestones:

| Priority | Definition | Milestone |
|----------|-----------|-----------|
| **P0** (must-have) | Core loop, essential mechanics | Vertical Slice |
| **P1** (should-have) | Key systems, basic content | Alpha |
| **P2** (nice-to-have) | Polish, extra content, QoL | Beta |
| **P3** (cut if needed) | Stretch goals | Post-launch or cut |

#### 3c. Risk Matrix

For each milestone, assess risks:

| Risk | Likelihood (1-5) | Impact (1-5) | Score | Mitigation |
|------|:-:|:-:|:-:|------------|
| Tech: New framework | 3 | 4 | 12 | Prototype early, have fallback |
| Art: Style undefined | 2 | 3 | 6 | Lock art bible before Alpha |
| Scope: Feature creep | 4 | 4 | 16 | Strict P0/P1 discipline |
| Team: Key person leaves | 2 | 5 | 10 | Document everything, pair work |
| Performance: Frame drops | 3 | 3 | 9 | Budget per feature, profile monthly |

**Score = Likelihood x Impact**. Prioritize mitigation for scores >= 9.

#### 3d. Timeline Estimation

Ask the user for:
- **Team size** (solo, 2-5, 5-15, 15+)
- **Work schedule** (full-time, part-time, weekends only)
- **Target deadline** (if any)

Estimation guidelines by team/level:

| Level | Solo (full-time) | Small team (3-5) |
|-------|-----------------|-------------------|
| **Proto** | 1-2 weeks | 3-5 days |
| **Jam** | 2-4 weeks | 1-2 weeks |
| **Indie** | 6-18 months | 3-12 months |
| **AA** | N/A | 12-24 months |

**Rule of thumb:** Take your estimate, multiply by 1.5 (optimism bias). Add 20% buffer per milestone for unknowns.

### Step 4: Generate Timeline

```
Proto ──[2 weeks]──► VS ──[6 weeks]──► Alpha ──[8 weeks]──► Beta ──[4 weeks]──► Gold
  │                    │                   │                    │                    │
  P0 core loop         P0 complete         P0+P1 complete      All content          Ship
  Placeholders         1 polished level    All levels blocked   Feature freeze       Bug-free
```

### Step 5: Save

`.claude/context/unity-planner/milestones-{project}.md`
Template: see `unity-planner/references/templates/milestone-template.md`

## Agent

**Producer** — Expert in milestones, scope, risks, project management.
Details: see `unity-planner/references/agents.md` (Agent 4)
