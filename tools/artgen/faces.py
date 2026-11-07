"""通用表情產生器：依角色的 FaceStyle 畫出 rig.EXPRESSIONS 的每一種表情。

座標為頭部骨骼局部座標（3/4 側面朝右）。
"""
from dataclasses import dataclass, field

from .svg import Part

FACE_BOX = (-60, -80, 80, 0)
# (中心 x, 中心 y, 半寬)；遠側眼睛較窄
EYES = ((-10, -42, 11), (26, -42, 9))
EYE_RY = 14


@dataclass
class FaceStyle:
    iris: str
    dark: str
    light: str
    mouth: str
    blush: str
    idle_lid: str = "normal"        # normal | half
    idle_mouth: str = "smile"       # smile | flat | cat
    slit: bool = False              # 貓眼直瞳
    extras: list = field(default_factory=list)   # 額外 SVG（淚痣、鬍鬚…）


def _lash(d: str, st: FaceStyle, w: float = 4) -> str:
    return f'<path d="{d}" fill="none" stroke="{st.dark}" stroke-width="{w:g}" stroke-linecap="round"/>'


# 上眼瞼高度：越大蓋越多
LID_COVER = {"normal": 0.0, "half": 0.4, "sharp": 0.3, "wide": -0.1}


def _eye_parts(i: int, st: FaceStyle, lid: str, clip_id: str) -> tuple[str, str, str, str]:
    """一隻睜開的眼睛：(裁切開頭, 眼眶, 瞳孔, 眼線)。眼眶和瞳孔都要包在裁切裡（被上眼瞼切掉），眼線畫在最上面。
    分層臉把眼眶、瞳孔、眼線放在不同圖層，瞳孔可以位移；整張臉則照順序畫在一起。"""
    cx, cy, rx = EYES[i]
    ry = EYE_RY
    inner = 1 if i == 0 else -1     # 往臉中心的方向
    iris_r = 0.55 if lid == "wide" else 0.78
    top = cy - ry * (1 - LID_COVER[lid] * 2)
    clip = (f'<clipPath id="{clip_id}{i}"><rect x="{cx - rx - 2}" y="{top:g}" width="{2 * rx + 4}" height="{ry * 3}"/></clipPath>'
            f'<g clip-path="url(#{clip_id}{i})">')
    shell = f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{st.dark}"/>'
    pupil = f'<ellipse cx="{cx}" cy="{cy + 3}" rx="{rx * iris_r:g}" ry="{ry * iris_r:g}" fill="{st.iris}"/>'
    if st.slit:
        pupil += f'<ellipse cx="{cx}" cy="{cy + 2}" rx="{rx * 0.18:g}" ry="{ry * 0.55:g}" fill="{st.dark}"/>'
    pupil += (
        f'<ellipse cx="{cx}" cy="{cy + 8}" rx="{rx * 0.5:g}" ry="{ry * 0.25:g}" fill="{st.light}"/>'
        f'<circle cx="{cx - rx * 0.35 * inner:g}" cy="{cy - 4}" r="{rx * 0.3:g}" fill="#fff"/>'
    )
    if lid == "sharp":   # 外高內低的認真眼神
        lash = _lash(f"M{cx - (rx + 2) * inner},{top - 3:g} L{cx + (rx + 2) * inner},{top + 3:g}", st, 4.5)
    else:
        lash = _lash(f"M{cx - rx - 2},{top + 3:g} Q{cx},{top - 4:g} {cx + rx + 2},{top + 2:g}", st)
    return clip, shell, pupil, lash


def _open_eye(i: int, st: FaceStyle, lid: str) -> str:
    clip, shell, pupil, lash = _eye_parts(i, st, lid, "e")
    return clip + shell + pupil + "</g>" + lash


def _closed(shape: str, st: FaceStyle) -> str:
    out = ""
    for cx, cy, rx in EYES:
        if shape == "line":      # 眨眼／睡覺：往下彎
            d = f"M{cx - rx},{cy} Q{cx},{cy + 6} {cx + rx},{cy}"
        else:                    # 開心：^ ^
            d = f"M{cx - rx},{cy + 4} Q{cx},{cy - 10} {cx + rx},{cy + 4}"
        out += _lash(d, st, 3.5)
    return out


