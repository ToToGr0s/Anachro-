# Mise en place : GitHub + Discord + review Claude

Temps : environ 20 minutes, une seule fois. Fais les étapes dans l'ordre.

---

## 1. Discord : créer 4 salons et 4 webhooks

Salons : `#planning`, `#git`, `#review`, `#releases`.

Pour chacun : **clic droit sur le salon → Modifier le salon → Intégrations → Webhooks → Nouveau webhook → Copier l'URL du webhook.**
Garde les 4 adresses sous la main (dans un bloc-notes, pas dans un message Discord).

---

## 2. GitHub : créer le dépôt

1. Sur github.com → **New repository** → nom au choix (ex : `anachro`) → **Private** → Create.
2. **Settings → Collaborators** → ajoute Célian, Olivier, Hugo, JB.
3. Copie à la racine du dépôt tout le contenu de ce dossier (`.github/`, `CLAUDE.md`, `.gitignore`, `.gitattributes`), puis :
   ```bash
   git lfs install
   git add .
   git commit -m "chore: configuration du dépôt"
   git push origin main
   git checkout -b Dev && git push -u origin Dev
   ```

---

## 3. Le jeton Claude (ton abonnement)

1. Installe Claude Code sur ton PC si ce n'est pas fait : `npm install -g @anthropic-ai/claude-code`
2. Lance : `claude setup-token` → connecte-toi → copie le jeton affiché.
3. GitHub → ton dépôt → **Settings → Secrets and variables → Actions → New repository secret** :
   - `CLAUDE_CODE_OAUTH_TOKEN` = le jeton

⚠️ Chaque review (donc **chaque push sur une PR**, de n'importe qui) consomme **ton** quota Claude. Si tu atteins ta limite, les reviews échouent jusqu'au reset et Discord affiche « La review n'a pas abouti ». Conseil à l'équipe : pousser par blocs cohérents, pas à chaque ligne.

---

## 4. Les secrets Discord dans GitHub

Même endroit (**Settings → Secrets and variables → Actions**) :
- `DISCORD_WEBHOOK_REVIEW` = webhook de `#review`
- `DISCORD_WEBHOOK_RELEASES` = webhook de `#releases`

---

## 5. Commits / PR dans `#git` (aucun code)

GitHub → **Settings → Webhooks → Add webhook** :
- **Payload URL** : le webhook de `#git` **+ `/github` à la fin** (ex : `https://discord.com/api/webhooks/123/abc/github`)
- **Content type** : `application/json`
- **Let me select individual events** → coche : *Pull requests*, *Pull request reviews*, *Pushes* (décoche *Pushes* si c'est trop bavard)

---

## 6. Protéger `main` et `Dev` (Théo valide tout) — DÉJÀ FAIT

Ruleset `protection` (main + Dev) : PR obligatoire (1 approbation), **seul l'admin (Théo) peut mettre à jour ces branches, uniquement via une PR**, suppression et force-push bloqués. Squash merge uniquement, message = titre de la PR. Actions ne peut pas approuver de PR.

⚠️ **Sur un dépôt privé gratuit, GitHub n'applique pas les rulesets.** Pour les activer : passe ton compte en GitHub Pro, gratuit pour les étudiants via **education.github.com → Student Developer Pack** (vérification avec l'adresse de l'école). Le ruleset existant s'active tout seul ensuite. En attendant, c'est la discipline de l'équipe qui protège main/Dev.

---

## 7. Le planning dans `#planning` (site Cloudflare)

1. Colle le nouveau `worker.js` dans Cloudflare (Edit code → Ctrl+A → Ctrl+V → **Deploy**).
2. Worker `anacro-pro` → **Settings → Triggers → Cron Triggers → Add** → `*/15 * * * *` → Add.
   (Le Worker se réveille toutes les 15 min et décide lui-même : lundi 8h45, vendredi 16h30, jalons à 9h, heure de Paris.)
3. Sur le site → **/admin → onglet Discord** :
   - colle le webhook de `#planning`,
   - colle l'identifiant Discord de chacun (Discord → Paramètres → Avancés → **Mode développeur**, puis clic droit sur la personne → *Copier l'identifiant de l'utilisateur*),
   - **Enregistrer**, puis **Envoyer un test**.

---

## 8. Règles pour l'équipe (à leur envoyer)

- Une branche par sujet : `Feat-<sujet>` ou `Fix-<sujet>`, créée depuis `Dev` → PR vers `Dev`.
- **Titre de PR = `type: sujet`** : `feat:` (nouveau), `fix:` (correction), `perf:`, `refactor:`, `ld:`, `art:`, `docs:`, `chore:`.
- Le bot commente chaque push : lisez sa review, corrigez, poussez. **Il ne valide rien** : c'est Théo qui approuve et merge.
- Une fois `Dev` stable → PR `Dev` → `main` : à chaque merge sur `main`, une version `v0.x.y` est créée avec son changelog et annoncée dans `#releases`.

---

## Premier test (5 min)

1. Crée une branche `Feat-test` depuis `Dev`, modifie un `.cs` (même bidon), ouvre une PR vers `Dev` avec le titre `chore: test du bot`.
2. Attends 2 à 5 min : un commentaire « 🤖 Review Claude » apparaît sur la PR et un résumé dans `#review`.
3. Si rien : onglet **Actions** du dépôt → ouvre l'exécution en échec et envoie-moi le message d'erreur.
