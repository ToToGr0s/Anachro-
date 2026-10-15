import json, os, re
S = '/tmp/claude-0/sheet/'; C = '/tmp/claude-0/cf/src/'
p1 = open(S + 'part1.html').read()
head_end = p1.index('</style>') + len('</style>')
head, body1 = p1[:head_end], p1[head_end:]
head = head.replace('<title>Anachro Prod</title>', '<title>Anachro Prod</title>')
DOC = '<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="robots" content="noindex">{head}</head><body>{body}</body></html>'

app = DOC.format(head=head, body=body1 + open(S + 'regles.html').read() + open(S + 'app_part2.html').read())

style = p1[p1.index('<style>'):head_end].replace('</style>', open(C + 'admin_extra.css').read() + '</style>')
links = p1[p1.index('<link'):p1.index('<style>')]
admin = DOC.format(head='<title>Anachro Admin</title>' + links + style,
                   body=open(C + 'admin_body.html').read() + '<script>' + open(C + 'admin.js').read() + '</script>')

seed = []
d = '/tmp/claude-0/seed/docs/'
for f in sorted(os.listdir(d)):
    t = json.load(open(d + f)); t['id'] = f[:-5]; seed.append(t)

inject = ('const APP_HTML = ' + json.dumps(app, ensure_ascii=False) + ';\n'
          'const ADMIN_HTML = ' + json.dumps(admin, ensure_ascii=False) + ';\n'
          'const SEED = ' + json.dumps(seed, ensure_ascii=False, separators=(',', ':')) + ';\n')
w = open(C + 'worker.js').read().replace('/*__INJECT__*/', inject + open(C + 'discord.js').read())
os.makedirs('/tmp/claude-0/cf/dist', exist_ok=True)
open('/tmp/claude-0/cf/dist/worker.js', 'w').write(w)
open('/tmp/claude-0/cf/dist/app.html', 'w').write(app)
open('/tmp/claude-0/cf/dist/admin.html', 'w').write(admin)
print('worker.js', len(w.encode()), 'octets ;', len(seed), 'tâches')
