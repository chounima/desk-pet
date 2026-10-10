"""Choupaca 的色票與羊駝專用的形狀工具。"""
import math

from ...shapes import stroke

OL = "#5a3e2e"
WOOL, WOOL_SHADE, WOOL_HL = "#e8cfaa", "#d2b28a", "#f6e6cc"
MUZZLE = "#fbf1e2"
EAR, EAR_IN = "#8b5e3c", "#e8a99a"
TUFT, TUFT_HL = "#2a2626", "#4a4442"       # Chouma 的黑髮
GLASSES = "#151515"                        # Chouma 的黑框眼鏡
RED, SOLE = "#e0352a", "#f3eee6"           # Chouma 的紅白排球鞋
EYE, MOUTH, NOSE, BLUSH = "#3a2418", "#7a3a2a", "#c97a6e", "#f09a8a"
VEIN = "#e0453a"

L = stroke(OL, 2.5)


def cloud(cx: float, cy: float, rx: float, ry: float, n: int, bump: float = 0.14, start: float = 0) -> str:
    """毛茸茸的雲朵外框：沿橢圓分成 n 段，每段往外鼓成一個小弧。"""
    def at(a: float, k: float = 1) -> tuple:
        return cx + rx * k * math.cos(a), cy + ry * k * math.sin(a)

    step = 2 * math.pi / n
    x, y = at(start)
    d = f"M{x:.1f},{y:.1f}"
    for i in range(n):
        a0 = start + i * step
        qx, qy = at(a0 + step / 2, 1 + bump)
        x, y = at(a0 + step)
        d += f" Q{qx:.1f},{qy:.1f} {x:.1f},{y:.1f}"
    return d + " Z"


def fluff(d: str, fill: str = WOOL, ol: str = OL, sw: float = 2.5) -> str:
    return f'<path d="{d}" fill="{fill}" {stroke(ol, sw)}/>'
