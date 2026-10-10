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

也可以直接**雙擊專案根目錄的 `run_pet.cmd`** 啟動桌寵。

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
| 系統匣 | 切換角色、大小、自動行為、開機自動啟動、今日按鍵數、結束 |
| 設定存檔 | `user://settings.json`（`%APPDATA%\Godot\app_userdata\Desk Pet\`）：角色、大小、自動行為、每日按鍵次數 |
| 單一實例 | 具名互斥鎖 `Local\DeskPet.SingleInstance`，重複啟動時新的直接結束 |
| 站在視窗上 | 每 0.25 秒用 `GetTopWindow/GetWindow` 走訪視窗（不用需要回呼的 `EnumWindows`），沒被擋住的頂邊當平台；會跳上去、跳下來、跟著視窗移動，視窗關閉或最小化時掉下來。測試：`tools/test_platform.ps1` + `run_pet.ps1 -Action idle` 搭配 `--test=do:climb` |
| 排球對打 | 系統匣「🏐 排球對打」；邏輯在 `Game/Volley/`，球技與台詞在 `data/volley.json`（與網頁版共用）。游標用全域位置算揮動速度，游標在球上時才接收點擊。測試：`--volley-bot` |
| 音效 | `Audio/Synth.cs` 程式合成（pop、boing、thud、squeak、fall、chime、bonk、bump、spike、whoosh），台詞 key 對應音效見 `Sfx.LineSounds`；系統匣「🔊 音效」 |
| 右鍵選單 | 在角色身上按右鍵：動作（`data/menu.json`）、型態、排球、躲起來 N 分鐘（`UI/PetMenu.cs`；選單字型依螢幕縮放放大 `UI/MenuStyle.cs`） |
| 全螢幕自動隱藏 | 前景視窗蓋滿整個螢幕（含工作列）時躲起來，結束後說「我回來了」（`Platform/FullscreenWatcher.cs`、`Game/Presence.cs`）；系統匣可關 |
| 螢幕變化 | 每 2 秒檢查解析度／縮放比例／工作列，有變就重新鋪滿並調整大小（`DesktopWindow.RefreshDisplay`）；系統匣「大小跟著螢幕高度」改成依螢幕高度決定大小 |
| 依時間反應 | 啟動時依時段打招呼、深夜提醒睡覺、連續使用 50 分鐘提醒起來動（`Game/DailyLife.cs`，參數 `data/daily.json`） |
| 收集系統 | 點數、好感度、解鎖、商店（`Game/Progression/`，規則同網頁版），存檔 `user://progress.json`。`--selftest=progress` 跑規則測試 |
| Desk Pet 視窗 | 系統匣或右鍵選單「📖 收藏／🛒 商店／⚙ 設定」：獨立的系統視窗（`UI/Hub/`）。設定勾「開發者模式」多一個分頁，可改參數（存成 `user://progression.override.json`）、作弊、模擬隔天。`--hub=shop` 啟動時直接打開 |
| 彈吉他 | 右鍵選單／系統匣「🎸 彈吉他」或 Desk Pet 視窗「🎵 音樂」分頁（`Audio/Music/`）。內建曲 `data/music/*.json` 用撥弦合成演奏；自訂音樂放 `文件\DeskPet\music`（mp3／ogg／wav，`bpm.json` 可指定速度，否則從低音能量抓拍點）。刷弦動作依 BPM 調速並在拍點重來（`Animator.Speed`、`Rephase`）。`--mute` 測試時靜音、`--test=guitar` 直接開始彈 |
| 道具位置修正 | 角色模組 `PROP_OVERRIDES = {"guitar": {"dx", "dy", "scale"}}`：正常頭身角色抱吉他的位置 |
| 專注模式 | 番茄鐘（`Game/Focus.cs`、`Main.Focus.cs`、`data/focus.json`）：專注中只做安靜動作、不講一般台詞，倒數顯示在頭旁邊；完成一輪送點數與好感度，休息時伸懶腰。右鍵選單／系統匣「🍅 開始專注」、Desk Pet 視窗「🍅 專注」分頁可改分鐘數。測試：`--sandbox --focus-minutes=0.1 --test=focus` |
| 依正在用的程式反應 | 設定勾選才開（預設關）：只看前景程式的執行檔名稱（`Platform/ActiveApp.cs`），依 `data/apps.json` 分類，說一句話並讓相關動作更常出現（`Game/AppAwareness.cs`）。`--selftest=apps` 檢查分類 |
| 測試沙盒 | `--sandbox`：收集進度從空白開始、不存檔，測試不會動到真正的進度 |
| 自訂內容 | `文件\DeskPetccessories\<名稱>ccessory.json`＋圖片、`outfits\<名稱>\outfit.json`＋圖片、`characters\<名稱>\character.json`＋部件與表情圖片（`Content/CustomContent.cs`，格式見檔頭；自訂角色複製範本角色的 rig，id 為 `custom_<名稱>`）。自訂配件是免費商品；衣服在「👗 衣櫃」分頁選。圖片支援 PNG／JPG／WebP／SVG。測試可用環境變數 `DESKPET_CUSTOM` 換資料夾、`--test=outfit:<名稱>` |
| 成就 | 15 個（`data/achievements.json`），依統計數字自動解鎖並送點數；收藏分頁最下面看進度。之後接 Steam 見「接上 Steam」 |
| 多螢幕 | 設定分頁「螢幕」選要顯示在哪個螢幕（`DesktopWindow.Screen`）；角色不會跨螢幕走 |
| 開機自動啟動 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DeskPet`（`--autostart=on/off` 也可設定） |

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

### 打包成 .exe

`powershell -File tools/build_release.ps1` → `build/DeskPet/`（`DeskPet.exe` + `data_DeskPet_windows_x86_64/`，兩個要放在一起）與 `build/DeskPet-<日期>.zip`。

- `tools/sync_content.py` 先把 `art/`、`data/` 複製到 `godot/content/`（不進 git），SVG 用 `importer="keep"` 原樣打包，執行時一樣即時轉貼圖
- `ContentLoader` 找資料的順序：環境變數 `DESKPET_CONTENT` → 匯出版用 `res://content` → 開發時用上層的 `art/`、`data/`
- 需要 Godot 匯出模板（版本要和編輯器一致，放在 `%APPDATA%\Godot\export_templates\<版本>`）與 `godot/DeskPet.sln`（Godot 匯出 C# 時一定要有）
- 匯出記錄在 `build/import.log`、`build/export.log`，有 `ERROR:` 就判定失敗
- 圖示：`godot/icon.ico`（16～256 六種尺寸，用 `tools/make_icon.py` 從 PNG 組成）

