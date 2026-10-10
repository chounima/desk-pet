"""把 content_<語言>.py 的翻譯寫進遊戲讀的檔案：

  data/i18n/<語言>.json         名稱（與介面文字放在同一個表；介面文字由程式維護，這裡只更新名稱的 key）
  data/i18n/<語言>/volley.json  排球的稱號與台詞（蓋在 data/volley.json 上）
  data/i18n/<語言>/lines/*.json 台詞（蓋在 data/lines/ 上，只放有翻的 key）

用法: python tools/i18n/build.py
"""
import importlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
DATA = HERE.parent.parent / "data"
sys.path.insert(0, str(HERE))


def write(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def check_lines(lang: str, lines: dict) -> None:
    """翻譯的台詞 key 要存在原文裡（避免打錯 key 變成永遠用不到的翻譯）"""
    for name, table in lines.items():
        original = json.loads((DATA / "lines" / f"{name}.json").read_text(encoding="utf-8"))
        extra = set(table) - set(original)
        if extra:
            raise SystemExit(f"{lang}/{name}: 原文沒有這些 key：{sorted(extra)}")


for lang in ("en", "ja"):
    mod = importlib.import_module(f"content_{lang}")
    table_path = DATA / "i18n" / f"{lang}.json"
    table = json.loads(table_path.read_text(encoding="utf-8")) if table_path.exists() else {}
    table.update(mod.NAMES)
    write(table_path, table)
    write(DATA / "i18n" / lang / "volley.json", mod.VOLLEY)
    check_lines(lang, mod.LINES)
    for name, lines in mod.LINES.items():
        write(DATA / "i18n" / lang / "lines" / f"{name}.json", lines)
    print(f"{lang}: {len(mod.NAMES)} names, {sum(len(v) for v in mod.LINES.values())} line keys")
