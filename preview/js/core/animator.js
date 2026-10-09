// 動作播放器：取樣關鍵影格，切換動作時做交叉淡化

const smooth = t => t * t * (3 - 2 * t);

// keys: [[時間, 數值], ...]，兩個關鍵影格之間平滑插值
export function sampleTrack(keys, t) {
  if (keys.length === 1 || t <= keys[0][0]) return keys[0][1];
  for (let i = 0; i < keys.length - 1; i++) {
    const [t0, v0] = keys[i], [t1, v1] = keys[i + 1];
    if (t <= t1) return v0 + (v1 - v0) * smooth((t - t0) / (t1 - t0 || 1));
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
