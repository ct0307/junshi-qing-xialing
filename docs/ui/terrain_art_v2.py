"""以 Pillow 绘制青绿微缩沙盘，坐标均以 1920×1080 为基准。"""

from __future__ import annotations

import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter


_WIDTH, _HEIGHT = 1920, 1080
_BACKGROUND = (25, 62, 55, 255)
_BOARD = (24, 128, 1896, 858)
_ROUTES = (
    ((70, 342), (430, 342), (430, 546), (790, 546)),
    ((70, 742), (620, 742), (620, 546), (790, 546)),
    ((790, 546), (1080, 546), (1080, 320), (1460, 320),
     (1460, 655), (1730, 655), (1762, 655)),
)
_SLOTS = (
    (190, 240), (330, 230), (535, 237), (700, 239), (865, 234),
    (1000, 218), (1182, 210), (1362, 206), (1558, 236), (1743, 230),
    (197, 435), (315, 445), (543, 435), (686, 431), (1182, 425),
    (1374, 430), (1580, 417), (1758, 407), (190, 604), (342, 615),
    (746, 670), (910, 700), (1090, 707), (1240, 625), (1347, 609),
    (1360, 762), (1195, 770), (770, 795), (908, 806), (220, 816),
)
_DEPLOYED = ((866, 444), (1270, 472), (516, 652), (1606, 747))


class _Painter:
    """将逻辑坐标统一缩放，避免细节绘制混入物理像素。"""

    def __init__(self, scale: int):
        self.scale = scale
        self.size = (_WIDTH * scale, _HEIGHT * scale)
        self.image = Image.new("RGBA", self.size)
        self.draw = ImageDraw.Draw(self.image)

    def points(self, points):
        return [(round(x * self.scale), round(y * self.scale)) for x, y in points]

    def box(self, box):
        return tuple(round(value * self.scale) for value in box)

    def polygon(self, points, fill):
        self.draw.polygon(self.points(points), fill=fill)

    def ellipse(self, box, fill, outline=None, width=1):
        self.draw.ellipse(self.box(box), fill=fill, outline=outline,
                          width=max(1, round(width * self.scale)))

    def line(self, points, fill, width=1):
        self.draw.line(self.points(points), fill=fill,
                       width=max(1, round(width * self.scale)), joint="curve")

    def rounded(self, box, radius, fill, outline=None, width=1):
        self.draw.rounded_rectangle(self.box(box), round(radius * self.scale),
                                    fill=fill, outline=outline,
                                    width=max(1, round(width * self.scale)))

    def blur(self, radius):
        self.image = self.image.filter(ImageFilter.GaussianBlur(radius * self.scale))
        self.draw = ImageDraw.Draw(self.image)
        return self.image


def _curve(points, radius=43):
    """用二次曲线削去直角，保留每条固定路线的拓扑与中心线。"""
    result = [points[0]]
    for index in range(1, len(points) - 1):
        before, corner, after = points[index - 1:index + 2]
        first = math.dist(before, corner)
        second = math.dist(corner, after)
        cut = min(radius, first * 0.42, second * 0.42)
        incoming = (corner[0] + (before[0] - corner[0]) * cut / first,
                    corner[1] + (before[1] - corner[1]) * cut / first)
        outgoing = (corner[0] + (after[0] - corner[0]) * cut / second,
                    corner[1] + (after[1] - corner[1]) * cut / second)
        result.append(incoming)
        for step in range(1, 17):
            time = step / 16
            result.append(((1 - time) ** 2 * incoming[0]
                           + 2 * (1 - time) * time * corner[0] + time ** 2 * outgoing[0],
                           (1 - time) ** 2 * incoming[1]
                           + 2 * (1 - time) * time * corner[1] + time ** 2 * outgoing[1]))
    result.append(points[-1])
    return result


