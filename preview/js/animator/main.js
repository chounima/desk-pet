// 動作編輯器：選角色與動作 → 在角色身上拖關節調姿勢、在時間軸上調關鍵影格與曲線 → 存回 data/animations（透過 tools/serve.py）
// 動作分層與差異規則同 core/loader.js（共用 → 體型 → 角色 → 角色＋型態，{ extends, tracks }）
import { TUNING, MOTION } from "../config.js";
import { extendClip, formRig, loadAll } from "../core/loader.js";
import { ease, rest, sampleTrack } from "../core/animator.js";
import { Overlay } from "../core/overlay.js";
import { Spring } from "../core/spring.js";
import { Pet } from "../game/pet.js";
import { Timeline } from "./timeline.js";

const $ = sel => document.querySelector(sel);
const NS = "http://www.w3.org/2000/svg";
const [{ ids, rigs }, { names, clips: sharedClips, sets }, props] = await loadAll();

const state = {
  charId: ids.includes("sera") ? "sera" : ids[0],
  form: null,
  clipName: "walk",
  clip: null,            // 正在編輯的動作（複本）
  original: "",          // 載入時的 JSON（判斷有沒有改過）
  time: 0,
  playing: false,
  speed: 1,
  sel: null,             // { track, index }
  bone: null,            // 選到的骨骼
};

const rig = () => formRig(rigs[state.charId], state.form);
const bodyOf = r => r.body ?? "humanoid";
const charIndex = () => sets.characterIndex ?? {};

// ---------- 動作來自哪一層 ----------
function sourceOf(r, name) {
  const id = r.animSource ?? r.id, entry = charIndex()[id];
  if (r.formName && entry?.forms?.[r.formName]?.includes(name)) return "form";
  if (entry?.clips?.includes(name)) return "character";
  if (bodyOf(r) !== "humanoid" && sets[bodyOf(r)]?.[name] && sets[bodyOf(r)][name] !== sharedClips[name]) return "body";
  return sharedClips[name] ? "shared" : "new";
}
const SOURCE_LABEL = { shared: "共用（所有角色）", body: "這個體型", character: "這個角色專屬", form: "這個角色＋型態專屬", new: "新動作（還沒存）" };

// 下一層的動作（存差異時的基準）
function baseFor(target, name) {
  const r = rig();
  if (target === "form") return sets.for({ ...r, formName: null })[name];
  if (target === "character") return sets[bodyOf(r)]?.[name] ?? sharedClips[name];
  return null;
}

// ---------- 舞台 ----------
const canvas = $("#canvas");
let pet, onion = [], compare = [];
let editClips = {};

const plainClip = c => { const { name, ...rest } = c; return JSON.parse(JSON.stringify(rest)); };

function loadClip(name) {
  const r = rig();
  const set = sets.for(r);
  state.clipName = name;
  state.clip = set[name] ? plainClip(set[name]) : { label: name, duration: 1, loop: true, face: "idle", tracks: {} };
  state.original = JSON.stringify(state.clip);
  state.sel = null;
  state.time = 0;
  editClips = { ...set, [name]: state.clip };
  $("[data-target]").value = sourceOf(r, name) === "new" ? "character" : sourceOf(r, name);
  buildPets();
  syncFields();
  renderTimeline();
}

function makePet(layer, r, clipSets, scale) {
  const p = new Pet({ layer, rig: r, clipSets, props, scale, crossfade: TUNING.crossfade });
  p.overlay = new Overlay($("[data-overlay]").checked ? MOTION : { fade: 0.5, layers: {} });
  p.play(state.clipName, true);
  return p;
}

