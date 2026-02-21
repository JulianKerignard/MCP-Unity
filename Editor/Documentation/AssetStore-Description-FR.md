# Conductor — MCP AI Toolkit for Unity

**Connectez n'importe quelle IA à votre éditeur Unity. 164 outils. Zéro code.**

---

## Description

Conductor transforme votre éditeur Unity en un environnement entièrement pilotable par intelligence artificielle. Demandez à Claude, GPT-4, Gemini ou n'importe quel LLM de créer des GameObjects, configurer des composants, sculpter du terrain, monter des animators, builder votre projet — le tout en langage naturel.

Le plugin repose sur le Model Context Protocol (MCP), un standard ouvert développé par Anthropic. Il expose 164 outils couvrant la quasi-totalité de l'API Unity Editor, organisés en 13 catégories chargeables à la demande pour économiser les tokens.

Conductor inclut également un panneau de chat intégré directement dans Unity : 9 fournisseurs IA, streaming temps réel, exécution automatique des outils, et glisser-déposer d'assets comme contexte — sans quitter l'éditeur.

---

## Fonctionnalités principales

### 164 outils Unity pilotables par IA

Créez, modifiez, inspectez et supprimez des GameObjects, Composants, Scènes, Prefabs, Materials, Terrains, Animators, UI, Audio, NavMesh, Lighting — tout ce que vous faites manuellement dans l'éditeur, l'IA peut le faire pour vous.

### Chargement dynamique par catégories

Seuls les 47 outils essentiels (Core) sont chargés par défaut. Les 12 autres catégories (Terrain, Animator, Physics, Rendering...) se chargent à la demande, réduisant la consommation de tokens de 70%.

### Chat IA intégré dans Unity

9 fournisseurs supportés : Anthropic Claude, OpenAI, Google Gemini, DeepSeek, Groq, Mistral AI, Ollama, LM Studio, ou endpoint personnalisé. Streaming token par token, rendu Markdown, historique exportable.

### Exécution automatique des outils

L'IA appelle les outils, Conductor les exécute, renvoie le résultat, et l'IA continue — jusqu'à 10 itérations par réponse. Boucle autonome sans intervention.

### Confirmation des opérations destructives

Les actions irréversibles (suppression, écrasement de scripts, changement de plateforme, baking) demandent une confirmation via dialogue avant exécution. Pas de mauvaises surprises.

### Compatible avec tous les clients MCP

Claude Desktop, Claude Code CLI, Cursor, Windsurf, et tout client compatible MCP. Configuration générée automatiquement depuis le Setup Wizard.

### Cache intelligent côté serveur

Les lectures sont mises en cache avec TTL par catégorie (5s à 5min). Les écritures invalident automatiquement les entrées concernées. Moins d'allers-retours, réponses plus rapides.

### Authentification WebSocket

Secret partagé optionnel entre le bridge Node.js et l'éditeur Unity pour sécuriser la connexion.

---

## Les 164 outils par catégorie

| Catégorie | Outils | Exemples |
|-----------|--------|----------|
| **Core** | 47 | Créer/lister/modifier des GameObjects, ajouter/inspecter des composants, gérer les scènes, lire/écrire des scripts, cache mémoire, captures d'écran, sélection, workflows éditeur |
| **Asset** | 16 | Recherche, info, previews, import settings, prefabs, création/suppression/déplacement de fichiers et dossiers |
| **Material** | 3 | Get/set/create avec mapping automatique URP/HDRP |
| **UI** | 9 | Canvas, boutons, sliders, dropdowns, ScrollView, layout groups, RectTransform, ContentSizeFitter |
| **Animator** | 23 | Controllers, paramètres, layers, states, transitions, blend trees, clips, diagramme de flux, validation |
| **Terrain** | 17 | Sculpt, peinture de textures, peinture de chemins, arbres, détails, import/export heightmap, voisinage |
| **Physics** | 8 | Raycast, Rigidbody, Collider, PhysicsMaterial, NavMesh bake/clear/settings/static |
| **Audio** | 3 | AudioSource, AudioMixer création et inspection |
| **Rendering** | 13 | Camera, render-to-file, lighting bake (sync/async), occlusion, reflection probes, lightmap settings |
| **Build** | 6 | Build settings, gestion des scènes, changement de plateforme, Package Manager (ajout/suppression/liste) |
| **Settings** | 11 | Project settings, quality levels, physics layer collision matrix, tags et layers |
| **Input** | 3 | Input System actions et bindings |
| **Advanced** | 5 | Références SerializedField (simple + array), ScriptableObject CRUD |

---

## Architecture

```
Client IA  <-->  MCP (stdio)  <-->  Bridge Node.js  <-->  WebSocket  <-->  Unity Editor
```

Deux composants :

- **Plugin Unity** (C#) — Serveur WebSocket intégré à l'éditeur. Reçoit les commandes JSON-RPC 2.0 et les exécute sur le main thread Unity.
- **Bridge Node.js** (TypeScript) — Serveur MCP qui traduit les appels d'outils du client IA en requêtes WebSocket vers Unity, avec cache TTL intégré.

Le chat intégré fonctionne indépendamment du bridge — il appelle les outils directement via le registre interne, sans serveur externe.

---

## Installation rapide

1. Ajoutez le package via Package Manager (git URL ou disque local)
2. Lancez le Setup Wizard : **Tools > MCP Unity > Setup Wizard**
3. Le wizard vérifie Node.js, lance `npm install && npm run build`, et génère la configuration Claude
4. Démarrez le serveur dans la Server Window et commencez à discuter avec votre éditeur

---

## Configuration requise

- **Unity 6** (6000.0.0f1 ou supérieur)
- **Node.js 18+** (pour le bridge MCP)
- **Un client MCP** : Claude Desktop, Claude Code, Cursor, ou tout client compatible

---

## Fournisseurs IA supportés (chat intégré)

| Fournisseur | Modèles | Auth |
|-------------|---------|------|
| Anthropic Claude | Claude 4 Opus/Sonnet, 3.5 Sonnet | Clé API ou OAuth |
| OpenAI | GPT-4o, GPT-4, o1, o3 | Clé API |
| Google Gemini | Gemini 2.5 Pro/Flash | Clé API |
| DeepSeek | DeepSeek-V3, R1 | Clé API |
| Groq | Llama, Mixtral | Clé API |
| Mistral AI | Mistral Large, Medium | Clé API |
| Ollama | Tout modèle local | Local (gratuit) |
| LM Studio | Tout modèle local | Local (gratuit) |
| Custom | Tout endpoint compatible OpenAI | Configurable |

---

## Ce qui différencie Conductor

- **164 outils** — la couverture la plus complète de l'API Unity Editor disponible via MCP
- **Pas un wrapper de prompt** — chaque outil est une implémentation C# native qui manipule Unity directement via Undo, SerializedObject et les APIs éditeur officielles
- **Tout est annulable** — chaque opération passe par le système Undo de Unity (Ctrl+Z)
- **Extensible** — ajoutez vos propres outils comme partial classes de `McpUnityServer`
- **Open protocol** — MCP est un standard ouvert, pas un format propriétaire
- **Fonctionne hors-ligne** — avec Ollama ou LM Studio, aucune donnée ne quitte votre machine

---

## Support

- Documentation complète en français et anglais (incluse dans le package)
- 3 exemples inclus : Quick Start, Terrain Builder, Batch Workflow
- Repository GitHub pour les issues et contributions
