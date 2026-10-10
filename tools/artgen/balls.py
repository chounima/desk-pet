"""排球對打的球外觀（商店商品）。都和經典排球一樣大（半徑 BALL_R），換外觀不影響手感。"""
from .svg import Part

BALL_R = 14     # 排球半徑（Q 版單位）；對打小遊戲依此換算顯示大小
BOX = (-17, -17, 17, 17)
OL = "#24161b"


def _ball(fill: str, inner: str, ol: str = OL) -> Part:
    clip = '<clipPath id="ballclip"><circle cx="0" cy="0" r="14"/></clipPath>'
    return Part(
        f'{clip}<circle cx="0" cy="0" r="{BALL_R}" fill="{fill}"/>'
        f'<g clip-path="url(#ballclip)">{inner}</g>'
        f'<circle cx="0" cy="0" r="{BALL_R}" fill="none" stroke="{ol}" stroke-width="2.2"/>'
        '<circle cx="-4.5" cy="-6" r="3.4" fill="#fff" opacity="0.7"/>',
        BOX)


def volleyball() -> Part:
    return _ball(
        "#f7f5ee",
        '<path d="M-13,-4.5 Q0,-9.5 13,-4.5 M-8,11 Q-3,0 -9,-11 M8,11.5 Q3,0 9,-11" '
        'fill="none" stroke="#2f6fd6" stroke-width="3.2" stroke-linecap="round"/>'
        '<path d="M-13,-4.5 Q0,-9.5 13,-4.5" fill="none" stroke="#f2c230" stroke-width="1.8"/>'
        '<path d="M-12,4 Q0,9 12,4" fill="none" stroke="#f2c230" stroke-width="1.6"/>')


def beach() -> Part:
    """沙灘球：六片彩色扇形。"""
    colors = ["#ff5a5a", "#ffffff", "#ffd23f", "#ffffff", "#3fa7ff", "#ffffff"]
    import math
    slices = ""
    for i, c in enumerate(colors):
        a0, a1 = i * math.pi / 3, (i + 1) * math.pi / 3
        x0, y0, x1, y1 = 16 * math.cos(a0), 16 * math.sin(a0), 16 * math.cos(a1), 16 * math.sin(a1)
        slices += f'<path d="M0,0 L{x0:.1f},{y0:.1f} A16,16 0 0 1 {x1:.1f},{y1:.1f} Z" fill="{c}"/>'
    return _ball("#fff", slices + '<circle cx="0" cy="0" r="3" fill="#ff5a5a"/>')


def basket() -> Part:
    """籃球紋。"""
    line = 'fill="none" stroke="#2a1a10" stroke-width="1.6"'
    return _ball("#e8752a", f'<path d="M-14,0 L14,0 M0,-14 L0,14" {line}/>'
                            f'<path d="M-9,-11 Q-3,0 -9,11 M9,-11 Q3,0 9,11" {line}/>')


def watermelon() -> Part:
    """西瓜球：綠色條紋。"""
    stripes = "".join(f'<path d="M{x},-15 Q{x + 4},0 {x},15" fill="none" stroke="#1f6b2a" stroke-width="3"/>'
                      for x in (-10, -3, 4, 11))
    return _ball("#5fbf4a", stripes, "#1b3a14")


def fire() -> Part:
    """火焰球：橘紅漸層加火紋。"""
    grad = ('<defs><radialGradient id="fireg" cx="0.4" cy="0.35" r="0.7">'
            '<stop offset="0" stop-color="#fff3a0"/><stop offset="0.5" stop-color="#ff9a2a"/>'
            '<stop offset="1" stop-color="#d9311f"/></radialGradient></defs>')
    flames = ('<path d="M-12,8 Q-6,-2 -8,-12 Q-2,-4 0,-14 Q3,-4 8,-11 Q7,0 12,6" '
              'fill="none" stroke="#ffe066" stroke-width="2" stroke-linejoin="round"/>')
    return Part(
        grad + f'<circle cx="0" cy="0" r="{BALL_R}" fill="url(#fireg)"/>' + flames
        + f'<circle cx="0" cy="0" r="{BALL_R}" fill="none" stroke="#7a1a10" stroke-width="2.2"/>',
        BOX)


# 道具名稱: 產生函式（都綁在 prop 骨骼、圖層 19，見 props.PROPS）
BALLS = {
    "volleyball": volleyball,
    "ball_beach": beach,
    "ball_basket": basket,
    "ball_watermelon": watermelon,
    "ball_fire": fire,
}