function buildPets() {
  for (const id of ["#pets", "#onion", "#compare"]) $(id).replaceChildren();
  const r = rig();
  const clipSets = { humanoid: editClips, for: () => editClips };      // Pet 建立時先用 humanoid，換角色時用 for
  pet = makePet($("#pets"), r, clipSets, 2);
  onion = $("[data-onion]").checked ? [makePet($("#onion"), r, clipSets, 2), makePet($("#onion"), r, clipSets, 2)] : [];
  compare = [];
  if ($("[data-compare]").checked) {
    // 不同身材各挑一個：Q 版、正常頭身、四足（目前的角色除外）
    const others = [["tk01"], ["mira", "chouma"], ["choupaca", "quadruped"]].map(([id, form]) => [id, form])
      .filter(([id]) => rigs[id] && id !== state.charId).slice(0, 3);
    const target = $("[data-target]").value;
    for (const [id, form] of others) {
      const cr = formRig(rigs[id], form);
      // 編輯的是共用（或同體型）動作時，比較的角色也用編輯中的版本
      const shared = target === "shared" || (target === "body" && bodyOf(cr) === bodyOf(r));
      const own = sets.for(cr);
      const set = shared ? { ...own, [state.clipName]: state.clip } : own;
      const p = makePet($("#compare"), cr, { humanoid: set, for: () => set }, 0.9);
      p.label = `${cr.name ?? id}${form ? "（四足）" : ""}`;
      compare.push(p);
    }
  }
  renderPhysics();
  layout();
}

function layout() {
  const { width, height } = canvas.getBoundingClientRect();
  const r = rig();
  pet.scale = Math.min(3, (height * 0.7) / (r.height ?? 270));
  pet.x = width * (compare.length ? 0.38 : 0.5);
  pet.y = height * 0.86;
  for (const o of onion) Object.assign(o, { x: pet.x, y: pet.y, scale: pet.scale });
  compare.forEach((p, i) => {
    p.scale = Math.min(1.1, (height * 0.3) / (p.rig.height ?? 270));
    p.x = width * 0.72 + (i % 2) * width * 0.14;
    p.y = height * (i < 2 ? 0.42 : 0.86);
  });
  const ground = $("[data-ground]");
  Object.entries({ x1: 0, x2: width, y1: pet.y, y2: pet.y }).forEach(([k, v]) => ground.setAttribute(k, v));
}

// 讓某隻寵物顯示動作在 t 秒的樣子（dt：物理要用的時間）
function showAt(p, t, dt = 0) {
  if (p.animator.clip !== p.animator.clips[state.clipName]) p.play(state.clipName, true);
  if (!p.animator.clip) return;
  p.animator.seek(Math.max(0, t - dt));
  p.update(dt);
}

// ---------- 關節點與姿勢 ----------
const jointsLayer = $("#joints");

function jointScreen(p, bone) {
  const m = p.world[bone];
  return [p.x + p.facing * p.scale * m[4], p.y + p.scale * m[5]];
}

function drawJoints() {
  const items = [];
  for (const b of rig().bones) {
    if (b.name === "prop") continue;
    const [x, y] = jointScreen(pet, b.name);
    const c = document.createElementNS(NS, "circle");
    Object.entries({ cx: x, cy: y, r: 5 }).forEach(([k, v]) => c.setAttribute(k, v));
    c.dataset.bone = b.name;
    if (b.name === state.bone) {
      c.classList.add("sel");
      const label = document.createElementNS(NS, "text");
      Object.entries({ x: x + 10, y: y - 8 }).forEach(([k, v]) => label.setAttribute(k, v));
      label.textContent = b.name;
      items.push(label);
    }
    items.push(c);
  }
  jointsLayer.replaceChildren(...items);
}

const currentValue = track => (state.clip.tracks[track] ? sampleTrack(state.clip.tracks[track], state.time) : rest(track));

// 在目前時間設定（或新增）關鍵影格
function setKey(track, t, value) {
  const keys = (state.clip.tracks[track] ??= []);
  let i = keys.findIndex(k => Math.abs(k[0] - t) < 0.004);
  if (i >= 0) keys[i][1] = round(value);
  else {
    keys.push([round(t), round(value)]);
    keys.sort((a, b) => a[0] - b[0]);
    i = keys.findIndex(k => Math.abs(k[0] - t) < 0.004);
  }
  state.sel = { track, index: i };
  changed();
}
const round = v => Math.round(v * 100) / 100;

