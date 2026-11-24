"""虎牢关主题的微缩山口沙盘；为美术改编，不作地理复原。"""

from __future__ import annotations

import importlib.util
import math
from pathlib import Path
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter


# 只复用绘图工具，绝不调用旧版地图生成函数。
_spec = importlib.util.spec_from_file_location(
    "_terrain_v2_tools", Path(__file__).with_name("terrain_art_v2.py"))
_tools = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_tools)
_Painter, _curve, _ground = _tools._Painter, _tools._curve, _tools._ground

ROUTES = (
    ((65, 350), (340, 350), (490, 440), (780, 440), (900, 500)),
    ((65, 720), (370, 720), (540, 620), (760, 620), (900, 500)),
    ((900, 500), (1120, 500), (1280, 560), (1560, 560), (1700, 560)),
)
EMPTY_SLOTS = (
    (190, 250), (365, 250), (550, 280), (715, 290), (895, 270),
    (1080, 280), (1250, 295), (1430, 290), (1555, 340), (200, 460),
    (345, 470), (525, 520), (655, 520), (850, 400), (1100, 600),
    (1250, 690), (1395, 695), (1515, 780), (1705, 740), (1830, 735),
    (200, 620), (300, 615), (430, 600), (200, 810), (365, 820),
    (535, 765), (680, 780), (830, 745), (990, 735), (1120, 790),
)
DEPLOYED = ((1030, 407), (1365, 458), (810, 560), (1585, 690))
_BACKGROUND = (25, 62, 55, 255)


def _road_mask(scale, width):
    painter = _Painter(scale)
    for route in ROUTES + (((1700, 560), (1752, 560)),):
        painter.line(_curve(route), "white", width)
        for x, y in (route[0], route[-1]):
            half = width / 2
            painter.ellipse((x - half, y - half, x + half, y + half), "white")
    return painter.image.getchannel("A")


def _ridge(painter, points, depth=15):
    """暖灰石壁的切面、背光面与顶部薄草坡。"""
    painter.polygon([(x + 5, y + depth) for x, y in points], (91, 105, 88, 255))
    painter.polygon(points, (160, 164, 137, 255))
    center = (sum(x for x, _ in points) / len(points),
              sum(y for _, y in points) / len(points) - 14)
    for index, first in enumerate(points):
        second = points[(index + 1) % len(points)]
        if index % 3 == 0:
            color = (182, 181, 151, 255)
        elif index % 3 == 1:
            color = (141, 151, 125, 255)
        else:
            color = (164, 171, 136, 255)
        painter.polygon((first, second, center), color)
    painter.line(points + [points[0]], (198, 197, 160, 190), 1.2)
    for index in range(1, len(points) - 1, 3):
        first, second = points[index], points[index + 1]
        painter.line(((first[0] + 2, first[1] + 6),
                      (second[0] + 2, second[1] + 6)), (115, 128, 104, 140), 1)


