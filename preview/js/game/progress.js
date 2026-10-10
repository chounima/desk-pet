// 收集系統：點數 💎、好感度 ❤、解鎖。所有數值來自 data/progression.json（cfg）
// 只負責規則與狀態，不碰畫面；存檔方式由外部提供（網頁用 localStorage，Godot 用 user://）
// 事件透過 onEvent 通知：{ type: "points", amount, reason } / { type: "levelUp", char, level } / { type: "unlock", key }

const today = () => new Date().toLocaleDateString("sv");     // YYYY-MM-DD（本地時間）

function emptyDay(date) {
  return { date, keys: 0, pts: { typing: 0, interaction: 0, volley: 0 }, xp: { companion: {}, interaction: {}, volley: {} } };
}

export function emptyState() {
  return { points: 0, xp: {}, unlocked: [], owned: [], equipped: {}, wear: {}, best: {}, login: { last: null, streak: 0 }, day: emptyDay(today()) };
}

export class Progress {
  constructor(cfg, state, save) {
    this.cfg = cfg;
    this.state = { ...emptyState(), ...state };
    this.save = save;
    this.onEvent = () => {};
    this.companion = 0;          // 累積的陪伴秒數（滿一分鐘才算）
  }

  // ---------- 每日 ----------

  // 換日時重置每日上限，並發每日登入獎勵；啟動時與每次計算前都會呼叫
  checkDay(date = today()) {
    const s = this.state;
    if (s.day.date === date && s.login.last === date) return;
    if (s.day.date !== date) s.day = emptyDay(date);
    if (s.login.last !== date) {
      const yesterday = new Date(Date.parse(date) - 86400000).toLocaleDateString("sv");
      s.login.streak = s.login.last === yesterday ? s.login.streak + 1 : 1;
      s.login.last = date;
      const L = this.cfg.points.login;
      this.addPoints(L.base + Math.min(L.maxBonus, L.streakBonus * (s.login.streak - 1)), "login");
    }
    this.commit();
  }

  // ---------- 點數 ----------

  addPoints(amount, reason) {
    if (amount <= 0) return;
    this.state.points += amount;
    this.onEvent({ type: "points", amount, reason });
  }

  // 有每日上限的點數：回傳實際給了多少
  capped(kind, amount, max) {
    const pts = this.state.day.pts;
    const give = Math.max(0, Math.min(amount, max - pts[kind]));
    pts[kind] += give;
    this.addPoints(give, kind);
    return give;
  }

  // 只計次數，不記錄按了哪個鍵
  onKey(char, n = 1) {
    this.checkDay();
    const T = this.cfg.points.typing, d = this.state.day;
    d.keys += n;
    const earned = Math.floor(d.keys / T.every) * T.gain;
    const given = d.pts.typing;
    if (earned > given) this.capped("typing", earned - given, T.dailyMax);
    this.commit();
  }

  onInteraction(char) {
    this.checkDay();
    const P = this.cfg.points.interaction, A = this.cfg.affection;
    this.capped("interaction", P.gain, P.dailyMax);
    this.addXp(char, A.interaction, "interaction", A.interactionDailyMax);
    this.commit();
  }

  // 排球每次來回數變化時呼叫（rally = 目前連續來回數）
  onVolley(char, rally) {
    this.checkDay();
    const P = this.cfg.points.volley, A = this.cfg.affection;
    if (rally > 0 && rally % P.everyRally === 0) this.capped("volley", P.gain, P.dailyMax);
    if (rally > 0 && rally % A.volleyEveryRally === 0) this.addXp(char, A.volleyGain, "volley", A.volleyDailyMax);
    const best = this.state.best[char] ?? 0;
    // 新的一局（來回數歸零重來或換角色）：記下開局時的紀錄，整局只在超過它的那一球給一次獎勵
    if (rally <= 1 || this.volleyChar !== char) {
      this.volleyChar = char;
      this.roundBest = best;
    }
    if (rally > best) this.state.best[char] = rally;
    // 第一次玩（沒有舊紀錄）不算破紀錄
    if (this.roundBest > 0 && rally === this.roundBest + 1) this.addPoints(P.recordBonus, "record");
    this.commit();
  }