let poseDrag = null;
// 滑鼠位置換成舞台（SVG）座標：關節點的座標是相對舞台左上角
const local = e => { const r = canvas.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
canvas.addEventListener("pointerdown", e => {
  const bone = e.target.dataset?.bone;
  if (!bone) return;
  state.bone = bone;
  state.playing = false;
  syncPlay();
  const [jx, jy] = jointScreen(pet, bone);
  const [px, py] = local(e);
  const translate = e.shiftKey || bone === "root";
  poseDrag = translate
    ? { bone, translate, sx: px, sy: py, x0: currentValue(`${bone}.x`), y0: currentValue(`${bone}.y`) }
    : { bone, translate, a0: Math.atan2(py - jy, px - jx), v0: currentValue(bone), jx, jy };
  canvas.setPointerCapture(e.pointerId);
  renderTimeline();
});
canvas.addEventListener("pointermove", e => {
  if (!poseDrag) return;
  const r = rig();
  const [px, py] = local(e);
  if (poseDrag.translate) {
    // 位移軌道以 Q 版身材寫（Skeleton 乘上 rootScale）
    const k = pet.scale * (r.rootScale ?? 1);
    setKey(`${poseDrag.bone}.x`, state.time, poseDrag.x0 + (px - poseDrag.sx) / k);
    setKey(`${poseDrag.bone}.y`, state.time, poseDrag.y0 + (py - poseDrag.sy) / k);
  } else {
    let delta = (Math.atan2(py - poseDrag.jy, px - poseDrag.jx) - poseDrag.a0) * 180 / Math.PI;
    delta = ((delta + 540) % 360) - 180;
    const angleScale = r.trackScale?.[poseDrag.bone] ?? 1;
    setKey(poseDrag.bone, state.time, poseDrag.v0 + delta / angleScale);
  }
});
canvas.addEventListener("pointerup", () => { poseDrag = null; });

// ---------- 時間軸 ----------
const boneOrder = Object.fromEntries(rig().bones.map((b, i) => [b.name, i]));
const order = track => (boneOrder[track.split(".")[0]] ?? 99) + (track.includes(".") ? 0.5 : 0);

// 表情類的列（@face、@eyes、@mouth）：關鍵影格存在動作的 face、eyes、mouth 欄位（字串，或 [[時間, 名稱], …]）
const DISCRETE = [["face", "表情"], ["eyes", "眼睛"], ["mouth", "嘴巴"]];
const isDiscrete = track => track.startsWith("@");
// 可以選的圖：表情 = 表情插槽的整張臉；眼睛、嘴巴 = 分層臉的圖層（沒有分層臉的角色不顯示這兩列）
function choicesOf(ch) {
  const r = rig();
  if (ch === "face") return Object.keys(r.slots.find(s => s.variants)?.variants ?? { idle: 1 });
  return Object.keys(r.face?.layers.find(l => l.name === ch)?.variants ?? {});
}
function discreteKeys(ch) {
  const v = state.clip[ch];
  return v == null ? [] : typeof v === "string" ? [[0, v]] : v.map(k => [...k]);
}
// 存回去：只有一個、在 0 秒 = 字串；沒有了 = 表情回到 idle、眼睛／嘴巴拿掉
function storeDiscrete(ch, keys) {
  keys.sort((a, b) => a[0] - b[0]);
  if (!keys.length) { if (ch === "face") state.clip.face = "idle"; else delete state.clip[ch]; }
  else state.clip[ch] = keys.length === 1 && keys[0][0] === 0 ? keys[0][1] : keys;
}
const discreteRows = () => DISCRETE.filter(([ch]) => ch === "face" || rig().face)
  .map(([ch, label]) => ({ track: `@${ch}`, label, keys: discreteKeys(ch) }));
const sampleAt = (keys, t) => keys.reduce((v, k) => (t >= k[0] ? k[1] : v), keys[0]?.[1]);

const timeline = new Timeline($("#timeline"), {
  onSeek: t => { state.time = t; state.playing = false; syncPlay(); timeline.setTime(t); },
  onSelect: (track, index) => {
    state.sel = index == null ? null : { track, index };
    if (isDiscrete(track)) {
      if (index != null) state.time = discreteKeys(track.slice(1))[index][0];
      renderTimeline();
      return;
    }
    state.bone = track.split(".")[0];
    if (index != null) state.time = state.clip.tracks[track][index][0];
    renderTimeline();
  },
  onMove: (track, index, t) => {
    const discrete = isDiscrete(track);
    const keys = discrete ? discreteKeys(track.slice(1)) : state.clip.tracks[track];
    const key = keys[index];
    key[0] = Math.min(state.clip.duration, Math.max(0, t));
    keys.sort((a, b) => a[0] - b[0]);
    const i = keys.indexOf(key);
    if (discrete) storeDiscrete(track.slice(1), keys);
    state.sel = { track, index: i };
    state.time = key[0];
    changed();
    return i;
  },
  onAdd: (track, t) => {
    state.time = t;
    if (!isDiscrete(track)) { setKey(track, t, currentValue(track)); return; }
    const ch = track.slice(1), keys = discreteKeys(ch);
    if (keys.some(k => Math.abs(k[0] - t) < 0.004)) return;
    // 預設：這一刻原本的圖（眼睛、嘴巴 = 目前表情用的那張）
    const own = rig().face?.expressions[sampleAt(discreteKeys("face"), t) ?? "idle"]?.[ch];
    keys.push([round(t), sampleAt(keys, t) ?? own ?? choicesOf(ch)[0]]);
    storeDiscrete(ch, keys);
    state.sel = { track, index: discreteKeys(ch).findIndex(k => Math.abs(k[0] - t) < 0.004) };
    changed();
  },
});

function renderTimeline() {
  timeline.render(state.clip, order, state.time, state.sel, discreteRows());
  renderKeyPanel();
}

function changed() {
  renderTimeline();
  syncFields();
}

addEventListener("keydown", e => {
  if (e.target?.matches?.("input, select")) return;
  if (e.key === " ") { e.preventDefault(); state.playing = !state.playing; syncPlay(); }
  else if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
    state.time = Math.min(state.clip.duration, Math.max(0, state.time + (e.key === "ArrowRight" ? 1 : -1) / 30));
    timeline.setTime(state.time);
  } else if ((e.key === "Delete" || e.key === "Backspace") && state.sel && isDiscrete(state.sel.track)) {
    const ch = state.sel.track.slice(1), keys = discreteKeys(ch);
    keys.splice(state.sel.index, 1);
    storeDiscrete(ch, keys);
    state.sel = null;
    changed();
  } else if ((e.key === "Delete" || e.key === "Backspace") && state.sel) {
    const keys = state.clip.tracks[state.sel.track];
    keys.splice(state.sel.index, 1);
    if (!keys.length) delete state.clip.tracks[state.sel.track];
    state.sel = null;
    changed();
  }
});

