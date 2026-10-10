// 排球對打小遊戲：寵物 vs 滑鼠游標
//
// 玩家：游標快速揮過球 → 依揮動速度擊球；直接點球 → 自動吊回寵物那邊；
//       游標碰到高速球（防守殺球）→ 自動接起
// 寵物：每顆來球決定一次打法 —— 能殺就三步助跑殺球、來得及就低手接球、來不及就魚躍救球
//       能力與台詞依角色而不同（volleySkills.js）
// 連續來回 10 次以上，球會燒起來
import { VOLLEY } from "../config.js";
import { skillFor } from "./volleySkills.js";

const now = () => performance.now() / 1000;
const clamp = (v, a, b) => Math.min(Math.max(v, a), b);
const rand = ([a, b]) => a + Math.random() * (b - a);
const pick = list => list[Math.floor(Math.random() * list.length)];

const HAND = ["handNear", 0, 12];      // 擊球點：近側手掌中間
const ACTIONS = ["vbBump", "vbApproach", "vbSpike", "vbDive"];

export class Volley {
  constructor({ pet, world, fx, view, onScore, onShake = () => {} }) {
    this.pet = pet;
    this.world = world;
    this.fx = fx;
    this.view = view;
    this.onScore = onScore;
    this.onShake = onShake;
    this.active = false;
    this.best = 0;
    this.cursor = { x: 0, y: 0, vx: 0, vy: 0, t: 0, seen: false };
  }

  get skill() { return skillFor(this.pet.rig.id); }

  // ---------- 開始／結束 ----------

  start() {
    this.active = true;
    this.view.show();
    this.serve();
  }

  stop() {
    this.active = false;
    this.view.hide();
    this.setFire(false);
    this.pet.faceOverride = null;
  }

  // 寵物先面向游標，在擊球點上方把球往上拋，落下時自己接
  serve() {
    this.pet.facing = this.aimDir();
    const hp = this.hitPoint();
    this.ball = { x: hp.x, y: hp.y - 20, vx: 0, vy: -VOLLEY.serveToss, r: VOLLEY.ballRadius, rot: 0 };
    this.state = "play";
    this.rally = 0;
    this.lastHitter = null;
    this.incomingSince = now();
    this.petWillMiss = false;
    this.missOffset = 0;
    this.plan = null;
    this.decided = false;
    this.pending = null;
    this.setFire(false);
    this.pet.play("vbReady");
    this.onScore(this.rally, this.best);
  }

  // ---------- 玩家輸入 ----------

  onPointerMove(x, y) {
    const c = this.cursor, t = now(), dt = t - c.t;
    if (c.seen && dt > 0 && dt < 0.1) {
      // 平滑過的游標速度
      c.vx = c.vx * 0.5 + ((x - c.x) / dt) * 0.5;
      c.vy = c.vy * 0.5 + ((y - c.y) / dt) * 0.5;
    }
    Object.assign(c, { x, y, t, seen: true });
  }

  // 點球：自動把球吊向寵物
  onBallClick() {
    if (this.state !== "play") return;
    this.lobToPet();
    this.playerHit("Nice!");
  }

  checkCursorHit() {
    const b = this.ball, c = this.cursor;
    if (!c.seen || now() - (this.lastPlayerHit ?? 0) < 0.25) return;
    if (Math.hypot(b.x - c.x, b.y - c.y) > b.r + VOLLEY.cursorRadius) return;

    // 游標停下來就不會再有移動事件，速度要視為 0，否則靜止的游標會一直「揮擊」
    const moving = now() - c.t < 0.05;
    if (moving && Math.hypot(c.vx, c.vy) >= VOLLEY.minSwipe) {
      b.vx = clamp(c.vx * 0.8, -VOLLEY.maxVx, VOLLEY.maxVx);
      b.vy = clamp(c.vy * 0.8 - 300, -1400, 1000);
      this.playerHit(this.rally + 1 >= 5 ? `${this.rally + 1}!` : "Nice!");
    } else if (this.lastHitter === "pet" && Math.hypot(b.vx, b.vy) > VOLLEY.digSpeed) {
      this.lobToPet();
      this.playerHit("接到了！");
    }
  }

  lobToPet() {
    const hp = this.hitPoint();
    this.launch(hp.x + (Math.random() - 0.5) * 300, hp.y, VOLLEY.lobArc);
  }

