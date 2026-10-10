"""四足型態：橫躺的毛團身體、四條短腿（四隻都穿紅白小鞋）、長脖子往上抬。

頭、臉、眼鏡沿用直立型態的圖；骨骼對應見 rig.QuadProportions。
"""
from ...rig import QuadProportions
from ...shapes import capsule, capsule_box
from ...svg import Part
from . import faces, head, limbs
from .palette import OL, WOOL, WOOL_HL, WOOL_SHADE, cloud, fluff

LABEL = "四足"

PROPORTIONS = QuadProportions(
    hip=50, body=26, neck=(18, -14),
    front=(6, 14, 6), back=(-30, -22, 6), leg=(20, 18),
    tail=(-46, -12), head_top=(6, -114),
)

# 排球：一般接球用頭頂（Q 版單位 × 身體縮放；四足的 rootScale = 1）
VOLLEY = {"reach": 48, "hitHeight": 176, "spike": False, "dive": False}


def hips() -> Part:
    """整個身體是一大團毛，掛在身體中心。前半身骨頭（torso）不畫圖，只讓前腳和脖子接在上面。"""
    curls = "".join(f'<path d="M{x},{y} q4,-5 8,0" fill="none" stroke="{OL}" stroke-width="1.8" '
                    f'stroke-linecap="round" opacity="0.55"/>' for x, y in ((-30, -14), (-8, 2), (22, -16)))
    return Part(
        fluff(cloud(3, -4, 51, 26, 17, 0.1, 0.3))
        + f'<ellipse cx="24" cy="-2" rx="20" ry="15" fill="{WOOL_HL}" opacity="0.55"/>'
        + curls
        + f'<path d="M-38,10 Q-8,24 30,16" fill="none" stroke="{WOOL_SHADE}" stroke-width="5" '
          f'stroke-linecap="round" opacity="0.7"/>',
        (-56, -36, 62, 30))


def neck() -> Part:
    """四足的脖子比直立時細、稍微往前傾；耳朵沿用直立版。"""
    d = "M-12,16 C-14,-6 -8,-32 -6,-52 L22,-52 C22,-30 24,-6 20,16 Z"
    ripples = "".join(
        f'<path d="M{x},{y} q{k * 5},4 0,8" fill="none" stroke="{OL}" stroke-width="2" stroke-linecap="round"/>'
        for x, y, k in ((-10, -30, -1), (-12, -12, -1), (22, -34, 1)))
    return Part(
        head.ears() + fluff(d) + ripples
        + f'<path d="M2,-46 C2,-30 2,-12 4,8" fill="none" stroke="{WOOL_SHADE}" stroke-width="4" '
          f'stroke-linecap="round" opacity="0.6"/>',
        head.HEAD_BOX)


def tail() -> Part:
    return Part(fluff(cloud(-4, 0, 9, 8, 7, 0.25)), (-18, -12, 10, 12))


def leg_u() -> Part:
    return Part(capsule(20, 15, 13, WOOL, OL, 2.5), capsule_box(20, 15, 13))


def leg_l() -> Part:
    return Part(
        capsule(18, 13, 12, WOOL, OL, 2.5)
        + f'<path d="M-4,7 q4,3 8,0" fill="none" stroke="{WOOL_SHADE}" stroke-width="2"/>',
        capsule_box(18, 13, 12))


PARTS = {
    "tail": tail,
    "hair_back": neck, "head": head.head, "hair_front": head.hair_front,
    "hips": hips,
    "arm_u": leg_u, "arm_l": leg_l, "hand": limbs.foot,
    "leg_u": leg_u, "leg_l": leg_l, "foot": limbs.foot,
}

FACES = faces.make()
