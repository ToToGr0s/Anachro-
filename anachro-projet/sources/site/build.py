"""Assemble le Worker déployé (worker.dist.js) à partir des sources du site.

Usage, depuis n'importe quel dossier :  python build.py
Puis coller worker.dist.js dans Cloudflare (Edit code → Ctrl+A → Ctrl+V → Deploy).

Entrées (chemins relatifs à ce fichier) :
- sheet/part1.html, sheet/regles.html, sheet/app_part2.html : l'appli (/) ;
- src/admin_body.html, src/admin.js, src/admin_extra.css : l'admin (/admin) ;
- seed/taches.json : les tâches insérées au premier lancement (base vide) ;
- src/worker.js + src/discord.js : le code du Worker.
"""

import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent
SHEET, SRC = ROOT / "sheet", ROOT / "src"


def read(path):
    return path.read_text(encoding="utf-8")


p1 = read(SHEET / "part1.html")
head_end = p1.index("</style>") + len("</style>")
head, body1 = p1[:head_end], p1[head_end:]
DOC = ('<!doctype html><html lang="fr"><head><meta charset="utf-8">'
       '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">'
       '<meta name="robots" content="noindex">{head}</head><body>{body}</body></html>')

app = DOC.format(head=head, body=body1 + read(SHEET / "regles.html") + read(SHEET / "app_part2.html"))

style = p1[p1.index("<style>"):head_end].replace("</style>", read(SRC / "admin_extra.css") + "</style>")
links = p1[p1.index("<link"):p1.index("<style>")]
admin = DOC.format(head="<title>Anachro Admin</title>" + links + style,
                   body=read(SRC / "admin_body.html") + "<script>" + read(SRC / "admin.js") + "</script>")

seed = json.loads(read(ROOT / "seed" / "taches.json"))

inject = ("const APP_HTML = " + json.dumps(app, ensure_ascii=False) + ";\n"
          "const ADMIN_HTML = " + json.dumps(admin, ensure_ascii=False) + ";\n"
          "const SEED = " + json.dumps(seed, ensure_ascii=False, separators=(",", ":")) + ";\n")
worker = read(SRC / "worker.js").replace("/*__INJECT__*/", inject + read(SRC / "discord.js"))

out = ROOT / "worker.dist.js"
out.write_text(worker, encoding="utf-8", newline="\n")
print(f"{out.name} : {len(worker.encode())} octets, {len(seed)} tâches dans le seed")
