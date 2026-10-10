// 商店視窗：分類分頁、商品卡片（預覽、價格、鎖定條件）、購買與切換使用中的款式

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

export class ShopWindow {
  // props：art/props/props.json（含 url）；names(id)：角色名稱；onPreview(id, item)：在角色身上預覽
  constructor(root, { shop, props, names, onPreview }) {
    this.root = root;
    this.shop = shop;
    this.props = props;
    this.names = names;
    this.onPreview = onPreview;
    this.tab = Object.keys(shop.cfg.categories)[0];
    root.querySelector("[data-close]").onclick = () => this.toggle(false);
    root.addEventListener("pointerdown", e => e.stopPropagation());
  }

  get open() { return !this.root.hidden; }

  toggle(on = !this.open, tab) {
    if (tab) this.tab = tab;
    this.root.hidden = !on;
    if (on) this.render();
  }

  render() {
    if (!this.open) return;
    const shop = this.shop;
    this.root.querySelector("[data-points]").textContent = `💎 ${shop.state.points}`;

    const tabs = this.root.querySelector("[data-tabs]");
    tabs.replaceChildren(...Object.entries(shop.cfg.categories).map(([cat, label]) => {
      const b = el("button", cat === this.tab ? "on" : "", label);
      b.onclick = () => { this.tab = cat; this.render(); };
      return b;
    }));

    const grid = this.root.querySelector("[data-items]");
    grid.replaceChildren(...shop.items(this.tab).map(([id, it]) => this.card(id, it)));
  }

  preview(it) {
    const box = el("div", "preview");
    if (it.prop && this.props[it.prop]) {
      const img = el("img");
      img.src = this.props[it.prop].url;
      box.append(img);
    } else if (it.symbol) {
      box.append(el("span", "symbol", it.symbol));
      box.lastChild.style.color = it.color;
    } else if (it.style) {
      box.append(el("span", `bubble sample style-${it.style}`, "嗨！"));
    } else {
      box.append(el("span", "symbol", it.icon ?? "🎁"));
    }
    return box;
  }

  card(id, it) {
    const shop = this.shop;
    const owned = shop.owns(id);
    const equipped = shop.cfg.defaults[it.category] && shop.equipped(it.category) === id;
    const card = el("div", `item${owned ? " owned" : ""}${equipped ? " equipped" : ""}`);
    card.append(this.preview(it), el("b", "", it.label));

    const actions = el("div", "item-actions");
    const btn = (text, fn, cls = "") => {
      const b = el("button", cls, text);
      b.onclick = () => { fn(); this.render(); };
      actions.append(b);
      return b;
    };
    if (owned) {
      if (equipped) actions.append(el("span", "tag", "使用中"));
      else if (shop.cfg.defaults[it.category]) btn("使用", () => shop.equip(id));
      else actions.append(el("span", "tag", "✓ 已擁有"));
    } else {
      const check = shop.canBuy(id);
      const req = shop.requirement(id);
      if (req) actions.append(el("span", "muted", `🔒 ${this.names(req.char)} ❤ Lv${req.have} / ${req.need}`));
      else {
        const b = btn(`購買 💎 ${it.price}`, () => shop.buy(id), "buy");
        b.disabled = !check.ok;
        if (check.reason === "points") b.title = `還差 ${it.price - shop.state.points} 點`;
      }
    }
    btn("預覽", () => this.onPreview(id, it), "ghost");
    card.append(actions);
    return card;
  }
}
