"""表情：小眼睛、平平的眼皮，預設就是一臉不爽。"""
from ...svg import Part
from .head import EYES
from .palette import BLUSH, EYE, MOUTH, NOSE, VEIN

FACE_BOX = (-40, -110, 60, -46)


def _line(d: str, color: str = EYE, w: float = 2.6, opacity: float = 1) -> str:
    return (f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{w:g}" '
            f'stroke-linecap="round" stroke-linejoin="round" opacity="{opacity:g}"/>')


def _half(x: float, y: float, r: float) -> str:
    """被眼皮蓋住上半的眼珠。"""
    return f'<path d="M{x - r:g},{y:g} L{x + r:g},{y:g} A{r:g},{r:g} 0 0 1 {x - r:g},{y:g} Z" fill="{EYE}"/>'


def _eyes(kind: str) -> str:
    out = ""
    for i, (x, y) in enumerate(EYES):
        inner = 1 if i == 0 else -1          # 往臉中心的方向
        if kind == "grumpy":                 # 眼皮壓一半，看起來很不耐煩
            out += _half(x, y - 1, 4.5) + _line(f"M{x - 6},{y - 1 - 1.5 * inner} L{x + 6},{y - 1 + 1.5 * inner}", w=2.8)
        elif kind == "angry":                # 眉毛倒豎
            out += _half(x, y, 4) + _line(f"M{x - 6 * inner},{y - 9} L{x + 6 * inner},{y - 4}", w=3)
        elif kind == "narrow":
            out += f'<rect x="{x - 4}" y="{y - 1.5}" width="8" height="3.5" rx="1.5" fill="{EYE}"/>'
        elif kind == "wide":
            out += f'<circle cx="{x}" cy="{y}" r="4" fill="{EYE}"/><circle cx="{x - 1.2}" cy="{y - 1.3}" r="1.3" fill="#fff"/>'
        elif kind == "line":
            out += _line(f"M{x - 5},{y} L{x + 5},{y}")
        elif kind == "sleep":                # ︶
            out += _line(f"M{x - 5},{y - 1} Q{x},{y + 4} {x + 5},{y - 1}")
        elif kind == "arc":                  # ^
            out += _line(f"M{x - 5},{y + 2} Q{x},{y - 5} {x + 5},{y + 2}")
        elif kind == "squeeze":              # > <
            out += _line(f"M{x - 4 * inner},{y - 4} L{x + 4 * inner},{y} L{x - 4 * inner},{y + 4}")
    return out


_NOSE = f'<ellipse cx="13" cy="-68" rx="3.2" ry="2.2" fill="{NOSE}"/>'

MOUTHS = {
    "frown": _line("M7,-61 Q12,-64.5 17,-61", MOUTH, 2.2),
    "flat": _line("M8,-62 L17,-62", MOUTH, 2.2),
    "w": _line("M6,-63 Q8.5,-60 11,-63 Q13.5,-60 16,-63", MOUTH, 2),
    "o": f'<ellipse cx="12" cy="-61" rx="2.2" ry="2.6" fill="{MOUTH}"/>',
    "O": f'<ellipse cx="12" cy="-60" rx="3.5" ry="4.5" fill="{MOUTH}"/>',
    "wavy": _line("M6,-61 Q8,-63 10,-61 Q12,-59 14,-61 Q16,-63 18,-61", MOUTH, 1.8),
    # 張嘴大叫（要吐口水了）
    "yell": (f'<path d="M6,-64 L18,-64 Q17,-55 12,-55 Q7,-55 6,-64 Z" fill="{MOUTH}"/>'
             '<path d="M7,-64 L17,-64 L16.5,-62.2 L7.5,-62.2 Z" fill="#fff"/>'),
}


def _blush(opacity: float) -> str:
    return (f'<ellipse cx="-14" cy="-68" rx="6" ry="3" fill="{BLUSH}" opacity="{opacity:g}"/>'
            f'<ellipse cx="31" cy="-69" rx="5" ry="3" fill="{BLUSH}" opacity="{opacity:g}"/>')


# 生氣的青筋（十字）
_VEIN = _line("M30,-100 q3,3 0,6 M36,-100 q-3,3 0,6 M30,-100 q3,-1 6,0 M30,-94 q3,1 6,0", VEIN, 2)


def _face(*parts: str) -> Part:
    return Part("".join(parts) + _NOSE, FACE_BOX)


def make() -> dict[str, Part]:
    return {
        "idle": _face(_eyes("grumpy"), _blush(0.2), MOUTHS["frown"]),
        "blink": _face(_eyes("line"), _blush(0.2), MOUTHS["frown"]),
        "happy": _face(_eyes("arc"), _blush(0.6), MOUTHS["w"]),       # 嘴上不說，其實很開心
        "sleep": _face(_eyes("sleep"), _blush(0.3), MOUTHS["o"]),
        "surprised": _face(_eyes("wide"), _blush(0.2), MOUTHS["O"]),
        "focus": _face(_eyes("narrow"), MOUTHS["flat"]),
        "shy": _face(_eyes("squeeze"), _blush(1.0), MOUTHS["wavy"]),
        "angry": _face(_eyes("angry"), _blush(0.5), MOUTHS["yell"], _VEIN),
    }
