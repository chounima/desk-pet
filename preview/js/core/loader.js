// 讀取角色骨架、動作、道具資料
import { PATHS } from "../config.js";

async function getJSON(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

export async function loadCharacters() {
  const ids = await getJSON(`${PATHS.art}/characters/index.json`);
  const rigs = {};
  await Promise.all(ids.map(async id => {
    const rig = await getJSON(`${PATHS.art}/characters/${id}/rig.json`);
    rig.base = `${PATHS.art}/characters/${id}/`;
    rigs[id] = rig;
  }));
  return { ids, rigs };
}

export async function loadAnimations() {
  const names = await getJSON(`${PATHS.data}/animations/index.json`);
  const clips = {};
  await Promise.all(names.map(async name => {
    clips[name] = { name, ...(await getJSON(`${PATHS.data}/animations/${name}.json`)) };
  }));
  return { names, clips };
}

export async function loadProps() {
  const props = await getJSON(`${PATHS.art}/props/props.json`);
  for (const p of Object.values(props)) p.url = `${PATHS.art}/props/${p.file}`;
  return props;
}

export const loadAll = () => Promise.all([loadCharacters(), loadAnimations(), loadProps()]);
