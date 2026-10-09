"""Chouma 專屬表情：眉毛、鼻子、男性眼型、露齒笑。對應設定圖的開心／認真／驚訝／害羞。"""
from ...faces import FACE_BOX, head_transform, scale_box
from ...svg import Part
from .palette import (BLUSH, EYE_DARK, EYE_IRIS, EYE_LIGHT, HAIR, HEAD_DY, HEAD_SCALE,
                      MOUTH, MOUTH_IN, OL, SKIN, SKIN_DEEP, TONGUE)

# (中心 x, 中心 y, 半寬, 半高)
EYES = ((-10, -42, 9, 7.5), (26, -42, 7.5, 7.5))


def _line(d: str, color: str = EYE_DARK, w: float = 3.2, opacity: float = 1) -> str:
    return (f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{w:g}" '
            f'stroke-linecap="round" stroke-linejoin="round" opacity="{opacity:g}"/>')


def _eye(i: int, look: float = 0, lid: str = "normal") -> str:
    """lid: normal | narrow（認真）| wide（驚訝）"""
    cx, cy, rx, ry = EYES[i]
    if lid == "wide":
        ry *= 1.25
    sclera = f"M{cx - rx},{cy} Q{cx},{cy - ry * 1.5:g} {cx + rx},{cy} Q{cx},{cy + ry * 1.15:g} {cx - rx},{cy} Z"
    iris_r = ry * (0.62 if lid == "wide" else 0.92)
    top = cy - ry * (0.35 if lid == "narrow" else 1.2)
    ix = cx + look
    return (
        f'<clipPath id="eye{i}"><path d="{sclera}"/></clipPath>'
        f'<clipPath id="lid{i}"><rect x="{cx - rx - 2}" y="{top:g}" width="{rx * 2 + 4}" height="{ry * 3}"/></clipPath>'
        f'<g clip-path="url(#lid{i})"><g clip-path="url(#eye{i})">'
        f'<path d="{sclera}" fill="#fbf7f2"/>'
        f'<circle cx="{ix:g}" cy="{cy + 0.5:g}" r="{iris_r:g}" fill="{EYE_IRIS}"/>'
        f'<path d="M{ix - iris_r * 0.8:g},{cy + iris_r * 0.4:g} Q{ix:g},{cy + iris_r * 1.1:g} {ix + iris_r * 0.8:g},{cy + iris_r * 0.4:g}" '
        f'fill="none" stroke="{EYE_LIGHT}" stroke-width="2"/>'
        f'<circle cx="{ix:g}" cy="{cy + 0.5:g}" r="{iris_r * 0.45:g}" fill="{EYE_DARK}"/>'
        f'<circle cx="{ix - 2:g}" cy="{cy - 2:g}" r="1.9" fill="#fff"/>'
        '</g></g>'
        + _line(f"M{cx - rx - 1},{top + ry * 0.9 if lid == 'narrow' else cy + 1:g} "
                f"Q{cx},{top - 1 if lid == 'narrow' else cy - ry * 1.5:g} {cx + rx + 1},{top + ry * 0.7 if lid == 'narrow' else cy - 1:g}")
        + _line(f"M{cx - rx * 0.6:g},{cy + ry * 0.9:g} Q{cx},{cy + ry * 1.15:g} {cx + rx * 0.6:g},{cy + ry * 0.8:g}",
                w=1.2, opacity=0.6)
    )


def _closed(kind: str) -> str:
    out = ""
    for cx, cy, rx, _ in EYES:
        if kind == "smile":     # ^ ^ 瞇眼笑
            out += _line(f"M{cx - rx - 1},{cy + 2} Q{cx},{cy - 8} {cx + rx + 1},{cy + 2}", w=3.4)
            out += _line(f"M{cx - rx * 0.6:g},{cy + 5} Q{cx},{cy + 7} {cx + rx * 0.6:g},{cy + 5}", w=1.3, opacity=0.6)
        else:                   # 眨眼／睡覺
            out += _line(f"M{cx - rx - 1},{cy} Q{cx},{cy + 4} {cx + rx + 1},{cy}", w=3.2)
    return out


BROWS = {
    "normal": ("M-22,-57 Q-11,-62 1,-59", "M17,-59 Q26,-62 35,-57"),
    "happy": ("M-22,-58 Q-11,-64 1,-60", "M17,-60 Q26,-64 35,-58"),
    "serious": ("M-22,-60 L1,-56", "M17,-56 L35,-60"),
    "raised": ("M-22,-62 Q-11,-69 1,-64", "M17,-64 Q26,-69 35,-62"),
    "worried": ("M-22,-57 Q-11,-59 1,-63", "M17,-63 Q26,-59 35,-57"),
}


