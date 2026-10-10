"""配件（商店商品，所有角色共用）。每個配件掛在角色的一個錨點上（見 rig.anchors）：

  hat  頭頂中央（帽子、髮飾）     eyes 兩眼中間（眼鏡）
  neck 脖子根部（圍巾、領結）     back 背上（翅膀、背包）

座標以錨點為原點、Q 版大小繪製；正常頭身的角色由錨點的 scale 縮放。
"""
from .shapes import stroke
from .svg import Part

OL = "#24161b"
L = stroke(OL, 2.2)

# 配件部位 → (錨點, 圖層順序)。同一部位一次只能戴一個
SLOTS = {
    "head": ("hat", 15.5),       # 瀏海上面、近側手臂下面
    "face": ("eyes", 14.6),      # 表情上面、瀏海下面
    "neck": ("neck", 12.6),      # 軀幹上面、頭下面
    "back": ("back", 0.3),       # 最後面
}


def strawhat() -> Part:
    return Part(
        f'<ellipse cx="0" cy="4" rx="58" ry="12" fill="#f2d27a" {L}/>'
        f'<path d="M-30,4 C-30,-26 30,-26 30,4 Z" fill="#f6dc8c" {L}/>'
        '<path d="M-30,-2 C-10,4 10,4 30,-2 L30,4 C10,10 -10,10 -30,4 Z" fill="#e0453a"/>'
        '<path d="M-48,2 Q0,10 48,2 M-22,-12 Q0,-16 22,-12" fill="none" stroke="#c9a24e" stroke-width="1.5"/>',
        (-62, -28, 62, 18))


def catears() -> Part:
    ear = lambda x, flip: (   # noqa: E731
        f'<path d="M{x - 14 * flip},6 L{x},-28 L{x + 14 * flip},4 Z" fill="#2b2b33" {L}/>'
        f'<path d="M{x - 8 * flip},2 L{x},-18 L{x + 8 * flip},2 Z" fill="#ff9fb4"/>')
    return Part(
        f'<path d="M-40,10 C-40,-14 40,-14 40,10" fill="none" stroke="#2b2b33" stroke-width="5" stroke-linecap="round"/>'
        + ear(-22, 1) + ear(24, 1),
        (-46, -32, 46, 14))


def ribbon() -> Part:
    return Part(
        f'<path d="M0,0 L-24,-14 L-26,12 Z" fill="#ff6b8a" {L}/>'
        f'<path d="M0,0 L24,-14 L26,12 Z" fill="#ff6b8a" {L}/>'
        f'<circle cx="0" cy="0" r="6" fill="#e04a6c" {L}/>',
        (-30, -18, 30, 16))


def crown() -> Part:
    return Part(
        f'<path d="M-24,6 L-26,-18 L-12,-6 L0,-24 L12,-6 L26,-18 L24,6 Z" fill="#ffcf3a" {L}/>'
        '<circle cx="0" cy="-2" r="3.5" fill="#e0453a"/><circle cx="-14" cy="0" r="2.5" fill="#3fa7ff"/>'
        '<circle cx="14" cy="0" r="2.5" fill="#3fa7ff"/>',
        (-30, -28, 30, 10))


def roundglasses() -> Part:
    return Part(
        f'<circle cx="-18" cy="0" r="12" fill="#ffffff" fill-opacity="0.15" {stroke("#7a4a1c", 2.6)}/>'
        f'<circle cx="18" cy="0" r="11" fill="#ffffff" fill-opacity="0.15" {stroke("#7a4a1c", 2.6)}/>'
        '<path d="M-6,-1 Q0,-5 7,-1" fill="none" stroke="#7a4a1c" stroke-width="2.4"/>'
        '<path d="M-30,-2 L-44,-6" stroke="#7a4a1c" stroke-width="2.4" stroke-linecap="round"/>',
        (-48, -16, 32, 14))


def sunglasses() -> Part:
    return Part(
        f'<path d="M-32,-8 L-6,-8 L-8,6 Q-20,12 -30,4 Z" fill="#1e2130" {L}/>'
        f'<path d="M6,-8 L30,-8 L28,4 Q18,10 8,5 Z" fill="#1e2130" {L}/>'
        '<path d="M-6,-6 L6,-6" stroke="#1e2130" stroke-width="3"/>'
        '<path d="M-28,-5 L-20,-5 M10,-5 L18,-5" stroke="#ffffff" stroke-width="2" opacity="0.6" stroke-linecap="round"/>',
        (-36, -12, 34, 12))


def scarf() -> Part:
    return Part(
        f'<path d="M-22,-6 Q0,6 24,-6 L24,6 Q0,18 -22,6 Z" fill="#e0453a" {L}/>'
        f'<path d="M-14,4 L-20,34 L-8,34 L-4,6 Z" fill="#c9352b" {L}/>'
        '<path d="M-20,30 L-8,30 M-19,26 L-7,26" stroke="#ffffff" stroke-width="2" opacity="0.7"/>',
        (-28, -12, 30, 38))


def wings() -> Part:
    feather = "M0,0 C-20,-30 -60,-40 -76,-20 C-60,-18 -56,-8 -66,0 C-50,0 -46,8 -54,16 C-36,12 -20,10 0,0 Z"
    return Part(
        f'<path d="{feather}" fill="#ffffff" {stroke("#9fb8e8", 2.2)}/>'
        '<path d="M-10,-2 C-30,-14 -50,-20 -64,-14 M-12,2 C-30,0 -44,2 -56,8" fill="none" stroke="#c8d8f4" stroke-width="1.6"/>',
        (-80, -40, 4, 20))


# 名稱: (產生函式, 部位)
ACCESSORIES = {
    "strawhat": (strawhat, "head"),
    "catears": (catears, "head"),
    "ribbon": (ribbon, "head"),
    "crown": (crown, "head"),
    "roundglasses": (roundglasses, "face"),
    "sunglasses": (sunglasses, "face"),
    "scarf": (scarf, "neck"),
    "wings": (wings, "back"),
}
