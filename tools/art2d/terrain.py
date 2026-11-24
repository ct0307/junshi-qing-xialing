"""黄巾惊野的原创二维青绿郊野；导入模块不读写图像或正式数据。"""

from __future__ import annotations

import importlib.util
import math
from pathlib import Path
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter


__all__ = ["make_terrain"]
_ROOT = Path(__file__).resolve().parents[2]
_SPEC = importlib.util.spec_from_file_location(
    "_terrain_v6_drawing_tools", _ROOT / "docs/ui/terrain_art_v2.py")
_BASE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_BASE)
_SCALE = 2
_SIZE = (1920, 1080)
_TEAL = (31, 76, 68, 255)


def _soften(image, radius):
    # 预乘Alpha后再柔化，避免透明黑色混入边界，生成脏暗圈。
    return image.convert("RGBa").filter(ImageFilter.GaussianBlur(radius * _SCALE)).convert("RGBA")


class _Art(_BASE._Painter):
    """沿用现有超采样笔刷，局部画布避免为每棵小树申请整幅图。"""

    def __init__(self, bounds=(0, 0, 1920, 1080)):
        self.scale = _SCALE
        self.origin = bounds[:2]
        self.size = (round((bounds[2] - bounds[0]) * self.scale),
                     round((bounds[3] - bounds[1]) * self.scale))
        self.image = Image.new("RGBA", self.size)
        self.draw = ImageDraw.Draw(self.image)

    def points(self, points):
        return [(round((x - self.origin[0]) * self.scale),
                 round((y - self.origin[1]) * self.scale)) for x, y in points]

    def box(self, bounds):
        return tuple(round((value - self.origin[index % 2]) * self.scale)
                     for index, value in enumerate(bounds))

    def path(self, commands, fill=None, outline=None, width=1):
        # 贝塞尔仅用于手绘景物，正式道路从不进入此方法。
        points = []
        for command in commands:
            if command[0] in ("M", "L"):
                points.append(command[1:3])
            elif command[0] == "C":
                start = points[-1]
                for step in range(1, 19):
                    t = step / 18
                    points.append(tuple((1 - t) ** 3 * start[i]
                                        + 3 * (1 - t) ** 2 * t * command[i + 1]
                                        + 3 * (1 - t) * t * t * command[i + 3]
                                        + t ** 3 * command[i + 5] for i in (0, 1)))
        if fill is not None:
            self.polygon(points, fill)
        if outline is not None:
            self.line(points, outline, width)

    def curve(self, start, first, second, end, fill, width=1):
        self.path([("M", *start), ("C", *first, *second, *end)],
                  outline=fill, width=width)


class _Local:
    """小景的局部手绘坐标；建筑纵向略补偿正式布局的纵缩。"""

    def __init__(self, painter, x, y, scale=1, vertical=1, angle=0):
        self.painter = painter
        self.x, self.y = x, y
        self.sx, self.sy = scale, scale * vertical
        self.angle = math.radians(angle)

    def point(self, point):
        local_x, local_y = point[0] * self.sx, point[1] * self.sy
        return (self.x + local_x * math.cos(self.angle) - local_y * math.sin(self.angle),
                self.y + local_x * math.sin(self.angle) + local_y * math.cos(self.angle))

    def line(self, points, fill, width=1):
        self.painter.line([self.point(p) for p in points], fill, width * self.sx)

    def polygon(self, points, fill):
        self.painter.polygon([self.point(p) for p in points], fill)

    def ellipse(self, bounds, fill, outline=None, width=1):
        a, b = self.point(bounds[:2]), self.point(bounds[2:])
        self.painter.ellipse((*a, *b), fill, outline, width * self.sx)

    def path(self, commands, fill=None, outline=None, width=1):
        transformed = []
        for command in commands:
            values = []
            for index in range(1, len(command), 2):
                values.extend(self.point(command[index:index + 2]))
            transformed.append((command[0], *values))
        self.painter.path(transformed, fill, outline, width * self.sx)

    def curve(self, start, first, second, end, fill, width=1):
        self.path([("M", *start), ("C", *first, *second, *end)],
                  outline=fill, width=width)


def _overlap(first, second):
    return (first[0] < second[2] and first[2] > second[0]
            and first[1] < second[3] and first[3] > second[1])


def _segment_hits(bounds, a, b):
    """检查完整投影矩形，不以树根或房屋中心代替可见范围。"""
    first, last = 0.0, 1.0
    dx, dy = b[0] - a[0], b[1] - a[1]
    for direction, distance in ((-dx, a[0] - bounds[0]), (dx, bounds[2] - a[0]),
                                (-dy, a[1] - bounds[1]), (dy, bounds[3] - a[1])):
        if abs(direction) < 1e-9:
            if distance < 0:
                return False
            continue
        at = distance / direction
        if direction < 0:
            first = max(first, at)
        else:
            last = min(last, at)
        if first > last:
            return False
    return True


