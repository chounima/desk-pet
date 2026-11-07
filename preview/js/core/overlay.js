// 疊加層：在動作的姿勢上加呼吸、重心晃動等週期性小動作（設定 data/motion.json；和桌寵 godot/src/Core/Overlay.cs 算法一樣）
// value = amp × sin(2π × time / period + phase)，只在 actions 列出的動作套用，換動作時用 fade 秒淡入淡出
import { rest } from "./animator.js";

export class Overlay {
  constructor(config) {
    this.config = config ?? { fade: 0.5, layers: {} };
    this.weights = {};
    this.time = 0;
  }

  apply(pose, action, dt) {
    this.time += dt;
    const { fade, layers } = this.config;
    for (const [name, layer] of Object.entries(layers ?? {})) {
      const target = layer.actions.includes(action) ? 1 : 0;
      let w = this.weights[name] ?? target;
      const rate = fade > 0 ? dt / fade : 1;
      w = w < target ? Math.min(target, w + rate) : Math.max(target, w - rate);
      this.weights[name] = w;
      if (w <= 0) continue;
      for (const [ch, wave] of Object.entries(layer.tracks))
        pose[ch] = (pose[ch] ?? rest(ch)) + w * wave.amp * Math.sin(2 * Math.PI * this.time / layer.period + (wave.phase ?? 0));
    }
  }
}

// 測試用：某個時間點（權重 1）每條軌道加多少
export function overlayDeltas(config, action, time) {
  const result = {};
  for (const layer of Object.values(config.layers ?? {})) {
    if (!layer.actions.includes(action)) continue;
    for (const [ch, wave] of Object.entries(layer.tracks))
      result[ch] = (result[ch] ?? 0) + wave.amp * Math.sin(2 * Math.PI * time / layer.period + (wave.phase ?? 0));
  }
  return result;
}
