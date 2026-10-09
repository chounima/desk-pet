// 路徑與調整參數（純資料）

// 以本檔位置為基準，任何頁面（含 tests/）載入都正確
export const PATHS = {
  art: new URL("../../art", import.meta.url).href,
  data: new URL("../../data", import.meta.url).href,
};

export const TUNING = {
  scale: 0.8,          // 角色縮放
  gravity: 2400,       // 掉落加速度 (px/s²)
  crossfade: 0.18,     // 動作切換的混合時間 (s)
  sleepAfter: 30,      // 多久沒輸入就睡著 (s)
  typingHold: 2.5,     // 停止打字後多久收起筆電 (s)
  focusRate: 6,        // 每秒幾下進入專注表情
  taskbar: 48,         // 假工作列高度 (px)
  margin: 80,          // 走路時離螢幕邊緣的距離 (px)
};

// 自動行為：權重越高越常出現；time = 循環動作持續秒數範圍
export const AUTO = {
  idle: { weight: 3, time: [2, 4] },
  walk: { weight: 5 },
  run: { weight: 1 },
  sit: { weight: 2, time: [5, 9] },
  sitEdge: { weight: 1, time: [5, 9] },
  think: { weight: 1, time: [3, 5] },
  wave: { weight: 1, time: [2, 3] },
  dance: { weight: 1, time: [3, 5] },
  stretch: { weight: 1 },
  jump: { weight: 1 },
  peace: { weight: 1, time: [2, 3] },
  volleyball: { weight: 1, time: [4, 7] },
};

// 排球對打小遊戲（長度單位：螢幕 px；reach / hitHeight 為 Q 版單位，會依角色身材放大）
export const VOLLEY = {
  gravity: 900,         // 球的重力，比真實小，比較好接
  ballRadius: 26,       // 球的顯示半徑
  artRadius: 14,        // 排球圖檔裡的半徑（= tools/artgen/props.py 的 BALL_R）
  reach: 44,            // 擊球點在身體前方多遠
  hitHeight: 104,       // 擊球點離地多高
  hitTolerance: 34,     // 球離擊球點多近算接到
  runSpeed: 420,        // 跑去接球的速度
  reactDelay: 0.2,      // 看到球飛來後多久開始跑
  arc: [150, 280],      // 回球的拋物線高度（高於起點與終點）
  maxVx: 1200,
  cursorRadius: 20,     // 游標擊球的判定半徑
  minSwipe: 150,        // 游標至少要這麼快才算揮擊
  serveToss: 620,       // 發球時往上拋的初速
  miss: { base: 0.03, perRally: 0.012, max: 0.3 },   // 漏接機率，來回越多次越高（再乘上角色的 miss）
  restartAfter: 2.2,    // 一回合結束後多久重新發球
  lobArc: [200, 400],   // 玩家點球、救球時的高吊球
  digSpeed: 650,        // 球速超過這個值時，游標碰到就算接起（防守殺球用）

  // 殺球：三步助跑（vbApproach）→ 起跳揮臂（vbSpike）
  approachTime: 0.6,    // = vbApproach.json 的 duration
  approachDist: 70,     // 助跑距離（Q 版單位）
  spikeTimes: [0.35, 0.3, 0.4, 0.45],   // vbSpike 揮臂過程中可擊球的時刻（依偏好排序，0.35 = 手最高）
  spikeSpeed: 1100,     // 殺球球速（再乘上角色的 power）

  // 魚躍救球（vbDive）
  diveContact: 0.35,    // vbDive 開始後幾秒手碰到球
  diveSlide: 70,        // 撲出時身體往前衝的距離（Q 版單位），在碰到球之前完成
  contactWindow: 0.08,  // 擊球時間點前後的容許誤差 (s)

  fireAt: 10,           // 連續來回幾次後球燒起來
};

// 點擊角色時隨機挑一個反應
export const CLICK_REACTIONS = ["wave", "cheer", "peace", "shy"];
