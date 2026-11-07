// 動作播放器：取樣關鍵影格，切換動作時做交叉淡化

const smooth = t => t * t * (3 - 2 * t);

// back：先超過一點再回來（easeOutBack）；hold：停在前一個值，到下一個關鍵影格才跳過去（和桌寵 Animator.Ease 一樣）
// 用 Math.fround 讓中間值和桌寵的 float（32 位元）一致
const C1 = Math.fround(1.70158), C3 = Math.fround(C1 + 1);
export const ease = (curve, u) => {
  switch (curve) {
    case "linear": return u;
    case "easeIn": return u * u;
    case "easeOut": return 1 - (1 - u) * (1 - u);
    case "back": return 1 + C3 * (u - 1) ** 3 + C1 * (u - 1) ** 2;
    case "hold": return u >= 1 ? 1 : 0;
    default: return smooth(u);
  }
};

// keys: [[時間, 數值, 曲線], ...]，兩個關鍵影格之間依前一個的曲線插值（預設平滑）
export function sampleTrack(keys, t) {
  if (keys.length === 1 || t <= keys[0][0]) return keys[0][1];
  for (let i = 0; i < keys.length - 1; i++) {
    const [t0, v0, curve] = keys[i], [t1, v1] = keys[i + 1];
    if (t <= t1) return v0 + (v1 - v0) * ease(curve, (t - t0) / (t1 - t0 || 1));
  }
  return keys[keys.length - 1][1];
}

// 離散軌道（表情）：字串，或 [[時間, 名稱], ...]
function sampleDiscrete(track, t) {
  if (typeof track === "string") return track;
  let value = track[0][1];
  for (const [k, v] of track) if (t >= k) value = v;
  return value;
}

export class Animator {
  constructor(clips, crossfade) {
    this.clips = clips;
    this.crossfade = crossfade;
    this.clip = null;
    this.name = null;
    this.time = 0;
    this.pose = {};
    this.from = {};
    this.blend = 1;
    this.mods = {};
    this.finished = false;
  }

  // mods：播放時縮放某些軌道，例如 { "root.y": 0.5 } 讓跳躍只跳一半高
  play(name, restart = false, mods = {}) {
    const clip = this.clips[name];
    if (!clip || (this.clip === clip && !restart)) return;
    this.from = { ...this.pose };
    this.blend = 0;
    this.clip = clip;
    this.name = name;
    this.mods = mods;
    this.time = 0;
    this.finished = false;
  }

  // 直接跳到某個時間點（不做淡化），截圖用
  seek(t) {
    this.time = t;
    this.blend = 1;
  }

  get localTime() {
    const c = this.clip;
    return c.loop ? this.time % c.duration : Math.min(this.time, c.duration);
  }

  update(dt) {
    const c = this.clip;
    this.time += dt;
    if (!c.loop && this.time >= c.duration) this.finished = true;

    const t = this.localTime;
    const target = {};
    for (const ch in c.tracks) target[ch] = sampleTrack(c.tracks[ch], t) * (this.mods[ch] ?? 1);

    this.blend = Math.min(1, this.blend + dt / this.crossfade);
    const w = smooth(this.blend);
    const pose = {};
    for (const ch of new Set([...Object.keys(this.from), ...Object.keys(target)])) {
      const a = this.from[ch] ?? 0, b = target[ch] ?? 0;
      pose[ch] = a + (b - a) * w;
    }
    this.pose = pose;
    return pose;
  }

  get face() { return sampleDiscrete(this.clip.face ?? "idle", this.localTime); }
  get props() { return this.clip.props ?? []; }
  get alt() { return this.clip.alt ?? {}; }       // { 插槽名: 替換圖名 }
  get fx() { return this.clip.fx ?? null; }
  get move() { return this.clip.move ?? 0; }
}
