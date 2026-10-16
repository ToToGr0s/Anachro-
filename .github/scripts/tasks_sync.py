"""Coche sur le site de suivi les tâches d'une PR mergée dans Dev.

Lancé par .github/workflows/tasks-sync.yml. Lit la description de la PR :
- section « ## Tâche(s) du planning » : identifiants T02-5, T03, C04-1… ; un identifiant de tâche entière
  (T03) coche toutes ses sous-tâches. Une ligne qui contient « partiel », « reste », « correctif »,
  « préparation » ou « aide » n'est pas cochée (ex. « T02 (partiel : …) », « Préparation de T13 ») ;
- checklist : si une ligne « - [ ] Testé … » n'est pas cochée, rien n'est coché
  (règle de TACHES.md : une tâche est faite quand elle est mergée sur Dev ET testée en réseau).

Sans secret ou sans URL configurés, le script prévient et sort sans erreur : la PR reste mergée.
DRY_RUN=1 affiche ce qui serait envoyé, sans rien appeler.
"""

import json
import os
import re
import sys
import urllib.error
import urllib.request

SECTION = re.compile(r"^##\s*T[âa]che\(s\) du planning\s*$(.*?)(?=^##\s|\Z)", re.M | re.S | re.I)
COMMENT = re.compile(r"<!--.*?-->", re.S)
# Préfixes des tâches du site : Art, Célian, LD, Olivier, Production, Théo (online), Updates.
TASK = re.compile(r"\b([ACLOPTU]\d{2}[a-z]?(?:-\d+)?)\b")
# Mots qui disent que la tâche n'est pas finie, où qu'ils soient sur la ligne (avant ou après l'identifiant).
NOT_DONE = re.compile(r"partiel|reste|correctif|pr[ée]paration|aide", re.I)
UNCHECKED_TEST = re.compile(r"^\s*[-*]\s*\[ \]\s*Test", re.M | re.I)


def parse(body):
    """Renvoie (ids à cocher, ids ignorés, tests non cochés ?)."""
    body = COMMENT.sub("", body or "")
    section = SECTION.search(body)
    ids, skipped = [], []
    if section:
        for line in section.group(1).splitlines():
            target = skipped if NOT_DONE.search(line) else ids
            for task_id in TASK.findall(line):
                if task_id not in target:
                    target.append(task_id)
    return ids, skipped, bool(UNCHECKED_TEST.search(body))


def comment(text):
    token, repo, number = os.environ.get("GH_TOKEN"), os.environ.get("REPO"), os.environ.get("PR_NUMBER")
    if not (token and repo and number) or os.environ.get("DRY_RUN"):
        print("[commentaire]", text)
        return
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repo}/issues/{number}/comments",
        data=json.dumps({"body": text}).encode(),
        headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json"},
        method="POST")
    urllib.request.urlopen(request, timeout=20).close()


def main():
    ids, skipped, has_unchecked_test = parse(os.environ.get("PR_BODY", ""))
    note_skipped = f"\nNon cochées (partiel / correctif) : {', '.join(skipped)}" if skipped else ""

    if not ids:
        print("Aucune tâche à cocher." + note_skipped)
        return 0

    if has_unchecked_test:
        comment("📋 **Synchro des tâches** : rien n'a été coché, une case « Testé » de la checklist n'est pas cochée. "
                f"Tâches concernées : {', '.join(ids)}. Coche-les sur le site après le test.{note_skipped}")
        return 0

    site, token = os.environ.get("SITE_URL", "").rstrip("/"), os.environ.get("SYNC_TOKEN", "")
    payload = {"ids": ids, "pr": int(os.environ.get("PR_NUMBER") or 0) or None}
    if os.environ.get("DRY_RUN"):
        print("[envoi]", json.dumps(payload), note_skipped)
        return 0

    if not site or not token:
        print("::warning::Synchro des tâches non configurée (variable ANACHRO_SITE_URL ou secret ANACHRO_SYNC_TOKEN manquant).")
        return 0

    request = urllib.request.Request(
        f"{site}/api/sync/tasks",
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json", "User-Agent": "anachro-tasks-sync"},
        method="POST")
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        # Le message du site (jeton invalide, route absente…) aide plus que le code HTTP seul.
        message = error.read().decode(errors="replace")[:300]
        comment(f"⚠️ **Synchro des tâches** : le site a refusé ({error.code}) : `{message}`. Coche {', '.join(ids)} à la main.")
        print(f"::error::Synchro refusée ({error.code}) : {message}")
        return 1

    lines = ["📋 **Synchro des tâches**"]
    if result.get("done"):
        lines.append(f"✅ Cochées sur le site : {', '.join(result['done'])}")
    if result.get("already"):
        lines.append(f"Déjà faites : {', '.join(result['already'])}")
    if result.get("unknown"):
        lines.append(f"⚠️ Introuvables sur le site : {', '.join(result['unknown'])}")
    if skipped:
        lines.append(f"Non cochées (partiel / correctif) : {', '.join(skipped)}")
    comment("\n".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main())
