"""Chouma：愛打排球的資工系大學生（依角色設定圖）。

20+ 歲、約 175cm、黑短髮、黑框眼鏡、黑色系服 T 恤（NFU C.S.I.E）、黑短褲、
黑色後背包、黑襪、紅白排球鞋。個性開朗、有點害羞、喜歡打球、電玩、吉他、寫程式。
"""
from ...rig import MALE
from . import body, faces, head, limbs

ID = "chouma"
NAME = "Chouma"
DESCRIPTION = "愛打排球的資工系大學生"
PROPORTIONS = MALE

PARTS = {
    "back": body.back,
    "hair_back": head.hair_back, "head": head.head, "hair_front": head.hair_front,
    "torso": body.torso, "hips": body.hips,
    "arm_u": limbs.arm_u, "arm_l": limbs.arm_l, "hand": limbs.hand,
    "leg_u": limbs.leg_u, "leg_l": limbs.leg_l, "foot": limbs.foot,
}

# 部件替換圖：動作可用 "alt": {"handNear": "peace"} 指定
VARIANTS = {
    "hand": {"peace": limbs.hand_peace},
}

FACES = faces.make()
