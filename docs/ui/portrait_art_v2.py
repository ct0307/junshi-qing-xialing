"""绘制演义沙盘原创全身棋子；仅依赖 Pillow，不在导入时写入文件。"""

from __future__ import annotations

import math

from PIL import Image, ImageDraw, ImageFilter


__all__ = ["make_character"]

PAPER = "#F0EBDD"
GOLD = "#C7AA71"
GOLD_LIGHT = "#E8D4A1"
INK = "#47574F"
HAIR = "#3D4944"
HAIR_LIGHT = "#637067"
STEEL = "#B7C9C6"
STEEL_LIGHT = "#EDF1E7"

PROFILES = {
    "关羽": dict(coat="#547F67", light="#84A089", dark="#365E50", cape="#65806A", skin="#D3A58A", armor=True),
    "赵云": dict(coat="#DEE3D8", light="#F6F3E7", dark="#90ABA4", cape="#CCDCD3", skin="#E4C4A5", armor=True),
    "张飞": dict(coat="#4B5D5B", light="#788781", dark="#344947", cape="#777363", skin="#CFA583", armor=True),
    "黄忠": dict(coat="#B6A071", light="#D3C399", dark="#85734F", cape="#9A9678", skin="#DEBA96", armor=True),
    "周瑜": dict(coat="#D3D8C8", light="#EEEADB", dark="#819083", cape="#B26A5B", skin="#E8C5A6", armor=True),
    "诸葛亮": dict(coat="#DCE0C8", light="#F2EEDB", dark="#8C9F8A", cape="#8BA69A", skin="#DEC1A0", armor=False),
    "曹操": dict(coat="#526C77", light="#81969A", dark="#354F5C", cape="#6C7C7B", skin="#DCB496", armor=False),
    "貂蝉": dict(coat="#BAADC3", light="#E0D5DE", dark="#897E9D", cape="#CBBCCB", skin="#ECD0B7", armor=False),
    "庞统": dict(coat="#9A8368", light="#BEAC8D", dark="#6B6955", cape="#7F8065", skin="#D5AF8B", armor=False),
    "华佗": dict(coat="#9EB09A", light="#D1D8BD", dark="#6F8C78", cape="#B4BBA1", skin="#E1C3A1", armor=False),
    "张辽": dict(coat="#A5685D", light="#C4937C", dark="#744E48", cape="#B47A62", skin="#DDB696", armor=True),
}


def _mix(first, second, amount):
    """在同一材质内混色，保持浅轮廓与柔和折面。"""
    def channels(value):
        if isinstance(value, str):
            value = value.lstrip("#")
            return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))
        return value[:3]

    a, b = channels(first), channels(second)
    return tuple(round(x + (y - x) * amount) for x, y in zip(a, b))


class _Painter:
    """以 320 单位绘制，并在函数出口统一超采样缩小。"""

    def __init__(self, size):
        self.resolution = max(960, size * 3)
        self.scale = self.resolution / 320
        self.image = Image.new("RGBA", (self.resolution, self.resolution))
        self.draw = ImageDraw.Draw(self.image)

    def point(self, point):
        return tuple(round(value * self.scale) for value in point)

    def box(self, rectangle):
        return tuple(round(value * self.scale) for value in rectangle)

    def line(self, points, fill, width=1):
        self.draw.line([self.point(p) for p in points], fill=fill,
                       width=max(1, round(width * self.scale)), joint="curve")

    def ellipse(self, rectangle, fill, outline=None, width=1):
        self.draw.ellipse(self.box(rectangle), fill=fill, outline=outline,
                          width=max(1, round(width * self.scale)))

    def rounded(self, rectangle, radius, fill, outline=None, width=1):
        self.draw.rounded_rectangle(self.box(rectangle), round(radius * self.scale),
                                    fill=fill, outline=outline,
                                    width=max(1, round(width * self.scale)))

    def polygon(self, points, fill, outline=None, width=1):
        self.draw.polygon([self.point(p) for p in points], fill=fill)
        if outline:
            self.line(points + [points[0]], outline, width)

    def path(self, commands, fill=None, outline=None, width=1):
        """用三次贝塞尔采样曲线，不引入额外绘图库。"""
        points = []
        for command in commands:
            if command[0] in ("M", "L"):
                points.append((command[1], command[2]))
            elif command[0] == "C":
                start = points[-1]
                control_a = command[1:3]
                control_b = command[3:5]
                end = command[5:7]
                for step in range(1, 21):
                    t = step / 20
                    points.append(tuple((1 - t) ** 3 * start[i]
                                        + 3 * (1 - t) ** 2 * t * control_a[i]
                                        + 3 * (1 - t) * t * t * control_b[i]
                                        + t ** 3 * end[i] for i in (0, 1)))
        if fill:
            self.polygon(points, fill)
        if outline:
            self.line(points, outline, width)

    def curve(self, start, first, second, end, fill, width=1):
        self.path([("M", *start), ("C", *first, *second, *end)],
                  outline=fill, width=width)


