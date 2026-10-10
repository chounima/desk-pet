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
  return { ids, rigs };
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

export async function loadProps() {
  const props = await getJSON(`${PATHS.art}/props/props.json`);
  for (const p of Object.values(props)) p.url = `${PATHS.art}/props/${p.file}`;
  return props;
}

export async function loadAll() {
  const chars = await loadCharacters();
  const bodies = new Set();
  for (const rig of Object.values(chars.rigs)) {
    bodies.add(bodyOf(rig));
    for (const f of Object.values(rig.forms ?? {})) bodies.add(bodyOf(f.data));
  }
  const [anims, props] = await Promise.all([loadAnimations(bodies), loadProps()]);
  return [chars, anims, props];
}
