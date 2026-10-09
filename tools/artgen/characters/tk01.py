"""TK-01：紅髮機械少女。白色緊身衣、黑色裝甲與手套、耳機模組。"""
from ..faces import FaceStyle, make_faces
from ..shapes import FACE_PATH, HEAD_BOX, capsule, capsule_box, stroke
from ..svg import Part

ID = "tk01"
NAME = "TK-01"
DESCRIPTION = "打字輔助型人形機體"

OL = "#24161b"
HAIR, HAIR_SHADE, HAIR_LIGHT = "#d81e2c", "#9e1420", "#ff6a6a"
SKIN = "#fde8de"
SUIT, SUIT_SHADE = "#eef0f4", "#c9ccd4"
ARMOR, ARMOR_LIGHT = "#3a3b41", "#5a5c64"
ACCENT = "#ff3b4a"
L = stroke(OL)


def hair_back() -> Part:
    back = "M-44,-60 C-54,-112 64,-114 56,-60 L58,54 Q6,70 -48,54 Z"
    strands = "M-36,0 L-40,50 M-16,10 L-18,62 M30,6 L36,56"
    return Part(
        f'<path d="{back}" fill="{HAIR_SHADE}" {L}/>'
        f'<path d="{strands}" fill="none" stroke="{OL}" stroke-width="1.5" opacity="0.35"/>',
        HEAD_BOX)


def head() -> Part:
    return Part(
        f'<rect x="-8" y="-10" width="18" height="14" fill="{SKIN}" {L}/>'
        f'<rect x="-11" y="-4" width="24" height="9" rx="2" fill="{ARMOR}" {L}/>'
        f'<path d="{FACE_PATH}" fill="{SKIN}" {L}/>',
        HEAD_BOX)


def hair_front() -> Part:
    bangs = ("M-42,-58 C-44,-102 56,-104 56,-58 L50,-50 L42,-58 L34,-48 L24,-58 "
             "L14,-48 L4,-58 L-6,-48 L-16,-58 L-26,-48 L-36,-58 Z")
    near_lock = "M-40,-64 C-48,-30 -48,0 -44,12 L-28,12 C-30,-12 -30,-38 -26,-56 Z"
    far_lock = "M48,-60 C54,-30 54,-6 52,6 L42,6 C42,-16 42,-40 40,-54 Z"
    band = "M-44,-64 Q-38,-110 14,-108 Q56,-104 58,-62"
    return Part(
        f'<path d="{far_lock}" fill="{HAIR}" {L}/>'
        f'<path d="{near_lock}" fill="{HAIR}" {L}/>'
        f'<path d="{bangs}" fill="{HAIR}" {L}/>'
        f'<path d="M-20,-94 Q8,-102 36,-94" fill="none" stroke="{HAIR_LIGHT}" stroke-width="4" '
        'stroke-linecap="round" opacity="0.8"/>'
        f'<path d="{band}" fill="none" stroke="{OL}" stroke-width="10" stroke-linecap="round"/>'
        f'<path d="{band}" fill="none" stroke="{ARMOR}" stroke-width="6" stroke-linecap="round"/>'
        # 耳機模組
        f'<rect x="51" y="-60" width="9" height="24" rx="4" fill="{ARMOR}" {L}/>'
        f'<rect x="-56" y="-64" width="15" height="30" rx="6" fill="{ARMOR}" {L}/>'
        f'<circle cx="-48.5" cy="-43" r="3" fill="{ACCENT}"/>',
        HEAD_BOX)


def torso() -> Part:
    suit = "M-19,0 L-21,-40 C-23,-58 -12,-66 2,-66 C16,-66 25,-58 23,-40 L21,0 Z"
    plate = "M-17,-64 L21,-64 L24,-46 L4,-38 L-18,-46 Z"
    tie = "M2,-62 L-3,-42 L2,-46 L7,-42 Z"
    return Part(
        f'<path d="{suit}" fill="{SUIT}" {L}/>'
        f'<path d="M2,-36 L2,0 M-12,-20 C-8,-12 -6,-6 -6,0" fill="none" stroke="{SUIT_SHADE}" stroke-width="2.5"/>'
        f'<path d="{plate}" fill="{ARMOR}" {L}/>'
        f'<path d="{tie}" fill="#1c1c20" {stroke(OL, 2)}/>'
        f'<circle cx="-11" cy="-52" r="2" fill="{ACCENT}"/>'
        f'<rect x="8" y="-32" width="12" height="4" rx="1" fill="{ARMOR}"/>',
        (-30, -74, 32, 6))


def hips() -> Part:
    return Part(
        f'<path d="M-21,-8 L23,-8 L24,10 Q2,22 -22,10 Z" fill="{SUIT}" {L}/>'
        f'<rect x="-22" y="-11" width="46" height="7" rx="2" fill="{ARMOR}" {L}/>',
        (-30, -16, 32, 26))


def arm_u() -> Part:
    return Part(
        capsule(32, 15, 13, ARMOR, OL)
        + f'<ellipse cx="0" cy="2" rx="11" ry="9" fill="{ARMOR_LIGHT}" {L}/>',
        capsule_box(32, 22, 13))


def arm_l() -> Part:
    return Part(capsule(28, 13, 11, ARMOR, OL), capsule_box(28, 13, 11))


def hand() -> Part:
    return Part(
        f'<ellipse cx="0" cy="7" rx="7" ry="8" fill="{ARMOR}" {L}/>'
        f'<ellipse cx="-2" cy="4" rx="3" ry="2.5" fill="{ARMOR_LIGHT}"/>',
        (-12, -6, 12, 20))


def leg_u() -> Part:
    return Part(capsule(44, 20, 16, SUIT, OL), capsule_box(44, 20, 16))


def leg_l() -> Part:
    return Part(
        capsule(42, 16, 12, ARMOR, OL)
        + f'<path d="M-7,6 L7,6" stroke="{ARMOR_LIGHT}" stroke-width="2.5"/>',
        capsule_box(42, 16, 12))


def foot() -> Part:
    return Part(f'<path d="M-7,-4 L-7,6 L16,6 Q20,6 19,1 Q16,-4 6,-5 Z" fill="{ARMOR}" {L}/>',
                (-12, -10, 24, 10))


PARTS = {
    "hair_back": hair_back, "head": head, "hair_front": hair_front,
    "torso": torso, "hips": hips,
    "arm_u": arm_u, "arm_l": arm_l, "hand": hand,
    "leg_u": leg_u, "leg_l": leg_l, "foot": foot,
}

FACES = make_faces(FaceStyle(
    iris="#c8102e", dark="#3a0a12", light="#ff8a8a", mouth="#7a2a34", blush="#ff9fae",
    idle_lid="half", idle_mouth="flat",
    extras=[f'<circle cx="35" cy="-31" r="1.4" fill="{OL}"/>'],   # 淚痣
))
