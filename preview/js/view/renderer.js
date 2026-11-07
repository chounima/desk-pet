// 把角色部件畫成 SVG <image>，每幀套用骨骼矩陣
// 有網格（slot.mesh）的部件：每個三角形一張裁切過的 <image>，依頂點位置做仿射變換（core/mesh.js 算頂點）
import { mul } from "../core/math.js";
import { MeshDeform } from "../core/mesh.js";
import { Skeleton } from "../core/skeleton.js";

const NS = "http://www.w3.org/2000/svg";
const SEAM = 1;           // 三角形往外多裁一點，接縫才不會露出細縫
let meshIds = 0;

// 網格部件：img 是陣列（每個三角形一張），換圖時全部一起換
function setImage(img, href, box) {
  if (Array.isArray(img)) { for (const i of img) setImage(i, href, box); return; }
  const [x0, y0, x1, y1] = box;
  img.setAttribute("href", href);
  img.setAttribute("x", x0);
  img.setAttribute("y", y0);
  img.setAttribute("width", x1 - x0);
  img.setAttribute("height", y1 - y0);
}

// 三角形 (u0,u1,u2) → (p0,p1,p2) 的仿射矩陣 [a, b, c, d, e, f]
function affine([u0, u1, u2], [p0, p1, p2]) {
  const du1 = [u1[0] - u0[0], u1[1] - u0[1]], du2 = [u2[0] - u0[0], u2[1] - u0[1]];
  const dp1 = [p1[0] - p0[0], p1[1] - p0[1]], dp2 = [p2[0] - p0[0], p2[1] - p0[1]];
  const det = du1[0] * du2[1] - du2[0] * du1[1];
  const a = (dp1[0] * du2[1] - dp2[0] * du1[1]) / det, c = (dp2[0] * du1[0] - dp1[0] * du2[0]) / det;
  const b = (dp1[1] * du2[1] - dp2[1] * du1[1]) / det, d = (dp2[1] * du1[0] - dp1[1] * du2[0]) / det;
  return [a, b, c, d, p0[0] - a * u0[0] - c * u0[1], p0[1] - b * u0[0] - d * u0[1]];
}

// 三角形每條邊往外推 SEAM（頂點移到相鄰兩條外推邊的交點），細長的三角形也蓋得住接縫
function grow(p) {
  const cx = (p[0][0] + p[1][0] + p[2][0]) / 3, cy = (p[0][1] + p[1][1] + p[2][1]) / 3;
  // 邊的法向量，朝三角形外面（和重心反方向）
  const normal = (a, b) => {
    const dx = b[0] - a[0], dy = b[1] - a[1], len = Math.hypot(dx, dy) || 1;
    const n = [dy / len, -dx / len];
    return n[0] * ((a[0] + b[0]) / 2 - cx) + n[1] * ((a[1] + b[1]) / 2 - cy) < 0 ? [-n[0], -n[1]] : n;
  };
  return p.map((v, i) => {
    const n1 = normal(p[(i + 2) % 3], v), n2 = normal(v, p[(i + 1) % 3]);
    const k = SEAM / Math.max(0.2, 1 + n1[0] * n2[0] + n1[1] * n2[1]);
    return [v[0] + (n1[0] + n2[0]) * k, v[1] + (n1[1] + n2[1]) * k];
  });
}

// 網格部件的 DOM：每個三角形 <g clip-path><image transform/></g>，裁切多邊形放在 <defs>
function makeMesh(el, href, box, mesh) {
  const defs = document.createElementNS(NS, "defs");
  el.append(defs);
  return mesh.triangles.map(() => {
    const id = `mesh${++meshIds}`;
    const clip = document.createElementNS(NS, "clipPath");
    clip.id = id;
    const poly = document.createElementNS(NS, "polygon");
    clip.append(poly);
    defs.append(clip);
    const g = document.createElementNS(NS, "g");
    g.setAttribute("clip-path", `url(#${id})`);
    const img = makeImage(href, box);
    g.append(img);
    el.append(g);
    return { poly, img };
  });
}

function makeImage(href, box) {
  const img = document.createElementNS(NS, "image");
  setImage(img, href, box);
  return img;
}

function preload(urls) {
  for (const url of urls) new Image().src = url;
}

// 部件圖的網址：完整網址（部件包的圖）直接用，否則接在角色資料夾後面
const resolve = (rig, file) => /^[a-z]+:/i.test(file) ? file : rig.base + file;