// ---------- 關鍵影格面板與曲線 ----------
const selKey = () => (state.sel && !isDiscrete(state.sel.track) ? state.clip.tracks[state.sel.track]?.[state.sel.index] : null);

function renderKeyPanel() {
  // 表情類：選圖，不用數值與曲線
  const d = state.sel && isDiscrete(state.sel.track) ? discreteKeys(state.sel.track.slice(1))[state.sel.index] : null;
  $("[data-choice-field]").hidden = !d;
  if (d) {
    const ch = state.sel.track.slice(1);
    fillSelect($("[data-key-choice]"), [...new Set([...choicesOf(ch), d[1]])].map(v => [v, v]), d[1]);
    $("[data-key-info]").textContent = `${DISCRETE.find(x => x[0] === ch)[1]} 在 ${d[0].toFixed(2)} 秒`;
  }
  const k = selKey();
  $("[data-key-value]").disabled = $("[data-curve]").disabled = !k;
  if (!d) $("[data-key-info]").textContent = k ? `${state.sel.track} 在 ${k[0].toFixed(2)} 秒` : "點時間軸上的關鍵影格";
  if (k) {
    $("[data-key-value]").value = k[1];
    $("[data-curve]").value = k[2] ?? "smooth";
  }
  drawCurve(k?.[2] ?? "smooth");
}

