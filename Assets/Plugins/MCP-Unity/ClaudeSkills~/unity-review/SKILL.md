---
name: "Unity Review"
description: "Audit a Unity project's architecture. Use when the user says '/unity-review', 'review unity', 'audit architecture', 'check unity project', 'architecture review', or wants to analyze the technical quality of an existing Unity project."
---

# Unity Review — Architecture Audit

Audits a Unity project: scripts, components, missing references, performance, TDD conformity.

## Prerequisites

- MCP Unity connected
- Project with existing code to audit

## Step-by-Step Guide

### Step 1: Comprehensive Inspection via MCP Unity

```
unity_get_project_overview → project summary, packages, asset counts
unity_get_render_pipeline_info → pipeline details and quality settings
unity_list_project_scripts → all MonoBehaviours
unity_get_script_info { className } → public API of each key script
unity_read_script { filePath } → implementation of critical scripts
unity_find_gameobjects_by_component { componentType } → usage patterns
unity_find_missing_references → broken references
unity_get_console_logs { type: "error" } → current errors
unity_run_tests → test results
unity_list_gameobjects { outputMode: "tree" } → scene hierarchy
```

### Step 2: Analyze — Checklist

#### Architecture
- Conformity vs TDD if it exists, otherwise vs best practices
- Pattern consistency (Singleton, Observer, ECS, etc.)
- Assembly Definitions present and properly scoped?
- Namespace organization — clear responsibility boundaries?

#### Code Quality
- Script responsibilities — single-responsibility or bloated MonoBehaviours?
- Coupling between systems — tight or loosely coupled?
- Public API surface — minimal exposure?
- Magic numbers / hardcoded values vs ScriptableObject configs?

#### References & Assets
- Missing references — list and locate with `unity_find_missing_references`
- Unused assets or dead scripts?
- Prefab health — broken overrides?

#### Performance
- Excessive Rigidbodies or colliders without purpose?
- Materials — batching-friendly? Shared materials reused?
- Scene hierarchy depth (deep nesting hurts performance)
- Shader complexity vs target platform

#### Tests
- Test coverage — EditMode and PlayMode?
- Missing tests for critical systems?
- Tests actually passing?

### Step 3: Severity Assessment

Classify each finding:

| Severity | Definition | Action |
|----------|-----------|--------|
| **CRITICAL** | Blocks shipping or causes crashes/data loss | Fix immediately |
| **HIGH** | Significant performance or architecture issue | Fix before next milestone |
| **MEDIUM** | Code smell or maintainability concern | Fix when touching related code |
| **LOW** | Style issue or minor improvement | Optional, track for later |

### Step 4: Generate the Report

Structure:

```markdown
# Architecture Audit — {Project Name}
**Date:** {date}  |  **Auditor:** Claude  |  **Project Level:** {0-4}

## Executive Summary
Overall health: HEALTHY / NEEDS ATTENTION / CRITICAL
Key metrics: {scripts count}, {scenes count}, {test pass rate}, {missing refs count}

## Findings

### Critical
- [C1] {Description} — {File/GameObject} — {Remediation}

### High
- [H1] {Description} — {File/GameObject} — {Remediation}

### Medium
- [M1] ...

### Low
- [L1] ...

## Architecture Overview
- Pattern used: {pattern}
- Assembly structure: {description}
- Scene organization: {description}

## Missing References
{Output of unity_find_missing_references}

## Code Organization
| Namespace | Scripts | Responsibility | Issues |
|-----------|---------|---------------|--------|

## Performance
| Area | Status | Detail |
|------|--------|--------|

## Test Coverage
| Test Suite | Total | Pass | Fail | Skip |
|-----------|-------|------|------|------|

## Prioritized Recommendations
1. {Most impactful fix}
2. ...
3. ...
```

### Step 5: Save

`.claude/context/unity-planner/review-{project}-{date}.md`

## Common Unity Antipatterns to Check

| Antipattern | What to Look For | Fix |
|-------------|-----------------|-----|
| God MonoBehaviour | Scripts >300 lines with mixed concerns | Split into focused components |
| Update Abuse | Heavy logic in Update() every frame | Use events, coroutines, or timers |
| Find in Update | `Find()`, `GetComponent()` in Update | Cache references in Awake/Start |
| String Tags | `CompareTag("Enemy")` with magic strings | Use constants or enums |
| Public Fields Everywhere | All fields public for Inspector | Use `[SerializeField] private` |
| No Assembly Definitions | Everything in Assembly-CSharp | Create asmdef per module |
| Singleton Overuse | >5 Singletons managing global state | Use ScriptableObject events or DI |
| Deep Hierarchy | >5 levels of nesting in scene | Flatten, use prefab composition |
| Resources Folder | Large Resources/ folder | Migrate to Addressables |
| Missing Null Checks | No null checks on GetComponent results | Validate at boundaries |

## Agent

**Technical Director** — Expert in Unity architecture, performance, quality.
Details: see `unity-planner/references/agents.md` (Agent 2)
MCP workflows: W2, W6 in `mcp-unity-workflows.md`
