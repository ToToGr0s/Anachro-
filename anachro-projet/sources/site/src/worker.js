// Anachro Prod — Cloudflare Worker + D1
// Sert l'app (/), l'admin (/admin) et l'API (/api/*). Données dans D1 (binding DB).
// APP_HTML, ADMIN_HTML et SEED sont injectés par build.py.

/*__INJECT__*/

const PEOPLE_DEFAULT = [
  { id: "theo", name: "Théo", role: "admin" },
  { id: "celian", name: "Célian", role: "member" },
  { id: "olivier", name: "Olivier", role: "member" },
  { id: "art1", name: "Hugo", role: "member" },
  { id: "art2", name: "JB", role: "member" },
  { id: "xavier", name: "Xavier", role: "viewer" },
];
const STATUSES = ["todo", "doing", "done", "blocked"];
const WHO = ["theo", "celian", "olivier", "art1", "art2", "tous"];
const SESSION_DAYS = 30;
const PBKDF2_ITER = 20000;

let initDone = false;

async function init(env) {
  if (initDone) return;
  await env.DB.batch([
    env.DB.prepare("CREATE TABLE IF NOT EXISTS users (id TEXT PRIMARY KEY, name TEXT NOT NULL, role TEXT NOT NULL, salt TEXT, hash TEXT, fails INTEGER DEFAULT 0, lock_until INTEGER DEFAULT 0)"),
    env.DB.prepare("CREATE TABLE IF NOT EXISTS tasks (id TEXT PRIMARY KEY, g TEXT, gt TEXT, t TEXT NOT NULL, who TEXT, wk TEXT, cat TEXT, s TEXT DEFAULT 'todo', o REAL DEFAULT 0, updated_at INTEGER DEFAULT 0, updated_by TEXT)"),
    env.DB.prepare("CREATE TABLE IF NOT EXISTS log (id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, user TEXT, action TEXT NOT NULL, task_id TEXT, before TEXT, after TEXT)"),
    env.DB.prepare("CREATE INDEX IF NOT EXISTS log_user ON log(user, id)"),
    env.DB.prepare("CREATE INDEX IF NOT EXISTS log_task ON log(task_id, id)"),
    env.DB.prepare("CREATE TABLE IF NOT EXISTS meta (k TEXT PRIMARY KEY, v TEXT)"),
  ]);
  const secret = await env.DB.prepare("SELECT v FROM meta WHERE k='secret'").first();
  if (!secret) {
    const b = crypto.getRandomValues(new Uint8Array(32));
    await env.DB.prepare("INSERT OR IGNORE INTO meta (k, v) VALUES ('secret', ?)").bind(hex(b)).run();
  }
  const n = await env.DB.prepare("SELECT COUNT(*) AS n FROM tasks").first();
  if (!n.n && typeof SEED !== "undefined") {
    const now = Date.now();
    const stmts = SEED.map((t) =>
      env.DB.prepare("INSERT OR IGNORE INTO tasks (id,g,gt,t,who,wk,cat,s,o,updated_at,updated_by) VALUES (?,?,?,?,?,?,?,?,?,?,?)")
        .bind(t.id, t.g, t.gt, t.t, t.who, t.wk, t.cat, t.s || "todo", t.o || 0, now, null));
    for (let i = 0; i < stmts.length; i += 90) await env.DB.batch(stmts.slice(i, i + 90));
    await env.DB.prepare("INSERT INTO log (ts,user,action,task_id,before,after) VALUES (?,?,?,?,?,?)")
      .bind(now, null, "import", null, null, JSON.stringify({ count: SEED.length })).run();
  }
  // Migrations douces (base déjà créée avec une ancienne version)
  await env.DB.batch([
    env.DB.prepare("INSERT INTO users (id,name,role) SELECT 'xavier','Xavier','viewer' WHERE EXISTS (SELECT 1 FROM users WHERE id='theo') AND NOT EXISTS (SELECT 1 FROM users WHERE id='xavier')"),
    env.DB.prepare("UPDATE users SET name='Hugo' WHERE id='art1' AND name='Artiste 1'"),
    env.DB.prepare("UPDATE users SET name='JB' WHERE id='art2' AND name='Artiste 2'"),
  ]);
  initDone = true;
}

