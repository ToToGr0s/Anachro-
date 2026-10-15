// ---------- Discord (planning) ----------
// Calendrier studio (doit rester aligné avec la page planning)
const STUDIO_WEEKS = [
  ["S2", "2026-10-12", "2026-10-16", "12–16 oct"], ["S3", "2026-10-19", "2026-10-23", "19–23 oct"],
  ["S4", "2026-11-30", "2026-12-04", "30 nov–4 déc"], ["S5", "2026-12-07", "2026-12-11", "7–11 déc"],
  ["S6", "2026-12-14", "2026-12-18", "14–18 déc"], ["S7", "2027-01-11", "2027-01-15", "11–15 jan"],
  ["S8", "2027-01-18", "2027-01-22", "18–22 jan"], ["S9", "2027-01-25", "2027-01-29", "25–29 jan"],
  ["S10", "2027-03-08", "2027-03-12", "8–12 mars"], ["S11", "2027-03-15", "2027-03-19", "15–19 mars"],
  ["S12", "2027-03-22", "2027-03-26", "22–26 mars"], ["S13", "2027-04-05", "2027-04-09", "5–9 avr"],
  ["S14", "2027-04-12", "2027-04-16", "12–16 avr"], ["S15", "2027-04-19", "2027-04-23", "19–23 avr"],
  ["S16", "2027-05-31", "2027-06-04", "31 mai–4 juin"], ["S17", "2027-06-07", "2027-06-11", "7–11 juin"],
  ["S18", "2027-06-14", "2027-06-18", "14–18 juin"],
];
const MILESTONES = [
  ["2026-10-23", "Boucle en LAN", "Greybox, 3 instances : rôles secrets, PNJ, cache, recherche, tir validé serveur"],
  ["2026-12-04", "1re partie en ligne", "Via Steam sur PC différents, de bout en bout. Playtest à 4+"],
  ["2026-12-18", "Core gelé", "Playtest à 8, vocal OK, dégradation OK. Page Steam en ligne"],
  ["2027-01-29", "Démo", "Map 1 avec l'art, stable à 120 ms / 2 %. Envoyée au Next Fest"],
  ["2027-03-19", "Release candidate", "Zéro bug bloquant, build en review Steam"],
  ["2027-03-26", "Sortie", "Jeu en vente sur Steam"],
  ["2027-04-23", "MAJ 1.1", "Bugs majeurs corrigés + 2-3 demandes des joueurs"],
  ["2027-06-18", "MAJ 1.2", "Map 2 + nouveaux anachronismes"],
];
const COLORS = { info: 0x2f5fd0, good: 0x1f8a5b, warn: 0xb7860b, bad: 0xc0262d };

function paris(d = new Date()) {
  const f = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Paris", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", weekday: "short", hourCycle: "h23" });
  const o = Object.fromEntries(f.formatToParts(d).map((p) => [p.type, p.value]));
  return { date: `${o.year}-${o.month}-${o.day}`, h: Number(o.hour), m: Number(o.minute), wd: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].indexOf(o.weekday) + 1 };
}
const dayDiff = (a, b) => Math.round((Date.parse(b + "T00:00:00Z") - Date.parse(a + "T00:00:00Z")) / 864e5);
const studioWeekOf = (date) => STUDIO_WEEKS.find(([, a, b]) => date >= a && date <= b);

async function discordConfig(env) {
  const r = await env.DB.prepare("SELECT v FROM meta WHERE k='discord'").first();
  try { return r ? JSON.parse(r.v) : {}; } catch { return {}; }
}
async function people(env) {
  return (await env.DB.prepare("SELECT id, name, role FROM users WHERE role != 'viewer' ORDER BY rowid").all()).results;
}
const mention = (cfg, p) => (cfg.ids && /^\d{5,25}$/.test(cfg.ids[p.id] || "") ? `<@${cfg.ids[p.id]}>` : `**${p.name}**`);

async function postDiscord(env, payload) {
  const cfg = await discordConfig(env);
  if (!cfg.planning) return { ok: false, error: "Aucun webhook Discord configuré." };
  const ids = Object.values(cfg.ids || {}).filter((x) => /^\d{5,25}$/.test(x));
  const r = await fetch(cfg.planning, {
    method: "POST", headers: { "content-type": "application/json" },
    body: JSON.stringify({ username: "Anachro Prod", allowed_mentions: { parse: [], users: ids }, ...payload }),
  });
  return r.ok ? { ok: true } : { ok: false, error: `Discord a répondu ${r.status}` };
}
async function onceKey(env, key) {
  const r = await env.DB.prepare("INSERT OR IGNORE INTO meta (k, v) VALUES (?, ?)").bind("sent:" + key, String(Date.now())).run();
  return r.meta && r.meta.changes > 0;
}
const clipLines = (lines, max = 8) => (lines.length > max ? [...lines.slice(0, max), `… et ${lines.length - max} de plus`] : lines).join("\n").slice(0, 1000);

function groupLines(tasks) {
  const g = new Map();
  for (const t of tasks) { if (!g.has(t.g)) g.set(t.g, { gt: t.gt, n: 0, d: 0, b: 0 }); const x = g.get(t.g); x.n++; if (t.s === "done") x.d++; if (t.s === "blocked") x.b++; }
  return [...g.values()].map((x) => `${x.d === x.n ? "✅" : x.b ? "🚧" : "▫️"} ${x.gt} (${x.d}/${x.n})`);
}

