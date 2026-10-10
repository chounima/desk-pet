// 各角色的排球能力與台詞：資料在 data/volley.json（與 Godot 版共用）
import { PATHS } from "../config.js";

let data = { default: { lines: {} } };

/** 開始對打前先載入一次 */
export async function loadSkills() {
  const res = await fetch(`${PATHS.data}/volley.json`);
  if (res.ok) data = await res.json();
}

export function skillFor(id) {
  const base = data.default, s = data[id] ?? {};
  return { ...base, ...s, lines: { ...base.lines, ...(s.lines ?? {}) } };
}
