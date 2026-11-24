"""演义沙盘人物第三稿：复用第二稿材质，重绘七位人物的容貌与神情。"""

from __future__ import annotations

import importlib.util
from pathlib import Path

from PIL import Image

_spec = importlib.util.spec_from_file_location("_portrait_base_v2", Path(__file__).with_name("portrait_art_v2.py"))
_v2 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_v2)
__all__ = ["make_character"]

# 这些是绘画设计参数；银甲、色彩及夸张比例不作为原著服饰考据。
_FACES = {
    "关羽": ("#AA6655", (100, 78, 188, 158), 118, 17, 5, 2.4),
    "赵云": ("#DFC1A3", (100, 77, 190, 158), 119, 15, 9, 0.5),
    "张飞": ("#C29778", (98, 77, 191, 159), 119, 16, 13, 0),
    "黄忠": ("#D2B18E", (102, 78, 187, 158), 119, 15, 6, 1.2),
    "周瑜": ("#E7C8AC", (107, 78, 184, 156), 117, 14, 7, 0.3),
    "诸葛亮": ("#E1C8AA", (107, 75, 184, 166), 122, 14, 7, 0.3),
    "吕布": ("#D6AE8C", (101, 77, 190, 158), 118, 16, 8, 1.8),
}
_FEATURES = {
    "关羽": (122, 164, 145, 133, 140, 17), "赵云": (121, 166, 146, 133, 142, 16),
    "张飞": (121, 168, 148, 134, 144, 20), "黄忠": (123, 164, 146, 134, 142, 16),
    "周瑜": (124, 162, 145, 132, 139, 14), "诸葛亮": (123, 163, 145, 138, 147, 15),
    "吕布": (121, 166, 147, 132, 141, 18),
}


def _eye(p, x, y, width, height, tilt, round_eye=False):
    """始终绘制有眼白、瞳孔和上眼睑的睁眼造型。"""
    edge = "#4C4F43"
    if round_eye:
        p.ellipse((x - width / 2, y - height / 2, x + width / 2, y + height / 2), "#E8DECA", edge, 1.1)
    else:
        p.path([("M", x - width / 2, y - tilt),
                ("C", x - width / 4, y - height * .7, x + width / 4, y - height * .6, x + width / 2, y + tilt),
                ("C", x + width / 4, y + height * .6, x - width / 4, y + height * .6, x - width / 2, y - tilt)],
               "#EDE2CE", edge, 1)
    radius = min(height * .43, 4.8)
    p.ellipse((x - radius, y - radius, x + radius, y + radius), "#505B4F")
    p.ellipse((x - radius * .55, y - radius * .65, x + radius * .55, y + radius * .7), "#36483E")
    p.ellipse((x - radius * .3, y - radius * .7, x + radius * .15, y - radius * .15), "#EFEAD6")
    p.curve((x - width / 2, y - tilt), (x - width / 4, y - height * .7),
            (x + width / 4, y - height * .6), (x + width / 2, y + tilt), edge, 1.3)


def _brows(p, name, y, centers):
    color = "#D4D3BB" if name == "黄忠" else "#455048"
    for x, sign in ((centers[0], 1), (centers[1], -1)):
        if name == "关羽":
            p.path([("M", x - 11, y + sign * 1.5),
                    ("C", x - 7, y - 6, x + 5, y - 6, x + 11, y + 1),
                    ("C", x + 4, y - 1, x - 5, y, x - 11, y + sign * 1.5)], color)
        elif name == "张飞":
            p.path([("M", x - 12, y - sign * 4), ("L", x + 10, y + sign * 2),
                    ("L", x + 9, y + sign * 2 + 5), ("L", x - 12, y + 1)], color)
            for offset in (-8, -3, 2, 7):
                p.line([(x + offset, y + 1), (x + offset + 1, y - 3)], "#5F6658", .7)
        elif name == "吕布":
            p.polygon([(x - 11, y - sign * 3), (x + 10, y + sign * 2),
                       (x + 7, y + sign * 2 + 3), (x - 9, y)], color)
        else:
            weight = 3.1 if name == "赵云" else (2.8 if name == "黄忠" else 1.8)
            p.curve((x - 10, y), (x - 3, y - 3), (x + 4, y - 2), (x + 10, y + .5), color, weight)