// ---------- utilitaires ----------
const hex = (b) => [...new Uint8Array(b)].map((x) => x.toString(16).padStart(2, "0")).join("");
const json = (data, status = 200, headers = {}) =>
  new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store", ...headers } });
const err = (msg, status = 400) => json({ error: msg }, status);
const html = (body) => new Response(body, { headers: {
  "content-type": "text/html; charset=utf-8", "cache-control": "no-store",
  "x-frame-options": "DENY", "referrer-policy": "same-origin",
  "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'" } });

async function hashPin(pin, saltHex) {
  const salt = saltHex ? Uint8Array.from(saltHex.match(/../g).map((h) => parseInt(h, 16))) : crypto.getRandomValues(new Uint8Array(16));
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(pin), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt, iterations: PBKDF2_ITER }, key, 256);
  return { salt: hex(salt), hash: hex(bits) };
}
function safeEq(a, b) {
  if (typeof a !== "string" || typeof b !== "string" || a.length !== b.length) return false;
  let r = 0; for (let i = 0; i < a.length; i++) r |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return r === 0;
}
async function hmac(env, data) {
  const s = (await env.DB.prepare("SELECT v FROM meta WHERE k='secret'").first()).v;
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(s), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return hex(await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(data)));
}
async function makeSession(env, uid) {
  const exp = Date.now() + SESSION_DAYS * 864e5;
  const payload = `${uid}.${exp}`;
  const sig = await hmac(env, payload);
  return `s=${payload}.${sig}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=${SESSION_DAYS * 86400}`;
}
async function currentUser(req, env) {
  const c = req.headers.get("cookie") || "";
  const m = c.match(/(?:^|;\s*)s=([a-z0-9]+)\.(\d+)\.([0-9a-f]{64})/);
  if (!m) return null;
  const [, uid, exp, sig] = m;
  if (Number(exp) < Date.now()) return null;
  if (!safeEq(sig, await hmac(env, `${uid}.${exp}`))) return null;
  const u = await env.DB.prepare("SELECT id, name, role, hash FROM users WHERE id=?").bind(uid).first();
  if (!u || !u.hash) return null;
  return { id: u.id, name: u.name, role: u.role };
}
function sameOrigin(req) {
  const o = req.headers.get("origin");
  return !o || o === new URL(req.url).origin;
}
async function readJson(req) {
  try { const b = await req.json(); return b && typeof b === "object" ? b : {}; } catch { return {}; }
}
const clip = (v, n) => String(v ?? "").slice(0, n);
function cleanTask(b, base = {}) {
  const t = { ...base };
  if ("t" in b) t.t = clip(b.t, 200).trim();
  if ("gt" in b) t.gt = clip(b.gt, 80).trim();
  if ("g" in b) t.g = clip(b.g, 40).replace(/[^A-Za-z0-9_-]/g, "");
  if ("who" in b && WHO.includes(b.who)) t.who = b.who;
  if ("wk" in b && /^S\d{1,2}$/.test(b.wk)) t.wk = b.wk;
  if ("cat" in b) t.cat = clip(b.cat, 30);
  if ("s" in b && STATUSES.includes(b.s)) t.s = b.s;
  if ("o" in b && Number.isFinite(Number(b.o))) t.o = Number(b.o);
  return t;
}
const TASK_COLS = ["id", "g", "gt", "t", "who", "wk", "cat", "s", "o"];
const pick = (t) => (t ? Object.fromEntries(TASK_COLS.map((k) => [k, t[k] ?? null])) : null);

async function writeTask(env, user, action, before, after) {
  const now = Date.now();
  const stmts = [];
  if (after) {
    stmts.push(env.DB.prepare("INSERT INTO tasks (id,g,gt,t,who,wk,cat,s,o,updated_at,updated_by) VALUES (?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET g=excluded.g, gt=excluded.gt, t=excluded.t, who=excluded.who, wk=excluded.wk, cat=excluded.cat, s=excluded.s, o=excluded.o, updated_at=excluded.updated_at, updated_by=excluded.updated_by")
      .bind(after.id, after.g, after.gt, after.t, after.who, after.wk, after.cat, after.s, after.o, now, user.id));
  } else {
    stmts.push(env.DB.prepare("DELETE FROM tasks WHERE id=?").bind(before.id));
  }
  stmts.push(env.DB.prepare("INSERT INTO log (ts,user,action,task_id,before,after) VALUES (?,?,?,?,?,?)")
    .bind(now, user.id, action, (after || before).id, before ? JSON.stringify(pick(before)) : null, after ? JSON.stringify(pick(after)) : null));
  await env.DB.batch(stmts);
  return now;
}