class _Scene:
    def __init__(self, canvas, level):
        self.canvas = canvas
        self.routes = [[(float(p["X"]), float(p["Y"])) for p in route["Points"]]
                       for route in level["Routes"]]
        self.slots = [(float(slot["Position"]["X"]), float(slot["Position"]["Y"]))
                      for slot in level["Slots"]]
        self.placed, self.rejected = [], []

    def clear(self, bounds, allow_road=False, occupied=True):
        if bounds[0] < 39 or bounds[1] < 145 or bounds[2] > 1881 or bounds[3] > 841:
            return False, "可见裁剪边界"
        for index, (x, y) in enumerate(self.slots):
            if _overlap(bounds, (x - 55, y - 42, x + 55, y + 42)):
                return False, "阵位" + str(index + 1)
        if not allow_road:
            expanded = (bounds[0] - 43, bounds[1] - 43, bounds[2] + 43, bounds[3] + 43)
            for route in self.routes:
                for a, b in zip(route, route[1:]):
                    if _segment_hits(expanded, a, b):
                        return False, "道路"
        if occupied:
            for item in self.placed:
                if _overlap(bounds, item["Bounds"]):
                    return False, item["Name"]
        return True, ""

    def place(self, name, x, y, drawer, scale=1, vertical=1, allow_road=False):
        # 先画局部，再读取真实Alpha包围盒，阴影、檐角和叶尖都纳入验收。
        box = (math.floor(x - 180 * scale), math.floor(y - 190 * scale * vertical),
               math.ceil(x + 180 * scale), math.ceil(y + 90 * scale * vertical))
        painter = _Art(box)
        drawer(_Local(painter, x, y, scale, vertical))
        drawn = painter.image.getbbox()
        if drawn is None:
            return False
        bounds = tuple((box[i % 2] + drawn[i] / _SCALE) for i in range(4))
        clear, reason = self.clear(bounds, allow_road)
        if not clear:
            self.rejected.append({"Name": name, "Bounds": list(bounds), "Reason": reason})
            return False
        self.canvas.alpha_composite(painter.image, (box[0] * _SCALE, box[1] * _SCALE))
        item = {"Name": name, "Bounds": list(bounds), "AllowRoad": allow_road}
        if allow_road:
            item["Role"] = "terminal_gate"
        self.placed.append(item)
        return True


def _ground(level):
    """鲜明草绿底色叠手绘低频斑块；阵位与正式道路留白。"""
    low = Image.new("RGB", (240, 135))
    pixels = low.load()
    for y in range(135):
        for x in range(240):
            light = 2.3 * math.sin(x / 39 + y / 47) + 1.5 * math.cos(y / 17)
            light += 1.2 * math.sin((x - y) / 29)
            warmth = 2.5 * math.exp(-((x - 115) / 95) ** 2 - ((y - 63) / 50) ** 2)
            pixels[x, y] = (round(126 + light + warmth), round(176 + light), round(89 + light * .62))
    ground = low.resize((3840, 2160), Image.Resampling.BICUBIC).convert("RGBA")
    edge = _Art()
    edge.rounded((25, 134, 1895, 852), 34, "white")
    ground.putalpha(edge.image.getchannel("A"))
    canvas = Image.new("RGBA", (3840, 2160), _TEAL)
    canvas.alpha_composite(ground)
    washes, soft_washes = _Art(), _Art()
    # 深草、晒亮的草皮与裸土错落叠放，边缘保持手绘而不铺成整片滤镜。
    patches = ((316, 405, 104, 29, (87, 151, 73, 108)),
               (650, 405, 129, 35, (111, 163, 77, 93)),
               (817, 420, 106, 25, (101, 158, 74, 101)),
               (1070, 405, 112, 35, (101, 158, 74, 101)),
               (1210, 436, 88, 16, (153, 176, 78, 86)),
               (1480, 480, 88, 20, (102, 160, 75, 96)),
               (1645, 190, 88, 34, (97, 157, 77, 88)),
               (1750, 785, 83, 48, (99, 159, 78, 95)),
               (390, 642, 22, 9, (162, 187, 98, 59)),
               (600, 642, 59, 9, (164, 188, 93, 57)),
               (295, 438, 34, 12, (177, 184, 98, 64)),
               (760, 438, 42, 15, (86, 145, 70, 78)),
               (1182, 425, 32, 13, (167, 183, 91, 57)),
               (1372, 366, 42, 11, (96, 148, 70, 70)),
               (470, 521, 57, 16, (157, 177, 91, 67)),
               (1040, 518, 69, 20, (91, 149, 69, 72)),
               (1325, 517, 59, 18, (169, 184, 93, 63)))
    for index, (x, y, rx, ry, color) in enumerate(patches):
        brush = soft_washes if index in (0, 6) else washes
        brush.path([("M", x - rx, y - ry * .15),
                     ("C", x - rx * .93, y - ry * .74, x - rx * .35, y - ry, x, y - ry * .78),
                     ("C", x + rx * .44, y - ry, x + rx, y - ry * .38, x + rx * .85, y + ry * .14),
                     ("C", x + rx * .69, y + ry * .72, x + rx * .16, y + ry * .50, x - rx * .18, y + ry * .71),
                     ("C", x - rx * .67, y + ry * .62, x - rx, y + ry * .29, x - rx, y - ry * .15)], color)
        if index in (0, 3, 6, 9, 11, 13):
            brush.curve((x - rx * .48, y + ry * .12), (x - rx * .13, y + ry * .26),
                        (x + rx * .29, y + ry * .20), (x + rx * .55, y + ry * .07),
                        (49, 111, 51, 112), .8)
    wash = _soften(soft_washes.image, 2.0)
    wash.alpha_composite(_soften(washes.image, .35))
    # 低频色块在阵位保护区和道路外沿停止，避免调色盖住玩法标记。
    clear = Image.new("L", (3840, 2160), 255)
    clear_draw = ImageDraw.Draw(clear)
    for slot in level["Slots"]:
        x, y = slot["Position"]["X"], slot["Position"]["Y"]
        clear_draw.rectangle((round((x - 59) * _SCALE), round((y - 46) * _SCALE),
                              round((x + 59) * _SCALE), round((y + 46) * _SCALE)), fill=0)
    for route in level["Routes"]:
        points = [(round(point["X"] * _SCALE), round(point["Y"] * _SCALE)) for point in route["Points"]]
        ImageDraw.Draw(clear).line(points, 0, 100 * _SCALE, joint="curve")
    wash.putalpha(ImageChops.multiply(wash.getchannel("A"), clear))
    wash.putalpha(ImageChops.multiply(wash.getchannel("A"), edge.image.getchannel("A")))
    canvas.alpha_composite(wash)
    # 单层浅草缘仅区分郊野与UI底色，不画立面、厚边或悬浮阴影。
    rim = _Art()
    rim.line([(51, 145), (210, 143), (402, 145), (600, 143), (808, 145)], (222, 224, 169, 92), 1.2)
    rim.line([(1118, 842), (1335, 841), (1548, 843), (1824, 841)], (208, 219, 163, 84), 1.1)
    canvas.alpha_composite(rim.image)
    return canvas


