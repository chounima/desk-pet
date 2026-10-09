"""共用的繪圖小工具。座標皆為骨骼局部座標（關節在原點，肢體往 +y 延伸）。"""


def stroke(color: str, width: float = 3) -> str:
    return (f'stroke="{color}" stroke-width="{width:g}" '
            'stroke-linejoin="round" stroke-linecap="round"')


def capsule(length: float, w0: float, w1: float, fill: str, ol: str, sw: float = 3) -> str:
    """從 (0,0) 往下到 (0,length) 的膠囊形肢體，上寬 w0、下寬 w1。"""
    r0, r1 = w0 / 2, w1 / 2
    d = (f"M{-r0:g},0 L{-r1:g},{length:g} A{r1:g},{r1:g} 0 0 0 {r1:g},{length:g} "
         f"L{r0:g},0 A{r0:g},{r0:g} 0 0 0 {-r0:g},0 Z")
    return f'<path d="{d}" fill="{fill}" {stroke(ol, sw)}/>'


def capsule_box(length: float, w0: float, w1: float, pad: float = 4) -> tuple:
    r = max(w0, w1) / 2
    return (-r - pad, -w0 / 2 - pad, r + pad, length + w1 / 2 + pad)


def hgrad(gid: str, *stops: str) -> str:
    """水平漸層（左→右），用來做圓柱狀肢體的明暗。"""
    n = len(stops) - 1
    s = "".join(f'<stop offset="{i / n:g}" stop-color="{c}"/>' for i, c in enumerate(stops))
    return f'<defs><linearGradient id="{gid}" x1="0" x2="1" y1="0" y2="0">{s}</linearGradient></defs>'


def dots(points, r: float, fill: str) -> str:
    return "".join(f'<circle cx="{x:g}" cy="{y:g}" r="{r:g}" fill="{fill}"/>' for x, y in points)


# 頭部共用的臉型（3/4 側面朝右，脖子在原點）
FACE_PATH = ("M-38,-58 C-40,-98 52,-100 54,-58 C56,-24 36,0 10,2 "
             "C-16,2 -40,-22 -38,-58 Z")
HEAD_BOX = (-80, -170, 90, 110)
