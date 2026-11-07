// 路徑與調整參數（純資料）

// 以本檔位置為基準，任何頁面（含 tests/）載入都正確
export const PATHS = {
  art: new URL("../../art", import.meta.url).href,
  data: new URL("../../data", import.meta.url).href,
};

// 調整參數：data/tuning.json（桌寵 Godot 版讀同一份）。模組最上層 await，匯入本檔的模組會等它讀完
const tuning = await (await fetch(`${PATHS.data}/tuning.json`)).json();
const omitNotes = obj => Object.fromEntries(Object.entries(obj).filter(([k]) => !k.startsWith("_")));

export const TUNING = {
  ...omitNotes(tuning.pet),
  taskbar: 48,         // 假工作列高度 (px)，只有網頁預覽用
};

// 自動行為：權重越高越常出現；time = 循環動作持續秒數範圍。
// needsWindows（跳上／跳下桌面視窗）網頁預覽沒有視窗可以跳，略過
export const AUTO = Object.fromEntries(Object.entries(tuning.auto).filter(([, v]) => !v.needsWindows));

// 排球對打小遊戲（長度單位：螢幕 px；reach / hitHeight 為 Q 版單位，會依角色身材放大）
export const VOLLEY = omitNotes(tuning.volley);

// 點擊角色時隨機挑一個反應（TODO：桌寵已改用 data/interactions.json 的部位與連擊規則，網頁還沒同步）
export const CLICK_REACTIONS = ["wave", "cheer", "peace", "shy"];
