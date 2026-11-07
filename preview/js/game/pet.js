// 寵物本體：位置、朝向、骨架、動作、表情，負責把這些組合後畫出來
import { Animator, sampleTrack } from "../core/animator.js";
import { Spring } from "../core/spring.js";
import { Overlay } from "../core/overlay.js";
import { MOTION } from "../config.js";
import { apply } from "../core/math.js";
import { Skeleton } from "../core/skeleton.js";
import { PetView } from "../view/renderer.js";
import { faceLayers } from "./faceDriver.js";

export class Pet {
  // clipSets：{ 體型: { 動作名: clip } }，見 loader.loadAnimations
  constructor({ layer, rig, clipSets, props, scale, crossfade }) {
    this.layer = layer;
    this.props = props;
    this.scale = scale;
    this.x = 0;
    this.y = 0;
    this.facing = 1;              // 1 = 朝右，-1 = 朝左
    this.faceOverride = null;     // 由行為邏輯暫時指定的表情
    this.mouthShape = null;       // 嘴巴（講話：closed、half、open、母音），分層臉換嘴型
    this.look = null;             // 視線 [x, y] -1～1，分層臉的瞳孔跟著看
    this.blinkTimer = 2;
    this.blinkLeft = 0;
    this.clipSets = clipSets;
    this.animator = new Animator(clipSets.humanoid, crossfade);
    this.animator.play("idle");
    this.setCharacter(rig);
  }

  setCharacter(rig) {
    this.view?.destroy();
    this.rig = rig;
    // 換體型時換一套動作，並用新的姿勢重播目前的動作
    // 動作組：角色專屬（sets.for）＞ 體型 ＞ 共用
    const clips = this.clipSets.for?.(rig) ?? this.clipSets[rig.body ?? "humanoid"] ?? this.clipSets.humanoid;
    if (clips !== this.animator.clips) {
      this.animator.clips = clips;
      this.animator.play(this.animator.name ?? "idle", true);
    }
    this.probeCache?.clear();
    this.skeleton = new Skeleton(rig.bones, rig.rootScale ?? 1, rig.trackScale ?? {});
    this.motion = new Spring(rig.bones, rig.physics);          // 次級物理（尾巴、披布…）
    this.world = this.skeleton.compute(this.animator.pose);   // 第一幀前就能查詢骨骼位置
    this.view = new PetView(this.layer, rig, this.props);
    this.view.setAccessories(this.accessories ?? []);
    this.view.setOutfit(this.outfit ?? null);
    this.view.setFaceImages(this.faces ?? null);
  }

  // 自訂表情 { box, images: { 表情: url } }（自訂角色編輯器用）
  setFaceImages(faces) {
    this.faces = faces;
    this.view.setFaceImages(faces);
  }

  // 衣服 { 部件圖名: { url, box } }；換角色時會重新套用（不同角色通常要換掉）
  setOutfit(map) {
    this.outfit = map;
    this.view.setOutfit(map);
  }

  // 配件 [{ url, box, anchor, z }]；換角色時會重新套用
  setAccessories(list) {
    this.accessories = list;
    this.view.setAccessories(list);
  }

  play(name, restart = false, mods) { this.animator.play(name, restart, mods); }
  get action() { return this.animator.name; }
  // 目前動作的移動速度（依腿長放大）
  get speed() { return this.animator.move * (this.rig.rootScale ?? 1); }

  // 次級物理歸零：瞬間移動、換大小時呼叫
  resetPhysics() { this.motion.reset(); }

  update(dt) {
    const pose = this.animator.update(dt);
    (this.overlay ??= new Overlay(MOTION)).apply(pose, this.animator.name, dt);     // 呼吸、重心晃動（data/motion.json）
    this.world = this.skeleton.compute(pose);
    // 次級物理：用「只有動畫」的姿勢算偏移，再重算一次骨架（物理骨不參與 probe）
    if (this.motion.active) this.world = this.skeleton.compute(pose, this.motion.update(dt, this.world, this.x, this.y, this.scale, this.facing));
    const { face, blink } = this.currentFace(dt);
    if (this.view.layered) this.view.setFaceLayers(faceLayers(this.rig.face, { expression: face, blink, mouth: this.mouthShape, look: this.look }));
    else this.view.setFace(blink && (face === "idle" || face === "focus") ? "blink" : face);
    this.view.setProps(this.animator.props);
    this.view.setAlt(this.animator.alt);
    this.view.draw(this.world, this.x, this.y, this.facing, this.scale);
  }

  // 優先使用 override，其次是動作指定的表情；另外回傳這一刻是不是在眨眼（整張臉只有 idle、focus 會眨，分層臉睜著眼就會眨）
  currentFace(dt) {
    const face = this.faceOverride ?? this.animator.face;
    this.blinkTimer -= dt;
    if (this.blinkTimer <= 0) {
      this.blinkLeft = 0.12;
      this.blinkTimer = 2.5 + Math.random() * 3;
    }
    const blink = this.blinkLeft > 0;
    if (blink) this.blinkLeft -= dt;
    return { face, blink };
  }

  // 探測：動作 name 播到 t 秒時，某根骨骼上的點相對腳底的螢幕位移（dx 以面向前方為正）
  // 用來預先知道殺球、魚躍時手會在哪裡
  probe(name, t, bone, lx = 0, ly = 0, mods = {}) {
    const key = `${this.rig.id}|${name}|${t}|${bone}|${lx}|${ly}|${this.scale}|${JSON.stringify(mods)}`;
    this.probeCache ??= new Map();
    if (!this.probeCache.has(key)) {
      const clip = this.animator.clips[name];
      const pose = {};
      for (const ch in clip.tracks) pose[ch] = sampleTrack(clip.tracks[ch], t) * (mods[ch] ?? 1);
      const world = new Skeleton(this.rig.bones, this.rig.rootScale ?? 1, this.rig.trackScale ?? {}).compute(pose);
      const [px, py] = apply(world[bone], lx, ly);
      this.probeCache.set(key, { dx: px * this.scale, dy: py * this.scale });
    }
    return this.probeCache.get(key);
  }

  // 視線：看著畫面上的點（null = 不看），分層臉的瞳孔慢慢轉過去（和桌寵 Gaze 一樣，離臉 300 px 算看到底）
  lookAt(point, dt) {
    let target = [0, 0];
    if (point) {
      const [hx, hy] = this.rig.headTop ?? [6, -110];
      const [px, py] = apply(this.world.head, hx * 0.5, hy * 0.5);
      const clamp = v => Math.max(-1, Math.min(1, v));
      target = [clamp((point.x - this.x - px * this.facing * this.scale) * this.facing / 300), clamp((point.y - this.y - py * this.scale) / 300)];
    }
    const [lx, ly] = this.look ?? [0, 0], k = Math.min(1, dt * 12);
    this.look = [lx + (target[0] - lx) * k, ly + (target[1] - ly) * k];
  }

  // 頭頂在螢幕上的位置（特效錨點）
  headTop() {
    const [hx, hy] = this.rig.headTop ?? [6, -110];
    const [px, py] = apply(this.world.head, hx, hy);
    return { x: this.x + px * this.facing * this.scale, y: this.y + py * this.scale };
  }
}
