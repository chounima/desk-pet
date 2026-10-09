"""身體：T 恤（含校徽、背包肩帶）、短褲腰部、背包。座標為骨骼局部座標。"""
from ...svg import Part
from .palette import (BAG, BAG_HL, BAG_SHADE, BUCKLE, L, SHIRT, SHIRT_HL, SHIRT_SHADE,
                      SHORTS, SHORTS_SHADE, SKIN_SHADE, WHITE)


def _logo(x: float, y: float) -> str:
    """胸前的 NFU C.S.I.E 校徽（簡化）。"""
    return (
        f'<g transform="translate({x},{y})">'
        f'<circle r="6.5" fill="none" stroke="{WHITE}" stroke-width="1"/>'
        f'<path d="M-4.6,-4.6 L4.6,4.6 M4.6,-4.6 L-4.6,4.6" stroke="{WHITE}" stroke-width="2.2"/>'
        f'<circle r="3.3" fill="{SHIRT}" stroke="{WHITE}" stroke-width="0.6"/>'
        f'<text y="0.8" font-size="2.2" font-family="Arial" font-weight="bold" fill="{WHITE}" text-anchor="middle">CSIE</text>'
        f'<text y="-7.6" font-size="2.6" font-family="Arial" font-weight="bold" fill="{WHITE}" text-anchor="middle">NFU</text>'
        '</g>'
    )


def torso() -> Part:
    shirt = ("M-22,26 L-24,-20 C-25,-44 -26,-58 -18,-66 C-12,-70 -6,-72 0,-72 "
             "C8,-72 16,-70 22,-66 C28,-58 27,-44 26,-20 L25,26 Q2,31 -22,26 Z")
    shade = "M-22,26 L-24,-20 C-25,-44 -26,-58 -18,-66 L-13,-62 C-17,-42 -16,-8 -13,27 Z"
    hem_shade = "M-22,22 Q2,27 25,22 L25,26 Q2,31 -22,26 Z"
    folds = ("M-12,-36 Q-2,-28 8,-34 M-10,-6 Q2,0 14,-6 M-4,18 L-2,26 "
             "M12,16 L14,25 M20,-30 Q18,-10 20,8")
    return Part(
        f'<path d="{shirt}" fill="{SHIRT}" {L}/>'
        f'<path d="{shade}" fill="{SHIRT_SHADE}"/>'
        f'<path d="{hem_shade}" fill="{SHIRT_SHADE}"/>'
        f'<path d="{folds}" fill="none" stroke="{SHIRT_HL}" stroke-width="1.2" stroke-linecap="round" opacity="0.8"/>'
        # 領口
        f'<path d="M-9,-72 Q2,-61 13,-72 Z" fill="{SKIN_SHADE}"/>'
        f'<path d="M-10,-72 Q2,-59 14,-72" fill="none" stroke="{SHIRT_HL}" stroke-width="2.5"/>'
        + _logo(-6, -44)
        # 背包肩帶
        + f'<path d="M17,-67 C19,-52 20,-40 22,-22" fill="none" stroke="{BAG_SHADE}" stroke-width="4.5"/>'
        f'<path d="M-17,-67 C-19,-50 -20,-36 -21,-14" fill="none" stroke="{BAG}" stroke-width="6.5"/>'
        f'<path d="M-17,-64 C-19,-50 -20,-36 -21,-16" fill="none" stroke="{BAG_HL}" stroke-width="0.8" stroke-dasharray="2 1.5"/>'
        f'<rect x="-23.5" y="-40" width="6" height="4.5" rx="1" fill="{BUCKLE}" {L}/>'
        f'<path d="M-21,-14 L-22,-6" stroke="{BAG}" stroke-width="3"/>',
        (-34, -80, 36, 34))


def hips() -> Part:
    return Part(
        f'<path d="M-24,-34 L24,-34 L27,14 Q2,22 -25,14 Z" fill="{SHORTS}" {L}/>'
        f'<path d="M-24,-34 L24,-34 L24,-29 L-24,-29 Z" fill="{SHORTS_SHADE}"/>'
        f'<path d="M-24,-34 L-14,-34 L-15,16 L-25,14 Z" fill="{SHORTS_SHADE}" opacity="0.7"/>',
        (-32, -40, 32, 26))


def back() -> Part:
    """背在背後的黑色後背包。"""
    body = ("M-24,-64 C-34,-64 -42,-58 -43,-46 L-45,8 C-45,18 -39,24 -30,24 "
            "L-12,24 L-12,-64 Z")
    pocket = ("M-47,-18 C-48,-24 -44,-26 -40,-26 L-24,-26 L-24,14 L-42,14 "
              "C-46,14 -48,10 -47,4 Z")
    return Part(
        f'<path d="M-30,-64 Q-26,-73 -20,-64" fill="none" stroke="{BAG}" stroke-width="3"/>'
        f'<path d="{body}" fill="{BAG}" {L}/>'
        f'<path d="M-24,-64 C-30,-62 -34,-56 -35,-46 L-37,16 C-36,20 -33,24 -30,24 L-12,24 L-12,-64 Z" fill="{BAG_SHADE}" opacity="0.6"/>'
        f'<path d="{pocket}" fill="{BAG_HL}" {L}/>'
        f'<path d="M-44,-21 L-26,-21" stroke="{BUCKLE}" stroke-width="1"/>'
        f'<path d="M-40,-58 C-34,-62 -26,-62 -16,-62" fill="none" stroke="{BAG_HL}" stroke-width="1.2"/>'
        f'<rect x="-44" y="-12" width="4" height="7" rx="1" fill="{BUCKLE}"/>'
        f'<rect x="-31" y="-12" width="4" height="7" rx="1" fill="{BUCKLE}"/>'
        f'<path d="M-45,-6 L-12,-6" stroke="{BAG_SHADE}" stroke-width="2"/>',
        (-54, -78, 0, 30))
