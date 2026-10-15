# Anachro

Party game Unity 3D en ligne (4 à 8 joueurs), FishNet v4 + Steam. Règles du projet : [`CLAUDE.md`](CLAUDE.md).

## Installer le projet (une fois, ~15 min)

1. **Outils** : [Git](https://git-scm.com), Git LFS (`git lfs install`), Unity Hub, Steam.
2. **Unity 6000.6.5f1** (exactement cette version, via Unity Hub → Installs → Install Editor → Archive).
3. **Cloner** :
   ```bash
   git clone https://github.com/ToToGr0s/Anachro-.git
   ```
   ```bash
   cd Anachro- && git lfs pull && git checkout Dev
   ```
4. **Fusion des scènes et prefabs** : sans ça, deux personnes qui touchent la même scène = conflit illisible.
   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   ```
   ```bash
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   ```
   Adapte le chemin si Unity Hub est installé ailleurs.
5. **Ouvrir** : Unity Hub → Add → choisir le dossier **`Anachro/`** (pas la racine du dépôt). Le premier import prend plusieurs minutes.
6. Ouvrir `Assets/_Project/Scenes/Boot.unity` → Play. Le HUD en haut à gauche doit apparaître.

## Tester le réseau

- **Local (sans Steam)** : une instance « Hôte local », les autres « Client local » sur `localhost`. Plusieurs instances : Multiplayer Play Mode (Package Manager → `com.unity.multiplayer.playmode`) ou un build Development.
- **Steam** : Steam lancé **avant** le jeu, 2 PC avec 2 comptes amis. « Héberger un lobby » → « Inviter des amis » → l'autre accepte dans l'overlay (Maj+Tab). En dev on utilise l'AppID 480 (Spacewar).
- Toujours tester avec **l'hôte + au moins 2 clients** dès que ça touche au gameplay ou au réseau.

## Travailler

1. Partir de `Dev` à jour, créer sa branche :
   ```bash
   git checkout Dev && git pull && git checkout -b Feat-mon-sujet
   ```
   `Feat-<sujet>` pour une nouveauté, `Fix-<sujet>` pour une correction. Majuscule incluse.
2. Coder dans **son** dossier (`Assets/_Project/Scripts/<Domaine>/`) et dans **sa** scène `Scenes/Sandbox/Sandbox_<Prenom>`. Une scène partagée : prévenir + `git lfs lock`.
3. Committer les `.meta` avec les fichiers. Ne jamais déplacer ou renommer un asset hors de Unity.
4. Pousser, ouvrir une PR **vers `Dev`**, titre `type: sujet` (`feat:`, `fix:`, `perf:`, `refactor:`, `ld:`, `art:`, `docs:`, `chore:`).
5. Attendre la review Claude (2 à 5 min), corriger, puis Théo relit et merge en « Squash and merge ».

## Où est quoi

| Dossier | Contenu | Responsable |
|---|---|---|
| `Assets/_Project/Scripts/Network` | Réseau, lobby Steam, MatchManager | Théo |
| `Assets/_Project/Scripts/Gameplay` | Joueur, arme, pose, portail | Célian |
| `Assets/_Project/Scripts/{Npc,World,UI}` | PNJ, dégradation, interface | Olivier |
| `Assets/_Project/Art`, `Audio` | Modèles, textures, sons | Hugo, JB, Olivier |
| `Assets/ThirdParty` | FishNet, FishySteamworks : **ne jamais modifier** (voir son README) | — |

Le menu **Anachro > Setup > Construire la base réseau** régénère le NetworkManager de `Boot` si besoin.
