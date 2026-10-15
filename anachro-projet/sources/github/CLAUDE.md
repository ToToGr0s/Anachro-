# Anachro — règles du projet (lues par Claude pendant les reviews)

Jeu Unity 3D, multijoueur en ligne (4 à 8 joueurs), FishNet v4 + Steam (FishySteamworks), l'hôte fait serveur.
Équipe : Théo (online + level design, valide toutes les PR), Célian (joueur & gameplay), Olivier (PNJ, monde, UI, audio), Hugo et JB (art).

## Arborescence
- `Assets/_Project/Scripts/{Core,Network,Gameplay,Npc,World,UI,Editor}` — namespace = dossier (`Anachronism.Network`, …).
- Responsables : Network → Théo · Gameplay → Célian · Npc / World / UI → Olivier. Toucher le dossier d'un autre = le signaler dans la PR.
- `Assets/ThirdParty/` (FishNet, etc.) : ne jamais modifier.
- 2 asmdefs seulement : `Anachronism.Runtime` et `Anachronism.Editor`.

## Nommage
- PascalCase : classes, méthodes, propriétés, events, enums, constantes, `static readonly`.
- `_camelCase` : tous les champs privés, y compris `[SerializeField] private`.
- camelCase : paramètres et variables locales.
- Interfaces `I…`, booléens `Is/Has/Can…`, events au passé (`PhaseChanged`) et handlers `On…`.
- RPC FishNet : suffixe `ServerRpc`, `ObserversRpc`, préfixe `Target` pour les TargetRpc.
- Accessibilité toujours explicite. Jamais de champ public : `[SerializeField] private` + propriété.
- 1 classe publique par fichier, fichier = nom de la classe.

## Règles de code
1. Pas de `Find` / `FindObjectOfType` / `GetComponent` dans `Update` (références par l'inspecteur ou cache dans `Awake`).
2. Singletons : seulement `MatchManager` et les services de `Core/`.
3. Valeurs d'équilibrage dans des ScriptableObjects, rien en dur.
4. Découplage par events C#. Monde / UI / audio écoutent `MatchManager`, ne le modifient jamais.
5. L'UI ne touche jamais l'état réseau (UI → gameplay → RPC).
6. Règles partagées client/serveur (ex : validité d'une pose) = fonctions pures.
7. Commentaires = le pourquoi. `// TODO (Prénom) :` pour le travail restant.

## Règles réseau (prioritaires en review)
1. L'hôte décide de tout : phases, rôles, pose, tirs, munitions, capture, victoire. Le client envoie des intentions, jamais des résultats.
2. Toute RPC client → serveur est hostile : vérifier rôle, phase, cadence, distance, état.
3. Aucun secret en SyncVar : rôle via `TargetRpc` uniquement ; « est un anachronisme » reste côté serveur.
4. SyncVar = état persistant, RPC = événement ; animations et FX calculés en local.
5. PNJ et méchant : synchronisation STRICTEMENT identique (même NetworkTransform, même fréquence, Animator piloté par la vitesse).
6. Temps de jeu en ticks (`TimeManager`), pas `Time.time` côté client.
7. Code exécuté des deux côtés protégé par `IsServerInitialized` / `IsOwner` ; méthodes serveur marquées `[Server]`.
8. Seul point client-autoritaire accepté : le mouvement des joueurs.
9. Vocal de proximité sans indicateur de parole au-dessus des têtes.

## Git
- Branches : `main` (buildable), `develop` (intégration), `feature/prenom-sujet`, `fix/sujet`.
- Titre de PR = `type: sujet` (feat, fix, perf, refactor, ld, art, docs, chore). Merge en « Squash and merge ».
- Toujours committer les `.meta`. Binaires (fbx, png, wav…) en Git LFS. Scènes partagées : prévenir + `git lfs lock`.

## Ce que la review doit vérifier en priorité
1. Violations des règles réseau (triche possible, secret qui fuit, RPC non validée, code serveur exécuté côté client).
2. Bugs probables (null, ordre d'initialisation réseau, spawn avant que l'objet soit réseau, ownership).
3. Performance dans `Update` / boucles par frame.
4. Nommage et organisation.
5. Ce qui manque : validation, gestion d'erreur, cas limites (déconnexion, hôte qui quitte), `.meta`, description de PR.
