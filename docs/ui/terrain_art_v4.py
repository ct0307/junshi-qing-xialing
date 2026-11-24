"""虎牢关第四稿：保留验收布局，细化岩层、关楼与营帐。

所有结构仍为演义沙盘的美术改编。模块导入不生成文件。
"""

from __future__ import annotations

import importlib.util
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


_spec = importlib.util.spec_from_file_location(
    "_terrain_v3_base_v4", Path(__file__).with_name("terrain_art_v3.py"))
_v3 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_v3)
_Painter = _v3._Painter
ROUTES, EMPTY_SLOTS, DEPLOYED = _v3.ROUTES, _v3.EMPTY_SLOTS, _v3.DEPLOYED
__all__ = ["make_terrain", "make_gate_asset", "make_tent_asset",
           "ROUTES", "EMPTY_SLOTS", "DEPLOYED"]

_RIDGES = (
    ((44, 153), (103, 148), (147, 171), (145, 202), (111, 228), (44, 223)),
    ((212, 149), (346, 145), (465, 166), (483, 187), (361, 180), (253, 192)),
    ((786, 148), (935, 145), (1028, 168), (1190, 153), (1375, 146),
     (1451, 176), (1395, 205), (1271, 192), (1158, 211), (996, 193), (850, 194)),
    ((1500, 148), (1690, 150), (1740, 195), (1840, 213), (1877, 260),
     (1877, 474), (1820, 469), (1770, 439), (1755, 398), (1710, 366),
     (1690, 312), (1610, 276), (1595, 217), (1535, 204)),
    ((1762, 641), (1812, 620), (1878, 633), (1880, 674),
     (1834, 674), (1793, 660), (1760, 665)),
    ((48, 780), (96, 779), (137, 814), (135, 834), (45, 836)),
)


class _AssetPainter(_Painter):
    """独立构件仅申请自身范围的画布，避免按整张地图放大。"""

    def __init__(self, scale, box):
        self.scale = scale
        self.origin = box[:2]
        self.size = (round((box[2] - box[0]) * scale),
                     round((box[3] - box[1]) * scale))
        self.image = Image.new("RGBA", self.size)
        self.draw = ImageDraw.Draw(self.image)

    def points(self, points):
        return [(round((x - self.origin[0]) * self.scale),
                 round((y - self.origin[1]) * self.scale)) for x, y in points]

    def box(self, box):
        return tuple(round((value - self.origin[index % 2]) * self.scale)
                     for index, value in enumerate(box))


def _validate_size(value, label):
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise ValueError(f"{label} 必须是正整数")


