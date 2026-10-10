"""佔位美術產生器。

輸出：
  art/characters/<id>/parts/*.svg   身體部件
  art/characters/<id>/faces/*.svg   表情
  art/characters/<id>/rig.json      骨架、插槽、表情對應
  art/characters/<id>/forms/<型態>/ 其他型態（例如四足），結構同上
  art/props/*.svg + props.json      道具
"""
import json
import shutil
from pathlib import Path

from . import rig
from .accessories import ACCESSORIES, SLOTS as ACCESSORY_SLOTS
from .characters import ALL as CHARACTERS
from .props import PROPS


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def build_character(char, out: Path, form=None) -> dict:
    """form：角色的其他型態（模組，提供 PARTS / FACES / PROPORTIONS，可選 VARIANTS / VOLLEY）。
    名稱、台詞、排球能力都沿用角色本身。"""
    src = form or char
    parts = {name: fn() for name, fn in src.PARTS.items()}
    for name, part in parts.items():
        _write(out / "parts" / f"{name}.svg", part.to_svg())
    for name, part in src.FACES.items():
        _write(out / "faces" / f"{name}.svg", part.to_svg())

    # 部件替換圖：{部件圖名: {替換名: {file, box}}}
    alts = {}
    for art, variants in getattr(src, "VARIANTS", {}).items():
        for vname, fn in variants.items():
            part = fn()
            file = f"parts/{art}_{vname}.svg"
            _write(out / file, part.to_svg())
            alts.setdefault(art, {})[vname] = {"file": file, "box": part.box}

    slots = []
    for slot, bone, z, art, far in rig.SLOTS:
        if art not in parts:
            continue
        entry = {"name": slot, "bone": bone, "z": z, "file": f"parts/{art}.svg",
                 "box": parts[art].box, "far": far}
        if art in alts:
            entry["alt"] = alts[art]
        slots.append(entry)
    face_name, face_bone, face_z = rig.FACE_SLOT
    slots.append({
        "name": face_name, "bone": face_bone, "z": face_z,
        "box": src.FACES["idle"].box,
        "variants": {e: f"faces/{e}.svg" for e in rig.EXPRESSIONS if e in src.FACES},
    })
    prop = getattr(src, "PROPORTIONS", rig.CHIBI)
    data = {
        "id": char.ID, "name": char.NAME, "description": char.DESCRIPTION,
        "body": getattr(prop, "body_type", "humanoid"),
        "rootScale": prop.root_scale,
        "headTop": prop.head_top,
        "height": prop.height,
        "trackScale": getattr(src, "TRACK_SCALE", {}),
        # 個別道具的位置／大小修正（Q 版單位）：{ 道具: { dx, dy, scale } }，例如高個子抱吉他要低一點
        "propOverrides": getattr(src, "PROP_OVERRIDES", {}),
        # 配件錨點：預設依頭部大小計算，角色可用 ANCHORS 覆寫（例如四足、頭型特殊）
        "anchors": {name: {"bone": b, "x": round(x, 1), "y": round(y, 1), "scale": round(sc, 3)}
                    for name, (b, x, y, sc) in {**rig.default_anchors(prop, getattr(src, "HEAD_TRANSFORM", (1, 0))),
                                                **getattr(src, "ANCHORS", {})}.items()},
        "bones": [{"name": n, "parent": p, "x": x, "y": y} for n, p, x, y in rig.bones(prop)],
        "slots": sorted(slots, key=lambda s: s["z"]),
    }
    if hasattr(src, "VOLLEY"):
        data["volley"] = src.VOLLEY
    forms = getattr(char, "FORMS", {}) if form is None else {}
    if forms:
        # 型態清單：預設型態用 rig.json，其他放在 forms/<名稱>/
        data["forms"] = {getattr(char, "FORM", "upright"): {"label": getattr(char, "FORM_LABEL", "直立"), "rig": ""}}
        for fname, fmod in forms.items():
            build_character(char, out / "forms" / fname, fmod)
            data["forms"][fname] = {"label": fmod.LABEL, "rig": f"forms/{fname}/"}
    _write(out / "rig.json", json.dumps(data, ensure_ascii=False, indent=2))
    return data


def build_props(out: Path) -> None:
    meta = {}
    for name, (fn, bone, x, y, z) in PROPS.items():
        part = fn()
        _write(out / f"{name}.svg", part.to_svg())
        meta[name] = {"file": f"{name}.svg", "bone": bone, "x": x, "y": y, "z": z, "box": part.box}
    _write(out / "props.json", json.dumps(meta, ensure_ascii=False, indent=2))


def build_accessories(out: Path) -> None:
    meta = {}
    for name, (fn, slot) in ACCESSORIES.items():
        part = fn()
        _write(out / f"{name}.svg", part.to_svg())
        anchor, z = ACCESSORY_SLOTS[slot]
        meta[name] = {"file": f"{name}.svg", "box": part.box, "slot": slot, "anchor": anchor, "z": z}
    _write(out / "accessories.json", json.dumps(meta, ensure_ascii=False, indent=2))
    # 部位 → 錨點與圖層（自訂配件也照這個掛）
    _write(out / "slots.json", json.dumps({s: {"anchor": a, "z": z} for s, (a, z) in ACCESSORY_SLOTS.items()}, indent=2))


def build(art_root: Path) -> list[str]:
    """清空並重建 art/characters 與 art/props，回傳角色 id。"""
    for sub in ("characters", "props", "accessories"):
        shutil.rmtree(art_root / sub, ignore_errors=True)
    ids = []
    for char in CHARACTERS:
        build_character(char, art_root / "characters" / char.ID)
        ids.append(char.ID)
    build_props(art_root / "props")
    build_accessories(art_root / "accessories")
    _write(art_root / "characters" / "index.json", json.dumps(ids))
    return ids