export class PetView {
  static meshDisabled = new URLSearchParams(location.search).has("nomesh");     // 比較用（?nomesh）
  constructor(layer, rig, props) {
    this.rig = rig;
    this.g = document.createElementNS(NS, "g");
    this.g.classList.add("pet");
    layer.append(this.g);

    const items = [];
    this.altSlots = [];       // 有替換圖的插槽
    let rest = null;          // 靜止姿勢（網格算權重用）
    for (const slot of rig.slots) {
      const el = document.createElementNS(NS, "g");
      const href = rig.base + (slot.variants ? slot.variants.idle : slot.file);
      let img, mesh = null, tris = null;
      if (slot.mesh?.bones?.length && !PetView.meshDisabled) {
        rest ??= { ...new Skeleton(rig.bones, rig.rootScale ?? 1, rig.trackScale ?? {}).compute({}) };
        mesh = new MeshDeform(slot, rest, rig.bones, rig.slots, rig.rootScale ?? 1);
        tris = makeMesh(el, href, slot.box, mesh);
        img = tris.map(t => t.img);
      } else {
        img = makeImage(href, slot.box);
        el.append(img);
      }
      if (slot.far) el.classList.add("far");
      if (slot.variants) {
        this.faceImg = img;
        this.faceEl = el;
        this.faceVariants = slot.variants;
        preload(Object.values(slot.variants).map(f => rig.base + f));
        // 分層臉：每個圖層一張圖，疊在表情插槽的位置（顯示哪張由 game/faceDriver.js 決定）
        if (rig.face) {
          this.faceLayers = rig.face.layers.map((l, i) => {
            const layerEl = document.createElementNS(NS, "g");
            const layerImg = makeImage(rig.base + Object.values(l.variants)[0], rig.face.box);
            layerEl.append(layerImg);
            const it = { z: slot.z + (i + 1) * 0.0001, bone: slot.bone, el: layerEl, img: layerImg, file: null, shift: null, depth: slot.depth ?? 1 };
            items.push(it);
            return it;
          });
          preload(rig.face.layers.flatMap(l => Object.values(l.variants)).map(f => rig.base + f));
        }
      }
      if (slot.alt) {
        this.altSlots.push({ slot, img, current: null });
        preload(Object.values(slot.alt).map(a => rig.base + a.file));
      }
      // depth：轉頭視差，位移 = 臉的位移 × depth（沒寫時表情插槽算 1）
      items.push({ z: slot.z, bone: slot.bone, el, slot, img, mesh, tris, depth: slot.depth ?? (slot.variants ? 1 : 0) });
    }

    // 道具以 Q 版身材設計，依角色腿長等比放大
    const s = rig.rootScale ?? 1;
    this.propEls = {};
    for (const [name, p] of Object.entries(props)) {
      const el = document.createElementNS(NS, "g");
      el.append(makeImage(p.url, p.box));
      el.style.display = "none";
      const o = rig.propOverrides?.[name] ?? {};      // 角色個別的位置／大小修正
      const k = s * (o.scale ?? 1);
      items.push({ z: p.z, bone: p.bone, el, offset: [k, 0, 0, k, (p.x + (o.dx ?? 0)) * s, (p.y + (o.dy ?? 0)) * s] });
      this.propEls[name] = el;
    }

    items.sort((a, b) => a.z - b.z);
    for (const it of items) this.g.append(it.el);
    this.items = items;
    this.face = "idle";
    this.propsKey = "";
    if (this.faceLayers) this.showFace();
  }

  // 有分層臉、而且沒有被自訂表情（整張臉）蓋掉
  get layered() { return !!this.faceLayers && !Object.keys(this.customFaces?.images ?? {}).length; }

  // 分層臉：[{ name, file, alpha, dx, dy }]（game/faceDriver.js 算的，順序和 rig.face.layers 一樣）
  setFaceLayers(states) {
    this.faceLayers.forEach((it, i) => {
      const st = states[i];
      it.el.style.display = st?.file ? "" : "none";
      if (!st?.file) return;
      if (st.file !== it.file) {
        setImage(it.img, this.rig.base + st.file, this.rig.face.box);
        it.file = st.file;
      }
      it.el.style.opacity = st.alpha;
      it.shift = st.dx || st.dy ? [st.dx, st.dy] : null;
    });
  }

  setFace(name) {
    if (name === this.face) return;
    this.face = name;
    this.showFace();
  }

  // 自訂表情：{ box, images: { 表情: url } }，沒有的表情用 idle；null = 用角色原本的表情
  setFaceImages(faces) {
    this.customFaces = faces;
    this.showFace();
  }

