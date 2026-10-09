// 桌面預覽進入點：載入資料、建立寵物、綁定輸入、主迴圈
import { TUNING, VOLLEY } from "./config.js";
import { loadAll } from "./core/loader.js";
import { Behavior } from "./game/behavior.js";
import { Pet } from "./game/pet.js";
import { Volley } from "./game/volley.js";
import { Panel } from "./ui/panel.js";
import { BallView } from "./view/ball.js";
import { Fx } from "./view/fx.js";

const $ = id => document.getElementById(id);
const [{ ids, rigs }, { names, clips }, props] = await loadAll();
const q = new URLSearchParams(location.search);

// ---------- 世界與寵物 ----------
const world = {
  get width() { return innerWidth; },
  get groundY() { return innerHeight - TUNING.taskbar; },
};
const pet = new Pet({
  layer: $("pets"), rig: rigs[q.get("char") ?? ids[0]], clips, props,
  scale: Number(q.get("scale") ?? TUNING.scale), crossfade: TUNING.crossfade,
});
pet.x = world.width / 2;
pet.y = world.groundY;
const behavior = new Behavior(pet, world);
const fx = new Fx($("fx"));
let score = 0;

// ---------- 排球對打 ----------
const volley = new Volley({
  pet, world, fx,
  view: new BallView($("world"), props.volleyball, VOLLEY.artRadius, () => volley.onBallClick()),
  onScore: (rally, best) => {
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
  onCharacter: id => { pet.setCharacter(rigs[id]); panel.setCharacter(id); },
  onAction: name => { if (!volley.active) behavior.force(name); },
  onAuto: on => behavior.setAuto(on),
  onVolley: toggleVolley,
});
panel.setCharacter(pet.rig.id);

// ---------- 輸入 ----------
addEventListener("keydown", e => {
  if (e.repeat) return;
  behavior.onKey();
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
  // ?debug=1：顯示寵物對上一顆來球的殺球判斷
  if (q.has("debug") && volley.active) $("debugHud").textContent = `殺球判斷：${volley.spikeWhy ?? "—"}`;
  $("clock").textContent = new Date().toLocaleTimeString("zh-TW", { hour: "2-digit", minute: "2-digit" });
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
