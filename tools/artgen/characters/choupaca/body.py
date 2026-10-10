"""身體：一大團羊毛（軀幹）＋下半圈毛（臀部）＋小尾巴。"""
from ...svg import Part
from .palette import OL, WOOL_HL, WOOL_SHADE, cloud, fluff

CURLS = ((-22, -30), (14, -44), (-6, 6), (22, -10))


def torso() -> Part:
    curls = "".join(f'<path d="M{x},{y} q4,-5 8,0" fill="none" stroke="{OL}" stroke-width="1.8" '
                    f'stroke-linecap="round" opacity="0.55"/>' for x, y in CURLS)
    return Part(
        fluff(cloud(0, -18, 44, 48, 16, 0.1, 0.2))
        + f'<ellipse cx="8" cy="-6" rx="24" ry="28" fill="{WOOL_HL}" opacity="0.55"/>'
        + curls
        + f'<path d="M-30,10 Q-10,26 22,18" fill="none" stroke="{WOOL_SHADE}" stroke-width="5" '
          f'stroke-linecap="round" opacity="0.7"/>',
        (-52, -76, 52, 40))


def hips() -> Part:
    return Part(fluff(cloud(2, 8, 38, 18, 11, 0.16, 0.4)), (-44, -16, 48, 32))


def tail() -> Part:
    return Part(fluff(cloud(-6, 4, 10, 9, 7, 0.25)), (-22, -10, 10, 18))
