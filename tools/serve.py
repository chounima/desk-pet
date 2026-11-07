"""本機開發伺服器：以專案根目錄為網站根，並停用快取（重新產生美術後直接重新整理即可）。

另外提供編輯器用的 API（只接受本機 127.0.0.1 連線，只能寫進 art_src/<id>/）：
  GET  /api/packages              art_src 裡的部件包清單 [{ id, name, base, form }]
  POST /api/package/<id>[?import=1]
       { "parts": { 部件: { "box": [...], "upload": "data:image/png;base64,…"（選配，換新圖） } },
         "faces": { "box": [...], "uploads": { 表情: dataURL } }, "anchors": { 名稱: { bone, x, y, scale } } }
       更新 art_src/<id>/character.json 的範圍、錨點（其他欄位不動），新圖存進 parts/、faces/；
       存檔前用 schemas/character.schema.json 檢查；?import=1 存完接著跑 tools/import_layers.py
  POST /api/animation  { "name", "target": shared|body|character|form, "body", "char", "form", "clip": {...} }
       動作編輯器存檔：寫到 data/animations/（共用）、<體型>/、characters/<角色>/、characters/<角色>/forms/<型態>/，
       並更新對應的清單（index.json、體型的 clips、characters/index.json）；clip 可以是完整動作或 { extends, tracks } 差異
  POST /api/physics    { "char": "sera" 或 "choupaca@quadruped", "physics": { 骨骼: 參數 } }
       次級物理參數存到 data/physics.json（蓋過 gen_art 產生的預設值）

用法: python tools/serve.py [port] [--no-open]
然後開啟 http://localhost:8787/preview/（編輯器：preview/editor.html）
"""
import base64
import json
import re
import subprocess
import sys
import webbrowser
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ART_SRC = ROOT / "art_src"
ID_PATTERN = re.compile(r"^[A-Za-z0-9_\-]+$")
NAME_PATTERN = re.compile(r"^[A-Za-z0-9_\-@]+$")
MIME_EXT = {"image/png": ".png", "image/webp": ".webp", "image/jpeg": ".jpg", "image/svg+xml": ".svg"}


class ApiError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status


def list_packages() -> list[dict]:
    result = []
    for folder in sorted(ART_SRC.iterdir()) if ART_SRC.exists() else []:
        f = folder / "character.json"
        if f.exists():
            pkg = json.loads(f.read_text(encoding="utf-8"))
            result.append({"id": folder.name, "name": pkg.get("name") or folder.name, "base": pkg.get("base") or folder.name, "form": pkg.get("form")})
    return result


def save_upload(folder: Path, sub: str, name: str, data_url: str) -> str:
    """dataURL 存成 art_src/<id>/<sub>/<name>.<副檔名>，回傳相對路徑"""
    m = re.match(r"^data:([\w/+.-]+);base64,(.+)$", data_url, re.S)
    if not m or m.group(1) not in MIME_EXT:
        raise ApiError(400, f"{name}：只接受 PNG、WebP、JPG、SVG 圖片")
    if not NAME_PATTERN.match(name):
        raise ApiError(400, f"不合法的名稱 {name}")
    rel = f"{sub}/{name}{MIME_EXT[m.group(1)]}"
    target = (folder / rel).resolve()
    if folder.resolve() not in target.parents:
        raise ApiError(400, "路徑不能寫出部件包資料夾")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(base64.b64decode(m.group(2)))
    return rel


