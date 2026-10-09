"""佔位美術產生器。

輸出：
  art/characters/<id>/parts/*.svg   身體部件
  art/characters/<id>/faces/*.svg   表情
  art/characters/<id>/rig.json      骨架、插槽、表情對應
  art/props/*.svg + props.json      道具
"""
import json
import shutil
from pathlib import Path

from . import rig
from .characters import ALL as CHARACTERS
from .props import PROPS


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def build_character(char, out: Path) -> dict:
    parts = {name: fn() for name, fn in char.PARTS.items()}
    for name, part in parts.items():
        _write(out / "parts" / f"{name}.svg", part.to_svg())
    for name, part in char.FACES.items():
        _write(out / "faces" / f"{name}.svg", part.to_svg())

    # 部件替換圖：{部件圖名: {替換名: {file, box}}}
    alts = {}
    for art, variants in getattr(char, "VARIANTS", {}).items():
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
        "box": char.FACES["idle"].box,
        "variants": {e: f"faces/{e}.svg" for e in rig.EXPRESSIONS if e in char.FACES},
    })
    prop = getattr(char, "PROPORTIONS", rig.CHIBI)
    data = {
        "id": char.ID, "name": char.NAME, "description": char.DESCRIPTION,
        "rootScale": prop.root_scale,
        "headTop": prop.head_top,
        "height": prop.height,
        "trackScale": getattr(char, "TRACK_SCALE", {}),
        "bones": [{"name": n, "parent": p, "x": x, "y": y} for n, p, x, y in rig.bones(prop)],
        "slots": sorted(slots, key=lambda s: s["z"]),
    }
    _write(out / "rig.json", json.dumps(data, ensure_ascii=False, indent=2))
    return data


def build_props(out: Path) -> None:
    meta = {}
    for name, (fn, bone, x, y, z) in PROPS.items():
        part = fn()
        _write(out / f"{name}.svg", part.to_svg())
        meta[name] = {"file": f"{name}.svg", "bone": bone, "x": x, "y": y, "z": z, "box": part.box}
    _write(out / "props.json", json.dumps(meta, ensure_ascii=False, indent=2))


def build(art_root: Path) -> list[str]:
    """清空並重建 art/characters 與 art/props，回傳角色 id。"""
    for sub in ("characters", "props"):
        shutil.rmtree(art_root / sub, ignore_errors=True)
    ids = []
    for char in CHARACTERS:
        build_character(char, art_root / "characters" / char.ID)
        ids.append(char.ID)
    build_props(art_root / "props")
    _write(art_root / "characters" / "index.json", json.dumps(ids))
    return ids
