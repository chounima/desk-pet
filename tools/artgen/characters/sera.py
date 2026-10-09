"""Sera：藪貓獸人盜賊。大耳朵、斑點毛皮、藍色兜帽背心、短夾克、燈籠褲、皮革護脛、長尾巴。"""
from ..faces import FaceStyle, make_faces
from ..shapes import FACE_PATH, HEAD_BOX, capsule, capsule_box, dots, stroke
from ..svg import Part

ID = "sera"
NAME = "Sera"
DESCRIPTION = "藪貓族的見習盜賊"

OL = "#2a1a12"
FUR, FUR_LIGHT, SPOT = "#dca55e", "#f7ece0", "#5a3a22"
EAR_DARK, EAR_IN = "#2e221c", "#f3d6c0"
HAIR = "#8a5a32"
BLUE, BLUE_DARK = "#2b4fd8", "#1d348f"
JACKET, LEATHER, LEATHER_DARK = "#5a3b28", "#7a4a2c", "#4e2f1c"
CLOTH, METAL = "#efe6d8", "#b9bcc4"
L = stroke(OL)


def hair_back() -> Part:
    """耳朵畫在頭後面，讓臉蓋住耳根。"""
    return Part(
        f'<path d="M-38,-74 L-52,-150 L-6,-96 Z" fill="{EAR_DARK}" {L}/>'
        f'<path d="M-34,-84 L-46,-136 L-14,-98 Z" fill="{EAR_IN}"/>'
        f'<path d="M28,-90 L58,-148 L56,-80 Z" fill="{EAR_DARK}" {L}/>'
        f'<path d="M34,-92 L54,-134 L52,-86 Z" fill="{EAR_IN}"/>'
        f'<circle cx="-36" cy="-82" r="3" fill="{METAL}" {stroke(OL, 1.5)}/>',
        HEAD_BOX)


def head() -> Part:
    muzzle = "M-20,-30 C-10,-8 30,-4 46,-28 C42,-10 26,4 8,4 C-10,4 -24,-12 -20,-30 Z"
    return Part(
        f'<rect x="-8" y="-10" width="18" height="14" fill="{FUR}" {L}/>'
        f'<path d="{FACE_PATH}" fill="{FUR}" {L}/>'
        f'<path d="{muzzle}" fill="{FUR_LIGHT}"/>'
        + dots([(-30, -60), (-24, -70), (42, -66), (48, -54), (-32, -40)], 2.6, SPOT)
        # 淚痕紋
        + f'<path d="M-2,-34 Q-4,-26 -8,-20 M32,-34 Q34,-28 36,-24" fill="none" '
          f'stroke="{SPOT}" stroke-width="2" stroke-linecap="round"/>',
        HEAD_BOX)


def hair_front() -> Part:
    tuft = ("M-40,-62 L-36,-104 L-20,-86 L-10,-122 L4,-92 L16,-120 L24,-88 L44,-108 "
            "L50,-66 L38,-74 L28,-58 L16,-72 L6,-56 L-6,-70 L-18,-56 L-28,-70 Z")
    return Part(
        f'<path d="{tuft}" fill="{HAIR}" {L}/>'
        f'<path d="M-2,-104 L8,-80 M20,-106 L26,-84" fill="none" stroke="#b07a48" stroke-width="2.5" stroke-linecap="round"/>',
        HEAD_BOX)


def torso() -> Part:
    body = "M-19,0 L-21,-40 C-23,-58 -12,-66 2,-66 C16,-66 25,-58 23,-40 L21,0 Z"
    vest = "M-21,-38 C-23,-58 -12,-66 2,-66 C16,-66 25,-58 24,-38 L16,-32 L6,-50 L-2,-50 L-10,-32 Z"
    return Part(
        f'<path d="{body}" fill="{FUR_LIGHT}" {L}/>'
        + dots([(-8, -22), (8, -16), (-12, -8), (12, -6)], 2.2, SPOT)
        + f'<rect x="-14" y="-52" width="32" height="9" rx="2" fill="{CLOTH}" {stroke(OL, 2)}/>'
        f'<path d="{vest}" fill="{BLUE}" {L}/>'
        # 兜帽（放下堆在頸後）
        f'<ellipse cx="-14" cy="-64" rx="13" ry="7" fill="{BLUE}" {L}/>'
        # 頸上的護目鏡
        f'<circle cx="-4" cy="-64" r="5" fill="{METAL}" {stroke(OL, 2)}/>'
        f'<circle cx="10" cy="-64" r="5" fill="{METAL}" {stroke(OL, 2)}/>'
        f'<circle cx="-4" cy="-64" r="2.5" fill="#3b4f6b"/><circle cx="10" cy="-64" r="2.5" fill="#3b4f6b"/>',
        (-34, -76, 32, 6))


