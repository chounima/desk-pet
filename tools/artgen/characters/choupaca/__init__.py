"""Choupaca：Chouma 的羊駝化身。

變成羊駝時脾氣比較暴躁、體力也差（跑不快、跳不高）。
保留 Chouma 的特徵：頭頂一撮黑毛（黑髮）、黑框眼鏡、紅白排球鞋。
外型是原創的毛茸茸雲朵羊駝，使用者提供的參考圖只當「直立羊駝」的概念參考。
"""
from ...rig import Proportions
from . import body, faces, head, limbs, quadruped

ID = "choupaca"
NAME = "Choupaca"
DESCRIPTION = "Chouma 的羊駝化身（暴躁、體力差）"

# 短腿、大毛團身體、短脖子埋在毛裡；臀高不到 Q 版的一半，動作裡的跳躍高度也跟著縮小
PROPORTIONS = Proportions(
    hip=44, waist=8, neck=42, shoulder=(-16, 18, -40), arm=(14, 12),
    hip_joint=(-11, 11, 4), leg=(18, 16), tail=(-42, -2), head_top=(6, -114),
)

PARTS = {
    "tail": body.tail,
    "hair_back": head.hair_back, "head": head.head, "hair_front": head.hair_front,
    "torso": body.torso, "hips": body.hips,
    "arm_u": limbs.arm_u, "arm_l": limbs.arm_l, "hand": limbs.hand,
    "leg_u": limbs.leg_u, "leg_l": limbs.leg_l, "foot": limbs.foot,
}

FACES = faces.make()

# 可切換的型態：預設直立，四足的動作放在 data/animations/quadruped/
FORM_LABEL = "直立"
FORMS = {"quadruped": quadruped}
