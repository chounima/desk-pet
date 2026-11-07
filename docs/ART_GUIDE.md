# 美術規格（部件包）

給畫師與 AI 輔助流程：照這份規格交出「部件包」，不用碰程式就能讓角色在桌寵和網頁上動起來。
人工畫、AI 產生、兩者混合都一樣；AI 的建議做法見 [ART_GUIDE_AI.md](ART_GUIDE_AI.md)。

## 一句話流程

```
畫（或 AI 產生）部件 → 放進 art_src/<角色>/ → python tools/import_layers.py art_src/<角色>
→ python tools/art_check.py art_src/<角色>（看報告） → 網頁編輯器微調位置 → 桌寵重新啟動就看到
```

## 角色是怎麼動的

角色是一套**骨架**（頭、身體、上臂、前臂、手、大腿、小腿、腳…），每根骨頭上貼一張**部件圖**，動作只是轉動骨頭。
所以部件圖要**分開畫**，而且每一張都是「這個部件在中立姿勢時的樣子」：

- **正側面**、面向**右邊**（程式會自己翻面）
- 手臂、腳**自然垂直往下**（不要彎），關節在圖的上端和下端
- 每張圖只有自己這個部件，**透明背景**
- **關節處要多畫一段重疊**：上臂的下端要畫到手肘再多一點（被前臂蓋住的那段），大腿畫到膝蓋再多一點……
  轉動時重疊的部分會互相遮住，不畫的話彎曲時會露出縫

近側和遠側的手腳用**同一張圖**（遠側程式會自動畫暗一點、放在身體後面）。

## 部件清單

部件名就是**檔名**（或分層檔的**圖層名**）。範圍（box）是這張圖放在骨頭上的位置，單位是「Q 版單位」，匯入時會自動放進範本角色同名部件的範圍，之後在編輯器微調。

### 人形（以 Chouma 為範本的正常頭身；Q 版角色如 Sera 部件一樣、比例不同）

| 部件名 | 是什麼 | 掛在 | Chouma 的範圍（寬×高） | 備註 |
|---|---|---|---|---|
| `head` | 頭（臉的底色、耳朵、脖子上緣） | head | 85×140 | 臉上的五官不要畫在這裡（畫在表情） |
| `hair_back` | 後面的頭髮 | head | 85×140 | 畫在頭的後面 |
| `hair_front` | 前面的頭髮（瀏海、側邊） | head | 85×140 | 畫在臉的前面 |
| `torso` | 上半身（衣服） | torso | 70×114 | 腰的地方要和 `hips` 重疊 |
| `hips` | 腰、褲子上半 | hips | 64×66 | |
| `arm_u` | 上臂 | armNear_u／armFar_u | 40×82 | 肩膀圓一點；下端畫過手肘 |
| `arm_l` | 前臂 | armNear_l／armFar_l | 20×70 | 下端畫過手腕 |
| `hand` | 手（放鬆） | handNear／handFar | 26×32 | 替換圖 `hand@peace`（比 YA） |
| `leg_u` | 大腿 | legNear_u／legFar_u | 48×104 | 下端畫過膝蓋 |
| `leg_l` | 小腿 | legNear_l／legFar_l | 24×90 | 下端畫過腳踝 |
| `foot` | 腳、鞋子 | footNear／footFar | 50×33 | 腳尖朝右 |
| `back` | 背包等背在背上的東西 | torso | 54×108 | 選配 |
| `tail` | 尾巴 | tail | Sera 74×126 | 選配（有尾巴的角色） |
| `cape` | 披風、長外套下襬 | cape | — | 選配 |

完整、最新的清單與範圍：`art/characters/<範本>/rig.json` 的 `slots`（`file` 的檔名就是部件名，`box` 是範圍）。

### 四足（Choupaca 的四足型態）

部件名和人形一樣（前腳 = `arm_u`、`arm_l`、`hand`；後腳 = `leg_u`、`leg_l`、`foot`；身體 = `hips`），在部件包裡寫 `"form": "quadruped"`。

### 表情（整張臉）

放在 `faces/`：`idle`（平常）、`blink`（閉眼）、`happy`、`sleep`、`surprised`、`focus`（專注）、`shy`、`angry`。
每張都是**同樣大小、同樣位置**的整張臉（眼、眉、嘴、腮紅），只有五官不一樣。沒做的表情會用 `idle`。

## 預先產出（之後的階段才會用到，可以先一次畫好）

部件包會把這些原樣保留，等程式做到那個功能時直接用，不用再重畫。