def hips() -> Part:
    return Part(
        f'<path d="M-21,-6 L23,-6 L25,12 L-23,12 Z" fill="{BLUE}" {L}/>'
        f'<path d="M-6,-2 L14,-2 L16,34 L-4,34 Z" fill="{CLOTH}" {L}/>'
        f'<rect x="-23" y="-12" width="48" height="8" rx="2" fill="{LEATHER}" {L}/>'
        f'<circle cx="3" cy="-8" r="5" fill="{METAL}" {stroke(OL, 2)}/>',
        (-30, -18, 32, 40))


def arm_u() -> Part:
    return Part(
        capsule(32, 12, 11, FUR, OL) + capsule(20, 18, 17, JACKET, OL),
        capsule_box(32, 18, 17))


def arm_l() -> Part:
    return Part(
        capsule(28, 11, 10, FUR, OL)
        + f'<rect x="-7.5" y="6" width="15" height="18" rx="3" fill="{LEATHER}" {L}/>'
        f'<path d="M-3,11 Q3,13 -1,17 Q-4,20 2,21" fill="none" stroke="{LEATHER_DARK}" stroke-width="1.8"/>',
        capsule_box(28, 15, 10))


def hand() -> Part:
    return Part(
        f'<ellipse cx="0" cy="7" rx="7" ry="8" fill="{FUR_LIGHT}" {L}/>'
        f'<path d="M-3,14 L-3,16 M1,14.5 L1,16.5 M4.5,13 L5,15" stroke="{OL}" stroke-width="1.5" stroke-linecap="round"/>',
        (-12, -6, 12, 20))


def leg_u() -> Part:
    return Part(
        capsule(44, 24, 22, BLUE, OL)
        + f'<path d="M-6,8 Q6,12 2,22 Q-4,30 6,36" fill="none" stroke="{BLUE_DARK}" stroke-width="2.5"/>',
        capsule_box(44, 24, 22))


def leg_l() -> Part:
    return Part(
        capsule(42, 15, 12, FUR, OL)
        + f'<rect x="-9" y="4" width="18" height="28" rx="4" fill="{LEATHER}" {L}/>'
        f'<circle cx="0" cy="3" r="8" fill="{LEATHER_DARK}" {L}/>',
        capsule_box(42, 18, 12))


def foot() -> Part:
    return Part(
        f'<path d="M-7,-4 L-7,6 L16,6 Q21,6 20,0 Q18,-5 8,-5 Z" fill="{FUR_LIGHT}" {L}/>'
        f'<path d="M12,1 L12,6 M16,1 L16,6" stroke="{OL}" stroke-width="1.3"/>',
        (-12, -10, 26, 10))


def tail() -> Part:
    d = "M0,0 C-24,16 -44,56 -40,100"
    return Part(
        f'<path d="{d}" fill="none" stroke="{OL}" stroke-width="15" stroke-linecap="round"/>'
        f'<path d="{d}" fill="none" stroke="{FUR}" stroke-width="11" stroke-linecap="round"/>'
        f'<path d="M-41,84 C-40,92 -40,96 -40,100" fill="none" stroke="{FUR_LIGHT}" stroke-width="11" stroke-linecap="round"/>'
        + dots([(-10, 8), (-24, 22), (-34, 42), (-40, 62)], 2.6, SPOT),
        (-60, -12, 14, 114))


PARTS = {
    "tail": tail, "hair_back": hair_back, "head": head, "hair_front": hair_front,
    "torso": torso, "hips": hips,
    "arm_u": arm_u, "arm_l": arm_l, "hand": hand,
    "leg_u": leg_u, "leg_l": leg_l, "foot": foot,
}

_whiskers = (f'<path d="M44,-24 L60,-28 M44,-20 L60,-18 M-28,-24 L-44,-28 M-28,-20 L-44,-18" '
             f'fill="none" stroke="{OL}" stroke-width="1.2" opacity="0.6"/>')
_nose = '<path d="M14,-28 L20,-28 L17,-24 Z" fill="#c86b6b"/>'

FACES = make_faces(FaceStyle(
    iris="#f2a81e", dark="#4a2a08", light="#ffe08a", mouth="#7a3a2a", blush="#ff9f8a",
    idle_lid="normal", idle_mouth="cat", slit=True,
    extras=[_nose, _whiskers],
))
