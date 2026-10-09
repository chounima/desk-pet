# Desk Pet

全身角色的桌面寵物：在工作列上走動、坐下、睡覺，你打字時會拿出筆電一起打。
角色和動作都可以切換。規劃見 [PLAN.md](PLAN.md)。

## 快速開始

```powershell
python tools/gen_art.py      # 產生角色美術（改了 tools/artgen 後要重跑）
python tools/serve.py        # 網頁預覽：http://localhost:8787/preview/
powershell -File tools/run_pet.ps1                       # 桌寵（Godot 版）：建置並啟動
powershell -File tools/run_pet.ps1 -Char sera -NoBuild   # 指定角色、略過建置
```

桌寵結束方式：系統匣圖示右鍵 →「結束」。

## 桌寵（Godot 版，`godot/`）

Godot 4.7 .NET（C#）。直接讀取專案根目錄的 `art/` 與 `data/`，和網頁預覽共用同一份角色與動作資料。

| 功能 | 做法 |
|---|---|
| 透明置頂視窗 | 無邊框、透明背景，鋪滿工作區；**高度少 1px**（和螢幕一樣大會被驅動當成獨佔全螢幕，畫面不更新） |
| 點擊穿透 | 視窗加 `WS_EX_LAYERED`，依游標是否在角色不透明像素上切換 `WS_EX_TRANSPARENT`。不用 Godot 的 `mouse_passthrough` 多邊形（Windows 上會裁切畫面且不重繪） |
| 全域鍵盤 | 背景執行緒每 5ms 用 `GetAsyncKeyState` 輪詢。不用 `SetWindowsHookEx`（Godot 的可卸載組件環境裡回呼不會被執行） |
| 隱私 | 按鍵當下分類成「一般字元／Enter／Backspace／Ctrl+S」就丟掉，不記錄內容 |
| 高 DPI | 讀取螢幕 DPI，角色大小、速度、重力、特效文字都乘上縮放倍率（例如 4K 300% → ×3） |
| 角色繪製 | 執行時把 SVG 轉成貼圖（依顯示大小 ×2 解析度），保留 Image 做像素級點擊判定 |
| 系統匣 | 切換角色、自動行為開關、結束 |

```
godot/src/
├── Main.cs            進入點、主迴圈、拖曳
├── Content/           資料格式與讀取
├── Core/              Skeleton、Animator（移植自 preview/js/core）
├── View/              PetView（部件繪製、點擊判定）、SvgTextures、Fx
├── Game/              Pet、Behavior、Tuning（移植自 preview/js/game）
├── Platform/          DesktopWindow（視窗、點擊穿透、DPI）、GlobalInput（全域鍵盤）
└── UI/                Tray（系統匣）
```

除錯：`run_pet.ps1 -Dump` 每 2 秒印出狀態；`tools/send_f24.ps1` 送出 F24（沒有程式會用的鍵）測試全域鍵盤；
`tools/screenshot.ps1` DPI 感知截圖；`tools/window_at.ps1` 查詢某點下方是哪個程式（驗證點擊穿透）。

- 桌面預覽：`/preview/index.html`
- 動作總覽：`/preview/gallery.html`（所有動作同時播放）
- 截圖參數：`index.html?char=sera&action=walk&t=0.3&scale=2.4`、`gallery.html?char=tk01&t=0.3`
  （`scale` 可放大檢查細節；`index.html?volley=1` 直接進入排球對打）
  （`index.html?volley=1&rally=12` 直接從第 12 次來回開始，檢查火焰特效）
  （`index.html?volley=1&debug=1` 在工作列顯示她對每顆球的殺球判斷：球不夠高／時間不夠／跑不到…）
- 測試：`/preview/tests/volley.html` 不開畫面，用真實角色資料模擬每個角色 20 回合排球對打，
  顯示平均來回、殺球／魚躍成功數、火焰次數、每回合怎麼結束、沒殺球的原因
  - `?player=click|swipe`：模擬點球或往上揮球的玩家
  - `?w=1280&h=720`：模擬螢幕大小（小視窗會讓球撞天花板，要特別測）
  - `?char=chouma`：只測一個角色；`?seed=3`：固定亂數以重現問題

## 小遊戲：排球對打

- 面板按「🏐 排球對打」開始，再按一次結束
- 玩家：游標快速揮過球 → 依揮動速度擊球；直接點球 → 自動吊回寵物那邊；
  游標碰到高速球（防守殺球）→ 自動接起
- 寵物每顆來球決定一次打法：
  - **殺球**：球夠高、來得及 → 三步助跑（左、右大步、左併步）→ 起跳揮臂扣向游標，畫面震動
  - **低手接球**：跑得到落點 → 低手把球打向游標
  - **魚躍救球**：來不及 → 往前撲出、胸口著地滑行再起身；撲不到也會撲
- 擊球判定用 `Pet.probe()` 從動作資料算出「那一刻手在哪裡」，畫面上的手和球會對上
- 連續來回 10 次以上：球燒起來（光暈＋火焰尾巴）
- 角色能力與台詞在 `game/volleySkills.js`（跑速、漏接率、殺球傾向、魚躍傾向、殺球力道）