def _brows(kind: str) -> str:
    return "".join(_line(d, HAIR, 4.2) for d in BROWS[kind])


NOSE = _line("M15,-31 Q18,-25 14,-23", SKIN_DEEP, 2)


def _blush(opacity: float, hatch: bool = False) -> str:
    out = (f'<ellipse cx="-20" cy="-27" rx="8" ry="3.5" fill="{BLUSH}" opacity="{opacity:g}"/>'
           f'<ellipse cx="37" cy="-27" rx="6.5" ry="3.5" fill="{BLUSH}" opacity="{opacity:g}"/>')
    if hatch:   # 害羞的斜線
        out += _line("M-25,-30 L-22,-24 M-20,-30 L-17,-24 M-15,-30 L-12,-24 M33,-30 L35,-25 M37,-30 L39,-25",
                     BLUSH, 1.6)
    return out


MOUTHS = {
    "smile": _line("M2,-13 Q10,-7 19,-13", MOUTH, 2.2) + _line("M0,-14 L2,-13", MOUTH, 1.2, 0.6),
    "grin": (
        f'<path d="M-1,-15 Q10,1 22,-15 Q10,-11 -1,-15 Z" fill="{MOUTH_IN}" stroke="{OL}" stroke-width="1.6" stroke-linejoin="round"/>'
        '<path d="M0.5,-14.6 Q10,-11 20.5,-14.6 Q20,-12.2 19,-11.4 Q10,-8.6 1.6,-11.4 Q0.8,-12.4 0.5,-14.6 Z" fill="#fff"/>'
        f'<path d="M6,-5.5 Q10,-3.5 14,-5.5 Q10,-7 6,-5.5 Z" fill="{TONGUE}"/>'
    ),
    "flat": _line("M3,-12 L17,-12.5", MOUTH, 2.2),
    "open": (f'<ellipse cx="10" cy="-10" rx="5.5" ry="6.5" fill="{MOUTH_IN}" stroke="{OL}" stroke-width="1.6"/>'
             '<path d="M5.5,-13 Q10,-15.5 14.5,-13 L14,-12 Q10,-13.5 6,-12 Z" fill="#fff"/>'),
    "o": f'<ellipse cx="10" cy="-11" rx="3" ry="3.5" fill="{MOUTH_IN}"/>',
    "wavy": _line("M1,-12 Q6,-9 10,-12 Q14,-9 19,-12", MOUTH, 2.2),
    "pout": _line("M3,-10 Q10,-15 17,-10", MOUTH, 2.4),
}

SWEAT = ('<path d="M50,-72 C46,-64 44,-60 44,-57 C44,-53 47,-51 50,-51 C53,-51 56,-53 56,-57 '
         'C56,-60 54,-64 50,-72 Z" fill="#cdeeff" stroke="#4a8fc4" stroke-width="1.8"/>')


def _face(*parts: str) -> Part:
    t = head_transform(HEAD_SCALE, HEAD_DY)
    return Part(f'<g transform="{t}">{"".join(parts)}</g>', scale_box(FACE_BOX, HEAD_SCALE, HEAD_DY))


def make() -> dict[str, Part]:
    both = lambda **kw: _eye(0, **kw) + _eye(1, **kw)   # noqa: E731
    return {
        "idle": _face(_brows("normal"), both(), NOSE, _blush(0.15), MOUTHS["smile"]),
        "blink": _face(_brows("normal"), _closed("line"), NOSE, _blush(0.15), MOUTHS["smile"]),
        "happy": _face(_brows("happy"), _closed("smile"), NOSE, _blush(0.35), MOUTHS["grin"]),
        "sleep": _face(_brows("normal"), _closed("line"), NOSE, _blush(0.2), MOUTHS["o"]),
        "surprised": _face(_brows("raised"), both(lid="wide"), NOSE, _blush(0.15), MOUTHS["open"], SWEAT),
        "focus": _face(_brows("serious"), both(lid="narrow"), NOSE, MOUTHS["flat"]),
        "shy": _face(_brows("worried"), both(look=-3), NOSE, _blush(0.7, hatch=True), MOUTHS["wavy"]),
        "angry": _face(_brows("serious"), both(lid="narrow"), NOSE, _blush(0.35), MOUTHS["pout"]),
    }
