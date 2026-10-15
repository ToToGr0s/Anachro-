"""Poste le résumé de la review Claude dans le salon Discord #review."""
import json, os, urllib.request

webhook = os.environ.get("WEBHOOK", "")
if not webhook:
    print("Pas de webhook DISCORD_WEBHOOK_REVIEW : rien à envoyer.")
    raise SystemExit(0)

pr_url, title = os.environ["PR_URL"], os.environ["PR_TITLE"]
author, number = os.environ["PR_AUTHOR"], os.environ["PR_NUMBER"]
sha = os.environ.get("HEAD_SHA", "")[:7]

LOOK = {
    "pret": ("✅ Prête pour la relecture de Théo", 0x1F8A5B),
    "corriger": ("⚠️ À corriger avant relecture", 0xB7860B),
    "bloquant": ("⛔ Bloquante", 0xC0262D),
}

try:
    s = json.load(open("review-summary.json", encoding="utf-8"))
    label, color = LOOK.get(s.get("verdict"), ("❔ Verdict inconnu", 0x5D6375))
    counts = f"🔴 {s.get('bloquants', 0)} · 🟠 {s.get('importants', 0)} · 🟡 {s.get('mineurs', 0)}"
    points = "\n".join(f"• {p}" for p in (s.get("points") or [])[:3])
    desc = f"**{label}**\n{s.get('resume', '')}\n\n{counts}" + (f"\n\n{points}" if points else "")
except Exception:
    color = 0x5D6375
    desc = ("La review n'a pas abouti (quota Claude atteint, erreur ou review annulée par un nouveau push).\n"
            f"[Voir l'exécution]({os.environ.get('RUN_URL', '')})")

payload = {
    "username": "Review Claude",
    "allowed_mentions": {"parse": []},
    "embeds": [{
        "title": f"PR #{number} · {title}"[:250],
        "url": pr_url,
        "description": desc[:3800],
        "color": color,
        "footer": {"text": f"par {author} · commit {sha} · avis seulement, Théo valide"},
    }],
}
req = urllib.request.Request(webhook, data=json.dumps(payload).encode(), headers={"content-type": "application/json", "user-agent": "anachro-review"})
urllib.request.urlopen(req, timeout=15)
print("Résumé envoyé dans Discord.")
