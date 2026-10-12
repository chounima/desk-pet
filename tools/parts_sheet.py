"""把角色的部件與表情組成一張分解圖（給準備新角色圖片時對照用）。

用法: python tools/parts_sheet.py chouma                    # → Character_design_sheet/chouma_parts.svg / .png
      python tools/parts_sheet.py choupaca --form quadruped  # 角色型態

讀 art/characters/<id>/ 的 rig.json、parts/、faces/（改了角色要先跑 gen_art.py）。
PNG 用 Chrome／Edge 無頭模式截圖；找不到瀏覽器就只輸出 SVG。
"""
import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "Character_design_sheet"
BROWSERS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

FONT = "Microsoft JhengHei, Noto Sans TC, sans-serif"
INK, SUB, LINE, ACCENT, JOINT = "#24324f", "#6b7280", "#c9ced8", "#2f5fb3", "#e0453a"
W = 1900

# 部件圖名: (說明, 是否必要)
LABELS = {
    "hair_back": ("後髮", True), "head": ("頭（不畫五官）", True), "hair_front": ("瀏海", True),
    "torso": ("上身", True), "hips": ("下身／腰", True),
    "back": ("背後的東西", False), "cape": ("披布", False), "tail": ("尾巴", False),
    "arm_u": ("上臂", True), "arm_l": ("前臂", True), "hand": ("手", True), "hand_peace": ("比 YA 的手", False),
    "leg_u": ("大腿", True), "leg_l": ("小腿", True), "foot": ("腳／鞋", True),
}
ROWS = [["hair_back", "head", "hair_front"], ["torso", "hips", "back", "cape", "tail"],
        ["arm_u", "arm_l", "hand", "hand_peace"], ["leg_u", "leg_l", "foot"]]
FACE_LABELS = {"idle": "平常", "blink": "眨眼", "happy": "開心", "sleep": "睡覺",
               "surprised": "驚訝", "focus": "專注", "shy": "害羞", "angry": "生氣"}


class Sheet:
    def __init__(self):
        self.out = []
        self._n = 0

    def add(self, s):
        self.out.append(s)

    def embed(self, path, x, y, w, h, extra=""):
        """把一個 SVG 檔嵌進來（id 加前綴，避免各檔案的 id 互相衝突）"""
        self._n += 1
        src = path.read_text(encoding="utf-8")
        vb = re.search(r'viewBox="([^"]+)"', src).group(1)
        body = re.sub(r"^.*?<svg[^>]*>", "", src, count=1, flags=re.S).rsplit("</svg>", 1)[0]
        p = f"e{self._n}_"
        body = re.sub(r'id="([^"]+)"', rf'id="{p}\1"', body)
        body = re.sub(r"url\(#([^)]+)\)", rf"url(#{p}\1)", body)
        body = re.sub(r'href="#([^"]+)"', rf'href="#{p}\1"', body)
        return (f'<svg x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" viewBox="{vb}" '
                f'preserveAspectRatio="none" overflow="visible" {extra}>{body}</svg>')


def viewbox(path):
    return [float(v) for v in re.search(r'viewBox="([^"]+)"', path.read_text(encoding="utf-8")).group(1).split()]


def text(x, y, s, size=16, color=INK, weight=400, anchor="start"):
    return (f'<text x="{x:.1f}" y="{y:.1f}" font-family="{FONT}" font-size="{size}" fill="{color}" '
            f'font-weight="{weight}" text-anchor="{anchor}">{s}</text>')


def dot(x, y, r=5):
    return f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="{JOINT}" stroke="#fff" stroke-width="2"/>'


