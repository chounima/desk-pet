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


def _open_eye(i: int, st: FaceStyle, lid: str) -> str:
    cx, cy, rx = EYES[i]
    ry = EYE_RY
    inner = 1 if i == 0 else -1     # 往臉中心的方向
    iris_r = 0.55 if lid == "wide" else 0.78
    # 上眼瞼高度：越大蓋越多
    cover = {"normal": 0.0, "half": 0.4, "sharp": 0.3, "wide": -0.1}[lid]
    top = cy - ry * (1 - cover * 2)
    clip = f"e{i}"
    out = (
        f'<clipPath id="{clip}"><rect x="{cx - rx - 2}" y="{top:g}" width="{2 * rx + 4}" height="{ry * 3}"/></clipPath>'
        f'<g clip-path="url(#{clip})">'
        f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{st.dark}"/>'
        f'<ellipse cx="{cx}" cy="{cy + 3}" rx="{rx * iris_r:g}" ry="{ry * iris_r:g}" fill="{st.iris}"/>'
    )
    if st.slit:
        out += f'<ellipse cx="{cx}" cy="{cy + 2}" rx="{rx * 0.18:g}" ry="{ry * 0.55:g}" fill="{st.dark}"/>'
    out += (
        f'<ellipse cx="{cx}" cy="{cy + 8}" rx="{rx * 0.5:g}" ry="{ry * 0.25:g}" fill="{st.light}"/>'
        f'<circle cx="{cx - rx * 0.35 * inner:g}" cy="{cy - 4}" r="{rx * 0.3:g}" fill="#fff"/>'
        '</g>'
    )
    if lid == "sharp":   # 外高內低的認真眼神
        out += _lash(f"M{cx - (rx + 2) * inner},{top - 3:g} L{cx + (rx + 2) * inner},{top + 3:g}", st, 4.5)
    else:
        out += _lash(f"M{cx - rx - 2},{top + 3:g} Q{cx},{top - 4:g} {cx + rx + 2},{top + 2:g}", st)
    return out


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
        "O": f'<ellipse cx="9.5" cy="-16" rx="4.5" ry="6" fill="{st.mouth}"/>',
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
        "surprised": both_open("wide") + _blush(0.3, st) + _mouth("O", st),
        "focus": both_open("sharp") + _blush(0.2, st) + _mouth("flat", st),
        "shy": _closed("arc", st) + _blush(1.0, st) + _mouth(st.idle_mouth, st),
    }
    extras = "".join(st.extras)
    if scale == 1 and dy == 0:
        return {name: Part(body + extras, FACE_BOX) for name, body in bodies.items()}
    t = head_transform(scale, dy)
    box = scale_box(FACE_BOX, scale, dy)
    return {name: Part(f'<g transform="{t}">{body}{extras}</g>', box) for name, body in bodies.items()}
