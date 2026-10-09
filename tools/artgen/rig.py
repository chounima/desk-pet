"""共用的人形骨架與部件插槽。

座標系：腳底中心為原點，y 向下為正。角色面朝右（+x），往左走時整體水平翻轉。
「near」是靠近鏡頭的一側（畫在身體前面），「far」是另一側（畫在身體後面、顏色稍暗）。

骨架的長度由 Proportions 決定，動作只記錄角度，所以不同身材的角色可以共用同一套動作。
動作裡的 root 位移是以 Q 版（臀高 96）為基準寫的，執行時乘上 root_scale。
"""
from dataclasses import dataclass

BASE_HIP = 96   # 動作資料的基準臀高


@dataclass(frozen=True)
class Proportions:
    hip: float              # 腳底到臀部骨骼
    waist: float            # 臀部到腰（軀幹關節）
    neck: float             # 腰到脖子（頭部關節）
    shoulder: tuple         # (近側 x, 遠側 x, 相對腰的 y)
    arm: tuple              # (上臂, 前臂)
    hip_joint: tuple        # (近側 x, 遠側 x, 相對臀部的 y)
    leg: tuple              # (大腿, 小腿)；腳踝到腳底的距離由部件圖負責
    tail: tuple             # 尾巴與披布的掛點 (x, y)，相對臀部
    head_top: tuple         # 頭頂在頭部骨骼中的位置，特效錨點用

    @property
    def root_scale(self) -> float:
        return round(self.hip / BASE_HIP, 3)

    @property
    def height(self) -> float:
        """腳底到頭頂的大約高度。"""
        return round(self.hip + self.waist + self.neck - self.head_top[1])


CHIBI = Proportions(
    hip=96, waist=4, neck=66, shoulder=(-20, 17, -58), arm=(32, 28),
    hip_joint=(-9, 9, 4), leg=(44, 42), tail=(-14, -2), head_top=(6, -110),
)

# 約 7.5 頭身：身高約 330
TALL = Proportions(
    hip=170, waist=30, neck=70, shoulder=(-17, 15, -60), arm=(56, 50),
    hip_joint=(-10, 9, 6), leg=(76, 75), tail=(-10, -20), head_top=(0, -62),
)


# 成年男性，約 175cm：肩較寬、臀較窄
MALE = Proportions(
    hip=172, waist=32, neck=72, shoulder=(-21, 19, -62), arm=(58, 52),
    hip_joint=(-10, 10, 6), leg=(78, 76), tail=(-10, -20), head_top=(0, -62),
)


def bones(p: Proportions) -> list[tuple]:
    """(骨骼, 父骨骼, 相對父骨骼的位移 x, y)，父骨骼在前。"""
    near_sx, far_sx, sy = p.shoulder
    near_hx, far_hx, hy = p.hip_joint
    up, low = p.arm
    thigh, shin = p.leg
    return [
        ("root", None, 0, 0),
        ("prop", "root", 0, 0),          # 獨立移動的道具（例如排球），用 prop.x / prop.y 軌道控制
        ("hips", "root", 0, -p.hip),
        ("torso", "hips", 0, -p.waist),
        ("head", "torso", 0, -p.neck),
        ("tail", "hips", *p.tail),
        ("cape", "hips", *p.tail),
        ("armFar_u", "torso", far_sx, sy), ("armFar_l", "armFar_u", 0, up), ("handFar", "armFar_l", 0, low),
        ("armNear_u", "torso", near_sx, sy), ("armNear_l", "armNear_u", 0, up), ("handNear", "armNear_l", 0, low),
        ("legFar_u", "hips", far_hx, hy), ("legFar_l", "legFar_u", 0, thigh), ("footFar", "legFar_l", 0, shin),
        ("legNear_u", "hips", near_hx, hy), ("legNear_l", "legNear_u", 0, thigh), ("footNear", "legNear_l", 0, shin),
    ]


# (插槽, 骨骼, 圖層順序, 部件圖名, 是否為遠側)；角色沒畫的部件會自動略過
SLOTS = [
    ("tail", "tail", 0, "tail", False),
    ("cape", "cape", 0.2, "cape", False),           # 腰後披布
    ("back", "torso", 0.5, "back", False),          # 背在背上的東西
    ("hair_back", "head", 1, "hair_back", False),
    ("armFar_u", "armFar_u", 2, "arm_u", True),
    ("armFar_l", "armFar_l", 3, "arm_l", True),
    ("handFar", "handFar", 4, "hand", True),
    ("legFar_u", "legFar_u", 5, "leg_u", True),
    ("legFar_l", "legFar_l", 6, "leg_l", True),
    ("footFar", "footFar", 7, "foot", True),
    ("legNear_u", "legNear_u", 8, "leg_u", False),
    ("legNear_l", "legNear_l", 9, "leg_l", False),
    ("footNear", "footNear", 10, "foot", False),
    ("hips", "hips", 11, "hips", False),
    ("torso", "torso", 12, "torso", False),
    ("head", "head", 13, "head", False),
    # 14 = 表情（face），見 FACE_SLOT
    ("hair_front", "head", 15, "hair_front", False),
    ("armNear_u", "armNear_u", 16, "arm_u", False),
    ("armNear_l", "armNear_l", 17, "arm_l", False),
    ("handNear", "handNear", 18, "hand", False),
]

FACE_SLOT = ("face", "head", 14)

EXPRESSIONS = ("idle", "blink", "happy", "sleep", "surprised", "focus", "shy")
