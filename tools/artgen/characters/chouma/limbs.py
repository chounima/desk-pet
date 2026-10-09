"""四肢：T 恤袖子＋手臂、手、短褲＋大腿、小腿＋襪子、排球鞋。"""
from ...shapes import capsule, hgrad
from ...svg import Part
from .palette import (L, LACE, OL, RED, RED_DARK, REFLECT, SHIRT, SHIRT_HL, SHIRT_SHADE,
                      SHORTS, SHORTS_HL, SHORTS_SHADE, SKIN, SKIN_DEEP, SKIN_HL, SKIN_SHADE,
                      SOCK, SOCK_RIB, SOLE, SOLE_LINE, WHITE)

SKIN_GRAD = hgrad("skin", SKIN_SHADE, SKIN, SKIN_HL, SKIN)


def arm_u() -> Part:
    sleeve = "M-13,-6 C-12,-10 12,-10 13,-6 L15,28 Q0,31 -15,28 Z"
    return Part(
        SKIN_GRAD + capsule(58, 13, 10.5, "url(#skin)", OL, 2)
        + f'<path d="{sleeve}" fill="{SHIRT}" {L}/>'
        f'<path d="M-13,-6 L-6,-8 L-7,30 L-15,28 Z" fill="{SHIRT_SHADE}"/>'
        f'<path d="M-15,25 Q0,28 15,25 M2,4 Q6,14 4,22" fill="none" stroke="{SHIRT_HL}" stroke-width="1.2"/>',
        (-20, -14, 20, 68))


def arm_l() -> Part:
    return Part(
        SKIN_GRAD + capsule(52, 11.5, 8, "url(#skin)", OL, 2)
        + f'<path d="M3,6 Q5,18 3,30" fill="none" stroke="{SKIN_HL}" stroke-width="1.5" opacity="0.8"/>'
        f'<path d="M-4,40 Q-3,46 -3,50" fill="none" stroke="{SKIN_DEEP}" stroke-width="1" opacity="0.6"/>',
        (-10, -10, 10, 60))


def hand() -> Part:
    palm = "M-6,0 C-8,6 -8,12 -6.5,16 L-5,22 Q0,25.5 5,22 L6,14 C7.5,10 7.5,4 6,0 Z"
    thumb = "M5,4 C10,6 11,11 8.5,15 L5.5,12 Z"
    return Part(
        f'<path d="{palm}" fill="{SKIN}" {L}/>'
        f'<path d="{thumb}" fill="{SKIN}" {L}/>'
        f'<path d="M-6,4 C-7,10 -6,16 -5,20" fill="none" stroke="{SKIN_SHADE}" stroke-width="2.5" opacity="0.8"/>'
        f'<path d="M-2.5,16 L-2.5,23 M1,16.5 L1,24 M4,15 L4,22" stroke="{SKIN_DEEP}" stroke-width="0.9"/>',
        (-12, -4, 14, 28))


def hand_peace() -> Part:
    """比 YA：食指與中指伸直張開，其餘手指握起。手腕在原點，手指朝 +y。"""
    palm = "M-6,0 C-8,5 -8,10 -6,14 Q0,17 6,14 C7.5,10 7.5,4 6,0 Z"
    fingers = ("M-5,12 L-8,29 Q-6.5,31.5 -4.5,30 L-1.2,14 Z",
               "M1,14 L4.5,30 Q6.5,31.5 8,29 L5.5,12 Z")
    return Part(
        "".join(f'<path d="{d}" fill="{SKIN}" {L}/>' for d in fingers)
        + f'<path d="{palm}" fill="{SKIN}" {L}/>'
        f'<path d="M-5,12 Q0,16 5,12" fill="none" stroke="{SKIN_DEEP}" stroke-width="1"/>'
        f'<path d="M5,3 C9,5 9,10 5,12" fill="{SKIN}" {L}/>'
        f'<path d="M-6,3 C-7,8 -6,12 -5,13" fill="none" stroke="{SKIN_SHADE}" stroke-width="2" opacity="0.8"/>',
        (-12, -4, 12, 34))


