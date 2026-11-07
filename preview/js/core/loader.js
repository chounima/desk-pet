// 讀取角色骨架、動作、道具資料
import { FACE, PATHS } from "../config.js";

async function getJSON(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

// 角色可以有多個型態（例如直立／四足），rig.forms[名稱].data 是該型態的骨架資料
export async function loadCharacters() {
  const ids = await getJSON(`${PATHS.art}/characters/index.json`);
  const rigs = {};
  await Promise.all(ids.map(async id => {
    const base = `${PATHS.art}/characters/${id}/`;
    const rig = await getJSON(`${base}rig.json`);
    rig.base = base;
    rig.animSource = id;                 // 用哪個角色的專屬動作
    rigs[id] = rig;
    await Promise.all(Object.entries(rig.forms ?? {}).map(async ([formName, f]) => {
      if (!f.rig) { f.data = rig; rig.formName = formName; return; }
      f.data = await getJSON(`${base}${f.rig}rig.json`);
      f.data.base = `${base}${f.rig}`;
      f.data.animSource = id;
      f.data.formName = formName;
    }));
    // 其他視角（正面、背面…）：rig.views[名稱].data 是那個視角的骨架資料
    await Promise.all(Object.values(rig.views ?? {}).map(async v => {
      if (!v.rig) { v.data = rig; return; }
      v.data = await getJSON(`${base}${v.rig}rig.json`);
      v.data.base = `${base}${v.rig}`;
      v.data.animSource = id;
      v.data.id = id;
    }));
  }));
  await applyPhysicsOverrides(rigs);
  await loadPackages(ids, rigs);
  // data/face.json 的表情調整（部件包之後：部件包畫了整張臉時就沒有分層臉）
  for (const [id, rig] of Object.entries(rigs))
    for (const r of new Set([rig, ...Object.values(rig.forms ?? {}).map(f => f.data), ...Object.values(rig.views ?? {}).map(v => v.data)]))
      applyFaceOverrides(r, id);
  return { ids, rigs };
}

// 把 data/face.json 的表情調整套到分層臉上（"*" 先、角色 id 後）；新的表情名從 idle 複製一份再改。和桌寵 FaceConfig.Apply 一樣
export function applyFaceOverrides(rig, id) {
  const face = rig?.face, all = FACE.expressions;
  if (!face || !all) return;
  for (const scope of ["*", id]) {
    for (const [expr, spec] of Object.entries(all[scope] ?? {})) {
      if (expr.startsWith("_") || typeof spec !== "object") continue;
      const layers = (face.expressions[expr] ??= { ...(face.expressions.idle ?? {}) });
      for (const [layer, value] of Object.entries(spec)) {
        if (layer === "alpha") {
          face.alpha ??= {};
          Object.assign((face.alpha[expr] ??= { ...(face.alpha.idle ?? {}) }), value);
        } else if (value) layers[layer] = value;
        else delete layers[layer];
      }
      if (face.alpha && !face.alpha[expr] && face.alpha.idle) face.alpha[expr] = { ...face.alpha.idle };
    }
  }
}

// data/physics.json：{ 角色 或 角色@型態: { 骨骼: 參數 } }，蓋過 gen_art 產生的物理參數（動作編輯器調的；規則同桌寵）
async function applyPhysicsOverrides(rigs) {
  const res = await fetch(`${PATHS.data}/physics.json`);
  if (!res.ok) return;
  for (const [key, bones] of Object.entries(await res.json())) {
    if (key.startsWith("_")) continue;
    const [id, form] = key.split("@");
    if (!rigs[id]) continue;
    const target = formRig(rigs[id], form);
    target.physics = { ...(target.physics ?? {}), ...bones };
  }
}

// 美術部件包 art/packages/<id>/character.json（tools/import_layers.py 產生，清單在 index.json）：
// 和佔位角色同名 = 換掉它的美術（骨架、動作沿用）；新名字 = 新角色（要指定 base）。規則同桌寵的 CharacterPackage
async function loadPackages(ids, rigs) {
  const res = await fetch(`${PATHS.art}/packages/index.json`);
  if (!res.ok) return;
  for (const name of await res.json()) {
    try {
      const folder = `${PATHS.art}/packages/${name}/`;
      const pkg = await getJSON(`${folder}character.json`);
      const rig = applyPackage(name, pkg, folder, rigs);
      // 其他視角：部件包有 views/<視角>/character.json 就換那個視角的圖，沒有就沿用範本的（測試圖）
      const template = formRig(rigs[pkg.base || name], pkg.form);
      if (template.views) {
        rig.views = {};
        for (const [v, view] of Object.entries(template.views)) {
          let data = view.rig ? view.data : rig;
          if (view.rig) {
            const vfolder = `${folder}views/${v}/`;
            const res2 = await fetch(`${vfolder}character.json`);
            if (res2.ok) data = applyPackage(name, await res2.json(), vfolder, rigs, view.data);
          }
          rig.views[v] = { ...view, data };
        }
      }
      if (!rigs[name]) ids.push(name);
      rigs[name] = rig;
    } catch (e) {
      console.warn(`部件包 ${name} 讀取失敗：${e.message}`);
    }
  }
}

// viewTemplate：套到某個視角的骨架上（部件包的 views/<視角>/）
export function applyPackage(id, pkg, folder, rigs, viewTemplate = null) {
  const baseId = pkg.base || id;
  const baseRig = rigs[baseId];
  if (!baseRig) throw new Error(`找不到範本角色 ${baseId}`);
  const template = viewTemplate ?? formRig(baseRig, pkg.form);
  const { forms, views, ...rest } = template;
  const rig = JSON.parse(JSON.stringify(rest));          // 複製範本（不含型態、視角，它們的 data 會指回自己）
  rig.base = template.base;
  rig.id = id;
  rig.animSource = template.animSource ?? template.id;     // 動作沿用範本角色的（含專屬動作）
  rig.formName = template.formName;
  rig.name = pkg.name || id;
  rig.description = pkg.description ?? "";
  const url = file => new URL(file, new URL(folder, location.href)).href;
  rig.defaultOutfit = {};
  for (const [name, part] of Object.entries(pkg.parts ?? {})) {
    rig.defaultOutfit[name] = { url: url(part.image), box: part.box };
    // 替換圖：用這張部件圖的插槽（近側、遠側）都換
    for (const [alt, a] of Object.entries(part.alt ?? {}))
      for (const slot of rig.slots.filter(s => s.file?.split("/").pop().replace(/\.[a-z]+$/, "") === name))
        (slot.alt ??= {})[alt] = { file: url(a.image), box: a.box };
  }
  if (pkg.faces?.images && Object.keys(pkg.faces.images).length) {
    rig.defaultFaces = { box: pkg.faces.box, images: Object.fromEntries(Object.entries(pkg.faces.images).map(([k, v]) => [k, url(v)])) };
    delete rig.face;      // 部件包畫了整張臉：不用範本的分層臉
  }
  Object.assign(rig.anchors ??= {}, pkg.anchors ?? {});
  Object.assign(rig.propOverrides ??= {}, pkg.propOverrides ?? {});
  if (pkg.headTop?.length === 2) rig.headTop = pkg.headTop;
  // 換掉自己的美術：其他型態照舊（預設型態指向新的資料）
  if (baseId === id && template === baseRig && forms)
    rig.forms = Object.fromEntries(Object.entries(forms).map(([k, f]) => [k, { ...f, data: f.rig ? f.data : rig }]));
  return rig;
}

// 取得角色某個型態的骨架；沒有這個型態就用預設的
export const formRig = (rig, form) => rig.forms?.[form]?.data ?? rig;

// 依面向角度（0 正面、90 側面朝右、180 背面、270 左側）選最接近的視角（和桌寵 ContentLoader.ViewFor 一樣）：
// { rig, facing（1／-1，左側 = 側面鏡像）, bias（-1～1，角度和視角差多少，臉往那邊偏）, viewYaw }
export function viewFor(rig, yaw) {
  yaw = ((yaw % 360) + 360) % 360;
  const diff = (a, b) => ((b - a + 540) % 360) - 180;
  const options = [];
  for (const v of Object.values(rig.views ?? {})) {
    options.push({ rig: v.data, yaw: v.yaw, facing: 1 });
    if (v.yaw > 0 && v.yaw < 180) options.push({ rig: v.data, yaw: 360 - v.yaw, facing: -1 });      // 鏡像（側面 → 左側）
  }
  if (!options.length) return { rig, facing: yaw > 180 ? -1 : 1, bias: 0, viewYaw: yaw > 180 ? 270 : 90 };
  const best = options.reduce((a, b) => (Math.abs(diff(b.yaw, yaw)) < Math.abs(diff(a.yaw, yaw)) ? b : a));
  return { rig: best.rig, facing: best.facing, bias: Math.max(-1, Math.min(1, diff(best.yaw, yaw) / 45)) * best.facing, viewYaw: best.yaw };
}

const bodyOf = rig => rig.body ?? "humanoid";

// 共用動作 + 各體型的專屬動作。sets[體型][動作名] = clip
// 體型專屬版放在 data/animations/<體型>/，沒有的動作依 alias 借用相近的姿勢（名稱與 next 維持原本的）
export async function loadAnimations(bodies = []) {
  const dir = `${PATHS.data}/animations`;
  const names = await getJSON(`${dir}/index.json`);
  const clips = {};
  await Promise.all(names.map(async name => {
    clips[name] = { name, ...(await getJSON(`${dir}/${name}.json`)) };
  }));
  const sets = { humanoid: clips };
  await Promise.all([...bodies].filter(b => b !== "humanoid").map(async body => {
    const index = await getJSON(`${dir}/${body}/index.json`);
    const set = { ...clips };
    await Promise.all(index.clips.map(async name => {
      set[name] = { name, ...(await getJSON(`${dir}/${body}/${name}.json`)) };
    }));
    for (const [name, target] of Object.entries(index.alias ?? {})) {
      const own = clips[name];
      set[name] = { ...set[target], name, label: own?.label ?? set[target].label, hidden: own?.hidden, next: own?.next };
    }
    sets[body] = set;
  }));
  await attachCharacterClips(sets, dir);
  return { names, clips, sets };
}

// 角色專屬動作（data/animations/characters/index.json，規則同桌寵的 ClipLibrary）：
// 共用 → 體型 → 角色 → 角色＋型態，後面蓋前面；可以只寫差異 { extends, tracks }。
// sets.for(rig) 回傳這個角色要用的動作組（依角色｜型態快取）；for 不會出現在 Object.keys(sets)
async function attachCharacterClips(sets, dir) {
  const res = await fetch(`${dir}/characters/index.json`);
  const index = res.ok ? await res.json() : {};
  const files = {};
  await Promise.all(Object.entries(index).filter(([id]) => !id.startsWith("_")).flatMap(([id, entry]) => [
    ...(entry.clips ?? []).map(name => [`${id}/${name}`, `${dir}/characters/${id}/${name}.json`]),
    ...Object.entries(entry.forms ?? {}).flatMap(([form, names]) => names.map(name => [`${id}/forms/${form}/${name}`, `${dir}/characters/${id}/forms/${form}/${name}.json`])),
  ].map(async ([key, url]) => { files[key] = await getJSON(url); })));
  const cache = {};
  const apply = (set, names, prefix) => {
    for (const name of names) {
      const file = files[`${prefix}${name}`];
      set[name] = { name, ...(file.extends ? extendClip(set[file.extends], file) : file) };
    }
  };
  Object.defineProperty(sets, "characterIndex", { enumerable: false, value: index });   // 動作編輯器判斷動作來自哪一層
  Object.defineProperty(sets, "for", {
    enumerable: false,
    value(rig) {
      const body = sets[rig.body ?? "humanoid"] ?? sets.humanoid;
      const id = rig.animSource ?? rig.id, entry = index[id];
      if (!entry) return body;
      const key = `${id}|${rig.formName ?? ""}|${rig.body ?? "humanoid"}`;
      if (cache[key]) return cache[key];
      const set = { ...body };
      apply(set, entry.clips ?? [], `${id}/`);
      if (rig.formName && entry.forms?.[rig.formName]) apply(set, entry.forms[rig.formName], `${id}/forms/${rig.formName}/`);
      return (cache[key] = set);
    },
  });
}

// 差異合併：以 base 為底，覆蓋列出的欄位；tracks 只覆蓋列出的軌道（null = 拿掉那條軌道）
export function extendClip(base, diff) {
  if (!base) throw new Error(`extends 的 ${diff.extends} 不存在`);
  const { extends: _, tracks, ...rest } = diff;
  const merged = { ...base, ...rest, tracks: { ...base.tracks } };
  for (const [ch, keys] of Object.entries(tracks ?? {})) {
    if (keys === null) delete merged.tracks[ch];
    else merged.tracks[ch] = keys;
  }
  return merged;
}

// 收集系統的參數；開發者模式的暫存覆寫（JSON 字串）優先
export async function loadProgression(override) {
  const defaults = await getJSON(`${PATHS.data}/progression.json`);
  let cfg = defaults;
  if (override) {
    try { cfg = JSON.parse(override); } catch { /* 覆寫壞掉就用預設 */ }
  }
  return { defaults, cfg };
}

export async function loadProps() {
  const props = await getJSON(`${PATHS.art}/props/props.json`);
  for (const p of Object.values(props)) p.url = `${PATHS.art}/props/${p.file}`;
  return props;
}

export async function loadAccessories() {
  const meta = await getJSON(`${PATHS.art}/accessories/accessories.json`);
  for (const a of Object.values(meta)) a.url = `${PATHS.art}/accessories/${a.file}`;
  return meta;
}

export async function loadAll() {
  const chars = await loadCharacters();
  const bodies = new Set();
  for (const rig of Object.values(chars.rigs)) {
    bodies.add(bodyOf(rig));
    for (const f of Object.values(rig.forms ?? {})) bodies.add(bodyOf(f.data));
  }
  const [anims, props, accessories] = await Promise.all([loadAnimations(bodies), loadProps(), loadAccessories()]);
  return [chars, anims, props, accessories];
}
