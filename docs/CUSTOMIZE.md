# 自己調整：表情、角度、聲音反應

所有參數都放在 `data/` 的 JSON 檔裡（桌寵與網頁讀同一份）。改完：

- **開發版**（`tools/run_pet.ps1` 或 Godot 開的）：重新啟動桌寵就生效
- **打包版**（`build/DeskPet/DeskPet.exe`）：資料在打包時複製進去，要重新打包 `powershell -File tools/build_release.ps1`
- **網頁預覽**：重新整理頁面
- 改完可以跑 `python tools/validate.py` 檢查格式（打錯字、數字寫成文字會告訴你哪一行）

JSON 小提醒：字串要用雙引號、最後一項後面不能有逗號。開頭是底線的 key（`_說明`、`_範例`）是說明，程式不會用。

---

## 1. 表情長什麼樣子：`data/face.json` 的 `expressions`

有分層臉的角色（TK-01、Sera、Mira），每個表情 = 每一層用哪張圖：

| 圖層 | 可以用的圖 |
|---|---|
| `eyes` | `open`、`half`（半睜）、`sharp`（認真）、`wide`（睜大）、`line`（閉眼）、`arc`（開心 ^ ^） |
| `mouth` | `flat`、`smile`、`cat`、`open`、`o`、`pout`（嘟嘴）、`gasp`（驚訝）、母音 `va`、`vi`、`vu`、`ve`、`vo` |
| `blush` | `on`（深淺用 `alpha`） |
| `extras` | `on`（淚痣、鬍鬚等） |

瞳孔、眼線會自動跟著眼睛。完整清單在 `art/characters/<角色>/rig.json` 的 `face.layers`。

```json
"expressions": {
  "*": {
    "happy": { "mouth": "va", "alpha": { "blush": 0.9 } }
  },
  "tk01": {
    "idle": { "eyes": "open" },
    "wink": { "eyes": "arc", "mouth": "smile", "alpha": { "blush": 0.5 } }
  }
}
```

- `"*"` = 所有角色，寫角色 id 只改那個角色（後寫的蓋過先寫的）
- 可以**新增表情名**（上面的 `wink`）：沒寫的圖層從 `idle` 複製。新表情可以用在快捷鍵、聲音規則（見下面）
- 某一層寫空字串 `""` = 這個表情不顯示那一層
- 只有整張臉的角色（Chouma、Choupaca）每個表情是一整張圖，這裡改不到；新表情名會用 `idle`

## 2. 眨眼、視線、動嘴：`data/face.json`

| 區塊 | 參數 | 說明 |
|---|---|---|
| `blink` | `intervalMin`、`intervalMax` | 每隔幾秒眨一次（隨機） |
| | `duration` | 閉眼多久（秒） |
| | `wholeFaceExpressions` | 只有整張臉的角色，哪些表情會眨 |
| `gaze` | `eyeFollow`、`headFollow`、`bodyFollow` | 眼睛、頭、身體跟上的速度（越大越快） |
| | `headRatio`、`headMin`、`headMax` | 頭轉多少（游標角度 × 比例，限制在最小～最大度） |
| | `bodyRatio` | 身體跟著頭轉的比例 |
| | `faceShiftX`、`faceShiftY` | 看旁邊時臉移多少（轉頭的立體感） |
| | `range` | 游標離臉多遠以內才看（px）；半身模式不限 |
| | `lookDistance` | 游標離臉多遠算「看到底」（px） |
| | `stillLimit` | 游標幾秒沒動就看回前方 |
| | `glanceMin`、`glanceMax`、`glanceTimeMin`、`glanceTimeMax` | 每隔幾秒自己瞄別的地方、瞄多久 |
| | `lookActions` | 哪些動作會看游標 |
| `talk` | `openMin`～`closedMax`、`halfChance` | 沒有聲音分析時（Discord 講話、冒台詞）張嘴閉嘴的節奏 |
| | `wholeFace` | 只有整張臉的角色，嘴型對應哪張臉 |

瞳孔能移多遠：`art/characters/<角色>/rig.json` 的 `face.layers` 裡 `pupils` 的 `move`（[左, 上, 右, 下]），由 `tools/artgen/faces.py` 的 `PUPIL_MOVE` 產生。

## 3. 聲音觸發表情與動作、快捷鍵：`data/vtuber.json`

```json
{ "id": "happy", "when": { "semitones": 4, "forSeconds": 0.6 }, "face": "happy", "seconds": 1.5, "cooldown": 3 }
```