def leg_u() -> Part:
    shorts = "M-16,-8 L16,-8 L18,50 Q0,53 -18,50 Z"
    return Part(
        SKIN_GRAD + capsule(78, 16, 12, "url(#skin)", OL, 2)
        + f'<path d="M-5,72 Q0,76 5,72" fill="none" stroke="{SKIN_SHADE}" stroke-width="1.5"/>'
        f'<path d="{shorts}" fill="{SHORTS}" {L}/>'
        f'<path d="M-16,-8 L-8,-8 L-9,51 L-18,50 Z" fill="{SHORTS_SHADE}"/>'
        f'<path d="M-18,47 Q0,50 18,47" fill="none" stroke="{SHORTS_SHADE}" stroke-width="1.5"/>'
        f'<path d="M4,18 Q8,30 6,42" fill="none" stroke="{SHORTS_HL}" stroke-width="1"/>'
        # 反光條
        f'<path d="M-13.5,6 L-15.5,40" stroke="{REFLECT}" stroke-width="1.6" stroke-linecap="round"/>',
        (-24, -14, 24, 90))


def leg_l() -> Part:
    calf = "M-7,0 C-9,16 -9,30 -6.5,46 L-5,78 L5,78 L6,46 C8,30 7.5,14 7,0 Z"
    return Part(
        SKIN_GRAD + f'<path d="{calf}" fill="url(#skin)" {L}/>'
        f'<path d="M-6.6,40 L-5.2,80 L5.2,80 L6.2,40 Q0,38 -6.6,40 Z" fill="{SOCK}" {L}/>'
        f'<path d="M-6.4,43 Q0,41 6.1,43 M-6.3,45.5 Q0,43.5 6,45.5" fill="none" stroke="{SOCK_RIB}" stroke-width="0.9"/>'
        # 襪子上的紅色標誌
        f'<path d="M0.5,50 L4.5,54 M4.5,50 L0.5,54 M2.5,48.5 L2.5,56" stroke="{RED}" stroke-width="1.3" stroke-linecap="round"/>',
        (-12, -6, 12, 84))


def foot() -> Part:
    upper = ("M-9,-8 C-11,-2 -12,4 -11,8 L-11,9 L25,9 C29,9 30,6 28,2 "
             "C25,-3 17,-5 10,-7 L5,-11 L-5,-11 Z")
    heel = "M-11,-2 L-11,9 L1,9 C0,4 0,-2 -2,-8 L-9,-8 Z"
    toe = "M15,-3 C21,-2 27,0 28,3 C26,7 22,8 18,8 C17,4 16,0 15,-3 Z"
    sole = "M-12,8 L26,8 C30,8 31,11 28,13 L-11,13 C-13,12 -13,9 -12,8 Z"
    return Part(
        f'<path d="M-5,-12 L5,-12 L5,-8 L-5,-8 Z" fill="{SOCK}"/>'
        f'<path d="{upper}" fill="{WHITE}" {L}/>'
        f'<path d="{heel}" fill="{RED}"/>'
        f'<path d="{toe}" fill="{RED}"/>'
        f'<path d="M0,6 L6,-3 L9,3 L14,-4" fill="none" stroke="{RED}" stroke-width="2.6" stroke-linejoin="round"/>'
        f'<path d="M-9,4 L-3,4" stroke="{RED_DARK}" stroke-width="1.2"/>'
        f'<path d="{upper}" fill="none" {L}/>'
        f'<path d="M2,-9 L8,-6 M4,-10 L10,-7 M6,-10.5 L12,-7.5 M3,-6 L9,-9 M5,-7 L11,-10" '
        f'stroke="{LACE}" stroke-width="1.1" stroke-linecap="round"/>'
        f'<path d="{sole}" fill="{SOLE}" {L}/>'
        f'<path d="M-11,11 L28,11" stroke="{SOLE_LINE}" stroke-width="0.8"/>',
        (-16, -16, 34, 17))