  // ---------- 每幀 ----------

  update(dt) {
    if (!this.active) return;
    this.updateBall(dt);
    if (this.state === "play") {
      this.checkCursorHit();
      this.updatePet(dt);
      this.updateFire(dt);
    } else {
      if (this.pending && now() >= this.pending.at) {
        this.pet.play(this.pending.anim, true);
        this.say(this.pending.line);
        this.pending = null;
      }
      if (now() > this.restartAt) this.serve();
    }
    this.view.draw(this.ball);
  }

  updateBall(dt) {
    const b = this.ball, w = this.world.width, ground = this.world.groundY;
    b.vy += VOLLEY.gravity * dt;
    b.x += b.vx * dt;
    b.y += b.vy * dt;
    b.rot += (b.vx * dt / b.r) * (180 / Math.PI);

    if (b.x < b.r) { b.x = b.r; b.vx = Math.abs(b.vx) * 0.8; }
    if (b.x > w - b.r) { b.x = w - b.r; b.vx = -Math.abs(b.vx) * 0.8; }
    if (b.y < b.r) { b.y = b.r; b.vy = Math.abs(b.vy) * 0.6; }
    if (b.y > ground - b.r) {
      b.y = ground - b.r;
      if (this.state === "play") this.endRally();
      b.vy = Math.abs(b.vy) > 80 ? -Math.abs(b.vy) * 0.45 : 0;
      b.vx *= 0.75;
    }
  }

  // ---------- 寵物：決定打法 ----------

  updatePet(dt) {
    if (this.plan) return this.runPlan(dt);

    const incoming = this.lastHitter !== "pet";
    const busy = this.isBusy();
    if (!incoming || now() - this.incomingSince < VOLLEY.reactDelay) {
      if (!busy) this.pet.play("vbReady");
      return;
    }
    if (!busy && !this.decided) {
      this.decided = true;
      this.plan = this.makePlan();
      if (this.plan) return this.runPlan(dt);
    }
    this.chaseBump(dt, busy);
  }

  isBusy() {
    return ACTIONS.includes(this.pet.action) && !this.pet.animator.finished;
  }

  // 殺球 > 一般低手接球 > 魚躍；回傳 null 表示用一般接球
  makePlan() {
    if (this.lastHitter === null) return null;        // 自己拋的發球只用低手
    const pet = this.pet, sk = this.skill, s = this.bodyScale(), aim = this.aimDir();
    const speed = VOLLEY.runSpeed * sk.speed;
    const inside = x => x > 40 && x < this.world.width - 40;

    // 1. 殺球：球會經過跳起時手的高度，而且來得及跑到助跑起點
    const spike = this.body("spike") === false ? { why: "這個型態不殺球" } : this.planSpike(aim, speed, s, inside);
    this.spikeWhy = spike.why;            // 除錯用：這球為什麼沒殺
    if (spike.plan) return spike.plan;

    // 2. 跑得到一般接球的位置 → 不需要特別打法
    const pb = this.predict(pet.y - this.body("hitHeight") * s);
    if (pb) {
      const standX = clamp(pb.x - aim * this.body("reach") * s, 40, this.world.width - 40);
      if (Math.abs(standX - pet.x) <= speed * pb.t) return null;
    }

    // 3. 來不及 → 魚躍；就算撲不到也撲（比站著看球落地有戲）
    if (this.body("dive") === false) return null;
    const d = pet.probe("vbDive", VOLLEY.diveContact, ...HAND);
    const pd = this.predict(pet.y + d.dy);
    if (!pd || Math.random() >= sk.dive) return null;
    const runT = pd.t - VOLLEY.diveContact;
    if (runT <= 0) return null;
    const dir = Math.sign(pd.x - pet.x) || pet.facing;
    const slide = VOLLEY.diveSlide * s;
    const reach = speed * runT;
    const xs = pet.x + clamp(pd.x - dir * (d.dx + slide) - pet.x, -reach, reach);
    return { type: "dive", dir, xs, d, slide, startAt: now() + runT, contactAt: now() + pd.t };
  }

