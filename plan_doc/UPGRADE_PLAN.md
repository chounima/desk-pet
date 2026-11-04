# 動畫與美術升級規劃

2026-11-04 起草，同日依討論修訂。目標：動作像專業桌寵一樣流暢（頭髮、尾巴、衣服自然飄動、身體有彈性、表情細膩），角色美術從程式佔位圖升級成精緻手繪，而且美術和程式分開，畫師可以自己修改、擴充。

## 已定案（2026-11-04）

| 項目 | 決定 |
|---|---|
| 升級路線 | 在現有系統上自己加（物理、曲線、網格變形），不換 Live2D／Spine |
| 程式碼 | **升級前先整理**：參數進 `data/`、功能模組化、共用測試、架構文件 |
| 網頁預覽 | **每個功能都和 Godot 同步**，包含網格變形（網頁改用 PixiJS 繪製） |
| 手機版 | **先不做**（響應式、觸控、PWA、手機存檔都延後） |
| 動作編輯器 | 要做，放在動作系統升級之後、重修動作之前 |
| 角色專屬動作 | 動作依「角色＋型態 → 角色 → 體型 → 共用」往上找；角色專屬可以只寫差異 |
| 美術 | 和程式分離：原始檔放 `art_src/`，綁骨在編輯器裡做，正式角色和自訂角色用同一種格式 |

## 結論：做得到，而且不用打掉重來

| 現況 | 為什麼適合升級 |
|---|---|
| 骨架、插槽、部件都是資料（`rig.json`、`data/animations/*.json`） | 新功能可以做成「選配欄位」，舊角色、自訂角色不用改 |
| 畫面集中在 `PetView`（Godot）和 `renderer.js`（網頁） | 換繪製方式（例如網格變形）只動這一層 |
| 姿勢流程是一條線：`Animator.Update` → `Gaze` → `Skeleton.Compute` → `PetView.Render` | 物理和疊加動作可以插在 `Skeleton.Compute` 前後，不用動行為邏輯 |
| 已經支援 PNG 部件與 `character.json`（自訂角色、衣服） | 手繪美術與正式角色可以沿用同一條讀取路徑 |
| 已經有選配骨頭（`tail`、`cape`、`back`）與體型動作組（`quadruped/index.json` 的 `clips`、`alias`） | 頭髮骨鏈、角色專屬動作都是同一個模式的延伸 |

## 跟流暢桌寵的差距

| 項目 | 現況 | 差距 |
|---|---|---|
| 插值 | 每段都用 smoothstep（`Animator.SampleTrack`） | 沒有彈回、預備、停頓、線性這些選擇，動作看起來都一樣「軟」 |
| 次級動作 | 尾巴、披布寫死在關鍵影格（`walk.json` 的 `tail`、`cape`） | 停下來、被拖曳、落地時不會甩 |
| 頭髮 | `hair_front`、`hair_back` 黏在 `head` 上，是整片硬圖 | 不會飄 |
| 部件 | Sprite2D／SVG 硬紙片，只能轉、移 | 關節會折出硬角，身體不能彎 |
| 骨骼 | 只有角度與位移，**沒有縮放** | 做不出 squash & stretch |
| 待機 | `idle` 一個循環＋眨眼 | 沒有呼吸、重心晃動 |
| 表情 | 整張臉一張 SVG，直接換圖 | 眼睛、嘴、眉毛不能分開組合；主角色不會動嘴 |
| 轉頭 | `Gaze` 讓整張臉位移一點 | 沒有前後圖層的視差立體感 |
| 動作製作 | 手打 JSON | 沒有時間軸編輯器，這是動作做不細的最大瓶頸 |
| 角色差異 | 只能依體型分組，或用 `TRACK_SCALE` 調幅度 | 某個角色不能有自己的走路方式 |
| 美術 | `tools/artgen` 用 Python 畫 SVG，美術就是程式 | 畫師沒辦法改；細節有上限 |

## 程式碼現況的問題（第 0 階段要處理）