def _road(canvas, routes):
    """正式70点直接连接，不削角、不再次拟合或重采样中心线。"""
    size = canvas.size
    road_mask = Image.new("L", size)
    shoulder_mask = Image.new("L", size)
    deep_edge_mask = Image.new("L", size)
    for target, width in ((deep_edge_mask, 96), (shoulder_mask, 91), (road_mask, 86)):
        draw = ImageDraw.Draw(target)
        for route in routes:
            points = [(round(x * _SCALE), round(y * _SCALE)) for x, y in route]
            draw.line(points, 255, round(width * _SCALE), joint="curve")
            radius = width * _SCALE / 2
            for x, y in (points[0], points[-1]):
                draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=255)
    deep_edge = Image.new("RGBA", size, (91, 57, 35, 255))
    deep_edge.putalpha(deep_edge_mask.point(lambda value: round(value * .86)))
    canvas.alpha_composite(deep_edge)
    shoulder = Image.new("RGBA", size, (146, 86, 48, 255))
    shoulder.putalpha(shoulder_mask.point(lambda value: round(value * .90)))
    canvas.alpha_composite(shoulder)
    low = Image.new("RGB", (240, 135))
    pixels = low.load()
    for y in range(135):
        for x in range(240):
            variation = 2.3 * math.sin(x / 27 + y / 18) + 1.4 * math.cos(y / 8)
            pixels[x, y] = (round(205 + variation), round(132 + variation), round(66 + variation * .65))
    road = low.resize(size, Image.Resampling.BICUBIC).convert("RGBA")
    road.putalpha(road_mask)
    canvas.alpha_composite(road)
    detail = _Art()
    # 草齿只切入路缘一至两像素，不改变正式折线或中心通行带。
    for route in routes:
        for segment, (a, b) in enumerate(zip(route, route[1:])):
            length = math.dist(a, b)
            if length < 20:
                continue
            tangent = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
            normal = (-tangent[1], tangent[0])
            for step in range(max(1, int(length / 43))):
                distance = 15 + step * 43 + 7 * math.sin(step * 1.7 + segment)
                if distance > length - 5:
                    continue
                x, y = a[0] + tangent[0] * distance, a[1] + tangent[1] * distance
                for side in (-1, 1):
                    def edge(at, depth):
                        return (x + tangent[0] * at + normal[0] * depth * side,
                                y + tangent[1] * at + normal[1] * depth * side)
                    detail.polygon([edge(-3.5, 44), edge(.8, 41.4), edge(5.5, 43.5)], (156, 183, 135, 195))
    # 小块磨损有疏密变化，替代整条平行车辙与砂点。
    wear = ((219, 277, 21, 5), (374, 325, 29, 4), (628, 283, 31, 5),
            (904, 321, 23, 4), (1138, 279, 32, 5), (1372, 327, 18, 4),
            (1666, 468, 16, 4), (1725, 544, 15, 5), (1489, 718, 27, 5),
            (1240, 677, 21, 4), (983, 723, 28, 5), (711, 678, 26, 5), (435, 725, 23, 4))
    for index, (x, y, rx, ry) in enumerate(wear):
        detail.path([("M", x - rx, y), ("C", x - rx * .5, y - ry, x + rx * .1, y - ry * .6, x + rx, y - 1),
                     ("C", x + rx * .6, y + ry, x - rx * .3, y + ry * .6, x - rx, y)],
                    (122, 75, 42, 82) if index % 3 else (247, 193, 112, 96))
        if index % 2 == 0:
            detail.polygon([(x + rx * .4, y + 3), (x + rx * .4 + 3, y + 1.3),
                            (x + rx * .4 + 7, y + 2), (x + rx * .4 + 5, y + 4)], (112, 72, 43, 136))
    detail.image.putalpha(ImageChops.multiply(detail.image.getchannel("A"), road_mask))
    canvas.alpha_composite(detail.image)


def _village_ground(canvas, slots):
    """只在营寨脚边铺薄院土，短脚径止于院口，不跨场拖成浅色长影。"""
    yard = _Art()
    yard.path([("M", 65, 484), ("C", 81, 466, 111, 465, 137, 478),
               ("C", 162, 488, 183, 473, 207, 482),
               ("C", 234, 489, 250, 501, 252, 513),
               ("C", 232, 527, 207, 529, 181, 520),
               ("C", 148, 533, 108, 530, 81, 517),
               ("C", 62, 508, 56, 497, 65, 484)], (193, 181, 137, 54))
    yard.path([("M", 94, 489), ("C", 119, 482, 139, 491, 158, 499),
               ("C", 181, 504, 199, 494, 221, 505),
               ("C", 203, 517, 174, 514, 153, 519),
               ("C", 127, 520, 105, 513, 94, 489)], (208, 199, 153, 29))
    yard.curve((71, 486), (83, 475), (97, 473), (111, 477), (146, 160, 104, 58), 1.0)
    yard.curve((165, 511), (168, 520), (173, 529), (178, 537), (163, 136, 99, 65), 2.2)
    layer = _soften(yard.image, .35)
    alpha = layer.getchannel("A")
    cut = ImageDraw.Draw(alpha)
    for x, y in slots:
        cut.rectangle((round((x - 55) * _SCALE), round((y - 42) * _SCALE),
                       round((x + 55) * _SCALE), round((y + 42) * _SCALE)), fill=0)
    layer.putalpha(alpha)
    canvas.alpha_composite(layer)