def _mouth(kind: str, st: FaceStyle) -> str:
    line = f'fill="none" stroke="{st.mouth}" stroke-width="2.5" stroke-linecap="round"'
    return {
        "flat": f'<path d="M5,-18 L14,-18" {line}/>',
        "smile": f'<path d="M3,-20 Q9,-14 16,-20" {line}/>',
        "cat": f'<path d="M2,-19 Q5.5,-15 9,-19 Q12.5,-15 16,-19" {line}/>',
        "open": f'<path d="M3,-21 Q9.5,-10 16,-21 Z" fill="{st.mouth}" stroke="{st.mouth}" stroke-width="2" stroke-linejoin="round"/>',
        "o": f'<ellipse cx="9.5" cy="-17" rx="3" ry="3.5" fill="{st.mouth}"/>',
        "pout": f'<path d="M4,-16 Q9.5,-21 15,-16" {line}/>',
        "gasp": f'<ellipse cx="9.5" cy="-16" rx="4.5" ry="6" fill="{st.mouth}"/>',
    }[kind]


def _blush(opacity: float, st: FaceStyle) -> str:
    return (f'<ellipse cx="-22" cy="-27" rx="8" ry="3.5" fill="{st.blush}" opacity="{opacity}"/>'
            f'<ellipse cx="36" cy="-27" rx="7" ry="3.5" fill="{st.blush}" opacity="{opacity}"/>')


def head_transform(scale: float, dy: float) -> str:
    """頭部圖都以 Q 版大小繪製，正常比例的角色用這個縮小並往上移。"""
    return f"translate(0,{dy:g}) scale({scale:g})"


def scale_box(box: tuple, scale: float, dy: float) -> tuple:
    x0, y0, x1, y1 = box
    return (x0 * scale, y0 * scale + dy, x1 * scale, y1 * scale + dy)


def make_faces(st: FaceStyle, scale: float = 1, dy: float = 0) -> dict[str, Part]:
    both_open = lambda lid: _open_eye(0, st, lid) + _open_eye(1, st, lid)   # noqa: E731
    bodies = {
        "idle": both_open(st.idle_lid) + _blush(0.25, st) + _mouth(st.idle_mouth, st),
        "blink": _closed("line", st) + _blush(0.25, st) + _mouth(st.idle_mouth, st),
        "happy": _closed("arc", st) + _blush(0.7, st) + _mouth("open", st),
        "sleep": _closed("line", st) + _blush(0.4, st) + _mouth("o", st),
        "surprised": both_open("wide") + _blush(0.3, st) + _mouth("gasp", st),
        "focus": both_open("sharp") + _blush(0.2, st) + _mouth("flat", st),
        "shy": _closed("arc", st) + _blush(1.0, st) + _mouth(st.idle_mouth, st),
        "angry": both_open("sharp") + _blush(0.6, st) + _mouth("pout", st),
    }
    extras = "".join(st.extras)
    if scale == 1 and dy == 0:
        faces = Faces({name: Part(body + extras, FACE_BOX) for name, body in bodies.items()})
    else:
        t = head_transform(scale, dy)
        box = scale_box(FACE_BOX, scale, dy)
        faces = Faces({name: Part(f'<g transform="{t}">{body}{extras}</g>', box) for name, body in bodies.items()})
    faces.layers = make_face_layers(st, scale, dy)
    return faces


# ---------- 分層臉（第 3 階段）：眼眶、瞳孔、眼線、腮紅、嘴、額外裝飾分開，疊起來和整張臉一樣 ----------

# 表情 → (眼睛, 腮紅不透明度, 嘴型)；idle 的眼睛、嘴型依 FaceStyle
_EXPRESSIONS = {
    "idle": (None, 0.25, None),
    "blink": ("line", 0.25, None),
    "happy": ("arc", 0.7, "open"),
    "sleep": ("line", 0.4, "o"),
    "surprised": ("wide", 0.3, "gasp"),
    "focus": ("sharp", 0.2, "flat"),
    "shy": ("arc", 1.0, None),
    "angry": ("sharp", 0.6, "pout"),
}
# 睜眼的眼睛圖名（normal 叫 open）
_EYE_NAME = {"normal": "open", "half": "half", "sharp": "sharp", "wide": "wide"}
# 瞳孔可以位移的範圍 [左, 上, 右, 下]（頭部座標）：虹膜往下的空間很小，所以下面只給一點
PUPIL_MOVE = (-2.2, -2.4, 2.2, 0.4)


