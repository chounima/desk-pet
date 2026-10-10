// 商店：用點數買商品，買了永久擁有；外觀類可以切換使用中的款式
// 商品與價格在 data/progression.json 的 shop；狀態存在 Progress 的存檔裡（owned、equipped）

export class Shop {
  constructor(progress) {
    this.progress = progress;
    const s = progress.state;
    s.owned ??= [];
    s.equipped ??= {};
    s.wear ??= {};          // 配件：{ 角色: { 部位: 商品 id } }
  }

  get cfg() { return this.progress.cfg.shop; }
  get state() { return this.progress.state; }

  // 依分類列出商品 [[id, item], ...]
  items(category) {
    return Object.entries(this.cfg.items).filter(([id, it]) => !id.startsWith("_") && (!category || it.category === category));
  }

  item(id) { return this.cfg.items[id]; }

  owns(id) {
    const it = this.item(id);
    return !!it && (it.price === 0 || this.state.owned.includes(id));
  }

  // 還沒達成購買條件時回傳 { char, have, need }
  requirement(id) {
    const a = this.item(id)?.requires?.affection;
    if (!a) return null;
    const have = this.progress.level(a.char).level;
    return have >= a.level ? null : { char: a.char, have, need: a.level };
  }

  // { ok, reason }：reason = owned | locked | points
  canBuy(id) {
    const it = this.item(id);
    if (!it) return { ok: false, reason: "missing" };
    if (this.owns(id)) return { ok: false, reason: "owned" };
    if (this.requirement(id)) return { ok: false, reason: "locked" };
    if (this.state.points < it.price) return { ok: false, reason: "points" };
    return { ok: true };
  }

  buy(id) {
    if (!this.canBuy(id).ok) return false;
    const it = this.item(id);
    this.state.points -= it.price;
    this.state.owned.push(id);
    this.progress.onEvent({ type: "buy", id, item: it });
    if (this.cfg.defaults[it.category]) this.state.equipped[it.category] = id;    // 外觀類買了直接換上
    this.progress.commit();
    return true;
  }

  // 外觀類：目前使用中的商品
  equipped(category) {
    const id = this.state.equipped[category];
    return this.owns(id) ? id : this.cfg.defaults[category];
  }

  equippedItem(category) { return this.item(this.equipped(category)); }

  equip(id) {
    const it = this.item(id);
    if (!it || !this.owns(id) || !this.cfg.defaults[it.category]) return false;
    this.state.equipped[it.category] = id;
    this.progress.onEvent({ type: "equip", id, item: it });
    this.progress.commit();
    return true;
  }

  // ---------- 配件（每個角色分開穿戴，同一部位一次一個）----------

  isWearable(id) { return !!this.item(id)?.accessory; }

  isWorn(id, char) { return this.state.wear[char]?.[this.item(id)?.slot] === id; }

  wear(id, char) {
    const it = this.item(id);
    if (!it?.accessory || !this.owns(id)) return false;
    (this.state.wear[char] ??= {})[it.slot] = id;
    this.progress.onEvent({ type: "wear", id, item: it, char });
    this.progress.commit();
    return true;
  }

  takeOff(id, char) {
    if (!this.isWorn(id, char)) return false;
    delete this.state.wear[char][this.item(id).slot];
    this.progress.onEvent({ type: "wear", id, item: this.item(id), char });
    this.progress.commit();
    return true;
  }

  // 角色身上的配件商品 id
  worn(char) {
    return Object.values(this.state.wear[char] ?? {}).filter(id => this.owns(id));
  }

  // 動作是否可以使用：不是商品的動作一律可以
  actionOwned(name) {
    const entry = this.items("action").find(([, it]) => it.action === name);
    return !entry || this.owns(entry[0]);
  }

  // 動作名 → 商品 id（不是商品時為 null）
  actionItem(name) {
    return this.items("action").find(([, it]) => it.action === name)?.[0] ?? null;
  }
}
