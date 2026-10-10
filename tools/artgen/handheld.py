"""拿在手上的道具（商店的新動作用）。座標是手掌骨骼的局部座標：手腕在原點，手掌往 +y。
動作會用 handNear 軌道把手腕轉正，所以這裡照「直立」的樣子畫。"""
from .shapes import stroke
from .svg import Part

OL = "#24161b"
L = stroke(OL, 2)


def mug() -> Part:
    """咖啡杯（冒著熱氣）。"""
    return Part(
        f'<path d="M-7,-2 L7,-2 L6,14 Q0,16 -6,14 Z" fill="#f4efe6" {L}/>'
        f'<path d="M7,2 Q13,3 12,8 Q11,12 6,11" fill="none" {L}/>'
        '<ellipse cx="0" cy="-1" rx="6" ry="1.6" fill="#6b3f22"/>'
        '<path d="M-6,5 L6,5" stroke="#c94f3a" stroke-width="3"/>'
        '<path d="M-2,-5 Q-4,-9 -2,-12 M3,-5 Q1,-9 3,-13" fill="none" stroke="#ffffff" '
        'stroke-width="1.6" stroke-linecap="round" opacity="0.7"/>',
        (-10, -16, 16, 18))


def guitar() -> Part:
    """木吉他，斜抱在身前：琴身在肚子前、琴頸往前上方伸。座標是軀幹骨骼（腰在原點、往上為負）。"""
    wood, dark, neck = "#d9913f", "#8a4f1c", "#5a3518"
    body = ("M-22,-18 C-30,-30 -18,-44 -6,-38 C2,-46 18,-40 16,-26 C26,-18 16,0 2,-4 "
            "C-6,6 -26,4 -22,-18 Z")
    return Part(
        f'<path d="M6,-28 L58,-56 L61,-50 L9,-22 Z" fill="{neck}" {L}/>'          # 琴頸
        f'<path d="M56,-58 L68,-64 L71,-56 L60,-50 Z" fill="{dark}" {L}/>'         # 琴頭
        '<circle cx="64" cy="-63" r="1.6" fill="#e8e0d0"/><circle cx="69" cy="-58" r="1.6" fill="#e8e0d0"/>'
        f'<path d="{body}" fill="{wood}" {L}/>'
        '<path d="M-14,-24 C-16,-32 -8,-36 -2,-32" fill="none" stroke="#f0b870" stroke-width="2.5" stroke-linecap="round" opacity="0.7"/>'
        f'<circle cx="0" cy="-20" r="6" fill="#3a2414" {stroke(OL, 1.5)}/>'          # 音孔
        f'<rect x="-14" y="-12" width="8" height="3" rx="1" fill="{dark}" transform="rotate(-28 -10 -10)"/>'
        '<path d="M-10,-11 L60,-53 M-9,-9 L61,-51" stroke="#f4efe6" stroke-width="0.6" opacity="0.8"/>',
        (-34, -70, 76, 10))


def book() -> Part:
    """打開的書（側面看是一個 V）。"""
    return Part(
        f'<path d="M0,10 L-14,-4 L-12,-8 L0,4 Z" fill="#f6f0e0" {L}/>'
        f'<path d="M0,10 L14,-4 L12,-8 L0,4 Z" fill="#fbf7ec" {L}/>'
        f'<path d="M0,12 L-16,-3 M0,12 L16,-3" stroke="#2f6fd6" stroke-width="3" stroke-linecap="round"/>'
        '<path d="M-10,-3 L-3,4 M-8,-5 L-2,1 M10,-3 L3,4 M8,-5 L2,1" stroke="#9a9488" stroke-width="1"/>',
        (-18, -11, 18, 15))