def build(char_dir: Path) -> tuple[str, int]:
    rig = json.loads((char_dir / "rig.json").read_text(encoding="utf-8"))
    bones = {b["name"]: b for b in rig["bones"]}
    sheet = Sheet()
    add = sheet.add

    def world(name):
        """站姿（所有角度 0）時骨頭在角色座標的位置"""
        b = bones[name]
        if b["parent"] is None:
            return b["x"], b["y"]
        px, py = world(b["parent"])
        return px + b["x"], py + b["y"]

    # ── 標題 ──
    add(text(48, 70, f"{rig['name']} 部件分解圖", 38, INK, 700))
    add(text(48, 104, "每個部件一張透明背景圖。紅點＝關節（旋轉中心，部件座標的原點），虛線框＝圖片範圍。"
             "手腳只要畫一隻，另一邊共用同一張圖。", 17, SUB))

    # ── 左：組裝好的樣子 ──
    # 依所有部件站姿時的外框決定縮放，整隻角色放進 460×800 的範圍
    boxes = [(world(s["bone"]), s["box"]) for s in rig["slots"] if s["bone"] in bones]
    bx0 = min(b[0] + x0 for b, (x0, _, _, _) in boxes)
    bx1 = max(b[0] + x1 for b, (_, _, x1, _) in boxes)
    by0 = min(b[1] + y0 for b, (_, y0, _, _) in boxes)
    by1 = max(b[1] + y1 for b, (_, _, _, y1) in boxes)
    sa = min(800 / (by1 - by0), 460 / (bx1 - bx0), 4)
    ox, oy = 300 - (bx0 + bx1) / 2 * sa, 1010 - by1 * sa   # root 在畫面上的位置（腳底貼齊地面線）
    add(f'<rect x="40" y="140" width="520" height="1000" rx="18" fill="#f4f6fa"/>')
    add(text(60, 178, "組裝後（站姿）", 22, INK, 700))
    add(f'<line x1="70" y1="{oy}" x2="530" y2="{oy}" stroke="{LINE}" stroke-width="2"/>')
    for slot in sorted(rig["slots"], key=lambda s: s["z"]):
        file = slot.get("file") or slot.get("variants", {}).get("idle")
        if slot["bone"] not in bones or not file or not (char_dir / file).exists():
            continue
        bx, by = world(slot["bone"])
        x0, y0, x1, y1 = slot["box"]
        extra = 'filter="url(#far)"' if slot.get("far") else ""
        add(sheet.embed(char_dir / file, ox + (bx + x0) * sa, oy + (by + y0) * sa, (x1 - x0) * sa, (y1 - y0) * sa, extra))
    for name in bones:
        if name not in ("root", "prop", "tail", "cape"):
            bx, by = world(name)
            add(dot(ox + bx * sa, oy + by * sa, 4))
    add(text(300, 1060, "遠側的手腳（較暗）＝同一張圖", 15, SUB, anchor="middle"))
    add(text(300, 1084, "紅點是骨架的關節位置", 15, SUB, anchor="middle"))

    # ── 右：拆開的部件（同一個縮放比例，看得出相對大小） ──
    # 同一個縮放比例；最高的部件（通常是頭部組）不超過 300px
    tallest = max((viewbox(p)[3] for p in (char_dir / "parts").glob("*.svg")), default=100)
    sp = min(1.75, 300 / tallest)
    px0, py = 600, 140
    for row in ROWS:
        items = [(a, char_dir / "parts" / f"{a}.svg") for a in row if (char_dir / "parts" / f"{a}.svg").exists()]
        if not items:
            continue
        vbs = [viewbox(p) for _, p in items]
        rh = max(v[3] for v in vbs) * sp
        x = px0
        for (art, path), (vx, vy, vw, vh) in zip(items, vbs):
            w, h = vw * sp, vh * sp
            cw = max(w, 150) + 30
            label, required = LABELS.get(art, (art, True))
            add(f'<rect x="{x}" y="{py}" width="{cw}" height="{rh + 92}" rx="14" fill="#fff" stroke="{LINE}"/>')
            ix, iy = x + (cw - w) / 2, py + 14
            add(f'<rect x="{ix:.1f}" y="{iy:.1f}" width="{w:.1f}" height="{h:.1f}" fill="none" '
                f'stroke="#9aa3b2" stroke-dasharray="5 4"/>')
            add(sheet.embed(path, ix, iy, w, h))
            add(dot(ix - vx * sp, iy - vy * sp))
            ty = py + rh + 46
            add(text(x + cw / 2, ty, label, 17, INK, 700, "middle"))
            tag, tc = ("必要", ACCENT) if required else ("選配", SUB)
            add(text(x + cw / 2, ty + 24, f"{art}.png · {tag}", 14, tc, 400, "middle"))
            x += cw + 16
        py += rh + 92 + 16

    # ── 下：表情（疊在淡化的頭上） ──
    face_slot = next((s for s in rig["slots"] if s.get("variants")), None)
    fy = max(py, 1140) + 30
    if face_slot:
        variants = face_slot["variants"]
        add(text(48, fy, f"表情 faces/（{len(variants)} 張，只畫眼睛、眉毛、嘴巴；疊在空白的頭上）", 22, INK, 700))
        head = char_dir / "parts" / "head.svg"
        hx0, hy0, hw, hh = viewbox(head)
        cw = 222
        sf = (cw - 30) / hw
        ch = hh * sf + 80
        fx = 48
        for key, file in variants.items():
            if not (char_dir / file).exists():
                continue
            add(f'<rect x="{fx}" y="{fy + 18}" width="{cw}" height="{ch}" rx="14" fill="#fff" stroke="{LINE}"/>')
            cx, cy = fx + 15, fy + 26                     # 頭部圖左上角在畫面上的位置
            add(f'<g opacity="0.28">{sheet.embed(head, cx, cy, hw * sf, hh * sf)}</g>')
            x0, y0, x1, y1 = face_slot["box"]
            add(sheet.embed(char_dir / file, cx + (x0 - hx0) * sf, cy + (y0 - hy0) * sf, (x1 - x0) * sf, (y1 - y0) * sf))
            add(text(fx + cw / 2, fy + ch - 22, FACE_LABELS.get(key, key), 17, INK, 700, "middle"))
            add(text(fx + cw / 2, fy + ch + 2, f"{key}.png" + (" · 必要" if key == "idle" else ""),
                     14, ACCENT if key == "idle" else SUB, 400, "middle"))
            fx += cw + 4
        fy += ch + 18

    order = " → ".join(s["name"] for s in sorted(rig["slots"], key=lambda s: s["z"]))
    h = int(fy + 50)
    add(text(48, h - 22, f"疊放順序（後 → 前）：{order}", 13, SUB))

    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {h}" width="{W}" height="{h}">'
           '<defs><filter id="far"><feComponentTransfer><feFuncR type="linear" slope="0.82"/>'
           '<feFuncG type="linear" slope="0.82"/><feFuncB type="linear" slope="0.82"/></feComponentTransfer></filter></defs>'
           f'<rect width="{W}" height="{h}" fill="#fbfaf7"/>' + "".join(sheet.out) + "</svg>")
    return svg, h


def to_png(svg_path: Path, png_path: Path, height: int) -> bool:
    browser = next((b for b in BROWSERS if Path(b).exists()), None) or shutil.which("chrome") or shutil.which("msedge")
    if not browser:
        return False
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    f"--window-size={W},{height}", f"--screenshot={png_path}", svg_path.as_uri()],
                   check=True, capture_output=True)
    return png_path.exists()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("char", help="角色 id，例如 chouma")
    ap.add_argument("--form", help="角色型態，例如 quadruped")
    args = ap.parse_args()

    char_dir = ROOT / "art" / "characters" / args.char
    if args.form:
        char_dir = char_dir / "forms" / args.form
    name = f"{args.char}_{args.form}_parts" if args.form else f"{args.char}_parts"
    OUT_DIR.mkdir(exist_ok=True)
    svg_path, png_path = OUT_DIR / f"{name}.svg", OUT_DIR / f"{name}.png"

    svg, height = build(char_dir)
    svg_path.write_text(svg, encoding="utf-8")
    print(svg_path)
    print(png_path if to_png(svg_path, png_path, height) else "找不到 Chrome／Edge，只輸出 SVG")
