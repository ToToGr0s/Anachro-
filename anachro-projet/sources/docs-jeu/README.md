# Anachro — récap production (devs)

> Nom de code provisoire : **Anachro** (namespace `Anachronism`). Pas de GDD pour l'instant : ce dossier liste seulement **qui fait quoi et quand**.

## Contenu du dossier

| Fichier | Rôle |
|---|---|
| `PLANNING.md` | Calendrier des semaines studio + plan semaine par semaine + jalons |
| `TACHES.md` | Backlog par dev (Théo / Célian / Olivier), avec IDs et cases à cocher |
| `ARCHITECTURE.md` | Arborescence, conventions de nommage, règles de code, réseau, Git |
| `Code/Scripts/` | Squelette FishNet de départ (MatchManager, rôles, phases, tir, observers) |

## Répartition

| Dev | Périmètre |
|---|---|
| **Théo** | Online (FishNet, Steam, vocal, validation serveur, builds) + Level Design |
| **Célian** | Joueur & gameplay : contrôleur, pistolet temporel, pose des objets, capture, portail, photos, data |
| **Olivier** | PNJ, monde & présentation : IA de foule, dégradation/glitchs, UI/HUD/menus, audio |

## ⚠️ À lire avant tout

1. **Il ne reste pas 20 semaines.** D'après le planning école, jusqu'à fin mars il reste **10,5 semaines de studio** (S2 en cours → S12), soit environ **315 h par dev** sur les heures studio. Février n'a **aucune** semaine studio, novembre une seule.
2. **2 maps pour mars, ce n'est pas tenable.** Plan : **sortie en mars avec 1 map bien finie**, puis Studio #2 (avril à juin) = **MAJ 1.1 fin avril** (bugs + retours joueurs) et **MAJ 1.2 mi-juin** (map 2 + contenu).
3. **Steam Next Fest de février** (dates à vérifier) : aucune semaine studio en février, donc la **démo doit être prête fin S9 (29 janvier)**. Vérifiez la date limite d'inscription.
4. **Steam** : l'AppID est payant (Steam Direct). Qui paie, l'école ? La page « Coming soon » doit être en ligne au moins 2 semaines avant la sortie, et la review du build prend plusieurs jours. **Visez une page en ligne fin S6.**
5. **Les trous de 5 à 6 semaines** entre deux blocs studio font perdre le contexte. Fin de chaque bloc : `main` buildable + une note « où on en est » dans `PLANNING.md`.