def _beard(p, name):
    white = name == "黄忠"
    base, shine = ("#D7D7C3", "#F0ECD8") if white else ("#414E44", "#718071")
    if name == "张飞":
        p.path([("M", 106, 133), ("C", 111, 146, 121, 148, 127, 145),
                ("L", 145, 149), ("L", 164, 144), ("C", 177, 146, 182, 141, 184, 133),
                ("L", 181, 157), ("L", 171, 167), ("L", 163, 165),
                ("L", 151, 180), ("L", 139, 174), ("L", 128, 177),
                ("L", 116, 162), ("L", 109, 161)], base)
        for x in (116, 127, 139, 151, 164, 175):
            p.curve((x, 148), (x + 3, 154), (x - 3, 162), (x + 1, 166), shine, .8)
    elif name == "诸葛亮":
        for start, tip in ((133, (135, 191)), (145, (146, 202)), (158, (157, 191))):
            p.path([("M", start - 4, 151), ("C", start - 3, 167, tip[0] - 6, 183, *tip),
                    ("C", tip[0] + 5, 181, start + 4, 166, start + 4, 151)], base)
            p.curve((start, 156), (start - 1, 171), (tip[0], 182), tip, shine, .65)
    elif name in ("关羽", "黄忠"):
        end = 209 if name == "关羽" else 187
        p.path([("M", 114, 135), ("C", 119, 153, 127, 154, 143, 151),
                ("C", 160, 154, 173, 147, 177, 135),
                ("C", 174, 171, 164, end - 6, 146, end),
                ("C", 130, end - 7, 117, 164, 114, 135)], base)
        for x in (125, 136, 146, 157, 167):
            p.curve((x, 153), (x - 2, 170), (146 + (x - 146) * .3, end - 19),
                    (146 + (x - 146) * .18, end - 7), shine, .75)
    if name in ("关羽", "张飞", "黄忠", "诸葛亮"):
        p.path([("M", 143, 135), ("C", 136, 132, 128, 136, 122, 141),
                ("C", 131, 143, 140, 141, 145, 138),
                ("C", 152, 143, 164, 143, 169, 139),
                ("C", 158, 137, 153, 131, 145, 135)], base)


def _head(p, name):
    skin, box, eye_y, eye_w, eye_h, tilt = _FACES[name]
    left_eye, right_eye, nose_x, nose_bottom, mouth_y, mouth_width = _FEATURES[name]
    x1, y1, x2, y2 = box
    hair = "#D8D8C4" if name == "黄忠" else "#414C45"
    p.ellipse((x1 - 6, y1 - 10, x2 + 5, y2 + 7), hair)
    p.rounded((132, y2 - 14, 160, y2 + 8), 8, _v2._mix(skin, "#AD8D71", .14))
    for x in (x1 - 6, x2 - 5):
        p.ellipse((x, 107, x + 12, 132), skin)
        p.curve((x + 7, 113), (x + 2, 112), (x + 3, 122), (x + 7, 124), _v2._mix(skin, "#AA876C", .4), .8)
    if name in ("张飞", "吕布", "赵云"):
        p.path([("M", x1 + 9, y1 + 6), ("C", x1 + 25, y1 - 3, x2 - 20, y1 - 3, x2 - 6, y1 + 10),
                ("L", x2, 124), ("C", x2, 146, 168, y2, 145, y2),
                ("C", 120, y2, x1, 147, x1, 126), ("L", x1 + 3, 99)], skin,
               _v2._mix(skin, "#A68A71", .35), .8)
    else:
        p.path([("M", 145, y1), ("C", x2 + 6, y1, x2 + 2, 118, x2 - 3, 135),
                ("C", x2 - 8, 149, 157, y2 - 1, 145, y2),
                ("C", 127, y2 - 2, x1 + 3, 146, x1, 128),
                ("C", x1 - 6, 101, x1 + 4, y1, 145, y1)], skin,
               _v2._mix(skin, "#A68A71", .3), .8)
    p.path([("M", x2 - 10, 97), ("C", x2 + 1, 123, x2 - 7, 145, 155, y2 - 5),
            ("C", 176, y2 - 7, x2 + 9, 130, x2 - 5, 103)], _v2._mix(skin, "#9B775F", .15))
    _brows(p, name, eye_y - (11 if name == "张飞" else 10), (left_eye, right_eye))
    _eye(p, left_eye, eye_y, eye_w, eye_h, tilt, name == "张飞")
    _eye(p, right_eye, eye_y, eye_w, eye_h, -tilt, name == "张飞")
    p.curve((nose_x, eye_y + 3), (nose_x - 2, eye_y + 9), (nose_x - 5, nose_bottom), (nose_x + 2, nose_bottom),
            _v2._mix(skin, "#9D745D", .55), 1)
    p.line([(nose_x + 3, nose_bottom), (nose_x + 6, nose_bottom - 1)], _v2._mix(skin, "#B18C70", .4), .7)
    if name == "黄忠":
        for x, sign in ((110, -1), (176, 1)):
            p.line([(x, 119), (x + sign * 5, 117)], "#B5997D", .7)
            p.line([(x, 124), (x + sign * 4, 127)], "#B5997D", .7)
        p.curve((122, 102), (134, 99), (155, 99), (167, 102), "#B89D7E", .7)
    _beard(p, name)
    color = "#8A3F3D" if name == "关羽" else _v2._mix(skin, "#945F51", .55)
    p.curve((nose_x - mouth_width / 2, mouth_y), (nose_x - 3, mouth_y - .8),
            (nose_x + 4, mouth_y + .6), (nose_x + mouth_width / 2, mouth_y - .3), color, 1.6)
    if name == "关羽":
        p.curve((138, 141), (143, 143), (149, 143), (153, 140), "#B97269", .8)