def _house(p, variant=0):
    """细洁青瓦与粉墙，以线面表达手绘屋檐，不模拟塑料高光。"""
    p.ellipse((-45, 2, 61, 24), (68, 104, 79, 31))
    p.polygon([(-36, -29), (9, -16), (9, 19), (-36, 6)], "#C7A58B" if variant == 1 else "#D9CFAE")
    p.polygon([(9, -16), (44, -31), (44, 5), (9, 19)], "#E2C2A1" if variant == 1 else "#EEE2BF")
    p.polygon([(-36, 6), (9, 19), (44, 5), (44, 10), (9, 24), (-36, 11)], "#ABA98B")
    p.line([(-34, -24), (-34, 6), (8, 18), (43, 5)], "#B5AE8D", .9)
    # 左侧前墙的木门与右侧短窗，窗格在缩图后仍成整洁色块。
    p.polygon([(-25, -13), (-10, -9), (-10, 12), (-25, 8)], "#986F55" if variant == 1 else "#7D8166")
    p.line([(-22, -11), (-22, 7)], "#9F9B75", .9)
    p.line([(-13, -8), (-13, 10)], "#5F715E", .8)
    p.ellipse((-14, 0, -11.7, 2.4), "#C3AA6F")
    p.polygon([(20, -13), (34, -19), (34, -6), (20, 0)], "#8EAA9B")
    p.line([(26, -15.5), (26, -2.5)], "#D8D2AC", 1.1)
    p.line([(20, -6), (34, -12)], "#D8D2AC", 1.0)
    # 屋顶两侧稍翘，正面瓦坡用少量分层曲线维持轻薄。
    p.path([("M", -55, -35), ("C", -44, -39, -25, -61, -9, -76),
            ("L", 47, -59), ("C", 43, -45, 21, -24, 7, -14),
            ("C", -16, -20, -41, -23, -55, -35)], "#527F77", "#527A70", 1.0)
    p.path([("M", -9, -76), ("L", 47, -59), ("C", 51, -48, 57, -42, 66, -42),
            ("C", 46, -35, 24, -22, 7, -14), ("C", 24, -29, 41, -48, 47, -59)], "#729C8A")
    for row in range(1, 5):
        t = row / 5
        left = (-9 + (-55 + 9) * t, -76 + 41 * t)
        right = (47 + (7 - 47) * t, -59 + 45 * t)
        p.curve(left, (left[0] + 15, left[1] + 7),
                (right[0] - 15, right[1] - 4), right, "#89AD99", .7)
    for column in range(1, 8):
        t = column / 8
        top = (-9 + 56 * t, -76 + 17 * t)
        bottom = (-55 + 62 * t, -35 + 21 * t)
        p.curve(top, (top[0] - 10, top[1] + 16),
                (bottom[0] + 9, bottom[1] - 9), bottom, (61, 104, 94, 165), .65)
    p.curve((-56, -36), (-43, -22), (-13, -22), (8, -14), "#A9C5AA", 1.7)
    p.curve((7, -14), (29, -24), (52, -35), (66, -42), "#B1CAB0", 1.5)
    p.curve((-12, -79), (1, -75), (35, -63), (49, -62), "#B9CAB0", 2.2)
    p.curve((-13, -80), (-10, -76), (-8, -75), (-5, -76), "#456F66", .9)
    p.polygon([(-25, 10), (-8, 15), (-5, 19), (-25, 14), (-30, 12)], "#E9DCB5")
    if variant == 1:
        p.polygon([(-38, -16), (-47, -18), (-47, 7), (-38, 10)], "#D9CBA4")
        p.line([(-45, -13), (-45, 6)], "#BBAE83", 1)
    elif variant == 2:
        for x in (-29, -18, -7, 4, 15, 26, 37):
            p.line([(x, -13), (x, 9 - x * .06)], (142, 130, 94, 95), .8)


def _tent(p, x, y, scale, front, side, seam):
    q = _Local(p.painter, *p.point((x, y)), p.sx * scale, scale)
    dark = "#49392D"
    q.ellipse((-47, -1, 51, 18), (48, 69, 42, 63))
    q.line([(-44, -6), (-72, 4)], dark, 1.5)
    q.line([(43, -5), (68, 4)], dark, 1.5)
    q.line([(-72, 4), (-76, 6)], "#C7AB72", 1.2)
    q.line([(68, 4), (72, 6)], "#C7AB72", 1.2)
    q.path([("M", -44, 2), ("L", -3, -70), ("L", 45, -8), ("L", 39, 9), ("L", -42, 11)], side, dark, 2)
    q.path([("M", -3, -70), ("L", 1, -78), ("L", 50, -17), ("L", 45, -8)], front, dark, 1.8)
    q.path([("M", -3, -70), ("L", 1, -78), ("L", -44, -3), ("L", -44, 2)], seam, dark, 1.6)
    q.path([("M", -4, -8), ("L", -2, -43), ("C", 5, -50, 17, -36, 23, -11),
            ("L", 18, 8), ("L", -4, 9)], "#453B2D", "#D4B782", 1.1)
    q.line([(-3, -70), (-3, -8)], "#E1C895", .9)
    q.line([(-39, -5), (-4, 5), (41, -6)], "#E6CF9D", 1.2)
    q.line([(1, -76), (3, -72)], "#F1D8A2", 1.1)


