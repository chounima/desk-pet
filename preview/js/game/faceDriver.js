// 分層臉的表情驅動（和桌寵 Game/FaceDriver.cs 同一套規則，tests/golden/face.json 比對）
// 輸入：rig.face 與 { expression, blink, mouth, eyes, look: [x, y] }
//   expression：表情名（idle、happy…），沒有的用 idle
//   blink：這一刻在眨眼（只有睜眼的表情會閉眼；睜眼 = 眼睛圖有跟隨圖層的同名圖，例如瞳孔）
//   mouth：嘴巴管道的值（closed、half、open、a、i、u、e、o），透過 face.talk 換成嘴型圖；closed 或沒有 = 用表情的嘴
//   eyes：眼睛管道的值、或動作的 eyes 軌道（直接指定眼睛圖，例如 half）
//   mouthArt：動作的 mouth 軌道（直接指定嘴巴圖；講話時被 mouth 蓋過）
//   look：視線 -1～1（右、下為正），瞳孔依圖層的 move [左, 上, 右, 下] 位移
// 輸出：照圖層順序 [{ name, file, alpha, dx, dy }]，file = null 表示這層不顯示

export function faceLayers(face, state) {
  const exprName = face.expressions[state.expression] ? state.expression : "idle";
  const expr = face.expressions[exprName];
  const alpha = face.alpha?.[exprName] ?? {};
  const eyesLayer = face.layers.find(l => l.name === "eyes");
  const chosen = { ...expr };

  if (state.eyes && eyesLayer?.variants[state.eyes]) chosen.eyes = state.eyes;
  if (state.blink && eyesLayer?.variants.line && isOpen(face, chosen.eyes)) chosen.eyes = "line";
  const mouthLayer = face.layers.find(l => l.name === "mouth");
  if (state.mouthArt && mouthLayer?.variants[state.mouthArt]) chosen.mouth = state.mouthArt;
  const talk = state.mouth && state.mouth !== "closed" ? face.talk?.[state.mouth] : null;
  if (talk) chosen.mouth = talk;

  const [lx, ly] = state.look ?? [0, 0];
  return face.layers.map(l => {
    const variant = l.follow ? chosen[l.follow] : chosen[l.name];
    const file = (variant != null && l.variants[variant]) || null;
    let dx = 0, dy = 0;
    if (l.move && file) {
      dx = lx < 0 ? -lx * l.move[0] : lx * l.move[2];
      dy = ly < 0 ? -ly * l.move[1] : ly * l.move[3];
    }
    return { name: l.name, file, alpha: alpha[l.name] ?? 1, dx, dy };
  });
}

// 睜開的眼睛：有圖層跟著眼睛、而且有這個眼睛的同名圖（瞳孔、眼線）
function isOpen(face, eyes) {
  return face.layers.some(l => l.follow === "eyes" && l.variants[eyes]);
}
