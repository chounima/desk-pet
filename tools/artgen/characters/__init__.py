"""所有角色。新增角色：在此資料夾新增模組或子資料夾（提供 ID / NAME / DESCRIPTION / PARTS / FACES，
可選 PROPORTIONS，預設為 Q 版），再加進 ALL。細節多的角色建議拆成資料夾（參考 chouma/）。"""
from . import chouma, mira, sera, tk01

ALL = (tk01, sera, mira, chouma)