$("[data-key-choice]").onchange = e => {
  if (!state.sel || !isDiscrete(state.sel.track)) return;
  const ch = state.sel.track.slice(1), keys = discreteKeys(ch);
  keys[state.sel.index][1] = e.target.value;
  storeDiscrete(ch, keys);
  changed();
};
$("[data-key-value]").oninput = e => { const k = selKey(); if (k) { k[1] = Number(e.target.value); changed(); } };
$("[data-curve]").onchange = e => {
  const k = selKey();
  if (!k) return;
  if (e.target.value === "smooth") k.length = 2;
  else k[2] = e.target.value;
  changed();
};

function drawCurve(curve) {
  const c = $("[data-curve-preview]"), g = c.getContext("2d");
  const w = c.width, h = c.height, pad = 14;
  g.clearRect(0, 0, w, h);
  g.strokeStyle = "#4b4f66";
  g.strokeRect(pad, pad, w - pad * 2, h - pad * 2);
  g.strokeStyle = "#ffd75e";
  g.lineWidth = 2;
  g.beginPath();
  for (let i = 0; i <= 60; i++) {
    const u = i / 60;
    const x = pad + u * (w - pad * 2), y = h - pad - ease(curve, u) * (h - pad * 2);
    if (i) g.lineTo(x, y); else g.moveTo(x, y);
  }
  g.stroke();
}

// ---------- 欄位 ----------
function syncFields() {
  const c = state.clip;
  $("[data-label]").value = c.label ?? "";
  $("[data-duration]").value = c.duration;
  $("[data-face]").value = typeof c.face === "string" ? c.face : "";
  $("[data-face]").disabled = c.face != null && typeof c.face !== "string";
  $("[data-loop]").checked = !!c.loop;
  const r = rig();
  const dirty = JSON.stringify(c) !== state.original;
  $("[data-source]").textContent = `來源：${SOURCE_LABEL[sourceOf(r, state.clipName)]}${dirty ? "　（已修改，還沒存）" : ""}`;
}

$("[data-label]").oninput = e => { state.clip.label = e.target.value; syncFields(); };
$("[data-duration]").onchange = e => {
  const d = Math.max(0.05, Number(e.target.value) || state.clip.duration);
  // 長度改了：關鍵影格依比例跟著伸縮
  const ratio = d / state.clip.duration;
  for (const keys of Object.values(state.clip.tracks)) for (const k of keys) k[0] = round(k[0] * ratio);
  for (const [ch] of DISCRETE) if (Array.isArray(state.clip[ch])) for (const k of state.clip[ch]) k[0] = round(k[0] * ratio);
  state.clip.duration = d;
  state.time = Math.min(state.time, d);
  changed();
};
$("[data-face]").oninput = e => { state.clip.face = e.target.value || "idle"; syncFields(); };
$("[data-loop]").onchange = e => { state.clip.loop = e.target.checked; syncFields(); };

function fillSelect(sel, entries, value) {
  sel.replaceChildren(...entries.map(([v, label]) => Object.assign(document.createElement("option"), { value: v, textContent: label })));
  if (value != null && entries.some(([v]) => v === value)) sel.value = value;
}

