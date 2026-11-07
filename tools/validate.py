"""檢查 art/、data/ 的所有資料：格式（schemas/）與互相引用是否找得到。

畫師、AI 部件包、動作編輯器、手改 JSON 之後都跑一次；有錯誤時結束碼 1。

用法: python tools/validate.py [--root 資料夾]   （預設：專案根目錄）
需要: python -m pip install -r tools/requirements.txt
"""
import argparse
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent

# 不是動作檔、是行為的動作名稱（Behavior 會轉成跳到視窗上／跳下來）
BEHAVIORS = {"climb", "hopDown"}


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, where: str, msg: str) -> None:
        self.errors.append(f"{where}: {msg}")

    def warn(self, where: str, msg: str) -> None:
        self.warnings.append(f"{where}: {msg}")


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def rel(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def check_schema(report: Report, root: Path, schema: Draft202012Validator, path: Path):
    try:
        data = load(path)
    except (OSError, json.JSONDecodeError) as e:
        report.error(rel(path, root), f"讀不到或不是 JSON：{e}")
        return None
    for e in sorted(schema.iter_errors(data), key=lambda e: list(e.path)):
        where = "/".join(map(str, e.path)) or "(根)"
        report.error(rel(path, root), f"{where}：{e.message}")
    return data


def schemas(root: Path) -> dict[str, Draft202012Validator]:
    return {p.name.removesuffix(".schema.json"): Draft202012Validator(load(p)) for p in (root / "schemas").glob("*.schema.json")}


# ---------- 動作 ----------

def check_animations(report: Report, root: Path, sch) -> dict[str, set[str]]:
    """回傳 { 體型: 這個體型能用的動作名稱 }"""
    anim_dir = root / "data" / "animations"
    shared = check_schema(report, root, sch["animation-index"], anim_dir / "index.json") or []
    for name in shared:
        f = anim_dir / f"{name}.json"
        if not f.exists():
            report.error(rel(anim_dir / "index.json", root), f"清單裡的 {name} 沒有 {name}.json")
            continue
        check_schema(report, root, sch["animation"], f)
    for f in anim_dir.glob("*.json"):
        if f.name != "index.json" and f.stem not in shared:
            report.warn(rel(f, root), "沒有列在 index.json，不會被載入")

    bodies = {"humanoid": set(shared)}
    for sub in (p for p in anim_dir.iterdir() if p.is_dir()):
        index = check_schema(report, root, sch["body-index"], sub / "index.json") if (sub / "index.json").exists() else None
        if index is None:
            report.error(rel(sub, root), "體型資料夾沒有 index.json")
            continue
        names = set()
        for name in index.get("clips", []):
            f = sub / f"{name}.json"
            if f.exists():
                check_schema(report, root, sch["animation"], f)
                names.add(name)
            else:
                report.error(rel(sub / "index.json", root), f"clips 裡的 {name} 沒有 {name}.json")
        for name, target in index.get("alias", {}).items():
            if target not in names:
                report.error(rel(sub / "index.json", root), f"alias {name} → {target}：{target} 不是這個體型的動作")
            names.add(name)
        bodies[sub.name] = names
    return bodies


def check_tracks_and_props(report: Report, root: Path, bones: set[str]):
    props = load(root / "art" / "props" / "props.json") if (root / "art" / "props" / "props.json").exists() else {}
    for f in (root / "data" / "animations").rglob("*.json"):
        if f.name == "index.json":
            continue
        clip = load(f)
        for track in clip.get("tracks", {}):
            bone = track.split(".")[0]
            if bone not in bones:
                report.warn(rel(f, root), f"軌道 {track} 的骨骼 {bone} 沒有任何角色有")
        for prop in clip.get("props", []):
            if prop not in props:
                report.error(rel(f, root), f"道具 {prop} 不在 art/props/props.json")


# ---------- 角色 ----------

def check_rig(report: Report, root: Path, sch, path: Path, bodies) -> dict | None:
    rig = check_schema(report, root, sch["rig"], path)
    if rig is None:
        return None
    where = rel(path, root)
    base = path.parent
    names = [b["name"] for b in rig.get("bones", [])]
    known = set(names)
    if len(known) != len(names):
        report.error(where, "骨骼名稱重複")
    for b in rig.get("bones", []):
        if b.get("parent") and b["parent"] not in known:
            report.error(where, f"骨骼 {b['name']} 的 parent {b['parent']} 不存在")
    for s in rig.get("slots", []):
        if s["bone"] not in known:
            report.error(where, f"插槽 {s['name']} 掛在不存在的骨骼 {s['bone']}")
        files = [s.get("file")] + list((s.get("variants") or {}).values()) + [a["file"] for a in (s.get("alt") or {}).values()]
        for file in filter(None, files):
            if not (base / file).exists():
                report.error(where, f"插槽 {s['name']} 的圖 {file} 不存在")
    for name, a in rig.get("anchors", {}).items():
        if a["bone"] not in known:
            report.error(where, f"錨點 {name} 掛在不存在的骨骼 {a['bone']}")
    for name, form in (rig.get("forms") or {}).items():
        if form["rig"] and not (base / form["rig"] / "rig.json").exists():
            report.error(where, f"型態 {name} 的資料夾 {form['rig']} 沒有 rig.json")
    if rig.get("body", "humanoid") not in bodies:
        report.error(where, f"體型 {rig.get('body')} 沒有對應的動作資料夾")
    return rig


def check_characters(report: Report, root: Path, sch, bodies) -> tuple[list[tuple[str, str]], set[str]]:
    """回傳 ([(角色或型態名稱, 體型)…], 所有骨骼名稱)"""
    char_dir = root / "art" / "characters"
    ids = load(char_dir / "index.json")
    result, bones = [], set()
    for cid in ids:
        path = char_dir / cid / "rig.json"
        if not path.exists():
            report.error("art/characters/index.json", f"{cid} 沒有 rig.json")
            continue
        rig = check_rig(report, root, sch, path, bodies)
        if rig is None:
            continue
        if rig.get("id") != cid:
            report.error(rel(path, root), f"id 是 {rig.get('id')}，但資料夾是 {cid}")
        result.append((cid, rig.get("body", "humanoid")))
        bones |= {b["name"] for b in rig["bones"]}
        for name, form in (rig.get("forms") or {}).items():
            if form["rig"] and (path.parent / form["rig"] / "rig.json").exists():
                frig = check_rig(report, root, sch, path.parent / form["rig"] / "rig.json", bodies)
                if frig:
                    result.append((f"{cid}/{name}", frig.get("body", "humanoid")))
                    bones |= {b["name"] for b in frig["bones"]}
    return result, bones


# ---------- 引用 ----------

def action_refs(root: Path) -> list[tuple[str, str]]:
    """[(哪個檔案的哪裡, 動作名稱)…]：資料檔裡用名字引用的動作"""
    d = root / "data"
    refs: list[tuple[str, str]] = []

    def add(where, name):
        if name:
            refs.append((where, name))

    if (d / "guests.json").exists():
        g = load(d / "guests.json")
        for t in g.get("chatTriggers", []):
            add(f"guests.json chatTriggers {t['match'][0]}", t["action"])
        for emoji, a in g.get("reactions", {}).items():
            add(f"guests.json reactions {emoji}", a)
    if (d / "interactions.json").exists():
        i = load(d / "interactions.json")
        for zone, rules in i.get("click", {}).items():
            for r in rules:
                add(f"interactions.json click.{zone}", r.get("action"))
                for a in r.get("random", []):
                    add(f"interactions.json click.{zone}", a)
        for k, r in {**i.get("busy", {}), **i.get("keys", {})}.items():
            add(f"interactions.json {k}", r.get("action"))
    if (d / "menu.json").exists():
        for a in load(d / "menu.json").get("actions", []):
            add("menu.json actions", a)
    if (d / "focus.json").exists():
        f = load(d / "focus.json")
        for k in ("calmActions", "breakActions"):
            for a in f.get(k, []):
                add(f"focus.json {k}", a)
    if (d / "apps.json").exists():
        for cat, c in load(d / "apps.json").get("categories", {}).items():
            for a in c.get("boost", {}):
                add(f"apps.json {cat}.boost", a)
    if (d / "tuning.json").exists():
        for a in load(d / "tuning.json").get("auto", {}):
            add("tuning.json auto", a)
    return refs


def check_refs(report: Report, root: Path, chars, bodies):
    humanoid = bodies.get("humanoid", set())
    for where, name in action_refs(root):
        if name in BEHAVIORS:
            continue
        if name not in humanoid:
            report.error(f"data/{where}", f"動作 {name} 不存在")
            continue
        # 其他體型沒有這個動作時會改用備用動作（例如跳一下），只提醒
        missing = sorted(c for c, body in chars if body != "humanoid" and name not in bodies.get(body, set()))
        if missing:
            report.warn(f"data/{where}", f"動作 {name} 在 {', '.join(missing)} 沒有（不同體型），會用備用動作")


def check_i18n(report: Report, root: Path):
    d = root / "data" / "i18n"
    base = {k for k in load(d / "zh-TW.json") if not k.startswith("_")}
    for f in d.glob("*.json"):
        if f.name == "zh-TW.json":
            continue
        keys = {k for k in load(f) if not k.startswith("_")}
        missing = sorted(base - keys)
        if missing:
            report.warn(rel(f, root), f"少了 {len(missing)} 個翻譯（會用繁中）：{', '.join(missing[:8])}{'…' if len(missing) > 8 else ''}")


def validate(root: Path) -> Report:
    report = Report()
    sch = schemas(root)
    bodies = check_animations(report, root, sch)
    chars, bones = check_characters(report, root, sch, bodies)
    check_tracks_and_props(report, root, bones | {"root"})
    for name in ("tuning", "guests"):
        if (root / "data" / f"{name}.json").exists():
            check_schema(report, root, sch[name], root / "data" / f"{name}.json")
    check_refs(report, root, chars, bodies)
    check_i18n(report, root)
    return report


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", type=Path, default=ROOT)
    ap.add_argument("--quiet", action="store_true", help="不印提醒，只印錯誤")
    args = ap.parse_args()
    report = validate(args.root.resolve())
    if not args.quiet:
        for w in report.warnings:
            print(f"⚠ {w}")
    for e in report.errors:
        print(f"✗ {e}")
    print(f"{'✓ 沒有錯誤' if not report.errors else f'✗ {len(report.errors)} 個錯誤'}，{len(report.warnings)} 個提醒")
    return 1 if report.errors else 0


if __name__ == "__main__":
    sys.exit(main())
