"""Coche sur le site de suivi les tâches d'une PR mergée dans Dev.

Lancé par .github/workflows/tasks-sync.yml. Lit la description de la PR :
- section « ## Tâche(s) du planning » : seules les lignes qui COMMENCENT par un identifiant comptent
  (« T02-5 », « T03, C04-1 », « - L01 ») ; le texte libre (« Aucune », « outil pour P01 ») et ce qui suit
  la liste (« T03 (suite de C04) ») sont ignorés.
  Un identifiant de tâche entière (T03) coche toutes ses sous-tâches ;
- une ligne qui contient « partiel », « reste », « correctif », « préparation » ou « aide » n'est pas cochée ;
- checklist : il faut au moins une case « - [x] Testé … » cochée et aucune « - [ ] Testé … » vide
  (règle de TACHES.md : une tâche est faite quand elle est mergée sur Dev ET testée en réseau).

Cochage manuel : un commit poussé sur la branche Sync-taches avec le message « sync: T01, T03-1 »
(MANUAL_IDS) coche ces tâches sans passer par une PR ; à faire seulement quand Théo l'a validé.

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
TASK_ID = r"[ACLOPTU]\d{2}[a-z]?(?:-\d+)?"
TASK = re.compile(rf"\b({TASK_ID})\b")
# Liste d'identifiants en tête de ligne (« T03, C04-1 ») : ce qui suit (« (suite de C04) », « voir P01 ») est ignoré.
TASK_LIST = re.compile(rf"^\s*(?:[-*]\s*)?({TASK_ID}(?:\s*[,+/&]\s*{TASK_ID})*)\b")
# Mots entiers : « reste » et « aide » comptent, « restera » ou « aidera » non.
NOT_DONE = re.compile(r"\b(?:partiel(?:le)?s?|reste|correctifs?|pr[ée]paration|aide)\b", re.I)
MANUAL = re.compile(r"^\s*sync\s*:\s*(.+)$", re.I | re.M)
CHECKED_TEST = re.compile(r"^\s*[-*]\s*\[[xX]\]\s*Test", re.M)
UNCHECKED_TEST = re.compile(r"^\s*[-*]\s*\[ \]\s*Test", re.M)


def parse(body):
    """Renvoie (ids à cocher, ids ignorés, raison de ne rien cocher ou None)."""
    body = COMMENT.sub("", body or "")
    section = SECTION.search(body)
    ids, skipped = [], []
    if section:
        for line in section.group(1).splitlines():
            head = TASK_LIST.match(line)
            if not head:
                continue
            target = skipped if NOT_DONE.search(line) else ids
            for task_id in TASK.findall(head.group(1)):
                if task_id not in target:
                    target.append(task_id)

    if UNCHECKED_TEST.search(body):
        blocker = "une case « Testé » de la checklist n'est pas cochée"
    elif not CHECKED_TEST.search(body):
        blocker = "aucune case « Testé » cochée dans la checklist"
    else:
        blocker = None
    return ids, skipped, blocker


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
    try:
        urllib.request.urlopen(request, timeout=20).close()
    except (urllib.error.URLError, TimeoutError) as error:
        # Le résultat de la synchro reste dans le journal de l'Action, même sans commentaire.
        print(f"::warning::Commentaire non publié ({error}) :\n{text}")


def send(site, token, payload):
    """Renvoie (résultat JSON, None) ou (None, message d'erreur lisible)."""
    request = urllib.request.Request(
        f"{site}/api/sync/tasks",
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json", "User-Agent": "anachro-tasks-sync"},
        method="POST")
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return json.load(response), None
    except urllib.error.HTTPError as error:
        # Le détail du site reste dans le journal de l'Action : le commentaire de PR, visible par tous, n'a que le code.
        print(f"Réponse du site : {error.read().decode(errors='replace')[:300]}")
        return None, f"le site a refusé (HTTP {error.code}, détail dans le journal de l'Action)"
    except (urllib.error.URLError, TimeoutError) as error:
        return None, f"site injoignable ({error})"
    except json.JSONDecodeError:
        return None, "réponse du site illisible (pas du JSON : mauvaise URL ou Worker pas à jour ?)"


def parse_manual(message):
    """« sync: T01, T03-1 » → [T01, T03-1]. Faux préfixe ou liste vide : rien (un commit ordinaire ne coche rien)."""
    match = MANUAL.match(message or "")
    return list(dict.fromkeys(TASK.findall(match.group(1)))) if match else []


def main():
    if "MANUAL_IDS" in os.environ:
        # Demande explicite (validée par Théo) : pas de PR, donc pas de checklist « Testé » à lire.
        ids, skipped, blocker = parse_manual(os.environ["MANUAL_IDS"]), [], None
        if not ids:
            print("::warning::Message attendu : « sync: T01, T03-1 ». Rien n'a été coché.")
            return 0
    else:
        ids, skipped, blocker = parse(os.environ.get("PR_BODY", ""))
    note_skipped = f"\nNon cochées (partiel / correctif…) : {', '.join(skipped)}" if skipped else ""

    if not ids:
        print("Aucune tâche à cocher." + note_skipped)
        return 0

    if blocker:
        comment(f"📋 **Synchro des tâches** : rien n'a été coché, {blocker}. "
                f"Tâches concernées : {', '.join(ids)}. Coche-les sur le site après le test.{note_skipped}")
        return 0

    payload = {"ids": ids, "pr": int(os.environ.get("PR_NUMBER") or 0) or None}
    if os.environ.get("DRY_RUN"):
        print("[envoi]", json.dumps(payload), note_skipped)
        return 0

    site, token = os.environ.get("SITE_URL", "").rstrip("/"), os.environ.get("SYNC_TOKEN", "")
    if not site or not token:
        print("::warning::Synchro des tâches non configurée (variable ANACHRO_SITE_URL ou secret ANACHRO_SYNC_TOKEN manquant).")
        return 0

    result, error = send(site, token, payload)
    if error:
        comment(f"⚠️ **Synchro des tâches** : {error}. Rien n'a été coché, coche {', '.join(ids)} à la main.")
        print(f"::error::Synchro échouée : {error}")
        return 1

    lines = ["📋 **Synchro des tâches**"]
    if result.get("done"):
        lines.append(f"✅ Cochées sur le site : {', '.join(result['done'])}")
    if result.get("already"):
        lines.append(f"Déjà faites : {', '.join(result['already'])}")
    if result.get("unknown"):
        lines.append(f"⚠️ Introuvables sur le site : {', '.join(result['unknown'])}")
    if skipped:
        lines.append(f"Non cochées (partiel / correctif…) : {', '.join(skipped)}")
    comment("\n".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main())