def _base(p, enemy):
    layer = Image.new("RGBA", p.image.size)
    d = ImageDraw.Draw(layer)
    d.ellipse(p.box((73, 277, 234, 302)), fill=(63, 78, 59, 37))
    p.image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(5 * p.scale)))
    p.ellipse((73, 263, 228, 292), "#865246" if enemy else "#B6AB8C")
    p.ellipse((73, 258, 228, 285), "#B57560" if enemy else "#E2D9BB", "#B6AD91", 1)
    p.ellipse((81, 260, 220, 279), "#D9A68B" if enemy else "#F0E9D0", "#D0C5A5", .8)
    p.curve((88, 274), (112, 283), (181, 284), (211, 272), "#EBC8A4" if enemy else "#F6EFDA", 1.2)
    for x in (97, 126, 158, 193):
        p.line([(x, 284), (x + 1, 288)], "#C8BDA0", .8)
    # 以朱砂台座和刻线区分敌方，保留人物自身配色。
    if enemy:
        p.curve((91, 280), (124, 294), (186, 294), (216, 278), "#AF7161", 2)


def _weapon_back(p, name):
    if name in ("关羽", "赵云", "张飞", "张辽"):
        p.line([(235, 72), (229, 275)], "#7B7058", 5)
        p.line([(233.5, 76), (227.5, 274)], "#BCAD86", 1.4)
        for y in (91, 175, 205, 260):
            x = 235 - (y - 72) * 6 / 203
            p.line([(x - 3, y), (x + 3, y)], GOLD, 3)
        if name == "关羽":
            p.path([("M", 233, 95), ("C", 260, 91, 279, 61, 270, 27),
                    ("C", 262, 43, 245, 48, 233, 48), ("L", 229, 86)],
                   "#83A99A", "#597C70", 1.3)
            p.path([("M", 238, 88), ("C", 259, 80, 270, 53, 270, 29),
                    ("C", 278, 56, 262, 83, 238, 91)], STEEL_LIGHT)
            p.curve((236, 65), (252, 53), (261, 62), (244, 75), GOLD_LIGHT, 1.5)
            p.ellipse((242, 60, 247, 64), GOLD_LIGHT)
            p.polygon([(229, 86), (240, 87), (240, 97), (229, 99)], GOLD)
        elif name == "赵云":
            p.polygon([(235, 20), (222, 50), (234, 70), (247, 50)], STEEL, "#819B94", 1)
            p.polygon([(235, 22), (235, 64), (226, 50)], STEEL_LIGHT)
            p.line([(235, 26), (234, 65)], "#93ADA7", 1)
            for offset in (-6, 0, 6):
                p.curve((235 + offset / 2, 70), (245 + offset, 81),
                        (243 + offset, 95), (238 + offset, 106), "#B27665", 2.2)
            p.rounded((229, 65, 240, 74), 3, GOLD)
        elif name == "张飞":
            p.path([("M", 235, 81), ("C", 223, 66, 248, 57, 235, 43),
                    ("C", 227, 34, 238, 23, 246, 20),
                    ("C", 236, 38, 257, 45, 249, 58),
                    ("C", 241, 67, 241, 72, 243, 82)],
                   "#9AAFA4", "#607C72", 1.2)
            p.curve((242, 29), (230, 45), (251, 50), (236, 74), STEEL_LIGHT, 1.2)
            p.rounded((229, 80, 246, 88), 3, GOLD)
        else:
            p.polygon([(239, 20), (227, 51), (237, 75), (249, 51)], STEEL, "#7E8B7D", 1)
            p.polygon([(239, 22), (237, 70), (231, 51)], STEEL_LIGHT)
            p.path([("M", 242, 63), ("C", 252, 65, 263, 58, 274, 49),
                    ("C", 275, 69, 264, 88, 242, 88)],
                   "#B6BCA5", "#827D63", 1)
            p.curve((248, 83), (261, 81), (271, 64), (273, 51), STEEL_LIGHT, 1.5)
            p.line([(226, 92), (251, 92)], GOLD, 4)
            p.curve((239, 87), (255, 99), (250, 112), (256, 121), "#B76E59", 3)
    elif name == "黄忠":
        p.polygon([(78, 157), (94, 157), (104, 230), (88, 234)], "#866F50", "#B39A71", 1)
        for x, y in ((79, 126), (88, 130), (96, 134)):
            p.line([(x, y), (x + 10, y + 58)], "#C8B389", 2)
            p.polygon([(x, y), (x - 5, y + 10), (x + 4, y + 17), (x + 3, y + 3)], "#DCD8BE")
        p.curve((220, 81), (271, 109), (278, 192), (224, 240), "#746343", 5)
        p.curve((220, 81), (269, 109), (275, 191), (224, 240), "#C8AD77", 2.7)
        p.line([(220, 81), (222, 164), (224, 240)], "#EAE0C6", 1.1)
        p.line([(250, 147), (253, 177)], "#8E6F4F", 5)
        for y in range(149, 178, 5):
            p.line([(249, y), (256, y - 1)], GOLD_LIGHT, .8)
    elif name == "周瑜":
        p.line([(203, 168), (227, 269)], "#53776D", 9)
        p.line([(205, 182), (225, 266)], "#9BB1A0", 1.7)
        p.line([(200, 154), (206, 180)], "#695743", 6)
        for y in (158, 165, 172):
            p.line([(197 + (y - 154) / 4, y), (204 + (y - 154) / 4, y - 1)], GOLD, 1)
        p.line([(194, 179), (218, 173)], GOLD, 4)
        p.ellipse((196, 150, 204, 158), GOLD_LIGHT, "#AA8A5C", .8)
        p.curve((205, 179), (221, 180), (216, 207), (225, 219), "#B16C5B", 2)


