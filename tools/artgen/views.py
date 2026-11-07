"""其他視角（正面、背面）的**測試圖**：用側面（3/4 朝右）的部件重新排骨架拼出來，讓「調角度」的流程先能動。

- 正面（yaw 0）：手腳左右對稱，臉往中間移（側面的臉偏右）
- 背面（yaw 180）：手腳左右對稱（近側、遠側對調），沒有臉、沒有瀏海；後髮、背包、披布、尾巴畫在身體前面
- 側面（yaw 90）就是原本的 rig.json；左側（270）由程式把側面鏡像

精細的圖之後用部件包替換：art_src/<id>/views/<視角>/（見 docs/ART_GUIDE.md「其他視角」）。
"""
import copy
import json
import shutil
from pathlib import Path

from .svg import Part

# 側面的臉中心大約在 x = 9.5（Q 版頭部座標），正面要移到 0
FACE_CENTER_X = 9.5
# 背面：這些插槽改畫在身體前面（z 越大越上面）
BACK_FRONT_Z = {"hair_back": 15.5, "back": 15.6, "cape": 15.7, "tail": 15.8}
BACK_HIDE = {"face", "hair_front"}


def _shift(part: Part, dx: float) -> Part:
    x0, y0, x1, y1 = part.box
    return Part(f'<g transform="translate({dx:g},0)">{part.body}</g>', (x0 + dx, y0, x1 + dx, y1))


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _symmetric_bones(bones: list[dict], slots: list[dict], mirror: bool) -> list[dict]:
    """手腳左右分開：肩膀約在身體寬度的 40%、髖關節約 18%（依 torso 部件的寬度）"""
    torso = next((s for s in slots if s["name"] == "torso"), None)
    half = (torso["box"][2] - torso["box"][0]) / 2 if torso else 20
    out = copy.deepcopy(bones)
    for b in out:
        n = b["name"]
        side = 1 if "Near" in n else -1 if "Far" in n else 0
        if mirror:
            side = -side
        if n.endswith("_u") and n.startswith("arm"):
            b["x"] = round(side * half * 0.8, 1)
        elif n.endswith("_u") and n.startswith("leg"):
            b["x"] = round(side * half * 0.38, 1)
    return out


def build_views(src, data: dict, out: Path) -> dict:
    """產生 views/front、views/back，回傳 rig.json 的 views 欄位"""
    scale = getattr(src, "HEAD_TRANSFORM", (1, 0))[0]
    dx = -FACE_CENTER_X * scale
    views = {"side": {"label": "側面", "yaw": 90, "rig": ""}}
    for name, label, yaw in (("front", "正面", 0), ("back", "背面", 180)):
        vout = out / "views" / name
        shutil.copytree(out / "parts", vout / "parts", dirs_exist_ok=True)
        v = copy.deepcopy(data)
        v.pop("forms", None)
        v["view"] = name
        v["bones"] = _symmetric_bones(data["bones"], data["slots"], mirror=(name == "back"))
        slots = []
        for s in v["slots"]:
            s["far"] = False                 # 正面、背面兩邊一樣亮
            if name == "back":
                if s["name"] in BACK_HIDE:
                    continue
                if s["name"] in BACK_FRONT_Z:
                    s["z"] = BACK_FRONT_Z[s["name"]]
                elif s["name"].startswith("hairB"):
                    s["z"] = round(15.5 + float(s["name"].split("_")[1]) * 0.01, 3)
                s.pop("depth", None)
            slots.append(s)
        v["slots"] = sorted(slots, key=lambda s: s["z"])
        if name == "front":
            # 臉移到中間（整張臉、分層臉都要）
            face_slot = next(s for s in v["slots"] if s.get("variants"))
            for expr, file in face_slot["variants"].items():
                part = src.FACES[expr]
                _write(vout / file, _shift(part, dx).to_svg())
            face_slot["box"] = list(_shift(src.FACES["idle"], dx).box)
            if layered := getattr(src.FACES, "layers", None):
                for file, part in layered["parts"].items():
                    _write(vout / file, _shift(part, dx).to_svg())
                x0, y0, x1, y1 = v["face"]["box"]
                v["face"]["box"] = [x0 + dx, y0, x1 + dx, y1]
            for a in ("eyes", "mouth"):
                if a in v["anchors"]:
                    v["anchors"][a]["x"] = round(v["anchors"][a]["x"] + dx, 1)
        else:
            v.pop("face", None)
            for a in ("eyes", "mouth"):
                v["anchors"].pop(a, None)
        _write(vout / "rig.json", json.dumps(v, ensure_ascii=False, indent=2))
        views[name] = {"label": label, "yaw": yaw, "rig": f"views/{name}/"}
    return views
