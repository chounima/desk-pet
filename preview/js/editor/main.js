// 自訂配件／衣服編輯器：上傳圖片 → 在動起來的角色身上拖曳對齊 → 匯出 json
// 配件掛在角色的錨點上（rig.anchors），衣服換掉同名部件；兩者的座標都是 Q 版單位，桌寵版用同一套規則畫
import { PATHS, TUNING } from "../config.js";
import { formRig, loadAll } from "../core/loader.js";
import { apply, mul } from "../core/math.js";
import { Pet } from "../game/pet.js";
import { accessoryJson, boxOf, download, folderName, outfitJson } from "./exporter.js";

const $ = sel => document.querySelector(sel);
const NS = "http://www.w3.org/2000/svg";
const [{ ids, rigs }, { names, sets }, props] = await loadAll();
const slots = await (await fetch(`${PATHS.art}/accessories/slots.json`)).json();

const PART_LABELS = {
  head: "頭", hair_front: "瀏海", hair_back: "後髮", torso: "上身", hips: "下身（腰）", arm_u: "上臂", arm_l: "前臂",
  hand: "手", leg_u: "大腿", leg_l: "小腿", foot: "腳／鞋", tail: "尾巴", cape: "披布", back: "背後的東西",
};

const state = {
  mode: "accessory",
  charId: ids.includes("chouma") ? "chouma" : ids[0],
  form: null,
  acc: { name: "我的配件", slot: "head", img: null },
  outfit: { name: "我的衣服", parts: {}, selected: null },
  paused: false,
};

// ---------- 舞台 ----------
const canvas = $("#canvas");
const ghostLayer = document.createElementNS(NS, "g");
ghostLayer.classList.add("ghost");
ghostLayer.style.pointerEvents = "none";
const outline = document.createElementNS(NS, "polygon");
outline.setAttribute("data-outline", "");
canvas.append(ghostLayer, outline);

let pet, ghost;
const rig = () => formRig(rigs[state.charId], state.form);
const clipsFor = r => sets[r.body ?? "humanoid"] ?? sets.humanoid;

function buildPets() {
  $("#pets").replaceChildren();
  ghostLayer.replaceChildren();
  const opts = { rig: rig(), clipSets: sets, props, scale: 2, crossfade: TUNING.crossfade };
  pet = new Pet({ layer: $("#pets"), ...opts });
  ghost = new Pet({ layer: ghostLayer, ...opts });          // 原本的部件，半透明疊在上面對齊用
  playAction($("[data-action]").value || "idle");
  applyEdits();
}

function playAction(name) {
  pet.play(name, true);
  ghost.play(name, true);
}

function layout() {
  const { width, height } = canvas.getBoundingClientRect();
  pet.x = ghost.x = width / 2;
  pet.y = ghost.y = height * 0.88;
  // 依角色身高放大，整個人都看得到（高個子不會被切到頭）
  pet.scale = ghost.scale = Math.min(3, (height * 0.78) / (rig().height ?? 270));
  const ground = $("[data-ground]");
  Object.entries({ x1: 0, x2: width, y1: pet.y, y2: pet.y }).forEach(([k, v]) => ground.setAttribute(k, v));
}

// ---------- 目前在編輯的圖 ----------
function currentImg() {
  return state.mode === "accessory" ? state.acc.img : state.outfit.parts[state.outfit.selected] ?? null;
}

// 圖片所在的座標系：骨骼世界矩陣（配件再乘上錨點的位移與縮放）
function frameOf() {
  const r = rig();
  if (state.mode === "accessory") {
    const a = r.anchors?.[slots[state.acc.slot].anchor];
    if (!a) return null;
    return mul(pet.world[a.bone], [a.scale, 0, 0, a.scale, a.x, a.y]);
  }
  const slot = partSlot(state.outfit.selected);
  return slot ? pet.world[slot.bone] : null;
}

// 部件圖名 → 插槽（優先用近側，畫在前面比較好對齊）
function partSlot(art) {
  const list = rig().slots.filter(s => s.file?.endsWith(`/${art}.svg`));
  return list.find(s => !s.far) ?? list[0];
}

function applyEdits() {
  const accImg = state.acc.img;
  pet.setAccessories(accImg ? [{ url: accImg.url, box: boxOf(accImg), anchor: slots[state.acc.slot].anchor, z: slots[state.acc.slot].z }] : []);
  const map = {};
  for (const [art, img] of Object.entries(state.outfit.parts)) map[art] = { url: img.url, box: boxOf(img) };
  pet.setOutfit(Object.keys(map).length ? map : null);
  ghostLayer.style.display = state.mode === "outfit" && $("[data-ghost]").checked ? "" : "none";
  syncFields();
  updateHint();
}

