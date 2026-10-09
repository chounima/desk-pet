// 對打小遊戲的球（獨立於角色骨架，畫在世界圖層上）
const NS = "http://www.w3.org/2000/svg";

export class BallView {
  // prop: props.json 裡的 volleyball；artRadius: 圖檔裡球的半徑
  constructor(layer, prop, artRadius, onClick) {
    this.artRadius = artRadius;
    this.g = document.createElementNS(NS, "g");
    this.g.classList.add("ball");
    const [x0, y0, x1, y1] = prop.box;
    const img = document.createElementNS(NS, "image");
    Object.entries({ href: prop.url, x: x0, y: y0, width: x1 - x0, height: y1 - y0 })
      .forEach(([k, v]) => img.setAttribute(k, v));
    this.g.append(img);
    this.g.addEventListener("pointerdown", e => { e.stopPropagation(); onClick(); });
    this.hide();
    layer.append(this.g);
  }

  show() { this.g.style.display = ""; }
  hide() { this.g.style.display = "none"; }
  setFire(on) { this.g.classList.toggle("fire", on); }

  draw(b) {
    const s = b.r / this.artRadius;
    this.g.setAttribute("transform", `translate(${b.x} ${b.y}) rotate(${b.rot}) scale(${s})`);
  }
}
