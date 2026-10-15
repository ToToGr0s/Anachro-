s = open('/tmp/claude-0/sheet/part2.html').read()

def rep(a, b):
    global s
    assert s.count(a) == 1, a[:70]
    s = s.replace(a, b)

# 1. statut
rep('''  if(!db || !canWrite) return;
  try{ await db.doc("tasks/"+id).update({s}); }catch(e){ note("Le statut n'a pas été enregistré ("+(e?.code||"erreur")+")."); }
}''', '''  try{ await api("/api/tasks/"+encodeURIComponent(id),{method:"PATCH",body:JSON.stringify({s})}); load(); }catch(e){ if(e.message!=="auth") note("Le statut n'a pas été enregistré : "+e.message); }
}''')

# 2. formulaire de renommage -> lien admin
i = s.index('  if(canWrite){\n    const f = el("form","rename");')
j = s.index('    p.append(f);\n  }\n', i) + len('    p.append(f);\n  }\n')
s = s[:i] + '  if(me && me.role==="admin") p.append(el("p","note","Pour renommer les artistes ou donner les codes d’accès : <a href=\\"/admin\\">page admin</a>."));\n' + s[j:]

# 3. suppression / enregistrement
rep('''    try{ await db.doc("tasks/"+d.id).delete(); }catch(e){ note("La suppression a échoué."); }''',
    '''    try{ await api("/api/tasks/"+encodeURIComponent(d.id),{method:"DELETE"}); load(); }catch(e){ if(e.message!=="auth") note("La suppression a échoué : "+e.message); }''')
rep('''    const id = d.id || ("x"+Date.now().toString(36)+Math.random().toString(36).slice(2,5));
    const doc = {id, g, gt, t:title, who:$("#e-who").value, wk:$("#e-wk").value, cat:$("#e-cat").value, s:$("#e-s").value, o};
    tasks.set(id, doc); close(); scheduleRender();
    try{ await db.doc("tasks/"+id).set(body(doc)); }catch(e2){ note("La tâche n'a pas été enregistrée ("+(e2?.code||"erreur")+")."); }''',
    '''    const doc = {g, gt, t:title, who:$("#e-who").value, wk:$("#e-wk").value, cat:$("#e-cat").value, s:$("#e-s").value, o};
    if(!isNew){ tasks.set(d.id,{...d,...doc}); }
    close(); scheduleRender();
    try{
      if(isNew) await api("/api/tasks",{method:"POST",body:JSON.stringify(doc)});
      else await api("/api/tasks/"+encodeURIComponent(d.id),{method:"PATCH",body:JSON.stringify(doc)});
      load();
    }catch(e2){ if(e2.message!=="auth") note("La tâche n'a pas été enregistrée : "+e2.message); }''')
rep('''  const box = $("#editor"); box.hidden=false; box.innerHTML="";
  const scrim = el("div","scrim"); const f = el("form","form"); f.setAttribute("role","dialog");''',
    '''  const box = $("#editor"); box.hidden=false; box.innerHTML=""; editorOpen=true;
  const scrim = el("div","scrim"); const f = el("form","form"); f.setAttribute("role","dialog");''')
rep('''  const close = ()=>{ box.hidden=true; box.innerHTML=""; };''',
    '''  const close = ()=>{ box.hidden=true; box.innerHTML=""; editorOpen=false; };''')

# 4. qui / quand au survol
rep('''  const r = el("div","sub"+(t.s==="done"?" done":""));''',
    '''  const r = el("div","sub"+(t.s==="done"?" done":""));
  if(t.updated_by) r.title = "Modifié par "+name(t.updated_by)+" le "+new Date(t.updated_at).toLocaleString("fr-FR",{day:"2-digit",month:"2-digit",hour:"2-digit",minute:"2-digit"});''')

# 5. bloc utilisateur dans l'en-tête
rep('''   + (next?`<div class="pill-box"><div class="k">Prochain jalon</div><div class="v">${esc(next[2])} <small>${next[1]} · J-${days}</small></div></div>`:"");''',
    '''   + (next?`<div class="pill-box"><div class="k">Prochain jalon</div><div class="v">${esc(next[2])} <small>${next[1]} · J-${days}</small></div></div>`:"")
   + (me?`<div class="pill-box"><div class="k">Connecté</div><div class="v">${esc(me.name)} <small>${me.role==="admin"?`<a href="/admin">Admin</a> · `:""}<a href="#" id="logout">Déconnexion</a></small></div></div>`:"");
  const lo = $("#logout"); if(lo) lo.addEventListener("click",async e=>{ e.preventDefault(); await fetch("/api/logout",{method:"POST"}); location.reload(); });''')

# 6. couche données -> API du Worker
i = s.index('// ---------- données partagées ----------')
j = s.index('</script>')
s = s[:i] + open('/tmp/claude-0/sheet/data_layer.js').read() + s[j:]
assert 'db.' not in s and 'window.claude' not in s, "reste du runtime claude"
open('/tmp/claude-0/sheet/app_part2.html', 'w').write(s)
print("ok")
