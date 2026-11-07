// 網格變形（和桌寵 Core/Mesh.cs 同一套計算，tests/golden/mesh.json 比對）
// 插槽的 mesh：{ cols, rows, bones: [骨骼…] }。在部件範圍（box）鋪 cols × rows 的格子，
// 每個頂點依「到每根骨頭線段的距離」自動算權重，每幀把各骨骼的變換依權重混合 → 關節彎曲時部件跟著彎，不會折出硬角。
import { apply, invert } from "./math.js";

const MIN_WEIGHT = 0.001;     // 太小的權重丟掉（兩邊一樣）

// 骨頭線段（靜止姿勢、世界座標）：起點 = 骨骼原點，終點 = 子骨骼原點的平均；沒有子骨骼時用掛在它上面的部件範圍中心
function segment(name, rest, bones, slots) {
  const m = rest[name];
  const children = bones.filter(b => b.parent === name);
  let end;
  if (children.length) {
    end = [children.reduce((s, b) => s + b.x, 0) / children.length, children.reduce((s, b) => s + b.y, 0) / children.length];
  } else {
    const boxes = slots.filter(s => s.bone === name && s.box).map(s => s.box);
    end = boxes.length ? [boxes.reduce((s, b) => s + (b[0] + b[2]) / 2, 0) / boxes.length, boxes.reduce((s, b) => s + (b[1] + b[3]) / 2, 0) / boxes.length] : [0, 0];
  }
  return [apply(m, 0, 0), apply(m, end[0], end[1])];
}

function distToSegment([px, py], [[ax, ay], [bx, by]]) {
  const dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
  const t = len2 > 0 ? Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / len2)) : 0;
  return Math.hypot(px - ax - t * dx, py - ay - t * dy);
}

export class MeshDeform {
  // slot：{ bone, box, mesh }；rest：靜止姿勢的世界矩陣；bones、slots：rig 的骨骼與插槽；unit：rootScale（距離換成 Q 版單位算權重）
  constructor(slot, rest, bones, slots, unit = 1) {
    const { cols, rows } = slot.mesh;
    const names = slot.mesh.bones.filter(b => rest[b]);
    const [x0, y0, x1, y1] = slot.box;
    this.cols = cols;
    this.rows = rows;
    this.uv = [];             // 頂點在部件裡的位置（插槽骨骼的座標）
    for (let r = 0; r <= rows; r++)
      for (let c = 0; c <= cols; c++) this.uv.push([x0 + ((x1 - x0) * c) / cols, y0 + ((y1 - y0) * r) / rows]);
    this.triangles = [];      // 每個格子兩個三角形（頂點索引）
    for (let r = 0; r < rows; r++)
      for (let c = 0; c < cols; c++) {
        const i = r * (cols + 1) + c;
        this.triangles.push([i, i + 1, i + cols + 1], [i + 1, i + cols + 2, i + cols + 1]);
      }

    const segs = names.map(n => segment(n, rest, bones, slots));
    const inverse = Object.fromEntries(names.map(n => [n, invert(rest[n])]));
    // 每個頂點：[{ bone, x, y（在那根骨頭座標裡的靜止位置）, w }]
    this.binds = this.uv.map(([u, v]) => {
      const p = apply(rest[slot.bone], u, v);
      let ws = segs.map(s => 1 / (distToSegment(p, s) / unit + 1) ** 4);
      let sum = ws.reduce((a, b) => a + b, 0);
      ws = ws.map(w => (w / sum < MIN_WEIGHT ? 0 : w));
      sum = ws.reduce((a, b) => a + b, 0);
      return names.map((n, k) => ({ bone: n, w: ws[k] / sum, local: apply(inverse[n], p[0], p[1]) })).filter(b => b.w > 0);
    });
  }

  // 這一幀的頂點位置（角色座標，和 world 矩陣同一個空間）
  compute(world) {
    return this.binds.map(bs => {
      let x = 0, y = 0;
      for (const b of bs) {
        const [wx, wy] = apply(world[b.bone], b.local[0], b.local[1]);
        x += wx * b.w;
        y += wy * b.w;
      }
      return [x, y];
    });
  }
}