def _ridge(scale, points, index):
    """用顺着岩壁的层理替代放射扇面，不向可部署区域扩散细节。"""
    mask_painter = _Painter(scale)
    mask_painter.polygon(points, "white")
    mask = mask_painter.image.getchannel("A")
    p = _Painter(scale)
    left, right = min(x for x, _ in points), max(x for x, _ in points)
    top, bottom = min(y for _, y in points), max(y for _, y in points)
    span = max(1, right - left)
    p.polygon(points, (160, 166, 139, 255))
    # 轻缓的岩层采用折断的长面，形体不再汇聚到单个三角中心。
    rows = max(2, math.ceil((bottom - top) / 48))
    palette = ((173, 177, 148, 255), (159, 168, 140, 255),
               (166, 172, 143, 255), (154, 164, 137, 255))
    for row in range(rows):
        y = top + row * 48
        ridge = []
        for step in range(9):
            x = left + span * step / 8
            waviness = 7 * math.sin(step * 1.7 + index * .9 + row * .7)
            ridge.append((x, y + waviness + step * 1.7))
        # 大岩面只铺局部不等长的层片，避免像整齐的横条纹。
        segment = ridge if span < 180 else ridge[(row + index) % 3:9 - row % 3]
        lower = [(x + 11, y + 49 + 8 * math.sin(i + row))
                 for i, (x, y) in enumerate(reversed(segment))]
        p.polygon(segment + lower, palette[(row + index) % len(palette)])
        # 只画断续层缝，避免整齐条纹覆盖整面岩体。
        if row:
            first = 1 + (row + index) % 3
            p.line(ridge[first:first + 4], (119, 136, 113, 125), .9)
            p.line([(x, y - 1.5) for x, y in ridge[first:first + 3]],
                   (201, 202, 166, 150), .65)
    # 结构性纵裂缝在每个岩块只出现一至两处。
    for fracture in range(1 if span < 180 else 2):
        x = left + span * (.35 + fracture * .35)
        y = top + 9 + fracture * 7
        height = min(72, (bottom - top) * .63)
        pts = ((x, y), (x + 8, y + height * .25),
               (x + 3, y + height * .48), (x + 12, y + height))
        p.line(pts, (118, 137, 114, 160), 1)
        p.line([(a - 1.3, b) for a, b in pts], (203, 204, 168, 135), .7)
    # 顶缘草土仅在上沿出现；不以均匀噪点制造质感。
    cap = [(left, top - 3), (right, top - 3),
           (right, top + 13), (left + span * .69, top + 9),
           (left + span * .42, top + 20), (left + span * .16, top + 12),
           (left, top + 18)]
    p.polygon(cap, (162, 176, 131, 255))
    for step in range(max(1, int(span / 38))):
        x = left + 13 + step * 38
        y = top + 11 + 4 * math.sin(step * 1.3 + index)
        p.line(((x - 4, y + 2), (x + 6, y)), (185, 194, 145, 180), .8)
        p.line(((x, y + 2), (x + 1.4, y - 2)), (131, 158, 111, 140), .7)
    p.image.putalpha(ImageChops.multiply(p.image.getchannel("A"), mask))
    # 薄边保持 v3 的岩块轮廓与微缩模型的厚度。
    outline = _Painter(scale)
    outline.line(list(points) + [points[0]], (198, 197, 160, 160), .8)
    p.image.alpha_composite(outline.image)
    return p.image


