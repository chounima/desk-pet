// 骨架：依姿勢（各骨骼角度與位移）算出每根骨骼的世界矩陣
import { mul, rotate, translate } from "./math.js";

export class Skeleton {
  // bones: [{ name, parent, x, y }]，父骨骼必須在前
  // moveScale: 動作位移是以 Q 版身材寫的，依角色腿長放大
  // angleScale: 個別骨骼的角度倍率，例如披布沿用尾巴動作但擺幅較小
  constructor(bones, moveScale = 1, angleScale = {}) {
    this.bones = bones;
    this.moveScale = moveScale;
    this.angleScale = angleScale;
    this.world = {};
  }

  // pose: { 骨骼名: 角度, "骨骼名.x": 位移, "骨骼名.y": 位移 }
  compute(pose) {
    const s = this.moveScale;
    for (const b of this.bones) {
      const tx = b.x + (pose[`${b.name}.x`] ?? 0) * s;
      const ty = b.y + (pose[`${b.name}.y`] ?? 0) * s;
      const angle = (pose[b.name] ?? 0) * (this.angleScale[b.name] ?? 1);
      const local = mul(translate(tx, ty), rotate(angle));
      this.world[b.name] = b.parent ? mul(this.world[b.parent], local) : local;
    }
    return this.world;
  }
}