def _route_mask(scale, extra=0):
    mask = Image.new("L", (_WIDTH * scale, _HEIGHT * scale))
    draw = ImageDraw.Draw(mask)
    width = round((74 + extra) * scale)
    for route in _ROUTES:
        points = [(round(x * scale), round(y * scale)) for x, y in _curve(route)]
        draw.line(points, fill=255, width=width, joint="curve")
        for x, y in (points[0], points[-1]):
            half = width // 2
            draw.ellipse((x - half, y - half, x + half, y + half), fill=255)
    return mask


def _ground(scale, rng):
    """先以低频色差建立材料，再叠加克制的草屑，不使用高频噪声滤镜。"""
    size = (_WIDTH * scale, _HEIGHT * scale)
    mask_painter = _Painter(scale)
    mask_painter.rounded((37, 140, 1883, 841), 23, "white")
    mask = mask_painter.image.getchannel("A")
    small = Image.new("RGB", (480, 270))
    pixels = small.load()
    for y in range(270):
        for x in range(480):
            light = 4.8 * math.sin(x / 76) + 3 * math.sin((x + y) / 43)
            light += 2.4 * math.cos(y / 21) + rng.uniform(-1.3, 1.3)
            slope = y / 270
            pixels[x, y] = (round(136 + slope * 13 + light),
                             round(163 + slope * 7 + light),
                             round(124 + slope * 0.5 + light * 0.72))
    ground = small.resize(size, Image.Resampling.BICUBIC).convert("RGBA")
    ground.putalpha(mask)

    washes = _Painter(scale)
    for _ in range(66):
        x, y = rng.randint(45, 1870), rng.randint(150, 835)
        rx, ry = rng.randint(50, 180), rng.randint(18, 65)
        color = rng.choice(((52, 101, 77, 31), (173, 181, 121, 24),
                            (143, 162, 106, 28), (45, 99, 79, 24)))
        washes.ellipse((x - rx, y - ry, x + rx, y + ry), color)
    wash = washes.blur(26)
    wash.putalpha(ImageChops.multiply(wash.getchannel("A"), mask))
    ground.alpha_composite(wash)
    return ground, mask


def _shelf(painter, points, height=12):
    """台地的石土切面统一向右下投影。"""
    down = [(x + 2, y + height) for x, y in points]
    painter.polygon(down, (72, 91, 69, 255))
    for index in range(len(points) - 1):
        first, second = points[index], points[index + 1]
        if second[0] < first[0]:
            painter.polygon((first, second, down[index + 1], down[index]),
                            (92, 105, 75, 255))
            painter.line(((first[0], first[1] + height * 0.58),
                          (second[0], second[1] + height * 0.58)),
                         (121, 128, 88, 130), 1)
    painter.polygon(points, (120, 143, 99, 255))
    painter.line(points + [points[0]], (149, 163, 115, 180), 1.2)


def _stone(painter, x, y, radius, rng):
    """岩石采用低饱和切面，避免成为强于可部署阵位的视觉目标。"""
    points = [(x - radius, y + radius * 0.10),
              (x - radius * 0.61, y - radius * 0.55),
              (x + radius * 0.04, y - radius * 0.86),
              (x + radius * 0.76, y - radius * 0.36),
              (x + radius, y + radius * 0.29),
              (x + radius * 0.32, y + radius * 0.58),
              (x - radius * 0.60, y + radius * 0.53)]
    painter.ellipse((x - radius, y + radius * 0.17,
                     x + radius * 1.3, y + radius * 0.87), (29, 58, 43, 67))
    painter.polygon(points, (107, 127, 107, 255))
    painter.polygon((points[0], points[1], points[2],
                     (x + radius * 0.14, y - radius * 0.08)), (153, 164, 132, 255))
    painter.polygon((points[2], points[3], points[4],
                     (x + radius * 0.14, y - radius * 0.08)), (126, 145, 119, 255))
    painter.polygon((points[0], (x + radius * 0.14, y - radius * 0.08),
                     points[4], points[5], points[6]), (87, 109, 93, 255))
    painter.line((points[1], points[2], points[3]), (179, 184, 145, 150), 0.8)
    if rng.random() < 0.65:
        painter.line(((x - radius * 0.39, y + radius * 0.32),
                      (x + radius * 0.13, y + radius * 0.11),
                      (x + radius * 0.37, y + radius * 0.26)), (61, 87, 76, 130), 0.9)


