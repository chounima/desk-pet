// 控制面板：切換角色、手動播放動作、自動行為開關

function button(text, onClick, title = "") {
  const b = document.createElement("button");
  b.textContent = text;
  b.title = title;
  b.onclick = e => { e.stopPropagation(); onClick(); };
  return b;
}

export class Panel {
  constructor(root, { characters, actions, onCharacter, onForm, onAction, onAuto, onVolley }) {
    this.charButtons = {};
    this.actionButtons = {};
    this.formsRow = root.querySelector("[data-forms]");
    this.onForm = onForm;

    const chars = root.querySelector("[data-characters]");
    for (const rig of characters) {
      const b = button(rig.name, () => onCharacter(rig.id), rig.description);
      chars.append(b);
      this.charButtons[rig.id] = b;
    }

    const acts = root.querySelector("[data-actions]");
    for (const [name, clip] of actions) {
      if (clip.hidden) continue;      // 小遊戲專用等內部動作不放在面板上
      const b = button(clip.label, () => onAction(name), name);
      acts.append(b);
      this.actionButtons[name] = b;
    }

    const auto = root.querySelector("[data-auto]");
    auto.onchange = () => onAuto(auto.checked);

    this.volleyButton = button("🏐 排球對打", onVolley, "用游標揮球和她對打");
    root.querySelector("[data-games]").append(this.volleyButton);
  }

  setVolley(on) {
    this.volleyButton.classList.toggle("on", on);
    this.volleyButton.textContent = on ? "🏐 結束對打" : "🏐 排球對打";
  }

  setCharacter(id) {
    for (const [k, b] of Object.entries(this.charButtons)) b.classList.toggle("on", k === id);
  }

  // 型態按鈕：只有一種型態的角色不顯示
  setForms(forms, current) {
    const entries = Object.entries(forms ?? {});
    this.formsRow.hidden = entries.length < 2;
    this.formsRow.replaceChildren(...entries.map(([name, f]) => {
      const b = button(f.label, () => this.onForm(name));
      b.classList.toggle("on", name === current);
      return b;
    }));
  }

  setAction(name) {
    if (name === this.current) return;
    this.current = name;
    for (const [k, b] of Object.entries(this.actionButtons)) b.classList.toggle("on", k === name);
  }
}