def _gate(scale, painter=None):
    """以同一门楼足迹绘制砖石、门洞、木构和瓦檐。"""
    p = painter if painter is not None else _Painter(scale)
    # 城墙厚度、背光面与垛口；不占用任何阵位。
    for top, bottom in ((421, 520), (604, 662)):
        p.polygon(((1759, top), (1781, top - 7), (1796, top + 4),
                   (1796, bottom), (1773, bottom + 9), (1759, bottom)),
                  (118, 132, 109, 255))
        p.polygon(((1759, top), (1781, top - 7), (1796, top + 4),
                   (1773, top + 12)), (192, 191, 154, 255))
        p.line(((1759, top + 2), (1759, bottom)), (210, 203, 164, 220), 1.3)
        for row, y in enumerate(range(top + 6, bottom - 1, 12)):
            p.line(((1774, y + 6), (1794, y - 1)), (86, 110, 94, 125), .8)
            for x in (1780 + (row % 2) * 7, 1790 - (row % 2) * 5):
                p.line(((x, y + 3), (x, y + 10)), (86, 110, 94, 120), .65)
        for y in range(top + 5, bottom - 2, 15):
            p.polygon(((1757, y), (1766, y - 3), (1766, y + 5),
                       (1757, y + 8)), (174, 178, 143, 255))
            p.line(((1757, y), (1766, y - 3)), (222, 212, 168, 210), .8)
    # 门楼底座。朝西面比北侧暗，方向与整张沙盘一致。
    p.polygon(((1740, 530), (1788, 518), (1810, 538), (1810, 594),
               (1763, 608), (1740, 590)), (164, 166, 133, 255))
    p.polygon(((1740, 539), (1762, 549), (1762, 607), (1740, 590)),
              (125, 140, 115, 255))
    for row, y in enumerate(range(551, 598, 10)):
        p.line(((1767, y), (1806, y - 10)), (113, 132, 110, 160), .7)
        for x in (1776 + row % 2 * 6, 1792 + row % 2 * 5):
            p.line(((x, y - 4), (x, y + 4)), (113, 132, 110, 145), .65)
    # 深门洞与侧开木门，让入口在缩图中仍清楚。
    p.ellipse((1741.5, 545, 1763.5, 575), (33, 57, 49, 255))
    p.polygon(((1741.5, 559), (1763.5, 559), (1763.5, 594), (1741.5, 579)),
              (33, 57, 49, 255))
    p.polygon(((1758, 551), (1763, 554), (1763, 589), (1758, 585)),
              (105, 98, 68, 255))
    p.line(((1761, 555), (1761, 586)), (161, 142, 94, 255), .65)
    for y in (564, 576):
        p.line(((1758.5, y), (1763, y + 2)), (52, 70, 52, 230), 1.1)
        p.ellipse((1759, y, 1760, y + 1), (189, 167, 108, 255))
    p.line(((1740, 552), (1745, 545), (1754, 543), (1763, 549)),
           (221, 207, 163, 255), 2)
    p.line(((1740, 553), (1740, 579)), (221, 207, 163, 255), 2)
    p.line(((1740, 580), (1762, 594)), (193, 184, 139, 255), 1.2)
    # 木构楼身，柱间暗窗和檐底阴影在小尺寸下保留识别。
    p.polygon(((1746, 512), (1787, 505), (1798, 514), (1798, 538),
               (1757, 548), (1746, 539)), (151, 116, 77, 255))
    p.polygon(((1759, 516), (1795, 510), (1795, 531), (1759, 540)),
              (77, 86, 66, 255))
    for x in (1752, 1765, 1780, 1794):
        p.line(((x, 515), (x, 539)), (105, 86, 59, 255), 2.5)
        p.line(((x - .6, 517), (x - .6, 537)), (204, 169, 113, 255), .65)
    p.line(((1757, 539), (1798, 530)), (215, 178, 116, 255), 1.3)
    p.line(((1757, 543), (1798, 534)), (103, 84, 57, 255), 2)
    for x in (1762, 1771, 1784, 1792):
        p.line(((x, 523), (x, 530)), (142, 128, 89, 255), .65)
    # 原屋顶轮廓保留。檐底与瓦脊分成连续暗面、细亮边。
    roof = ((1728, 516), (1761, 491), (1807, 507), (1818, 524), (1758, 537))
    p.polygon(tuple((x, y + 3) for x, y in roof), (43, 67, 53, 255))
    p.polygon(roof, (47, 86, 71, 255))
    p.polygon(((1728, 516), (1761, 491), (1786, 507), (1758, 530)),
              (83, 120, 89, 255))
    # 两坡瓦槽服从屋脊与檐口方向，避免纵横杂乱网格。
    for t in (.15, .3, .45, .6, .75, .9):
        a = (1761 + (1786 - 1761) * t, 491 + 16 * t)
        b = (1728 + 30 * t, 516 + 14 * t)
        p.line((a, b), (140, 159, 115, 155), .65)
        c = (1786 + 21 * t, 507)
        d = (1758 + 60 * t, 533 - 9 * t)
        p.line((c, d), (107, 137, 102, 170), .65)
    p.line(((1727, 516), (1758, 534), (1818, 523)), (174, 183, 137, 255), 1.7)
    p.line(((1761, 491), (1786, 507), (1807, 507)), (199, 201, 156, 255), 1.2)
    p.line(((1727, 516), (1725, 512)), (174, 183, 137, 255), 1.4)
    p.line(((1817, 523), (1820, 518)), (174, 183, 137, 255), 1.4)
    # 青旗布褶采用三个大明暗面。
    p.line(((1789, 500), (1789, 449)), (111, 109, 77, 255), 2)
    p.line(((1788.5, 451), (1788.5, 498)), (217, 205, 149, 255), .65)
    p.polygon(((1790, 450), (1810, 455), (1822, 452), (1818, 465),
               (1820, 475), (1806, 479), (1790, 471)), (47, 133, 117, 255))
    p.polygon(((1790, 451), (1806, 455), (1806, 478), (1790, 471)),
              (70, 153, 128, 255))
    p.polygon(((1806, 456), (1811, 455), (1810, 478), (1806, 479)),
              (39, 109, 99, 255))
    p.line(((1791, 451), (1809, 456), (1821, 453)), (178, 207, 161, 220), .9)
    return p.image