def save_package(pid: str, body: dict) -> dict:
    if not ID_PATTERN.match(pid):
        raise ApiError(400, f"不合法的 id {pid}")
    folder = ART_SRC / pid
    path = folder / "character.json"
    if not path.exists():
        raise ApiError(404, f"art_src/{pid}/character.json 不存在")
    pkg = json.loads(path.read_text(encoding="utf-8"))
    parts = pkg.setdefault("parts", {})
    for name, p in (body.get("parts") or {}).items():
        part, _, alt = name.partition("@")
        if p.get("upload"):
            image = save_upload(folder, "parts", name, p["upload"])
        else:
            existing = parts.get(part, {}) if not alt else parts.get(part, {}).get("alt", {}).get(alt, {})
            image = existing.get("image")
            if not image:
                raise ApiError(400, f"{name} 沒有圖片")
        entry = {"image": image, "box": [round(v, 2) for v in p["box"]]}
        if alt:
            parts.setdefault(part, {}).setdefault("alt", {})[alt] = entry
        else:
            parts[part] = {**parts.get(part, {}), **entry}
    faces = body.get("faces")
    if faces:
        f = pkg.setdefault("faces", {"images": {}})
        if faces.get("box"):
            f["box"] = [round(v, 2) for v in faces["box"]]
        for expr, data_url in (faces.get("uploads") or {}).items():
            f.setdefault("images", {})[expr] = save_upload(folder, "faces", expr, data_url)
    if body.get("anchors"):
        pkg.setdefault("anchors", {}).update(body["anchors"])

    # 存檔前檢查格式
    from jsonschema import Draft202012Validator
    schema = json.loads((ROOT / "schemas" / "character.schema.json").read_text(encoding="utf-8"))
    errors = [f"{'/'.join(map(str, e.path))}：{e.message}" for e in Draft202012Validator(schema).iter_errors(pkg)]
    if errors:
        raise ApiError(400, "格式錯誤：" + "；".join(errors[:5]))
    path.write_text(json.dumps(pkg, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return pkg


ANIM = ROOT / "data" / "animations"


def validate_with(schema_name: str, data) -> list[str]:
    from jsonschema import Draft202012Validator
    schema = json.loads((ROOT / "schemas" / f"{schema_name}.schema.json").read_text(encoding="utf-8"))
    return [f"{'/'.join(map(str, e.path))}：{e.message}" for e in Draft202012Validator(schema).iter_errors(data)]


NL = chr(10)
NL_COMMA = "," + NL


def write_json(path: Path, data, compact_tracks: bool = False) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if compact_tracks:
        # 動作檔：最上層一個欄位一行、每條軌道一行（和手寫的動作檔一樣好讀）
        one = lambda v: json.dumps(v, ensure_ascii=False, separators=(", ", ": "))
        rows = []
        for k, v in data.items():
            if k == "tracks":
                tracks = NL_COMMA.join(f"    {one(ch)}: {one(keys)}" for ch, keys in v.items())
                rows.append('  "tracks": {' + NL + tracks + NL + "  }")
            else:
                rows.append(f"  {one(k)}: {one(v)}")
        text = "{" + NL + NL_COMMA.join(rows) + NL + "}"
    else:
        text = json.dumps(data, ensure_ascii=False, indent=2)
    path.write_text(text + NL, encoding="utf-8")



def save_animation(body: dict) -> str:
    name, target, clip = body.get("name", ""), body.get("target"), body.get("clip")
    if not ID_PATTERN.match(name) or not isinstance(clip, dict):
        raise ApiError(400, "動作名稱只能用英數字")
    errors = validate_with("animation", clip)
    if errors:
        raise ApiError(400, "格式錯誤：" + "；".join(errors[:5]))
    if target == "shared":
        folder = ANIM
        index_path = ANIM / "index.json"
        index = json.loads(index_path.read_text(encoding="utf-8"))
        if name not in index:
            index.append(name)
            index_path.write_text(json.dumps(index, ensure_ascii=False) + NL, encoding="utf-8")
    elif target == "body":
        b = body.get("body", "")
        if not ID_PATTERN.match(b) or b == "humanoid":
            raise ApiError(400, "人形的動作就是共用動作；體型要是 quadruped 這類")
        folder = ANIM / b
        index_path = folder / "index.json"
        index = json.loads(index_path.read_text(encoding="utf-8")) if index_path.exists() else {"clips": [], "alias": {}}
        if name not in index["clips"]:
            index["clips"].append(name)
        index.get("alias", {}).pop(name, None)            # 有自己的版本就不再借用
        write_json(index_path, index)
    elif target in ("character", "form"):
        cid, form = body.get("char", ""), body.get("form") or ""
        if not ID_PATTERN.match(cid) or (target == "form" and not ID_PATTERN.match(form)):
            raise ApiError(400, "角色或型態名稱不合法")
        folder = ANIM / "characters" / cid / ("forms/" + form if target == "form" else "")
        index_path = ANIM / "characters" / "index.json"
        index = json.loads(index_path.read_text(encoding="utf-8")) if index_path.exists() else {}
        entry = index.setdefault(cid, {"clips": []})
        names = entry.setdefault("forms", {}).setdefault(form, []) if target == "form" else entry.setdefault("clips", [])
        if name not in names:
            names.append(name)
        write_json(index_path, index)
    else:
        raise ApiError(400, f"不認識的存檔位置 {target}")
    path = (folder / f"{name}.json").resolve()
    if ANIM.resolve() not in path.parents:
        raise ApiError(400, "路徑不能寫出 data/animations")
    write_json(path, clip, compact_tracks=True)
    return path.relative_to(ROOT).as_posix()


def save_physics(body: dict) -> str:
    key, physics = body.get("char", ""), body.get("physics")
    if not re.match(r"^[A-Za-z0-9_\-]+(@[A-Za-z0-9_\-]+)?$", key) or not isinstance(physics, dict):
        raise ApiError(400, "角色名稱不合法")
    errors = validate_with("rig", {"id": "x", "bones": [{"name": "root"}], "slots": [], "physics": physics})
    if errors:
        raise ApiError(400, "格式錯誤：" + "；".join(errors[:5]))
    path = ROOT / "data" / "physics.json"
    data = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"_說明": "次級物理參數覆寫：{ 角色 或 角色@型態: { 骨骼: 參數 } }，蓋過 gen_art 產生的預設值（動作編輯器存的）"}
    data[key] = physics
    write_json(path, data)
    return "data/physics.json"


def run_import(pid: str) -> tuple[int, str]:
    p = subprocess.run([sys.executable, str(ROOT / "tools" / "import_layers.py"), str(ART_SRC / pid)],
                       capture_output=True, text=True, encoding="utf-8", env={"PYTHONIOENCODING": "utf-8", **__import__("os").environ})
    return p.returncode, (p.stdout + p.stderr).strip()


class NoCacheHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, *args):
        pass

    def send_json(self, status: int, data) -> None:
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def local_only(self) -> bool:
        if self.client_address[0] != "127.0.0.1":
            self.send_json(403, {"error": "只接受本機連線"})
            return False
        return True

    def do_GET(self):
        if self.path.split("?")[0] == "/api/packages":
            if self.local_only():
                self.send_json(200, list_packages())
            return
        super().do_GET()

    def do_POST(self):
        if not self.local_only():
            return
        if self.path in ("/api/animation", "/api/physics"):
            try:
                body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
                saved = save_animation(body) if self.path == "/api/animation" else save_physics(body)
                self.send_json(200, {"ok": True, "path": saved})
            except ApiError as e:
                self.send_json(e.status, {"error": str(e)})
            except (ValueError, KeyError, TypeError) as e:
                self.send_json(400, {"error": f"資料格式不對：{e}"})
            return
        m = re.match(r"^/api/package/([^/?]+)(\?.*)?$", self.path)
        if not m:
            self.send_json(404, {"error": "沒有這個 API"})
            return
        try:
            body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
            pkg = save_package(m.group(1), body)
            result = {"ok": True, "parts": len(pkg.get("parts", {}))}
            if "import=1" in (m.group(2) or ""):
                code, output = run_import(m.group(1))
                result.update(imported=code == 0, output=output)
            self.send_json(200, result)
        except ApiError as e:
            self.send_json(e.status, {"error": str(e)})
        except (ValueError, KeyError, TypeError) as e:
            self.send_json(400, {"error": f"資料格式不對：{e}"})


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 8787
    url = f"http://localhost:{port}/preview/"
    server = ThreadingHTTPServer(("127.0.0.1", port), partial(NoCacheHandler, directory=str(ROOT)))
    print(f"serving {ROOT} at {url}")
    if "--no-open" not in sys.argv:
        webbrowser.open(url)
    server.serve_forever()
