// 自訂配件／衣服編輯器：上傳圖片 → 在動起來的角色身上拖曳對齊 → 匯出 json
// 配件掛在角色的錨點上（rig.anchors），衣服換掉同名部件；兩者的座標都是 Q 版單位，桌寵版用同一套規則畫
import { PATHS, TUNING } from "../config.js";
import { formRig, loadAll } from "../core/loader.js";
import { apply, mul } from "../core/math.js";
import { Pet } from "../game/pet.js";
import { accessoryJson, boxOf, characterJson, download, folderName, outfitJson } from "./exporter.js";

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
  // 自訂角色：parts 同衣服；faces.place 是所有表情共用的位置，faces.images 每個表情一張圖
  character: { name: "我的角色", desc: "自己做的角色", greet: "嗨！", parts: {}, selected: null, faces: { place: null, images: {} } },
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

// 換部件的兩種模式（衣服、自訂角色）各有一份資料
const EXPRESSIONS = { idle: "待機", blink: "眨眼", happy: "開心", sleep: "睡覺", surprised: "驚訝", focus: "專注", shy: "害羞", angry: "生氣" };
const parts = () => (state.mode === "character" ? state.character : state.outfit);
const faceOf = sel => (sel?.startsWith("face:") ? sel.slice(5) : null);
const faceBox = () => rig().slots.find(s => s.variants).box;

// ---------- 目前在編輯的圖 ----------
function currentImg() {
  if (state.mode === "accessory") return state.acc.img;
  const ps = parts();
  return faceOf(ps.selected) ? ps.faces?.place ?? null : ps.parts[ps.selected] ?? null;
}

// 圖片所在的座標系：骨骼世界矩陣（配件再乘上錨點的位移與縮放）
function frameOf() {
  const r = rig();
  if (state.mode === "accessory") {
    const a = r.anchors?.[slots[state.acc.slot].anchor];
    if (!a) return null;
    return mul(pet.world[a.bone], [a.scale, 0, 0, a.scale, a.x, a.y]);
  }
  const sel = parts().selected;
  const slot = faceOf(sel) ? r.slots.find(s => s.variants) : partSlot(sel);
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
  if (state.mode !== "accessory") for (const [art, img] of Object.entries(parts().parts)) map[art] = { url: img.url, box: boxOf(img) };
  pet.setOutfit(Object.keys(map).length ? map : null);
  // 自訂角色的表情：共用一個位置；選到某個表情時讓角色擺出那個表情
  const faces = state.character.faces, charMode = state.mode === "character";
  const urls = Object.fromEntries(Object.entries(faces.images).map(([k, v]) => [k, v.url]));
  pet.setFaceImages(charMode && faces.place ? { box: boxOf(faces.place), images: urls } : null);
  pet.faceOverride = charMode ? faceOf(state.character.selected) : null;
  ghostLayer.style.display = state.mode !== "accessory" && $("[data-ghost]").checked ? "" : "none";
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
  const ps = parts(), sel = ps.selected;
  if (!file || !sel) return;
  const pic = await readImage(file);
  const expr = faceOf(sel);
  // 預設蓋在原本部件（或表情）的範圍上：寬度相同、高度依圖片比例
  const [x0, y0, x1, y1] = expr ? faceBox() : partSlot(sel).box;
  const w = x1 - x0;
  const place = { w, h: (w * pic.natH) / pic.natW, x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 };
  if (expr) {
    ps.faces.images[expr] = { url: pic.url, fileName: pic.fileName };
    ps.faces.place ??= place;            // 第一張表情決定位置，之後的表情共用
  } else ps.parts[sel] = { ...pic, ...place };
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
  else {
    const sel = parts().selected;
    const [x0, y0, x1, y1] = faceOf(sel) ? faceBox() : partSlot(sel).box;
    Object.assign(img, { x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 });
  }
  applyEdits();
};
$("[data-remove]").onclick = () => {
  const ps = parts(), expr = faceOf(ps.selected);
  if (state.mode === "accessory") state.acc.img = null;
  else if (expr) {
    delete ps.faces.images[expr];
    if (!Object.keys(ps.faces.images).length) ps.faces.place = null;
  } else delete ps.parts[ps.selected];
  renderPartList();
  applyEdits();
};
$("[data-acc-name]").oninput = e => { state.acc.name = e.target.value; updateHint(); };
$("[data-outfit-name]").oninput = e => { state.outfit.name = e.target.value; updateHint(); };
$("[data-char-name]").oninput = e => { state.character.name = e.target.value; updateHint(); };
$("[data-char-desc]").oninput = e => { state.character.desc = e.target.value; };
$("[data-char-greet]").oninput = e => { state.character.greet = e.target.value; };
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
  fillParts();
  buildPets();
  renderPartList();
}