def _garments(p, name, palette):
    main, light, dark = (palette[key] for key in ("coat", "light", "dark"))
    cape = palette["cape"]
    p.path([("M", 117, 146), ("C", 89, 153, 93, 211, 71, 253),
            ("C", 91, 265, 111, 263, 131, 252), ("L", 178, 254),
            ("C", 194, 268, 212, 262, 219, 250),
            ("C", 192, 209, 198, 155, 174, 147)], cape,
           _mix(cape, INK, .2), 1)
    p.path([("M", 118, 155), ("C", 104, 181, 106, 221, 88, 254),
            ("L", 102, 256), ("C", 115, 215, 111, 183, 128, 156)],
           _mix(cape, PAPER, .22))
    p.curve((184, 168), (185, 204), (192, 232), (207, 254), _mix(cape, INK, .22), 1.8)
    # 长靴先于衣摆绘制，保持完整站姿。
    for left, right in ((106, 139), (158, 191)):
        p.path([("M", left + 5, 242), ("L", right - 3, 242),
                ("L", right, 266), ("C", right + 3, 273, left - 8, 273, left - 7, 267),
                ("L", left + 3, 260)], _mix(dark, INK, .25), _mix(dark, INK, .5), 1)
        p.curve((left - 1, 267), (left + 5, 269), (right - 8, 270), (right - 2, 267),
                _mix(dark, PAPER, .28), 1)
    robe_bottom = 267 if not palette["armor"] else 255
    p.path([("M", 119, 145), ("C", 107, 158, 106, 190, 109, 209),
            ("L", 98, robe_bottom - 6),
            ("C", 119, robe_bottom + 3, 175, robe_bottom + 5, 199, robe_bottom - 4),
            ("L", 183, 207), ("C", 188, 182, 184, 156, 171, 146)],
           main, _mix(main, dark, .55), 1.1)
    p.path([("M", 121, 150), ("L", 137, 162), ("L", 125, 204),
            ("L", 114, robe_bottom - 6), ("L", 104, robe_bottom - 8),
            ("L", 115, 205)], light)
    p.path([("M", 169, 151), ("L", 180, 167), ("L", 177, 207),
            ("L", 193, robe_bottom - 5), ("L", 178, robe_bottom - 3),
            ("L", 160, 202)], _mix(main, dark, .43))
    for start, end in (((135, 211), (125, robe_bottom - 3)), ((153, 215), (156, robe_bottom)),
                       ((173, 219), (183, robe_bottom - 4))):
        p.curve(start, (start[0] - 5, start[1] + 13),
                (end[0] + 2, end[1] - 13), end, _mix(main, dark, .35), 1)
    # 交领在铠甲与文士袍服中沿用同一纸艺折叠语言。
    p.polygon([(124, 145), (146, 163), (162, 145), (171, 151), (146, 184), (115, 156)], light)
    p.line([(124, 148), (147, 170), (164, 149)], _mix(light, dark, .3), 1.5)
    if palette["armor"]:
        armor = _mix(main, STEEL, .35)
        p.rounded((118, 168, 179, 208), 11, armor, _mix(armor, dark, .45), 1)
        p.path([("M", 123, 172), ("C", 134, 178, 149, 182, 175, 172)],
               outline=_mix(armor, PAPER, .65), width=1.4)
        for row in range(3):
            for column in range(5):
                x = 122 + column * 10 + (row % 2) * 3
                y = 181 + row * 8
                p.path([("M", x, y), ("C", x + 1, y + 7, x + 7, y + 7, x + 9, y)],
                       outline=_mix(armor, dark, .35), width=.8)
        p.ellipse((140, 180, 158, 198), _mix(main, PAPER, .32), GOLD, 1)
        p.polygon([(149, 183), (155, 189), (149, 195), (143, 189)], _mix(main, dark, .35))
        for x, tilt in ((111, -4), (129, -1), (149, 0), (169, 4)):
            p.polygon([(x, 214), (x + 16, 214), (x + 18 + tilt, 248),
                       (x + 8 + tilt, 253), (x - 2 + tilt, 246)], armor,
                      _mix(armor, dark, .4), .8)
            for y in (225, 233, 241):
                p.line([(x + 2, y), (x + 14, y)], _mix(armor, PAPER, .27), .8)
        for x in (119, 177):
            p.polygon([(x, 151), (x - 18, 156), (x - 23, 173),
                       (x - 7, 179), (x + 9, 170), (x + 12, 158)], armor,
                      _mix(armor, dark, .35), 1)
            p.curve((x - 17, 166), (x - 8, 171), (x + 3, 168), (x + 7, 160),
                    _mix(armor, PAPER, .55), 1.1)
            for ox in (-12, -3):
                p.ellipse((x + ox, 164, x + ox + 2.5, 166.5), GOLD_LIGHT)
    belt_y = 210 if palette["armor"] else 204
    p.rounded((108, belt_y, 185, belt_y + 10), 4, dark)
    p.line([(113, belt_y + 2), (180, belt_y + 2)], GOLD, 1)
    p.rounded((137, belt_y - 1, 156, belt_y + 11), 3, GOLD, _mix(GOLD, dark, .3), .8)
    p.rounded((142, belt_y + 2, 151, belt_y + 8), 2, _mix(main, light, .4))
    p.line([(163, belt_y + 9), (169, belt_y + 32)], GOLD, 1.3)
    p.ellipse((164, belt_y + 27, 174, belt_y + 37), _mix(main, PAPER, .45), GOLD, 1)
    if name == "貂蝉":
        p.path([("M", 122, 153), ("C", 108, 174, 119, 211, 110, 252),
                ("L", 122, 263), ("C", 137, 211, 127, 181, 142, 162)], "#D9C6CB")
        p.path([("M", 157, 204), ("C", 171, 222, 179, 236, 201, 240),
                ("C", 185, 230, 183, 214, 174, 204)], "#D9C6CB")
        for x, y in ((135, 235), (167, 252), (123, 221)):
            _flower(p, x, y, 3.5, "#E9D8D5", "#BAA478")


