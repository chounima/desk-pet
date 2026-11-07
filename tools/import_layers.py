"""把美術原始檔匯入成角色部件包：art_src/<id>/ → art/packages/<id>/（桌寵與網頁都會讀）。

來源可以是（人工、AI 輔助都一樣）：
  1. 資料夾：parts/<部件名>.png（替換圖 <部件名>@<替換名>.png）、faces/<表情>.png，可以有 character.json
  2. Krita .ora 分層檔：圖層名 = 部件名（hand@peace 是替換圖；face:idle 或 face_idle 是表情）
  3. Photoshop .psd：同 .ora（需要 python -m pip install psd-tools）
部件名對應範本角色的部件圖檔名（torso、arm_u、hair_front…，見 docs/ART_GUIDE.md）。

沒有 character.json 時會產生草稿：每個部件放在範本角色同名部件的範圍裡（保持圖片比例、置中），
之後在網頁編輯器（preview/editor.html 綁骨模式）微調。

用法:
  python tools/import_layers.py art_src/chouma                   （資料夾，id = 資料夾名）
  python tools/import_layers.py art_src/sera/sera.ora --id sera   （分層檔）
  python tools/import_layers.py art_src/newchar --base mira --id newchar --name 新角色
選項: --base 範本角色（預設 = id）、--form 範本型態、--dry-run 只檢查不寫入
需要: Pillow、jsonschema（tools/requirements.txt）
"""
import argparse
import io
import json
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
FACE_PREFIXES = ("face:", "face_", "faces/")
# 之後的階段才用到、但美術可以先一次產齊的部件：原樣保留在部件包裡（見 docs/ART_GUIDE.md「預先產出」）
#   face_parts/ 分層表情（第 3 階段）　hair_parts/ 頭髮分節（第 3 階段）　bust/ 正面半身（第 6 階段）
RESERVED = {"face_parts": "face", "hair_parts": "hair", "bust": "views.bust"}
EXPRESSIONS = ("idle", "blink", "happy", "sleep", "surprised", "focus", "shy", "angry")


# ---------- 讀來源：回傳 { 名稱: Image }（名稱：部件名、部件名@替換名、face:表情） ----------

def read_folder(folder: Path) -> dict[str, Image.Image]:
    layers = {}
    for f in sorted((folder / "parts").glob("*")) if (folder / "parts").exists() else []:
        if f.suffix.lower() in (".png", ".webp", ".jpg", ".jpeg"):
            layers[f.stem] = Image.open(f)
    for f in sorted((folder / "faces").glob("*")) if (folder / "faces").exists() else []:
        if f.suffix.lower() in (".png", ".webp", ".jpg", ".jpeg"):
            layers[f"face:{f.stem}"] = Image.open(f)
    for sub in RESERVED:
        for f in sorted((folder / sub).glob("*")) if (folder / sub).exists() else []:
            if f.suffix.lower() in (".png", ".webp", ".jpg", ".jpeg"):
                layers[f"{sub}/{f.stem}"] = Image.open(f)
    return layers


def read_ora(path: Path) -> dict[str, Image.Image]:
    """OpenRaster：zip 裡 stack.xml 列出圖層（name、src），圖層圖片是 PNG"""
    layers = {}
    with zipfile.ZipFile(path) as z:
        stack = ET.fromstring(z.read("stack.xml"))
        for layer in stack.iter("layer"):
            if layer.get("visibility", "visible") == "hidden":
                continue
            img = Image.open(io.BytesIO(z.read(layer.get("src")))).convert("RGBA")
            layers[normalize(layer.get("name", ""))] = crop(img)
    return layers


def read_psd(path: Path) -> dict[str, Image.Image]:
    try:
        from psd_tools import PSDImage
    except ImportError:
        sys.exit("讀 .psd 需要 psd-tools：python -m pip install psd-tools（或在繪圖軟體另存成 .ora／一層一張 PNG）")
    layers = {}
    for layer in PSDImage.open(path).descendants():
        if layer.is_group() or not layer.is_visible():
            continue
        layers[normalize(layer.name)] = crop(layer.composite().convert("RGBA"))
    return layers


