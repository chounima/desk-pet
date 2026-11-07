// 次級物理：尾巴、披布、頭髮等骨頭在動畫給的角度上加一個「彈簧＋阻尼」的偏移（和桌寵 godot/src/Core/Spring.cs 算法一樣）
//
// 每根物理骨頭有偏移角 θ 與角速度 ω（弧度），每個子步驟（固定 1/120 秒）：
//   α = −stiffness·θ − damping·ω + gravity·cos(a) + (inertia·加速度 + drag·速度) 在骨頭垂直方向的分量 / length
// a = 動畫角度＋祖先物理骨頭的偏移＋θ；速度、加速度是骨頭起點的（Q 版單位／秒，含身體在畫面上的移動）。
// 翻面時 θ、ω 取負；瞬間移動時重設。共用測試：tests/golden/physics.json

export const STEP = 1 / 120;
const MAX_STEPS = 8;
const TELEPORT_DISTANCE = 400;
const DEFAULTS = { stiffness: 120, damping: 8, gravity: 0, inertia: 1, drag: 0, limit: 35, length: 60 };

export class Spring {
  // bones: [{ name, parent }]（父骨骼在前）；physics: rig.physics { 骨骼: 參數 }
  constructor(bones, physics) {
    this.bones = [];
    this.offsets = {};
    this.accumulator = 0;
    this.prevFacing = 0;
    if (!physics) return;
    const parents = Object.fromEntries(bones.map(b => [b.name, b.parent]));
    for (const b of bones) {
      if (!physics[b.name]) continue;
      let ancestor = b.parent;
      while (ancestor && !physics[ancestor]) ancestor = parents[ancestor];
      this.bones.push({ name: b.name, p: { ...DEFAULTS, ...physics[b.name] }, parent: ancestor ?? null,
                        theta: 0, omega: 0, hasPrev: false, hasVel: false, prevX: 0, prevY: 0, velX: 0, velY: 0 });
    }
  }

  get active() { return this.bones.length > 0; }

  reset() {
    for (const b of this.bones) Object.assign(b, { theta: 0, omega: 0, hasPrev: false, hasVel: false });
    this.offsets = {};
    this.accumulator = 0;
  }

  // world：這一幀「只有動畫」的骨骼世界矩陣 [a, b, c, d, e, f]；回傳 { 骨骼: 偏移角（度） }
  update(dt, world, bodyX, bodyY, scale, facing) {
    if (!this.bones.length) return this.offsets;
    if (this.prevFacing !== 0 && facing !== this.prevFacing)
      for (const b of this.bones) Object.assign(b, { theta: -b.theta, omega: -b.omega, hasPrev: false, hasVel: false });
    this.prevFacing = facing;

    const accel = {};
    for (const b of this.bones) {
      const m = world[b.name];
      const gx = facing * bodyX / scale + m[4], gy = bodyY / scale + m[5];
      let ax = 0, ay = 0;
      if (b.hasPrev && dt > 0) {
        if (Math.abs(gx - b.prevX) + Math.abs(gy - b.prevY) > TELEPORT_DISTANCE) b.hasVel = false;
        else {
          const vx = (gx - b.prevX) / dt, vy = (gy - b.prevY) / dt;
          if (b.hasVel) { ax = (vx - b.velX) / dt; ay = (vy - b.velY) / dt; }
          Object.assign(b, { velX: vx, velY: vy, hasVel: true });
        }
      }
      Object.assign(b, { prevX: gx, prevY: gy, hasPrev: true });
      accel[b.name] = [ax, ay, b.hasVel ? b.velX : 0, b.hasVel ? b.velY : 0];
    }

    this.accumulator += dt;
    let steps = 0;
    while (this.accumulator >= STEP && steps < MAX_STEPS) {
      this.accumulator -= STEP;
      steps++;
      for (const b of this.bones) {
        const p = b.p;
        const angle = this.animatedAngle(world, b) + b.theta;
        const [ax, ay, vx, vy] = accel[b.name];
        const fx = p.inertia * ax + p.drag * vx, fy = p.inertia * ay + p.drag * vy;
        const alpha = -p.stiffness * b.theta - p.damping * b.omega + p.gravity * Math.cos(angle)
                      + (fx * Math.sin(angle) - fy * Math.cos(angle)) / Math.max(1, p.length);
        b.omega += alpha * STEP;
        b.theta += b.omega * STEP;
        const limit = p.limit * Math.PI / 180;
        if (Math.abs(b.theta) > limit) { b.theta = Math.sign(b.theta) * limit; b.omega = 0; }
      }
    }
    if (steps === MAX_STEPS) this.accumulator = 0;

    this.offsets = {};
    for (const b of this.bones) this.offsets[b.name] = b.theta * 180 / Math.PI;
    return this.offsets;
  }

  animatedAngle(world, b) {
    const m = world[b.name];
    let angle = Math.atan2(m[1], m[0]);
    for (let parent = b.parent; parent;) {
      const pb = this.bones.find(x => x.name === parent);
      angle += pb.theta;
      parent = pb.parent;
    }
    return angle;
  }
}