def _arms(p, name, palette):
    main, light, dark = (palette[key] for key in ("coat", "light", "dark"))
    p.path([("M", 111, 156), ("C", 96, 159, 91, 180, 88, 200),
            ("C", 88, 210, 100, 214, 111, 207),
            ("L", 125, 183), ("L", 125, 163)], main, _mix(main, dark, .5), 1)
    p.path([("M", 104, 165), ("C", 97, 178, 95, 191, 95, 204),
            ("L", 102, 205), ("C", 103, 184, 109, 177, 115, 168)], light)
    p.path([("M", 177, 157), ("C", 190, 159, 191, 178, 206, 182),
            ("L", 218, 176), ("L", 228, 188),
            ("C", 218, 207, 199, 206, 187, 192), ("L", 173, 176)],
           main, _mix(main, dark, .5), 1)
    p.curve((183, 165), (187, 186), (204, 193), (215, 185), light, 5)
    p.line([(90, 199), (109, 205)], dark, 5)
    p.line([(214, 181), (223, 193)], dark, 5)
    p.line([(91, 198), (110, 204)], GOLD, 1)
    p.line([(213, 180), (222, 192)], GOLD, 1)
    skin = palette["skin"]
    p.ellipse((93, 200, 111, 218), skin, _mix(skin, "#AB8D70", .4), .8)
    p.ellipse((216, 177, 237, 196), skin, _mix(skin, "#AB8D70", .4), .8)
    p.curve((221, 182), (225, 179), (232, 182), (231, 187), _mix(skin, PAPER, .4), 1)


def _flower(p, x, y, radius, petal, center):
    for angle in range(0, 360, 72):
        rad = math.radians(angle)
        dx, dy = math.cos(rad) * radius, math.sin(rad) * radius
        p.ellipse((x + dx - radius * .65, y + dy - radius * .65,
                   x + dx + radius * .65, y + dy + radius * .65), petal)
    p.ellipse((x - radius * .35, y - radius * .35, x + radius * .35, y + radius * .35), center)


