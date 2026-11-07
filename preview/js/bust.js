// 半身預覽（preview/bust.html）：麥克風 → core/voice.js 分析 → 嘴型（分層臉的母音嘴、或整張臉的驚訝／開心），眼睛跟游標
// 和桌寵的半身模式（Features/Vtuber）用同一套聲音分析與嘴型規則；聲音觸發的表情規則只在桌寵版。
// ?char=sera 指定角色；?selftest=1 用合成的「い」測嘴型（tools/web_test.ps1 讀標題）
import { TUNING } from "./config.js";
import { formRig, loadAll, viewFor } from "./core/loader.js";
import { Skeleton } from "./core/skeleton.js";
import { apply } from "./core/math.js";
import { VoiceAnalyzer, VOICE, synth } from "./core/voice.js";
import { Pet } from "./game/pet.js";

const $ = id => document.getElementById(id);
const NS = "http://www.w3.org/2000/svg";
const MARGIN = 0.06, OPEN_DB = 6;
const FACES = { 1: "happy", 2: "surprised", 3: "angry", 4: "shy", 5: "focus" };

const [{ ids, rigs }, { sets }, props] = await loadAll();
const q = new URLSearchParams(location.search);
const select = $("char");
for (const id of ids) select.append(Object.assign(document.createElement("option"), { value: id, textContent: rigs[id].name }));
select.value = q.get("char") ?? ids[0];

const svg = $("bust");
let pet = null, view = null;
let yaw = Number(q.get("yaw") ?? 90);        // 面向角度：0 正面、90 側面、180 背面、270 左側

// 依角度選視角（和桌寵一樣：最接近的視角，左側 = 側面鏡像），換了視角才重建
function applyYaw(target) {
  const v = viewFor(formRig(rigs[select.value], null), target);
  if (!pet || v.rig !== view?.rig || v.facing !== view?.facing) { view = v; build(); }
  pet.lookBias = [v.rig === view.rig ? v.bias : 0, 0];
}

// 取景：頭頂到腰（hips），最多到脖子往下 1.5 個頭高（和桌寵一樣）
function build() {
  svg.replaceChildren();
  const layer = document.createElementNS(NS, "g");
  svg.append(layer);
  view ??= viewFor(formRig(rigs[select.value], null), yaw);
  const rig = view.rig;
  pet = new Pet({ layer, rig, clipSets: sets, props, scale: 1, crossfade: TUNING.crossfade });
  pet.x = 0;
  pet.y = 0;
  pet.facing = view.facing;
  const rest = new Skeleton(rig.bones, rig.rootScale ?? 1, rig.trackScale ?? {}).compute({});
  const [hx, hy] = rig.headTop ?? [6, -110];
  const [tx, top0] = apply(rest.head, hx, hy);
  const neck = rest.head[5];
  const top = top0 - (neck - top0) * 0.2;            // 頭髮、耳朵比頭頂高一點
  const bottom = Math.min(rest.hips ? rest.hips[5] : Infinity, neck + 1.5 * (neck - top0));
  const span = bottom - top, h = span * (1 + MARGIN), w = h * 0.85;
  svg.setAttribute("viewBox", `${tx * view.facing - w / 2} ${top - span * MARGIN} ${w} ${h}`);
}
select.onchange = () => { view = null; build(); };
const setYaw = v => { yaw = ((Number(v) % 360) + 360) % 360; $("yaw").value = yaw; $("yawText").textContent = `${Math.round(yaw)}°`; };
$("yaw").oninput = e => setYaw(e.target.value);
for (const b of document.querySelectorAll("[data-yaw]")) b.onclick = () => setYaw(b.dataset.yaw);
setYaw(yaw);
build();

// ---------- 聲音 ----------
let analyzer = null, frame = { db: -100, speaking: false, pitch: 0, vowel: null };
const noise = [], speech = [];
let floor = -50;

function onSamples(samples, rate) {
  if (!analyzer || analyzer.rate * analyzer.decimate !== rate) analyzer = new VoiceAnalyzer(rate, floor);
  analyzer.floor = floor;
  frame = analyzer.feed(samples);
  // 自動校正（同桌寵 VoiceCalibration）：安靜時的 90% 分位 + 3 dB 是噪音底線，講話時的中位數是平常音量
  const list = frame.speaking ? speech : noise;
  list.push(frame.db);
  if (list.length > (frame.speaking ? 600 : 180)) list.shift();
  if (noise.length >= 60) {
    const s = [...noise].sort((a, b) => a - b);
    floor = Math.min(-20, Math.max(-75, s[Math.floor(s.length * 0.9)] + 3));
  }
}

