// 聲音分析（和桌寵 Core/Voice/VoiceAnalyzer.cs 同一套計算，tests/golden/voice.json 比對）
// 每 1/60 秒算一次：音量（dB）、是否在講話（噪音門檻＋保持時間）、音調（Hz，自相關）、母音（共振峰 → あいうえお）。
// 先降取樣到約 12 kHz，分析最近 512 個樣本（約 43 ms），每幀計算量很小。樣本只在記憶體裡算，不錄音。

export const VOICE = {
  targetRate: 12000,      // 降取樣後的取樣率（約）
  window: 512,            // 分析視窗（降取樣後的樣本數）
  fps: 60,
  open: 8,                // 比噪音底線大幾 dB 算在講話
  hold: 0.25,             // 講話停下後保持幾秒（字和字之間不要閉嘴）
  minPitch: 60, maxPitch: 500,
  voiced: 0.6,            // 自相關峰值超過這個才算有音調
};

// 母音的共振峰（F1, F2，Hz），日語五母音的大概位置
export const VOWELS = { a: [800, 1200], i: [300, 2300], u: [350, 1300], e: [500, 1900], o: [500, 850] };

// 原地 FFT（長度是 2 的次方）：re、im 是 Float64Array
function fft(re, im) {
  const n = re.length;
  for (let i = 1, j = 0; i < n; i++) {
    let bit = n >> 1;
    for (; j & bit; bit >>= 1) j ^= bit;
    j ^= bit;
    if (i < j) { [re[i], re[j]] = [re[j], re[i]]; [im[i], im[j]] = [im[j], im[i]]; }
  }
  for (let len = 2; len <= n; len <<= 1) {
    const ang = (-2 * Math.PI) / len;
    for (let i = 0; i < n; i += len)
      for (let k = 0; k < len / 2; k++) {
        const wr = Math.cos(ang * k), wi = Math.sin(ang * k);
        const a = i + k, b = a + len / 2;
        const xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
        re[b] = re[a] - xr; im[b] = im[a] - xi;
        re[a] += xr; im[a] += xi;
      }
  }
}

export class VoiceAnalyzer {
  constructor(sampleRate, floorDb = -50) {
    this.decimate = Math.max(1, Math.round(sampleRate / VOICE.targetRate));
    this.rate = sampleRate / this.decimate;
    this.hop = sampleRate / VOICE.fps;
    this.floor = floorDb;               // 噪音底線（校正後設定）
    this.ring = new Float64Array(VOICE.window);     // 環狀緩衝區（最新的樣本寫在 head）
    this.head = 0;
    this.buf = new Float64Array(VOICE.window);      // 分析時排成一列（最舊 → 最新）
    this.acc = 0; this.accN = 0;        // 降取樣：幾個樣本取平均
    this.sinceHop = 0;
    this.holdLeft = 0;
    this.result = { db: -100, speaking: false, pitch: 0, vowel: null };
  }

  // 餵樣本（任意長度）；每滿 1/60 秒分析一次，回傳這次餵進去之後最新的結果
  feed(samples) {
    for (let i = 0; i < samples.length; i++) {
      this.acc += samples[i];
      if (++this.accN === this.decimate) {
        this.ring[this.head] = this.acc / this.decimate;
        this.head = (this.head + 1) % VOICE.window;
        this.acc = 0; this.accN = 0;
      }
      if (++this.sinceHop >= this.hop) {
        this.sinceHop -= this.hop;
        this.analyze();
      }
    }
    return this.result;
  }

  analyze() {
    const h = this.head, w = VOICE.window;
    this.buf.set(this.ring.subarray(h), 0);
    this.buf.set(this.ring.subarray(0, h), w - h);
    const x = this.buf, n = x.length;
    let sum = 0;
    for (let i = 0; i < n; i++) sum += x[i] * x[i];
    const db = 10 * Math.log10(sum / n + 1e-12);
    const loud = db > this.floor + VOICE.open;
    this.holdLeft = loud ? VOICE.hold : Math.max(0, this.holdLeft - 1 / VOICE.fps);
    const pitch = loud ? this.pitch() : 0;
    this.result = { db, speaking: this.holdLeft > 0, pitch, vowel: pitch > 0 ? this.vowel() : null };
  }