  // 沿著預測軌跡找第一個「可以殺」的點。擊球可以發生在揮臂過程中的任一時刻（VOLLEY.spikeTimes），
  // 起跳高度也可以在 0～100% 之間調整，所以每個時刻都有一段可擊球的高度範圍。
  // 還要：時間夠三步助跑（不夠就原地起跳）、而且跑得到。
  planSpike(aim, speed, s, inside) {
    const pet = this.pet;
    const options = VOLLEY.spikeTimes.map(tc => {
      const full = pet.probe("vbSpike", tc, ...HAND);                      // 全力起跳時的手
      const flat = pet.probe("vbSpike", tc, ...HAND, { "root.y": 0 });     // 不起跳時的手
      return { tc, dx: full.dx, hi: -full.dy, lo: -flat.dy };
    });
    const hMax = Math.max(...options.map(o => o.hi)), hMin = Math.min(...options.map(o => o.lo));

    let why = "球不夠高";
    for (const p of this.trajectory()) {
      const h = pet.y - p.y;                     // 球離地高度
      if (p.vy <= 0 || h > hMax) continue;       // 還在上升，或比最高擊球點還高
      if (h < hMin) break;                       // 已經低到殺不到
      for (const o of options) {                 // 依偏好順序（最標準的擊球點在前）
        if (h < o.lo || h > o.hi) continue;
        const approachLead = VOLLEY.approachTime + o.tc;
        const approach = p.t >= approachLead + 0.05;
        if (!approach && p.t < o.tc + 0.05) { why = "時間不夠"; continue; }
        const x1 = p.x - aim * o.dx;                                       // 起跳點
        const x0 = approach ? x1 - aim * VOLLEY.approachDist * s : x1;     // 助跑起點
        if (!inside(x0) || !inside(x1)) { why = "太靠邊"; continue; }
        if (Math.abs(x0 - pet.x) > speed * (p.t - (approach ? approachLead : o.tc))) { why = "跑不到"; continue; }
        if (Math.random() >= this.skill.spike) return { why: "沒選殺球" };
        const contactAt = now() + p.t;
        return {
          why: approach ? "殺球（助跑）" : "殺球（原地）",
          plan: {
            type: "spike", aim, x0, x1, approach, contactAt,
            jump: (h - o.lo) / Math.max(1, o.hi - o.lo),
            startAt: contactAt - (approach ? approachLead : o.tc),
            c: { dx: o.dx, dy: -h },
          },
        };
      }
    }
    return { why };
  }

  runPlan(dt) {
    const p = this.plan, pet = this.pet, t = now();
    const speed = VOLLEY.runSpeed * this.skill.speed;

    if (p.type === "spike") {
      if (t < p.startAt) return this.moveTo(p.x0, speed, dt, p.aim);
      const k = p.approach ? (t - p.startAt) / VOLLEY.approachTime : 1;
      if (k < 1) {
        // 三步助跑：從目前位置往起跳點前進
        if (pet.action !== "vbApproach") {
          p.from = pet.x;
          pet.facing = p.aim;
          pet.play("vbApproach", true);
        }
        pet.x = p.from + (p.x1 - p.from) * k;
        return;
      }
      if (!p.jumped) {
        p.jumped = true;
        pet.x = p.x1;
        pet.facing = p.aim;
        pet.play("vbSpike", true, { "root.y": p.jump });   // 依球的高度決定跳多高
      }
      if (!p.hit && this.touches(p.c, t - p.contactAt, 1.4)) {
        p.hit = true;
        this.spikeHit();
      }
      if (pet.action === "vbSpike" && pet.animator.finished) {
        if (!p.hit) this.lastWhiff = p;     // 除錯：揮空的殺球
        this.plan = null;
      }
      return;
    }

    if (p.type === "dive") {
      if (t < p.startAt) return this.moveTo(p.xs, speed, dt, p.dir);
      if (!p.dived) {
        p.dived = true;
        p.from = pet.x;
        pet.facing = p.dir;
        pet.play("vbDive", true);
      }
      // 撲出的前衝：在碰到球之前滑完
      const k = Math.min(1, (t - p.startAt) / VOLLEY.diveContact);
      pet.x = clamp(p.from + p.dir * p.slide * k, 20, this.world.width - 20);
      if (!p.hit && this.touches(p.d, t - p.contactAt, 1.7, 1.5)) {
        p.hit = true;
        this.diveHit();
      }
      if (pet.action === "vbDive" && pet.animator.finished) this.plan = null;
    }
  }