def _bamboo(painter, x, y, height, rng):
    """纤细竹节和成组叶片，保留空隙，不绘成大片深色剪影。"""
    painter.ellipse((x - 26, y - 5, x + 31, y + 9), (33, 67, 46, 49))
    for stem in range(6):
        offset = (stem - 2.5) * 5
        top = y - height * rng.uniform(0.70, 1.08)
        lean = rng.uniform(-9, 9)
        painter.line(((x + offset + 2, y), (x + offset + lean + 2, top)),
                     (42, 80, 59, 210), 3)
        painter.line(((x + offset, y - 1), (x + offset + lean, top)),
                     (143, 166, 105, 255), 1.5)
        for segment in range(1, 4):
            level = y - (y - top) * segment / 4
            at = x + offset + lean * segment / 4
            painter.line(((at - 1, level), (at + 3, level)), (184, 185, 123, 190), 1)
        for level in (top + 8, top + 21, top + 31):
            at = x + offset + lean * (y - level) / max(1, y - top)
            direction = -1 if (stem + round(level)) % 2 else 1
            for leaf in range(3):
                end_x = at + direction * (13 + leaf * 5)
                end_y = level - 6 + leaf * 6
                painter.polygon(((at, level + leaf * 1.3),
                                 (end_x, end_y),
                                 (at + direction * (8 + leaf * 3), level + leaf * 3 + 2)),
                                rng.choice(((54, 106, 72, 245), (70, 123, 81, 245),
                                            (93, 139, 86, 245))))


