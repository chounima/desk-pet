"""把 frame_00.png、frame_01.png… 排成一張比較圖（灰色棋盤格背景，底下標第幾張）。

用法: python tools/contact_sheet.py <資料夾> [輸出.png] [--cols 6] [--height 360]
需要: Pillow（tools/requirements.txt）
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageDraw


def checker(w: int, h: int, size: int = 16) -> Image.Image:
    img = Image.new("RGBA", (w, h), (205, 208, 216, 255))
    d = ImageDraw.Draw(img)
    for y in range(0, h, size):
        for x in range((y // size) % 2 * size, w, size * 2):
            d.rectangle([x, y, x + size - 1, y + size - 1], fill=(225, 228, 234, 255))
    return img


def sheet(frames: list[Path], cols: int, height: int) -> Image.Image:
    imgs = [Image.open(f).convert("RGBA") for f in frames]
    scale = height / max(i.height for i in imgs)
    imgs = [i.resize((max(1, round(i.width * scale)), max(1, round(i.height * scale))), Image.LANCZOS) for i in imgs]
    cw, ch = max(i.width for i in imgs), height + 22
    rows = (len(imgs) + cols - 1) // cols
    out = checker(cw * min(cols, len(imgs)), ch * rows)
    d = ImageDraw.Draw(out)
    for k, img in enumerate(imgs):
        x, y = (k % cols) * cw, (k // cols) * ch
        out.alpha_composite(img, (x + (cw - img.width) // 2, y + height - img.height))
        d.text((x + 6, y + height + 4), f"#{k}", fill=(40, 44, 60, 255))
        d.line([(x, y), (x, y + ch)], fill=(160, 164, 176, 255))
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("folder", type=Path)
    ap.add_argument("out", type=Path, nargs="?")
    ap.add_argument("--cols", type=int, default=6)
    ap.add_argument("--height", type=int, default=360)
    a = ap.parse_args()
    frames = sorted(a.folder.glob("frame_*.png"))
    if not frames:
        print(f"{a.folder} 裡沒有 frame_*.png")
        return 1
    out = a.out or a.folder / "sheet.png"
    sheet(frames, a.cols, a.height).save(out)
    print(f"{len(frames)} 張 → {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