  // 擊球時間點附近，球是否碰到手
  // off：probe 得到的手部位移；reachMul / windowMul：距離與時間容許誤差的倍率
  touches(off, dtFromContact, reachMul, windowMul = 1) {
    if (Math.abs(dtFromContact) > VOLLEY.contactWindow * windowMul) return false;
    const b = this.ball, pet = this.pet;
    const hx = pet.x + pet.facing * off.dx, hy = pet.y + off.dy;
    const dist = Math.hypot(b.x - hx, b.y - hy);
    if (this.plan) this.plan.minDist = Math.min(this.plan.minDist ?? Infinity, dist);   // 除錯用
    return dist < b.r + VOLLEY.hitTolerance * reachMul;
  }

  moveTo(x, speed, dt, faceAfter) {
    const pet = this.pet, d = x - pet.x;
    if (Math.abs(d) > 6) {
      pet.facing = Math.sign(d);
      pet.x += Math.sign(d) * Math.min(Math.abs(d), speed * dt);
      pet.play("run");
    } else {
      pet.facing = faceAfter;
      pet.play("vbReady");
    }
  }

  // 一般低手接球：跑到落點，等球掉到手臂高度
  chaseBump(dt, busy) {
    const pet = this.pet, b = this.ball, s = this.bodyScale(), aim = this.aimDir();
    const pb = this.predict(pet.y - this.body("hitHeight") * s);
    const landX = pb ? pb.x : b.x;
    const standX = clamp(landX - aim * this.body("reach") * s + this.missOffset, 40, this.world.width - 40);
    if (!busy) this.moveTo(standX, VOLLEY.runSpeed * this.skill.speed, dt, aim);

    const hp = this.hitPoint();
    if (!this.petWillMiss && b.vy > 0 && Math.hypot(b.x - hp.x, b.y - hp.y) < b.r + VOLLEY.hitTolerance) {
      pet.facing = this.aimDir();
      pet.play("vbBump", true);
      const c = this.aimTarget();
      this.launch(c.x, Math.min(c.y, this.world.groundY - 120));
      this.afterPetHit();
    }
  }

  // ---------- 擊球 ----------

  // 往下扣向游標的位置
  spikeHit() {
    const b = this.ball, c = this.aimTarget(), g = VOLLEY.gravity;
    const tx = clamp(c.x, b.r, this.world.width - b.r), ty = this.world.groundY - b.r;
    const T = clamp(Math.hypot(tx - b.x, ty - b.y) / (VOLLEY.spikeSpeed * this.skill.power), 0.45, 1);
    b.vx = (tx - b.x) / T;
    b.vy = (ty - b.y - 0.5 * g * T * T) / T;
    this.say("spike");
    this.onShake();
    this.afterPetHit();
  }

  diveHit() {
    const c = this.aimTarget();
    this.launch(c.x, Math.min(c.y, this.world.groundY - 120), VOLLEY.lobArc);
    this.say("dive");
    this.afterPetHit();
  }

  afterPetHit() {
    this.lastHitter = "pet";
    this.rally++;
    this.onScore(this.rally, this.best);
    this.checkFire();
  }

  playerHit(text) {
    this.lastHitter = "player";
    this.lastPlayerHit = now();
    this.incomingSince = now();
    this.rally++;
    this.plan = null;
    this.decided = false;
    const { base, perRally, max } = VOLLEY.miss;
    this.petWillMiss = Math.random() < Math.min(max, (base + perRally * this.rally) * this.skill.miss);
    // 漏接時故意站偏一點，讓球從手邊擦過，而不是穿過身體
    this.missOffset = this.petWillMiss
      ? (this.ball.r + VOLLEY.hitTolerance + 20) * (Math.random() < 0.5 ? 1 : -1) : 0;
    this.fx.burst(text, { x: this.ball.x, y: this.ball.y - this.ball.r }, "fx-pop");
    this.onScore(this.rally, this.best);
    this.checkFire();
  }