| 問題 | 例子 |
|---|---|
| 參數寫死在兩邊程式裡 | 自動行為權重 `AUTO` 同時在 `preview/js/config.js` 和 `godot/src/Game/Tuning.cs`，靠手動同步 |
| 兩邊邏輯沒有自動比對 | 寵物、行為、排球、收集、動畫器、骨架，JS 約 2,900 行、C# 約 9,400 行各一份，算出來是否一致只能靠人看 |
| `Main` 什麼都管 | 拆成 7 個 `partial` 檔（`Main.Focus.cs`、`Main.Music.cs`…），共用同一份狀態；每加功能都要動 `Main` |
| 雜項 | 根目錄有空的 `Volley.cs`（0 byte）；`PLAN.md` 搬到 `planDoc/` 後，README 的連結還指向舊位置 |

## 路線比較

| 路線 | 優點 | 問題 | 判斷 |
|---|---|---|---|
| **A. 自己加** | 保留所有既有功能；每一步都能單獨上線；網頁預覽能同步；不用授權費 | 要自己寫物理、網格變形、編輯工具 | ✅ 採用 |
| B. Live2D（GDCubism 外掛） | 頭髮物理、轉頭立體感是業界標準 | 擅長正面，**側面走路、殺球、魚躍很難做**；排球 `Probe`、配件、自訂角色、四足、網頁預覽全要重做；Godot 4.7＋C# 相容性與授權**需確認** | ❌ |
| C. Spine（spine-godot） | 側面全身、網格、物理、IK 都很強 | 動作全部重做；要授權；Godot 4.7 C# 整合**需確認**；網頁與自訂內容要重做 | ⏸ A 做到第 4 階段還不夠才考慮 |
| D. 3D（VRM）＋卡通渲染 | 轉身、頭髮都自然 | 等於另一個遊戲 | ❌ |

## 執行原則

- **一步一個檔案**，每步做完停下來審閱
- **兩邊同步**：每個 Godot 步驟都有對應的網頁步驟，表格裡成對列出；同一個功能兩邊都做完、共用測試通過，才算完成
- **新欄位都是選配**：沒有新欄位的角色、自訂角色、四足型態照舊運作
- **參數放 `data/`，不寫在程式裡**：兩邊讀同一份
- **新功能做成模組**：照 `docs/ARCHITECTURE.md` 的分層與模組規則，不直接塞進 `Main`
- **物理骨不參與 `Probe`**：排球判定只算關鍵影格
- **每個階段前後都錄影比較、量效能**

---

## 第 0 階段：整理程式碼與量測

先把地基整理好，之後每個功能才不會越加越亂。

| 步驟 | 檔案 | 內容 |
|---|---|---|
| 0a | `Volley.cs`（刪除）、`README.md` | 刪掉根目錄的空檔；README 的 `PLAN.md` 連結改成 `planDoc/PLAN.md` |
| 0b | `docs/ARCHITECTURE.md`（新） | 分層規則（`Core` 純計算、不碰 Godot 節點 → `Game` → `View`／`UI`；`Platform` 獨立；下層不引用上層）、功能模組的介面、資料放哪裡、兩邊同步的規則、新增功能的檢查清單 |
| 0c | `data/tuning.json`（新） | 把 `config.js` 的 `TUNING`、`AUTO`、`VOLLEY` 與 `Tuning.cs` 的數值比對後合成一份（不一致的地方先列出來討論） |
| 0d | `godot/src/Game/Tuning.cs` | 改成讀 `data/tuning.json` |
| 0e | `preview/js/config.js` | 改成讀 `data/tuning.json` |
| 0f | `schemas/*.schema.json`（新） | 用 JSON Schema 定義 `rig.json`、動作、`character.json`、`tuning.json` 的格式 |
| 0g | `tools/validate.py`（新） | 檢查 `data/`、`art/` 所有檔案符合格式（之後畫師、編輯器輸出都靠它把關） |
| 0h | `tests/golden/*.json`（新） | **共用測試資料**：輸入（角色、動作、時間、mods）→ 預期輸出（各骨骼世界座標、表情、Probe 結果） |
| 0i | `godot/src/Tests/Golden.cs`（新） | `--selftest=golden` 跑共用測試 |
| 0j | `preview/tests/golden.html`（新） | 網頁跑同一份共用測試 |
| 0k | `godot/src/Features/IFeature.cs`（新） | 功能模組介面：初始化、每幀更新、提供系統匣／右鍵選單項目、存檔、結束 |
| 0l～0q | `Main.Focus.cs` → `Features/Focus/…`，依序 Music、Daily、Progress、Custom、Guests | **一次搬一個功能**，搬家不改行為，每搬一個就跑共用測試與手動確認 |
| 0r | `tools/record_clip.ps1`（新） | 錄下某角色某動作 3 秒（網頁與 Godot 都能錄），做升級前後比較 |
| 0s | `godot/src/Main.cs` | `-Dump` 加上每幀耗時（平均／最大）與客人角色數量 |