除錯：`run_pet.ps1 -Dump` 每 2 秒印出狀態；`tools/send_f24.ps1` 送出 F24（沒有程式會用的鍵）測試全域鍵盤；
`tools/screenshot.ps1` DPI 感知截圖；`tools/window_at.ps1` 查詢某點下方是哪個程式（驗證點擊穿透）。

- 自訂配件／衣服／角色編輯器：`/preview/editor.html`（上傳 PNG／SVG、在動起來的角色身上拖曳對齊、滾輪縮放、匯出 json；`?demo=accessory|outfit|character` 用現成圖示範）
- 桌面預覽：`/preview/index.html`
- 動作總覽：`/preview/gallery.html`（所有動作同時播放）
- 截圖參數：`index.html?char=sera&action=walk&t=0.3&scale=2.4`、`gallery.html?char=tk01&t=0.3`
  （`scale` 可放大檢查細節；`index.html?volley=1` 直接進入排球對打）
  （`index.html?volley=1&rally=12` 直接從第 12 次來回開始，檢查火焰特效）
  （`index.html?volley=1&debug=1` 在工作列顯示她對每顆球的殺球判斷：球不夠高／時間不夠／跑不到…）
- 收集系統：面板「📖 收藏」看點數、好感度、解鎖進度；進度存在瀏覽器 localStorage（`deskpet.progress`）
  - `?dev=1` 開發者模式：即時修改 `data/progression.json` 的參數（暫存在瀏覽器）、💎 +100、❤ +50、模擬隔天、重置進度
  - `?collection=1` 直接打開收藏視窗、`?shop=ball` 直接打開商店某個分頁（截圖用）
  - 商店：面板「🛒 商店」；商品在 `progression.json` 的 `shop.items`（分類 ball／fx／bubble／action）。新動作商品要在 `config.js` 的 `AUTO` 加權重，買了才會自動出現
  - 測試：`/preview/tests/progress.html` 用真實參數跑一遍規則（每日上限、升級、解鎖、連續登入、排球破紀錄）
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
- **角色型態**（例如 Choupaca 的直立／四足）：角色模組加 `FORMS = {"名稱": 型態模組}`，型態模組提供 `LABEL`、`PARTS`、`FACES`、`PROPORTIONS`，可選 `VOLLEY`（擊球點、能不能殺球／魚躍）。輸出到 `art/characters/<id>/forms/<名稱>/`；網頁面板與系統匣「🔁 型態」可切換，每個角色分開記
- **非人形體型**：`PROPORTIONS` 用 `rig.QuadProportions`（四足）。骨骼名稱和人形相同（前腳 = arm、後腳 = leg），所以互動規則、台詞完全共用
  - 體型專屬動作放 `data/animations/<體型>/`，`index.json` 的 `clips` 列出有專屬版的動作，`alias` 讓其他動作借用相近的姿勢（名稱與 `next` 維持原本的）
  - 測試：`gallery.html?char=choupaca&form=quadruped`、`run_pet.ps1` 或 Godot 加 `--form=quadruped`
