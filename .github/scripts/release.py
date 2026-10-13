"""Calcule la prochaine version, crée la release GitHub et l'annonce sur Discord.

Convention des titres (PR en « Squash and merge ») : `type: sujet`
  feat → nouvelle version mineure (0.X.0)   | fix, perf, refactor, ld, art, docs, chore… → correctif (0.x.Y)
  `type!: sujet` ou « BREAKING CHANGE » → mineure aussi tant qu'on est en 0.x (pas de 1.0 automatique).
"""
import json, os, re, subprocess, urllib.request

def sh(*a):
    return subprocess.run(a, capture_output=True, text=True).stdout.strip()

last = sh("git", "describe", "--tags", "--abbrev=0", "--match", "v*")
rng = f"{last}..HEAD" if last else "HEAD"
subjects = [s for s in sh("git", "log", rng, "--no-merges", "--pretty=format:%s").splitlines() if s.strip()]
if not subjects:
    print("Aucun nouveau commit depuis", last or "le début : pas de release.")
    raise SystemExit(0)

major, minor, patch = (int(x) for x in (last[1:] if last else "0.0.0").split("."))
TYPE = re.compile(r"^(\w+)(?:\([^)]*\))?(!)?:\s*(.+)$")
groups = {}
bump_minor = False
for s in subjects:
    m = TYPE.match(s)
    t, bang, text = (m.group(1).lower(), m.group(2), m.group(3)) if m else ("autre", None, s)
    if t == "feat" or bang or "BREAKING CHANGE" in s:
        bump_minor = True
    groups.setdefault(t, []).append(text)
PUBLISH = {"feat", "fix", "perf", "refactor", "ld", "art"}
if not bump_minor and not (PUBLISH & set(groups)):
    print("Seulement docs / chore / autre depuis", last or "le début : pas de nouvelle version.")
    raise SystemExit(0)
if bump_minor:
    minor, patch = minor + 1, 0
else:
    patch += 1
new = f"v{major}.{minor}.{patch}"

LABEL = {"feat": "✨ Nouveautés", "fix": "🐛 Corrections", "perf": "⚡ Performances", "refactor": "♻️ Refactorisation",
         "ld": "🗺️ Level design", "art": "🎨 Art", "docs": "📝 Docs", "chore": "🔧 Maintenance", "autre": "📦 Autre"}
order = list(LABEL) + sorted(k for k in groups if k not in LABEL)
lines = [f"## {new}", ""]
for k in order:
    if k in groups:
        lines += [f"### {LABEL.get(k, k)}"] + [f"- {x}" for x in groups[k]] + [""]
notes = "\n".join(lines)
open("notes.md", "w", encoding="utf-8").write(notes)

subprocess.run(["gh", "release", "create", new, "--title", new, "--notes-file", "notes.md",
                "--target", os.environ.get("GITHUB_SHA", "HEAD")], check=True)
print("Release créée :", new)

webhook = os.environ.get("WEBHOOK", "")
if webhook:
    payload = {"username": "Release", "allowed_mentions": {"parse": []},
               "embeds": [{"title": f"🚀 {new} sur main", "url": f"{os.environ.get('REPO_URL','')}/releases/tag/{new}",
                           "description": notes[:3800], "color": 0x8B3FB8}]}
    req = urllib.request.Request(webhook, data=json.dumps(payload).encode(),
                                 headers={"content-type": "application/json", "user-agent": "anachro-release"})
    urllib.request.urlopen(req, timeout=15)
    print("Annonce Discord envoyée.")
