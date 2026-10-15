# Anachro — contexte du projet (mis à jour le 14 octobre 2026)

## Le jeu en bref
Party game Unity 3D low poly, 4 à 8 joueurs en ligne, l'hôte fait serveur, sortie Steam fin mars 2027 (objectif 4 000 ventes). Méchants déguisés en PNJ qui cachent 4 anachronismes ; gentils qui les cherchent, gagnent des munitions et capturent les méchants. Règles complètes dans les instructions du projet.

## Équipe et rôles
| Qui | Rôle |
|---|---|
| Théo | Online (FishNet, Steam, vocal, validation serveur, builds) + level design. Valide et merge toutes les PR. Admin du site. |
| Célian | Joueur & gameplay : contrôleur, pistolet, pose, capture, portail, photos, data |
| Olivier | PNJ, monde, UI, menus, audio, dégradation |
| Hugo | Persos & animations |
| JB | Décors, props, anachronismes, visuels Steam |
| Xavier | École, spectateur du site (lecture seule) |

## Décisions techniques figées
- Unity + **FishNet v4**, l'hôte fait serveur. Le client envoie des intentions, l'hôte décide de tout (rôles, tirs, munitions, capture, victoire).
- Réseau : **Steam** (FishySteamworks, AppID 480 en dev) pour le jeu en ligne ; **Tugboat** pour le dev local et le LAN (Multipass pour passer de l'un à l'autre).
- **Vocal de proximité : PAS celui de Steam.** Vocal maison qui passe par FishNet (micro Unity + Opus via Concentus ou UniVoice, AudioSource 3D, l'hôte ne relaie la voix qu'aux joueurs proches). Prototype dès le bloc 1. Plan B si la qualité coince : Vivox.
- Aucun indicateur de parole au-dessus des têtes.
- 1 map à la sortie, la 2e en MAJ 1.2 (juin).

## Planning (sprints = semaines studio de 30 h/dev)
| Bloc | Sprints | Jalon |
|---|---|---|
| Bloc 1 — fondations | S1–S3 (5 → 23 oct) | S3 : boucle en LAN |
| *pas de studio* | 26 oct → 27 nov | |
| Bloc 2 — jeu complet | S4–S6 (30 nov → 18 déc) | S4 : 1re partie en ligne · S6 : core gelé + page Steam |
| *pas de studio* | vacances + 4-8 jan | |
| Bloc 3 — démo | S7–S9 (11 → 29 jan) | S9 : démo Next Fest |
| *pas de studio* | 1 fév → 5 mars | |
| Bloc 4 — sortie | S10–S12 (8 → 26 mars) | S11 : release candidate · S12 : SORTIE |
| Studio #2 | S13–S15 (avril) | MAJ 1.1 |
| Studio #2 | S16–S18 (juin) | MAJ 1.2 (map 2) + jury |

297 tâches détaillées dans `sources/site/seed/taches.json` et en ligne sur le site.

## Outils en place (tout fonctionne, testé le 14 oct)
**Site de suivi** — https://anacro-pro.contact-davcel.workers.dev
- Cloudflare Worker `anacro-pro` + base D1 `anachro-prod` (liaison `DB`), cron toutes les 15 min.
- Connexion par nom + code. `/admin` : activité (qui a fait quoi, quand, avec annulation), par personne, comptes & codes, liens (GDD…), Discord.
- Onglets : Cette semaine, Planning, Toutes les tâches (liste repliable), Jalons, Infos, Code & règles. Clic sur un sprint = fenêtre de détail. Lien direct : `/#sprint-S4`.
- Pour modifier le site : demander à Claude, puis coller le nouveau `worker.js` dans Cloudflare (Edit code → Ctrl+A → coller → Deploy).

**GitHub** — ToToGr0s/Anachro- (privé)
- Branches `main` (buildable) et `develop` (intégration) ; travail sur `feature/prenom-sujet`, PR vers `develop`.
- Titre de PR = `type: sujet` (feat, fix, perf, refactor, ld, art, docs, chore). Squash merge uniquement.
- **Review Claude** sur chaque push de PR : commentaire en français (verdict, problèmes 🔴🟠🟡 avec ligne et correction, ce qui manque, à tester) + résumé dans #review. Elle n'approuve jamais : Théo approuve et merge.
- **Release auto** à chaque merge sur `main` : tag `v0.x.y` + changelog + annonce dans #releases (feat → 0.X.0, sinon 0.x.Y, rien si seulement chore/docs).
- Règles du code lues par la review : `CLAUDE.md` à la racine du dépôt.
- Secrets : `CLAUDE_CODE_OAUTH_TOKEN` (abonnement de Théo, expire en octobre 2027), `DISCORD_WEBHOOK_REVIEW`, `DISCORD_WEBHOOK_RELEASES`.
- Ruleset « protection » sur main/develop (seul l'admin merge, via PR) : **ne s'applique qu'avec GitHub Pro** (gratuit via le GitHub Student Developer Pack).

**Discord** — salons #planning (récap lundi 8h45, bilan vendredi 16h30, alertes jalons J-3/J-1, tâche bloquée avec mention), #git (PR et pushes), #review, #releases.

## Reste à faire / à vérifier
- [ ] Fermer la PR #1 « chore: test du bot » **sans merger** et supprimer la branche `feature/theo-test-bot`.
- [ ] Inviter Célian, Olivier, Hugo, JB sur le dépôt (Settings → Collaborators) ; Hugo et JB : `git lfs install` avant de cloner.
- [ ] Donner à chacun son code d'accès au site (/admin → Comptes & codes).
- [ ] Activer GitHub Pro via GitHub Education (sinon tout le monde peut pousser sur main).
- [ ] Mettre le lien du GDD dans /admin → Liens.
- [ ] Mettre à jour les tâches vocal du planning (vocal maison, prototype en bloc 1).
- [ ] Vérifier la date limite d'inscription au Steam Next Fest de février.

## Points de vigilance
- Chaque push sur une PR consomme une review sur l'abonnement de Théo : pousser par blocs.
- Steam : page « bientôt disponible » au moins 2 semaines avant la sortie, review du build = plusieurs jours. Page en ligne fin S6.
- Pas de semaine studio en février : la démo doit être finie le 29 janvier.
- Fin de chaque bloc : `main` buildable + une note sur où on en est.

## Contenu de `sources/`
- `docs-jeu/` : README, PLANNING, TACHES, ARCHITECTURE et premiers scripts C# (MatchManager, NpcAgent…).
- `github/` : tous les fichiers du dépôt (workflows, scripts, CLAUDE.md…) + SETUP.md.
- `site/` : sources du site, `worker.dist.js` prêt à coller, tâches en JSON.