def _camp(p):
    """西侧起点营寨：帐篷、尖木栅与暖金旗形成完整聚落。"""
    dark = "#493A2B"
    wood = "#795332"
    light_wood = "#B68A51"
    p.path([("M", -111, 26), ("C", -103, 7, -66, 8, -38, 18),
            ("C", -9, 7, 23, 8, 44, 19), ("C", 73, 6, 106, 13, 113, 32),
            ("C", 85, 51, 26, 52, -19, 45), ("C", -63, 53, -97, 45, -111, 26)],
            "#C8AB72", "#65503A", 1.2)

    # 后栅与两侧栅栏围出向南开的营门，木桩高度错落。
    for index, x in enumerate((-101, -82, -62, -42, -22, 0, 22, 44, 66, 87, 106)):
        top = -57 - (index * 13 % 17)
        lean = 2 if index % 2 else -1
        p.polygon([(x - 4, 18), (x - 4, top + 7), (x + lean, top),
                   (x + 5, top + 8), (x + 6, 21)], wood)
        p.line([(x - 2, 13), (x - 2, top + 10)], "#D0A36A", .9)
        p.line([(x - 5, top + 8), (x + lean, top)], dark, 1.3)
    p.line([(-100, -35), (-66, -38), (-31, -32), (4, -35), (40, -31), (77, -36), (105, -31)], dark, 2.5)
    p.line([(-99, -14), (-63, -17), (-28, -11), (10, -14), (47, -10), (79, -15), (104, -10)], dark, 3.0)
    p.line([(-99, -33), (-66, -36), (-31, -30), (4, -33), (40, -29), (77, -34), (104, -29)], light_wood, 1.0)
    for x, top in ((-105, -38), (-96, -60), (105, -54)):
        p.line([(x, -4), (x, top)], wood, 4.0)
        p.line([(x - 1, -8), (x - 1, top + 5)], "#D1A36D", .9)
    for x in (-103, -82, -62, 65, 86, 105):
        p.polygon([(x - 5, 17), (x - 4, -6), (x + 4, -8), (x + 6, 19)], wood)
        p.line([(x - 3, 12), (x - 3, -4)], "#D0A36A", .8)
    p.line([(-101, -4), (-72, -7), (-60, -5)], dark, 2.8)
    p.line([(59, -5), (80, -7), (105, -4)], dark, 2.8)
    p.line([(-100, -2), (-72, -5), (-60, -3)], light_wood, 1.0)
    p.line([(59, -3), (80, -5), (104, -2)], light_wood, 1.0)

    _tent(p, -39, 0, .88, "#B94F35", "#854532", "#D89255")
    _tent(p, 37, -8, .66, "#C57D3D", "#8E5934", "#E3B56D")
    _tent(p, -5, 18, .49, "#4F7960", "#3F624D", "#91A76B")

    # 木杆、绳结和布旗都落在围栏内，强调营地而不是散落建筑。
    p.line([(75, -67), (77, -116)], dark, 2.5)
    p.line([(76, -66), (78, -115)], "#B88D55", .8)
    p.path([("M", 78, -113), ("C", 93, -111, 99, -108, 109, -112),
            ("L", 104, -101), ("L", 110, -91), ("C", 98, -90, 89, -98, 78, -97)],
            "#A74231", dark, 1.4)
    p.line([(81, -108), (92, -105), (104, -107)], "#E2BC72", 1.1)
    p.ellipse((72, -70, 81, -62), "#D2B06D", dark, .8)


def _gate(p):
    """横向单檐关墙；拱门中心与西南正式路线终点对齐。"""
    p.ellipse((-105, 5, 60, 27), (69, 103, 80, 34))
    dark = "#45392C"
    # 两段短墙留出宽阔门洞，来路在终点处仍能从拱下穿过。
    p.path([("M", -101, -12), ("L", -79, -21), ("L", -37, -10),
            ("L", -37, 13), ("L", -79, 24), ("L", -101, 16), ("L", -101, -12)],
           "#C3B88C", dark, 1.8)
    p.path([("M", 37, -10), ("L", 53, -17), ("L", 61, -14),
            ("L", 61, 11), ("L", 37, 17), ("L", 37, -10)], "#9BA77A", dark, 1.8)
    for x, y in ((-91, -16), (-77, -22), (-62, -18), (-47, -14), (46, -14), (56, -18)):
        p.path([("M", x - 4, y + 4), ("L", x - 4, y - 7), ("L", x, y - 11),
                ("L", x + 4, y - 7), ("L", x + 4, y + 5)], "#D8B56F", dark, .8)
    p.line([(-97, 3), (-80, 9), (-39, -2)], (226, 215, 174, 145), .9)
    p.line([(39, 2), (53, -2), (59, -4)], (218, 219, 179, 145), .8)

    # 单片宽檐压住城墙，顶脊和两侧瓦坡合成一层屋顶。
    p.path([("M", -103, -16), ("C", -79, -20, -46, -48, -23, -63),
            ("L", 53, -37), ("C", 55, -29, 58, -23, 60, -20),
            ("C", 39, -13, 12, -7, -7, -8),
            ("C", -40, -10, -79, -9, -103, -16)], "#3F7664", dark, 2.1)
    for row in range(1, 4):
        t = row / 4
        a = (-23 - 80 * t, -63 + 47 * t)
        b = (53 - 60 * t, -37 + 29 * t)
        p.curve(a, (a[0] + 16, a[1] + 8), (b[0] - 13, b[1] - 4), b, "#96B69B", .8)
    for column in range(1, 7):
        t = column / 7
        p.line([(-23 + 76 * t, -63 + 26 * t), (-103 + 96 * t, -16 + 8 * t)],
               (57, 105, 90, 145), .65)
    p.curve((-104, -17), (-72, -5), (-33, -8), (-7, -8), "#B2C6A4", 1.7)
    p.curve((-25, -65), (-4, -59), (30, -43), (54, -39), "#B9C9AA", 2.0)

    # 门拱压在檐口之下，空心区域保留道路色，避免读成封闭民居。
    p.path([("M", -36, 21), ("L", -36, -13),
            ("C", -36, -42, -20, -49, 0, -49),
            ("C", 20, -49, 36, -42, 36, -13), ("L", 36, 21)],
           outline=dark, width=4.0)
    p.path([("M", -30, 19), ("L", -30, -12),
            ("C", -30, -34, -16, -41, 0, -41),
            ("C", 16, -41, 30, -34, 30, -12), ("L", 30, 19)],
           outline="#E1D19B", width=1.6)
    p.polygon([(-28, -2), (-21, -5), (-21, 16), (-28, 17)], "#875B39")
    p.polygon([(21, -5), (28, -2), (28, 17), (21, 16)], "#875B39")
    p.line([(-26, 0), (-26, 14)], "#D8B56F", 1.0)
    p.line([(26, 0), (26, 14)], "#D8B56F", 1.0)
    p.line([(-32, 20), (32, 20)], "#E1D19B", 1.4)


