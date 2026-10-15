# Architecture du code

## Arborescence

```
Assets/
├── _Project/
│   ├── Art/            (artistes) Models/ Materials/ Textures/ Animations/ VFX/
│   ├── Audio/          Music/ Sfx/ Mixers/
│   ├── Data/           ScriptableObjects : Anachronisms/ Eras/ MatchPresets/
│   ├── Prefabs/        Network/ Player/ Npc/ Anachronisms/ Props/ UI/
│   ├── Scenes/         Boot, MainMenu, Maps/Map_<Epoque>, Sandbox/Sandbox_<Prenom>
│   └── Scripts/
│       ├── Core/       bootstrap, utilitaires, extensions
│       ├── Network/    MatchManager, lobby Steam, observers, vocal       → Théo
│       ├── Gameplay/   Player/ Weapon/ Placement/ Portal/ Photos/ Anachronisms/ → Célian
│       ├── Npc/        IA, spawner de foule                              → Olivier
│       ├── World/      Degradation/ Glitch/                              → Olivier
│       ├── UI/         Hud/ Menus/ Lobby/                                → Olivier
│       └── Editor/     outils éditeur (asmdef Editor)
└── ThirdParty/         FishNet, FishySteamworks, etc. (ne jamais modifier)
```

- **Namespace = dossier** : `Anachronism.Network`, `Anachronism.Gameplay`, `Anachronism.Npc`, `Anachronism.World`, `Anachronism.UI`, `Anachronism.Editor`.
- **2 asmdefs seulement** : `Anachronism.Runtime` (tout `Scripts/` sauf `Editor/`) et `Anachronism.Editor`. On en découpe d'autres seulement si la compilation devient lente.
- **Un dossier = un responsable** (voir les flèches). Pour toucher le dossier d'un autre, on le prévient.

## Conventions de nommage

| Élément | Convention | Exemple |
|---|---|---|
| Classe, struct, enum, méthode, propriété, event | PascalCase | `MatchManager`, `StartMatch()`, `PhaseChanged` |
| Champ privé (y compris `[SerializeField]`) | `_camelCase` | `[SerializeField] private float _shootCooldown;` |
| Paramètre, variable locale | camelCase | `claimedTarget` |
| Constante, `static readonly` | PascalCase | `MaxPlayers` |
| Interface | `I` + PascalCase | `IShootable` |
| Booléen | préfixe `Is` / `Has` / `Can` | `IsFound`, `_hasPlaced` |
| Event / handler | event au passé, handler en `On…` | `VillainCaptured` / `OnVillainCaptured` |
| RPC FishNet | suffixe selon le type | `ShootServerRpc`, `PlayFxObserversRpc`, `TargetAssignRole` |
| Fichier | 1 classe publique par fichier, fichier = nom de la classe | `MatchManager.cs` |
| Prefab | PascalCase, préfixe de domaine | `Npc_Villager`, `Anachro_Phone` |
| Scène | `Map_<Epoque>`, `Sandbox_<Prenom>` | `Map_Rome`, `Sandbox_Celian` |
| Assets art (à valider avec les artistes) | `SM_` mesh, `SK_` skinné, `M_` matériau, `T_` texture, `A_` anim | `SM_Phone`, `T_Phone_Albedo` |

Accessibilité **toujours explicite** (`private`, `public`…). Pas de champ public : `[SerializeField] private` + propriété en lecture si nécessaire.

## Règles de code

1. **Pas de `Find` / `FindObjectOfType` / `GetComponent` dans `Update`**. Les références passent par l'inspecteur ou sont cachées dans `Awake`.
2. **Singletons limités** à `MatchManager` (objet réseau de scène) et aux services de `Core/`. Pas de nouveau singleton sans en parler.
3. **Les données dans des ScriptableObjects** (anachronismes, époques, préréglages). Aucune valeur d'équilibrage en dur dans le code.
4. **Découplage par events C#** : le monde, l'UI et l'audio écoutent `MatchManager` (`PhaseChanged`, etc.). Ils ne le modifient jamais.
5. **L'UI ne touche jamais l'état réseau.** Elle appelle le gameplay, qui appelle les RPC.
6. **Les règles partagées client/serveur sont des fonctions pures** (ex : validité d'une pose), appelées par l'aperçu client **et** par la validation serveur.
7. **Les commentaires** expliquent le *pourquoi*, pas le *quoi*. `// TODO (Prénom) :` pour le travail restant.

## Règles réseau (FishNet, l'hôte fait serveur)

- **L'hôte décide de tout** : phases, rôles, pose, tirs, munitions, capture, victoire. Le client envoie des *intentions*, jamais des résultats.
- **Toute RPC client → serveur est considérée comme hostile** : vérifier le rôle, la phase, la cadence, la distance et l'état.
- **Secrets jamais en SyncVar** : un rôle passe par un `TargetRpc` au seul joueur concerné, et le statut « est un anachronisme » reste sur le serveur.
- **SyncVar** pour l'état persistant, **RPC** pour les événements ponctuels. Les animations et les FX sont déduits localement, jamais synchronisés.
- **PNJ et méchant : synchro strictement identique** (même NetworkTransform, même fréquence, même Animator piloté par la vitesse). Sinon le méchant est repérable.
- **Le temps de jeu se compte en ticks** (`TimeManager`), pas en `Time.time` côté client.
- Le code exécuté des deux côtés est protégé par `IsServerInitialized` / `IsOwner`, et les méthodes serveur portent l'attribut `[Server]`.
- **Toujours tester avec l'hôte + au moins 2 clients**, et une fois par semaine sous 120 ms / 2 % de perte.
- **Seul point client-autoritaire assumé** : le mouvement des joueurs. Pas de prédiction ni de réconciliation, c'est hors scope.

## Git

- **Setup** : `.gitignore` Unity officiel, Asset Serialization = Force Text, Visible Meta Files, **Git LFS** (`*.fbx *.blend *.png *.psd *.tga *.wav *.ogg *.mp3 *.ttf`), `.gitattributes` avec `merge=unityyamlmerge` pour `*.unity *.prefab *.asset *.mat *.anim *.controller`.
- **Branches** : `main` (toujours buildable, releases), `develop` (intégration), `feature/<prenom>-<sujet>`, `fix/<sujet>`. Des PR petites, relues par un autre dev.
- **Commits** : `type: sujet` (`feat: pose des objets`, `fix: désync PNJ`, `ld: greybox zone marché`).
- **Scènes** : chacun travaille dans son `Sandbox_<Prenom>`. La scène de la map appartient à Théo, les autres ajoutent des **prefabs**. Faites un `git lfs lock` avant de toucher une scène partagée.
- **Toujours committer les `.meta`**. Ne jamais déplacer ou renommer un asset hors de Unity.

## Squelette fourni (`Code/Scripts/`)

| Fichier | Contenu |
|---|---|
| `Network/GameTypes.cs` | `GamePhase`, `PlayerRole`, `Winner`, `MatchSettings` (+ préréglages) |
| `Network/MatchManager.cs` | Phases, rôles secrets, chrono en ticks, dégradation, munitions, tir validé côté serveur, capture, portail, déconnexions |
| `Network/HiddenWhileHidingCondition.cs` | Condition d'observation pour la phase de cache |
| `Gameplay/Anachronisms/PlacedAnachronism.cs` | Objet posé (état « trouvé ») |
| `Npc/NpcAgent.cs` | Marqueur PNJ (à compléter par Olivier) |

Écrit pour **FishNet v4**, **non compilé ici** : vérifiez les signatures à l'import (notamment `ObserverCondition`, ou `Clone()` selon la version, et `RebuildObservers`).
