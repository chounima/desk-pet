// 動作總覽：同一個角色的所有動作排成格子同時播放
import { TUNING } from "./config.js";
import { formRig, loadAll } from "./core/loader.js";
import { Pet } from "./game/pet.js";

const CELL = { w: 230, h: 300, ground: 250, scale: 0.7, fitHeight: 270 };
const NS = "http://www.w3.org/2000/svg";
const $ = id => document.getElementById(id);

const [{ ids, rigs }, { names, sets }, props] = await loadAll();
const q = new URLSearchParams(location.search);
let charId = q.get("char") ?? ids[0];
const rig = formRig(rigs[charId], q.get("form"));
const clips = sets[rig.body ?? "humanoid"] ?? sets.humanoid;
const freezeAt = q.has("t") ? Number(q.get("t")) : null;

// 角色切換
for (const id of ids) {
  const a = document.createElement("a");
  a.href = `?char=${id}`;
  a.textContent = rigs[id].name;
  if (id === charId) a.className = "on";
  $("chars").append(a);
}
// 型態切換（例如 ?char=choupaca&form=quadruped）
for (const [name, f] of Object.entries(rigs[charId].forms ?? {})) {
  const a = document.createElement("a");
  a.href = `?char=${charId}&form=${name}`;
  a.textContent = `🔁 ${f.label}`;
  if (f.data === rig) a.className = "on";
  $("chars").append(a);
}

// 依視窗寬度排格子
const cols = Math.max(1, Math.floor((innerWidth - 32) / CELL.w));
const svg = $("grid");
svg.setAttribute("width", cols * CELL.w);
svg.setAttribute("height", Math.ceil(names.length / cols) * CELL.h);

const pets = names.map((name, i) => {
  const cx = (i % cols) * CELL.w, cy = Math.floor(i / cols) * CELL.h;
  const layer = document.createElementNS(NS, "g");
  const bg = document.createElementNS(NS, "rect");
  Object.entries({ x: cx + 6, y: cy + 6, width: CELL.w - 12, height: CELL.h - 12, rx: 12 })
    .forEach(([k, v]) => bg.setAttribute(k, v));
  bg.classList.add("cell");
  const floor = document.createElementNS(NS, "line");
  Object.entries({ x1: cx + 20, x2: cx + CELL.w - 20, y1: cy + CELL.ground, y2: cy + CELL.ground })
    .forEach(([k, v]) => floor.setAttribute(k, v));
  floor.classList.add("floor");
  const label = document.createElementNS(NS, "text");
  label.setAttribute("x", cx + CELL.w / 2);
  label.setAttribute("y", cy + CELL.h - 22);
  label.textContent = `${clips[name].label}  ${name}`;
  layer.append(bg, floor, label);
  svg.append(layer);

  // 高個子角色縮小，讓每個角色在格子裡差不多大
  const scale = CELL.scale * Math.min(1, CELL.fitHeight / (rig.height ?? CELL.fitHeight));
  const pet = new Pet({ layer, rig, clipSets: sets, props, scale, crossfade: TUNING.crossfade });
  pet.x = cx + CELL.w / 2;
  pet.y = cy + CELL.ground;
  pet.play(name, true);
  pet.animator.seek(freezeAt ?? 0);
  return pet;
});

let last = performance.now();
function frame(ts) {
  const dt = freezeAt !== null ? 0 : Math.min(0.05, (ts - last) / 1000);
  last = ts;
  for (const pet of pets) {
    if (pet.animator.finished) pet.play(pet.action, true);   // 非循環動作也重播
    pet.update(dt);
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
