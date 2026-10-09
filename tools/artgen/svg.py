"""部件（Part）：一段 SVG 內容加上它在骨骼座標系中的範圍。"""
from dataclasses import dataclass


@dataclass
class Part:
    body: str
    box: tuple[float, float, float, float]   # (x0, y0, x1, y1)，骨骼局部座標

    def to_svg(self) -> str:
        x0, y0, x1, y1 = self.box
        w, h = x1 - x0, y1 - y0
        return (
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x0:g} {y0:g} {w:g} {h:g}" '
            f'width="{w:g}" height="{h:g}">\n{self.body}\n</svg>\n'
        )
