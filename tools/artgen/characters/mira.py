"""Mira：蒸汽龐克塗鴉滑板師（正常頭身）。

銀白短髮、機械貓耳頭飾與護目鏡、白襯衫＋皮革馬甲、黑短褲、吊襪帶、黑色過膝襪、
短靴、無指手套、背上的機械滑板、腰後的長披布。
⚠ 外觀參考他人原創設計，僅供原型使用；正式版需改為自有設計或取得授權。
"""
import math

from ..faces import FaceStyle, head_transform, make_faces, scale_box
from ..rig import TALL
from ..shapes import HEAD_BOX, capsule, capsule_box, stroke
from ..svg import Part

ID = "mira"
NAME = "Mira"
DESCRIPTION = "蒸汽龐克的塗鴉滑板師"
PROPORTIONS = TALL

OL = "#2a1e1a"
HAIR, HAIR_SHADE, HAIR_LIGHT = "#ecebf0", "#c3c0cc", "#ffffff"
SKIN = "#fbe6dc"
SHIRT = "#f6f3ee"
LEATHER, LEATHER_DARK = "#7a5236", "#4e3424"
GOLD, GOLD_DARK = "#d6a944", "#9c7424"
BLACK = "#2b2729"
BLUE = "#6d9de0"
MECH = "#2e2a2c"
CLOTH, CLOTH_SHADE = "#e7e5ec", "#bdbac6"
L = stroke(OL, 2)
LH = stroke(OL, 5)          # 頭部縮小後約 2.3

HEAD_SCALE, HEAD_DY = 0.46, -10


def _head(body: str) -> Part:
    """頭部用 Q 版座標繪製後縮小。"""
    return Part(f'<g transform="{head_transform(HEAD_SCALE, HEAD_DY)}">{body}</g>',
                scale_box(HEAD_BOX, HEAD_SCALE, HEAD_DY))


def _gear(cx: float, cy: float, r: float, teeth: int = 8) -> str:
    pts = []
    for i in range(teeth * 2):
        a = math.pi * i / teeth
        rr = r if i % 2 == 0 else r * 0.78
        pts.append(f"{cx + rr * math.cos(a):.1f},{cy + rr * math.sin(a):.1f}")
    return (f'<polygon points="{" ".join(pts)}" fill="{GOLD}" {LH}/>'
            f'<circle cx="{cx}" cy="{cy}" r="{r * 0.35:g}" fill="{GOLD_DARK}"/>')


# ---------- 頭 ----------

def hair_back() -> Part:
    bob = ("M-46,-58 C-56,-112 64,-116 58,-58 C60,-30 58,-6 50,8 L30,0 L20,12 "
           "L-4,4 L-20,14 L-36,0 C-46,-14 -48,-36 -46,-58 Z")
    return _head(
        f'<path d="M-34,-92 L-50,-148 L-8,-110 Z" fill="{MECH}" {LH}/>'
        f'<path d="M-32,-102 L-43,-136 L-16,-112 Z" fill="{GOLD}"/>'
        f'<path d="M22,-106 L46,-150 L54,-96 Z" fill="{MECH}" {LH}/>'
        f'<path d="M29,-108 L45,-140 L49,-102 Z" fill="{GOLD}"/>'
        f'<path d="{bob}" fill="{HAIR_SHADE}" {LH}/>'
    )


def head() -> Part:
    face = "M-36,-60 C-38,-98 50,-100 52,-60 C53,-34 38,-10 12,3 C-12,-2 -36,-30 -36,-60 Z"
    neck = (f'<rect x="-5" y="-15" width="11" height="17" fill="{SKIN}" {L}/>'
            f'<rect x="-6" y="-9" width="13" height="3" fill="{BLACK}"/>'
            f'<circle cx="1" cy="-6" r="1.6" fill="{GOLD}"/>')
    return Part(neck + _head(f'<path d="{face}" fill="{SKIN}" {LH}/>').body,
                scale_box(HEAD_BOX, HEAD_SCALE, HEAD_DY))


