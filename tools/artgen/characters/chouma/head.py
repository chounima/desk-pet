"""頭部：後髮、臉與耳朵、眼鏡與前髮。以 Q 版座標繪製後縮小。"""
from ...faces import head_transform, scale_box
from ...shapes import HEAD_BOX
from ...svg import Part
from .palette import (GLASSES, HAIR, HAIR_HL, HAIR_MID, HEAD_DY, HEAD_SCALE, L, LH,
                      SKIN, SKIN_DEEP, SKIN_SHADE)

BOX = scale_box(HEAD_BOX, HEAD_SCALE, HEAD_DY)


def _scaled(body: str) -> str:
    return f'<g transform="{head_transform(HEAD_SCALE, HEAD_DY)}">{body}</g>'


def hair_back() -> Part:
    # 頭頂輪廓做成小鋸齒，表現亂翹的短髮
    mass = ("M-48,-58 C-56,-90 -44,-104 -32,-104 L-30,-112 L-20,-104 L-10,-114 L-2,-106 "
            "L8,-115 L14,-106 L26,-111 L28,-102 C48,-98 60,-84 58,-60 "
            "C60,-42 56,-28 50,-20 L-46,-18 C-52,-32 -52,-46 -48,-58 Z")
    return Part(_scaled(f'<path d="{mass}" fill="{HAIR}" {LH}/>'), BOX)


def head() -> Part:
    face = ("M-36,-64 C-38,-102 52,-104 54,-64 C56,-44 50,-24 38,-10 C28,0 16,6 6,5 "
            "C-10,2 -26,-14 -33,-30 C-36,-40 -36,-52 -36,-64 Z")
    jaw_shade = "M-33,-30 C-26,-14 -10,2 6,5 C-6,-2 -18,-14 -24,-28 Z"
    ear = "M-34,-60 C-48,-64 -52,-44 -44,-33 C-40,-27 -35,-29 -33,-33 Z"
    neck = (
        f'<path d="M-6,-16 L-6,2 L9,2 L9,-14 Z" fill="{SKIN}" {L}/>'
        f'<path d="M-6,-15 L9,-13 L9,-7 Q2,-6 -6,-9 Z" fill="{SKIN_SHADE}"/>'
        f'<path d="M2,-5 Q3,-3 2,-1" fill="none" stroke="{SKIN_DEEP}" stroke-width="0.8"/>'
    )
    return Part(neck + _scaled(
        f'<path d="{ear}" fill="{SKIN}" {LH}/>'
        f'<path d="M-41,-55 C-46,-50 -46,-42 -41,-37" fill="none" stroke="{SKIN_DEEP}" stroke-width="3"/>'
        f'<path d="{face}" fill="{SKIN}" {LH}/>'
        f'<path d="{jaw_shade}" fill="{SKIN_SHADE}" opacity="0.75"/>'
        f'<path d="M45,-44 C47,-30 43,-20 36,-13" fill="none" stroke="{SKIN_SHADE}" stroke-width="3" opacity="0.6"/>'
    ), BOX)


def _glasses() -> str:
    frame = f'fill="#cfe0ec" fill-opacity="0.16" stroke="{GLASSES}" stroke-width="4"'
    return (
        f'<path d="M-26,-47 L-41,-51" stroke="{GLASSES}" stroke-width="4" stroke-linecap="round"/>'
        f'<rect x="-26" y="-52" width="31" height="19" rx="4" {frame}/>'
        f'<rect x="15" y="-52" width="22" height="19" rx="4" {frame}/>'
        # 鏡框上緣較粗
        f'<path d="M-25,-51 L4,-51 M16,-51 L36,-51" stroke="{GLASSES}" stroke-width="6" stroke-linecap="round"/>'
        f'<path d="M5,-46 Q10,-50 15,-46" fill="none" stroke="{GLASSES}" stroke-width="3.5"/>'
        # 鏡片反光
        '<path d="M-20,-36 L-12,-49 M-15,-36 L-9,-45 M20,-36 L26,-48" stroke="#fff" '
        'stroke-width="2.4" stroke-linecap="round" opacity="0.5"/>'
    )


def hair_front() -> Part:
    # 瀏海：尖端與縫隙交錯，縫隙露出眉毛
    bangs = ("M-46,-58 C-50,-108 60,-112 58,-58 L52,-50 L48,-64 L42,-48 L36,-66 L28,-50 "
             "L22,-68 L12,-50 L6,-70 L-4,-52 L-10,-70 L-18,-52 L-24,-68 L-32,-50 L-38,-64 L-44,-52 Z")
    near_side = "M-44,-66 C-48,-56 -46,-44 -40,-38 L-38,-52 L-34,-46 L-34,-64 Z"
    far_side = "M50,-66 C56,-56 56,-46 52,-38 L48,-50 Z"
    return Part(_scaled(
        _glasses()
        + f'<path d="{far_side}" fill="{HAIR}" {LH}/>'
        f'<path d="{near_side}" fill="{HAIR}" {LH}/>'
        f'<path d="{bangs}" fill="{HAIR}" {LH}/>'
        # 髮絲分線與高光
        f'<path d="M-14,-92 L-20,-58 M8,-98 L4,-62 M30,-92 L32,-60 M-32,-80 L-36,-58" '
        f'fill="none" stroke="{HAIR_MID}" stroke-width="2.5"/>'
        f'<path d="M-28,-88 Q-14,-100 2,-98 M14,-100 Q30,-98 42,-88" fill="none" '
        f'stroke="{HAIR_HL}" stroke-width="3" stroke-linecap="round" opacity="0.8"/>'
    ), BOX)
