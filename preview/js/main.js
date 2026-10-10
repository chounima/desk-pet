// 桌面預覽進入點：載入資料、建立寵物、綁定輸入、主迴圈
import { TUNING, VOLLEY } from "./config.js";
import { formRig, loadAll, loadProgression } from "./core/loader.js";
import { Behavior } from "./game/behavior.js";
import { Pet } from "./game/pet.js";
import { Progress } from "./game/progress.js";
import { Volley } from "./game/volley.js";
import { loadSkills } from "./game/volleySkills.js";
import { Collection } from "./ui/collection.js";
import { DevPanel, OVERRIDE_KEY } from "./ui/devPanel.js";
import { Panel } from "./ui/panel.js";
import { BallView } from "./view/ball.js";
import { Fx } from "./view/fx.js";

const $ = id => document.getElementById(id);
const [{ ids, rigs }, { names, clips, sets }, props] = await loadAll();
await loadSkills();
const q = new URLSearchParams(location.search);

// ---------- 收集系統（進度存在瀏覽器 localStorage）----------
const SAVE_KEY = "deskpet.progress";
const store = {
  get: k => { try { return localStorage.getItem(k); } catch { return null; } },
  set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* 無痕模式等情況存不了 */ } },
};
const dev = q.has("dev");
const { defaults: progressDefaults, cfg: progressCfg } = await loadProgression(dev ? store.get(OVERRIDE_KEY) : null);
let savedProgress = {};
try { savedProgress = JSON.parse(store.get(SAVE_KEY) ?? "{}"); } catch { /* 存檔壞掉就重新開始 */ }
const progress = new Progress(progressCfg, savedProgress, s => store.set(SAVE_KEY, JSON.stringify(s)));

// ---------- 世界與寵物 ----------
const world = {
  get width() { return innerWidth; },
  get groundY() { return innerHeight - TUNING.taskbar; },
};
// 每個角色選的型態（?form=quadruped 可指定起始角色的型態）
const forms = {};
let charId = q.get("char") ?? ids[0];
if (q.has("form")) forms[charId] = q.get("form");
const currentRig = () => formRig(rigs[charId], forms[charId]);
const pet = new Pet({
  layer: $("pets"), rig: currentRig(), clipSets: sets, props,
  scale: Number(q.get("scale") ?? TUNING.scale), crossfade: TUNING.crossfade,
});
pet.x = world.width / 2;
pet.y = world.groundY;
const behavior = new Behavior(pet, world);
const fx = new Fx($("fx"));

// 點數入帳：短時間內的合併成一個「+N 💎」
let pendingPoints = 0, pointsTimer = 0;
function showToast(text) {
  const t = $("toast");
  t.textContent = text;
  t.hidden = false;
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => { t.hidden = true; }, 3000);
}
progress.onEvent = e => {
  if (e.type === "points") pendingPoints += e.amount;
  if (e.type === "levelUp" && e.char === charId) fx.burst(`❤ Lv${e.level}!`, pet.headTop(), "fx-level");
  if (e.type === "levelUp") showToast(`❤ ${rigs[e.char]?.name ?? e.char} 好感度 Lv${e.level}！`);
  if (e.type === "unlock") {
    const [kind, what] = e.key.split(":");
    const [id, form] = what.split("/");
    showToast(kind === "character" ? `🔓 解鎖新角色：${rigs[id]?.name ?? id}！`
      : `🔓 解鎖 ${rigs[id]?.name ?? id} 的${rigs[id]?.forms?.[form]?.label ?? form}型態！`);
    refreshLocks();
  }
};
let score = 0;

// ---------- 排球對打 ----------
const volley = new Volley({
  pet, world, fx,
  view: new BallView($("world"), props.volleyball, VOLLEY.artRadius, () => volley.onBallClick()),
  onScore: (rally, best) => {
    progress.onVolley(charId, rally);
    const fire = rally >= VOLLEY.fireAt ? "🔥" : "🏐";
    $("volleyHud").textContent = `· ${fire} 來回 ${rally}（最佳 ${best}）· ${pet.rig.name}：${volley.skill.title}`;
  },
  onShake: () => {
    const w = $("world");
    w.classList.remove("shake");
    void w.getBoundingClientRect();   // 重新觸發動畫
    w.classList.add("shake");
  },
});
behavior.minigame = volley;

function toggleVolley() {
  if (volley.active) {
    volley.stop();
    behavior.start("idle");
    $("volleyHud").textContent = "";
  } else if (behavior.state === "ground") {
    volley.start();
  }
  panel.setVolley(volley.active);
}

