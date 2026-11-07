// 錄影：某角色某動作從 0 秒開始，每一步固定前進 1/60 秒，等距排出 N 格（和桌寵的 --record 用同樣的方式，見 tools/record_clip.ps1）
// ?char=sera&form=&clip=walk&frames=12&cols=6
import { TUNING } from "./config.js";
import { formRig, loadAll } from "./core/loader.js";
import { Pet } from "./game/pet.js";

const STEP = 1 / 60;
const CELL = { w: 200, h: 290, ground: 260, scale: 0.7, fitHeight: 270 };
const NS = "http://www.w3.org/2000/svg";
const $ = id => document.getElementById(id);

const [{ ids, rigs }, { sets }, props] = await loadAll();
const q = new URLSearchParams(location.search);
const charId = q.get("char") ?? ids[0];
const rig = formRig(rigs[charId], q.get("form"));
const clips = sets[rig.body ?? "humanoid"] ?? sets.humanoid;
const clipName = q.get("clip") ?? "idle";
const clip = clips[clipName];
const frames = Math.max(2, Number(q.get("frames") ?? 12));
const cols = Number(q.get("cols") ?? 6);
// 和 Godot 一樣：N 張平均分布在動作長度內（循環動作的最後一張不和第一張重複）
const stepsPer = Math.max(1, Math.round(clip.duration / STEP / (clip.loop ? frames : frames - 1)));
$("info").textContent = `${rig.name} ${clip.label} ${clipName}：${clip.duration}s，${frames} 張，每 ${stepsPer} 步一張`;

const svg = $("grid");
svg.setAttribute("width", cols * CELL.w);
svg.setAttribute("height", Math.ceil(frames / cols) * CELL.h);
const scale = CELL.scale * Math.min(1, CELL.fitHeight / (rig.height ?? CELL.fitHeight));

for (let k = 0; k < frames; k++) {
  const cx = (k % cols) * CELL.w, cy = Math.floor(k / cols) * CELL.h;
  const layer = document.createElementNS(NS, "g");
  const bg = document.createElementNS(NS, "rect");
  Object.entries({ x: cx + 4, y: cy + 4, width: CELL.w - 8, height: CELL.h - 8, rx: 10 }).forEach(([a, v]) => bg.setAttribute(a, v));
  bg.classList.add("cell");
  const label = document.createElementNS(NS, "text");
  label.setAttribute("x", cx + 12);
  label.setAttribute("y", cy + CELL.h - 10);
  label.textContent = `#${k}`;
  layer.append(bg, label);
  svg.append(layer);

  // 每一格各自從 0 秒模擬到第 k 張的時間（之後有物理時，甩動才會一樣）
  const pet = new Pet({ layer, rig, clipSets: sets, props, scale, crossfade: TUNING.crossfade });
  pet.x = cx + CELL.w / 2;
  pet.y = cy + CELL.ground;
  pet.play(clipName, true);
  pet.animator.seek(0);
  pet.update(0);
  for (let s = 0; s < k * stepsPer; s++) pet.update(STEP);
}
document.title = "DONE";