def _vowel(kind: str, st: FaceStyle) -> str:
    """講話的母音嘴型（あいうえお）"""
    fill = f'fill="{st.mouth}" stroke="{st.mouth}" stroke-width="1.5" stroke-linejoin="round"'
    return {
        "a": f'<path d="M3,-21 Q9.5,-6 16,-21 Q9.5,-23 3,-21 Z" {fill}/>',
        "i": f'<path d="M2,-20 Q9.5,-14 17,-20 Q9.5,-21.5 2,-20 Z" {fill}/>',
        "u": f'<ellipse cx="9.5" cy="-17.5" rx="2.5" ry="2.5" fill="{st.mouth}"/>',
        "e": f'<path d="M3,-20.5 Q9.5,-11 16,-20.5 Q9.5,-22 3,-20.5 Z" {fill}/>',
        "o": f'<ellipse cx="9.5" cy="-16.5" rx="4" ry="5" fill="{st.mouth}"/>',
    }[kind]


class Faces(dict):
    """make_faces 的結果：{表情: 整張臉}，另外帶著分層臉 .layers（build_character 寫進 rig.json 的 face）"""
    layers: dict | None = None


def make_face_layers(st: FaceStyle, scale: float = 1, dy: float = 0) -> dict:
    """分層臉：{ "parts": {檔名: Part}, "spec": rig.json 的 face 欄位（檔名相對角色資料夾）}"""
    wrap = (lambda body: f'<g transform="{head_transform(scale, dy)}">{body}</g>') if (scale, dy) != (1, 0) else (lambda body: body)
    box = scale_box(FACE_BOX, scale, dy) if (scale, dy) != (1, 0) else FACE_BOX
    layers: dict[str, dict[str, str]] = {"eyes": {}, "pupils": {}, "lids": {}, "blush": {}, "mouth": {}}
    for lid, name in _EYE_NAME.items():
        shell = pupil = lash = ""
        for i in range(2):
            clip, s, p, l = _eye_parts(i, st, lid, f"{name}_")
            shell += clip + s + "</g>"
            pupil += clip + p + "</g>"
            lash += l
        layers["eyes"][name] = shell
        layers["pupils"][name] = pupil
        layers["lids"][name] = lash
    layers["eyes"]["line"] = _closed("line", st)
    layers["eyes"]["arc"] = _closed("arc", st)
    layers["blush"]["on"] = _blush(1, st)
    for kind in ("flat", "smile", "cat", "open", "o", "pout", "gasp"):
        layers["mouth"][kind] = _mouth(kind, st)
    for v in "aiueo":
        layers["mouth"][f"v{v}"] = _vowel(v, st)
    if st.extras:
        layers["extras"] = {"on": "".join(st.extras)}

    parts, spec_layers = {}, []
    for layer, variants in layers.items():
        files = {}
        for v, body in variants.items():
            file = f"face_parts/{layer}_{v}.svg"
            parts[file] = Part(wrap(body), box)
            files[v] = file
        entry = {"name": layer, "variants": files}
        if layer in ("pupils", "lids"):
            entry["follow"] = "eyes"            # 跟著眼睛換圖；眼睛閉著（沒有同名圖）就不顯示
        if layer == "pupils":
            entry["move"] = [round(m * scale, 2) for m in PUPIL_MOVE]
        spec_layers.append(entry)

    idle_eye = _EYE_NAME[st.idle_lid]
    expressions, alpha = {}, {}
    for e, (eye, blush, mouth) in _EXPRESSIONS.items():
        expressions[e] = {"eyes": eye or idle_eye, "blush": "on", "mouth": mouth or st.idle_mouth}
        if st.extras:
            expressions[e]["extras"] = "on"
        alpha[e] = {"blush": blush}
    spec = {
        "box": list(box), "layers": spec_layers, "expressions": expressions, "alpha": alpha,
        # 講話時嘴巴管道的值 → 嘴型圖（closed = 用表情原本的嘴）
        "talk": {"half": "o", "open": "gasp", "a": "va", "i": "vi", "u": "vu", "e": "ve", "o": "vo"},
    }
    return {"parts": parts, "spec": spec}