完成標準：兩邊讀同一份參數；共用測試兩邊都通過；`Main.cs` 只剩串接模組；有一組升級前的錄影與效能數字（1 隻、5 隻）。

## 第 1 階段：動作系統

| 步驟 | Godot | 網頁 | 內容 |
|---|---|---|---|
| 1a / 1b | `Core/Spring.cs`（新） | `core/spring.js`（新） | **次級物理**：骨鏈上每根骨頭有角度與角速度，用彈簧＋阻尼追動畫給的目標角度；受身體加速度（走、停、被拖、落地）與重力影響。固定 120 Hz 子步進，瞬間移動時重設 |
| 1c | `Content/Models.cs` ＋ schema | — | `rig.json` 加選配 `physics: { 骨骼: { stiffness, damping, gravity, inertia, limit } }` |
| 1d / 1e | `Game/Pet.cs` | `game/pet.js` | 在 `Skeleton.Compute` 之後套物理，翻面時把物理狀態鏡像 |
| 1f | `tools/artgen/rig.py` | — | 先給 `tail`、`cape` 開物理，關鍵影格的擺動變成「目標」 |
| 1g / 1h | `Core/Animator.cs` | `core/animator.js` | **關鍵影格曲線** `[時間, 數值, "曲線"]`：`smooth`（預設）、`linear`、`easeIn`、`easeOut`、`back`、`hold` |
| 1i / 1j | `Core/Skeleton.cs` | `core/skeleton.js` | 縮放軌道 `骨骼.sx`、`骨骼.sy`（squash & stretch），預設 1 |
| 1k / 1l | `Core/Overlay.cs`（新） | `core/overlay.js`（新） | **疊加層**：呼吸、重心晃動；設定在 `data/motion.json`（新） |
| 1m | `tests/golden/` | — | 加物理、曲線、縮放、疊加的測試案例 |

完成標準：Sera 走路停下時尾巴會甩兩下再靜止；被拖曳時尾巴和披布落後；落地能壓扁；待機時在呼吸。兩邊共用測試通過；5 隻客人角色時每幀耗時增加不超過 1 ms。

## 第 2 階段：動作編輯器＋角色專屬動作

### 2-1 角色專屬動作

找動作的順序（找到就用）：

```
data/animations/characters/<角色>/forms/<型態>/<動作>.json   角色＋型態專屬
data/animations/characters/<角色>/<動作>.json                角色專屬
data/animations/<體型>/<動作>.json                           體型專屬（現在就有）
data/animations/<動作>.json                                  共用（現在就有）
```

角色專屬檔可以是**完整動作**，也可以只寫**差異**：`{ "extends": "walk", "tracks": { "armNear_u": [...] } }`，只覆蓋列出的軌道，其他沿用下一層。現有的 `alias` 照舊運作。

| 步驟 | Godot | 網頁 | 內容 |
|---|---|---|---|
| 2a / 2b | `Content/ContentLoader.cs` | `core/loader.js` | 分層找動作、`extends` 差異合併 |
| 2c | `tests/golden/` | — | 分層與差異合併的測試案例 |

### 2-2 動作編輯器