def _tree(p, tint=0):
    """不规则的叠叶树冠，保留水洗色面与少数叶脉，避免圆球模型感。"""
    palettes = (("#4E8975", "#6EA080", "#98BA8C"),
                ("#5A9279", "#80AC84", "#B1C797"),
                ("#6B9878", "#97B58A", "#CCD19A"))
    dark, middle, light = palettes[tint % 3]
    p.ellipse((-22, -2, 31, 11), (65, 109, 83, 31))
    p.path([("M", -4, -3), ("C", -3, -22, -6, -36, -2, -50),
            ("L", 3, -50), ("C", -1, -29, 3, -17, 4, -2)], "#7E8565")
    p.line([(0, -20), (-12, -39)], "#8B8C69", 2)
    p.line([(1, -27), (13, -42)], "#777E5E", 1.7)
    p.path([("M", -30, -42), ("C", -37, -56, -24, -68, -15, -67),
            ("C", -16, -80, 0, -87, 11, -76), ("C", 28, -79, 38, -65, 31, -54),
            ("C", 45, -42, 30, -28, 17, -30), ("C", 5, -19, -10, -26, -14, -31),
            ("C", -29, -28, -39, -33, -30, -42)], dark, "#344C34", 2.0)
    p.path([("M", -29, -49), ("C", -30, -62, -18, -68, -9, -63),
            ("C", -9, -76, 5, -80, 14, -69), ("C", 27, -72, 34, -61, 27, -49),
            ("C", 19, -38, 8, -42, 3, -37), ("C", -10, -31, -25, -37, -29, -49)],
            middle, "#46623D", 1.2)
    p.path([("M", -23, -56), ("C", -22, -66, -11, -68, -6, -63),
            ("C", 0, -75, 11, -74, 17, -65), ("C", 24, -65, 27, -57, 20, -51),
            ("C", 8, -47, 1, -51, -5, -49), ("C", -15, -45, -22, -48, -23, -56)],
            light, "#6A8B51", .9)
    for x, y, k in ((-17, -57, 1), (2, -65, 1.1), (13, -52, .8), (-10, -42, .75), (23, -39, .7)):
        p.curve((x - 3 * k, y), (x, y - 4 * k), (x + 3 * k, y - 2 * k),
                (x + 4 * k, y + 1 * k), (206, 220, 164, 135), .8)


def _grove(p, tint=0):
    for x, y, scale, color in ((-19, -7, .76, tint), (18, -2, .93, tint + 1), (0, 10, .56, tint)):
        _tree(_Local(p.painter, *p.point((x, y)), p.sx * scale, p.sy / p.sx), color)
    for x, y in ((-20, 8), (28, 11), (8, 18)):
        _tuft(_Local(p.painter, *p.point((x, y)), p.sx * .55, p.sy / p.sx), False)


def _rock(p):
    p.ellipse((-34, 1, 42, 18), (71, 111, 87, 26))
    p.path([("M", -29, 4), ("L", -24, -17), ("L", -6, -28), ("L", 19, -24),
            ("L", 29, -7), ("L", 22, 9), ("L", -8, 12)], "#94AE77", "#394A32", 1.8)
    p.polygon([(-24, -17), (-6, -28), (19, -24), (9, -9), (-17, -5)], "#D8D19A")
    p.polygon([(9, -9), (19, -24), (29, -7), (22, 9), (5, 11)], "#A8C08A")
    p.line([(-24, -17), (-6, -28), (19, -24)], "#F0E6BA", 1.3)
    p.line([(9, -9), (19, -24), (29, -7), (22, 9)], "#5A7446", 1.1)
    p.line([(-10, 2), (-2, -2), (7, 1)], "#536C3F", 1.1)
    p.ellipse((-36, 8, -18, 19), "#AFBDA3")
    p.ellipse((20, 11, 39, 21), "#C5CEAF")
    p.curve((-31, 8), (-26, 3), (-19, 3), (-17, 7), "#92AC84", 3)


def _tuft(p, flower=True):
    p.ellipse((-14, 0, 17, 7), (104, 151, 103, 56))
    for x, y, bend in ((-10, 0, -5), (-4, 1, -2), (1, 2, 3), (8, 2, 5), (12, 3, 2)):
        p.curve((x, y), (x + bend * .2, y - 4), (x + bend, y - 9),
                (x + bend, y - 13 - x % 3), "#425F36", 1.25)
        p.line([(x + .6, y - 2), (x + bend * .6, y - 8)], "#ACCC6D", .8)
    if flower:
        for x, y in ((-5, -9), (7, -12), (13, -6)):
            p.line([(x, 1), (x, y)], "#819F70", .7)
            for dx, dy in ((-1.8, 0), (1.8, 0), (0, -1.7), (0, 1.7)):
                p.ellipse((x + dx - 1.7, y + dy - 1.5, x + dx + 1.7, y + dy + 1.5), "#F2E8BB")
            p.ellipse((x - .8, y - .8, x + .8, y + .8), "#D3AE63")


def _bamboo(p):
    for x, lean, height in ((-13, -3, 68), (-4, 6, 82), (7, 8, 72), (17, 4, 60)):
        p.line([(x, 1), (x + lean, -height)], "#6F956C", 2.0)
        p.line([(x - .7, 0), (x + lean - .7, -height)], "#B1C48B", .7)
        for segment in range(1, 5):
            y = -height * segment / 5
            at = x + lean * segment / 5
            p.line([(at - 2, y), (at + 2, y)], "#D3D6A0", .8)
        for y, side in ((-height + 12, -1), (-height + 24, 1), (-height + 39, -1)):
            at = x + lean * abs(y) / height
            p.curve((at, y), (at + side * 8, y - 4), (at + side * 17, y - 7),
                    (at + side * 24, y - 6), "#638E6A", .8)
            for i in range(3):
                xx, yy = at + side * (6 + i * 6), y - 2 - i
                p.path([("M", xx, yy), ("C", xx + side * 2, yy - 8,
                        xx + side * 7, yy - 10, xx + side * 9, yy - 12),
                        ("C", xx + side * 10, yy - 6, xx + side * 5, yy - 2, xx, yy)], "#72A174")
    _tuft(p, False)