| 欄位 | 說明 |
|---|---|
| `when.jumpDb` | 音量比前 1 秒突然大這麼多 dB |
| `when.loudDb` | 比你平常講話大這麼多 dB |
| `when.semitones` | 音調比平常高（正數）或低（負數）幾個半音 |
| `when.silentSeconds` | 安靜幾秒 |
| `when.forSeconds` | 條件要持續幾秒才觸發 |
| `face`／`action` | 換成哪個表情（含你在 face.json 新增的）／做哪個動作（`data/animations/` 的檔名：wave、cheer、peace、stretch、think、shy、angry…） |
| `seconds` | 維持幾秒 |
| `cooldown` | 觸發後幾秒內不再觸發 |
| `priority` | 誰蓋過誰（預設 10；講話動嘴 20、快捷鍵 40） |

- 「平常」的音量、音調會自動學（Hub「🎥 半身」分頁可以看即時數值、按「重新校正」）
- **講話門檻**：Hub「🎥 半身」分頁的拉桿，音量超過才算在講話。預設跟著環境噪音自動調整；拉了就固定（存在 settings.json 的 `Vtuber.AutoFloor`、`NoiseFloor`），按「重新校正」回到自動。太容易動嘴（鍵盤、風扇聲）往右拉，講話沒反應往左拉
- 半身模式關掉後還要全身角色跟著講話動嘴：勾「半身模式關掉後，全身角色還是跟著我講話動嘴」（`Vtuber.MicWhenOff`，麥克風會保持開著）
- `mouth.openDb`：沒認出母音時，比平常小聲不到這麼多 dB 算「全開」，更小聲算「半開」
- `hotkeys`：`"Ctrl+Alt+1": { "face": "happy", "seconds": 3 }`，修飾鍵 Ctrl、Alt、Shift，按鍵 0～9、A～Z、F1～F12
- `bust.height`、`bust.margin`：半身預設高度、頭頂上面留多少

## 4. 角度（正面、側面、背面…）

- Hub「🎥 半身」分頁：角度拉桿（0°～359°）、正面／側面／背面／左側按鈕、「跟著游標轉身」
- 設定存在 `%APPDATA%\Godot\app_userdata\Desk Pet\settings.json` 的 `Vtuber.ViewYaw`、`AutoTurn`、`TurnRange`（跟著游標最多轉幾度）
- 換更細緻的正面、背面圖：見 [ART_GUIDE.md「其他視角」](ART_GUIDE.md#其他視角正面背面)

## 5. 想改程式

| 想改 | 檔案（桌寵 Godot） | 網頁 | 測試 |
|---|---|---|---|
| 表情怎麼組成（眨眼、嘴型、瞳孔位移的規則） | `godot/src/Game/Face/FaceDriver.cs` | `preview/js/game/faceDriver.js` | 兩邊要一樣：`tests/golden/face.json` |
| 視線（三層速度、瞄開、看回前方） | `godot/src/Game/Gaze.cs` | `preview/js/game/pet.js` 的 `lookAt` | |
| 聲音分析（音量、音調、母音） | `godot/src/Core/Voice/VoiceAnalyzer.cs` | `preview/js/core/voice.js` | `tests/golden/voice.json`、`preview/tests/voice.html` |
| 自動校正 | `godot/src/Core/Voice/VoiceCalibration.cs` | `preview/js/bust.js` | |
| 聲音規則怎麼判斷、嘴型 | `godot/src/Game/Vtuber/VoiceRules.cs` | `preview/js/bust.js`（只有嘴型） | `--selftest=vtuber` |
| 半身模式（取景、拖曳、角度、快捷鍵） | `godot/src/Features/Vtuber/VtuberFeature.cs` | `preview/js/bust.js` | |
| 依角度選視角 | `godot/src/Content/ContentLoader.cs` 的 `ViewFor` | `preview/js/core/loader.js` 的 `viewFor` | `tests/golden/views.json` |
| 測試視角圖怎麼拼 | `tools/artgen/views.py` | | `python tools/gen_art.py` |
| 誰蓋過誰（表情、動作的優先順序） | `godot/src/Game/Control/ControlArbiter.cs` | | `--selftest=control` |

兩邊都有的計算，改完跑共用測試：

```
cd godot
dotnet build
<Godot>_console.exe --headless --path . -- --selftest=golden --update     ← 桌寵重新產生答案
cd ..
powershell -File tools/web_test.ps1                                     ← 網頁要和答案一樣
```

半身模式不用麥克風也能測：`--sandbox --vtuber --vtuber-char=tk01 --vtuber-yaw=0 --vtuber-voice=talk:2,loud:0.3,high:1 --vtuber-shot=畫面.png --vtuber-shot-quit`（假的聲音劇本：quiet、talk、loud、high、low，冒號後面是秒數）。