def _gate(scale):
    painter = _Painter(scale)
    # 南北城墙嵌入山体；门洞一侧留出连续的浅色关道。
    for top, bottom in ((421, 520), (604, 662)):
        painter.polygon(((1759, top), (1781, top - 7), (1796, top + 4),
                         (1796, bottom), (1773, bottom + 9), (1759, bottom)),
                        (118, 132, 109, 255))
        painter.polygon(((1759, top), (1781, top - 7), (1796, top + 4),
                         (1773, top + 12)), (192, 191, 154, 255))
        painter.line(((1758, top + 2), (1758, bottom)), (205, 201, 161, 210), 1.4)
        for y in range(top + 5, bottom - 2, 15):
            painter.polygon(((1757, y), (1766, y - 3), (1766, y + 5), (1757, y + 8)),
                            (174, 178, 143, 255))
            painter.line(((1775, y + 7), (1793, y)), (87, 108, 91, 170), 0.8)
    # 门楼座落于岩壁的缺口，西向暗门洞正对来路。
    painter.polygon(((1740, 530), (1788, 518), (1810, 538), (1810, 594),
                     (1763, 608), (1740, 590)), (164, 166, 133, 255))
    painter.polygon(((1740, 539), (1762, 549), (1762, 607), (1740, 590)),
                    (125, 140, 115, 255))
    painter.ellipse((1742, 546, 1764, 575), (37, 61, 52, 255))
    painter.polygon(((1742, 560), (1764, 559), (1764, 593), (1742, 579)),
                    (37, 61, 52, 255))
    painter.line(((1740, 552), (1745, 545), (1754, 543), (1763, 549)),
                 (218, 204, 155, 255), 2)
    painter.line(((1740, 554), (1740, 579)), (218, 204, 155, 255), 2)
    for y in range(552, 590, 11):
        painter.line(((1770, y), (1804, y - 9)), (116, 133, 108, 180), 0.8)
    painter.polygon(((1746, 512), (1787, 505), (1798, 514), (1798, 538),
                     (1757, 548), (1746, 539)), (150, 117, 77, 255))
    for x in (1751, 1765, 1780, 1792):
        painter.line(((x, 515), (x, 538)), (90, 84, 61, 255), 2.2)
    painter.polygon(((1728, 516), (1761, 491), (1807, 507), (1818, 524),
                     (1758, 537)), (47, 86, 71, 255))
    painter.polygon(((1728, 516), (1761, 491), (1786, 507), (1758, 530)),
                    (83, 120, 89, 255))
    painter.line(((1727, 517), (1758, 534), (1818, 523)), (153, 169, 124, 255), 2)
    painter.line(((1761, 491), (1786, 507)), (184, 188, 142, 255), 1.3)
    for x in range(1737, 1804, 7):
        painter.line(((x, 516), (x + 8, 525)), (116, 147, 105, 170), 0.9)
    painter.line(((1789, 500), (1789, 449)), (111, 109, 77, 255), 2)
    painter.polygon(((1790, 450), (1810, 455), (1822, 452), (1818, 465),
                     (1820, 475), (1806, 479), (1790, 471)), (47, 133, 117, 255))
    painter.line(((1791, 451), (1809, 456), (1821, 453)), (157, 192, 150, 220), 1)
    return painter.image


