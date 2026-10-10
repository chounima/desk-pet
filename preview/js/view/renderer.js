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
      items.push({ z: slot.z, bone: slot.bone, el, slot, img });
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
      if (want) setImage(a.img, this.rig.base + a.slot.alt[want].file, a.slot.alt[want].box);
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
      const it = { z: acc.z, bone: a.bone, el, accessory: true, offset: [a.scale, 0, 0, a.scale, a.x, a.y] };
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
    const part = this.outfit?.[art];
    if (part) setImage(img, part.url, part.box);
    else setImage(img, this.rig.base + slot.file, slot.box);
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