def _head(p, name, palette):
    skin = palette["skin"]
    pale_hair = name in ("黄忠", "华佗")
    hair = "#D8D9C9" if pale_hair else HAIR
    hair_light = "#F2EEDB" if pale_hair else HAIR_LIGHT
    p.ellipse((95, 68, 191, 163), hair)
    if name == "貂蝉":
        for x in (93, 179):
            p.path([("M", x, 101), ("C", x - 8, 129, x - 3, 153, x - 11, 180),
                    ("C", x + 8, 166, x + 14, 132, x + 10, 105)], hair)
    p.rounded((132, 135, 159, 161), 9, _mix(skin, "#AD8F76", .13))
    for rectangle in ((96, 106, 111, 130), (178, 106, 193, 130)):
        p.ellipse(rectangle, skin)
        p.curve((rectangle[0] + 5, 113), (rectangle[0] + 1, 111),
                (rectangle[0] + 2, 121), (rectangle[0] + 6, 122), _mix(skin, "#B28D72", .5), .9)
    face_box = (104, 77, 184, 152) if name != "张飞" else (101, 76, 187, 153)
    p.ellipse(face_box, skin, _mix(skin, "#AE957B", .4), .8)
    p.ellipse((108, 81, 176, 143), _mix(skin, PAPER, .09))
    p.path([("M", 175, 92), ("C", 185, 110, 185, 134, 165, 146),
            ("C", 178, 145, 190, 129, 180, 106)], _mix(skin, "#B49175", .18))
    p.ellipse((114, 128, 126, 133), _mix(skin, "#C58E7C", .25))
    p.ellipse((165, 128, 177, 133), _mix(skin, "#C58E7C", .25))
    # 面部用眉眼角度区分性格，避免同一张圆脸换装。
    if name == "张飞":
        p.curve((115, 106), (121, 102), (126, 105), (131, 108), HAIR, 4)
        p.curve((155, 108), (163, 102), (170, 102), (176, 105), HAIR, 4)
        for x in (124, 164):
            p.ellipse((x - 5, 113, x + 5, 122), "#F4EAD9")
            p.ellipse((x - 2, 113, x + 3, 121), INK)
            p.ellipse((x - 1, 114, x + .8, 116), PAPER)
    elif name in ("华佗", "黄忠"):
        for x in (121, 162):
            p.curve((x - 7, 110), (x - 2, 105), (x + 5, 106), (x + 10, 110), hair_light, 3)
            p.curve((x - 4, 118), (x, 115), (x + 4, 115), (x + 8, 118), INK, 1.6)
            p.line([(x - 8, 120), (x - 5, 122)], "#C4A88A", .8)
    elif name == "貂蝉":
        for x in (122, 163):
            p.curve((x - 7, 109), (x - 2, 106), (x + 4, 106), (x + 9, 108), "#75675E", 1.3)
            p.curve((x - 5, 117), (x - 2, 114), (x + 4, 114), (x + 7, 117), "#685B59", 1.6)
            p.ellipse((x - 1, 116, x + 3, 121), "#665859")
            p.line([(x + 6, 116), (x + 9, 113)], "#685B59", .8)
        p.curve((140, 137), (143, 140), (147, 140), (151, 136), "#B67B77", 1.4)
    else:
        strong = name in ("关羽", "曹操", "张辽")
        for x in (122, 163):
            slope = -2 if x == 122 else 2
            p.curve((x - 8, 108 - slope), (x - 2, 106), (x + 5, 107), (x + 9, 108 + slope),
                    hair, 2.5 if strong else 1.8)
            if name in ("关羽", "诸葛亮", "庞统"):
                p.curve((x - 5, 117), (x - 1, 119), (x + 4, 119), (x + 7, 116), INK, 1.7)
            else:
                p.curve((x - 5, 116), (x, 114), (x + 4, 115), (x + 7, 117), INK, 1.5)
                p.ellipse((x - .5, 116, x + 3.5, 121), INK)
                p.ellipse((x, 116.5, x + 1, 117.5), PAPER)
    p.curve((144, 119), (141, 125), (140, 128), (146, 128), _mix(skin, "#B49175", .55), .9)
    if name != "貂蝉":
        p.curve((136, 136), (142, 140), (149, 140), (154, 135), "#AA7C65", 1.2)
    if name in ("关羽", "张飞", "黄忠", "诸葛亮", "曹操", "庞统", "华佗", "张辽"):
        _beard(p, name, hair, hair_light)


def _beard(p, name, hair, highlight):
    if name == "张飞":
        for x, y, r in ((113, 140, 10), (122, 149, 13), (137, 154, 15),
                        (153, 154, 15), (167, 148, 12), (176, 139, 9)):
            p.ellipse((x - r, y - r, x + r, y + r), hair)
            p.curve((x - 4, y + 3), (x - 7, y - 3), (x + 5, y - 6), (x + 5, y + 1), highlight, .8)
    else:
        end = {"关羽": 208, "黄忠": 186, "诸葛亮": 172, "曹操": 180,
               "庞统": 182, "华佗": 184, "张辽": 169}[name]
        side = 120 if name in ("关羽", "黄忠") else 127
        p.path([("M", side, 136), ("C", side + 2, 153, 129, end - 8, 145, end),
                ("C", 164, end - 9, 172, 150, 169, 136),
                ("C", 158, 149, 137, 148, side, 136)], hair)
        for offset in (-9, -1, 7):
            p.curve((145 + offset, 148), (143 + offset, 163),
                    (148 + offset / 3, end - 14), (145 + offset / 4, end - 5), highlight, .75)
    p.path([("M", 142, 132), ("C", 135, 130, 129, 135, 125, 138),
            ("C", 135, 140, 141, 138, 145, 135),
            ("C", 151, 140, 159, 140, 165, 136),
            ("C", 155, 135, 151, 130, 145, 132)], hair)


