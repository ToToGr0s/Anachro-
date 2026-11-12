"""Poste dans #releases le lien de téléchargement d'un build (pré-release build-… créée par Tools/publish-build.ps1)."""
import json, os, urllib.request

release = json.load(open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8"))["release"]
# Le repo est privé : le lien ne marche que pour les membres connectés à GitHub.
links = [f"📦 [{a['name']}]({a['browser_download_url']}) ({round(a['size'] / 1024 / 1024)} Mo)" for a in release.get("assets", [])]
description = "\n".join(links + ["", release.get("body") or ""])

webhook = os.environ.get("WEBHOOK", "")
if not webhook:
    print("Pas de webhook DISCORD_WEBHOOK_RELEASES : rien envoyé.")
    raise SystemExit(0)
payload = {"username": "Release", "allowed_mentions": {"parse": []},
           "embeds": [{"title": f"🎮 {release['name']}", "url": release["html_url"],
                       "description": description[:3800], "color": 0x2E8B57}]}
req = urllib.request.Request(webhook, data=json.dumps(payload).encode(),
                             headers={"content-type": "application/json", "user-agent": "anachro-build"})
urllib.request.urlopen(req, timeout=15)
print("Annonce Discord envoyée :", release["tag_name"])
