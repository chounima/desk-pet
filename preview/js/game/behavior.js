// 行為邏輯：自動選動作、走路移動、打字反應、睡覺、被拖曳與掉落
import { AUTO, CLICK_REACTIONS, TUNING } from "../config.js";

const now = () => performance.now() / 1000;
const rand = ([a, b]) => a + Math.random() * (b - a);

function weightedPick(table, allowed = () => true) {
  const entries = Object.entries(table).filter(([name]) => allowed(name));
  let r = Math.random() * entries.reduce((s, [, v]) => s + v.weight, 0);
  for (const [name, v] of entries) if ((r -= v.weight) <= 0) return name;
  return entries[0][0];
}

export class Behavior {
  // world: { width, groundY }
  constructor(pet, world) {
    this.pet = pet;
    this.world = world;
    this.auto = true;
    this.state = "ground";        // ground | held | air
    this.vy = 0;
    this.timer = 0;
    this.target = null;           // 走路的目標 x
    this.lastInput = now();
    this.typingUntil = 0;
    this.taps = [];
    this.grabOffset = 0;
    this.minigame = null;         // 進行中的小遊戲會接管寵物
    this.start("idle");
  }

  get inMinigame() { return !!this.minigame?.active; }

  // ---------- 外部事件 ----------

  onKey() {
    const t = now();
    this.lastInput = t;
    this.taps.push(t);
    if (this.state !== "ground" || this.inMinigame) return;
    this.typingUntil = t + TUNING.typingHold;
    if (this.pet.action !== "typing") this.start("typing");
  }

  onActivity() {
    this.lastInput = now();
    if (this.pet.action === "sleep") this.start("stretch");
  }

  onPetClick() {
    this.lastInput = now();
    if (this.state !== "ground" || this.inMinigame) return;
    const reaction = CLICK_REACTIONS[Math.floor(Math.random() * CLICK_REACTIONS.length)];
    this.start(this.pet.action === "sleep" ? "stretch" : reaction, 2.4);
  }

  grab(x, y) {
    this.state = "held";
    this.grabOffset = this.pet.y - y;
    this.pet.faceOverride = null;
    this.pet.play("dragged");
    this.moveHeld(x, y);
  }

  moveHeld(x, y) {
    this.pet.x = x;
    this.pet.y = y + this.grabOffset;
  }

  release() {
    this.state = "air";
    this.vy = 0;
    this.pet.play("fall");
  }

  // 從面板手動播放
  force(name) {
    this.lastInput = now();
    if (this.state !== "ground") return;
    if (name === "typing") this.typingUntil = this.auto ? now() + 5 : Infinity;
    this.start(name, this.auto ? undefined : Infinity);
  }

  setAuto(on) {
    this.auto = on;
    if (on) this.timer = 0;
  }

  // ---------- 每幀 ----------

  update(dt) {
    const pet = this.pet;
    const ground = this.world.groundY;

    if (this.inMinigame) {
      this.lastInput = now();
      pet.y = ground;
      return;
    }
    if (this.state === "held") return;
    if (this.state === "air") {
      this.vy += TUNING.gravity * dt;
      pet.y += this.vy * dt;
      if (pet.y >= ground) {
        pet.y = ground;
        this.state = "ground";
        this.start("idle");
      }
      return;
    }
    pet.y = ground;
    pet.x = Math.min(Math.max(pet.x, 40), this.world.width - 40);

    if (pet.action === "typing") return this.updateTyping();
    pet.faceOverride = null;

    if (pet.action === "sleep") return;
    if (this.auto && now() - this.lastInput > TUNING.sleepAfter) return this.start("sleep", Infinity);

    if (this.target !== null) return this.updateWalk(dt);

    this.timer -= dt;
    if (!pet.animator.finished && this.timer > 0) return;
    if (this.auto) this.start(weightedPick(AUTO, this.allowed ?? undefined));
    else if (pet.animator.finished) this.start(pet.animator.clip.next ?? "idle", Infinity);
  }

  updateTyping() {
    const t = now();
    this.taps = this.taps.filter(k => t - k < 1);
    this.pet.faceOverride = this.taps.length >= TUNING.focusRate ? "focus" : null;
    if (t > this.typingUntil) {
      this.pet.faceOverride = null;
      this.start("idle");
    }
  }

  updateWalk(dt) {
    const pet = this.pet;
    const step = pet.speed * dt;
    const dist = this.target - pet.x;
    if (Math.abs(dist) <= step) {
      pet.x = this.target;
      this.target = null;
      this.start(this.auto ? "idle" : pet.action, this.auto ? undefined : Infinity);
      return;
    }
    pet.facing = Math.sign(dist);
    pet.x += Math.sign(dist) * step;
  }

  // ---------- 切換動作 ----------

  start(name, duration) {
    const pet = this.pet;
    pet.play(name, true);
    const clip = pet.animator.clip;
    this.timer = duration ?? (clip.loop ? rand(AUTO[name]?.time ?? [3, 5]) : Infinity);
    this.target = clip.move ? this.pickTarget() : null;
  }

  pickTarget() {
    const { width } = this.world;
    const m = TUNING.margin;
    let x;
    do x = m + Math.random() * (width - 2 * m);
    while (Math.abs(x - this.pet.x) < 150 && width > 400);
    return x;
  }
}
