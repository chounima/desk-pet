"""把多張 PNG 包成一個 Windows 圖示（.ico，每個尺寸一張 PNG，Windows Vista 以後都支援）。

Windows 會依顯示位置挑尺寸（工作列 16/32、檔案總管 48～256），所以最好提供 16、32、48、64、128、256。

用法: python tools/make_icon.py <output.ico> <16.png> <32.png> ... <256.png>
"""
import struct
import sys
from pathlib import Path


def png_size(data: bytes) -> tuple[int, int]:
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("不是 PNG 檔")
    return struct.unpack(">II", data[16:24])


def make_ico(pngs: list[bytes]) -> bytes:
    pngs = sorted(pngs, key=lambda p: png_size(p)[0])
    header = struct.pack("<HHH", 0, 1, len(pngs))          # 保留、類型=1 圖示、張數
    offset = 6 + 16 * len(pngs)
    entries, blobs = b"", b""
    for png in pngs:
        w, h = png_size(png)
        # 寬高 256 以 0 表示
        entries += struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0, 1, 32, len(png), offset)
        blobs += png
        offset += len(png)
    return header + entries + blobs


if __name__ == "__main__":
    dst, sources = Path(sys.argv[1]), [Path(p) for p in sys.argv[2:]]
    pngs = [p.read_bytes() for p in sources]
    dst.write_bytes(make_ico(pngs))
    sizes = ", ".join(f"{png_size(p)[0]}x{png_size(p)[1]}" for p in sorted(pngs, key=lambda p: png_size(p)[0]))
    print(f"wrote {dst} ({sizes})")