// ---------- 上傳 ----------
function readImage(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => resolve({ url: reader.result, fileName: file.name, natW: img.naturalWidth || 100, natH: img.naturalHeight || 100 });
      img.onerror = reject;
      img.src = reader.result;
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

$("[data-acc-file]").onchange = async e => {
  const file = e.target.files[0];
  if (!file) return;
  const pic = await readImage(file);
  const w = 60;      // 預設寬 60（大約一個 Q 版頭寬）
  state.acc.img = { ...pic, w, h: (w * pic.natH) / pic.natW, x: 0, y: state.acc.slot === "head" ? -6 : 0, scale: 1 };
  applyEdits();
};

$("[data-part-file]").onchange = async e => {
  const file = e.target.files[0];
  const art = state.outfit.selected;
  if (!file || !art) return;
  const pic = await readImage(file);
  // 預設蓋在原本部件的範圍上（寬度相同、高度依圖片比例）
  const [x0, y0, x1, y1] = partSlot(art).box;
  const w = x1 - x0;
  state.outfit.parts[art] = { ...pic, w, h: (w * pic.natH) / pic.natW, x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 };
  renderPartList();
  applyEdits();
};

// ---------- 欄位 ----------
function syncFields() {
  const img = currentImg();
  for (const sel of ["[data-x]", "[data-y]", "[data-scale]"]) $(sel).disabled = !img;
  if (!img) { $("[data-scale-text]").textContent = ""; return; }
  $("[data-x]").value = Math.round(img.x * 10) / 10;
  $("[data-y]").value = Math.round(img.y * 10) / 10;
  $("[data-scale]").value = img.scale;
  $("[data-scale-text]").textContent = `${img.scale.toFixed(2)}×`;
}

$("[data-x]").oninput = e => { const img = currentImg(); if (img) { img.x = Number(e.target.value); applyEdits(); } };
$("[data-y]").oninput = e => { const img = currentImg(); if (img) { img.y = Number(e.target.value); applyEdits(); } };
$("[data-scale]").oninput = e => { const img = currentImg(); if (img) { img.scale = Number(e.target.value); applyEdits(); } };
$("[data-reset]").onclick = () => {
  const img = currentImg();
  if (!img) return;
  if (state.mode === "accessory") Object.assign(img, { x: 0, y: 0, scale: 1 });
  else { const [x0, y0, x1, y1] = partSlot(state.outfit.selected).box; Object.assign(img, { x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 }); }
  applyEdits();
};
$("[data-remove]").onclick = () => {
  if (state.mode === "accessory") state.acc.img = null;
  else delete state.outfit.parts[state.outfit.selected];
  renderPartList();
  applyEdits();
};
$("[data-acc-name]").oninput = e => { state.acc.name = e.target.value; updateHint(); };
$("[data-outfit-name]").oninput = e => { state.outfit.name = e.target.value; updateHint(); };
$("[data-acc-slot]").onchange = e => { state.acc.slot = e.target.value; applyEdits(); };
$("[data-ghost]").onchange = applyEdits;
$("[data-pause]").onchange = e => { state.paused = e.target.checked; };
$("[data-action]").onchange = e => playAction(e.target.value);

// ---------- 拖曳：螢幕位移 → 圖片所在座標系的位移 ----------
let drag = null;
canvas.addEventListener("pointerdown", e => {
  if (!currentImg()) return;
  drag = { x: e.clientX, y: e.clientY };
  canvas.setPointerCapture(e.pointerId);
});
canvas.addEventListener("pointermove", e => {
  const img = currentImg(), m = frameOf();
  if (!drag || !img || !m) return;
  const sx = (e.clientX - drag.x) / (pet.facing * pet.scale), sy = (e.clientY - drag.y) / pet.scale;
  const det = m[0] * m[3] - m[1] * m[2];
  img.x += (m[3] * sx - m[2] * sy) / det;
  img.y += (-m[1] * sx + m[0] * sy) / det;
  drag = { x: e.clientX, y: e.clientY };
  applyEdits();
});
canvas.addEventListener("pointerup", () => { drag = null; });
canvas.addEventListener("wheel", e => {
  const img = currentImg();
  if (!img) return;
  e.preventDefault();
  img.scale = Math.min(4, Math.max(0.1, img.scale * (e.deltaY < 0 ? 1.05 : 0.95)));      // 滾輪縮放
  applyEdits();
}, { passive: false });

// 選取框：圖片範圍的四個角換算到螢幕
function drawOutline() {
  const img = currentImg(), m = frameOf();
  if (!img || !m) { outline.setAttribute("points", ""); return; }
  const [x0, y0, x1, y1] = boxOf(img);
  const pts = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]].map(([x, y]) => {
    const [px, py] = apply(m, x, y);
    return `${pet.x + pet.facing * pet.scale * px},${pet.y + pet.scale * py}`;
  });
  outline.setAttribute("points", pts.join(" "));
}

// ---------- 角色、模式 ----------
function fillSelect(sel, entries, value) {
  sel.replaceChildren(...entries.map(([v, label]) => Object.assign(document.createElement("option"), { value: v, textContent: label })));
  if (value != null) sel.value = value;
}