| 步驟 | 檔案 | 內容 |
|---|---|---|
| 2d | `preview/animator.html`（新）＋ `js/animator/main.js` | 頁面骨架：選角色、型態、動作，播放／暫停 |
| 2e | `js/animator/timeline.js` | 時間軸：拖曳預覽、每根骨頭一列、新增／刪除／移動關鍵影格 |
| 2f | `js/animator/posing.js` | 在角色身上點骨頭拖曳旋轉、拖曳位移 |
| 2g | `js/animator/curves.js` | 每個關鍵影格選曲線，顯示曲線形狀 |
| 2h | `js/animator/onion.js` | 洋蔥皮（前後影格半透明） |
| 2i | `js/animator/compare.js` | 多角色同時播放同一個動作（Q 版、正常頭身、四足並排） |
| 2j | `js/animator/source.js` | 顯示動作來自哪一層；存檔時選「存成共用／體型／這個角色專屬／只存差異」 |
| 2k | `tools/serve.py` | 加存檔 API：只接受本機（127.0.0.1）連線，存檔前用 schema 檢查 |
| 2l | `js/animator/physics.js` | 物理參數即時調整滑桿，可以存回角色的 `physics` |

### 2-3 重修主要動作（用編輯器）

| 步驟 | 內容 |
|---|---|
| 2m | `idle`、`walk`、`run`、`jump`、`fall`、`dragged`、`vbSpike`：加預備動作、重疊（頭先動、手後到）、落地壓扁 |
| 2n | 試做一個角色專屬動作：例如 Chouma 的 `walk`（只改手臂，用差異）、Choupaca 的生氣走路 |

完成標準：不打 JSON 就能做完一個新動作並在桌寵上播放；角色專屬動作與差異動作兩邊都正確。

## 第 3 階段：骨架與表情擴充

| 步驟 | Godot | 網頁 | 內容 |
|---|---|---|---|
| 3a | `tools/artgen/rig.py` | — | **頭髮骨鏈**（選配）：`hairF_1..3`、`hairS_1..3`、`hairB_1..3`，每節一個插槽 |
| 3b | `tools/artgen/characters/tk01.py`、`sera.py` | — | TK-01 長髮、Sera 耳朵拆成骨鏈並開物理 |
| 3c / 3d | `View/PetView.cs` | `view/renderer.js` | **轉頭視差**：頭部圖層設 `depth`，轉頭時依深度位移不同量 |
| 3e | `tools/artgen/faces.py` | — | **分層表情**：臉拆成 `eyes`、`brows`、`mouth`；舊的整張臉照樣可用 |
| 3f / 3g | `View/PetView.cs` | `view/renderer.js` | 分層表情的繪製與組合 |
| 3h / 3i | `Game/Pet.cs` | `game/pet.js` | 多格眨眼；主角色接 `Mouth` 動嘴 |
| 3j | `preview/js/editor/main.js` | — | 自訂角色編輯器認得新插槽 |
| 3k | `js/animator/` | — | 動作編輯器可以編表情時間軸（眼、眉、嘴分開） |

完成標準：TK-01 走路時長髮往後飄、停下會晃；轉頭有立體感；講話會動嘴。

## 第 4 階段：網格變形（兩邊都做）

| 步驟 | Godot | 網頁 | 內容 |
|---|---|---|---|
| 4a | — | `preview/vendor/pixi.min.js`（新） | 放入固定版本的 PixiJS（放在專案裡，不連 CDN，離線也能用） |
| 4b | — | `view/renderer.js` | **網頁繪製從 SVG 換成 PixiJS**，先做到畫面跟現在一模一樣（還沒有網格），所有頁面（預覽、動作總覽、編輯器、測試頁）確認正常 |
| 4c | — | `view/hitTest.js`（新） | 換成 PixiJS 後的點擊判定 |
| 4d | `Core/Mesh.cs`（新） | `core/mesh.js`（新） | **網格計算**（純計算，兩邊共用測試）：鋪格子、依距離自動算骨骼權重、每幀算頂點位置 |
| 4e | `Content/Models.cs` ＋ schema | — | 插槽加選配 `mesh: { cols, rows, bones, weights }` |
| 4f / 4g | `View/MeshPart.cs`（新） | `view/meshPart.js`（新） | 用 `Polygon2D`／PixiJS Mesh 畫網格部件 |
| 4h / 4i | `View/PetView.cs` | `view/renderer.js` | 有 `mesh` 的插槽用網格部件，沒有的照舊 |
| 4j / 4k | `View/PetView.cs` | `view/hitTest.js` | 網格部件的點擊判定（點在哪個三角形 → 換算回原圖像素） |
| 4l | 角色資料 | — | 先套在手肘、腰、頭髮骨鏈 |
| 4m | `js/animator/` | — | 動作編輯器顯示網格、可調權重 |