def make_terrain(scale: int = 2) -> Image.Image:
    """生成无文字、无人物的虎牢关沙盘，返回缩放后的 RGBA 图像。"""
    if isinstance(scale, bool) or not isinstance(scale, int) or scale < 1:
        raise ValueError("scale 必须是正整数")
    rng = random.Random(31811104)
    canvas = Image.new("RGBA", (1920 * scale, 1080 * scale), _BACKGROUND)
    painter = _Painter(scale)
    painter.rounded((25, 139, 1898, 871), 29, (7, 29, 24, 100))
    canvas.alpha_composite(painter.blur(9))
    painter = _Painter(scale)
    painter.rounded((24, 128, 1896, 858), 27, (56, 78, 57, 255), (111, 130, 88, 255), 1)
    painter.rounded((30, 133, 1890, 849), 25, (113, 131, 88, 255))
    canvas.alpha_composite(painter.image)
    ground, board_mask = _ground(scale, rng)
    canvas.alpha_composite(ground)

    # 山口上、下岩脊把右侧关城纳入地形，营地阵位仍保持空旷。
    ridges = (
        [(44, 153), (103, 148), (147, 171), (145, 202), (111, 228), (44, 223)],
        [(212, 149), (346, 145), (465, 166), (483, 187), (361, 180), (253, 192)],
        [(786, 148), (935, 145), (1028, 168), (1190, 153), (1375, 146),
         (1451, 176), (1395, 205), (1271, 192), (1158, 211), (996, 193), (850, 194)],
        [(1500, 148), (1690, 150), (1740, 195), (1840, 213), (1877, 260),
         (1877, 474), (1820, 469), (1770, 439), (1755, 398), (1710, 366),
         (1690, 312), (1610, 276), (1595, 217), (1535, 204)],
        [(1762, 641), (1812, 620), (1878, 633), (1880, 674),
         (1834, 674), (1793, 660), (1760, 665)],
        [(48, 780), (96, 779), (137, 814), (135, 834), (45, 836)],
    )
    shadow = _Painter(scale)
    for points in ridges:
        shadow.polygon([(x + 7, y + 19) for x, y in points], (45, 68, 44, 89))
    shadow.image.putalpha(ImageChops.multiply(shadow.blur(8).getchannel("A"), board_mask))
    canvas.alpha_composite(shadow.image)
    painter = _Painter(scale)
    for points in ridges:
        _ridge(painter, points)
    painter.image.putalpha(ImageChops.multiply(painter.image.getchannel("A"), board_mask))
    canvas.alpha_composite(painter.image)

    # 两支斜向关道的蒙版统一合成，汇合处不产生重叠接缝。
    core = _road_mask(scale, 73)
    edge = _road_mask(scale, 83)
    cast = ImageChops.offset(edge, 3 * scale, 6 * scale).filter(
        ImageFilter.GaussianBlur(4 * scale))
    shadow = Image.new("RGBA", canvas.size, (41, 65, 43, 0))
    shadow.putalpha(cast.point(lambda value: value * 70 // 255))
    canvas.alpha_composite(shadow)
    for color, mask in (((157, 154, 108, 255), edge), ((219, 201, 158, 255), core)):
        layer = Image.new("RGBA", canvas.size, color)
        layer.putalpha(mask)
        canvas.alpha_composite(layer)
    logical_road = core.resize((1920, 1080), Image.Resampling.NEAREST)
    painter = _Painter(scale)
    for _ in range(6500):
        x, y = rng.randint(45, 1745), rng.randint(300, 765)
        if logical_road.getpixel((x, y)):
            radius = rng.uniform(0.4, 1.1)
            painter.ellipse((x - radius, y - radius * 0.45, x + radius, y + radius * 0.45),
                            rng.choice(((144, 126, 83, 35), (255, 238, 187, 63))))
    canvas.alpha_composite(painter.image)

    # 远处的少量帐篷与旌旗只负责交代军阵气氛，不占用阵位。
    painter = _Painter(scale)
    for x, y in ((602, 213), (646, 220), (690, 211)):
        painter.ellipse((x - 19, y + 8, x + 25, y + 18), (44, 70, 44, 53))
        painter.polygon(((x - 19, y + 10), (x, y - 13), (x + 21, y + 9)),
                        (188, 178, 130, 255))
        painter.polygon(((x, y - 13), (x + 21, y + 9), (x + 5, y + 8)),
                        (151, 150, 108, 255))
        painter.line(((x, y - 13), (x + 5, y + 8)), (218, 204, 149, 180), 1)
    for x, y in ((590, 203), (719, 218)):
        painter.line(((x, y + 17), (x, y - 20)), (116, 111, 76, 255), 1.5)
        painter.polygon(((x + 1, y - 20), (x + 19, y - 15), (x + 14, y - 5),
                         (x + 1, y - 7)), (155, 100, 73, 255))
    # 几笔草坡短线沿岩脚出现；中央和部署区域保持疏朗。
    for _ in range(240):
        x, y = rng.randint(70, 1860), rng.choice((rng.randint(205, 235), rng.randint(790, 830)))
        if any(math.hypot(x - sx, y - sy) < 46 for sx, sy in EMPTY_SLOTS + DEPLOYED):
            continue
        if logical_road.getpixel((x, y)):
            continue
        painter.line(((x, y), (x + 2, y - 3)), (125, 151, 91, 110), 0.8)
    canvas.alpha_composite(painter.image)

    painter = _Painter(scale)
    for x, y in EMPTY_SLOTS:
        painter.ellipse((x - 28, y - 6, x + 31, y + 16), (56, 81, 48, 45))
        painter.ellipse((x - 26, y - 8, x + 26, y + 12), (108, 127, 91, 255))
        painter.ellipse((x - 26, y - 11, x + 26, y + 8), (145, 156, 114, 255))
        painter.ellipse((x - 22, y - 8, x + 22, y + 5), (134, 148, 107, 255))
        painter.line(((x - 20, y - 5), (x - 13, y - 9), (x + 8, y - 10)),
                     (181, 185, 137, 170), 1)
    canvas.alpha_composite(painter.image)
    canvas.alpha_composite(_gate(scale))
    canvas.paste(_BACKGROUND, (0, 0, canvas.width, 120 * scale))
    canvas.paste(_BACKGROUND, (0, 880 * scale, canvas.width, canvas.height))
    return canvas