def hair_front() -> Part:
    bangs = ("M-44,-56 C-48,-106 58,-108 56,-56 L50,-40 L42,-58 L34,-36 L24,-60 L12,-38 "
             "L2,-62 L-10,-40 L-20,-60 L-30,-38 L-38,-54 Z")
    near_lock = "M-42,-64 C-50,-36 -48,-6 -40,8 L-32,-6 L-28,6 C-30,-20 -30,-44 -26,-58 Z"
    far_lock = "M48,-62 C56,-36 54,-12 48,2 L42,-10 C44,-30 44,-48 40,-58 Z"
    return _head(
        f'<path d="{far_lock}" fill="{HAIR}" {LH}/>'
        f'<path d="{near_lock}" fill="{HAIR}" {LH}/>'
        f'<path d="{bangs}" fill="{HAIR}" {LH}/>'
        f'<path d="M-16,-92 Q8,-100 32,-92" fill="none" stroke="{HAIR_LIGHT}" stroke-width="6" stroke-linecap="round"/>'
        # 護目鏡（側面）與齒輪
        f'<circle cx="-46" cy="-76" r="15" fill="{GOLD}" {LH}/>'
        f'<circle cx="-46" cy="-76" r="9" fill="#6f8aa8" {LH}/>'
        f'<circle cx="-49" cy="-79" r="3" fill="#dfe8f2"/>'
        + _gear(40, -100, 12)
    )


# ---------- 身體 ----------

def torso() -> Part:
    shirt = ("M-17,0 C-19,-14 -23,-28 -23,-42 C-23,-58 -16,-68 -2,-70 L4,-70 "
             "C18,-68 25,-58 25,-42 C25,-28 21,-14 19,0 Z")
    corset = "M-18,-2 L-21,-28 C-22,-38 -16,-44 -6,-44 Q2,-38 10,-44 C20,-44 25,-38 24,-28 L20,-2 Z"
    lace = "M0,-40 L4,-34 L0,-28 L4,-22 L0,-16 L4,-10 L0,-4"
    return Part(
        f'<path d="{shirt}" fill="{SHIRT}" {L}/>'
        f'<path d="M-8,-69 L2,-50 L12,-69 Z" fill="{SKIN}" {L}/>'
        f'<path d="{corset}" fill="{LEATHER}" {L}/>'
        f'<path d="{lace}" fill="none" stroke="{GOLD}" stroke-width="1.4"/>'
        f'<path d="M-14,-43 L-14,-68 M18,-43 L18,-68" stroke="{LEATHER_DARK}" stroke-width="3"/>'
        + "".join(f'<circle cx="{x}" cy="{y}" r="1.6" fill="{GOLD}"/>'
                  for x, y in ((-12, -30), (-12, -18), (16, -30), (16, -18)))
        + f'<circle cx="2" cy="-66" r="2.2" fill="{BLUE}" {stroke(OL, 1)}/>',
        (-30, -76, 32, 6))


def hips() -> Part:
    shorts = "M-21,-32 L23,-32 L27,10 L5,14 L1,4 L-3,14 L-25,10 Z"
    return Part(
        f'<path d="{shorts}" fill="{BLACK}" {L}/>'
        f'<rect x="-27" y="-28" width="10" height="15" rx="2" fill="{LEATHER_DARK}" {L}/>'
        f'<rect x="-22" y="-35" width="46" height="6" rx="1.5" fill="{LEATHER}" {L}/>'
        f'<rect x="-2" y="-36" width="8" height="8" rx="1.5" fill="{GOLD}" {stroke(OL, 1.2)}/>',
        (-32, -40, 32, 20))


def cape() -> Part:
    """腰後的長披布。"""
    cloth = "M-6,-4 L10,-4 L8,60 C8,100 4,124 -4,146 L-22,142 C-18,110 -18,60 -18,16 Z"
    return Part(
        f'<path d="{cloth}" fill="{CLOTH}" {L}/>'
        f'<path d="M0,12 C0,60 -4,108 -10,140" fill="none" stroke="{CLOTH_SHADE}" stroke-width="5" stroke-linecap="round"/>'
        f'<path d="M-20,140 L-6,144" stroke="{GOLD}" stroke-width="3" stroke-linecap="round"/>',
        (-30, -10, 16, 154))