def _lubu_crown(p):
    # 以细羽轴、斜向羽片和垂落的末梢避免双翎被误读为兽角。
    for points in (((128, 61), (112, 3), (55, 2), (45, 78)),
                   ((158, 61), (176, 0), (229, 7), (235, 82))):
        p.curve(*points, "#A78D68", 3.8)
        for step in range(2, 18):
            t = step / 20
            a, b, c, d = points
            x = (1-t)**3*a[0]+3*(1-t)**2*t*b[0]+3*(1-t)*t*t*c[0]+t**3*d[0]
            y = (1-t)**3*a[1]+3*(1-t)**2*t*b[1]+3*(1-t)*t*t*c[1]+t**3*d[1]
            dx = 3*(1-t)**2*(b[0]-a[0])+6*(1-t)*t*(c[0]-b[0])+3*t*t*(d[0]-c[0])
            dy = 3*(1-t)**2*(b[1]-a[1])+6*(1-t)*t*(c[1]-b[1])+3*t*t*(d[1]-c[1])
            length = max(1, (dx*dx+dy*dy)**.5)
            tx, ty = dx/length, dy/length
            width = 6.2*(1-t*.65)
            for sign in (-1, 1):
                p.line([(x, y), (x-sign*ty*width-tx*7, y+sign*tx*width-ty*7)], "#CDB990", 1.1)
        p.curve(*points, "#E4D3AA", .85)
    p.path([("M", 99, 101), ("C", 98, 75, 122, 65, 145, 69),
            ("C", 170, 65, 190, 78, 190, 103),
            ("C", 171, 99, 153, 87, 145, 79),
            ("C", 132, 92, 116, 99, 99, 101)], "#424B44")
    p.curve((108, 91), (119, 76), (132, 74), (141, 75), "#667366", 1.1)
    p.polygon([(119, 69), (124, 41), (136, 49), (145, 31), (154, 49), (168, 41),
               (172, 69), (155, 79), (134, 79)], "#AA9270", "#D4BB83", 1.2)
    p.polygon([(125, 66), (129, 50), (138, 57), (145, 41), (153, 58),
               (163, 51), (166, 66), (153, 73), (136, 73)], "#857182")
    p.line([(124, 68), (167, 68)], "#DCC28E", 2)
    p.ellipse((138, 58, 152, 72), "#D9BF85", "#B3986B", .7)
    p.ellipse((142, 61, 148, 68), "#9B675E")


def _lubu_armor(p):
    p.path([("M", 130, 179), ("L", 139, 176), ("L", 147, 180), ("L", 156, 176),
            ("L", 166, 180), ("L", 162, 194), ("L", 148, 204), ("L", 134, 195)], "#A88F66", "#D4BB87", 1)
    for x, sign in ((140, 1), (156, -1)):
        p.line([(x - 4, 184 - sign), (x + 4, 184 + sign)], "#635C49", 1.8)
    p.polygon([(147, 185), (151, 191), (147, 194), (144, 190)], "#D0B781")
    p.curve((139, 194), (143, 198), (152, 198), (157, 193), "#E4D2A7", 1.2)


def make_character(name: str, size: int = 256, enemy: bool = False) -> Image.Image:
    """返回透明 RGBA 全身棋子；七位重绘，其他角色沿用第二稿。"""
    if name not in _FACES:
        return _v2.make_character(name, size, enemy)
    if not isinstance(size, int) or isinstance(size, bool) or size < 1:
        raise ValueError("图片尺寸必须是正整数")
    palette = dict(_v2.PROFILES.get(name, _v2.PROFILES["张辽"]))
    if name == "吕布":
        palette.update(coat="#A76D5D", light="#CC9D81", dark="#74584D", cape="#B77A67")
    palette["skin"] = _FACES[name][0]
    p = _v2._Painter(size)
    _v2._base(p, enemy)
    _v2._weapon_back(p, "张辽" if name == "吕布" else name)
    if name == "吕布":
        p.path([("M", 233, 64), ("C", 221, 64, 213, 60, 206, 53),
                ("C", 205, 73, 217, 87, 233, 88)], "#B6BCA5", "#827D63", .8)
    _v2._garments(p, name, palette)
    if name == "吕布":
        _lubu_armor(p)
    _v2._arms(p, name, palette)
    _head(p, name)
    if name == "吕布":
        _lubu_crown(p)
    else:
        _v2._headwear(p, name, palette)
    _v2._accessories(p, name)
    return p.image.resize((size, size), Image.Resampling.LANCZOS)