// ---------- 面板 ----------
const panel = new Panel($("panel"), {
  characters: ids.map(id => rigs[id]),
  actions: names.map(n => [n, clips[n]]),
  onCharacter: id => { charId = id; switchRig(); },
  onForm: form => { forms[charId] = form; switchRig(); fx.burst("💨", pet.headTop(), "fx-heart"); },
  onAction: name => { if (!volley.active) behavior.force(name); },
  onAuto: on => behavior.setAuto(on),
  onVolley: toggleVolley,
  onCollection: () => collection.toggle(),
});
const collection = new Collection($("collection"), { progress, ids, rigs });
if (dev) new DevPanel($("dev"), { progress, defaults: progressDefaults, getChar: () => charId, onChange: () => { refreshLocks(); collection.render(); } });
const defaultForm = id => Object.keys(rigs[id].forms ?? {})[0];
// 鎖住時回傳提示文字（顯示在按鈕的 title）
function lockHint(key) {
  const r = progress.requirement(key);
  const rule = progress.cfg.unlocks[key];
  if (!r) return "尚未解鎖";
  return `${rule.hint ?? "尚未解鎖"}（${r.cost ? `💎 ${r.cost}` : `${rigs[r.char]?.name ?? r.char} ❤ Lv${r.have} / ${r.need}`}）`;
}
function refreshLocks() {
  panel.setLocks(id => (progress.characterUnlocked(id) ? null : lockHint(`character:${id}`)));
  const all = rigs[charId].forms ?? {};
  panel.setForms(all, forms[charId] in all ? forms[charId] : defaultForm(charId),
    name => (progress.formUnlocked(charId, name, defaultForm(charId)) ? null : lockHint(`form:${charId}/${name}`)));
  collection.render();
}
function switchRig() {
  pet.setCharacter(currentRig());
  panel.setCharacter(charId);
  refreshLocks();
}
progress.checkDay();
progress.checkUnlocks();
switchRig();

// ---------- 輸入 ----------
addEventListener("keydown", e => {
  if (e.repeat) return;
  behavior.onKey();
  progress.onKey(charId);
  $("score").textContent = ++score;
});

let drag = null;
$("world").addEventListener("pointerdown", e => {
  if (!e.target.closest(".pet") || volley.active) return;
  drag = { x: e.clientX, y: e.clientY, moved: false };
  e.target.setPointerCapture(e.pointerId);
});
addEventListener("pointermove", e => {
  volley.onPointerMove(e.clientX, e.clientY);
  if (!drag) return;
  if (!drag.moved && Math.hypot(e.clientX - drag.x, e.clientY - drag.y) > 5) {
    drag.moved = true;
    behavior.grab(e.clientX, e.clientY);
  }
  if (drag.moved) behavior.moveHeld(e.clientX, e.clientY);
});
addEventListener("pointerup", () => {
  if (!drag) return;
  if (drag.moved) behavior.release();
  else {
    behavior.onPetClick();
    progress.onInteraction(charId);
    fx.burst("♥", pet.headTop(), "fx-heart");
  }
  drag = null;
});
$("wallpaper").addEventListener("pointerdown", () => {
  behavior.onActivity();
  $("score").textContent = ++score;
});

// ---------- 截圖用：?char=sera&action=walk&t=0.3 ----------
let frozen = false;
if (q.has("action")) {
  behavior.setAuto(false);
  $("panel").querySelector("[data-auto]").checked = false;
  behavior.force(q.get("action"));
  if (q.has("t")) {
    pet.animator.seek(Number(q.get("t")));
    frozen = true;
  }
}
if (q.has("collection")) collection.toggle(true);     // 截圖用：直接打開收藏視窗
if (q.has("volley")) {
  toggleVolley();
  // 截圖用：?volley=1&rally=12 直接從第 12 次來回開始（檢查火焰特效）
  if (q.has("rally")) {
    volley.rally = Number(q.get("rally"));
    volley.checkFire();
    volley.onScore(volley.rally, volley.best);
  }
}

// ---------- 主迴圈 ----------
let last = performance.now();
function frame(ts) {
  const dt = frozen ? 0 : Math.min(0.05, (ts - last) / 1000);
  last = ts;
  if (frozen) pet.y = world.groundY;
  else behavior.update(dt);
  volley.update(dt);
  pet.update(dt);
  fx.update(dt, pet.animator.fx, pet.headTop());
  panel.setAction(pet.action);
  if (!frozen) progress.tick(dt, charId);
  pointsTimer -= dt;
  if (pendingPoints > 0 && pointsTimer <= 0) {
    fx.burst(`+${pendingPoints} 💎`, pet.headTop(), "fx-points");
    pendingPoints = 0;
    pointsTimer = 0.6;
  }
  $("points").textContent = `💎 ${progress.state.points}`;
  if (collection.open && Math.floor(ts / 1000) !== Math.floor((ts - dt * 1000) / 1000)) collection.render();
  // ?debug=1：顯示寵物對上一顆來球的殺球判斷
  if (q.has("debug") && volley.active) $("debugHud").textContent = `殺球判斷：${volley.spikeWhy ?? "—"}`;
  $("clock").textContent = new Date().toLocaleTimeString("zh-TW", { hour: "2-digit", minute: "2-digit" });
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
