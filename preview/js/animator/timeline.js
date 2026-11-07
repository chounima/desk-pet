// 時間軸：每條軌道一列，關鍵影格是菱形；點一下跳到那個時間、拖關鍵影格移動、雙擊加關鍵影格
// 事件交給 main.js：onSeek(t)、onSelect(track, index)、onMove(track, index, t)、onAdd(track, t)
// 表情類（不連續）的列：track 名稱以 @ 開頭（@face、@eyes、@mouth），關鍵影格顯示成小標籤

const SNAP = 0.01;
const snap = t => Math.round(t / SNAP) * SNAP;

export class Timeline {
  constructor(root, handlers) {
    this.root = root;
    this.h = handlers;
    this.drag = null;
    addEventListener("pointermove", e => this.onMove(e));
    addEventListener("pointerup", () => { this.drag = null; });
  }

  // clip：正在編輯的動作；order：軌道順序（骨骼順序）；sel：{ track, index }
  // discrete：表情類的列 [{ track: "@face", label: "表情", keys: [[t, 名稱], …] }]，畫在骨骼軌道上面
  render(clip, order, time, sel, discrete = []) {
    this.clip = clip;
    const duration = clip.duration;
    const tracks = Object.keys(clip.tracks).sort((a, b) => order(a) - order(b));
    const rows = [this.ruler(duration)];
    for (const d of discrete) rows.push(this.discreteRow(d, duration, sel));
    for (const track of tracks) {
      const row = document.createElement("div");
      row.className = "track" + (sel?.track === track ? " sel" : "");
      const name = document.createElement("div");
      name.className = "name";
      name.textContent = track;
      name.title = track;
      name.onclick = () => this.h.onSelect(track, null);
      const lane = this.lane(duration);
      lane.onpointerdown = e => { if (e.target === lane) this.h.onSeek(this.timeAt(e, lane)); };
      lane.ondblclick = e => { if (e.target === lane) this.h.onAdd(track, snap(this.timeAt(e, lane))); };
      clip.tracks[track].forEach((k, i) => {
        const key = document.createElement("div");
        key.className = "key" + (k[2] && k[2] !== "smooth" ? " curve" : "") + (sel?.track === track && sel.index === i ? " sel" : "");
        key.style.left = `${(k[0] / duration) * 100}%`;
        key.title = `${k[0].toFixed(2)}s = ${Math.round(k[1] * 100) / 100}${k[2] ? `（${k[2]}）` : ""}`;
        key.onpointerdown = e => {
          e.stopPropagation();
          this.h.onSelect(track, i);
          this.drag = { track, index: i, lane };
        };
        lane.append(key);
      });
      row.append(name, lane);
      rows.push(row);
    }
    this.root.replaceChildren(...rows);
    this.setTime(time);
  }

  discreteRow({ track, label, keys }, duration, sel) {
    const row = document.createElement("div");
    row.className = "track discrete" + (sel?.track === track ? " sel" : "");
    const name = document.createElement("div");
    name.className = "name";
    name.textContent = label;
    name.title = track.slice(1);
    const lane = this.lane(duration);
    lane.onpointerdown = e => { if (e.target === lane) this.h.onSeek(this.timeAt(e, lane)); };
    lane.ondblclick = e => { if (e.target === lane) this.h.onAdd(track, snap(this.timeAt(e, lane))); };
    keys.forEach((k, i) => {
      const key = document.createElement("div");
      key.className = "tag" + (sel?.track === track && sel.index === i ? " sel" : "");
      key.style.left = `${(k[0] / duration) * 100}%`;
      key.textContent = k[1];
      key.title = `${k[0].toFixed(2)}s = ${k[1]}`;
      key.onpointerdown = e => {
        e.stopPropagation();
        this.h.onSelect(track, i);
        this.drag = { track, index: i, lane };
      };
      lane.append(key);
    });
    row.append(name, lane);
    return row;
  }

  ruler(duration) {
    const row = document.createElement("div");
    row.className = "ruler";
    const name = document.createElement("div");
    name.className = "name";
    name.textContent = `${duration.toFixed(2)} 秒`;
    const lane = this.lane(duration);
    const step = duration <= 1 ? 0.1 : duration <= 3 ? 0.25 : 0.5;
    for (let t = 0; t <= duration + 1e-6; t += step) {
      const tick = document.createElement("div");
      tick.className = "tick";
      tick.style.left = `${(t / duration) * 100}%`;
      tick.textContent = t.toFixed(t % 1 ? 2 : 0);
      lane.append(tick);
    }
    lane.onpointerdown = e => {
      this.h.onSeek(this.timeAt(e, lane));
      this.drag = { scrub: true, lane };
    };
    row.append(name, lane);
    return row;
  }

  lane() {
    const lane = document.createElement("div");
    lane.className = "lane";
    return lane;
  }

  timeAt(e, lane) {
    const r = lane.getBoundingClientRect();
    return Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)) * this.clip.duration;
  }

  onMove(e) {
    if (!this.drag) return;
    const t = snap(this.timeAt(e, this.drag.lane));
    if (this.drag.scrub) this.h.onSeek(t);
    else this.drag.index = this.h.onMove(this.drag.track, this.drag.index, t);
  }

  // 播放頭：每一列都畫一條（捲動時也看得到）
  setTime(time) {
    for (const lane of this.root.querySelectorAll(".lane")) {
      let head = lane.querySelector(".playhead");
      if (!head) {
        head = document.createElement("div");
        head.className = "playhead";
        lane.append(head);
      }
      head.style.left = `${(time / this.clip.duration) * 100}%`;
    }
  }
}
