"""可依動作顯示的道具。每個道具綁在某根骨骼上，有自己的位移與圖層順序。"""
from .shapes import stroke
from .svg import Part

OL = "#24161b"
L = stroke(OL)


def laptop() -> Part:
    return Part(
        f'<path d="M14,-4 L28,-4 L14,-42 L2,-42 Z" fill="#4a4d57" {L}/>'
        f'<circle cx="12" cy="-24" r="2.5" fill="#cfd3dc"/>'
        f'<path d="M-26,-6 L28,-6 L30,0 L-28,0 Z" fill="#9aa0ac" {L}/>'
        f'<path d="M-20,-4 L18,-4" stroke="#6b707c" stroke-width="2" stroke-dasharray="3 2"/>',
        (-34, -48, 36, 6))


def keyboard() -> Part:
    """放在地上的小鍵盤（四足角色打字時用前蹄踩）。"""
    keys = "".join(f'<rect x="{x}" y="{y}" width="5" height="3" rx="0.8" fill="#e6e8ee"/>'
                   for y in (-7.5, -3.5) for x in range(-22, 20, 7))
    return Part(
        f'<path d="M-26,-10 L26,-10 L28,0 L-28,0 Z" fill="#5a5e6a" {L}/>' + keys,
        (-32, -14, 32, 4))


BALL_R = 14     # 排球半徑（Q 版單位）；對打小遊戲依此換算顯示大小


def volleyball() -> Part:
    return Part(
        f'<circle cx="0" cy="0" r="{BALL_R}" fill="#f7f5ee" stroke="#24161b" stroke-width="2.2"/>'
        '<path d="M-13,-4.5 Q0,-9.5 13,-4.5 M-8,11 Q-3,0 -9,-11 M8,11.5 Q3,0 9,-11" '
        'fill="none" stroke="#2f6fd6" stroke-width="3.2" stroke-linecap="round"/>'
        '<path d="M-13,-4.5 Q0,-9.5 13,-4.5" fill="none" stroke="#f2c230" stroke-width="1.8"/>'
        '<path d="M-12,4 Q0,9 12,4" fill="none" stroke="#f2c230" stroke-width="1.6"/>'
        '<circle cx="-4.5" cy="-6" r="3.4" fill="#fff" opacity="0.8"/>',
        (-17, -17, 17, 17))


# 名稱: (產生函式, 骨骼, x, y, 圖層順序)
PROPS = {
    "laptop": (laptop, "hips", 46, -6, 15.5),
    "volleyball": (volleyball, "prop", 0, 0, 19),
    "keyboard": (keyboard, "root", 66, 0, 15.6),
}