def _paddy(p, variant=0):
    base, light, dark = "#D0BF7E", "#E2CF8E", "#BCB47A"
    p.path([("M", -81, -23), ("C", -55, -35, -10, -32, 30, -34),
            ("L", 82, -19), ("L", 75, 25), ("C", 32, 31, -31, 33, -78, 18)],
            base, "#665637", 1.8)
    p.path([("M", -77, -20), ("C", -35, -27, 10, -26, 40, -25),
            ("L", 76, -16), ("L", 72, 18), ("C", 27, 27, -32, 25, -73, 15)],
            "#E0CD83", "#A38E54", .8)
    # 田界靠色差与一两段埂线提示，不加包围四边的粗框。
    p.curve((-78, -23), (-55, -29), (-19, -30), (5, -30), "#DCD39A", .8)
    p.curve((74, 21), (52, 27), (27, 29), (11, 28), dark, .8)
    rows = 5 if variant == 0 else 4
    for row in range(rows):
        y = -19 + row * (37 / max(1, rows - 1))
        reach = 67 - abs(row - (rows - 1) / 2) * 3
        p.curve((-reach, y + 3), (-23, y + 7), (24, y - 2), (reach, y + 3),
                (171, 162, 103, 80), .65)
        for clump in range(9 if variant == 0 else 7):
            x = -60 + clump * (14 if variant == 0 else 19) + (row % 2) * 3
            yy = y + math.sin(clump * .8 + row) * 1.8
            height = 8 + (clump + row) % 3
            p.line([(x, yy + 2), (x + 1, yy - height)], "#AAAB70", .6)
            p.line([(x, yy), (x - 4, yy - 6)], "#C0B673", .6)
            p.line([(x, yy - 2), (x + 4, yy - 8)], light, .7)
            for grain in range(3):
                at_y = yy - height + grain * 2.0
                p.line([(x + .8, at_y), (x + 3.5, at_y - 1.2)], "#EFDB9E", 1.2)
                p.line([(x + .8, at_y + .6), (x - 1.6, at_y - .4)], "#DCC789", 1)
    if variant == 2:
        # 收割田仅留一段短篱，不沿田界整齐铺满。
        for x, y in ((32, 26), (48, 24), (65, 22)):
            p.line([(x, y - 8), (x, y + 4)], "#A2A074", 1.4)
        p.line([(30, 22), (67, 18)], "#C5B888", 1.1)


def _seedbed(p):
    """小苗圃用短绿叶和断续湿土表现，内容与成熟金稻区分。"""
    p.path([("M", -68, -14), ("C", -43, -24, -17, -20, 9, -23),
            ("L", 58, -16), ("L", 65, 13), ("C", 29, 22, -24, 21, -61, 15)],
            "#6D995C", "#425A35", 1.6)
    p.path([("M", -64, -13), ("C", -34, -20, -6, -17, 19, -19),
            ("L", 55, -13), ("L", 59, 11), ("C", 21, 18, -23, 17, -58, 12)], "#B6C092")
    for row in range(3):
        y = -10 + row * 10
        p.curve((-53, y + 3), (-14, y + 5), (21, y), (51, y + 1), (122, 152, 106, 82), .8)
        for column in range(7):
            x = -48 + column * 16 + (row % 2) * 3
            p.curve((x, y + 1), (x - 1, y - 3), (x - 4, y - 6), (x - 5, y - 6), "#789F6A", 1.1)
            p.curve((x, y + 1), (x + 1, y - 4), (x + 3, y - 7), (x + 5, y - 7), "#D1D3A0", 1.1)


def _sheaves(p):
    """两束错落的收割稻草，暖色面与细秆即可读出形状。"""
    for x, y, scale in ((-13, -1, 1), (17, 6, .77)):
        q = _Local(p.painter, *p.point((x, y)), p.sx * scale, p.sy / p.sx)
        q.ellipse((-14, -1, 17, 7), (112, 133, 88, 27))
        q.path([("M", -12, 1), ("C", -9, -9, -5, -15, -4, -25),
                ("C", -9, -33, -4, -38, 1, -34), ("C", 7, -39, 11, -31, 6, -26),
                ("C", 7, -15, 12, -7, 15, 1), ("C", 8, 5, -5, 5, -12, 1)],
                "#D1A94D", "#65442C", 1.3)
        for at in (-7, -2, 3, 8):
            q.curve((at, 1), (at * .5, -11), (at * .6, -23), (at * .6, -32), "#E2CF92", .8)
        q.line([(-5, -16), (7, -16)], "#A28D5F", 1.5)


def _angled_paddy(p, variant, angle):
    tilted = _Local(p.painter, p.x, p.y, p.sx, p.sy / p.sx, angle)
    _paddy(tilted, variant)


def _well(p):
    p.ellipse((-15, 2, 20, 11), (78, 119, 88, 28))
    p.ellipse((-13, -8, 14, 8), "#AFBA9E")
    p.polygon([(-13, -3), (14, -3), (14, 6), (-13, 6)], "#ADBA9A")
    p.ellipse((-13, -11, 14, 3), "#DBD8B4", "#9DAE91", 1)
    p.ellipse((-8, -8, 9, 0), "#729486")
    p.line([(-10, -20), (-10, -2)], "#9B9871", 2)
    p.line([(11, -20), (11, -2)], "#9B9871", 2)
    p.line([(-13, -20), (15, -20)], "#B8AB79", 2.1)
    p.line([(1, -20), (1, -3)], "#CFCAA0", .7)


