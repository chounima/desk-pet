// 對話泡泡：顯示在角色頭上，幾秒後消失。台詞來自 data/lines/<角色>.json（沒有的 key 用 default.json）
import { PATHS } from "../config.js";

export async function loadLines(ids) {
  const get = async name => {
    try {
      const res = await fetch(`${PATHS.data}/lines/${name}.json`);
      return res.ok ? res.json() : {};
    } catch { return {}; }
  };
  const lines = { default: await get("default") };
  await Promise.all(ids.map(async id => { lines[id] = await get(id); }));
  return lines;
}

export class Bubble {
  constructor(layer, lines) {
    this.lines = lines;
    this.el = document.createElement("div");
    this.el.className = "bubble style-classic";
    this.el.hidden = true;
    layer.append(this.el);
    this.left = 0;
  }

  // 款式：classic | cloud | pixel | hand（商店的對話泡泡）
  setStyle(style) { this.el.className = `bubble style-${style}`; }

  pick(char, key) {
    const list = this.lines[char]?.[key] ?? this.lines.default?.[key];
    return list?.length ? list[Math.floor(Math.random() * list.length)] : null;
  }

  say(char, key, seconds = 3) {
    const text = this.pick(char, key);
    if (text) this.show(text, seconds);
  }

  show(text, seconds = 3) {
    this.el.textContent = text;
    this.el.hidden = false;
    this.left = seconds;
  }

  // anchor：頭頂的螢幕座標
  update(dt, anchor) {
    if (this.el.hidden) return;
    this.left -= dt;
    if (this.left <= 0) { this.el.hidden = true; return; }
    this.el.style.left = `${anchor.x}px`;
    this.el.style.top = `${anchor.y - 12}px`;
  }
}