function renderCharacter() {
  fillSelect($("[data-char]"), ids.map(id => [id, rigs[id].name]), state.charId);
  const forms = Object.entries(rigs[state.charId].forms ?? {});
  $("[data-form]").hidden = forms.length < 2;
  fillSelect($("[data-form]"), forms.map(([k, f]) => [k, f.label]), state.form ?? forms[0]?.[0]);
  const clips = clipsFor(rig());
  fillSelect($("[data-action]"), names.filter(n => !clips[n]?.hidden).map(n => [n, clips[n].label]), "idle");
  const arts = [...new Set(rig().slots.filter(s => s.file && !s.variants).map(s => s.file.split("/").pop().replace(".svg", "")))];
  fillSelect($("[data-part]"), arts.map(a => [a, PART_LABELS[a] ?? a]), state.outfit.selected ?? "torso");
  state.outfit.selected = $("[data-part]").value;
  buildPets();
  renderPartList();
}

$("[data-char]").onchange = e => {
  state.charId = e.target.value;
  state.form = null;
  state.outfit.parts = {};       // 衣服是做給特定角色的，換角色就清空
  renderCharacter();
};
$("[data-form]").onchange = e => { state.form = e.target.value; state.outfit.parts = {}; renderCharacter(); };
$("[data-part]").onchange = e => { state.outfit.selected = e.target.value; renderPartList(); applyEdits(); };

function renderPartList() {
  const list = $("[data-part-list]");
  list.replaceChildren(...Object.keys(state.outfit.parts).map(art => {
    const b = document.createElement("button");
    b.textContent = `✓ ${PART_LABELS[art] ?? art}`;
    b.classList.toggle("on", art === state.outfit.selected);
    b.onclick = () => { state.outfit.selected = art; $("[data-part]").value = art; renderPartList(); applyEdits(); };
    return b;
  }));
}

for (const b of document.querySelectorAll("[data-mode]")) {
  b.onclick = () => {
    state.mode = b.dataset.mode;
    for (const x of document.querySelectorAll("[data-mode]")) x.classList.toggle("on", x === b);
    for (const p of document.querySelectorAll("[data-panel]")) p.hidden = p.dataset.panel !== state.mode;
    applyEdits();
  };
}

// ---------- 匯出 ----------
function updateHint() {
  const hint = $("[data-hint]");
  if (state.mode === "accessory") {
    const img = state.acc.img;
    hint.textContent = img
      ? `把 accessory.json 和圖片「${img.fileName}」一起放進：\n文件\\DeskPet\\accessories\\${folderName(state.acc.name)}\\\n再到桌寵的「👗 衣櫃」按「🔄 重新讀取」，配件會出現在「配件」裡（免費）。`
      : "先選一張圖片（建議去背的 PNG）。";
  } else {
    const files = Object.values(state.outfit.parts).map(p => p.fileName);
    hint.textContent = files.length
      ? `把 outfit.json 和這些圖片放進：\n文件\\DeskPet\\outfits\\${folderName(state.outfit.name)}\\\n${files.join("、")}\n只有 ${rigs[state.charId].name} 能穿，在「👗 衣櫃」選擇。`
      : "選一個部件、上傳圖片蓋上去；可以換很多個部件。";
  }
}

$("[data-export]").onclick = () => {
  if (state.mode === "accessory") {
    if (!state.acc.img) return alert("還沒有圖片");
    download("accessory.json", accessoryJson(state.acc.name, state.acc.slot, state.acc.img));
  } else {
    if (!Object.keys(state.outfit.parts).length) return alert("還沒有任何部件");
    download("outfit.json", outfitJson(state.outfit.name, state.charId, state.outfit.parts));
  }
};

// ---------- 主迴圈 ----------
renderCharacter();

// 截圖測試用：?demo=accessory 或 ?demo=outfit 用現成的圖當作上傳的圖片
const q = new URLSearchParams(location.search);
if (q.has("demo")) {
  const asData = async url => {
    const blob = await (await fetch(url)).blob();
    return readImage(new File([blob], url.split("/").pop(), { type: blob.type }));
  };
  if (q.get("demo") === "outfit") {
    document.querySelector('[data-mode="outfit"]').click();
    const pic = await asData(`${PATHS.art}/props/book.svg`);
    const [x0, y0, x1, y1] = partSlot("torso").box;
    state.outfit.parts.torso = { ...pic, w: x1 - x0, h: ((x1 - x0) * pic.natH) / pic.natW, x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 };
    renderPartList();
  } else {
    const pic = await asData(`${PATHS.art}/accessories/crown.svg`);
    state.acc.img = { ...pic, w: 60, h: (60 * pic.natH) / pic.natW, x: 0, y: -6, scale: 1 };
  }
  applyEdits();
}
addEventListener("resize", layout);
let last = performance.now();
function frame(ts) {
  const dt = state.paused ? 0 : Math.min(0.05, (ts - last) / 1000);
  last = ts;
  layout();
  pet.update(dt);
  ghost.update(dt);
  drawOutline();
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
