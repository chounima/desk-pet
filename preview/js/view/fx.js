// 頭上的特效：Zzz、音符、驚嘆號，以及一次性的小粒子

const EMITTERS = {
  zzz: { every: 0.9, text: () => "Z" },
  notes: { every: 0.45, text: () => (Math.random() < 0.5 ? "♪" : "♫") },
  hearts: { every: 0.5, text: () => "♥", cls: "fx-heart" },
};

// 停在頭上、會抖動的符號
const MARKS = {
  alert: { text: "!", dx: 0, dy: -14 },
  anger: { text: "💢", dx: 24, dy: -10 },
};

export class Fx {
  constructor(layer) {
    this.layer = layer;
    this.timer = 0;
    this.mark = null;
    this.markKind = null;
  }

  // kind: 目前動作的特效名稱；anchor: 頭頂的螢幕座標
  update(dt, kind, anchor) {
    this.setMark(MARKS[kind] ? kind : null, anchor);
    const emitter = EMITTERS[kind];
    if (!emitter) return;
    this.timer -= dt;
    if (this.timer <= 0) {
      this.timer = emitter.every;
      this.burst(emitter.text(), anchor, emitter.cls ?? `fx-${kind}`);
    }
  }

  setMark(kind, anchor) {
    if (kind !== this.markKind) {
      this.mark?.remove();
      this.mark = kind ? this.spawn(MARKS[kind].text, anchor, "fx-alert") : null;
      this.markKind = kind;
    }
    if (this.mark) this.place(this.mark, anchor.x + MARKS[kind].dx, anchor.y + MARKS[kind].dy);
  }

  burst(text, anchor, cls = "fx-pop") {
    const el = this.spawn(text, anchor, `fx-float ${cls}`);
    this.place(el, anchor.x + (Math.random() - 0.5) * 40, anchor.y);
    el.addEventListener("animationend", () => el.remove());
  }

  // 火焰粒子：火球的尾巴
  flame(x, y, size) {
    const el = this.spawn("", { x, y }, "fx-flame");
    const d = size * (0.6 + Math.random() * 0.5);
    el.style.width = el.style.height = `${d}px`;
    el.addEventListener("animationend", () => el.remove());
  }

  spawn(text, anchor, cls) {
    const el = document.createElement("span");
    el.className = `fx ${cls}`;
    el.textContent = text;
    this.place(el, anchor.x, anchor.y);
    this.layer.append(el);
    return el;
  }

  place(el, x, y) {
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
  }
}