| 資料夾 | 用在 | 內容 |
|---|---|---|
| `face_parts/` | 第 3 階段：分層表情、動嘴、眼睛跟游標 | 檔名 = `圖層_圖名`，和佔位角色 `art/characters/tk01/face_parts/` 一樣：眼眶 `eyes_open`、`eyes_half`、`eyes_sharp`、`eyes_wide`（**不含瞳孔**）、閉眼 `eyes_line`（眨眼、睡覺）、`eyes_arc`（開心 ^ ^）；瞳孔 `pupils_open`…（和睜眼的眼眶同名，會跟著視線位移，四周留 2～3 單位不要畫到眼眶外）；眼線 `lids_open`…（蓋在瞳孔上面）；嘴 `mouth_flat`、`mouth_smile`、`mouth_cat`、`mouth_open`、`mouth_o`、`mouth_pout`、`mouth_gasp`，母音 `mouth_va`、`mouth_vi`、`mouth_vu`、`mouth_ve`、`mouth_vo`；`blush_on`（腮紅，不透明度由表情決定）；`extras_on`（淚痣、鬍鬚等，選配）；眉毛 `brows_*`（選配）。全部同樣大小、同樣位置（和 `faces/` 一樣的臉框）。**目前部件包還不會讀 `face_parts/`**（先備好，接上前用 `faces/` 整張臉） |
| `hair_parts/` | 第 3 階段：頭髮飄動 | 長頭髮切成 2～3 節：`hairB_1..3`（後面，已支援：見 `tools/artgen/__init__.py` 的 `HAIR_CHAINS`），`hairF_*`、`hairS_*`（前面、側邊，格式相同）；每節的原點在上緣中間、往下畫，上端要和上一節重疊、上緣不要畫線 |
| 網格（`rig.json` 插槽的 `mesh`） | 第 4 階段：關節彎曲時部件跟著彎 | 不用另外畫。適合**畫成一整條、跨過關節**的部件（長髮、整條手臂）；關節畫成圓頭重疊的部件不要開（圓頭會被拉歪） |
| `bust/` | 第 6 階段：半身模式 | **正面**的上半身：`head`、`hair_back`、`hair_front`、`torso`、`arm_u`、`arm_l`、`hand`，以及正面的 `face_parts`（同上的名字） |

## 規格

| 項目 | 規格 |
|---|---|
| 檔案格式 | PNG（建議）、WebP、JPG（沒有透明度，不建議）；SVG 也可以。分層檔可以交 Krita `.ora`（建議）或 `.psd` |
| 背景 | **透明**。去背要乾淨：不要有白邊、霧邊、零星的半透明雜點 |
| 解析度 | **每 Q 版單位 4 像素**（例如 Chouma 的 `torso` 範圍 70×114 → 約 280×456 像素）。最少 2.5；超過 16 太浪費記憶體 |
| 比例 | 圖片的寬高比要和範圍一樣，不然會被拉伸（匯入時會自動保持比例放進範圍，編輯器可以再調） |
| 線條 | 全部部件**同樣粗細、同樣顏色**的外框線（例如深咖啡 `#24161b`），內部陰影方向一致（光從左上來） |
| 命名 | 檔名／圖層名 = 部件名（小寫英文，見上表）；替換圖用 `部件名@替換名`；表情放 `faces/` 或圖層名 `face:表情` |
| 分層檔 | 一個部件一層，圖層名 = 部件名；不要的草稿圖層隱藏起來（隱藏的不會匯入）；每層可以在整張畫布的任何位置（會自動裁掉透明邊） |

## 資料夾

```
art_src/<角色>/
├── character.json      （選配）名稱、範本角色、每個部件的位置；沒有的話匯入時自動產生草稿
├── parts/              部件：torso.png、arm_u.png、hand.png、hand@peace.png …
├── faces/              表情：idle.png、blink.png …
├── face_parts/  hair_parts/  bust/    （選配）預先產出
├── <角色>.ora / .psd   （選配）分層檔，可以取代 parts/ 和 faces/
└── CREDITS.md          作者、工具、授權（AI 輔助要寫工具與人工修改的範圍）
```

`art_src/chouma/` 是一個完整的範例（由佔位角色自動輸出的測試部件包）：**把裡面的圖換成精細的圖、重新匯入，就完成換圖**。

## 指令

| 要做什麼 | 指令 |
|---|---|
| 匯入（資料夾） | `python tools/import_layers.py art_src/chouma` |
| 匯入（分層檔） | `python tools/import_layers.py art_src/chouma/chouma.ora --id chouma` |
| 新角色（用別人的骨架） | `python tools/import_layers.py art_src/newchar --base mira --name 新角色` |
| 只檢查不寫入 | 加 `--dry-run` |
| 檢查報告 | `python tools/art_check.py art_src/chouma` → `build/art_check/chouma.html` |
| 看所有動作 | `python tools/serve.py` → `preview/gallery.html?char=chouma` |
| 微調位置 | `preview/editor.html`（綁骨模式） |
| 整體檢查 | `python tools/validate.py` |
| 換回佔位圖 | 刪掉 `art/packages/<角色>/`，並從 `art/packages/index.json` 拿掉 |

第一次使用要先 `python -m pip install -r tools/requirements.txt`。

## 授權

- 委託畫師：合約要寫清楚著作權歸屬（要能用在販售的遊戲裡、能修改）
- AI 輔助：在 `CREDITS.md` 記錄用了哪個工具（與版本）、哪些部分人工修過；確認工具的商用條款；上架 Steam 時要揭露 AI 生成內容
- 不要用別人的角色、商標或照片當作品的一部分；參考圖只用來對齊比例