  // 音調：正規化自相關，取第一個超過最高峰 0.9 倍的峰（避免抓到低八度），拋物線內插
  pitch() {
    const x = this.buf, n = x.length;
    const minLag = Math.floor(this.rate / VOICE.maxPitch), maxLag = Math.min(n - 32, Math.ceil(this.rate / VOICE.minPitch));
    const r = new Float64Array(maxLag + 2);
    // 能量的前綴和：sq[k] = x[0]² + … + x[k-1]²（每個延遲的 xx、yy 不用重算）
    const sq = new Float64Array(n + 1);
    for (let i = 0; i < n; i++) sq[i + 1] = sq[i] + x[i] * x[i];
    let best = 0;
    for (let lag = minLag; lag <= maxLag + 1; lag++) {
      let xy = 0;
      for (let i = 0; i + lag < n; i++) xy += x[i] * x[i + lag];
      const xx = sq[n - lag], yy = sq[n] - sq[lag];
      r[lag] = xy / (Math.sqrt(xx * yy) + 1e-12);
      if (lag <= maxLag) best = Math.max(best, r[lag]);
    }
    if (best < VOICE.voiced) return 0;
    for (let lag = minLag + 1; lag <= maxLag; lag++) {
      if (r[lag] >= best * 0.9 && r[lag] >= r[lag - 1] && r[lag] >= r[lag + 1]) {
        // 第 2 諧波很強時會抓到半個週期：兩倍週期的相關明顯更高就用兩倍（低八度）
        let L = lag;
        for (let d = -1; d <= 1; d++) {
          const l2 = 2 * lag + d;
          if (l2 <= maxLag && r[l2] > r[L] + 0.03 && r[l2] >= r[l2 - 1] && r[l2] >= r[l2 + 1]) L = l2;
        }
        const a = r[L - 1], b = r[L], c = r[L + 1], den = a - 2 * b + c;
        const shift = den !== 0 ? (0.5 * (a - c)) / den : 0;
        return this.rate / (L + shift);
      }
    }
    return 0;
  }

  // 母音：頻譜外形（200～3000 Hz，每 100 Hz 一個頻帶，開根號壓縮）和五個母音的範本（共振峰位置的高斯包絡）比，取最像的（餘弦相似度）
  vowel() {
    const n = this.buf.length, re = new Float64Array(n), im = new Float64Array(n);
    for (let i = 0; i < n; i++) re[i] = this.buf[i] * (0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (n - 1)));
    fft(re, im);
    const bands = BAND_CENTERS.map(fc => {
      let p = 0;
      for (let k = Math.ceil(((fc - 75) * n) / this.rate); k <= Math.floor(((fc + 75) * n) / this.rate); k++) p += re[k] * re[k] + im[k] * im[k];
      return Math.sqrt(p);
    });
    const norm = Math.sqrt(bands.reduce((s, v) => s + v * v, 0));
    if (norm <= 0) return null;
    let best = null, bestScore = -1;
    for (const [v, t] of Object.entries(TEMPLATES)) {
      const score = bands.reduce((s, b, i) => s + b * t[i], 0) / norm;
      if (score > bestScore) { bestScore = score; best = v; }
    }
    return best;
  }
}

// 母音範本：每個頻帶的包絡（開根號、正規化）
const BAND_CENTERS = Array.from({ length: 29 }, (_, i) => 200 + i * 100);
const TEMPLATES = Object.fromEntries(Object.entries(VOWELS).map(([v, fs]) => {
  const t = BAND_CENTERS.map(fc => Math.sqrt(fs.reduce((s, F) => s + Math.exp(-(((fc - F) / 200) ** 2)), 0)));
  const norm = Math.sqrt(t.reduce((s, x) => s + x * x, 0));
  return [v, t.map(x => x / norm)];
}));

// 「比平常高／低幾個半音」：base = 使用者平常的音調（校正）
export const semitones = (pitch, base) => (pitch > 0 && base > 0 ? 12 * Math.log2(pitch / base) : 0);

// 測試用合成音（tests/golden/voice.json）：靜音、純音、或依共振峰加權的諧波（像母音）
export function synth({ kind, f0 = 0, amp = 0.3, formants = [], seconds = 0.5 }, sampleRate) {
  const n = Math.round(seconds * sampleRate), out = new Float32Array(n);
  if (kind === "silence") return out;
  const harmonics = [];
  for (let h = 1; h * f0 < 3500; h++) {
    const f = h * f0;
    const g = kind === "tone" ? (h === 1 ? 1 : 0) : formants.reduce((s, F) => s + Math.exp(-(((f - F) / 120) ** 2)), 0) + 0.02;
    if (g > 0) harmonics.push([f, g]);
  }
  const norm = amp / Math.sqrt(harmonics.reduce((s, [, g]) => s + (g * g) / 2, 0));
  for (let i = 0; i < n; i++) {
    let s = 0;
    for (const [f, g] of harmonics) s += g * Math.sin((2 * Math.PI * f * i) / sampleRate);
    out[i] = s * norm;
  }
  return out;
}
