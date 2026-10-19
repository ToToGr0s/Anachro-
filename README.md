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
   cd Anachro- && git checkout Dev && git lfs pull
   ```
4. **Fusion des scènes et prefabs** : sans ça, deux personnes qui touchent la même scène = conflit illisible.
   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   ```
   ```bash
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p --force %O %B %A %A"
   ```
   Chemin Windows. Sur macOS : `/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/Tools/UnityYAMLMerge`. Adapte-le si Unity Hub est installé ailleurs.
5. **Ouvrir** : Unity Hub → Add → choisir le dossier **`Anachro/`** (pas la racine du dépôt). Le premier import prend plusieurs minutes.
6. Ouvrir `Assets/_Project/Scenes/Boot.unity` → Play. Le menu « Créer une partie / Rejoindre » doit apparaître. Play lancé depuis la map ou le menu marche pour le LD, mais sans réseau (pas de NetworkManager).

## Tester le réseau

Le jeu passe **uniquement par Steam** : il faut **un PC par joueur**, chacun avec son compte Steam (pas besoin d'être amis).

1. Steam lancé **avant** le jeu sur chaque PC. En dev, on utilise l'AppID 480 (Spacewar).
2. Faire un build : **Anachro > Build > Windows (Development)**, puis copier tout le dossier `Anachro/Builds/Dev` sur les autres PC. Un PC peut rester dans l'éditeur, à condition d'avoir le même code que le build.
3. Hôte : **Créer une partie**, puis donner le code de 7 caractères. Les autres : taper le code, puis **Rejoindre**.
4. Hôte : **Lancer la partie**.
- Toujours tester avec **l'hôte + au moins 2 clients** dès que ça touche au gameplay ou au réseau.

## Travailler

1. Partir de `Dev` à jour, créer sa branche :
   ```bash
   git checkout Dev && git pull && git checkout -b Feat-mon-sujet
   ```
   `Feat-<sujet>` pour une nouveauté, `Fix-<sujet>` pour une correction. Majuscule incluse.
2. Coder dans **son** dossier (`Assets/_Project/Scripts/<Domaine>/`) et dans **sa** scène `Scenes/Sandbox/Sandbox_<Prenom>` (à créer la première fois). Une scène partagée : prévenir + `git lfs lock`.
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

## Ajouter un PNJ ou un skin

1. **Modèle** : le déposer dans `Assets/_Project/Art/Characters/<Nom>/` (FBX, matériaux, textures ensemble). Un pack du store va dans `Assets/Import/<Pack>/`.
2. **Profil** : dans `Assets/_Project/ScriptableObjects/Npc/`, clic droit > Create > Anachronism > Npc Profile (ou dupliquer un profil existant). Y glisser le modèle (`Model Prefab`), son Animator Controller, l'échelle, et le comportement (vitesses, pauses, sprint).
3. C'est tout : la table `NpcSkins` et le prefab `Npc_v0` se mettent à jour seuls (`NpcAssetSync`). Tout profil avec un modèle devient un skin tiré au hasard pour les PNJ **et** les joueurs.
4. Pour que ce comportement **apparaisse** dans une map (combien de PNJ de ce type) : l'ajouter dans la liste du `NpcDestinationManager` de la map (objet `Npcs`).

Hors de Unity, rien à toucher : profils et skins sont repérés par leur nom, dans l'ordre alphabétique, identique pour tout le monde. En cas de doute : **Anachro > PNJ > Resynchroniser les profils et skins**.