function renderCharacter() {
  fillSelect($("[data-char]"), ids.map(id => [id, rigs[id].name]), state.charId);
  const forms = Object.entries(rigs[state.charId].forms ?? {});
  $("[data-form]").hidden = forms.length < 2;
  fillSelect($("[data-form]"), forms.map(([k, f]) => [k, f.label]), state.form ?? forms[0]?.[0]);
  const set = sets.for(rig());
  const all = [...new Set([...names, ...Object.keys(set)])].filter(n => set[n]);
  fillSelect($("[data-clip]"), all.map(n => [n, `${set[n].label ?? n}  ${n}${set[n].hidden ? "（隱藏）" : ""}`]), state.clipName);
  loadClip($("[data-clip]").value);
}

$("[data-char]").onchange = e => { state.charId = e.target.value; state.form = null; renderCharacter(); };
$("[data-form]").onchange = e => { state.form = e.target.value; renderCharacter(); };
$("[data-clip]").onchange = e => loadClip(e.target.value);
$("[data-new]").onclick = () => {
  const name = prompt("新動作的名稱（英數字，例如 bow）", `${state.clipName}2`);
  if (!name || !/^[A-Za-z0-9_-]+$/.test(name)) return;
  const copy = plainClip(state.clip);
  state.clipName = name;
  state.clip = { ...copy, label: name };
  state.original = "";
  editClips = { ...editClips, [name]: state.clip };
  fillSelect($("[data-clip]"), [...[...$("[data-clip]").options].map(o => [o.value, o.textContent]), [name, `${name}（新）`]], name);
  $("[data-target]").value = "character";
  buildPets();
  changed();
};
for (const sel of ["[data-onion]", "[data-compare]", "[data-overlay]"]) $(sel).onchange = buildPets;
$("[data-target]").onchange = buildPets;

function syncPlay() { $("[data-play]").textContent = state.playing ? "⏸ 暫停" : "▶ 播放"; }
$("[data-play]").onclick = () => { state.playing = !state.playing; syncPlay(); };
$("[data-speed]").onchange = e => { state.speed = Number(e.target.value); };

// ---------- 存檔 ----------
// 差異：和下一層的同名動作比，只留不一樣的軌道與欄位
function diffAgainst(base, clip) {
  const out = { extends: state.clipName };
  for (const [k, v] of Object.entries(clip)) {
    if (k === "tracks") continue;
    if (JSON.stringify(base[k]) !== JSON.stringify(v)) out[k] = v;
  }
  for (const k of Object.keys(base)) if (!(k in clip) && k !== "tracks" && k !== "extends") out[k] = null;     // 拿掉的欄位（例如眼睛軌道）
  const tracks = {};
  for (const [ch, keys] of Object.entries(clip.tracks)) if (JSON.stringify(base.tracks?.[ch]) !== JSON.stringify(keys)) tracks[ch] = keys;
  for (const ch of Object.keys(base.tracks ?? {})) if (!clip.tracks[ch]) tracks[ch] = null;
  out.tracks = tracks;
  return out;
}

$("[data-save]").onclick = async () => {
  const r = rig(), target = $("[data-target]").value;
  const status = $("[data-status]");
  let clip = plainClip(state.clip);
  const base = baseFor(target, state.clipName);
  if ($("[data-diff]").checked && base && (target === "character" || target === "form")) clip = diffAgainst(plainClip(base), clip);
  const body = { name: state.clipName, target, clip, body: bodyOf(r), char: r.animSource ?? r.id, form: r.formName };
  try {
    const res = await fetch("/api/animation", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error);
    state.original = JSON.stringify(state.clip);
    status.textContent = `已存到 ${data.path}${clip.extends ? "（只存差異）" : ""}。重新整理頁面、重新啟動桌寵就會用新的動作。`;
    syncFields();
  } catch (e) {
    status.textContent = `存檔失敗：${e.message}（要用 python tools/serve.py 開這個頁面）`;
  }
};

// ---------- 次級物理 ----------
const PHYS = [["stiffness", "彈簧", 10, 300, 1], ["damping", "阻尼", 0, 30, 0.5], ["drag", "空氣阻力", 0, 100, 1],
              ["inertia", "慣性", 0, 5, 0.1], ["gravity", "重力", -20, 20, 0.5], ["limit", "角度上限", 5, 90, 1]];
