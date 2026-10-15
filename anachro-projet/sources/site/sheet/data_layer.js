// ---------- serveur (Cloudflare Worker) ----------

async function api(path, opts={}){
  const r = await fetch(path,{credentials:"same-origin",headers:{"content-type":"application/json"},...opts});
  let j={}; try{ j=await r.json(); }catch(e){}
  if(r.status===401 && path!=="/api/login"){ showLogin(); throw new Error("auth"); }
  if(!r.ok) throw new Error(j.error||("Erreur "+r.status));
  return j;
}
async function load(){
  const j = await api("/api/tasks"+(rev?"?since="+rev:""));
  if(j.unchanged) return;
  rev=j.rev; me=j.me; loaded=true;
  canWrite = !!me && me.role !== "viewer";
  if(me && me.role==="viewer") note("Mode spectateur : tu peux tout consulter, mais rien modifier.");
  (j.people||[]).forEach(p=>{ if(p.role!=="viewer") PEOPLE[p.id]=p.name; });
  tasks = new Map(j.tasks.map(t=>[t.id,{...t, s: ST[t.s]?t.s:"todo"}]));
  scheduleRender();
}
setInterval(()=>{ if(!document.hidden && me && !editorOpen) load().catch(()=>{}); }, 8000);
document.addEventListener("visibilitychange",()=>{ if(!document.hidden && me) load().catch(()=>{}); });

let loginOpen=false;
async function showLogin(){
  if(loginOpen) return; loginOpen=true;
  let st={setup:false,people:[]}; try{ st = await (await fetch("/api/state")).json(); }catch(e){}
  const box = $("#editor"); box.hidden=false; box.innerHTML=""; editorOpen=true;
  const scrim = el("div","scrim"); const f = el("form","form");
  if(st.setup){
    f.innerHTML = `<h2>Première configuration</h2><p class="note" style="margin:0">Crée le mot de passe admin de Théo (6 caractères minimum). Tu donneras ensuite un code à chacun depuis la page admin.</p>
      <label>Mot de passe admin<input id="l-pin" type="password" autocomplete="new-password" minlength="6" required></label><p class="err" id="l-err" hidden></p>
      <div class="actions"><span class="sp"></span><button class="primary" type="submit">Créer</button></div>`;
  } else {
    f.innerHTML = `<h2>Connexion</h2><label>Qui es-tu ?<select id="l-user"></select></label>
      <label>Code<input id="l-pin" type="password" autocomplete="current-password" required></label><p class="err" id="l-err" hidden></p>
      <div class="actions"><span class="sp"></span><button class="primary" type="submit">Se connecter</button></div>`;
  }
  scrim.append(f); box.append(scrim);
  if(!st.setup){
    const sel=$("#l-user"); st.people.forEach(p=>{const o=el("option",null,esc(p.name)); o.value=p.id; sel.append(o);});
    try{ const last=localStorage.getItem("anachro-user"); if(last) sel.value=last; }catch(e){}
  }
  $("#l-pin").focus();
  f.addEventListener("submit",async e=>{
    e.preventDefault(); const er=$("#l-err"); er.hidden=true;
    try{
      if(st.setup) await api("/api/setup",{method:"POST",body:JSON.stringify({pin:$("#l-pin").value})});
      else {
        await api("/api/login",{method:"POST",body:JSON.stringify({user:$("#l-user").value,pin:$("#l-pin").value})});
        try{ localStorage.setItem("anachro-user",$("#l-user").value); }catch(e){}
      }
      box.hidden=true; box.innerHTML=""; editorOpen=false; loginOpen=false; rev=0; await load();
    }catch(x){ er.textContent=x.message; er.hidden=false; }
  });
}
load().catch(e=>{ if(e.message!=="auth") $("#p-semaine").innerHTML=`<div class="empty">Impossible de charger les tâches : ${esc(e.message)}</div>`; });
