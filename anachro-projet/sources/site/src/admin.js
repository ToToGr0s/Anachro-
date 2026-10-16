const $ = (s,r=document) => r.querySelector(s);
const el = (tag, cls, html) => { const e=document.createElement(tag); if(cls) e.className=cls; if(html!=null) e.innerHTML=html; return e; };
const esc = s => String(s??"").replace(/[&<>"]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));
const ST = {todo:"À faire", doing:"En cours", done:"Fait", blocked:"Bloqué"};
const ACT = {status:"Statut", edit:"Modifiée", create:"Ajoutée", delete:"Supprimée", revert:"Annulation", sync:"Synchro GitHub", login:"Connexion", account:"Compte", import:"Import", links:"Liens"};
const FIELDS = {t:"titre", gt:"groupe", who:"personne", wk:"semaine", cat:"catégorie", s:"statut"};
const COLOR = id => !id ? "tous" : id.startsWith("art") ? "art" : (["theo","celian","olivier"].includes(id) ? id : "tous");
let people = {}, tasks = {}, lastId = 0;

function note(m){ const n=$("#dbnote"); n.textContent=m; n.hidden=false; }
async function api(path, opts={}){
  const r = await fetch(path,{credentials:"same-origin",headers:{"content-type":"application/json"},...opts});
  let j={}; try{ j=await r.json(); }catch(e){}
  if(r.status===401){ document.body.innerHTML = `<div class="wrap"><div class="empty">Connecte-toi d'abord sur <a href="/">la page du planning</a>, puis reviens ici.</div></div>`; throw new Error("auth"); }
  if(r.status===403){ document.body.innerHTML = `<div class="wrap"><div class="empty">Cette page est réservée à l'admin. <a href="/">Retour au planning</a></div></div>`; throw new Error("forbidden"); }
  if(!r.ok) throw new Error(j.error||("Erreur "+r.status));
  return j;
}
const name = id => id ? (people[id] || id) : "Système";
const fmt = ts => new Date(ts).toLocaleString("fr-FR",{weekday:"short",day:"2-digit",month:"2-digit",hour:"2-digit",minute:"2-digit"});
const val = (k,v) => k==="s" ? (ST[v]||v) : k==="who" ? name(v) : (v ?? "—");

function diff(row){
  const b = row.before ? JSON.parse(row.before) : null, a = row.after ? JSON.parse(row.after) : null;
  if(row.action==="account" && a) return a.created ? `Compte créé : ${esc(name(a.account))} (${a.role==="viewer"?"spectateur":"membre"})` : `Compte ${esc(name(a.account))} : ${[a.name&&"nom changé",a.pin&&"code changé",a.role&&("rôle → "+(a.role==="viewer"?"spectateur":"membre"))].filter(Boolean).join(", ")}`;
  if(row.action==="import" && a) return `${a.count} tâches importées`;
  if(row.action==="links" && a) return a.length ? "Liens : "+a.map(l=>`<strong>${esc(l.label)}</strong>`).join(", ") : "Tous les liens supprimés";
  if(row.action==="login") return "";
  if(!b && a) return `Créée : ${esc(a.gt)} · ${esc(val("who",a.who))} · ${esc(a.wk)}`;
  if(b && !a) return `Supprimée (était : ${esc(b.gt)} · ${esc(val("who",b.who))} · ${esc(b.wk)})`;
  if(!b || !a) return "";
  return Object.keys(FIELDS).filter(k=>b[k]!==a[k]).map(k=>`<span class="ch"><b>${FIELDS[k]}</b> ${esc(val(k,b[k]))} → <strong>${esc(val(k,a[k]))}</strong></span>`).join("");
}
function taskTitle(row){
  const t = row.after ? JSON.parse(row.after) : row.before ? JSON.parse(row.before) : null;
  if(!t || !t.t) return "";
  return `<div class="tt">${esc(t.t)}</div><div class="tid">${esc(t.gt||"")} · ${esc(row.task_id)}</div>`;
}

function filters(){
  const q = new URLSearchParams(); const u=$("#f-user").value, a=$("#f-action").value, w=$("#f-when").value;
  if(u) q.set("user",u); if(a) q.set("action",a);
  if(w==="today"){ const d=new Date(); d.setHours(0,0,0,0); q.set("from",d.getTime()); }
  else if(w!=="all") q.set("from", Date.now()-Number(w)*864e5);
  q.set("limit","150"); return q;
}
async function loadLog(append=false){
  const q = filters(); if(append && lastId) q.set("before", lastId);
  const j = await api("/api/log?"+q);
  const box = $("#log"); if(!append) box.innerHTML="";
  if(!j.log.length && !append){ box.append(el("div","empty","Aucune activité sur cette période.")); $("#more").hidden=true; return; }
  for(const row of j.log){
    const r = el("div","lrow c-"+COLOR(row.user));
    const canUndo = row.task_id && ["create","edit","status","delete","revert","sync"].includes(row.action);
    r.innerHTML = `<div class="when">${fmt(row.ts)}</div>
      <div class="who"><i class="dot" style="background:var(--c)"></i>${esc(name(row.user))}</div>
      <div><span class="act a-${row.action}">${ACT[row.action]||esc(row.action)}</span></div>
      <div class="what">${taskTitle(row)}<div class="diff">${diff(row)}</div></div>
      <div class="undo"></div>`;
    if(canUndo){
      const b = el("button","ghost small","Annuler"); b.type="button"; b.title="Remettre la tâche dans l'état d'avant cette action";
      b.addEventListener("click",async()=>{
        if(b.dataset.armed!=="1"){ b.dataset.armed="1"; b.textContent="Confirmer"; return; }
        try{ await api(`/api/log/${row.id}/revert`,{method:"POST"}); loadLog(); }catch(e){ note("Annulation impossible : "+e.message); }
      });
      $(".undo",r).append(b);
    }
    box.append(r);
  }
  lastId = j.log[j.log.length-1].id;
  $("#more").hidden = j.log.length < 150;
}

async function loadPeople(){
  const since = Date.now()-7*864e5;
  const [s, u] = await Promise.all([api("/api/stats?since="+since), api("/api/users")]);
  const last = Object.fromEntries(u.last.map(x=>[x.user,x.last]));
  const box = $("#people"); box.innerHTML="";
  const doneLog = await api("/api/log?action=status&limit=500&from="+since);
  for(const p of u.users){
    const acts = s.activity.filter(a=>a.user===p.id);
    const total = acts.filter(a=>a.action!=="login").reduce((n,a)=>n+a.n,0);
    const checked = doneLog.log.filter(r=>r.user===p.id && r.after && JSON.parse(r.after).s==="done").length;
    const mine = s.tasks.filter(t=>t.who===p.id); const all = mine.reduce((n,t)=>n+t.n,0); const done = mine.filter(t=>t.s==="done").reduce((n,t)=>n+t.n,0);
    const c = el("div","pcard c-"+COLOR(p.id));
    c.innerHTML = `<h3>${esc(p.name)}</h3><div class="muted small">${p.role==="admin"?"Admin · ":""}${last[p.id]?"Dernière activité : "+fmt(last[p.id]):"Aucune activité"}</div>
      <div class="nums"><div><b>${checked}</b><span>cochées (7 j)</span></div><div><b>${total}</b><span>modifs (7 j)</span></div><div><b>${all?Math.round(100*done/all):0} %</b><span>de ses tâches faites</span></div></div>`;
    const b = el("button","ghost small","Voir son activité"); b.type="button";
    b.addEventListener("click",()=>{ $("#f-user").value=p.id; $("#f-when").value="7"; show("activite"); loadLog(); });
    c.append(b); box.append(c);
  }
}

function genCode(){ const a = crypto.getRandomValues(new Uint32Array(1))[0] % 1000000; return String(a).padStart(6,"0"); }
async function loadUsers(){
  const u = await api("/api/users");
  const box = $("#users"); box.innerHTML="";
  const add = el("form","urow add");
  add.innerHTML = `<i class="dot" style="background:var(--tous)"></i>
    <label class="nm">Nouveau compte<input id="new-name" maxlength="40" placeholder="Prénom (ex : un prof)"></label>
    <label class="nm">Rôle<select id="new-role"><option value="viewer">Spectateur (lecture seule)</option><option value="member">Membre (peut modifier)</option></select></label>
    <div class="ub"><button class="primary small" type="submit">Ajouter le compte</button></div>`;
  add.addEventListener("submit",async e=>{ e.preventDefault();
    try{ await api("/api/users",{method:"POST",body:JSON.stringify({name:$("#new-name").value, role:$("#new-role").value})}); await init(); await loadUsers(); note("Compte ajouté. Crée-lui un code pour qu'il puisse se connecter."); }catch(x){ note(x.message); } });
  box.append(add);
  for(const p of u.users){
    const r = el("div","urow c-"+COLOR(p.id));
    const state = p.lock_until > Date.now() ? `<span class="act a-delete">Bloqué (trop d'essais)</span>` : p.has_pin ? `<span class="act a-create">A un code</span>` : `<span class="act a-login">Pas encore de code</span>`;
    r.innerHTML = `<i class="dot" style="background:var(--c)"></i>
      <label class="nm">Nom<input id="nm-${p.id}" value="${esc(p.name)}" maxlength="40"></label>
      <div class="rl">${p.role==="admin"?"<b>Admin</b>":`<select id="rl-${p.id}" aria-label="Rôle de ${esc(p.name)}"><option value="member">Membre</option><option value="viewer">Spectateur</option></select>`}<br>${state}</div>
      <div class="ub"></div><div class="code" id="code-${p.id}" hidden></div>`;
    const save = el("button","ghost small","Enregistrer le nom"); save.type="button";
    save.addEventListener("click",async()=>{ try{ await api("/api/users/"+p.id,{method:"POST",body:JSON.stringify({name:$("#nm-"+p.id).value})}); await init(); note("Nom enregistré."); }catch(e){ note(e.message); } });
    const code = el("button","primary small", p.has_pin?"Nouveau code":"Créer un code"); code.type="button";
    code.addEventListener("click",async()=>{
      if(p.role==="admin" && code.dataset.armed!=="1"){ code.dataset.armed="1"; code.textContent="Sûr ? (ton propre code)"; return; }
      const c = genCode();
      try{ await api("/api/users/"+p.id,{method:"POST",body:JSON.stringify({pin:c})});
        const box = $("#code-"+p.id); box.hidden=false; box.innerHTML=`Code de <b>${esc(p.name)}</b> : <span class="big">${c}</span> — envoie-le-lui en privé, il ne sera plus affiché.`;
      }catch(e){ note(e.message); }
    });
    $(".ub",r).append(save, code); box.append(r);
    const rs = $("#rl-"+p.id);
    if(rs){ rs.value = p.role; rs.addEventListener("change",async()=>{ try{ await api("/api/users/"+p.id,{method:"POST",body:JSON.stringify({role:rs.value})}); note(`${p.name} est maintenant ${rs.value==="viewer"?"spectateur (lecture seule)":"membre"}.`); }catch(e){ note(e.message); rs.value=p.role; } }); }
  }
}

async function loadDiscord(){
  const [cfg, u] = await Promise.all([api("/api/discord"), api("/api/users")]);
  $("#d-url").value = cfg.planning || "";
  const box = $("#d-ids"); box.innerHTML = "";
  for(const p of u.users.filter(x=>x.role!=="viewer")){
    const r = el("div","urow c-"+COLOR(p.id));
    r.innerHTML = `<i class="dot" style="background:var(--c)"></i><label class="nm wide">Identifiant Discord de ${esc(p.name)}<input data-uid="${esc(p.id)}" value="${esc((cfg.ids||{})[p.id]||"")}" inputmode="numeric" placeholder="ex : 123456789012345678"></label>`;
    box.append(r);
  }
}
$("#dform").addEventListener("submit",async e=>{ e.preventDefault();
  const ids = {}; document.querySelectorAll("#d-ids input").forEach(i=>ids[i.dataset.uid]=i.value.trim());
  try{ await api("/api/discord",{method:"POST",body:JSON.stringify({planning:$("#d-url").value.trim(), ids})}); note("Réglages Discord enregistrés."); }catch(x){ note(x.message); } });
document.querySelectorAll("#dform [data-job]").forEach(b=>b.addEventListener("click",async()=>{
  try{ await api("/api/discord/test",{method:"POST",body:JSON.stringify({job:b.dataset.job})}); note("Message envoyé dans Discord."); }catch(x){ note(x.message); } }));

function linkRow(l={label:"",url:""}){
  const r = el("div","urow lrowx");
  r.innerHTML = `<span class="lgrip" aria-hidden="true">🔗</span><div class="lfields"><label class="nm">Nom du bouton<input class="l-label" maxlength="40" placeholder="ex : GDD" value="${esc(l.label)}"></label><label class="nm">Adresse<input class="l-url" placeholder="https://…" value="${esc(l.url)}" autocomplete="off"></label></div><div class="ub"><button class="ghost small" type="button" data-mv="-1" aria-label="Monter">↑</button><button class="ghost small" type="button" data-mv="1" aria-label="Descendre">↓</button><button class="danger small" type="button" data-rm aria-label="Supprimer ce lien">Supprimer</button></div>`;
  r.querySelector("[data-rm]").addEventListener("click",()=>r.remove());
  r.querySelectorAll("[data-mv]").forEach(b=>b.addEventListener("click",()=>{ const box=$("#l-rows"); if(b.dataset.mv==="-1"&&r.previousElementSibling) box.insertBefore(r,r.previousElementSibling); if(b.dataset.mv==="1"&&r.nextElementSibling) box.insertBefore(r.nextElementSibling,r); }));
  return r;
}
async function loadLinks(){
  const j = await api("/api/links");
  const box = $("#l-rows"); box.innerHTML="";
  (j.links.length ? j.links : [{label:"GDD",url:""}]).forEach(l=>box.append(linkRow(l)));
}
$("#l-add").addEventListener("click",()=>{ if($("#l-rows").children.length>=8) return note("8 liens maximum."); const r=linkRow(); $("#l-rows").append(r); r.querySelector("input").focus(); });
$("#lform").addEventListener("submit",async e=>{ e.preventDefault();
  const links=[...document.querySelectorAll("#l-rows .urow")].map(r=>({label:r.querySelector(".l-label").value.trim(), url:r.querySelector(".l-url").value.trim()}));
  try{ await api("/api/links",{method:"POST",body:JSON.stringify({links})}); note("Liens enregistrés : ils apparaissent en haut du planning."); loadLinks(); }catch(x){ note(x.message); } });

const TABS=["activite","personnes","comptes","liens","discord"];
function show(t){
  document.querySelectorAll(".tabs button").forEach(b=>b.setAttribute("aria-selected",String(b.dataset.tab===t)));
  document.querySelectorAll("[data-panel]").forEach(s=>s.hidden=s.dataset.panel!==t);
  if(t==="activite") loadLog().catch(e=>note(e.message));
  if(t==="personnes") loadPeople().catch(e=>note(e.message));
  if(t==="comptes") loadUsers().catch(e=>note(e.message));
  if(t==="discord") loadDiscord().catch(e=>note(e.message));
  if(t==="liens") loadLinks().catch(e=>note(e.message));
}
document.querySelectorAll(".tabs button").forEach(b=>b.addEventListener("click",()=>show(b.dataset.tab)));
["#f-user","#f-action","#f-when"].forEach(s=>$(s).addEventListener("change",()=>loadLog().catch(e=>note(e.message))));
$("#refresh").addEventListener("click",()=>loadLog().catch(e=>note(e.message)));
$("#more").addEventListener("click",()=>loadLog(true).catch(e=>note(e.message)));

async function init(){
  const st = await (await fetch("/api/state")).json();
  people = Object.fromEntries(st.people.map(p=>[p.id,p.name]));
  const sel = $("#f-user"); const cur = sel.value; sel.innerHTML = `<option value="">Tout le monde</option>`;
  st.people.forEach(p=>{ const o=el("option",null,esc(p.name)); o.value=p.id; sel.append(o); }); sel.value=cur;
}
init().then(()=>loadLog()).catch(e=>{ if(!["auth","forbidden"].includes(e.message)) note(e.message); });
setInterval(()=>{ if(!document.hidden && !$("[data-panel=activite]").hidden && !lastIdLocked()) loadLog().catch(()=>{}); }, 20000);
function lastIdLocked(){ return !$("#more").hidden && $("#log").children.length>150; }

{ const h=location.hash.slice(1); if(TABS.includes(h)) show(h); }