  // 讓球以拋物線飛到 (tx, ty)，頂點比起點和終點都高
  launch(tx, ty, arc = VOLLEY.arc) {
    const b = this.ball, g = VOLLEY.gravity;
    const apexY = Math.max(b.r + 10, Math.min(b.y, ty) - rand(arc));
    b.vy = -Math.sqrt(2 * g * Math.max(1, b.y - apexY));
    const t = -b.vy / g + Math.sqrt((2 * Math.max(1, ty - apexY)) / g);
    b.vx = clamp((tx - b.x) / t, -VOLLEY.maxVx, VOLLEY.maxVx);
  }

  // ---------- 火焰 ----------

  checkFire() {
    const on = this.rally >= VOLLEY.fireAt;
    if (on && !this.onFire) {
      this.fx.burst(`🔥 ${VOLLEY.fireAt} 連發！`, { x: this.ball.x, y: this.ball.y - 40 }, "fx-pop fx-big");
      this.say("fire");
    }
    this.setFire(on);
  }

  setFire(on) {
    this.onFire = on;
    this.view.setFire(on);
  }

  updateFire(dt) {
    if (!this.onFire) return;
    this.flameTimer = (this.flameTimer ?? 0) - dt;
    if (this.flameTimer > 0) return;
    this.flameTimer = 0.025;
    const b = this.ball;
    this.fx.flame(b.x + (Math.random() - 0.5) * b.r, b.y + (Math.random() - 0.5) * b.r, b.r);
  }

  // ---------- 回合結束 ----------

  endRally() {
    const petMissed = this.lastHitter !== "pet";
    const diving = this.plan?.type === "dive";
    this.state = "over";
    this.restartAt = now() + VOLLEY.restartAfter;
    this.best = Math.max(this.best, this.rally);
    this.onScore(this.rally, this.best);
    // 魚躍或殺球中就等動作做完再反應
    this.pending = {
      at: now() + (this.isBusy() ? 0.8 : 0),
      anim: petMissed ? "shy" : "cheer",
      line: petMissed ? (diving ? "diveFail" : "petMiss") : "playerMiss",
    };
    this.plan = null;
    this.setFire(false);
  }

  say(key) {
    this.fx.burst(pick(this.skill.lines[key]), this.pet.headTop(), "fx-pop");
  }

  // ---------- 幾何 ----------

  // 寵物要把球打向哪裡：游標；還沒偵測到游標時打向畫面中央
  aimTarget() {
    return this.cursor.seen ? this.cursor : { x: this.world.width / 2, y: this.world.groundY / 2 };
  }

  aimDir() {
    return Math.sign(this.aimTarget().x - this.pet.x) || this.pet.facing;
  }

  // 依型態調整的排球參數（例如四足用頭頂球，擊球點比較高、不殺球）；沒設定就用 VOLLEY
  body(key) {
    return this.pet.rig.volley?.[key] ?? VOLLEY[key];
  }

  bodyScale() {
    return this.pet.scale * (this.pet.rig.rootScale ?? 1);
  }

  // 一般低手接球的擊球點
  hitPoint() {
    const s = this.bodyScale();
    return { x: this.pet.x + this.pet.facing * this.body("reach") * s, y: this.pet.y - this.body("hitHeight") * s };
  }

  // 預測球接下來的軌跡 [{ t, x, y, vy }]，直到落地
  // 用和 updateBall 相同的規則往前模擬，所以撞牆、撞天花板反彈都算得準
  trajectory() {
    const b = this.ball, g = VOLLEY.gravity, w = this.world.width, ground = this.world.groundY;
    const step = 1 / 120, out = [];
    let { x, y, vx, vy } = b;
    for (let t = step; t < 6; t += step) {
      vy += g * step;
      x += vx * step;
      y += vy * step;
      if (x < b.r) { x = b.r; vx = Math.abs(vx) * 0.8; }
      if (x > w - b.r) { x = w - b.r; vx = -Math.abs(vx) * 0.8; }
      if (y < b.r) { y = b.r; vy = Math.abs(vy) * 0.6; }
      if (y > ground - b.r) break;
      out.push({ t, x, y, vy });
    }
    return out;
  }

  // 球下落經過高度 y 的時間與 x；不會經過時回傳 null
  predict(y) {
    let prev = { y: this.ball.y };
    for (const p of this.trajectory()) {
      if (p.vy > 0 && prev.y < y && p.y >= y) return { t: p.t, x: p.x };
      prev = p;
    }
    return null;
  }
}
