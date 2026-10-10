"""可依動作顯示的道具。每個道具綁在某根骨骼上，有自己的位移與圖層順序。"""
from .balls import BALLS
from .handheld import book, guitar, mug
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


# 名稱: (產生函式, 骨骼, x, y, 圖層順序)
PROPS = {
    "laptop": (laptop, "hips", 46, -6, 15.5),
    **{name: (fn, "prop", 0, 0, 19) for name, fn in BALLS.items()},      # 排球與商店的球外觀
    "mug": (mug, "handNear", 0, 10, 18.5),
    "book": (book, "handNear", 4, 12, 18.5),
    "guitar": (guitar, "torso", 4, 0, 15.8),     # 畫在身體前面、近側手臂後面
    "keyboard": (keyboard, "root", 66, 0, 15.6),
}
