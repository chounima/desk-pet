// 讀取角色骨架、動作、道具資料
import { PATHS } from "../config.js";

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
    rigs[id] = rig;
    await Promise.all(Object.values(rig.forms ?? {}).map(async f => {
      if (!f.rig) { f.data = rig; return; }
      f.data = await getJSON(`${base}${f.rig}rig.json`);
      f.data.base = `${base}${f.rig}`;
    }));
  }));
  await loadPackages(ids, rigs);
  return { ids, rigs };
}

// 美術部件包 art/packages/<id>/character.json（tools/import_layers.py 產生，清單在 index.json）：
// 和佔位角色同名 = 換掉它的美術（骨架、動作沿用）；新名字 = 新角色（要指定 base）。規則同桌寵的 CharacterPackage
async function loadPackages(ids, rigs) {
  const res = await fetch(`${PATHS.art}/packages/index.json`);
  if (!res.ok) return;
  for (const name of await res.json()) {
    try {
      const folder = `${PATHS.art}/packages/${name}/`;
      const rig = applyPackage(name, await getJSON(`${folder}character.json`), folder, rigs);
      if (!rigs[name]) ids.push(name);
      rigs[name] = rig;
    } catch (e) {
      console.warn(`部件包 ${name} 讀取失敗：${e.message}`);
    }
  }
}

export function applyPackage(id, pkg, folder, rigs) {
  const baseId = pkg.base || id;
  const baseRig = rigs[baseId];
  if (!baseRig) throw new Error(`找不到範本角色 ${baseId}`);
  const template = formRig(baseRig, pkg.form);
  const { forms, ...rest } = template;
  const rig = JSON.parse(JSON.stringify(rest));          // 複製範本（不含型態，型態的 data 會指回自己）
  rig.base = template.base;
  rig.id = id;
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
  if (pkg.faces?.images && Object.keys(pkg.faces.images).length)
    rig.defaultFaces = { box: pkg.faces.box, images: Object.fromEntries(Object.entries(pkg.faces.images).map(([k, v]) => [k, url(v)])) };
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
  return { names, clips, sets };
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