def _castle(scale):
    """西向门洞与道路相接，城墙、瓦檐和旗帜保持微缩比例。"""
    painter = _Painter(scale)
    painter.ellipse((1737, 675, 1888, 744), (18, 44, 36, 90))
    shadow = painter.blur(9)
    painter = _Painter(scale)
    # 基台的下沿和院内石铺地。
    painter.polygon(((1744, 606), (1848, 588), (1882, 612), (1882, 702),
                     (1778, 722), (1744, 701)), (62, 80, 69, 255))
    painter.polygon(((1744, 599), (1848, 581), (1882, 605), (1882, 693),
                     (1778, 713), (1744, 693)), (143, 151, 124, 255))
    painter.polygon(((1762, 614), (1846, 598), (1866, 612), (1866, 681),
                     (1783, 697), (1762, 682)), (172, 174, 143, 255))
    for index in range(5):
        y = 624 + index * 14
        painter.line(((1771, y), (1858, y - 16)), (139, 151, 127, 130), 0.7)
    for index in range(5):
        x = 1780 + index * 17
        painter.line(((x, 610), (x + 11, 688)), (139, 151, 127, 120), 0.7)
    # 北墙和东墙；顶部浅面在同一方向受光。
    painter.polygon(((1750, 606), (1849, 588), (1849, 608), (1750, 626)),
                    (123, 137, 112, 255))
    painter.polygon(((1750, 599), (1849, 581), (1856, 587), (1757, 605)),
                    (192, 193, 156, 255))
    painter.polygon(((1856, 588), (1878, 606), (1878, 691), (1862, 678)),
                    (95, 117, 101, 255))
    painter.polygon(((1855, 581), (1884, 603), (1878, 611), (1851, 589)),
                    (164, 176, 140, 255))
    painter.polygon(((1779, 687), (1878, 669), (1878, 695), (1779, 713)),
                    (124, 139, 117, 255))
    painter.polygon(((1772, 682), (1872, 664), (1879, 670), (1779, 689)),
                    (188, 191, 154, 255))
    # 砖缝与低矮垛口不使用强黑描边。
    for index in range(7):
        x = 1782 + index * 14
        y = 693 - index * 2.55
        painter.line(((x, y), (x + 11, y - 2)), (90, 111, 94, 170), 0.8)
        painter.line(((x + 7, y + 2), (x + 7, y + 8)), (91, 111, 94, 130), 0.7)
    for index in range(8):
        x = 1753 + index * 13
        y = 596 - index * 2.35
        painter.polygon(((x, y), (x + 7, y - 1.2), (x + 7, y + 6), (x, y + 7)),
                        (185, 188, 151, 255))
    for index in range(7):
        x = 1780 + index * 14
        y = 678 - index * 2.55
        painter.polygon(((x, y), (x + 8, y - 1.5), (x + 8, y + 8), (x, y + 9.5)),
                        (183, 188, 151, 255))
        painter.line(((x, y), (x + 8, y - 1.5)), (218, 215, 174, 230), 0.8)
    # 门楼面朝来路；门洞在道路高度保持畅通。
    painter.polygon(((1744, 618), (1775, 612), (1787, 626), (1787, 678),
                     (1757, 685), (1744, 674)), (152, 158, 126, 255))
    painter.polygon(((1744, 627), (1758, 635), (1758, 681), (1744, 672)),
                    (112, 129, 105, 255))
    painter.ellipse((1745, 640, 1761, 664), (41, 66, 56, 255))
    painter.polygon(((1745, 652), (1761, 651), (1761, 676), (1745, 668)),
                    (41, 66, 56, 255))
    painter.line(((1743, 643), (1749, 638), (1756, 640)), (202, 194, 151, 220), 2)
    painter.line(((1743, 646), (1743, 668)), (202, 194, 151, 210), 2)
    painter.polygon(((1749, 609), (1776, 604), (1782, 610), (1782, 630),
                     (1754, 636), (1749, 632)), (151, 120, 82, 255))
    for x in (1755, 1766, 1776):
        painter.line(((x, 612), (x, 630)), (91, 87, 66, 255), 2)
    painter.polygon(((1735, 610), (1759, 594), (1789, 604), (1797, 616),
                     (1758, 623)), (47, 83, 72, 255))
    painter.polygon(((1735, 610), (1759, 594), (1779, 606), (1758, 617)),
                    (72, 109, 86, 255))
    painter.line(((1734, 611), (1758, 620), (1798, 615)), (138, 156, 116, 255), 2)
    painter.line(((1759, 595), (1779, 606)), (160, 171, 126, 255), 1.3)
    for index in range(1, 7):
        x = 1741 + index * 6
        painter.line(((x, 610), (x + 7, 616)), (91, 126, 98, 210), 0.8)
    # 东北角楼和院内小屋形成层次。
    painter.polygon(((1835, 576), (1860, 572), (1872, 583), (1872, 615),
                     (1844, 620), (1835, 610)), (157, 165, 130, 255))
    painter.polygon(((1828, 578), (1848, 560), (1876, 578), (1880, 587),
                     (1844, 593)), (51, 89, 74, 255))
    painter.polygon(((1828, 578), (1848, 560), (1855, 578), (1844, 589)),
                    (80, 116, 88, 255))
    painter.line(((1828, 580), (1844, 591), (1880, 586)), (151, 169, 121, 255), 1.7)
    painter.polygon(((1803, 628), (1831, 623), (1846, 634), (1846, 652),
                     (1816, 659), (1803, 648)), (151, 138, 106, 255))
    painter.polygon(((1798, 630), (1820, 612), (1851, 630), (1855, 637),
                     (1814, 645)), (64, 102, 80, 255))
    painter.line(((1798, 632), (1814, 643), (1855, 636)), (135, 157, 115, 255), 1.4)
    # 青旗使用布褶和窄亮边，不加文字。
    painter.line(((1812, 605), (1812, 545)), (115, 112, 78, 255), 2)
    painter.line(((1811, 546), (1811, 603)), (210, 201, 142, 255), 0.7)
    painter.polygon(((1813, 546), (1832, 550), (1845, 547), (1840, 560),
                     (1842, 571), (1829, 574), (1813, 568)), (49, 130, 117, 255))
    painter.polygon(((1814, 547), (1829, 551), (1828, 572), (1814, 567)),
                    (76, 157, 134, 255))
    painter.line(((1814, 547), (1831, 551), (1844, 548)), (157, 192, 151, 220), 1)
    shadow.alpha_composite(painter.image)
    return shadow