const PHYS_DEFAULT = { stiffness: 120, damping: 8, gravity: 0, inertia: 1, drag: 0, limit: 35, length: 60 };

function renderPhysics() {
  const r = rig(), box = $("[data-physics]");
  const bones = Object.keys(r.physics ?? {});
  if (!bones.length) { box.innerHTML = `<p class="hint">這個角色沒有物理骨頭（尾巴、披布、頭髮）。</p>`; return; }
  box.replaceChildren(...bones.flatMap(bone => {
    const h = document.createElement("div");
    h.className = "muted";
    h.textContent = bone;
    return [h, ...PHYS.map(([key, label, min, max, step]) => {
      const row = document.createElement("label");
      row.className = "field";
      const input = Object.assign(document.createElement("input"), { type: "range", min, max, step, value: r.physics[bone][key] ?? PHYS_DEFAULT[key] });
      const text = document.createElement("span");
      text.textContent = input.value;
      input.oninput = () => {
        r.physics[bone] = { ...PHYS_DEFAULT, ...r.physics[bone], [key]: Number(input.value) };
        text.textContent = input.value;
        pet.motion = new Spring(r.bones, r.physics);
      };
      row.append(label, input, text);
      return row;
    })];
  }));
}

$("[data-save-physics]").onclick = async () => {
  const r = rig(), status = $("[data-status]");
  const key = `${r.animSource ?? r.id}${r.formName && state.form ? `@${r.formName}` : ""}`;
  try {
    const res = await fetch("/api/physics", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ char: key, physics: r.physics }) });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error);
    status.textContent = `物理參數已存到 ${data.path}（${key}）。`;
  } catch (e) {
    status.textContent = `存檔失敗：${e.message}`;
  }
};