- **新配件**：在 `tools/artgen/accessories.py` 畫圖並加進 `ACCESSORIES`（部位 head／face／neck／back），再到 `progression.json` 的 `shop.items` 加 `acc:<名稱>`
  - 配件掛在角色的錨點上：預設依頭部大小算（`rig.default_anchors`），頭部圖有縮小的角色設 `HEAD_TRANSFORM`，特殊頭型直接寫 `ANCHORS`（參考 `choupaca`）
  - 檢查位置：`gallery.html?char=chouma&acc=strawhat,roundglasses,scarf,wings`
- **新表情**：在 `rig.EXPRESSIONS` 加名稱，並在 `faces.make_faces` 畫出來
- **新道具**：在 `tools/artgen/props.py` 的 `PROPS` 加一筆，再在動作的 `props` 引用

## 接上 Steam（之後上架時）

目前成就只存在本機（`Game/Progression/Achievements.cs`，資料 `data/achievements.json`），程式已經留好介面 `IAchievementSink`：

1. 在 Steamworks 後台建立 App，取得 App ID；成就的 API 名稱用 `achievements.json` 的 `id`
2. `godot/` 加入 [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)（NuGet `Steamworks.NET`），把 `steam_api64.dll` 放在 exe 旁邊；開發時放 `steam_appid.txt`
3. 寫一個 `SteamSink : IAchievementSink`：`Unlock` 呼叫 `SteamUserStats.SetAchievement(id)` + `StoreStats()`、`SetStat` 呼叫 `SteamUserStats.SetStat`
4. `Main.Progress.cs` 建立 `Achievements` 後，`SteamAPI.Init()` 成功就設 `achievements.Sink = new SteamSink()`，每幀呼叫 `SteamAPI.RunCallbacks()`
5. 雲端存檔：在 Steamworks 後台設定 Auto-Cloud，路徑填 `%APPDATA%\Godotpp_userdata\Desk Pet\progress.json`（和 `settings.json`）

## 開發環境

- Python 3（產生美術、本機伺服器）
- Godot 4.7 .NET 版 + .NET 8 SDK（遊戲本體）
- Godot 匯出模板 4.7.2.stable.mono（只有打包時需要）
