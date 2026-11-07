"""打包前把 art/ 與 data/ 複製到 godot/content/，讓匯出的執行檔帶著角色與動作資料。

SVG 與 PNG／JPG／WebP 會加上 importer="keep" 的 .import 檔：Godot 原樣打包、不轉成貼圖（程式執行時才依顯示大小轉換）。
godot/content/ 是產生出來的，不進 git。

用法: python tools/sync_content.py
"""
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEST = ROOT / "godot" / "content"
KEEP_IMPORT = '[remap]\n\nimporter="keep"\n'


def main() -> None:
    shutil.rmtree(DEST, ignore_errors=True)
    for sub in ("art", "data"):
        shutil.copytree(ROOT / sub, DEST / sub)
    # SVG 與部件包的點陣圖都要原樣打包（執行時才依顯示大小轉貼圖；Godot 預設會把 PNG 轉成 .ctex、原檔不會帶上）
    keep = [p for ext in ("*.svg", "*.png", "*.jpg", "*.jpeg", "*.webp") for p in DEST.rglob(ext)]
    for f in keep:
        f.with_name(f.name + ".import").write_text(KEEP_IMPORT, encoding="utf-8")
    files = sum(1 for p in DEST.rglob("*") if p.is_file())
    print(f"synced {files} files ({len(keep)} 原樣打包的圖) -> {DEST}")


if __name__ == "__main__":
    main()
