"""部件包檢查（人工、AI 輔助的美術都跑）：找出常見問題，輸出 HTML 報告（附每個部件的縮圖）。

檢查項目：
  缺少的部件（會用範本角色的）、不認識的部件名、缺少的表情
  解析度：每 Q 版單位幾像素（4K 300% 大尺寸約需 4；太大浪費記憶體）
  背景不透明、去背殘邊（半透明雜點、白邊）
  部件之間線條顏色、亮度差太多（AI 產生時畫風不一致）
  手腳比範本短很多（關節處可能沒有多畫重疊，彎曲時會露出縫）

用法: python tools/art_check.py art_src/chouma [--out build/art_check]   （也可以檢查 art/packages/<id>）
結束碼：有錯誤 1，只有提醒 0
"""
import argparse
import base64
import html
import io
import json
import statistics
import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
EXPRESSIONS = ("idle", "blink", "happy", "sleep", "surprised", "focus", "shy", "angry")
LIMBS = ("arm_u", "arm_l", "leg_u", "leg_l")
MIN_PX, GOOD_PX, MAX_PX = 2.5, 4, 16      # 每 Q 版單位幾像素


class Part:
    def __init__(self, name: str, path: Path, box: list[float]):
        self.name, self.path, self.box = name, path, box
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.img = Image.open(path)
        self.stats: dict = {}


def template(base: str, form: str | None) -> tuple[dict[str, list[float]], list[float] | None]:
    folder = ROOT / "art" / "characters" / base
    rig = json.loads((folder / "rig.json").read_text(encoding="utf-8"))
    if form and (rig.get("forms") or {}).get(form, {}).get("rig"):
        rig = json.loads((folder / rig["forms"][form]["rig"] / "rig.json").read_text(encoding="utf-8"))
    boxes, face = {}, None
    for s in rig["slots"]:
        if s.get("variants"):
            face = s["box"]
        elif s.get("file"):
            boxes.setdefault(Path(s["file"]).stem, s["box"])
    return boxes, face