完成標準：手肘、腰不會折出硬角；長髮是連續彎曲的曲線；兩邊畫面一致，網格頂點共用測試通過；點擊穿透與部位判定跟現在一樣準。

## 第 5 階段：美術和程式分離＋手繪流程

### 目標的資料夾分工

```
art_src/<角色>/        畫師的原始檔（PSD／Krita），畫師只改這裡（用 Git LFS）
art/characters/<角色>/ 執行用的檔案：character.json ＋ 部件 PNG／SVG（由工具或編輯器產生）
tools/artgen/          佔位角色產生器，輸出同樣的格式
```

正式角色、自訂角色、佔位角色都是**同一種格式**，桌寵和網頁只認這一種。

| 步驟 | 檔案 | 內容 |
|---|---|---|
| 5a | `schemas/character.schema.json` | 統一角色格式：部件、關節點、插槽、表情對應、物理、網格、錨點、型態 |
| 5b / 5c | `Content/ContentLoader.cs`、`CustomContent.cs` / `core/loader.js` | 正式角色與自訂角色走同一條讀取路徑 |
| 5d | `tools/artgen/__init__.py` | 佔位角色改輸出統一格式（現有角色看起來不變） |
| 5e | `docs/ART_GUIDE.md`（新） | **給畫師的規格**：圖層命名（對應插槽名）、關節處多畫重疊部分、解析度、頭髮怎麼切、表情怎麼分層、怎麼預覽 |
| 5f | `tools/import_layers.py`（新） | 讀 Krita `.ora`（優先，格式簡單）或 PSD → 部件 PNG ＋ `box`，產生 `character.json` 草稿 |
| 5g | `preview/js/editor/rigging.js`（新） | 編輯器「綁骨」模式：拖曳設定關節點、部件範圍、圖層順序、表情對應、物理與網格，存回 `character.json`（透過 2k 的存檔 API） |
| 5h / 5i | `View/SvgTextures.cs` / `view/renderer.js` | 高解析度 PNG 依顯示大小縮小後才放進記憶體，加 mipmap |
| 5j | `.gitattributes` | Git LFS 管理 `art_src/` 的 PSD／Krita 檔；`art_src/CREDITS.md` 記錄每個素材的作者與授權 |
| 5k | 試做 **Chouma** | 以 `Character_design_sheet/chouma.png` 當參考，走完「畫 → 匯入 → 綁骨 → 播放」整條流程 |
| 5l | 其他角色 | 逐一替換；Mira 的外觀要先改成自有設計（見 `planDoc/PLAN.md` 的 ⚠） |

畫師的流程：改 `art_src/` 的分層檔 → 匯入（或拖進編輯器）→ 在網頁上看到動起來的樣子 → 需要調整就在編輯器拖拉，不用碰程式。

完成標準：畫師只用繪圖軟體和網頁編輯器就能更新或新增一個角色；Chouma 手繪版在 4K 300% 下清楚、所有動作正常、配件位置正確；記憶體用量實測可接受（含 5 隻客人角色）。

---

## 使用的技術

### 現有