// ---------- synchro GitHub ----------
// Appelée par l'Action « Synchro des tâches » quand une PR testée est mergée dans Dev.
// Le jeton est un secret Cloudflare (wrangler secret put SYNC_TOKEN), le même que le secret GitHub
// ANACHRO_SYNC_TOKEN : il ne donne accès qu'à cette route, et seulement pour passer des tâches à « done ».
const SYNC_ID = /^[A-Za-z0-9_-]{1,40}$/;
const SYNC_MAX = 50;
async function syncTasks(req, env) {
  if (!env.SYNC_TOKEN) return err("Synchro désactivée : secret SYNC_TOKEN absent du Worker.", 503);
  if (!safeEq(req.headers.get("authorization") || "", `Bearer ${env.SYNC_TOKEN}`)) return err("Jeton invalide", 401);

  const b = await readJson(req);
  const ids = Array.isArray(b.ids) ? [...new Set(b.ids.map(String).filter((id) => SYNC_ID.test(id)))].slice(0, SYNC_MAX) : [];
  if (!ids.length) return err("Aucune tâche à cocher.");

  const pr = Number.isInteger(b.pr) ? b.pr : null;
  const bot = { id: pr ? `github#${pr}` : "github" };
  const done = [], already = [], unknown = [];
  for (const id of ids) {
    // Sous-tâche (T02-5) ou tâche entière (T03 : toutes ses sous-tâches).
    const rows = (await env.DB.prepare("SELECT * FROM tasks WHERE id=? OR g=? ORDER BY o").bind(id, id).all()).results;
    if (!rows.length) { unknown.push(id); continue; }
    for (const before of rows) {
      if (before.s === "done") { already.push(before.id); continue; }
      await writeTask(env, bot, "sync", before, { ...pick(before), s: "done" });
      done.push(before.id);
    }
  }
  return json({ ok: true, pr, done, already, unknown });
}