def _grass_brush(canvas, scene):
    """草丝集中在田边与少数空地，密度随区域变化并避开玩法净空。"""
    rng = random.Random(90317)
    marks = _Art()
    accepted = 0
    clusters = ((297, 432, 31, 17, 34, .64), (786, 438, 56, 21, 42, .66),
                (1052, 447, 25, 17, 18, .54), (1410, 437, 60, 22, 38, .63),
                (442, 645, 32, 9, 26, .55), (1186, 641, 37, 8, 16, .44),
                (1770, 570, 34, 13, 12, .43), (1780, 801, 72, 27, 24, .48))
    for center_x, center_y, radius_x, radius_y, attempts, density in clusters:
        for _ in range(attempts):
            x = center_x + rng.uniform(-radius_x, radius_x)
            y = center_y + rng.uniform(-radius_y, radius_y)
            if ((x - center_x) / radius_x) ** 2 + ((y - center_y) / radius_y) ** 2 > 1:
                continue
            if rng.random() > density or not scene.clear((x - 8, y - 12, x + 8, y + 4))[0]:
                continue
            accepted += 1
            for blade in range(3):
                start_x = x + rng.uniform(-4, 4)
                start_y = y + rng.uniform(-2, 2)
                bend = rng.uniform(-4, 4)
                end_y = y - rng.uniform(5, 11)
                marks.curve((start_x, start_y), (start_x + bend * .2, y - 4),
                            (start_x + bend, end_y + 2), (start_x + bend, end_y),
                            (52, 91, 43, 180), .9)
                if blade == 1:
                    marks.line([(start_x + .8, start_y - 2), (start_x + bend * .6, end_y + 1)],
                               (196, 213, 112, 195), .65)
    canvas.alpha_composite(marks.image)
    return accepted


def make_terrain(level: dict) -> Image.Image:
    """读取调用方提供的正式黄巾数据，返回1920×1080 RGBA地图。"""
    if not isinstance(level, dict) or level.get("Id") != "huangjin":
        raise ValueError("二维黄巾地图需要正式huangjin关卡数据")
    if not level.get("Routes") or not level.get("Slots"):
        raise ValueError("黄巾地图缺少正式路线或阵位")
    if any(len(route.get("Points", [])) < 2 for route in level["Routes"]):
        raise ValueError("每条正式路线至少需要两个点")
    canvas = _ground(level)
    scene = _Scene(canvas, level)
    _village_ground(canvas, scene.slots)
    _road(canvas, scene.routes)

    endpoint = scene.routes[0][-1]
    scene.place("终点青瓦关楼", endpoint[0], endpoint[1] + 4,
                _gate, scale=1.18, vertical=1.03, allow_road=True)
    scene.place("西侧黄巾帐篷木栅营寨", 160, 460, _camp, scale=.95, vertical=1.0)

    # 三组农景各有朝向与疏密：营寨外金稻、青苗边竹岩、东侧收割田。
    scene.place("西田大片金稻", 430, 413, lambda p: _angled_paddy(p, 0, -10), scale=1.25, vertical=.88)
    scene.place("西田矮果树", 570, 446, lambda p: _tree(p, 1), scale=.66, vertical=1.06)
    scene.place("西中田小片金稻", 670, 405, lambda p: _angled_paddy(p, 1, 7), scale=.72, vertical=.90)
    scene.place("中田小菜圃", 914, 386, _seedbed, scale=.88, vertical=.95)
    scene.place("中田短竹", 1012, 432, _bamboo, scale=.56, vertical=1.0)
    scene.place("中田石沿", 842, 431, _rock, scale=.43)
    scene.place("东中田青苗圃", 1108, 392, _seedbed, scale=.76, vertical=.95)
    scene.place("东田收割金稻", 1300, 419, lambda p: _angled_paddy(p, 2, 12), scale=.82, vertical=.92)
    scene.place("东田收割草束", 1410, 401, _sheaves, scale=.81, vertical=1.02)
    scene.place("东田小树", 1460, 500, lambda p: _tree(p, 2), scale=.60, vertical=1.02)

    # 中段田畦分散落在阵位网格的空档里，压低体量并改变朝向。
    scene.place("西中段金稻", 480, 515, lambda p: _angled_paddy(p, 0, -15), scale=.82, vertical=.86)
    scene.place("中段小片金稻", 1035, 512, lambda p: _angled_paddy(p, 1, 13), scale=.74, vertical=.86)
    scene.place("东中段菜圃", 1330, 493, _seedbed, scale=.78, vertical=.90)

    # 边角成组，避开上排武将与会被正式布局裁掉的高枝。
    scene.place("东北林簇", 1593, 235, lambda p: _grove(p, 0), scale=.86, vertical=1.00)
    scene.place("东北浅岩", 1699, 203, _rock, scale=.85)
    scene.place("西侧浅岩", 300, 440, _rock, scale=.57)
    scene.place("西南林簇", 85, 816, lambda p: _grove(p, 1), scale=.72, vertical=1.0)
    scene.place("东南林簇", 1809, 813, lambda p: _grove(p, 0), scale=.88, vertical=1.0)
    scene.place("东南浅岩", 1687, 815, _rock, scale=.77)

    # 花草只成簇落在走廊和边角，不向全图撒高频噪点。
    flowers = ((281, 436, .62), (310, 436, .50),
               (780, 440, .52), (811, 434, .58),
               (1048, 448, .48), (1080, 446, .55),
               (1382, 435, .56), (1424, 435, .50),
               (72, 600, .61), (1812, 571, .66), (1846, 725, .68), (1709, 749, .61),
               (1514, 213, .63), (426, 649, .51), (454, 643, .54),
               (1170, 639, .52), (1198, 647, .48), (1451, 650, .52))
    for index, (x, y, scale) in enumerate(flowers):
        scene.place("疏花草" + str(index + 1), x, y,
                    lambda p, bloom=index % 3 != 1: _tuft(p, bloom), scale=scale)
    grass_clumps = _grass_brush(canvas, scene)

    result = canvas.resize(_SIZE, Image.Resampling.LANCZOS)
    result.info["layout_report"] = {
        "Style": "二维青绿手绘郊野",
        "Decorations": scene.placed,
        "Rejected": len(scene.rejected),
        "GrassClumps": grass_clumps,
        "RejectedDetails": scene.rejected,
        "RoutePointCount": sum(len(route) for route in scene.routes),
        "SlotCount": len(scene.slots),
        "SlotClearance": [55, 42],
        "RoadHalfWidth": 43,
        "VisibleBounds": [38, 144, 1882, 842],
    }
    return result
