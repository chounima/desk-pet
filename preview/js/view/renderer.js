// 把角色部件畫成 SVG <image>，每幀套用骨骼矩陣
import { mul } from "../core/math.js";

const NS = "http://www.w3.org/2000/svg";

function setImage(img, href, [x0, y0, x1, y1]) {
  img.setAttribute("href", href);
  img.setAttribute("x", x0);
  img.setAttribute("y", y0);
  img.setAttribute("width", x1 - x0);
  img.setAttribute("height", y1 - y0);
}

function makeImage(href, box) {
  const img = document.createElementNS(NS, "image");
  setImage(img, href, box);
  return img;
}

function preload(urls) {
  for (const url of urls) new Image().src = url;
}

export class PetView {
  constructor(layer, rig, props) {
    this.rig = rig;
    this.g = document.createElementNS(NS, "g");
    this.g.classList.add("pet");
    layer.append(this.g);

    const items = [];
    this.altSlots = [];       // 有替換圖的插槽
    for (const slot of rig.slots) {
      const el = document.createElementNS(NS, "g");
      const img = makeImage(rig.base + (slot.variants ? slot.variants.idle : slot.file), slot.box);
      el.append(img);
      if (slot.far) el.classList.add("far");
      if (slot.variants) {
        this.faceImg = img;
        this.faceVariants = slot.variants;
        preload(Object.values(slot.variants).map(f => rig.base + f));
      }
      if (slot.alt) {
        this.altSlots.push({ slot, img, current: null });
        preload(Object.values(slot.alt).map(a => rig.base + a.file));
      }
      items.push({ z: slot.z, bone: slot.bone, el });
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
  }

  setFace(name) {
    if (name === this.face) return;
    const file = this.faceVariants[name] ?? this.faceVariants.idle;
    this.faceImg.setAttribute("href", this.rig.base + file);
    this.face = name;
  }

  // map: { 插槽名: 替換圖名 }；沒指定或角色沒有該替換圖時用原圖
  setAlt(map) {
    for (const a of this.altSlots) {
      const want = a.slot.alt[map[a.slot.name]] ? map[a.slot.name] : null;
      if (want === a.current) continue;
      const src = want ? a.slot.alt[want] : a.slot;
      setImage(a.img, this.rig.base + src.file, src.box);
      a.current = want;
    }
  }

  setProps(list) {
    const key = list.join(",");
    if (key === this.propsKey) return;
    for (const [name, el] of Object.entries(this.propEls)) el.style.display = list.includes(name) ? "" : "none";
    this.propsKey = key;
  }

  draw(world, x, y, facing, scale) {
    this.g.setAttribute("transform", `translate(${x} ${y}) scale(${facing * scale} ${scale})`);
    for (const it of this.items) {
      const m = it.offset ? mul(world[it.bone], it.offset) : world[it.bone];
      it.el.setAttribute("transform", `matrix(${m.join(" ")})`);
    }
  }

  destroy() {
    this.g.remove();
  }
}
