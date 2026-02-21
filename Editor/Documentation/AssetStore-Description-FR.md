# Conductor — MCP AI Toolkit for Unity

# Soumission Asset Store — Champs du formulaire

---
---
---

## 1. SUMMARY (10-200 caractères)

---

Pilotez Unity par IA : 164 outils MCP, chat intégré multi-provider, zéro code. Compatible Claude, GPT-4, Gemini, Ollama.

---
---
---

## 2. DESCRIPTION

---

Conductor transforme votre éditeur Unity en un environnement entièrement pilotable par intelligence artificielle. Demandez à Claude, GPT-4, Gemini ou n'importe quel LLM de créer des GameObjects, configurer des composants, sculpter du terrain, monter des animators, builder votre projet — le tout en langage naturel.

Le plugin repose sur le Model Context Protocol (MCP), un standard ouvert développé par Anthropic. Il expose 164 outils couvrant la quasi-totalité de l'API Unity Editor, organisés en 13 catégories chargeables à la demande pour économiser jusqu'à 70% de tokens.

Conductor inclut également un panneau de chat intégré directement dans Unity : 9 fournisseurs IA (Claude, GPT-4, Gemini, DeepSeek, Groq, Mistral, Ollama, LM Studio, Custom), streaming temps réel token par token, exécution automatique des outils en boucle (jusqu'à 10 itérations par réponse), et glisser-déposer d'assets ou de GameObjects comme contexte — sans jamais quitter l'éditeur.

Chaque opération passe par le système Undo de Unity : tout est annulable avec Ctrl+Z. Les actions destructives (suppression, écrasement de scripts, baking, changement de plateforme) demandent une confirmation avant exécution.

Le bridge Node.js inclut un cache intelligent côté serveur avec TTL par catégorie et invalidation automatique sur écriture, réduisant les allers-retours avec Unity pour des réponses plus rapides.

Compatible avec Claude Desktop, Claude Code CLI, Cursor, Windsurf, et tout client MCP. Un Setup Wizard intégré vérifie les prérequis, build le serveur, et génère la configuration automatiquement.

Fonctionne aussi hors-ligne avec Ollama ou LM Studio — aucune donnée ne quitte votre machine.

---
---
---

## 3. TECHNICAL DETAILS — Key Features

---

- 164 outils Unity pilotables par IA couvrant 13 catégories : Core (47), Asset (16), Animator (23), Terrain (17), Rendering (13), Settings (11), UI (9), Physics (8), Build (6), Advanced (5), Material (3), Audio (3), Input (3)

- Chargement dynamique par catégories : seuls les 47 outils Core sont chargés par défaut, les autres se chargent à la demande via unity_enable_tool_category, réduisant la consommation de tokens de 70%

- Chat IA intégré dans l'éditeur Unity avec 9 fournisseurs supportés : Anthropic Claude, OpenAI, Google Gemini, DeepSeek, Groq, Mistral AI, Ollama, LM Studio, endpoint personnalisé

- Streaming temps réel token par token avec rendu Markdown, historique exportable (Markdown, JSON, texte brut, presse-papier)

- Boucle d'exécution automatique : l'IA appelle les outils, Conductor exécute, renvoie le résultat, l'IA continue — jusqu'à 10 itérations par réponse sans intervention

- Confirmation obligatoire pour 17 opérations destructives (suppression, écrasement de scripts, changement de plateforme, baking) via dialogue Unity

- Toutes les opérations passent par le système Undo de Unity (Ctrl+Z) — chaque action est annulable

- Architecture two-tier : Plugin C# (serveur WebSocket dans l'éditeur, exécution main thread) + Bridge Node.js (serveur MCP stdio, cache TTL, traduction JSON-RPC 2.0)

- Cache serveur intelligent avec TTL par catégorie (editorState: 5s, hierarchy: 30s, components: 1min, assets: 5min) et invalidation automatique sur écriture

- Authentification WebSocket par secret partagé optionnel entre le bridge et l'éditeur

- Compatible avec tous les clients MCP : Claude Desktop, Claude Code CLI, Cursor, Windsurf

- Setup Wizard intégré : vérification Node.js, npm install, build automatique, génération de la configuration Claude

- Glisser-déposer d'assets et de GameObjects dans le chat comme contexte pour l'IA

- OAuth 2.0 + PKCE pour l'authentification Anthropic (pas de clé API requise)

- Fonctionne hors-ligne avec Ollama ou LM Studio — aucune donnée ne quitte la machine

- Mapping automatique URP/HDRP pour les materials (détection du render pipeline actif)

- 3 exemples inclus : Quick Start, Terrain Builder, Batch Workflow

- Documentation complète en français et en anglais incluse dans le package

- Extensible : ajoutez vos propres outils comme partial classes de McpUnityServer

- Requiert Unity 6 (6000.0.0f1+) et Node.js 18+
