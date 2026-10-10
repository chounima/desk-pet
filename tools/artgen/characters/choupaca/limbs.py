"""短短的手腳（毛球手、紅白小鞋）。"""
from ...shapes import capsule, capsule_box
from ...svg import Part
from .palette import L, OL, RED, SOLE, WOOL, WOOL_HL, WOOL_SHADE


def arm_u() -> Part:
    return Part(capsule(14, 14, 13, WOOL, OL, 2.5), capsule_box(14, 14, 13))


def arm_l() -> Part:
    return Part(capsule(12, 13, 12, WOOL, OL, 2.5), capsule_box(12, 13, 12))


def hand() -> Part:
    return Part(
        f'<circle cx="0" cy="4" r="7" fill="{WOOL}" {L}/>'
        f'<circle cx="-2" cy="2" r="2.5" fill="{WOOL_HL}"/>',
        (-10, -6, 10, 14))


def leg_u() -> Part:
    return Part(capsule(18, 17, 15, WOOL, OL, 2.5), capsule_box(18, 17, 15))


def leg_l() -> Part:
    return Part(
        capsule(16, 15, 13, WOOL, OL, 2.5)
        + f'<path d="M-5,6 q5,3 10,0" fill="none" stroke="{WOOL_SHADE}" stroke-width="2"/>',
        capsule_box(16, 15, 13))


def foot() -> Part:
    """Chouma 的紅白排球鞋縮成小小一雙。"""
    return Part(
        f'<path d="M-8,-4 L-8,5 L13,5 Q17,5 16,0 Q14,-5 5,-5 Z" fill="{RED}" {L}/>'
        f'<rect x="-8.5" y="3" width="25" height="3" rx="1.5" fill="{SOLE}" {L}/>'
        f'<path d="M0,-4 L3,0 L6,-4" fill="none" stroke="{SOLE}" stroke-width="1.8"/>',
        (-12, -10, 20, 10))