def analyze(p: Part) -> None:
    img = p.img
    w, h = img.size
    bw, bh = p.box[2] - p.box[0], p.box[3] - p.box[1]
    ppu = min(w / bw, h / bh) if bw > 0 and bh > 0 else 0
    p.stats["size"] = f"{w}×{h}"
    p.stats["px/單位"] = f"{ppu:.1f}"
    if ppu < MIN_PX:
        p.errors.append(f"解析度太低（每單位 {ppu:.1f} 像素，至少 {MIN_PX}，建議 {GOOD_PX}）：4K 放大會糊")
    elif ppu < GOOD_PX - 0.05:          # 範圍不是整數時會差一點點
        p.warnings.append(f"解析度偏低（每單位 {ppu:.1f} 像素，建議 {GOOD_PX}）")
    elif ppu > MAX_PX:
        p.warnings.append(f"解析度太高（每單位 {ppu:.1f} 像素），浪費記憶體，可以縮小到 {GOOD_PX}～8")
    if abs((w / h) - (bw / bh)) / (bw / bh) > 0.08:
        p.warnings.append(f"圖片比例 {w / h:.2f} 和範圍比例 {bw / bh:.2f} 不同，會被拉伸")

    if img.mode not in ("RGBA", "LA", "PA") and "transparency" not in img.info:
        p.errors.append("沒有透明背景（存成有透明度的 PNG）")
        return
    rgba = img.convert("RGBA")
    px = list(rgba.get_flattened_data())
    corners = [rgba.getpixel((0, 0)), rgba.getpixel((w - 1, 0)), rgba.getpixel((0, h - 1)), rgba.getpixel((w - 1, h - 1))]
    if sum(1 for c in corners if c[3] > 200) >= 3:
        p.errors.append("四個角落是不透明的：背景可能沒去掉")
    opaque = [c for c in px if c[3] >= 245]
    semi = [c for c in px if 10 < c[3] < 245]
    if not opaque:
        p.errors.append("整張圖都是透明的")
        return
    # 去背殘留的雜點：半透明、而且附近（3 像素內）沒有不透明像素的點。
    # 正常的反鋸齒邊緣一定貼著不透明的線條，細線條（表情）的半透明比例高也沒關係
    # 刻意畫的半透明（腮紅、頭髮反光）是一整片，雜點是零星的：只算周圍 3×3 幾乎沒有同類的點
    alpha = rgba.getchannel("A")
    near_opaque = alpha.point(lambda v: 255 if v >= 245 else 0).filter(ImageFilter.MaxFilter(7))
    far = [10 < a < 245 and n == 0 for a, n in zip(alpha.get_flattened_data(), near_opaque.get_flattened_data())]
    mask = Image.new("L", alpha.size)
    mask.putdata([28 if f else 0 for f in far])           # 28 × 9 格 = 252，加總不會超過 255
    neighbours = mask.filter(ImageFilter.Kernel((3, 3), [1] * 9, scale=1)).get_flattened_data()
    specks = sum(1 for f, n in zip(far, neighbours) if f and n // 28 <= 2)     # 自己＋最多一個鄰居
    p.stats["雜點"] = specks
    if specks > max(30, len(opaque) * 0.005):
        p.warnings.append(f"有 {specks} 個離線條很遠的半透明雜點：去背可能沒去乾淨（霧邊、殘影）")
    light_fringe = sum(1 for c in semi if c[3] < 160 and min(c[:3]) > 225)
    if semi and light_fringe / len(semi) > 0.25:
        p.warnings.append("邊緣有白邊（去背時留下的淺色半透明像素），疊在深色上會很明顯")
    # 線條顏色：最暗的 5% 不透明像素平均；亮度：不透明像素平均
    lum = sorted((0.299 * r + 0.587 * g + 0.114 * b, (r, g, b)) for r, g, b, _ in opaque)
    darkest = lum[: max(1, len(lum) // 20)]
    p.stats["line"] = tuple(round(statistics.mean(c[1][i] for c in darkest)) for i in range(3))
    p.stats["亮度"] = round(statistics.mean(l for l, _ in lum))


def thumbnail(img: Image.Image, size: int = 120) -> str:
    t = img.convert("RGBA").copy()
    t.thumbnail((size, size))
    bg = Image.new("RGBA", t.size, (226, 229, 236, 255))
    for y in range(0, t.height, 8):
        for x in range((y // 8) % 2 * 8, t.width, 16):
            bg.paste((205, 208, 216, 255), (x, y, x + 8, y + 8))
    bg.alpha_composite(t)
    buf = io.BytesIO()
    bg.save(buf, "PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def check(folder: Path) -> tuple[list[Part], list[str], list[str], dict]:
    pkg = json.loads((folder / "character.json").read_text(encoding="utf-8"))
    base = pkg.get("base") or folder.name
    boxes, face_box = template(base, pkg.get("form"))
    parts: list[Part] = []
    errors, warnings = [], []
    for name, spec in pkg.get("parts", {}).items():
        if name not in boxes:
            warnings.append(f"「{name}」不是範本角色 {base} 的部件，不會顯示")
        f = folder / spec["image"]
        if not f.exists():
            errors.append(f"找不到 {spec['image']}")
            continue
        part = Part(name, f, spec["box"])
        # 手腳比範本短很多：關節處可能沒有多畫重疊
        if name in LIMBS and name in boxes:
            tb = boxes[name]
            ratio = (spec["box"][3] - spec["box"][1]) / (tb[3] - tb[1])
            if ratio < 0.85:
                part.warnings.append(f"比範本短 {1 - ratio:.0%}：關節處（兩端）可能沒有多畫重疊，彎曲時會露出縫")
        parts.append(part)
        for alt, a in (spec.get("alt") or {}).items():
            if (folder / a["image"]).exists():
                parts.append(Part(f"{name}@{alt}", folder / a["image"], a["box"]))
    missing = sorted(set(boxes) - set(pkg.get("parts", {})))
    if missing:
        warnings.append(f"沒有畫的部件（會用範本角色的佔位圖）：{', '.join(missing)}")
    faces = (pkg.get("faces") or {}).get("images", {})
    for e, file in faces.items():
        if (folder / file).exists():
            parts.append(Part(f"表情 {e}", folder / file, pkg["faces"]["box"]))
        else:
            errors.append(f"找不到表情 {file}")
    lack = [e for e in EXPRESSIONS if e not in faces]
    if faces and lack:
        warnings.append(f"沒有做的表情（會用 idle）：{', '.join(lack)}")
    if not faces:
        warnings.append("沒有表情圖（會用範本角色的臉）")

    for p in parts:
        analyze(p)
    # 畫風一致性：線條顏色、亮度和其他部件的中位數差太多（表情不算）
    body = [p for p in parts if "line" in p.stats and not p.name.startswith("表情")]
    if len(body) >= 3:
        med = [statistics.median(p.stats["line"][i] for p in body) for i in range(3)]
        med_l = statistics.median(p.stats["亮度"] for p in body)
        for p in body:
            dist = sum((p.stats["line"][i] - med[i]) ** 2 for i in range(3)) ** 0.5
            if dist > 70:
                p.warnings.append(f"線條顏色 {p.stats['line']} 和其他部件（約 {tuple(round(m) for m in med)}）差很多：畫風可能不一致")
    return parts, errors, warnings, pkg


def report_html(folder: Path, parts: list[Part], errors: list[str], warnings: list[str], pkg: dict) -> str:
    rows = []
    for p in parts:
        issues = "".join(f'<div class="err">✗ {html.escape(e)}</div>' for e in p.errors)
        issues += "".join(f'<div class="warn">⚠ {html.escape(w)}</div>' for w in p.warnings)
        stats = " · ".join(f"{k} {v}" for k, v in p.stats.items() if k != "line")
        rows.append(f'<tr class="{"bad" if p.errors else "warnrow" if p.warnings else ""}"><td><img src="{thumbnail(p.img)}"></td>'
                    f'<td><b>{html.escape(p.name)}</b><div class="muted">{html.escape(str(p.path.relative_to(folder)))}</div>'
                    f'<div class="muted">{stats}</div></td><td>{issues or "<span class=ok>✓</span>"}</td></tr>')
    total_err = len(errors) + sum(len(p.errors) for p in parts)
    total_warn = len(warnings) + sum(len(p.warnings) for p in parts)
    top = "".join(f'<li class="err">✗ {html.escape(e)}</li>' for e in errors) + "".join(f'<li class="warn">⚠ {html.escape(w)}</li>' for w in warnings)
    src = pkg.get("source") or {}
    return f"""<!doctype html><html lang="zh-Hant"><head><meta charset="utf-8"><title>部件包檢查 {html.escape(folder.name)}</title>
<style>body{{font:14px/1.5 system-ui,sans-serif;margin:24px;background:#1f2230;color:#e8e8ef}}table{{border-collapse:collapse;width:100%}}
td{{border-bottom:1px solid #3a3f55;padding:8px;vertical-align:top}}img{{border-radius:6px}}.muted{{color:#9aa0b4;font-size:12px}}
.err{{color:#ff7a85}}.warn{{color:#ffd75e}}.ok{{color:#6fdc8c}}tr.bad td{{background:#3a2028}}tr.warnrow td{{background:#2f2c22}}</style></head><body>
<h1>部件包檢查：{html.escape(pkg.get("name") or folder.name)}</h1>
<p>{html.escape(str(folder))}　範本 {html.escape(pkg.get("base") or folder.name)}{("@" + html.escape(pkg["form"])) if pkg.get("form") else ""}
來源 {html.escape(src.get("method", "未填"))} {html.escape(", ".join(src.get("tools", [])))}</p>
<h2 class="{"err" if total_err else "warn" if total_warn else "ok"}">{total_err} 個錯誤、{total_warn} 個提醒</h2><ul>{top}</ul>
<table>{"".join(rows)}</table>
<p class="muted">規格見 docs/ART_GUIDE.md；AI 輔助流程見 docs/ART_GUIDE_AI.md。
組裝起來看全部動作與表情：python tools/serve.py → <a href="http://localhost:8787/preview/gallery.html?pkg={html.escape(folder.name)}">preview/gallery.html?pkg={html.escape(folder.name)}</a>（不用先匯入）；
調整位置：preview/editor.html 的「📦 部件包」</p>
</body></html>"""


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("folder", type=Path)
    ap.add_argument("--out", type=Path, default=ROOT / "build" / "art_check")
    a = ap.parse_args()
    folder = a.folder.resolve()
    if not (folder / "character.json").exists():
        print(f"✗ {folder} 沒有 character.json（只有圖片的話先跑 tools/import_layers.py --dry-run 看看，或直接匯入產生草稿）")
        return 1
    parts, errors, warnings, pkg = check(folder)
    a.out.mkdir(parents=True, exist_ok=True)
    out = a.out / f"{folder.name}.html"
    out.write_text(report_html(folder, parts, errors, warnings, pkg), encoding="utf-8")
    n_err = len(errors) + sum(len(p.errors) for p in parts)
    n_warn = len(warnings) + sum(len(p.warnings) for p in parts)
    for e in errors + [f"{p.name}：{e}" for p in parts for e in p.errors]:
        print(f"✗ {e}")
    for w in warnings + [f"{p.name}：{w}" for p in parts for w in p.warnings]:
        print(f"⚠ {w}")
    print(f"{n_err} 個錯誤、{n_warn} 個提醒 → {out}")
    return 1 if n_err else 0


if __name__ == "__main__":
    sys.exit(main())