const speechDb = () => (speech.length >= 30 ? [...speech].sort((a, b) => a - b)[speech.length >> 1] : floor + 20);

// 嘴型（同桌寵 VoiceRules.MouthShape）
function mouthShape() {
  if (!frame.speaking || frame.db <= floor + VOICE.open) return "closed";
  return frame.vowel ?? (frame.db >= speechDb() - OPEN_DB ? "open" : "half");
}

$("mic").onclick = async () => {
  const stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } });
  const ctx = new AudioContext();
  const src = ctx.createMediaStreamSource(stream);
  const proc = ctx.createScriptProcessor(1024, 1, 1);
  proc.onaudioprocess = e => onSamples(e.inputBuffer.getChannelData(0), ctx.sampleRate);
  src.connect(proc);
  proc.connect(ctx.destination);         // 輸出是靜音（沒有寫入 outputBuffer）
  $("mic").disabled = true;
  $("mic").textContent = "🎙 麥克風開著";
};

// 測試音：合成的あいうえお（每個 0.4 秒）直接餵給分析，不經過喇叭
let testQueue = [];
$("test").onclick = () => {
  const rate = 48000;
  testQueue = [..."aiueo"].flatMap(v => {
    const [f1, f2] = { a: [800, 1200], i: [300, 2300], u: [350, 1300], e: [500, 1900], o: [500, 850] }[v];
    const s = synth({ kind: "vowel", f0: 200, formants: [f1, f2], amp: 0.2, seconds: 0.4 }, rate);
    return [...Array(Math.ceil(s.length / 800)).keys()].map(k => s.subarray(k * 800, (k + 1) * 800));
  });
};

// 表情：數字鍵
let face = null;
addEventListener("keydown", e => { if (e.key in FACES) face = FACES[e.key]; else if (e.key === "0") face = null; });

let cursor = null;
addEventListener("pointermove", e => {
  // 游標換算成角色座標（viewBox）
  const pt = svg.createSVGPoint();
  pt.x = e.clientX; pt.y = e.clientY;
  const m = svg.getScreenCTM();
  if (m) { const p = pt.matrixTransform(m.inverse()); cursor = { x: p.x, y: p.y }; }
});

let last = performance.now();
function tick(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  if (testQueue.length) onSamples(testQueue.shift(), 48000);
  else if (!$("mic").disabled) onSamples(new Float32Array(800), 48000);
  // 跟著游標轉身：游標在左右兩邊時角度往那邊轉（最多 60°）
  let target = yaw;
  if ($("turn").checked && cursor) {
    const vb = svg.viewBox.baseVal, side = Math.max(-1, Math.min(1, (cursor.x - (vb.x + vb.width / 2)) / (vb.width / 2)));
    target += side * 60 * (Math.cos((yaw * Math.PI) / 180) >= 0 ? 1 : -1);
  }
  applyYaw(target);
  pet.faceOverride = face;
  pet.mouthShape = mouthShape();
  pet.lookAt(cursor, dt, 60);          // 角色座標（Q 版單位），離臉 60 單位算看到底
  pet.update(dt);
  $("meter").firstElementChild.style.width = `${Math.max(0, Math.min(1, (frame.db - floor) / 40)) * 100}%`;
  $("stats").textContent = `音量 ${frame.db.toFixed(0)} dB（噪音 ${floor.toFixed(0)}）${frame.speaking ? "🎤" : ""}\n音調 ${frame.pitch ? frame.pitch.toFixed(0) + " Hz" : "-"}\n母音 ${frame.vowel ?? "-"}\n嘴型 ${pet.mouthShape}`;
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);

// ?selftest=1：合成的「い」→ 嘴巴要換成 vi（有分層臉的角色）
if (q.has("selftest")) {
  const s = synth({ kind: "vowel", f0: 200, formants: [300, 2300], amp: 0.2, seconds: 0.6 }, 48000);
  for (let k = 0; k < s.length; k += 800) onSamples(s.subarray(k, k + 800), 48000);
  pet.mouthShape = mouthShape();
  pet.update(1 / 60);
  const mouth = pet.view.faceLayers?.find((it, i) => pet.rig.face.layers[i].name === "mouth");
  const ok = frame.vowel === "i" && pet.mouthShape === "i" && mouth?.file?.endsWith("mouth_vi.svg");
  document.title = ok ? "PASS" : `FAIL vowel=${frame.vowel} shape=${pet.mouthShape} file=${mouth?.file}`;
}