| 類別 | 技術 |
|---|---|
| 桌寵本體 | Godot 4.7 .NET（C#、.NET 8），GL Compatibility 繪製器；SVG 執行時用 Godot 內建功能轉貼圖 |
| Windows 整合 | P/Invoke：`user32`（透明視窗、點擊穿透、`GetAsyncKeyState` 全域鍵盤、視窗走訪）、`dwmapi`、`ole32`（COM 讀取音量）；具名互斥鎖；登錄檔開機啟動 |
| 音訊 | 程式合成音效、撥弦合成、拍點偵測（自己寫的） |
| 直播／多人 | Twitch IRC（匿名）、Discord 本機 RPC |
| 網頁預覽與編輯器 | 原生 JavaScript（ES modules）＋ SVG ＋ HTML/CSS，沒有框架與建置步驟；localStorage |
| 工具 | Python 3（美術產生器、翻譯建置、內容同步、圖示、部件圖、`serve.py`） |
| 建置與測試 | PowerShell 腳本、`--selftest`、網頁測試頁 |
| 資料 | JSON |
| 外部套件 | 無（C# 沒有 NuGet 套件，網頁沒有 CDN 套件） |

### 新增

| 技術 | 用途 | 階段 |
|---|---|---|
| JSON Schema ＋ Python `jsonschema` | 資料格式定義與檢查（編輯器、畫師輸出、兩邊讀取都依同一份） | 0 |
| 共用測試資料（golden tests） | 兩邊算出一樣的結果；不需要新工具，Godot 用 `--selftest`、網頁用測試頁 | 0 |
| PixiJS（固定版本，放在 `preview/vendor/`） | 網頁版網格變形與繪製效能 | 4 |
| Krita `.ora` 讀取（zip＋XML，標準庫即可）；PSD 用 Python `psd-tools` | 分層美術匯入 | 5 |
| Git LFS | 管理大型美術原始檔 | 5 |

### 暫不採用

| 技術 | 原因 |
|---|---|
| 手機版（響應式、觸控、PWA、區網存檔） | 2026-11-04 決定延後 |
| TypeScript ＋ 建置工具 | 維持網頁零建置；動作編輯器變得太大時再評估 |
| React 等前端框架 | 原生 JS 夠用 |
| Godot 網頁匯出 | 網頁版繼續獨立維護，靠共用測試保持一致 |

## 風險與注意事項

| 風險 | 對策 |
|---|---|
| 兩邊程式不一致 | 每個功能都有共用測試；兩邊都通過才算完成 |
| 網頁從 SVG 換 PixiJS 範圍大 | 4b 單獨一步，先求畫面完全不變，再加網格 |
| 功能模組化搬家時改壞行為 | 一次搬一個功能、搬家不改行為，搬完跑測試與手動確認 |
| 物理參數難調，容易抖或爆掉 | 固定子步進、限制角度、瞬間移動時重設；編輯器即時調整 |
| 角色專屬動作越來越多、難以管理 | 優先用 `extends` 寫差異；編輯器顯示來源層 |
| 自訂角色是複製範本 rig | 新欄位全部選配；統一格式時保留舊格式的讀取 |
| 客人角色多隻同時跑，負擔增加 | 每階段量測；必要時客人角色降低網格密度或關掉物理 |
| `serve.py` 存檔 API 被外部存取 | 只綁 127.0.0.1，存檔前檢查格式與路徑（不能寫出專案資料夾） |
| 四足型態骨骼意義不同 | 物理、網格參數寫在各型態自己的設定 |
| 正式美術的來源與權利 | 委託畫師要確認著作權歸屬；若使用 AI 生成內容，上架 Steam 時需揭露 |
| 手繪 PNG 佔記憶體 | 縮小後才放進記憶體，不保留原圖 |

## 還要決定

1. **美術來源**：委託畫師／自己畫／AI 輔助再手修？影響第 5 階段與 `ART_GUIDE.md` 寫多細
2. **試做角色**：第 1、3 階段建議 **TK-01**（長髮）＋ **Sera**（尾巴、耳朵）
3. **0c 參數合併**：兩邊數值不一致的地方，以哪一邊為準（做 0c 時列出來再決定）

## 順序

`0（整理）→ 1（動作系統）→ 2（角色專屬動作、編輯器、重修動作）→ 3（頭髮、表情）→ 4（網格變形）→ 5（美術分離、手繪）`

第 1 階段做完就能感覺到變流暢；第 2 階段之後做動作不用再手打 JSON。