  showFace() {
    if (!this.faceImg) return;        // 沒有臉的視角（背面）
    // 分層臉時整張臉藏起來；自訂表情會換回整張臉
    this.faceEl.style.display = this.layered ? "none" : "";
    for (const it of this.faceLayers ?? []) if (!this.layered) it.el.style.display = "none";
    const c = this.customFaces ?? this.rig.defaultFaces;      // 編輯器的自訂表情 > 部件包的表情 > 角色原本的
    if (c?.images && Object.keys(c.images).length) {
      setImage(this.faceImg, c.images[this.face] ?? c.images.idle ?? Object.values(c.images)[0], c.box);
      return;
    }
    const slot = this.rig.slots.find(s => s.variants);
    setImage(this.faceImg, this.rig.base + (this.faceVariants[this.face] ?? this.faceVariants.idle), slot.box);
  }

  // map: { 插槽名: 替換圖名 }；沒指定或角色沒有該替換圖時用原圖
  setAlt(map) {
    for (const a of this.altSlots) {
      const want = a.slot.alt[map[a.slot.name]] ? map[a.slot.name] : null;
      if (want === a.current) continue;
      if (want) setImage(a.img, resolve(this.rig, a.slot.alt[want].file), a.slot.alt[want].box);
      else this.restoreImage(a.slot, a.img);
      a.current = want;
    }
  }

  // 配件：[{ url, box, anchor, z }]，掛在角色的錨點上（rig.anchors），依錨點的 scale 縮放
  setAccessories(list) {
    for (const it of this.accItems ?? []) it.el.remove();
    this.items = this.items.filter(it => !it.accessory);
    this.accItems = [];
    for (const acc of list) {
      const a = this.rig.anchors?.[acc.anchor];
      if (!a) continue;                      // 這個角色沒有這個錨點
      const el = document.createElementNS(NS, "g");
      el.append(makeImage(acc.url, acc.box));
      const it = { z: acc.z, bone: a.bone, el, accessory: true, offset: [a.scale, 0, 0, a.scale, a.x, a.y], depth: a.depth ?? 0 };
      this.accItems.push(it);
      this.items.push(it);
    }
    this.items.sort((a, b) => a.z - b.z);
    for (const it of this.items) this.g.append(it.el);       // 依圖層順序重新排列
  }

  // 衣服：{ 部件圖名（torso、arm_u…）: { url, box } }，把同名部件換成自訂圖片；null = 換回原本的
  setOutfit(map) {
    this.outfit = map;
    for (const it of this.items) {
      if (!it.slot || it.slot.variants || !it.slot.file) continue;
      this.restoreImage(it.slot, it.img);
    }
    for (const a of this.altSlots) a.current = null;
  }

  // 部件的基本圖：有穿衣服就用衣服的圖，否則用角色原本的
  restoreImage(slot, img) {
    const art = slot.file.split("/").pop().replace(/\.svg$/, "");
    const part = this.outfit?.[art] ?? this.rig.defaultOutfit?.[art];     // 衣服 > 部件包的圖 > 角色原本的
    if (part) setImage(img, part.url, part.box);
    else setImage(img, this.rig.base + slot.file, slot.box);
  }

  setProps(list) {
    const key = list.join(",");
    if (key === this.propsKey) return;
    for (const [name, el] of Object.entries(this.propEls)) el.style.display = list.includes(name) ? "" : "none";
    this.propsKey = key;
  }

  // 臉部在頭部骨骼座標中的位移（看游標）；其他頭部圖層依 depth 跟著移
  setFaceOffset(offset) { this.faceOffset = offset; }

  draw(world, x, y, facing, scale) {
    const [fx, fy] = this.faceOffset ?? [0, 0];
    this.g.setAttribute("transform", `translate(${x} ${y}) scale(${facing * scale} ${scale})`);
    for (const it of this.items) {
      if (it.mesh) { this.drawMesh(it, world, fx * it.depth, fy * it.depth); continue; }
      let m = it.offset ? mul(world[it.bone], it.offset) : world[it.bone];
      if ((it.depth && (fx || fy)) || it.shift) {
        const [sx, sy] = it.shift ?? [0, 0];
        m = mul(m, [1, 0, 0, 1, fx * (it.depth ?? 0) + sx, fy * (it.depth ?? 0) + sy]);
      }
      it.el.setAttribute("transform", `matrix(${m.join(" ")})`);
    }
  }

  // 網格：算頂點，每個三角形設定裁切多邊形（往外擴一點）與圖片的仿射矩陣
  drawMesh(it, world, sx, sy) {
    const verts = it.mesh.compute(world).map(([x, y]) => [x + sx, y + sy]);
    it.verts = verts;
    it.mesh.triangles.forEach((t, k) => {
      const p = t.map(i => verts[i]);
      it.tris[k].poly.setAttribute("points", grow(p).map(([x, y]) => `${x},${y}`).join(" "));
      it.tris[k].img.setAttribute("transform", `matrix(${affine(t.map(i => it.mesh.uv[i]), p).join(" ")})`);
    });
  }

  destroy() {
    this.g.remove();
  }
}