// ---------- 主迴圈 ----------
renderCharacter();
addEventListener("resize", layout);
let last = performance.now(), moveT = 0;
function frame(ts) {
  const dt = Math.min(0.05, (ts - last) / 1000);
  last = ts;
  layout();
  const c = state.clip;
  if (state.playing) {
    state.time += dt * state.speed;
    if (state.time > c.duration) state.time = c.loop ? state.time % c.duration : 0;
    timeline.setTime(state.time);
  }
  // 平移測試：左右來回、在兩端急停，看物理的甩動
  if ($("[data-move]").checked) {
    moveT += dt;
    const phase = moveT % 3;
    const offset = (phase < 1.2 ? phase / 1.2 : phase < 1.5 ? 1 : phase < 2.7 ? 1 - (phase - 1.5) / 1.2 : 0) * 180;
    pet.x += offset;
  }
  showAt(pet, state.time, dt);
  const span = c.duration * 0.12;
  if (onion.length) { showAt(onion[0], Math.max(0, state.time - span)); showAt(onion[1], Math.min(c.duration, state.time + span)); }
  for (const p of compare) showAt(p, state.time, dt);
  drawJoints();
  $("[data-time]").textContent = `${state.time.toFixed(2)} / ${c.duration.toFixed(2)} 秒`;
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// ?selftest=1：模擬拖關節、移動與刪除關鍵影格、計算差異，結果寫在標題（tools/web_test.ps1 讀 PASS）
if (new URLSearchParams(location.search).has("selftest")) {
  const fails = [];
  const check = (ok, what) => { if (!ok) fails.push(what); };
  await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
  // 1. 拖 armNear_u 的關節點：轉 30 度 → 在時間 0 有關鍵影格、數值變了
  state.time = 0;
  const before = currentValue("armNear_u");
  drawJoints();
  const dot = jointsLayer.querySelector('[data-bone="armNear_u"]');
  const [jx, jy] = [Number(dot.getAttribute("cx")), Number(dot.getAttribute("cy"))];
  const r0 = canvas.getBoundingClientRect();
  const at = (dx, dy) => ({ clientX: r0.left + jx + dx, clientY: r0.top + jy + dy, pointerId: 1, bubbles: true });
  dot.dispatchEvent(new PointerEvent("pointerdown", at(40, 0)));
  canvas.dispatchEvent(new PointerEvent("pointermove", at(40 * Math.cos(Math.PI / 6), 40 * Math.sin(Math.PI / 6))));
  canvas.dispatchEvent(new PointerEvent("pointerup", at(0, 0)));
  const k0 = state.clip.tracks.armNear_u.find(k => k[0] === 0);
  check(k0 && Math.abs(k0[1] - (before + 30)) < 1, `拖關節轉 30 度：${before} → ${k0?.[1]}`);
  // 2. 在 0.3 秒拖 → 新增關鍵影格並排好順序
  state.time = 0.3;
  setKey("armNear_u", 0.3, 55);
  const times = state.clip.tracks.armNear_u.map(k => k[0]);
  check(times.includes(0.3) && times.every((t, i) => !i || t >= times[i - 1]), `新增關鍵影格後要排序：${times}`);
  // 3. 移動關鍵影格、改曲線
  const idx = state.clip.tracks.armNear_u.findIndex(k => k[0] === 0.3);
  timeline.h.onMove("armNear_u", idx, 0.5);
  check(state.clip.tracks.armNear_u.some(k => k[0] === 0.5 && k[1] === 55), "移動關鍵影格到 0.5 秒");
  state.sel = { track: "armNear_u", index: state.clip.tracks.armNear_u.findIndex(k => k[0] === 0.5) };
  $("[data-curve]").value = "back";
  $("[data-curve]").onchange({ target: $("[data-curve]") });
  check(selKey()?.[2] === "back", "改曲線");
  // 4. 刪除
  dispatchEvent(new KeyboardEvent("keydown", { key: "Delete" }));
  check(!state.clip.tracks.armNear_u.some(k => k[0] === 0.5), "Delete 刪除關鍵影格");
  // 5. 差異：只留改過的軌道
  const diff = diffAgainst(plainClip(sharedClips.walk), state.clip);
  check(diff.extends === "walk" && Object.keys(diff.tracks).join() === "armNear_u", `差異只留 armNear_u：${Object.keys(diff.tracks)}`);
  const merged = extendClip(sharedClips.walk, diff);
  check(JSON.stringify(merged.tracks.armNear_u) === JSON.stringify(state.clip.tracks.armNear_u) && merged.tracks.legNear_u === sharedClips.walk.tracks.legNear_u, "差異合併回來要一樣");
  // 6. 表情類的列：嘴巴加兩個關鍵影格、換圖，動作的 mouth 欄位要跟著變，角色要換嘴巴圖
  if (rig().face) {
    timeline.h.onAdd("@mouth", 0);
    timeline.h.onAdd("@mouth", 0.3);
    $("[data-key-choice]").value = "va";
    $("[data-key-choice]").dispatchEvent(new Event("change"));
    const own = rig().face.expressions.idle.mouth;
    check(JSON.stringify(state.clip.mouth) === JSON.stringify([[0, own], [0.3, "va"]]), `mouth 欄位：${JSON.stringify(state.clip.mouth)}`);
    showAt(pet, 0.35);
    check(pet.view.faceLayers.find((it, i) => rig().face.layers[i].name === "mouth").file?.endsWith("mouth_va.svg"), "0.35 秒要換成 va 嘴型");
    dispatchEvent(new KeyboardEvent("keydown", { key: "Delete" }));
    dispatchEvent(new KeyboardEvent("keydown", { key: "Delete" }));
    check(typeof state.clip.mouth === "string" || state.clip.mouth === undefined, `刪到剩一個要變回字串：${JSON.stringify(state.clip.mouth)}`);
  } else fails.push(`${rig().id} 沒有分層臉，表情列沒測到`);
  $("[data-status]").textContent = fails.length ? fails.join("；") : "自我測試通過";
  document.title = fails.length ? `FAIL ${fails.length}: ${fails.join("；")}` : "PASS";
} else document.title = "DONE";
