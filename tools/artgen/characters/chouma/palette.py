"""Chouma 的色票（依角色設定圖）。"""
from ...shapes import stroke

OL = "#1a1412"

HAIR, HAIR_MID, HAIR_HL = "#1f1c1b", "#2e2a28", "#6a625d"
SKIN, SKIN_SHADE, SKIN_DEEP, SKIN_HL = "#e3a97c", "#c68a5e", "#a96d45", "#f4cba6"

SHIRT, SHIRT_SHADE, SHIRT_HL = "#1d1e23", "#111216", "#3c3e47"
SHORTS, SHORTS_SHADE, SHORTS_HL = "#2a2b30", "#18191c", "#45474e"
REFLECT = "#d4d7dd"
WHITE = "#f6f4ef"

SOCK, SOCK_RIB = "#18181b", "#2c2c31"
RED, RED_DARK = "#e0352a", "#a3221a"
SOLE, SOLE_LINE = "#e6e0d6", "#b8b0a2"
LACE = "#222226"

GLASSES = "#151515"
BAG, BAG_SHADE, BAG_HL = "#222327", "#131416", "#34363d"
BUCKLE = "#8a8d94"

EYE_DARK, EYE_IRIS, EYE_LIGHT = "#22140e", "#6b4426", "#b5824e"
MOUTH, MOUTH_IN, TONGUE = "#7a2c26", "#5e1f1b", "#c4504a"
BLUSH = "#e8786a"

L = stroke(OL, 2)          # 身體（原尺寸）
LH = stroke(OL, 4.5)       # 頭部（縮小 0.47 後約 2.1）

HEAD_SCALE, HEAD_DY = 0.5, -11