def _helmet(p, name):
    gold = name == "黄忠"
    red = name == "张辽"
    base = "#C2AB72" if gold else ("#A97763" if red else "#BACEC8")
    light = "#E4D5A7" if gold else ("#D3A48A" if red else "#EAF0E6")
    dark = "#8C7954" if gold else ("#7D554D" if red else "#809F97")
    p.path([("M", 98, 103), ("C", 94, 77, 107, 53, 141, 50),
            ("C", 172, 48, 191, 69, 189, 104),
            ("L", 173, 100), ("C", 153, 96, 124, 96, 98, 103)], base, dark, 1.2)
    p.path([("M", 107, 94), ("C", 104, 73, 121, 56, 140, 54),
            ("C", 125, 65, 123, 76, 122, 96)], light)
    p.path([("M", 145, 53), ("C", 159, 63, 166, 81, 166, 99),
            ("L", 173, 100), ("C", 174, 80, 161, 60, 145, 53)], _mix(base, dark, .2))
    p.path([("M", 140, 51), ("L", 148, 51), ("L", 153, 99), ("L", 137, 99)], light, dark, .7)
    p.curve((100, 99), (123, 91), (167, 91), (187, 100), GOLD, 3)
    p.ellipse((135, 86, 156, 105), GOLD, dark, .8)
    p.polygon([(145, 89), (151, 96), (145, 101), (139, 96)], "#819C88" if gold else "#B17964")
    for x in (99, 180):
        p.path([("M", x, 99), ("L", x + 9, 101), ("L", x + 10, 126),
                ("L", x + 4, 133), ("L", x - 2, 125)], base, dark, .8)
        p.line([(x + 2, 105), (x + 3, 125)], light, 1)
    if name == "赵云":
        p.path([("M", 145, 52), ("C", 141, 35, 165, 17, 197, 23),
                ("C", 189, 25, 185, 42, 166, 48), ("L", 151, 55)],
               "#F0EEDD", "#BFCFC2", .8)
        for offset in (0, 5, 10):
            p.curve((149, 48), (159, 35 + offset), (177, 25 + offset), (190, 25), "#C4D3C6", .8)
    elif name == "黄忠":
        p.polygon([(137, 51), (139, 35), (147, 30), (153, 36), (155, 52)], GOLD, dark, .8)
        p.line([(145, 36), (145, 49)], GOLD_LIGHT, 1.4)
    else:
        p.path([("M", 145, 51), ("C", 145, 25, 174, 21, 194, 33),
                ("C", 176, 29, 174, 52, 154, 57)], "#B76555", "#945A4E", .8)
        p.curve((150, 49), (161, 31), (177, 30), (185, 32), "#CD9277", 1.1)


