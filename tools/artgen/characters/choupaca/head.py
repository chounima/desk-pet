"""頭、耳朵、黑色頭毛與眼鏡。頭部骨骼在脖子根部（埋在身體毛裡），脖子是頭部圖的一部分。"""
from ...svg import Part
from .palette import EAR, EAR_IN, GLASSES, L, MUZZLE, OL, TUFT, TUFT_HL, WOOL_HL, WOOL_SHADE, cloud, fluff

HEAD_BOX = (-60, -140, 70, 24)
HEAD_C = (6, -76)       # 頭的中心
EYES = ((-5, -80), (20, -81))


def _ear(x: float, y: float, r: float) -> str:
    t = f'transform="rotate({r} {x} {y})"'
    return (f'<ellipse cx="{x}" cy="{y}" rx="9" ry="13" {t} fill="{EAR}" {L}/>'
            f'<ellipse cx="{x}" cy="{y + 2}" rx="4.5" ry="7" {t} fill="{EAR_IN}"/>')


def ears() -> str:
    return _ear(-20, -102, -25) + _ear(32, -104, 25)


def hair_back() -> Part:
    """圓耳朵和脖子畫在後面：脖子根部被身體的毛蓋住，只露出上半截。"""
    neck = "M-18,20 C-22,-4 -20,-30 -16,-52 L28,-52 C32,-30 30,-4 26,20 Z"
    ripples = "".join(
        f'<path d="M{x},{y} q{s * 6},5 0,10" fill="none" stroke="{OL}" stroke-width="2" stroke-linecap="round"/>'
        for x, y, s in ((-19, -38, -1), (28, -40, 1)))
    return Part(
        ears()
        + fluff(neck) + ripples
        + f'<path d="M-8,-46 C-6,-30 -6,-10 -4,10" fill="none" stroke="{WOOL_SHADE}" stroke-width="5" '
          f'stroke-linecap="round" opacity="0.6"/>',
        HEAD_BOX)


def head() -> Part:
    cx, cy = HEAD_C
    return Part(
        fluff(cloud(cx, cy, 31, 28, 15, 0.1, 0.3))
        + f'<ellipse cx="{cx - 14}" cy="{cy - 14}" rx="8" ry="5" fill="{WOOL_HL}" opacity="0.8"/>'
        # 吻部
        + f'<ellipse cx="11" cy="-64" rx="15" ry="10.5" fill="{MUZZLE}"/>',
        HEAD_BOX)


def hair_front() -> Part:
    """Chouma 的黑髮變成頭頂一撮黑毛，加上黑框眼鏡。"""
    frames = "".join(
        f'<rect x="{x - 9}" y="{y - 7}" width="18" height="14" rx="5" fill="#ffffff" fill-opacity="0.12" '
        f'stroke="{GLASSES}" stroke-width="2.6"/>' for x, y in EYES)
    return Part(
        fluff(cloud(6, -104, 19, 10, 9, 0.22, 3.4), TUFT)
        + f'<path d="M-4,-108 Q2,-112 10,-109" fill="none" stroke="{TUFT_HL}" stroke-width="2.5" stroke-linecap="round"/>'
        + frames
        + f'<path d="M4,-81 Q7.5,-84 11,-81" fill="none" stroke="{GLASSES}" stroke-width="2.4"/>'
        + f'<path d="M-14,-82 L-30,-86" stroke="{GLASSES}" stroke-width="2.4" stroke-linecap="round"/>',
        HEAD_BOX)
