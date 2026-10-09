"""產生佔位美術（角色部件、表情、骨架、道具）到 art/。

用法: python tools/gen_art.py
"""
from pathlib import Path

from artgen import build

ART = Path(__file__).resolve().parent.parent / "art"

if __name__ == "__main__":
    ids = build(ART)
    print(f"built characters: {', '.join(ids)} -> {ART}")