def _tent(scale, x=646, y=220, painter=None):
    """营帐的门帘、缝线、拉绳与地钉；位置仍在远景营区。"""
    p = painter if painter is not None else _Painter(scale)
    p.ellipse((x - 23, y + 6, x + 28, y + 18), (44, 70, 44, 46))
    p.line(((x - 8, y - 3), (x - 28, y + 12)), (212, 196, 143, 230), .65)
    p.line(((x + 12, y - 1), (x + 28, y + 13)), (199, 184, 129, 230), .65)
    for pin in (-28, 28):
        p.line(((x + pin, y + 10), (x + pin - 1, y + 14)), (101, 100, 68, 255), 1)
    p.polygon(((x - 19, y + 10), (x, y - 13), (x + 21, y + 9)),
              (192, 182, 134, 255))
    p.polygon(((x, y - 13), (x + 21, y + 9), (x + 5, y + 8)),
              (151, 150, 108, 255))
    p.line(((x, y - 13), (x + 5, y + 8)), (229, 213, 159, 240), .8)
    p.polygon(((x - 7, y + 9), (x - 2, y - 3), (x + 3, y + 9)),
              (95, 112, 79, 255))
    p.polygon(((x - 2, y - 3), (x + 3, y + 9), (x + 6, y + 8)),
              (207, 191, 138, 255))
    p.line(((x - 17, y + 9), (x - 4, y + 9)), (222, 205, 147, 210), .65)
    p.line(((x - 3, y - 8), (x - 12, y + 5)), (217, 202, 146, 155), .65)
    return p.image


def _square_asset(art, size, padding=.04):
    bbox = art.getchannel("A").getbbox()
    result = Image.new("RGBA", (size, size))
    art = art.crop(bbox)
    factor = (size * (1 - padding * 2)) / max(art.size)
    art = art.resize((max(1, round(art.width * factor)),
                      max(1, round(art.height * factor))), Image.Resampling.LANCZOS)
    result.alpha_composite(art, ((size - art.width) // 2, (size - art.height) // 2))
    return result


def make_gate_asset(size: int = 512) -> Image.Image:
    """返回含南北城墙的透明正方形关楼素材；与地图使用同一绘图源。"""
    _validate_size(size, "size")
    scale = max(2, math.ceil(size / 245))
    return _square_asset(_gate(scale, _AssetPainter(scale, (1720, 410, 1827, 677))), size)


def make_tent_asset(size: int = 256) -> Image.Image:
    """返回透明正方形营帐素材，含门帘、拉绳、地钉和接触阴影。"""
    _validate_size(size, "size")
    scale = max(2, math.ceil(size / 58))
    return _square_asset(_tent(scale, painter=_AssetPainter(scale, (614, 203, 678, 240))), size)


def make_terrain(scale: int = 2) -> Image.Image:
    """保留 v3 全部道路与 34 个阵位，覆盖精修后的环境构件。"""
    _validate_size(scale, "scale")
    canvas = _v3.make_terrain(scale)
    for index, points in enumerate(_RIDGES):
        canvas.alpha_composite(_ridge(scale, points, index))
    for x, y in ((602, 213), (646, 220), (690, 211)):
        canvas.alpha_composite(_tent(scale, x, y))
    canvas.alpha_composite(_gate(scale))
    assert len(EMPTY_SLOTS) == 30 and len(DEPLOYED) == 4
    return canvas


if __name__ == "__main__":
    target = Path(__file__).with_name("terrain-check-v4.png")
    make_terrain(2).resize((1920, 1080), Image.Resampling.LANCZOS).save(target)
    print(f"输出：{target}")
