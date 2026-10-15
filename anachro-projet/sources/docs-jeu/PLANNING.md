# Planning

Base : semaines « Quête Studio » du planning école (30 h/semaine/dev). Les heures hors studio servent de **marge**, elles ne sont pas planifiées.

## Calendrier studio

| Sem. | Dates | Bloc |
|---|---|---|
| S1 | 5–9 oct | ✅ fait |
| **S2** | 12–16 oct | **en cours** |
| S3 | 19–23 oct | |
| — | 26 oct → 27 nov | cours + Quête I1 (pas de studio) |
| S4 | 30 nov–4 déc | |
| S5 | 7–11 déc | |
| S6 | 14–18 déc | |
| — | vacances + 4–8 jan cours | |
| S7 | 11–15 jan | |
| S8 | 18–22 jan | |
| S9 | 25–29 jan | **démo** |
| — | 1 fév → 5 mars | cours + Quête I2 (pas de studio) |
| S10 | 8–12 mars | |
| S11 | 15–19 mars | build en review Steam |
| S12 | 22–26 mars | **sortie** |
| — | 29 mars → 2 avr | cours (hotfix éventuels hors studio) |
| S13 | 5–9 avr | Studio #2 — MAJ 1.1 |
| S14 | 12–16 avr | |
| S15 | 19–23 avr | **MAJ 1.1** |
| — | 26 avr → 28 mai | cours + Quête I3 (pas de studio) |
| S16 | 31 mai–4 juin | Studio #2 — MAJ 1.2 |
| S17 | 7–11 juin | |
| S18 | 14–18 juin | **MAJ 1.2** + jury |

## Jalons

| Fin de | Jalon | Critère de validation |
|---|---|---|
| S3 | **Boucle en LAN** | En greybox, via Tugboat, avec 3 instances : rôles secrets, PNJ, phase de cache (pose), phase de recherche, tir validé par le serveur |
| S4 | **Première partie en ligne** | Via Steam (AppID 480) sur des PC différents, de bout en bout : lobby → cache → recherche → portail/capture → fin. Playtest à 4 ou plus |
| S6 | **Gameplay core gelé** | Playtest à 8, vocal OK, dégradation OK. Page Steam en ligne |
| S9 | **Démo** | Map 1 avec l'art intégré, stable sous 120 ms / 2 % de perte. Build envoyé pour le Next Fest |
| S11 | **Release candidate** | Plus aucun bug bloquant, build envoyé en review Steam |
| S12 | **Sortie** | |
| S15 | **MAJ 1.1** | Les bugs majeurs remontés par les joueurs sont corrigés, plus 2 ou 3 améliorations demandées |
| S18 | **MAJ 1.2** | Map 2 jouable et polie, nouveaux anachronismes. Sortie avant les soldes d'été Steam (date à vérifier) |

## Semaine par semaine

| Sem. | Théo (online + LD) | Célian (joueur & gameplay) | Olivier (PNJ, monde, UI) |
|---|---|---|---|
| **S2** | T01 repo/Git, T02 FishNet + Tugboat, T03 MatchManager, **choix du vocal** | C01 contrôleur FPS, C02 prefabs gentil/méchant | O01 PNJ v0 (déplacement + arrêts) |
| **S3** | T04 synchro PNJ/méchant, T05 observers, T06–T07 RPC tir/pose, L01 greybox map 1 | C03 pistolet (client), C04 pose v1, C12 données (ScriptableObjects) | O02 Animator, O04 outil points PNJ, O05 HUD |
| **S4** | T08 Steam lobby/relais, T09 réglages hôte, L02 NavMesh + points d'intérêt | C05 outil surfaces/portail, C06 capture, C07 portail | O03 spawner de foule, O06 menu + UI lobby, O07 dégradation v1 |
| **S5** | T10 vocal de proximité, T12 tests de latence, L03 surfaces + points portail, L04 itération du layout | C08 photos temporelles, C09 auto-placement, C04 règle de visibilité | O08 glitchs, O09 audio, O03 perf à 60 PNJ |
| **S6** | T11 robustesse (déconnexions, hôte qui quitte, rejouer), **playtest à 8** | C10 flux de fin, C11 briefing/tuto | O10 options, O11 écran de fin |
| **S7** | T13 pipeline de build + SteamPipe, L05 intégration art | C13 juice tir/capture | O12 polish UI/FX |
| **S8** | L06 équilibrage, L07 optimisation | C14 équilibrage, bugfix | O13 caméra libre (trailer), bugfix |
| **S9** | T14 build démo | bugfix démo | bugfix démo |
| **S10** | retours du Next Fest, bugfix réseau | retours, polish | retours, polish |
| **S11** | T15 build release → review Steam | bugfix | bugfix |
| **S12** | sortie, hotfix | hotfix | hotfix |
| **S13** | tri des retours joueurs → backlog 1.1, L08 greybox map 2 | bugfix gameplay | bugfix UI/PNJ |
| **S14** | bugfix réseau, L08 itération map 2 | améliorations 1.1 (U01) | améliorations 1.1 (U02) |
| **S15** | build MAJ 1.1, playtest map 2 en greybox | bugfix, nouveaux anachronismes (data) | bugfix, PNJ de l'époque 2 |
| **S16** | L09 intégration de l'art de la map 2 | U03 nouveaux objets/mécaniques de la 1.2 | U04 dégradation/glitchs de l'époque 2, audio |
| **S17** | L10 équilibrage map 2, optimisation | équilibrage, juice | UI de sélection de map, polish |
| **S18** | build MAJ 1.2 + jury | bugfix | bugfix |

> Entre S12 et S13, et entre S15 et S16, le jeu est **en ligne et sans dev en studio**. Prévoyez qui surveille les avis et les bugs critiques pendant ces trous.

## Dépendances art (à caler avec les 2 artistes)

| Pour | Besoin | Utilisé par |
|---|---|---|
| S3 | Perso PNJ riggé (placeholder accepté) : marche, idle ×2 | Olivier |
| S4 | Les 10 anachronismes en low poly (petits / moyens / gros) | Célian |
| S5 | Kit modulaire de l'époque de la map 1 | Théo (LD) |
| S6 | Skins des gentils + variantes de PNJ | Célian, Olivier |
| S7 | Habillage final de la map 1 | Théo |
| S14 | Kit modulaire de l'époque 2 + variantes de PNJ (à commencer dès que la map 1 est finie) | Théo, Olivier |
| S15 | Nouveaux anachronismes | Célian |

## Où on en est (à remplir en fin de bloc)

- **Fin S3** :
- **Fin S6** :
- **Fin S9** :