def make_terrain(scale: int = 2) -> Image.Image:
    """返回 RGBA 沙盘；顶部与底部仅保留森林深青底色供 HUD 使用。"""
    if isinstance(scale, bool) or not isinstance(scale, int) or scale < 1:
        raise ValueError("scale 必须是正整数")
    rng = random.Random(20481104)
    canvas = Image.new("RGBA", (_WIDTH * scale, _HEIGHT * scale), _BACKGROUND)
    painter = _Painter(scale)
    painter.rounded((25, 139, 1898, 871), 29, (8, 30, 24, 105))
    canvas.alpha_composite(painter.blur(10))
    painter = _Painter(scale)
    painter.rounded(_BOARD, 27, (42, 66, 50, 255), (93, 115, 80, 255), 1.3)
    painter.rounded((26, 128, 1894, 848), 25, (69, 91, 63, 255))
    painter.rounded((31, 133, 1889, 844), 24, (106, 127, 87, 255))
    painter.rounded((35, 138, 1885, 842), 23, (71, 100, 72, 255))
    painter.line(((52, 131), (1868, 131)), (161, 174, 118, 170), 1)
    canvas.alpha_composite(painter.image)
    ground, board_mask = _ground(scale, rng)
    canvas.alpha_composite(ground)

    # 只在画面边缘放置切面台地，中央保留部署和辨路空间。
    shelves = (
        [(43, 165), (81, 153), (127, 166), (153, 199), (141, 249),
         (114, 278), (79, 273), (45, 257)],
        [(1047, 153), (1098, 149), (1132, 168), (1138, 205),
         (1121, 247), (1095, 256), (1069, 233), (1061, 195)],
        [(1800, 151), (1858, 148), (1878, 165), (1876, 292),
         (1844, 314), (1814, 293), (1821, 250), (1792, 214)],
        [(43, 530), (78, 515), (109, 539), (118, 584), (104, 627),
         (126, 668), (103, 689), (61, 682), (43, 650)],
        [(1522, 787), (1554, 780), (1572, 801), (1576, 836),
         (1455, 837), (1457, 819), (1484, 809)],
    )
    shadows = _Painter(scale)
    for points in shelves:
        shadows.polygon([(x + 9, y + 15) for x, y in points], (30, 67, 44, 92))
    canvas.alpha_composite(shadows.blur(9))
    details = _Painter(scale)
    for points in shelves:
        _shelf(details, points)
    canvas.alpha_composite(details.image)

    road_mask = _route_mask(scale)
    border_mask = _route_mask(scale, 13)
    road_shadow = ImageChops.offset(border_mask, 3 * scale, 7 * scale)
    road_shadow = road_shadow.filter(ImageFilter.GaussianBlur(4 * scale))
    shadow_layer = Image.new("RGBA", canvas.size, (38, 66, 43, 0))
    shadow_layer.putalpha(road_shadow.point(lambda value: value * 75 // 255))
    canvas.alpha_composite(shadow_layer)
    canvas.alpha_composite(Image.composite(Image.new("RGBA", canvas.size,
                                                     (123, 132, 89, 255)),
                                           Image.new("RGBA", canvas.size), border_mask))
    narrow_border = _route_mask(scale, 6)
    canvas.alpha_composite(Image.composite(Image.new("RGBA", canvas.size,
                                                     (187, 174, 122, 255)),
                                           Image.new("RGBA", canvas.size), narrow_border))
    road = Image.new("RGBA", (480, 270))
    road_pixels = road.load()
    for y in range(270):
        for x in range(480):
            light = 2.8 * math.sin(x / 12 + y / 27) + rng.uniform(-1.7, 1.7)
            road_pixels[x, y] = (round(219 + light), round(199 + light),
                                round(154 + light * 0.65), 255)
    road = road.resize(canvas.size, Image.Resampling.BICUBIC)
    road.putalpha(road_mask)
    canvas.alpha_composite(road)
    # 在统一路面蒙版里加砂粒、浅车辙，合流处不会叠出深色接缝。
    logical_road = road_mask.resize((_WIDTH, _HEIGHT), Image.Resampling.NEAREST)
    road_detail = _Painter(scale)
    for _ in range(7500):
        x, y = rng.uniform(40, 1780), rng.uniform(275, 785)
        if logical_road.getpixel((int(x), int(y))) < 128:
            continue
        radius = rng.uniform(0.30, 1.05)
        color = rng.choice(((137, 115, 75, 47), (255, 236, 180, 70),
                            (161, 133, 89, 43)))
        road_detail.ellipse((x - radius, y - radius * 0.5,
                             x + radius, y + radius * 0.5), color)
    for route in _ROUTES:
        points = _curve(route)
        for side in (-17, 17):
            offset_points = []
            for index, (x, y) in enumerate(points):
                before = points[max(0, index - 1)]
                after = points[min(len(points) - 1, index + 1)]
                distance = max(1, math.dist(before, after))
                offset_points.append((x - (after[1] - before[1]) * side / distance,
                                      y + (after[0] - before[0]) * side / distance))
            road_detail.line(offset_points, (151, 130, 85, 24), 1.2)
    road_detail.image.putalpha(ImageChops.multiply(road_detail.image.getchannel("A"), road_mask))
    canvas.alpha_composite(road_detail.image)

    # 部署点及其周围净空用于根模块绘制人物，不放山石或竹叶。
    avoid = _SLOTS + _DEPLOYED

    def clear(x, y, radius=0):
        if not (48 + radius < x < 1872 - radius and 154 + radius < y < 830 - radius):
            return False
        for at_x, at_y in avoid:
            safe = 72 if (at_x, at_y) in _DEPLOYED else 37
            if math.hypot(x - at_x, y - at_y) < safe + radius:
                return False
        if x > 1714 and 535 < y < 753:
            return False
        for dx, dy in ((0, 0), (radius + 7, 0), (-radius - 7, 0),
                       (0, radius + 7), (0, -radius - 7)):
            if logical_road.getpixel((round(x + dx), round(y + dy))) > 64:
                return False
        return True

    vegetation = _Painter(scale)
    # 草屑主要沿边缘聚集，大片地面以低频水洗质感表达。
    for _ in range(4200):
        x, y = rng.uniform(50, 1870), rng.uniform(155, 831)
        if not clear(x, y, 2) or rng.random() > 0.45:
            continue
        length = rng.uniform(1.1, 3.0)
        vegetation.line(((x, y), (x + length * 0.3, y - length)),
                        rng.choice(((57, 105, 71, 38), (190, 191, 123, 37),
                                    (70, 113, 76, 33))), 0.6)
    rock_clusters = ((86, 210, 31), (112, 249, 18), (1088, 184, 23),
                     (1107, 226, 16), (1846, 208, 30), (1833, 279, 17),
                     (83, 570, 23), (94, 643, 18), (1508, 819, 19),
                     (1544, 813, 15), (973, 792, 12), (1688, 504, 16))
    for x, y, radius in rock_clusters:
        if clear(x, y, radius):
            _stone(vegetation, x, y, radius, rng)
            for _ in range(3):
                at_x, at_y = x + rng.uniform(-radius, radius), y + radius * 0.5
                vegetation.ellipse((at_x - 5, at_y - 2, at_x + 6, at_y + 3),
                                   (77, 119, 74, 190))
    # 丛植有方向和疏密变化，避免围着每个阵位摆装饰。
    for x, y, height in ((1624, 228, 48), (1664, 544, 55), (1689, 552, 39),
                          (91, 478, 42), (986, 824, 36), (1790, 796, 49),
                          (1816, 798, 35), (1500, 198, 34)):
        if clear(x, y - height * 0.45, height * 0.5 + 8):
            _bamboo(vegetation, x, y, height, rng)
    for x, y, radius in ((391, 187, 17), (754, 180, 16), (1290, 188, 16),
                          (1680, 594, 20), (1836, 369, 16), (404, 814, 18),
                          (590, 823, 17), (1016, 633, 18), (1159, 572, 16),
                          (1521, 492, 15)):
        if not clear(x, y, radius + 5):
            continue
        vegetation.ellipse((x - radius, y - radius * 0.25, x + radius * 1.2,
                            y + radius * 0.65), (33, 72, 47, 48))
        for _ in range(9):
            at_x = x + rng.uniform(-radius * 0.7, radius * 0.7)
            at_y = y + rng.uniform(-radius * 0.3, radius * 0.3)
            leaf = rng.uniform(radius * 0.32, radius * 0.57)
            vegetation.ellipse((at_x - leaf, at_y - leaf * 0.6,
                                 at_x + leaf, at_y + leaf * 0.6),
                                rng.choice(((85, 128, 82, 255), (110, 148, 93, 255),
                                            (126, 158, 100, 255))))
    vegetation.image.putalpha(ImageChops.multiply(vegetation.image.getchannel("A"), board_mask))
    canvas.alpha_composite(vegetation.image)

    # 阵位为浅浮雕基座；不使用加号、白圆环或深色孔洞。
    base_shadow = _Painter(scale)
    for x, y in _SLOTS:
        base_shadow.ellipse((x - 30, y - 9, x + 33, y + 20), (42, 68, 42, 51))
    canvas.alpha_composite(base_shadow.blur(2.3))
    bases = _Painter(scale)
    for index, (x, y) in enumerate(_SLOTS):
        bases.ellipse((x - 27, y - 10, x + 27, y + 15), (103, 123, 88, 255))
        bases.ellipse((x - 27, y - 13, x + 27, y + 10), (143, 155, 112, 255))
        bases.ellipse((x - 23, y - 10, x + 23, y + 7), (133, 147, 105, 255))
        bases.line(((x - 23, y - 6), (x - 17, y - 10), (x - 5, y - 12),
                    (x + 11, y - 11)), (179, 184, 132, 185), 1)
        if index % 4 == 1:
            # 少量木质嵌片，与石质阵位保持相同亮度。
            bases.line(((x - 17, y - 3), (x + 18, y - 3)), (115, 130, 91, 110), 0.8)
            bases.line(((x - 18, y + 2), (x + 16, y + 2)), (115, 130, 91, 95), 0.8)
        else:
            bases.line(((x + 12, y + 5), (x + 17, y + 2), (x + 20, y + 4)),
                       (110, 129, 91, 110), 0.7)
    canvas.alpha_composite(bases.image)
    canvas.alpha_composite(_castle(scale))

    # 沙盘阴影不进入 HUD 保留区，保证调用方可直接覆盖上、下栏。
    top = Image.new("RGBA", (canvas.width, 120 * scale), _BACKGROUND)
    bottom = Image.new("RGBA", (canvas.width, 200 * scale), _BACKGROUND)
    canvas.paste(top, (0, 0))
    canvas.paste(bottom, (0, 880 * scale))
    return canvas