def back() -> Part:
    """背上的機械滑板。"""
    return Part(
        '<g transform="translate(4,-36) rotate(28)">'
        f'<rect x="-11" y="-78" width="22" height="150" rx="11" fill="#dcdde3" {L}/>'
        f'<rect x="-7" y="-70" width="14" height="134" rx="7" fill="none" stroke="{GOLD}" stroke-width="1.6"/>'
        f'<circle cx="0" cy="-58" r="8" fill="{GOLD}" {L}/><circle cx="0" cy="-58" r="3.5" fill="{BLUE}"/>'
        f'<circle cx="0" cy="54" r="8" fill="{GOLD}" {L}/><circle cx="0" cy="54" r="3.5" fill="{BLUE}"/>'
        '</g>',
        (-60, -110, 70, 60))


# ---------- 四肢 ----------

def arm_u() -> Part:
    return Part(
        capsule(56, 12, 10, SKIN, OL, 2)
        + f'<ellipse cx="0" cy="9" rx="12" ry="13" fill="{SHIRT}" {L}/>'
        f'<path d="M-11,19 Q0,23 11,19" fill="none" stroke="{GOLD}" stroke-width="2"/>'
        f'<path d="M-13,-5 Q0,-14 13,-5 L12,6 Q0,1 -12,6 Z" fill="{LEATHER}" {L}/>'
        f'<path d="M-12,5 Q0,0 12,5" fill="none" stroke="{GOLD}" stroke-width="1.5"/>',
        capsule_box(56, 28, 10))


def arm_l() -> Part:
    return Part(
        capsule(50, 10, 8.5, SKIN, OL, 2)
        + f'<rect x="-6" y="36" width="12" height="16" rx="3" fill="{LEATHER_DARK}" {L}/>'
        f'<circle cx="0" cy="42" r="1.5" fill="{GOLD}"/>',
        capsule_box(50, 12, 9))


def hand() -> Part:
    return Part(
        f'<ellipse cx="0" cy="9" rx="6.5" ry="10" fill="{LEATHER_DARK}" {L}/>'
        f'<ellipse cx="0" cy="17" rx="5" ry="4" fill="{SKIN}" {L}/>',
        (-10, -4, 10, 24))


def leg_u() -> Part:
    return Part(
        capsule(76, 21, 15, SKIN, OL, 2)
        + f'<g transform="translate(0,40)">{capsule(36, 17, 15, BLACK, OL, 2)}</g>'
        f'<path d="M-8,42 L8,42" stroke="#77737a" stroke-width="1.5"/>'
        f'<rect x="-11" y="22" width="22" height="4" fill="{BLACK}"/>'
        f'<circle cx="0" cy="24" r="3" fill="{BLUE}" {stroke(OL, 1)}/>',
        capsule_box(76, 21, 15))


def leg_l() -> Part:
    return Part(
        capsule(75, 15, 10, BLACK, OL, 2)
        + f'<g transform="translate(0,54)">{capsule(21, 14, 12, LEATHER, OL, 2)}</g>'
        f'<path d="M-7,60 L7,60" stroke="{GOLD}" stroke-width="2"/>',
        capsule_box(75, 15, 12))


def foot() -> Part:
    return Part(
        f'<path d="M-7,-8 L-8,13 L20,13 Q25,13 23,6 Q20,-1 6,-3 Z" fill="{LEATHER}" {L}/>'
        f'<rect x="-8" y="8" width="8" height="5" fill="{LEATHER_DARK}"/>'
        f'<rect x="3" y="-1" width="5" height="4" fill="{GOLD}"/>',
        (-14, -14, 28, 18))


PARTS = {
    "cape": cape, "back": back, "hair_back": hair_back, "head": head, "hair_front": hair_front,
    "torso": torso, "hips": hips,
    "arm_u": arm_u, "arm_l": arm_l, "hand": hand,
    "leg_u": leg_u, "leg_l": leg_l, "foot": foot,
}

FACES = make_faces(
    FaceStyle(iris="#c9963a", dark="#4a2e10", light="#f6d98a", mouth="#9a4a4a", blush="#ffb0b0",
              idle_lid="normal", idle_mouth="smile"),
    scale=HEAD_SCALE, dy=HEAD_DY,
)
