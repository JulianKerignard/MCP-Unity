# MCP Unity — Documentation

**Version** : 1.0.0 | **Protocole MCP** : 2024-11-05 | **Unity** : 6000.0+ | **Licence** : MIT

---

## Table des matieres

1. [Presentation](#presentation)
2. [Architecture](#architecture)
3. [Installation](#installation)
4. [Assistant de configuration](#assistant-de-configuration)
5. [Configuration](#configuration)
6. [Reference des outils (164 outils)](#reference-des-outils)
7. [Ressources](#ressources)
8. [Fenetre editeur](#fenetre-editeur)
9. [Systeme de chat integre](#systeme-de-chat-integre)
10. [Cache du bridge](#cache-du-bridge)
11. [Guide de developpement](#guide-de-developpement)
12. [Depannage](#depannage)

---

## Presentation

**MCP Unity** est un plugin Unity Editor implementant le [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) pour connecter n'importe quel assistant IA — Claude, GPT-4, Gemini, Ollama et plus — a vos projets Unity.

Il expose **164 outils** repartis en **13 categories** pour un controle complet de l'editeur Unity : manipulation de scene, gestion d'assets, creation d'UI, animation, physique, terrain, baking, scripting et plus encore.

Il inclut egalement un **panneau de chat IA multi-provider integre** directement dans l'editeur Unity, avec execution d'outils en temps reel et reponses en streaming.

### Fonctionnalites principales

- **164 outils Unity** couvrant toutes les operations majeures
- **Chargement dynamique par categorie** — 45 outils de base (+ 2 meta-outils) charges initialement, les autres a la demande (economie de tokens)
- **Confirmation des operations destructives** — dialogue de confirmation dans le chat pour les operations dangereuses
- **Authentification WebSocket** — secret partage optionnel pour securiser la connexion bridge/Unity
- **Systeme de chat integre** — 9 providers IA avec streaming en temps reel
- **Support multi-provider** — Claude, GPT-4, Gemini, DeepSeek, Groq, Mistral, Ollama, LM Studio
- **Authentification OAuth PKCE** pour Anthropic
- **Glisser-deposer** de references d'assets/GameObjects dans le chat
- **Cache TTL cote bridge** pour minimiser les appels Unity redondants
- **Validation de securite des chemins** — bloque les attaques de traversee de repertoires
- **File de messages thread-safe** pour l'acces API Unity sur le thread principal
- **Moniteur de requetes** avec chronometrage et suivi d'erreurs

---

## Architecture

```
Client IA (Claude, etc.)
    |  MCP via stdio
    v
Bridge Node.js (Server~/)
    |  JSON-RPC 2.0 via WebSocket
    v
Plugin Unity Editor (Editor/)
    |
    +- Execution des outils (thread principal)
    +- Panneau Chat ---- HTTP/SSE direct -----> APIs LLM
```

### Composants

| Composant | Langage | Role |
|-----------|---------|------|
| **Bridge Node.js** (`Server~/src/`) | TypeScript | Serveur MCP stdio, transmet les requetes a Unity via WebSocket, cache TTL |
| **Plugin Unity** (`Editor/McpServer/`) | C# | Serveur WebSocket dans Unity, execute les outils sur le thread principal |
| **Systeme de chat** (`Editor/McpServer/Chat/`) | C# | Panneau de chat LLM multi-provider integre |

### Flux de communication

1. Le client IA envoie une requete MCP via **stdio** au bridge Node.js
2. Le bridge transmet via **WebSocket** (JSON-RPC 2.0) vers l'editeur Unity
3. Unity met le message en file — traite sur le **thread principal** (requis pour l'API Unity)
4. Unity retourne la reponse via WebSocket au bridge
5. Le bridge retourne la reponse MCP au client IA via stdio

### Modele de threading

Les messages WebSocket arrivent sur des threads secondaires. `McpBehavior.OnMessage` les met en file dans une `ConcurrentQueue<QueuedMessage>`. `EditorApplication.update` traite jusqu'a **10 messages par frame** sur le thread principal.

---

## Installation

### Prerequis

| Requis | Version |
|--------|---------|
| Unity | 6000.0+ (Unity 6) |
| Node.js | 18+ |
| npm | 9+ |

### Option A — Unity Package Manager (URL Git)

Dans Unity : **Window > Package Manager > + > Add package from git URL** :

```
https://github.com/JulianKerignard/mcp-unity.git
```

### Option B — Installation locale

Copier le package dans le dossier `Packages/` de votre projet :

```
VotreProjet/
+-- Packages/
    +-- com.juliank.mcp-unity/
        +-- Editor/
        +-- Plugins/
        +-- Server~/
        +-- package.json
```

### Compiler le bridge Node.js

```bash
cd Packages/com.juliank.mcp-unity/Server~/
npm install
npm run build
```

> **Astuce** : Utilisez l'**Assistant de configuration** (`Tools > MCP Unity > Setup Wizard`) pour effectuer cette etape automatiquement.

### Configurer Claude Desktop

Ajouter dans `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) :

```json
{
  "mcpServers": {
    "mcp-unity": {
      "command": "node",
      "args": ["/chemin/absolu/vers/Server~/build/index.js"],
      "env": { "UNITY_PORT": "8090" }
    }
  }
}
```

### Configurer Claude Code CLI

```bash
claude mcp add mcp-unity -- node /chemin/absolu/vers/Server~/build/index.js
```

### Variables d'environnement

| Variable | Defaut | Description |
|----------|--------|-------------|
| `UNITY_HOST` | `localhost` | Hote WebSocket Unity |
| `UNITY_PORT` | `8090` | Port WebSocket Unity |
| `UNITY_SECRET` | — | Secret partage pour l'authentification WebSocket (optionnel) |
| `DEBUG` | `false` | Activer les logs de debug (`true` ou `1`) |
| `REQUEST_TIMEOUT` | `10000` | Timeout des requetes en ms |
| `RECONNECT_INTERVAL` | `3000` | Intervalle de reconnexion en ms |
| `MAX_RECONNECT_ATTEMPTS` | `3` | Nombre max de tentatives de reconnexion |

### Demarrer le serveur

Dans Unity : **Tools > MCP Unity > Server Window** → cliquer **Start Server**.

L'indicateur de statut devient **vert** quand le serveur est pret.

---

## Assistant de configuration

L'**Assistant de configuration** s'ouvre automatiquement lors du premier import du package. Il guide l'utilisateur a travers :

1. **Verification Node.js** — verifie que Node.js 18+ est installe
2. **Compilation du bridge** — execute `npm install && npm run build` automatiquement
3. **Config Claude** — genere et copie la configuration JSON dans le presse-papier

Accessible a tout moment : **Tools > MCP Unity > Setup Wizard**

---

## Configuration

Les parametres sont stockes dans `ProjectSettings/McpUnitySettings.json` et sauvegardes automatiquement a chaque modification.

| Parametre | Defaut | Description |
|-----------|--------|-------------|
| Port | 8090 | Port du serveur WebSocket |
| AutoStartServer | true | Demarrer le serveur au chargement de l'editeur |
| ShowNotifications | true | Afficher les popups de notification |
| RequestTimeoutMs | 30000 | Timeout des requetes (ms) |
| LogToConsole | true | Rediriger les logs vers la console Unity |
| LogToFile | false | Ecrire les logs dans un fichier |
| MinimumLogLevel | Info | Niveau minimum de log |
| MaxLogEntries | 500 | Nombre max d'entrees dans le buffer de logs |
| UseCustomServerPath | false | Utiliser un chemin custom pour le bridge |
| CustomServerPath | — | Chemin custom vers `index.js` |

### Authentification WebSocket (optionnel)

Pour securiser la connexion entre le bridge Node.js et Unity, un **secret partage** peut etre configure :

1. Dans Unity : **Tools > MCP Unity > Server Window > Settings > Shared Secret** — definir un secret
2. Definir la variable d'environnement `UNITY_SECRET` avec la meme valeur dans votre config Claude

Le bridge envoie le secret comme parametre `?secret=` sur la connexion WebSocket. Unity le valide a la connexion et rejette les clients non autorises.

Les configs auto-generees (via l'onglet Claude Config ou le Setup Wizard) incluent automatiquement `UNITY_SECRET` si un secret est configure.

---

## Reference des outils

### Chargement dynamique

MCP Unity utilise le **chargement par categorie** pour minimiser l'utilisation des tokens :

- **45 outils de base** (+ 2 meta-outils) toujours disponibles
- **117 outils supplementaires** charges a la demande par categorie
- Utiliser `unity_list_tool_categories` pour voir les categories disponibles
- Utiliser `unity_enable_tool_category` pour charger une categorie

---

### CORE — Toujours actifs (45 outils + 2 meta)

#### Meta-outils

| Outil | Description |
|-------|-------------|
| `unity_list_tool_categories` | Lister toutes les categories avec comptage et statut |
| `unity_enable_tool_category` | Activer une ou plusieurs categories |

#### Etat editeur et selection

| Outil | Description |
|-------|-------------|
| `unity_get_editor_state` | Etat courant de l'editeur (mode Play, compilation, selection) |
| `unity_get_selection` | Obtenir les objets selectionnes |
| `unity_set_selection` | Definir la selection (GameObjects ou assets) |
| `unity_focus_gameobject` | Cadrer la vue Scene sur un GameObject |
| `unity_get_project_overview` | Vue d'ensemble du projet (stats, scenes, packages) |
| `unity_find_missing_references` | Trouver les references manquantes dans la scene |
| `unity_get_console_logs` | Obtenir les logs console Unity avec filtrage |
| `unity_clear_console` | Vider la console Unity |
| `unity_take_screenshot` | Capturer la vue Scene/Game (utiliser `returnBase64=false` pour economiser les tokens) |

#### Workflow editeur

| Outil | Description |
|-------|-------------|
| `unity_execute_menu_item` | Executer un element de menu Unity (liste blanche) |
| `unity_run_tests` | Lancer les tests Unity Test Framework (EditMode ou PlayMode) |
| `unity_undo` | Annuler ou refaire des operations |
| `unity_refresh_and_compile` | Rafraichir les assets et declencher la recompilation des scripts |

#### GameObject

| Outil | Description |
|-------|-------------|
| `unity_list_gameobjects` | Lister la hierarchie — utiliser `outputMode='tree'` (90% de tokens en moins) |
| `unity_create_gameobject` | Creer un GameObject (primitif optionnel : Cube, Sphere, etc.) |
| `unity_create_gameobject_batch` | Creer plusieurs GameObjects en un appel avec groupement Undo |
| `unity_delete_gameobject` | Supprimer un GameObject (trouve aussi les inactifs) |
| `unity_rename_gameobject` | Renommer un GameObject |
| `unity_set_parent` | Re-parenter un GameObject (`worldPositionStays` supporte) |
| `unity_duplicate_gameobject` | Dupliquer un GameObject |
| `unity_move_gameobject` | Changer l'index dans les enfants du parent |
| `unity_set_transform` | Definir la position, rotation et/ou echelle d'un GameObject |
| `unity_get_gameobject` | Obtenir les details complets d'un GameObject (composants, enfants, transform) |
| `unity_find_gameobjects_by_component` | Trouver tous les GameObjects avec un composant specifique |
| `unity_set_gameobject_active` | Activer ou desactiver un GameObject |

#### Composant

| Outil | Description |
|-------|-------------|
| `unity_get_component` | Lire les proprietes d'un composant via reflection |
| `unity_add_component` | Ajouter un composant avec proprietes initiales optionnelles |
| `unity_modify_component_batch` | Modifier des composants sur plusieurs GameObjects en un appel |
| `unity_get_gameobject_components` | Lister tous les composants d'un GameObject |
| `unity_set_component_enabled` | Activer ou desactiver un composant |
| `unity_list_project_scripts` | Lister tous les scripts MonoBehaviour du projet |
| `unity_remove_component` | Supprimer un composant (Transform protege) |

#### Scene

| Outil | Description |
|-------|-------------|
| `unity_get_scene_info` | Informations sur la scene courante |
| `unity_list_scenes_in_project` | Lister toutes les scenes du projet |
| `unity_load_scene` | Charger une scene (Single ou Additive) |
| `unity_save_scene` | Sauvegarder la scene courante ou toutes les scenes ouvertes |
| `unity_create_scene` | Creer une nouvelle scene (sauvegarde auto dans Assets/Scenes/) |

#### Script

| Outil | Description |
|-------|-------------|
| `unity_create_script` | Creer un script C# depuis un template (MonoBehaviour, ScriptableObject, EditorWindow) |
| `unity_read_script` | Lire le contenu d'un fichier script C# |
| `unity_get_script_info` | Obtenir les infos de reflection d'un type (champs, proprietes, methodes) |
| `unity_write_script` | Ecrire un script C# complet (avec sauvegarde) |
| `unity_update_script` | Rechercher-remplacer une section unique dans un script (avec sauvegarde) |

#### Cache memoire

| Outil | Description |
|-------|-------------|
| `unity_memory_get` | Obtenir les donnees en cache (assets, scenes, hierarchie, operations) |
| `unity_memory_refresh` | Rafraichir une section du cache |
| `unity_memory_clear` | Vider les sections du cache |

---

### ASSET (16 outils)

| Outil | Description |
|-------|-------------|
| `unity_search_assets` | Rechercher des assets avec la syntaxe Unity (`t:Texture`, `l:Label`, patterns) |
| `unity_get_asset_info` | Obtenir les metadonnees detaillees d'un asset (GUID, type, taille, dependances) |
| `unity_delete_asset` | Supprimer un asset du projet |
| `unity_create_folder` | Creer un dossier dans le projet |
| `unity_move_asset` | Deplacer ou renommer un asset |
| `unity_copy_asset` | Copier un asset vers un nouveau chemin |
| `unity_list_folders` | Lister la structure de dossiers du projet |
| `unity_list_folder_contents` | Lister les assets d'un dossier avec filtre de type optionnel |
| `unity_get_asset_preview` | Obtenir l'apercu d'un asset (utiliser `size='small'` pour economiser les tokens) |
| `unity_get_import_settings` | Obtenir les parametres d'import d'un asset |
| `unity_set_import_settings` | Modifier les parametres d'import |
| `unity_instantiate_prefab` | Instancier un prefab dans la scene |
| `unity_create_prefab` | Creer un prefab a partir d'un GameObject de scene |
| `unity_unpack_prefab` | Depaqueter une instance de prefab |
| `unity_apply_prefab_overrides` | Appliquer les overrides de l'instance vers le prefab source |
| `unity_revert_prefab_overrides` | Revenir aux valeurs du prefab source |

---

### MATERIAL (3 outils)

| Outil | Description |
|-------|-------------|
| `unity_get_material` | Obtenir les proprietes d'un materiau (depuis chemin asset ou renderer) |
| `unity_set_material` | Modifier les proprietes — mapping auto URP/HDRP/Built-in |
| `unity_create_material` | Creer un nouveau materiau avec shader adapte au pipeline |

---

### UI (9 outils)

| Outil | Description |
|-------|-------------|
| `unity_create_canvas` | Creer un Canvas avec EventSystem (ScreenSpaceOverlay, Camera, WorldSpace) |
| `unity_create_ui_element` | Creer un element UI (Panel, Button, Text, Image, RawImage, Slider, Toggle, InputField, Dropdown, ScrollView) |
| `unity_get_ui_hierarchy` | Inspecter l'arborescence des elements UI |
| `unity_modify_ui_element` | Modifier texte, couleur, taille, interactable, valeur, sprite, placeholder, options |
| `unity_set_rect_transform` | Configurer les ancres, pivot et taille du RectTransform |
| `unity_add_layout_group` | Ajouter un layout group (Vertical, Horizontal, Grid) |
| `unity_add_content_size_fitter` | Ajouter un ContentSizeFitter |
| `unity_add_layout_element` | Ajouter un LayoutElement |
| `unity_set_canvas_scaler` | Configurer le CanvasScaler |

---

### ANIMATOR (23 outils)

| Outil | Description |
|-------|-------------|
| `unity_get_animator_controller` | Obtenir les infos du controller (etats, parametres, layers) |
| `unity_create_animator_controller` | Creer un nouveau Animator Controller |
| `unity_get_animator_parameters` | Lister tous les parametres |
| `unity_set_animator_parameter` | Modifier la valeur d'un parametre en temps reel |
| `unity_add_animator_parameter` | Ajouter un parametre (Float, Int, Bool, Trigger) |
| `unity_remove_animator_parameter` | Supprimer un parametre |
| `unity_add_animator_layer` | Ajouter un layer au controller |
| `unity_validate_animator` | Detecter les problemes (etats inaccessibles, clips manquants) |
| `unity_get_animator_flow` | Obtenir le diagramme complet de la machine a etats |
| `unity_add_animator_state` | Ajouter un etat a un layer |
| `unity_delete_animator_state` | Supprimer un etat |
| `unity_modify_animator_state` | Modifier les proprietes d'un etat (vitesse, tag, motion) |
| `unity_set_default_state` | Definir l'etat par defaut d'un layer |
| `unity_create_blend_tree` | Creer un etat Blend Tree |
| `unity_add_blend_motion` | Ajouter un motion a un Blend Tree |
| `unity_add_animator_transition` | Ajouter une transition avec conditions |
| `unity_delete_animator_transition` | Supprimer une transition |
| `unity_add_transition_condition` | Ajouter une condition a une transition |
| `unity_remove_transition_condition` | Supprimer une condition d'une transition |
| `unity_modify_transition` | Modifier les parametres d'une transition |
| `unity_list_animation_clips` | Lister les clips d'animation du projet |
| `unity_create_animation_clip` | Creer un nouveau clip d'animation |
| `unity_get_clip_info` | Obtenir les details d'un clip (duree, frequence, evenements) |

---

### TERRAIN (17 outils)

| Outil | Description |
|-------|-------------|
| `unity_create_terrain` | Creer un Terrain avec asset TerrainData |
| `unity_get_terrain_info` | Obtenir les infos du terrain (taille, layers, arbres, voisins) |
| `unity_modify_terrain` | Modifier les parametres du terrain (pixel error, distances, rendu) |
| `unity_set_terrain_heights_batch` | Sculpter le heightmap : flatten, raise, lower, set, noise, smooth |
| `unity_list_terrain_brushes` | Lister les brosses disponibles |
| `unity_add_terrain_layer` | Ajouter une couche de texture (diffuse, normal, taille, metallic) |
| `unity_paint_terrain_texture_batch` | Peindre une couche de texture sur une region (fusion alphamap) |
| `unity_paint_terrain_path` | Peindre une texture le long de waypoints (chemins, routes, rivieres) |
| `unity_add_terrain_trees` | Placer des arbres (positions explicites ou scatter aleatoire avec seed) |
| `unity_remove_terrain_trees` | Supprimer les arbres d'une region |
| `unity_list_terrain_trees` | Lister les prototypes d'arbres |
| `unity_add_terrain_detail` | Ajouter un detail (herbe, mesh) au terrain |
| `unity_paint_terrain_detail` | Peindre la densite des details |
| `unity_remove_terrain_detail` | Supprimer les details d'une region |
| `unity_import_heightmap` | Importer un heightmap depuis un fichier PNG/RAW |
| `unity_export_heightmap` | Exporter le heightmap vers un fichier PNG/RAW |
| `unity_set_terrain_neighbors` | Definir les terrains voisins pour les bords sans coutures |

---

### PHYSIQUE (8 outils)

| Outil | Description |
|-------|-------------|
| `unity_raycast` | Raycast physique — retourne tous les hits tries par distance |
| `unity_setup_rigidbody` | Ajouter ou configurer un Rigidbody (masse, drag, contraintes) |
| `unity_setup_collider` | Ajouter un collider (Box, Sphere, Capsule, Mesh, auto-detection) |
| `unity_set_physics_material` | Creer et assigner un PhysicsMaterial |
| `unity_bake_navmesh` | Generer le maillage de navigation |
| `unity_clear_navmesh` | Supprimer toutes les donnees NavMesh |
| `unity_get_navmesh_settings` | Obtenir les types d'agents et les infos de zones |
| `unity_set_navigation_static` | Marquer des GameObjects comme Navigation Static |

---

### AUDIO (3 outils)

| Outil | Description |
|-------|-------------|
| `unity_setup_audio_source` | Ajouter/configurer un AudioSource avec clip et groupe mixer |
| `unity_create_audio_mixer` | Creer un asset AudioMixer |
| `unity_get_audio_mixer` | Obtenir les infos du mixer (groupes, parametres exposes) |

---

### RENDU (13 outils)

| Outil | Description |
|-------|-------------|
| `unity_configure_camera` | Configurer les proprietes de camera (FOV, near/far, fond) |
| `unity_render_camera_to_file` | Rendre la vue camera dans un fichier PNG/JPG |
| `unity_get_render_pipeline_info` | Obtenir le pipeline de rendu actif (URP/HDRP/Built-in) |
| `unity_bake_lighting` | Generer les lightmaps (synchrone) |
| `unity_bake_lighting_async` | Demarrer un bake de lightmaps asynchrone |
| `unity_get_bake_status` | Obtenir la progression du bake en cours |
| `unity_cancel_bake` | Annuler le bake actif |
| `unity_clear_baked_data` | Supprimer toutes les donnees de bake |
| `unity_get_lightmap_settings` | Lire les parametres de lightmap |
| `unity_set_lightmap_settings` | Modifier les parametres de lightmap |
| `unity_bake_occlusion` | Generer l'occlusion culling |
| `unity_clear_occlusion` | Supprimer les donnees d'occlusion |
| `unity_bake_reflection_probes` | Generer les sondes de reflexion |

---

### BUILD (6 outils)

| Outil | Description |
|-------|-------------|
| `unity_get_build_settings` | Obtenir la configuration de build (plateforme, scenes, backend) |
| `unity_manage_build_scenes` | Ajouter, retirer, activer, desactiver ou reordonner les scenes |
| `unity_switch_platform` | Changer la plateforme cible (Windows, Mac, Linux, iOS, Android, WebGL) |
| `unity_list_packages` | Lister les packages Unity installes |
| `unity_add_package` | Ajouter un package via UPM |
| `unity_remove_package` | Supprimer un package via UPM |

---

### PARAMETRES (11 outils)

| Outil | Description |
|-------|-------------|
| `unity_get_project_settings` | Lire les parametres projet |
| `unity_set_project_settings` | Modifier les parametres projet |
| `unity_set_quality_level` | Definir le preset de qualite |
| `unity_get_physics_layer_collision` | Lire la matrice de collision des layers |
| `unity_set_physics_layer_collision` | Modifier les regles de collision entre layers |
| `unity_list_tags` | Lister tous les tags |
| `unity_list_layers` | Lister tous les layers |
| `unity_set_tag` | Assigner un tag a un GameObject |
| `unity_set_layer` | Assigner un layer a un GameObject |
| `unity_create_tag` | Creer un nouveau tag |
| `unity_create_layer` | Creer un nouveau layer |

---

### INPUT (3 outils)

| Outil | Description |
|-------|-------------|
| `unity_get_input_actions` | Obtenir le contenu d'un Input Action Asset |
| `unity_add_input_action` | Ajouter une nouvelle Input Action |
| `unity_add_input_binding` | Ajouter un binding a une Input Action |

---

### AVANCE (5 outils)

| Outil | Description |
|-------|-------------|
| `unity_set_reference` | Assigner une reference objet a un SerializedField |
| `unity_set_reference_array` | Assigner un tableau de references a un SerializedField |
| `unity_create_scriptable_object` | Creer un asset ScriptableObject |
| `unity_list_scriptable_object_types` | Lister les types ScriptableObject disponibles dans le projet |
| `unity_modify_scriptable_object` | Modifier les proprietes d'un ScriptableObject |

---

## Ressources

Les ressources MCP fournissent un acces en lecture seule a l'etat du projet Unity.

| URI | Type MIME | Description |
|-----|-----------|-------------|
| `unity://project/settings` | `application/json` | Nom, version, plateforme, backend de scripting |
| `unity://scene/hierarchy` | `application/json` | Objets racines de la scene courante avec composants |
| `unity://console/logs` | `application/json` | Entrees recentes des logs console Unity |
| `workflows://core` | `text/markdown` | Guide des workflows essentiels et optimisation des tokens |
| `workflows://animator` | `text/markdown` | Workflow complet Animator Controller |
| `workflows://materials` | `text/markdown` | Workflow materiaux et shaders (detection auto URP/HDRP) |
| `workflows://prefabs` | `text/markdown` | Creation, instanciation et workflow des overrides de prefabs |
| `workflows://assets` | `text/markdown` | Syntaxe de recherche d'assets et workflow navigateur |
| `workflows://terrain` | `text/markdown` | Guide de sculpture, peinture et brosses de terrain |

---

## Fenetre editeur

Accessible via **Tools > MCP Unity > Server Window**. Trois onglets :

### Onglet Chat

L'onglet principal — panneau de chat LLM multi-provider complet :

- **Saisie de messages** avec glisser-deposer de references assets/GameObjects
- **Streaming SSE** avec affichage token par token
- **Execution automatique d'outils** — boucle multi-tour (max 10 iterations par reponse)
- **Rendu Markdown** — blocs de code, titres, tableaux, citations, liens, listes
- **Barre de contexte** — affiche l'utilisation des tokens vs la fenetre de contexte du modele
- **Export** — Markdown, JSON, texte brut, ou presse-papier

### Onglet Settings

Quatre sections depliables :

| Section | Contenu |
|---------|---------|
| **Parametres provider** | Selecteur de provider, cle API, selecteur de modele, mode auth (Cle API / OAuth), override endpoint |
| **Categories d'outils** | 13 toggles de categories, boutons preset (Tous / Principal / Aucun), compteur |
| **Parametres serveur** | Port, demarrage auto, notifications, timeout, logs |
| **Avance** | Chemin custom du bridge, indicateur de sante |

### Onglet Diagnostics

Trois sections depliables :

| Section | Contenu |
|---------|---------|
| **Moniteur de requetes** | Statistiques live, chronometrage par requete (outil, duree, succes/erreur) |
| **Logs** | Entrees colorees (debug/info/warning/error), filtre de niveau, vidage |
| **Config Claude** | Statut `.mcp.json`, generateur de config Claude Desktop, copie presse-papier |

### Overlay barre d'outils

La barre d'outils de la vue Scene affiche un indicateur de statut du serveur MCP et un bouton demarrer/arreter.

---

## Systeme de chat integre

Le systeme de chat est construit avec l'IMGUI de Unity. Il fonctionne entierement dans l'editeur sans le bridge Node.js — il appelle les outils directement via le `McpToolRegistry` interne.

### Providers (9 presets)

| Provider | Exemples de modeles | Contexte | Auth |
|----------|--------------------|---------|----|
| **Anthropic Claude** | Sonnet 4.6, Opus 4.6, Haiku 4.5 | 200K | Cle API / OAuth PKCE |
| **OpenAI** | GPT-4o, o3 Mini, GPT-4.1 | 128K | Cle API |
| **Google Gemini** | 2.5 Pro, 2.5 Flash | 1M | Cle API |
| **DeepSeek** | Chat V3.2, Reasoner R1 | 128K | Cle API |
| **Groq** | Llama 3.3 70B, Qwen3 32B | 131K | Cle API |
| **Mistral AI** | Large 3, Codestral | 128K | Cle API |
| **Ollama** | Llama 3.3, Qwen 2.5 Coder (local) | 131K | Aucune |
| **LM Studio** | N'importe quel modele local | 131K | Aucune |
| **Custom** | N'importe quel endpoint OpenAI-compatible | 128K | Cle API |

### Boucle d'execution des outils

1. La reponse IA arrive via SSE
2. Le parseur detecte les blocs `tool_use`
3. Chaque outil est execute sur le thread principal Unity via `McpToolRegistry`
4. Les resultats sont ajoutes a la conversation et une nouvelle requete est envoyee
5. La boucle continue jusqu'a l'absence d'appels d'outils ou le max d'iterations (10)

### Confirmation des operations destructives

Les outils dangereux ou irreversibles declenchent un dialogue de confirmation **avant** execution dans le chat. L'utilisateur voit le nom de l'outil et un resume des arguments, et peut approuver ou refuser.

**Outils concernes** :
- **Destructifs** : `delete_gameobject`, `delete_asset`, `clear_baked_data`, `clear_navmesh`, `clear_occlusion`, `remove_terrain_trees`, `remove_terrain_detail`
- **Scripts** : `write_script`, `create_script`, `update_script`
- **Dangereux** : `execute_menu_item`, `unpack_prefab`
- **Longs/Irreversibles** : `switch_platform`, `bake_lighting`, `bake_lighting_async`, `bake_navmesh`, `bake_occlusion`

Si l'utilisateur refuse, l'outil retourne une erreur "Operation denied by user" et l'IA continue sans cette operation.

### Glisser-deposer de references

Glissez des assets depuis la fenetre Project ou des GameObjects depuis la Hierarchy vers le champ de saisie :

- Des puces d'assets apparaissent au-dessus du champ (icone + nom + bouton supprimer)
- La mention `@Nom` est inseree dans le texte
- A l'envoi, le contexte complet est resolu (composants, infos asset, dependances)

### Export de conversation

Cliquer sur le bouton `v` dans la barre d'outils :

| Format | Description |
|--------|-------------|
| **Markdown** (`.md`) | Titres par intervenant, code fence pour les appels d'outils, horodatages |
| **JSON** (`.json`) | Structure avec provider, modele, compteurs de tokens, tableau de messages |
| **Texte brut** (`.txt`) | Formatage epure avec horodatages |
| **Presse-papier** | Format Markdown |

### Authentification

- **Cles API** — par provider, stockees dans `EditorPrefs` (jamais committees dans le code source)
- **OAuth PKCE** (Anthropic uniquement) — flux OAuth2 complet via navigateur avec rafraichissement automatique des tokens

---

## Cache du bridge

Le bridge Node.js cache les resultats des outils en lecture seule pour reduire les appels Unity :

| Categorie | TTL | Outils caches |
|-----------|-----|---------------|
| `editorState` | 5s | `unity_get_editor_state` |
| `hierarchy` | 30s | `unity_list_gameobjects` |
| `components` | 1 min | `unity_get_component`, `unity_get_material` |
| `assets` | 5 min | `unity_search_assets`, `unity_get_asset_info`, `unity_list_folders` |
| `scenes` | 5 min | `unity_get_scene_info`, `unity_get_build_settings` |

Les operations d'ecriture invalident automatiquement les entrees de cache pertinentes.

---

## Guide de developpement

### Ajouter un nouvel outil

**1. C# — Enregistrer dans `Editor/McpServer/Tools/` :**

```csharp
static partial void RegisterMyTools()
{
    _toolRegistry.RegisterTool(new McpToolDefinition
    {
        name = "unity_mon_outil",
        description = "Description courte (moins de 80 caracteres)",
        inputSchema = new McpInputSchema
        {
            type = "object",
            properties = new Dictionary<string, McpPropertySchema>
            {
                ["path"] = new McpPropertySchema { type = "string", description = "..." }
            },
            required = new List<string> { "path" }
        }
    }, MonOutilHandler);
}

private static McpToolResult MonOutilHandler(Dictionary<string, object> args)
{
    var (path, pathErr) = RequireArg(args, "path");
    if (pathErr != null) return pathErr;

    // Pour les chemins d'assets, utiliser TrySanitizePath :
    // var (safePath, pathErr) = TrySanitizePath(rawPath, "path");
    // if (pathErr != null) return pathErr;

    // Pour trouver un GameObject :
    // var (go, goPath, goErr) = RequireGameObject(args, "gameObjectPath");
    // if (goErr != null) return goErr;

    // ... implementation ...

    return McpResponse.Success(new { result = "ok" });
}
```

**2. Declarer et appeler la methode partielle dans `McpUnityServer.cs`.**

**3. TypeScript — Ajouter dans `Server~/src/tools.ts` :**

```typescript
{
  name: 'unity_mon_outil',
  description: 'Description courte',
  inputSchema: {
    type: 'object',
    properties: { path: { type: 'string' } },
    required: ['path'],
  },
  defer_loading: true,
},
```

**4. Invalidation du cache dans `Server~/src/cache.ts` (si l'outil ecrit) :**

```typescript
unity_mon_outil: ['hierarchy', 'components'],
```

**5. Recompiler :** `npm run build` dans `Server~/`

### Conventions

| Regle | Detail |
|-------|--------|
| Nommage | Prefixe `unity_` + `snake_case` |
| Arguments requis | `RequireArg(args, "key")` — retourne `(value, error)` |
| Trouver un GameObject | `RequireGameObject(args, "key")` — retourne `(go, path, error)` |
| Securite des chemins | `TrySanitizePath(raw, "label")` — retourne `(path, error)`, bloque `..` et impose `Assets/` |
| Serialisation | Toujours utiliser `JsonHelper.ToJson()` — jamais `JsonUtility.ToJson()` sur les Dictionary |
| Reponse | `McpResponse.Success(data)` ou `McpToolResult.Error(message)` |
| Descriptions | Moins de 80 caracteres (budget tokens : ~1200 total pour toutes les descriptions) |

---

## Depannage

### Bridge et serveur

| Probleme | Solution |
|----------|----------|
| Le bridge ne se connecte pas | Verifier qu'Unity tourne et que le serveur est demarre (indicateur vert) |
| Port deja utilise | Changer le port dans Settings > Parametres serveur |
| Les outils retournent "not connected" | Le bridge se connecte en asynchrone — attendre quelques secondes |
| Serialisation `JsonUtility` retourne `{}` | Utiliser `JsonHelper.ToJson()` — gere les `Dictionary<string, object>` |
| Chemin rejete par `SanitizePath` | Le chemin doit commencer par `Assets/` et ne pas contenir `..` |
| Erreurs apres modification de script | Utiliser `unity_refresh_and_compile` pour le domain reload |
| Cache obsolete | L'outil doit figurer dans `cacheInvalidators` dans `cache.ts` |
| Timeout WebSocket | Augmenter `REQUEST_TIMEOUT` (defaut : 10s) |
| Node.js introuvable | Lancer l'assistant : Tools > MCP Unity > Setup Wizard |

### Systeme de chat

| Probleme | Solution |
|----------|----------|
| "No API key" | Entrer la cle dans Settings > Parametres provider |
| Outil indisponible dans le chat | Verifier Settings > Categories d'outils |
| Streaming s'arrete | Verifier la connexion ; appuyer sur Echap puis reessayer |
| Connexion OAuth echoue | Verifier que le navigateur peut atteindre le serveur Anthropic |
| Contexte se remplit trop vite | Desactiver les categories inutilisees ; utiliser Clear |
| Glisser-deposer ne fonctionne pas | Deposer sur le champ de saisie, pas sur la zone des messages |

### Messages d'erreur courants

| Erreur | Cause | Solution |
|--------|-------|----------|
| `GameObject not found: X` | Objet inactif ou chemin incorrect | Utiliser `unity_list_gameobjects` pour verifier |
| `Required parameter 'X' is missing` | Argument requis manquant | Verifier les champs `required` du schema de l'outil |
| `Tool 'X' exists but category 'Y' is not enabled` | Categorie non chargee | Appeler `unity_enable_tool_category` |
| `Invalid asset path` | Chemin avec `..` ou sans `Assets/` | Utiliser des chemins complets `Assets/...` |
