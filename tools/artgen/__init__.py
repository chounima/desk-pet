"""佔位美術產生器。

輸出：
  art/characters/<id>/parts/*.svg   身體部件
  art/characters/<id>/faces/*.svg   表情
  art/characters/<id>/rig.json      骨架、插槽、表情對應
  art/characters/<id>/forms/<型態>/ 其他型態（例如四足），結構同上
  art/props/*.svg + props.json      道具
"""
import json
import math
import shutil
from pathlib import Path

from . import rig
from .accessories import ACCESSORIES, SLOTS as ACCESSORY_SLOTS
from .characters import ALL as CHARACTERS
from .props import PROPS


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


# 次級物理的預設參數（見 godot/src/Core/Spring.cs）：有尾巴、披布的角色自動開；角色可以用 PHYSICS 覆寫或加別的骨頭
PHYSICS_DEFAULTS = {
    "tail": {"stiffness": 90, "damping": 6, "gravity": 0, "inertia": 1.3, "drag": 18, "limit": 40},
    "cape": {"stiffness": 140, "damping": 9, "gravity": 0, "inertia": 1, "drag": 15, "limit": 25},
}
# 頭髮骨鏈每一節的預設物理（往下延伸；後面的節跟著前面的節轉，甩起來是連續的）
HAIR_PHYSICS = {"stiffness": 220, "damping": 9, "gravity": 0, "inertia": 0.8, "drag": 7, "limit": 16, "axis": 90}


def hair_chains(src, out: Path) -> tuple[list, list, dict]:
    """頭髮骨鏈（選配）：HAIR_CHAINS = { "hairB": { "anchor": (x, y), "z": 1.05, "segments": [(長度, 部件函式), …], "physics": {…} } }
    第 1 節掛在頭上的 anchor，下一節掛在上一節的尾端（往下 長度）。回傳 (骨骼, 插槽, 物理)。
    每一節的部件圖以自己的骨骼為原點、往下畫，上端要和上一節重疊一點。"""
    bones, slots, physics = [], [], {}
    for chain, spec in getattr(src, "HAIR_CHAINS", {}).items():
        parent, (x, y) = "head", spec["anchor"]
        for i, (length, fn) in enumerate(spec["segments"], 1):
            bone = f"{chain}_{i}"
            part = fn()
            _write(out / "parts" / f"{bone}.svg", part.to_svg())
            bones.append((bone, parent, x, y))
            slots.append({"name": bone, "bone": bone, "z": round(spec.get("z", 1.05) + i * 0.001, 3),
                          "file": f"parts/{bone}.svg", "box": part.box, "far": False, "depth": spec.get("depth", rig.DEPTH["hair_back"])})
            physics[bone] = {**HAIR_PHYSICS, "length": length, **spec.get("physics", {})}
            parent, (x, y) = bone, (0, length)
        names = ["head"] + [f"{chain}_{i}" for i in range(1, len(spec["segments"]) + 1)]
        for s in slots:
            if s["bone"] in names:
                s["mesh"] = {"cols": rig.HAIR_MESH[0], "rows": rig.HAIR_MESH[1], "bones": names}
    return bones, slots, physics


def default_physics(slots: list[dict], overrides: dict) -> dict:
    """{ 骨骼: 參數 }；length = 掛在這根骨頭上的部件離骨頭起點最遠的距離（慣性造成的轉動依長度換算）"""
    result = {}
    for bone, params in PHYSICS_DEFAULTS.items():
        boxes = [s["box"] for s in slots if s["bone"] == bone]
        if not boxes:
            continue
        reach = max(max(abs(x), abs(y)) for b in boxes for x, y in ((b[0], b[1]), (b[2], b[3])))
        # axis：部件往哪個方向延伸（部件範圍中心的方向，0 = 往右、90 = 往下）
        cx = sum((b[0] + b[2]) / 2 for b in boxes) / len(boxes)
        cy = sum((b[1] + b[3]) / 2 for b in boxes) / len(boxes)
        result[bone] = {**params, "length": round(reach, 1), "axis": round(math.degrees(math.atan2(cy, cx)))}
    for bone, params in overrides.items():
        result[bone] = {**result.get(bone, {}), **params}
    return result


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
        if slot in rig.DEPTH:
            entry["depth"] = rig.DEPTH[slot]
        if art in alts:
            entry["alt"] = alts[art]
        slots.append(entry)
    chain_bones, chain_slots, chain_physics = hair_chains(src, out)
    slots += chain_slots
    face_name, face_bone, face_z = rig.FACE_SLOT
    slots.append({
        "name": face_name, "bone": face_bone, "z": face_z, "depth": rig.DEPTH["face"],
        "box": src.FACES["idle"].box,
        "variants": {e: f"faces/{e}.svg" for e in rig.EXPRESSIONS if e in src.FACES},
    })
    prop = getattr(src, "PROPORTIONS", rig.CHIBI)
    bone_names = {n for n, *_ in rig.bones(prop)}
    for s in slots:
        if (m := rig.MESH.get(s["name"])) and all(b in bone_names for b in m[2]):
            s["mesh"] = {"cols": m[0], "rows": m[1], "bones": m[2]}
    data = {
        "version": 1,       # 角色格式版本（schemas/rig.schema.json）
        "id": char.ID, "name": char.NAME, "description": char.DESCRIPTION,
        "body": getattr(prop, "body_type", "humanoid"),
        "rootScale": prop.root_scale,
        "headTop": prop.head_top,
        "height": prop.height,
        "trackScale": getattr(src, "TRACK_SCALE", {}),
        # 個別道具的位置／大小修正（Q 版單位）：{ 道具: { dx, dy, scale } }，例如高個子抱吉他要低一點
        "propOverrides": getattr(src, "PROP_OVERRIDES", {}),
        # 配件錨點：預設依頭部大小計算，角色可用 ANCHORS 覆寫（例如四足、頭型特殊）
        "anchors": {name: {"bone": b, "x": round(x, 1), "y": round(y, 1), "scale": round(sc, 3),
                           **({"depth": rig.ANCHOR_DEPTH[name]} if name in rig.ANCHOR_DEPTH else {})}
                    for name, (b, x, y, sc) in {**rig.default_anchors(prop, getattr(src, "HEAD_TRANSFORM", (1, 0))),
                                                **getattr(src, "ANCHORS", {})}.items()},
        "bones": [{"name": n, "parent": p, "x": x, "y": y} for n, p, x, y in rig.bones(prop) + chain_bones],
        "slots": sorted(slots, key=lambda s: s["z"]),
    }
    # 分層臉（選配；make_faces 會附上）：部件放 face_parts/，規格寫進 face（整張臉 faces/ 照樣保留給不支援的地方）
    if layered := getattr(src.FACES, "layers", None):
        for file, part in layered["parts"].items():
            _write(out / file, part.to_svg())
        data["face"] = layered["spec"]
    physics = {**default_physics(slots, getattr(src, "PHYSICS", {})), **chain_physics}
    if physics:
        data["physics"] = physics
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
