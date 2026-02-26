---
name: "Unity Review"
description: "Auditer l'architecture d'un projet Unity. Utiliser quand l'utilisateur dit '/unity-review', 'review unity', 'audit architecture', 'verifier le projet unity', 'architecture review', ou veut analyser la qualite technique d'un projet Unity existant."
---

# Unity Review — Architecture Audit

Audite un projet Unity : scripts, components, references manquantes, performance, conformite au TDD.

## Prerequisites

- MCP Unity connecte
- Projet avec du code existant a auditer

## Step-by-Step Guide

### Step 1 : Inspection comprehensive via MCP Unity

```
unity_list_project_scripts → tous les MonoBehaviours
unity_get_script_info { className } → API publique de chaque script cle
unity_read_script { filePath } → implementation des scripts critiques
unity_find_gameobjects_by_component { componentType } → patterns d'utilisation
unity_find_missing_references → references cassees
unity_get_console_logs { type: "error" } → erreurs actuelles
unity_run_tests → resultats des tests
```

### Step 2 : Analyser

- **Conformite architecture :** vs TDD si existant, sinon vs bonnes pratiques
- **References manquantes :** lister et localiser
- **Organisation scripts :** namespaces, responsabilites, couplage
- **Components :** composants trop gros, responsabilites melangees
- **Performance :** rigidbodies excessifs, materiaux non optimises
- **Tests :** couverture, tests manquants

### Step 3 : Generer le rapport

Structure :
1. Resume executif (etat global : sain / attention / critique)
2. References manquantes
3. Organisation du code
4. Couplage des components
5. Performance
6. Couverture de tests
7. Recommandations priorisees (critique / important / suggestion)

### Step 4 : Sauvegarder

`.claude/context/unity-planner/review-{projet}-{date}.md`

## Agent

**Technical Director** — Expert en architecture Unity, performance, qualite.
Details : `~/.claude/skills/unity-planner/references/agents.md` (Agent 2)
MCP workflows : W2, W6 dans `mcp-unity-workflows.md`
