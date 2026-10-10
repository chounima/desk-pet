// 收藏視窗：點數、今日進度、每個角色的好感度與解鎖狀態

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

export class Collection {
  // rigs：{ id: rig }（含 forms），ids：角色順序
  constructor(root, { progress, ids, rigs }) {
    this.root = root;
    this.progress = progress;
    this.ids = ids;
    this.rigs = rigs;
    root.querySelector("[data-close]").onclick = () => this.toggle(false);
    root.addEventListener("pointerdown", e => e.stopPropagation());
  }

  get open() { return !this.root.hidden; }

  toggle(on = !this.open) {
    this.root.hidden = !on;
    if (on) this.render();
  }

  name(id) { return this.rigs[id]?.name ?? id; }

  // 解鎖條件的文字，例如「Chouma ❤ Lv1 / 3」
  requirementText(key) {
    const r = this.progress.requirement(key);
    if (!r) return "";
    return r.cost ? `💎 ${r.cost}` : `${this.name(r.char)} ❤ Lv${r.have} / ${r.need}`;
  }

  render() {
    if (!this.open) return;
    const p = this.progress, s = p.state, cfg = p.cfg, d = s.day;
    this.root.querySelector("[data-points]").textContent = `💎 ${s.points}　🔥 連續登入 ${s.login.streak} 天`;

    const P = cfg.points;
    const today = this.root.querySelector("[data-today]");
    today.replaceChildren(...[
      ["⌨️ 打字", d.pts.typing, P.typing.dailyMax],
      ["👆 互動", d.pts.interaction, P.interaction.dailyMax],
      ["🏐 排球", d.pts.volley, P.volley.dailyMax],
    ].map(([label, v, max]) => {
      const row = el("div", "today-row");
      row.append(el("span", "", label), this.bar(v / max), el("span", "num", `${v} / ${max}`));
      return row;
    }));

    const list = this.root.querySelector("[data-chars]");
    list.replaceChildren(...this.ids.map(id => this.card(id)));
  }

  bar(ratio) {
    const b = el("span", "bar");
    const fill = el("span", "fill");
    fill.style.width = `${Math.round(Math.min(1, Math.max(0, ratio)) * 100)}%`;
    b.append(fill);
    return b;
  }

  card(id) {
    const p = this.progress, rig = this.rigs[id];
    const unlocked = p.characterUnlocked(id);
    const card = el("div", `card${unlocked ? "" : " locked"}`);
    const head = el("div", "card-head");
    head.append(el("b", "", unlocked ? rig.name : "？？？"), el("span", "muted", unlocked ? rig.description : ""));
    card.append(head);

    if (!unlocked) {
      const key = `character:${id}`;
      card.append(el("div", "hint", `🔒 ${p.cfg.unlocks[key]?.hint ?? "尚未解鎖"}`), el("div", "num", this.requirementText(key)));
      return card;
    }

    const lv = p.level(id);
    const row = el("div", "today-row");
    const ratio = lv.to === null ? 1 : (lv.xp - lv.from) / (lv.to - lv.from);
    row.append(el("span", "", `❤ Lv${lv.level}`), this.bar(ratio),
      el("span", "num", lv.to === null ? "MAX" : `${lv.xp} / ${lv.to}`));
    card.append(row);

    const best = p.state.best[id];
    if (best) card.append(el("div", "muted", `🏐 最佳來回 ${best}`));

    const forms = Object.entries(rig.forms ?? {});
    if (forms.length > 1) {
      const line = el("div", "forms");
      for (const [name, f] of forms) {
        const ok = p.formUnlocked(id, name, forms[0][0]);
        const tag = el("span", `tag${ok ? "" : " locked"}`, `${ok ? "✓" : "🔒"} ${f.label}`);
        if (!ok) tag.title = `${p.cfg.unlocks[`form:${id}/${name}`]?.hint ?? ""}（${this.requirementText(`form:${id}/${name}`)}）`;
        line.append(tag);
      }
      card.append(line);
    }
    return card;
  }
}