def normalize(name: str) -> str:
    name = name.strip().replace("\\", "/")
    if name.split("/")[0] in RESERVED:
        return name
    for p in FACE_PREFIXES:
        if name.lower().startswith(p):
            return "face:" + name[len(p):]
    return name


def crop(img: Image.Image) -> Image.Image:
    """裁掉四周全透明的部分（分層檔的每一層都是整張畫布大小）"""
    box = img.getchannel("A").getbbox()
    return img.crop(box) if box else img


# ---------- 範本角色 ----------

def template_boxes(base: str, form: str | None) -> tuple[dict[str, list[float]], list[float] | None]:
    """範本角色每張部件圖的範圍 { 部件名: box }、表情的範圍"""
    folder = ROOT / "art" / "characters" / base
    rig = json.loads((folder / "rig.json").read_text(encoding="utf-8"))
    if form and form in (rig.get("forms") or {}) and rig["forms"][form]["rig"]:
        rig = json.loads((folder / rig["forms"][form]["rig"] / "rig.json").read_text(encoding="utf-8"))
    boxes, face = {}, None
    for s in rig["slots"]:
        if s.get("variants"):
            face = s["box"]
        elif s.get("file"):
            boxes.setdefault(Path(s["file"]).stem, s["box"])
            for alt, a in (s.get("alt") or {}).items():
                boxes.setdefault(f"{Path(s['file']).stem}@{alt}", a["box"])
    return boxes, face


def fit(box: list[float], img: Image.Image) -> list[float]:
    """把圖片放進範本的範圍：保持比例、置中（草稿用，之後在編輯器微調）"""
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    scale = min(bw / img.width, bh / img.height)
    w, h = img.width * scale, img.height * scale
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return [round(cx - w / 2, 2), round(cy - h / 2, 2), round(cx + w / 2, 2), round(cy + h / 2, 2)]


# ---------- 匯入 ----------

def build(layers: dict[str, Image.Image], draft: dict | None, base: str, form: str | None,
          name: str | None, warnings: list[str]) -> tuple[dict, dict[str, Image.Image]]:
    """回傳 (character.json, { 輸出檔名: 圖 })"""
    boxes, face_box = template_boxes(base, form)
    pkg = dict(draft or {})
    pkg.setdefault("version", 1)
    pkg["base"] = base
    if form:
        pkg["form"] = form
    if name:
        pkg["name"] = name
    old_parts = pkg.get("parts", {})
    parts, files = {}, {}
    reserved: dict[str, dict[str, str]] = {}
    for key, img in layers.items():
        if key.startswith("face:"):
            continue
        if key.split("/")[0] in RESERVED:
            sub, stem = key.split("/", 1)
            files[f"{sub}/{stem}.png"] = img
            reserved.setdefault(sub, {})[stem] = f"{sub}/{stem}.png"
            continue
        part, _, alt = key.partition("@")
        if part not in boxes:
            warnings.append(f"「{key}」不是範本角色 {base} 的部件（可用：{', '.join(sorted(k for k in boxes if '@' not in k))}），略過")
            continue
        file = f"parts/{key}.png"
        files[file] = img
        # 已經有的範圍（編輯器調過的）保留，沒有的才用範本的範圍自動放
        if alt:
            prev = old_parts.get(part, {}).get("alt", {}).get(alt, {}).get("box")
            parts.setdefault(part, {}).setdefault("alt", {})[alt] = {"image": file, "box": prev or fit(boxes.get(key, boxes[part]), img)}
        else:
            prev = old_parts.get(part, {}).get("box")
            parts.setdefault(part, {}).update({"image": file, "box": prev or fit(boxes[part], img)})
    for part, p in list(parts.items()):
        if "image" not in p:
            warnings.append(f"「{part}」只有替換圖、沒有基本圖，略過")
            del parts[part]
    pkg["parts"] = parts

    faces = {k[5:]: img for k, img in layers.items() if k.startswith("face:")}
    if faces:
        unknown = [e for e in faces if e not in EXPRESSIONS]
        if unknown:
            warnings.append(f"不認識的表情 {', '.join(unknown)}（可用：{', '.join(EXPRESSIONS)}）")
        if "idle" not in faces:
            warnings.append("表情沒有 idle（沒做的表情會用 idle 代替）")
        for e, img in faces.items():
            files[f"faces/{e}.png"] = img
        first = faces.get("idle") or next(iter(faces.values()))
        old_box = (pkg.get("faces") or {}).get("box")
        pkg["faces"] = {"box": old_box or fit(face_box, first), "images": {e: f"faces/{e}.png" for e in faces}}
    # 預先產出的部件：記在預留欄位（face、hair、views.bust），之後的階段接上
    for sub, images in reserved.items():
        target = RESERVED[sub]
        if target == "views.bust":
            pkg.setdefault("views", {}).setdefault("bust", {})["images"] = images
        else:
            pkg.setdefault(target, {})["images"] = images
    if reserved:
        warnings.append("預先產出的部件（之後的階段才會用到）已保留：" + "、".join(f"{k} {len(v)} 張" for k, v in reserved.items()))
    missing = sorted(k for k in boxes if "@" not in k and k not in parts)
    if missing:
        warnings.append(f"沒有畫的部件會用範本角色的：{', '.join(missing)}")
    return pkg, files


