// 頭上的特效：Zzz、音符、驚嘆號，以及一次性的小粒子

const EMITTERS = {
  zzz: { every: 0.9, text: () => "Z" },
  notes: { every: 0.45, text: () => (Math.random() < 0.5 ? "♪" : "♫") },
};

export class Fx {
  constructor(layer) {
    this.layer = layer;
    this.timer = 0;
    this.alert = null;
  }

  // kind: 目前動作的特效名稱；anchor: 頭頂的螢幕座標
  update(dt, kind, anchor) {
    this.setAlert(kind === "alert", anchor);
    const emitter = EMITTERS[kind];
    if (!emitter) return;
    this.timer -= dt;
    if (this.timer <= 0) {
      this.timer = emitter.every;
      this.burst(emitter.text(), anchor, `fx-${kind}`);
    }
  }

  setAlert(on, anchor) {
    if (on && !this.alert) {
      this.alert = this.spawn("!", anchor, "fx-alert");
    } else if (!on && this.alert) {
      this.alert.remove();
      this.alert = null;
    }
    if (this.alert) this.place(this.alert, anchor.x, anchor.y - 14);
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