def _headwear(p, name, palette):
    main, light, dark = (palette[key] for key in ("coat", "light", "dark"))
    if name in ("赵云", "黄忠", "张辽"):
        _helmet(p, name)
    elif name == "关羽":
        p.path([("M", 99, 100), ("L", 105, 77), ("L", 128, 59),
                ("C", 144, 54, 168, 62, 177, 79), ("L", 188, 100),
                ("C", 158, 91, 127, 91, 99, 100)], main, dark, 1)
        p.path([("M", 107, 85), ("L", 128, 65), ("L", 142, 62),
                ("L", 133, 85), ("L", 105, 96)], light)
        p.curve((103, 96), (123, 87), (161, 87), (183, 96), GOLD, 2.2)
        p.ellipse((136, 81, 155, 97), GOLD)
        p.ellipse((141, 85, 150, 93), "#B9CCAE")
        p.path([("M", 103, 96), ("C", 86, 102, 97, 120, 81, 141),
                ("L", 94, 139), ("C", 106, 125, 102, 110, 109, 101)], dark)
        p.curve((101, 103), (97, 117), (100, 128), (91, 134), light, 1.2)
    elif name == "张飞":
        p.polygon([(96, 99), (99, 74), (110, 78), (116, 59), (130, 67),
                   (141, 53), (153, 66), (170, 57), (174, 77), (188, 75), (191, 101)], HAIR)
        p.path([("M", 102, 94), ("C", 121, 87, 164, 87, 186, 95),
                ("L", 185, 103), ("C", 161, 95, 124, 94, 103, 102)], dark)
        p.curve((107, 96), (129, 90), (162, 92), (181, 97), GOLD, 2)
        p.polygon([(143, 87), (151, 95), (143, 103), (135, 95)], "#B59A69", "#D8C08B", .8)
        p.curve((111, 82), (112, 74), (120, 70), (128, 73), HAIR_LIGHT, 1)
    elif name == "诸葛亮":
        p.path([("M", 98, 99), ("L", 108, 62), ("L", 126, 52),
                ("L", 153, 58), ("L", 171, 53), ("L", 189, 99),
                ("C", 159, 91, 128, 92, 98, 99)], "#54766F", "#3F605B", 1)
        p.polygon([(108, 65), (126, 56), (139, 61), (126, 93), (104, 96)], "#7C968A")
        p.polygon([(154, 62), (169, 57), (182, 95), (162, 92)], "#68877B")
        p.curve((103, 97), (128, 90), (163, 92), (185, 98), "#BCC6A7", 2)
        p.polygon([(101, 90), (86, 87), (93, 111), (102, 104)], "#54766F")
        p.polygon([(185, 92), (200, 88), (193, 112), (185, 106)], "#54766F")
    elif name == "曹操":
        p.path([("M", 103, 97), ("L", 115, 71), ("L", 117, 36),
                ("C", 133, 27, 158, 29, 171, 39), ("L", 174, 72),
                ("L", 187, 98), ("C", 158, 90, 129, 91, 103, 97)], dark, "#304B56", 1)
        p.path([("M", 121, 39), ("C", 129, 35, 137, 34, 142, 36),
                ("L", 139, 88), ("L", 120, 90)], main)
        for x in (127, 140, 154, 166):
            p.line([(x, 43), (x - 1, 78)], "#9B9B82", 1)
        p.curve((109, 94), (131, 86), (162, 87), (182, 94), GOLD, 3)
        p.ellipse((136, 81, 155, 96), GOLD, "#8A7E5E", .8)
        p.ellipse((141, 84, 150, 92), "#9CB3A0")
    elif name == "貂蝉":
        for rectangle in ((98, 56, 129, 87), (159, 55, 191, 87)):
            p.ellipse(rectangle, HAIR, HAIR_LIGHT, .8)
        p.path([("M", 101, 103), ("C", 95, 75, 122, 65, 144, 71),
                ("C", 169, 62, 192, 79, 186, 104),
                ("C", 165, 98, 153, 92, 144, 81),
                ("C", 134, 96, 116, 98, 101, 103)], HAIR)
        p.curve((108, 91), (117, 78), (130, 75), (141, 77), HAIR_LIGHT, 1.2)
        p.curve((152, 76), (164, 77), (177, 85), (180, 94), HAIR_LIGHT, 1.2)
        p.line([(90, 73), (199, 74)], GOLD, 2)
        _flower(p, 104, 76, 5, "#D2ACB3", GOLD_LIGHT)
        _flower(p, 181, 76, 5.5, "#D9BBC7", GOLD_LIGHT)
        for x in (98, 190):
            p.line([(x, 83), (x - 1, 103)], GOLD, .8)
            p.ellipse((x - 3, 101, x + 2, 109), "#DDD3BA", GOLD, .7)
        for x in (100, 187):
            p.ellipse((x, 128, x + 4, 137), GOLD_LIGHT)
    elif name == "周瑜":
        p.ellipse((127, 40, 160, 75), HAIR, HAIR_LIGHT, .8)
        p.path([("M", 100, 102), ("C", 98, 79, 111, 69, 134, 69),
                ("C", 159, 62, 187, 75, 188, 103),
                ("C", 164, 95, 149, 86, 142, 79),
                ("C", 130, 92, 114, 97, 100, 102)], HAIR)
        p.polygon([(126, 62), (129, 51), (159, 51), (164, 64), (147, 72)], "#A46858", "#835D4F", .8)
        p.line([(130, 54), (157, 54)], GOLD, 1.4)
        p.ellipse((140, 56, 151, 67), GOLD_LIGHT)
        p.curve((107, 92), (118, 77), (130, 74), (139, 75), HAIR_LIGHT, 1.2)
    elif name == "华佗":
        p.ellipse((126, 45, 159, 76), "#DFDECA", "#B9BEAA", .8)
        for offset in (0, 6, 12):
            p.curve((132 + offset, 52), (128 + offset, 61), (132 + offset, 66), (139 + offset, 69), PAPER, 1)
        p.path([("M", 100, 101), ("C", 100, 75, 119, 66, 143, 70),
                ("C", 167, 66, 188, 80, 187, 104),
                ("C", 176, 92, 164, 86, 145, 83),
                ("C", 126, 86, 111, 92, 100, 101)], "#DADBC9")
        p.curve((104, 85), (123, 74), (166, 75), (181, 89), "#B8AB86", 4)
        p.curve((105, 84), (126, 75), (164, 76), (178, 86), GOLD_LIGHT, 1)
    else:
        p.path([("M", 99, 103), ("L", 106, 76), ("L", 122, 70),
                ("L", 123, 48), ("L", 157, 56), ("L", 164, 73),
                ("L", 183, 84), ("L", 190, 104),
                ("C", 158, 94, 125, 93, 99, 103)], dark, _mix(dark, INK, .25), 1)
        p.polygon([(111, 80), (126, 75), (126, 55), (141, 60), (139, 91), (105, 98)], main)
        p.curve((105, 98), (128, 90), (160, 92), (184, 101), "#C7B58E", 2)
        if name == "庞统":
            p.curve((99, 90), (89, 88), (90, 103), (93, 110), HAIR, 3)
            p.curve((185, 93), (198, 94), (191, 111), (196, 120), HAIR, 3)