def write(out: Path, pkg: dict, files: dict[str, Image.Image], src: Path | None) -> None:
    if out.exists():
        shutil.rmtree(out)
    for file, img in files.items():
        (out / file).parent.mkdir(parents=True, exist_ok=True)
        img.convert("RGBA").save(out / file)
    (out / "character.json").write_text(json.dumps(pkg, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if src and (src / "CREDITS.md").exists():
        shutil.copy(src / "CREDITS.md", out / "CREDITS.md")
    index_path = out.parent / "index.json"
    index = json.loads(index_path.read_text(encoding="utf-8")) if index_path.exists() else []
    if out.name not in index:
        index.append(out.name)
        index_path.write_text(json.dumps(index, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("source", type=Path, help="art_src/<id> 資料夾、.ora 或 .psd")
    ap.add_argument("--id", help="角色 id（預設：資料夾名或檔名）")
    ap.add_argument("--base", help="範本角色（預設：character.json 的 base，或 = id）")
    ap.add_argument("--form", help="範本角色的型態")
    ap.add_argument("--name", help="顯示名稱")
    ap.add_argument("--out", type=Path, default=ROOT / "art" / "packages", help="輸出到哪裡（預設 art/packages）")
    ap.add_argument("--dry-run", action="store_true", help="只檢查，不寫入")
    a = ap.parse_args()

    src = a.source.resolve()
    folder = src if src.is_dir() else src.parent
    cid = a.id or (src.name if src.is_dir() else src.stem)
    draft = json.loads((folder / "character.json").read_text(encoding="utf-8")) if (folder / "character.json").exists() else None
    base = a.base or (draft or {}).get("base") or cid
    form = a.form or (draft or {}).get("form")
    if not (ROOT / "art" / "characters" / base / "rig.json").exists():
        print(f"✗ 範本角色 {base} 不存在（art/characters/{base}/rig.json）")
        return 1

    if src.is_dir():
        layers = read_folder(src)
    elif src.suffix.lower() == ".ora":
        layers = read_ora(src)
    elif src.suffix.lower() == ".psd":
        layers = read_psd(src)
    else:
        print(f"✗ 不支援的來源 {src.suffix}（資料夾、.ora、.psd）")
        return 1
    if not layers:
        print(f"✗ {src} 裡沒有找到部件圖")
        return 1

    warnings: list[str] = []
    pkg, files = build(layers, draft, base, form, a.name, warnings)
    for w in warnings:
        print(f"⚠ {w}")
    print(f"{cid}：{len(pkg['parts'])} 個部件、{len((pkg.get('faces') or {}).get('images', {}))} 個表情（範本 {base}{'@' + form if form else ''}）")
    if a.dry_run:
        return 0
    out = a.out.resolve() / cid
    write(out, pkg, files, folder)
    print(f"→ {out}")
    # 寫完馬上檢查（格式、圖片、範本）
    sys.path.insert(0, str(ROOT / "tools"))
    import validate
    report = validate.validate(ROOT)
    for e in report.errors:
        print(f"✗ {e}")
    return 1 if report.errors else 0


if __name__ == "__main__":
    sys.exit(main())
