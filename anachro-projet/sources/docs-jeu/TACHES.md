# Tâches

Format : `[ ] ID — tâche (semaine cible)`. Une tâche se coche quand elle est **mergée sur `develop` et testée en réseau**, pas seulement en solo.

---

## Théo — Online

- [ ] **T01** — Repo Git + LFS + `.gitattributes` + UnityYAMLMerge, version de Unity verrouillée, arborescence créée (S2)
- [ ] **T02** — FishNet + transport Tugboat, NetworkManager, spawn des joueurs, test à 3 instances (Multiplayer Play Mode / ParrelSync) (S2)
- [ ] **T03** — Intégrer le squelette `MatchManager` : phases, rôles secrets (TargetRpc), chrono en ticks (S2)
- [ ] **T04** — Synchro des PNJ (NetworkTransform 10 Hz, interpolation) + **prefab méchant avec une synchro identique** (avec Olivier) (S3)
- [ ] **T05** — `HiddenWhileHidingCondition` : méchants et objets invisibles aux gentils pendant la cache (S3)
- [ ] **T06** — `ShootServerRpc` : validation (rôle, phase, cadence, origine, rayon), munitions, pénalités (S3)
- [ ] **T07** — RPC de pose : le serveur revalide avec la règle partagée de Célian (C04), puis spawne l'objet (S3)
- [ ] **T08** — FishySteamworks, AppID 480, lobby Steam, invitations via l'overlay, relais Steam (S4)
- [ ] **T09** — Réglages de l'hôte dans les métadonnées du lobby + préréglages par nombre de joueurs (S4)
- [ ] **T10** — Vocal de proximité, **sans indicateur au-dessus des têtes**. Choix Steam natif ou Dissonance **décidé en S2** (S5)
- [ ] **T11** — Robustesse : déconnexions, départ de l'hôte → retour au menu, refus de rejoindre en cours, rejouer sans relancer (S6)
- [ ] **T12** — Test hebdo à 120 ms / 2 % de perte, dès S4 (récurrent)
- [ ] **T13** — Pipeline de build + SteamPipe, branche beta pour les playtests (S7)
- [ ] **T14** — Build démo Next Fest (S9)
- [ ] **T15** — Build release + soumission à la review Steam (S11)

## Théo — Level Design

- [ ] **L01** — Greybox map 1 : boucles de circulation, zones, lignes de vue, cachettes (S3)
- [ ] **L02** — NavMesh + points d'intérêt et zones de densité PNJ (avec l'outil O04) (S4)
- [ ] **L03** — Surfaces de pose valides + points d'apparition du portail (avec l'outil C05) (S5)
- [ ] **L04** — Itération du layout après le playtest S4 (S5)
- [ ] **L05** — Intégration de l'art sur la map 1 avec les artistes (S7)
- [ ] **L06** — Équilibrage : nombre de PNJ, taille des zones, durées selon le nombre de joueurs (S8)
- [ ] **L07** — Optimisation : occlusion, LOD, static batching (S8)
- [ ] **L08** — Greybox map 2 + playtest (S13–S15)
- [ ] **L09** — Intégration de l'art sur la map 2 (S16)
- [ ] **L10** — Équilibrage + optimisation map 2 (S17)

---

## Célian — Joueur & gameplay

- [ ] **C01** — Contrôleur FPS + caméra + New Input System (S2)
- [ ] **C02** — Prefabs joueur gentil / méchant (skin PNJ). Contrôles bridés pour le méchant : pas de saut, vitesse des PNJ (S2–S3)
- [ ] **C03** — Pistolet temporel côté client : visée, raycast, cible déclarée → `ShootServerRpc`, FX et son placeholder (S3)
- [ ] **C04** — Pose des objets (méchant) : inventaire imposé (1 petit, 1 moyen, 2 gros), aperçu fantôme, **règle de surface + visibilité minimale dans une fonction pure partagée client/serveur** (S3–S5)
- [ ] **C05** — Outil éditeur : marquer les surfaces de pose + les points d'apparition du portail (S4)
- [ ] **C06** — Capture : gel du méchant, état « capturé », caméra spectateur (S4)
- [ ] **C07** — Portail : apparition au hasard parmi les points, trigger → `NotifyEnteredPortal`, FX (S4)
- [ ] **C08** — Photos temporelles : 5 photos max (render texture) pendant la cache, album consultable pendant la recherche (S5)
- [ ] **C09** — Auto-placement des objets non posés à la fin de la cache (S5)
- [ ] **C10** — Flux de fin de partie : données de victoire → écran de fin (avec Olivier) → retour au lobby (S6)
- [ ] **C11** — Briefing / tuto des rôles en début de partie (S6)
- [ ] **C12** — Données : `AnachronismDefinition` (taille, prefab), `EraDefinition`, préréglages `MatchSettings` en ScriptableObjects (S3)
- [ ] **C13** — Juice : feedback du tir, de la capture, d'un objet trouvé (S7)
- [ ] **C14** — Équilibrage des valeurs avec Théo (S8)

---

## Olivier — PNJ, monde & présentation

- [ ] **O01** — PNJ : NavMeshAgent, déplacement entre points d'intérêt, arrêts + idle (S2–S3)
- [ ] **O02** — Animator piloté **uniquement par la vitesse** (réutilisé tel quel par le méchant) (S3)
- [ ] **O03** — Spawner de foule : densité par zone, nombre selon les joueurs. Perf à 60 PNJ côté hôte (S4–S5)
- [ ] **O04** — Outil éditeur : points d'intérêt / zones PNJ (S3)
- [ ] **O05** — HUD : rôle, phase, chrono, munitions, objets restants, jauge de dégradation (S3)
- [ ] **O06** — Menu principal + UI lobby : joueurs, réglages hôte, lancer, inviter (S4)
- [ ] **O07** — Dégradation v1 : post-process / shader piloté par `Degradation` (0..1) (S4)
- [ ] **O08** — Glitchs : événements seedés par l'hôte (PNJ dupliqués, flicker, objets qui changent) (S5)
- [ ] **O09** — Audio : musique de l'époque, SFX, mixer, musique qui se dégrade (S5)
- [ ] **O10** — Options : audio, sensibilité, micro / push-to-talk, graphismes (S6)
- [ ] **O11** — Écran de fin (S6)
- [ ] **O12** — Polish UI + FX de dégradation (S7)
- [ ] **O13** — Caméra libre spectateur pour les trailers et les clips (S8)

---

## Studio #2 — Mises à jour

Le contenu exact se décide en **S13**, à partir des avis Steam et des bugs remontés. Ne figez rien avant.

- [ ] **U00** (tous) — Tri des retours : bugs classés par gravité, top 3 des demandes des joueurs (S13)
- [ ] **U01** (Célian) — Améliorations gameplay de la 1.1 (S14)
- [ ] **U02** (Olivier) — Améliorations UI / PNJ de la 1.1 (S14)
- [ ] **T16** (Théo) — Build MAJ 1.1 + patch notes (S15)
- [ ] **U03** (Célian) — Nouveaux anachronismes / mécanique de la 1.2 (S16)
- [ ] **U04** (Olivier) — Dégradation, glitchs et audio de l'époque 2, sélection de map (S16–S17)
- [ ] **T17** (Théo) — Build MAJ 1.2 + patch notes (S18)