  // 陪伴時間：目前顯示的角色每分鐘加好感度
  tick(dt, char) {
    this.companion += dt;
    if (this.companion < 60) return;
    this.companion -= 60;
    this.checkDay();
    const A = this.cfg.affection;
    this.addXp(char, A.companionPerMinute, "companion", A.companionDailyMax);
    this.commit();
  }

  // ---------- 好感度 ----------

  addXp(char, amount, kind, dailyMax = Infinity) {
    if (kind) {
      const used = this.state.day.xp[kind];
      amount = Math.max(0, Math.min(amount, dailyMax - (used[char] ?? 0)));
      used[char] = (used[char] ?? 0) + amount;
    }
    if (amount <= 0) return;
    const before = this.level(char).level;
    this.state.xp[char] = (this.state.xp[char] ?? 0) + amount;
    const after = this.level(char).level;
    for (let lv = before + 1; lv <= after; lv++) {
      this.onEvent({ type: "levelUp", char, level: lv });
      this.addPoints(this.cfg.points.levelUp, "levelUp");
    }
    if (after > before) this.checkUnlocks();
  }

  // { level, xp, from, to }：to = 下一級門檻（滿級時為 null）
  level(char) {
    const xp = this.state.xp[char] ?? 0, L = this.cfg.affection.levels;
    let level = 0;
    while (level < L.length && xp >= L[level]) level++;
    return { level, xp, from: L[level - 1] ?? 0, to: L[level] ?? null };
  }

  // ---------- 解鎖 ----------

  // 條件達成就自動解鎖（cost 類型要自己買，不會自動解鎖）
  checkUnlocks() {
    for (const [key, rule] of this.rules()) {
      if (this.state.unlocked.includes(key) || !rule.affection) continue;
      if (this.level(rule.affection.char).level >= rule.affection.level) {
        this.state.unlocked.push(key);
        this.onEvent({ type: "unlock", key });
      }
    }
  }

  buy(key) {
    const rule = this.cfg.unlocks[key];
    if (!rule?.cost || this.isUnlocked(key) || this.state.points < rule.cost) return false;
    this.state.points -= rule.cost;
    this.state.unlocked.push(key);
    this.onEvent({ type: "unlock", key });
    this.commit();
    return true;
  }

  rules() {
    return Object.entries(this.cfg.unlocks).filter(([k]) => !k.startsWith("_"));
  }

  // 沒有列在 unlocks 裡的東西一律開放；角色另外看 start.characters
  isUnlocked(key) {
    if (this.state.unlocked.includes(key)) return true;
    if (key.startsWith("character:")) return this.cfg.start.characters.includes(key.slice(10)) || !(key in this.cfg.unlocks);
    return !(key in this.cfg.unlocks);
  }

  characterUnlocked(id) { return this.isUnlocked(`character:${id}`); }
  // defaultForm：角色的預設型態一律開放
  formUnlocked(id, form, defaultForm) { return form === defaultForm || this.isUnlocked(`form:${id}/${form}`); }

  // 解鎖條件與目前進度：{ cost } 或 { char, have, need }，加上 hint
  requirement(key) {
    const rule = this.cfg.unlocks[key];
    if (!rule) return null;
    if (rule.cost) return { cost: rule.cost, hint: rule.hint };
    const a = rule.affection;
    return { char: a.char, have: this.level(a.char).level, need: a.level, hint: rule.hint };
  }

  // 清除所有進度（開發者模式用），會重新發今天的登入獎勵
  resetState() {
    this.state = emptyState();
    this.state.day.date = null;
    this.checkDay();
  }

  commit() { this.save(this.state); }
}