| 角色 | 定位 | 模擬平均來回 |
|---|---|---|
| Chouma | 排球系主力 | 約 20 |
| TK-01 | 精準計算 | 約 11 |
| Sera | 敏捷但粗心（最愛撲） | 約 10 |
| Mira | 玩票性質 | 約 9 |

- 參數在 `preview/js/config.js` 的 `VOLLEY`；邏輯在 `game/volley.js`，球的繪製在 `view/ball.js`
- 小遊戲專用動作（`vbReady`、`vbBump`、`vbApproach`、`vbSpike`、`vbDive`）在 JSON 標了 `"hidden": true`，不會出現在面板上

## 動畫系統

2D 骨架動畫（cutout）：

- **骨架**：所有角色共用一套人形骨架（`tools/artgen/rig.py`），骨頭長度由 `Proportions` 決定
  - `CHIBI`：Q 版（TK-01、Sera）
  - `TALL`：約 7.5 頭身（Mira）
- **角色**：一組貼在骨骼上的部件圖＋表情（`tools/artgen/characters/<id>.py`）
- **動作**：各骨骼的關鍵影格角度（`data/animations/<name>.json`），所有角色共用
  - 動作以 Q 版身材撰寫；`root` 位移、道具大小、走路速度會依角色腿長（`rootScale`）自動放大
  - 角色可用 `TRACK_SCALE` 調整個別骨頭的擺動幅度
  - `MALE`：成年男性約 175cm（Chouma）
- 選配骨頭：`tail`（尾巴）、`cape`（披布）、`back`（背在背上的東西），角色沒畫就不顯示
- `prop` 骨頭：獨立移動的道具（例如排球），動作用 `prop.x` / `prop.y` 軌道控制
- **部件替換**：角色用 `VARIANTS = {"hand": {"peace": fn}}` 提供替換圖，動作用 `"alt": {"handNear": "peace"}` 指定；角色沒有該替換圖時自動用原圖
- **專屬表情**：角色可以自己畫 `FACES`（參考 `characters/chouma/faces.py`），不一定要用通用的 `make_faces`

座標與角度慣例（角色面朝右）：

| 部位 | 正值 | 負值 |
|---|---|---|
| 手臂、大腿（往下垂） | 往後擺 | 往前擺 |
| 前臂 | 往後 | 往前彎 |
| 小腿 | 膝蓋往後彎 | — |
| 軀幹、頭 | 往前傾 | 往後仰 |
| `root.y` | 往下 | 往上 |

## 專案結構

```
desk-pet/
├── PLAN.md
├── art/                         由 gen_art.py 產生，勿手動修改
│   ├── characters/
│   │   ├── index.json           角色清單
│   │   └── <id>/
│   │       ├── rig.json         骨架、插槽、表情對應
│   │       ├── parts/*.svg      身體部件
│   │       └── faces/*.svg      表情
│   └── props/                   道具（筆電…）＋ props.json
├── data/
│   └── animations/              動作（一個動作一個檔）＋ index.json
├── tools/
│   ├── gen_art.py               產生美術
│   ├── serve.py                 本機伺服器
│   └── artgen/
│       ├── rig.py               共用骨架與插槽
│       ├── faces.py             通用表情產生器
│       ├── shapes.py            繪圖工具
│       ├── props.py             道具
│       └── characters/          每個角色一個模組
└── preview/
    ├── index.html               模擬桌面
    ├── gallery.html             動作總覽
    ├── css/                     base / desktop / gallery
    └── js/
        ├── config.js            路徑與參數
        ├── main.js / gallery.js 兩個頁面的進入點
        ├── core/                math、skeleton、animator、loader（與畫面無關）
        ├── view/                renderer（SVG 繪製）、fx（特效）
        ├── game/                pet（寵物本體）、behavior（行為邏輯）
        └── ui/                  panel（控制面板）
```

## 新增內容

- **新動作**：在 `data/animations/` 新增 `<name>.json`，並加進 `index.json`
  - 欄位：`label`、`duration`、`loop`、`tracks`，可選 `face`、`props`、`fx`、`move`、`next`
  - 要讓它自動出現，在 `preview/js/config.js` 的 `AUTO` 加權重
- **新角色**：在 `tools/artgen/characters/` 新增模組（`ID`、`NAME`、`DESCRIPTION`、`PARTS`、`FACES`，可選 `PROPORTIONS`、`TRACK_SCALE`），加進 `characters/__init__.py`，重跑 `gen_art.py`
  - 正常頭身的角色，頭部可以用 Q 版座標畫，再用 `faces.head_transform` 縮小（參考 `mira.py`）
- **新表情**：在 `rig.EXPRESSIONS` 加名稱，並在 `faces.make_faces` 畫出來
- **新道具**：在 `tools/artgen/props.py` 的 `PROPS` 加一筆，再在動作的 `props` 引用

## 開發環境

- Python 3（產生美術、本機伺服器）
- Godot 4.7 .NET 版 + .NET 8 SDK（遊戲本體，尚未建立）
