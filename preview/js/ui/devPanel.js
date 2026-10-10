// 開發者模式（網頁 ?dev=1）：即時修改收集系統的參數、作弊按鈕
// 修改後的參數暫存在 localStorage（OVERRIDE_KEY），按「還原預設」回到 data/progression.json

export const OVERRIDE_KEY = "deskpet.progression.override";

export class DevPanel {
  // defaults：data/progression.json 原始內容；getChar：目前角色 id；onChange：參數或進度改變後要重畫
  constructor(root, { progress, defaults, getChar, onChange }) {
    root.hidden = false;
    root.querySelector("header").onclick = () => root.classList.toggle("collapsed");   // 點標題收合，免得擋到角色
    root.addEventListener("pointerdown", e => e.stopPropagation());
    root.addEventListener("keydown", e => e.stopPropagation());     // 在文字框打字不算打字點數
    const text = root.querySelector("[data-cfg]");
    const status = root.querySelector("[data-status]");
    const show = msg => { status.textContent = msg; };
    text.value = JSON.stringify(progress.cfg, null, 2);

    const actions = {
      points: () => { progress.addPoints(100, "dev"); progress.commit(); },
      xp: () => { progress.addXp(getChar(), 50); progress.commit(); },
      nextDay: () => {
        // 把存檔裡的日期都往前推一天，下一次 checkDay 就會當作換日（測試每日上限與連續登入）
        const s = progress.state, back = d => d && new Date(Date.parse(d) - 86400000).toLocaleDateString("sv");
        s.day.date = back(s.day.date);
        s.login.last = back(s.login.last);
        progress.checkDay();
      },
      reset: () => {
        if (!confirm("清除所有點數、好感度與解鎖？")) return;
        progress.resetState();
      },
      apply: () => {
        try {
          const cfg = JSON.parse(text.value);
          progress.cfg = cfg;
          localStorage.setItem(OVERRIDE_KEY, text.value);
          progress.checkUnlocks();
          progress.commit();
          show("已套用（暫存在瀏覽器）");
        } catch (e) {
          show(`JSON 格式錯誤：${e.message}`);
          return;
        }
      },
      restore: () => {
        localStorage.removeItem(OVERRIDE_KEY);
        progress.cfg = structuredClone(defaults);
        text.value = JSON.stringify(defaults, null, 2);
        show("已還原成 data/progression.json");
      },
    };
    for (const [name, fn] of Object.entries(actions))
      root.querySelector(`[data-dev="${name}"]`).onclick = () => { fn(); onChange(); };
  }
}