// 部件選單：部件；自訂角色模式再加上各個表情
function fillParts() {
  const arts = [...new Set(rig().slots.filter(s => s.file && !s.variants).map(s => s.file.split("/").pop().replace(".svg", "")))];
  const entries = arts.map(a => [a, PART_LABELS[a] ?? a]);
  if (state.mode === "character") entries.push(...Object.entries(EXPRESSIONS).map(([k, v]) => [`face:${k}`, `表情：${v}`]));
  const ps = parts();
  fillSelect($("[data-part]"), entries, ps.selected ?? "torso");
  ps.selected = $("[data-part]").value;
}

$("[data-char]").onchange = e => {
  state.charId = e.target.value;
  state.form = null;
  state.outfit.parts = {};       // 衣服是做給特定角色的，換角色就清空
  renderCharacter();
};
$("[data-form]").onchange = e => { state.form = e.target.value; state.outfit.parts = {}; renderCharacter(); };
$("[data-part]").onchange = e => { parts().selected = e.target.value; renderPartList(); applyEdits(); };

function renderPartList() {
  const ps = parts();
  const done = Object.keys(ps.parts).map(a => [a, PART_LABELS[a] ?? a]);
  if (state.mode === "character") done.push(...Object.keys(ps.faces.images).map(k => [`face:${k}`, `表情：${EXPRESSIONS[k]}`]));
  $("[data-part-list]").replaceChildren(...done.map(([key, label]) => {
    const b = document.createElement("button");
    b.textContent = `✓ ${label}`;
    b.classList.toggle("on", key === ps.selected);
    b.onclick = () => { ps.selected = key; $("[data-part]").value = key; renderPartList(); applyEdits(); };
    return b;
  }));
}

for (const b of document.querySelectorAll("[data-mode]")) {
  b.onclick = () => {
    state.mode = b.dataset.mode;
    for (const x of document.querySelectorAll("[data-mode]")) x.classList.toggle("on", x === b);
    const show = { accessory: ["accessory"], outfit: ["outfit"], character: ["character", "outfit"] }[state.mode];
    for (const p of document.querySelectorAll("[data-panel]")) p.hidden = !show.includes(p.dataset.panel);
    $("[data-parts-title]").textContent = state.mode === "character" ? "部件與表情" : "衣服";
    $("[data-outfit-name-field]").hidden = state.mode === "character";
    $("[data-form]").hidden = state.mode === "character" || Object.keys(rigs[state.charId].forms ?? {}).length < 2;
    fillParts();
    renderPartList();
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
  } else if (state.mode === "character") {
    const c = state.character;
    const files = [...Object.values(c.parts).map(p => p.fileName), ...Object.values(c.faces.images).map(f => f.fileName)];
    hint.textContent = files.length
      ? `把 character.json 和這些圖片放進：\n文件\\DeskPet\\characters\\${folderName(c.name)}\\\n${files.join("、")}\n重新啟動桌寵後會出現在角色清單。沒換的部件和表情會沿用 ${rigs[state.charId].name} 的。`
      : `以 ${rigs[state.charId].name} 的身形與動作為範本，把部件和表情一個個換成自己的圖。`;
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
  } else if (state.mode === "character") {
    const c = state.character;
    if (!Object.keys(c.parts).length && !Object.keys(c.faces.images).length) return alert("還沒有任何部件或表情");
    download("character.json", characterJson(c, state.charId));
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
  if (q.get("demo") === "character") {
    document.querySelector('[data-mode="character"]').click();
    const c = state.character;
    const pic = await asData(`${PATHS.art}/props/book.svg`);
    const [x0, y0, x1, y1] = partSlot("torso").box;
    c.parts.torso = { ...pic, w: x1 - x0, h: ((x1 - x0) * pic.natH) / pic.natW, x: (x0 + x1) / 2, y: (y0 + y1) / 2, scale: 1 };
    const face = await asData(`${PATHS.art}/accessories/sunglasses.svg`);
    const [f0, g0, f1, g1] = faceBox();
    c.faces.images.idle = { url: face.url, fileName: face.fileName };
    c.faces.place = { w: f1 - f0, h: ((f1 - f0) * face.natH) / face.natW, x: (f0 + f1) / 2, y: (g0 + g1) / 2, scale: 0.6 };
    c.selected = "face:idle";
    fillParts();
    renderPartList();
  } else if (q.get("demo") === "outfit") {
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