// ---------- routes ----------
export default {
  async scheduled(event, env, ctx) {
    ctx.waitUntil((async () => { await init(env); await runSchedule(env); })());
  },
  async fetch(req, env, ctx) {
    try {
      await init(env);
      const url = new URL(req.url);
      const p = url.pathname;
      if (req.method === "GET" && (p === "/" || p === "/index.html")) return html(APP_HTML);
      if (req.method === "GET" && p === "/admin") return html(ADMIN_HTML);
      if (!p.startsWith("/api/")) return new Response("Introuvable", { status: 404 });
      if (req.method !== "GET" && !sameOrigin(req)) return err("Origine refusée", 403);

      // --- comptes ---
      if (p === "/api/state" && req.method === "GET") {
        const admin = await env.DB.prepare("SELECT COUNT(*) AS n FROM users WHERE role='admin' AND hash IS NOT NULL").first();
        const people = (await env.DB.prepare("SELECT id, name, role FROM users ORDER BY rowid").all()).results;
        return json({ setup: !admin.n, me: await currentUser(req, env), people: people.length ? people : PEOPLE_DEFAULT.map(({ id, name }) => ({ id, name })) });
      }
      if (p === "/api/setup" && req.method === "POST") {
        const admin = await env.DB.prepare("SELECT COUNT(*) AS n FROM users WHERE role='admin' AND hash IS NOT NULL").first();
        if (admin.n) return err("Déjà configuré", 403);
        const b = await readJson(req);
        const pin = String(b.pin || "");
        if (pin.length < 6) return err("Le mot de passe admin doit faire au moins 6 caractères.");
        const h = await hashPin(pin);
        const stmts = PEOPLE_DEFAULT.map((u) => env.DB.prepare("INSERT OR IGNORE INTO users (id,name,role) VALUES (?,?,?)").bind(u.id, u.name, u.role));
        stmts.push(env.DB.prepare("UPDATE users SET salt=?, hash=?, role='admin' WHERE id='theo'").bind(h.salt, h.hash));
        await env.DB.batch(stmts);
        return json({ ok: true }, 200, { "set-cookie": await makeSession(env, "theo") });
      }
      if (p === "/api/login" && req.method === "POST") {
        const b = await readJson(req);
        const u = await env.DB.prepare("SELECT * FROM users WHERE id=?").bind(String(b.user || "")).first();
        if (!u || !u.hash) return err("Ce compte n'a pas encore de code. Demande-le à Théo.", 401);
        if (u.lock_until > Date.now()) return err("Trop d'essais. Réessaie dans quelques minutes.", 429);
        const h = await hashPin(String(b.pin || ""), u.salt);
        if (!safeEq(h.hash, u.hash)) {
          const fails = (u.fails || 0) + 1;
          await env.DB.prepare("UPDATE users SET fails=?, lock_until=? WHERE id=?").bind(fails >= 8 ? 0 : fails, fails >= 8 ? Date.now() + 15 * 60e3 : 0, u.id).run();
          return err("Code incorrect.", 401);
        }
        await env.DB.prepare("UPDATE users SET fails=0, lock_until=0 WHERE id=?").bind(u.id).run();
        await env.DB.prepare("INSERT INTO log (ts,user,action) VALUES (?,?,?)").bind(Date.now(), u.id, "login").run();
        return json({ ok: true }, 200, { "set-cookie": await makeSession(env, u.id) });
      }
      if (p === "/api/logout" && req.method === "POST") {
        return json({ ok: true }, 200, { "set-cookie": "s=; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=0" });
      }

      // --- synchro GitHub : une PR mergée dans Dev coche ses tâches (secret SYNC_TOKEN, jamais un compte) ---
      if (p === "/api/sync/tasks" && req.method === "POST") return syncTasks(req, env);

      const me = await currentUser(req, env);
      if (!me) return err("Connexion requise", 401);
      if (me.role === "viewer" && req.method !== "GET")
        return err("Compte spectateur : tu peux tout voir, mais rien modifier.", 403);

      // --- tâches ---
      if (p === "/api/tasks" && req.method === "GET") {
        const rev = await env.DB.prepare("SELECT COALESCE(MAX(id),0) AS r FROM log WHERE action NOT IN ('login')").first();
        if (url.searchParams.get("since") === String(rev.r)) return json({ rev: rev.r, unchanged: true });
        const tasks = (await env.DB.prepare("SELECT * FROM tasks").all()).results;
        const people = (await env.DB.prepare("SELECT id, name, role FROM users ORDER BY rowid").all()).results;
        const lk = await env.DB.prepare("SELECT v FROM meta WHERE k='links'").first();
        return json({ rev: rev.r, tasks, people, me, links: lk ? JSON.parse(lk.v) : [] });
      }
      if (p === "/api/tasks" && req.method === "POST") {
        const b = await readJson(req);
        const t = cleanTask(b, { s: "todo", o: Date.now() % 1e6, cat: "Gameplay", who: "tous", wk: "S2" });
        if (!t.t) return err("Donne un nom à la tâche.");
        if (!t.gt) return err("Donne un nom de groupe.");
        t.id = "x" + Date.now().toString(36) + hex(crypto.getRandomValues(new Uint8Array(3)));
        if (!t.g) t.g = "N" + Date.now().toString(36);
        await writeTask(env, me, "create", null, t);
        return json({ ok: true, task: t });
      }
      let m = p.match(/^\/api\/tasks\/([A-Za-z0-9_-]{1,60})$/);
      if (m && (req.method === "PATCH" || req.method === "DELETE")) {
        const before = await env.DB.prepare("SELECT * FROM tasks WHERE id=?").bind(m[1]).first();
        if (!before) return err("Tâche introuvable", 404);
        if (req.method === "DELETE") { await writeTask(env, me, "delete", before, null); return json({ ok: true }); }
        const b = await readJson(req);
        const after = cleanTask(b, pick(before));
        if (!after.t) return err("Donne un nom à la tâche.");
        const changed = TASK_COLS.some((k) => after[k] !== before[k]);
        if (!changed) return json({ ok: true, task: after });
        const onlyStatus = TASK_COLS.every((k) => k === "s" || after[k] === before[k]);
        await writeTask(env, me, onlyStatus ? "status" : "edit", before, after);
        if (after.s === "blocked" && before.s !== "blocked") ctx.waitUntil(notifyBlocked(env, after, me).catch(() => {}));
        return json({ ok: true, task: after });
      }

      // --- admin ---
      if (me.role !== "admin") return err("Réservé à l'admin", 403);
      if (p === "/api/log" && req.method === "GET") {
        const where = [], args = [];
        const u = url.searchParams.get("user"); if (u) { where.push("user=?"); args.push(u); }
        const tk = url.searchParams.get("task"); if (tk) { where.push("task_id=?"); args.push(tk); }
        const a = url.searchParams.get("action"); if (a) { where.push("action=?"); args.push(a); }
        const from = Number(url.searchParams.get("from")); if (from) { where.push("ts>=?"); args.push(from); }
        const to = Number(url.searchParams.get("to")); if (to) { where.push("ts<?"); args.push(to); }
        const bf = Number(url.searchParams.get("before")); if (bf) { where.push("id<?"); args.push(bf); }
        const lim = Math.min(500, Math.max(1, Number(url.searchParams.get("limit")) || 200));
        const rows = (await env.DB.prepare(`SELECT * FROM log ${where.length ? "WHERE " + where.join(" AND ") : ""} ORDER BY id DESC LIMIT ${lim}`).bind(...args).all()).results;
        return json({ log: rows });
      }
      if (p === "/api/stats" && req.method === "GET") {
        const since = Number(url.searchParams.get("since")) || 0;
        const rows = (await env.DB.prepare("SELECT user, action, COUNT(*) AS n, MAX(ts) AS last FROM log WHERE ts>=? AND user IS NOT NULL GROUP BY user, action").bind(since).all()).results;
        const done = (await env.DB.prepare("SELECT who, s, COUNT(*) AS n FROM tasks GROUP BY who, s").all()).results;
        return json({ activity: rows, tasks: done });
      }
      m = p.match(/^\/api\/log\/(\d+)\/revert$/);
      if (m && req.method === "POST") {
        const row = await env.DB.prepare("SELECT * FROM log WHERE id=?").bind(Number(m[1])).first();
        if (!row || !row.task_id || !["create", "edit", "status", "delete", "revert"].includes(row.action)) return err("Rien à annuler pour cette ligne.");
        const cur = await env.DB.prepare("SELECT * FROM tasks WHERE id=?").bind(row.task_id).first();
        const target = row.before ? JSON.parse(row.before) : null;
        if (!target && !cur) return json({ ok: true });
        await writeTask(env, me, "revert", cur ? pick(cur) : null, target);
        return json({ ok: true });
      }
      if (p === "/api/users" && req.method === "GET") {
        const rows = (await env.DB.prepare("SELECT id, name, role, (hash IS NOT NULL) AS has_pin, lock_until FROM users ORDER BY rowid").all()).results;
        const last = (await env.DB.prepare("SELECT user, MAX(ts) AS last FROM log WHERE user IS NOT NULL GROUP BY user").all()).results;
        return json({ users: rows, last });
      }
      if (p === "/api/links" && req.method === "GET") {
        const lk = await env.DB.prepare("SELECT v FROM meta WHERE k='links'").first();
        return json({ links: lk ? JSON.parse(lk.v) : [] });
      }
      if (p === "/api/links" && req.method === "POST") {
        const b = await readJson(req);
        if (!Array.isArray(b.links) || b.links.length > 8) return err("8 liens maximum.");
        const links = [];
        for (const l of b.links) {
          const label = clip(l && l.label, 40).trim(), url = String((l && l.url) || "").trim();
          if (!label && !url) continue;
          if (!label) return err("Chaque lien doit avoir un nom.");
          let u; try { u = new URL(url); } catch (e) { return err(`Adresse invalide pour « ${label} ».`); }
          if (!["https:", "http:"].includes(u.protocol) || url.length > 800) return err(`Adresse invalide pour « ${label} » (http ou https).`);
          links.push({ label, url: u.href });
        }
        const old = await env.DB.prepare("SELECT v FROM meta WHERE k='links'").first();
        await env.DB.batch([
          env.DB.prepare("INSERT INTO meta (k, v) VALUES ('links', ?) ON CONFLICT(k) DO UPDATE SET v=excluded.v").bind(JSON.stringify(links)),
          env.DB.prepare("INSERT INTO log (ts,user,action,task_id,before,after) VALUES (?,?,?,?,?,?)")
            .bind(Date.now(), me.id, "links", null, old ? old.v : "[]", JSON.stringify(links)),
        ]);
        return json({ ok: true, links });
      }
      if (p === "/api/discord" && req.method === "GET") return json(await discordConfig(env));
      if (p === "/api/discord" && req.method === "POST") {
        const b = await readJson(req);
        const url = String(b.planning || "").trim();
        if (url && !/^https:\/\/(?:ptb\.|canary\.)?discord(?:app)?\.com\/api\/webhooks\/\d+\/[\w-]+$/.test(url)) return err("L'adresse du webhook Discord n'est pas valide.");
        const ids = {};
        for (const [k, v] of Object.entries(b.ids || {})) { const x = String(v || "").trim(); if (x && !/^\d{5,25}$/.test(x)) return err(`Identifiant Discord invalide pour ${k} (que des chiffres).`); if (x) ids[clip(k, 20)] = x; }
        const cfg = { planning: url, ids, site: new URL(req.url).origin };
        await env.DB.prepare("INSERT INTO meta (k, v) VALUES ('discord', ?) ON CONFLICT(k) DO UPDATE SET v=excluded.v").bind(JSON.stringify(cfg)).run();
        return json({ ok: true });
      }
      if (p === "/api/discord/test" && req.method === "POST") {
        const b = await readJson(req);
        const r = b.job === "recap" ? await weeklyRecap(env) : b.job === "review" ? await weeklyReview(env)
          : await postDiscord(env, { content: "✅ Test : le planning Anachro est bien relié à ce salon." });
        return r.ok ? json({ ok: true }) : err(r.error);
      }
      if (p === "/api/users" && req.method === "POST") {
        const b = await readJson(req);
        const name = clip(b.name, 40).trim();
        if (!name) return err("Donne un nom au compte.");
        const role = b.role === "member" ? "member" : "viewer";
        let base = name.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().replace(/[^a-z0-9]/g, "").slice(0, 16) || "compte";
        let id = base, i = 2;
        while (await env.DB.prepare("SELECT 1 FROM users WHERE id=?").bind(id).first()) id = (base.slice(0, 14) + i++);
        await env.DB.batch([
          env.DB.prepare("INSERT INTO users (id,name,role) VALUES (?,?,?)").bind(id, name, role),
          env.DB.prepare("INSERT INTO log (ts,user,action,task_id,before,after) VALUES (?,?,?,?,?,?)")
            .bind(Date.now(), me.id, "account", null, null, JSON.stringify({ account: id, created: true, role })),
        ]);
        return json({ ok: true, id });
      }
      m = p.match(/^\/api\/users\/([a-z0-9]{1,20})$/);
      if (m && req.method === "POST") {
        const u = await env.DB.prepare("SELECT * FROM users WHERE id=?").bind(m[1]).first();
        if (!u) return err("Compte introuvable", 404);
        const b = await readJson(req);
        if (b.role != null) {
          if (u.role === "admin") return err("Le rôle admin ne se change pas ici.");
          if (!["member", "viewer"].includes(b.role)) return err("Rôle inconnu.");
          await env.DB.prepare("UPDATE users SET role=? WHERE id=?").bind(b.role, u.id).run();
        }
        if (b.name != null) {
          const name = clip(b.name, 40).trim(); if (!name) return err("Nom vide.");
          await env.DB.prepare("UPDATE users SET name=? WHERE id=?").bind(name, u.id).run();
        }
        if (b.pin != null) {
          const pin = String(b.pin);
          if (pin.length < 4) return err("Le code doit faire au moins 4 caractères.");
          const h = await hashPin(pin);
          await env.DB.prepare("UPDATE users SET salt=?, hash=?, fails=0, lock_until=0 WHERE id=?").bind(h.salt, h.hash, u.id).run();
        }
        await env.DB.prepare("INSERT INTO log (ts,user,action,task_id,before,after) VALUES (?,?,?,?,?,?)")
          .bind(Date.now(), me.id, "account", null, null, JSON.stringify({ account: u.id, name: b.name != null, pin: b.pin != null, role: b.role ?? null })).run();
        return json({ ok: true });
      }
      return err("Route inconnue", 404);
    } catch (e) {
      return err("Erreur serveur : " + (e && e.message ? e.message : "inconnue"), 500);
    }
  },
};
