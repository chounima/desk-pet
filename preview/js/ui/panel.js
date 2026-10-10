// 控制面板：切換角色、手動播放動作、自動行為開關

function button(text, onClick, title = "") {
  const b = document.createElement("button");
  b.textContent = text;
  b.title = title;
  b.onclick = e => { e.stopPropagation(); onClick(); };
  return b;
}

export class Panel {
  constructor(root, { characters, actions, onCharacter, onForm, onAction, onAuto, onVolley, onCollection }) {
    this.charButtons = {};
    this.actionButtons = {};
    this.formsRow = root.querySelector("[data-forms]");
    this.onForm = onForm;

    const chars = root.querySelector("[data-characters]");
    for (const rig of characters) {
      const b = button(rig.name, () => { if (!b.dataset.hint) onCharacter(rig.id); }, rig.description);
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
    root.querySelector("[data-collection]").append(button("📖 收藏", onCollection, "點數、好感度、解鎖"));
  }

  setVolley(on) {
    this.volleyButton.classList.toggle("on", on);
    this.volleyButton.textContent = on ? "🏐 結束對打" : "🏐 排球對打";
  }

  setCharacter(id) {
    for (const [k, b] of Object.entries(this.charButtons)) b.classList.toggle("on", k === id);
  }

  // 型態按鈕：只有一種型態的角色不顯示；lockedHint(name) 回傳文字表示鎖住
  setForms(forms, current, lockedHint = () => null) {
    const entries = Object.entries(forms ?? {});
    this.formsRow.hidden = entries.length < 2;
    this.formsRow.replaceChildren(...entries.map(([name, f]) => {
      const hint = lockedHint(name);
      const b = button(hint ? `🔒 ${f.label}` : f.label, () => { if (!hint) this.onForm(name); }, hint ?? "");
      b.classList.toggle("on", name === current);
      b.classList.toggle("locked", !!hint);
      return b;
    }));
  }

  // 角色按鈕的鎖定狀態：lockedHint(id) 回傳文字表示鎖住
  setLocks(lockedHint) {
    for (const [id, b] of Object.entries(this.charButtons)) {
      const hint = lockedHint(id);
      b.classList.toggle("locked", !!hint);
      b.dataset.hint = hint ?? "";
      if (hint) b.title = hint;
    }
  }

  setAction(name) {
    if (name === this.current) return;
    this.current = name;
    for (const [k, b] of Object.entries(this.actionButtons)) b.classList.toggle("on", k === name);
  }
}