def _fan(p, feminine=False):
    if feminine:
        p.line([(220, 201), (215, 239)], "#9D8869", 3)
        p.ellipse((196, 163, 251, 216), "#E0CED9", "#AE9EAC", 1.1)
        p.ellipse((200, 167, 247, 212), "#EBDDE2", "#D2BFCE", .7)
        p.curve((207, 202), (220, 188), (231, 189), (239, 177), "#9FAE97", 1)
        p.curve((218, 191), (211, 187), (211, 182), (213, 178), "#9FAE97", 1)
        for x, y in ((218, 188), (235, 183), (213, 179)):
            _flower(p, x, y, 2.8, "#C9A1B0", "#CDB67D")
        p.line([(214, 231), (218, 247)], "#C8A5AD", 1.5)
        return
    pivot = (219, 217)
    p.line([pivot, (212, 244)], "#A7926D", 3)
    for index, angle in enumerate((-150, -132, -114, -96, -78, -60, -42)):
        rad = math.radians(angle)
        length = 56 + (3 - abs(index - 3)) * 3
        vx, vy = math.cos(rad), math.sin(rad)
        nx, ny = -vy, vx
        tip = (pivot[0] + vx * length, pivot[1] + vy * length)
        width = 7
        first = (pivot[0] + vx * length * .43 + nx * width, pivot[1] + vy * length * .43 + ny * width)
        second = (tip[0] + nx * width, tip[1] + ny * width)
        third = (tip[0] - nx * width, tip[1] - ny * width)
        fourth = (pivot[0] + vx * length * .43 - nx * width, pivot[1] + vy * length * .43 - ny * width)
        p.path([("M", *pivot), ("C", *first, *second, *tip),
                ("C", *third, *fourth, *pivot)],
               "#F1EEDE" if index % 2 == 0 else "#DFE4D0", "#ACBCA6", .7)
        p.line([pivot, tip], "#BFC9AD", .65)
    p.ellipse((214, 212, 224, 222), GOLD, "#AF9C72", .7)


def _accessories(p, name):
    if name == "诸葛亮":
        _fan(p)
    elif name == "貂蝉":
        _fan(p, feminine=True)
    elif name == "黄忠":
        p.line([(187, 165), (279, 165)], "#B6A073", 2)
        p.polygon([(286, 165), (274, 161), (274, 169)], "#97ADA0")
        p.polygon([(188, 165), (193, 160), (202, 162), (199, 165), (202, 168), (193, 170)], "#D3D5BD")
    elif name in ("曹操", "庞统"):
        x, y = (197, 185) if name == "曹操" else (202, 191)
        p.rounded((x, y, x + 43, y + 39), 3, "#B9A279", "#8A785C", 1)
        for index in range(6):
            left = x + 2 + index * 6.3
            p.rounded((left, y + 2, left + 5, y + 37), 1.5, "#E0CBA1" if index % 2 else "#D2BC91")
            p.line([(left + 2, y + 10), (left + 2, y + 25 - index % 3 * 3)], "#9B9879", .65)
        for offset in (6, 31):
            p.line([(x, y + offset), (x + 43, y + offset)], "#8D9C7D", 1.6)
        p.ellipse((x - 6, y - 3, x + 4, y + 8), "#D9BA96")
    elif name == "华佗":
        # 药箱用木格、黄铜扣和草叶封签表现，避免现代医疗标识。
        p.line([(112, 157), (203, 218)], "#8C8263", 5)
        p.line([(112, 156), (203, 217)], "#D0BD91", 1)
        p.rounded((195, 198, 249, 247), 5, "#B89C70", "#8F7C5B", 1)
        p.rounded((193, 194, 250, 207), 4, "#CDB58A", "#9D8864", .8)
        p.line([(202, 211), (242, 211)], "#A38A64", 1)
        p.line([(202, 229), (242, 229)], "#A38A64", 1)
        p.rounded((210, 183, 234, 197), 4, None, "#9A8460", 3)
        for y in (214, 232):
            p.rounded((216, y, 230, y + 5), 2, GOLD_LIGHT, "#A99167", .6)
        p.rounded((202, 204, 208, 219), 1, GOLD)
        p.rounded((236, 204, 242, 219), 1, GOLD)
        p.line([(249, 204), (258, 176)], "#7D9279", 2)
        for x, y, sign in ((255, 189, -1), (259, 182, 1), (261, 174, -1)):
            p.path([("M", x, y), ("C", x + sign * 12, y - 3, x + sign * 14, y - 13, x + sign * 4, y - 12),
                    ("C", x, y - 8, x, y - 3, x, y)], "#8FA58A", "#6E8C76", .7)
    elif name == "周瑜":
        p.ellipse((215, 179, 233, 195), "#E6C1A0", "#C49F80", .7)


def make_character(name: str, size: int = 256, enemy: bool = False) -> Image.Image:
    """返回透明 RGBA 全身棋子；四周留白，脚下包含柔光阴影与浅木台座。

    支持关羽、赵云、张飞、黄忠、周瑜、诸葛亮、曹操、貂蝉、庞统、华佗、张辽。
    其他名字使用青绿文士造型；敌方使用朱砂台座，不抹去人物特征。
    所有形状均由本模块原创绘制，不读取图片、字体、网络或其他项目资源。
    """
    if not isinstance(name, str):
        raise TypeError("人物名称必须是字符串")
    if not isinstance(size, int) or isinstance(size, bool) or size < 1:
        raise ValueError("图片尺寸必须是正整数")
    palette = PROFILES.get(name, dict(coat="#78927B", light="#B6C7AA",
                                      dark="#536F60", cape="#90A28A", skin="#DEC1A0", armor=False))
    painter = _Painter(size)
    _base(painter, enemy)
    _weapon_back(painter, name)
    _garments(painter, name, palette)
    _arms(painter, name, palette)
    _head(painter, name, palette)
    _headwear(painter, name, palette)
    _accessories(painter, name)
    return painter.image.resize((size, size), Image.Resampling.LANCZOS)