async function weeklyRecap(env) {
  const now = paris(); const wk = studioWeekOf(now.date);
  if (!wk) return { ok: false, error: "Pas une semaine studio." };
  const cfg = await discordConfig(env); const ps = await people(env);
  const tasks = (await env.DB.prepare("SELECT * FROM tasks").all()).results;
  const wIdx = (s) => STUDIO_WEEKS.findIndex((w) => w[0] === s);
  const cur = wIdx(wk[0]);
  const fields = [];
  const pinged = [];
  for (const p of [...ps, { id: "tous", name: "Équipe (hors code)" }]) {
    const mine = tasks.filter((t) => t.who === p.id);
    const now_ = mine.filter((t) => t.wk === wk[0]);
    const late = mine.filter((t) => wIdx(t.wk) >= 0 && wIdx(t.wk) < cur && t.s !== "done");
    if (!now_.length && !late.length) continue;
    if (p.id !== "tous") pinged.push(mention(cfg, p));
    const lines = groupLines(now_);
    if (late.length) lines.push(`⚠️ ${late.length} tâche${late.length > 1 ? "s" : ""} en retard`);
    fields.push({ name: p.name, value: clipLines(lines) || "—", inline: false });
  }
  const ms = MILESTONES.find(([d]) => d >= wk[1] && d <= wk[2]);
  return postDiscord(env, {
    content: `📅 Nouvelle semaine studio ${pinged.join(" ")}`,
    embeds: [{ title: `Semaine ${wk[0]} · ${wk[3]}`, color: COLORS.info, url: cfg.site || undefined,
      description: ms ? `🎯 Jalon en fin de semaine : **${ms[1]}**\n${ms[2]}` : "Voici ce qui est prévu pour chacun.", fields: fields.slice(0, 25) }],
  });
}

async function weeklyReview(env) {
  const now = paris(); const wk = studioWeekOf(now.date);
  if (!wk) return { ok: false, error: "Pas une semaine studio." };
  const cfg = await discordConfig(env); const ps = await people(env);
  const tasks = (await env.DB.prepare("SELECT * FROM tasks WHERE wk=?").bind(wk[0]).all()).results;
  const fields = []; let tot = 0, done = 0;
  for (const p of [...ps, { id: "tous", name: "Équipe (hors code)" }]) {
    const mine = tasks.filter((t) => t.who === p.id); if (!mine.length) continue;
    const d = mine.filter((t) => t.s === "done").length; tot += mine.length; done += d;
    const left = mine.filter((t) => t.s !== "done").map((t) => `• ${t.t}${t.s === "blocked" ? " 🚧" : ""}`);
    fields.push({ name: `${p.name} — ${d}/${mine.length} (${Math.round((100 * d) / mine.length)} %)`, value: left.length ? clipLines(left, 6) : "Tout est fait 🎉", inline: false });
  }
  const pct = tot ? Math.round((100 * done) / tot) : 0;
  return postDiscord(env, {
    content: "🧾 Bilan de la semaine",
    embeds: [{ title: `Bilan ${wk[0]} · ${done}/${tot} tâches faites (${pct} %)`, color: pct >= 80 ? COLORS.good : pct >= 50 ? COLORS.warn : COLORS.bad, url: cfg.site || undefined, fields: fields.slice(0, 25) }],
  });
}

async function milestoneAlerts(env, today) {
  const cfg = await discordConfig(env);
  for (const [d, title, crit] of MILESTONES) {
    const left = dayDiff(today, d);
    if (left !== 3 && left !== 1) continue;
    if (!(await onceKey(env, `ms:${d}:${left}`))) continue;
    await postDiscord(env, { content: "⏳ Jalon en approche", embeds: [{ title: `${title} dans ${left} jour${left > 1 ? "s" : ""}`, description: crit, color: COLORS.warn, url: cfg.site || undefined }] });
  }
}

async function notifyBlocked(env, task, by) {
  const cfg = await discordConfig(env);
  const ps = await people(env); const owner = ps.find((p) => p.id === task.who);
  return postDiscord(env, {
    content: `🚧 Tâche bloquée ${owner ? mention(cfg, owner) : ""}`,
    embeds: [{ title: task.t, description: `Groupe : ${task.gt} · Semaine ${task.wk}\nSignalé par ${by.name}`, color: COLORS.bad, url: cfg.site || undefined }],
  });
}

async function runSchedule(env) {
  const now = paris();
  const inWindow = (h, m) => now.h * 60 + now.m >= h * 60 + m && now.h * 60 + now.m < h * 60 + m + 15;
  if (now.wd === 1 && inWindow(8, 45) && studioWeekOf(now.date) && (await onceKey(env, "recap:" + now.date))) await weeklyRecap(env);
  if (now.wd === 5 && inWindow(16, 30) && studioWeekOf(now.date) && (await onceKey(env, "review:" + now.date))) await weeklyReview(env);
  if (inWindow(9, 0)) await milestoneAlerts(env, now.date);
}
