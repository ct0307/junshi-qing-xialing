"""第四稿人物材质精修；七位主角单独精修，其他已有人物沿用第三稿。

保留第三稿轮廓、原著特征与纸艺沙盘比例。所有高光按左上光源绘制。
导入只提供函数，不写文件；使用现有 Pillow 与第二、三稿绘图工具。
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

_spec = importlib.util.spec_from_file_location("_portrait_v4_base", Path(__file__).with_name("portrait_art_v3.py"))
_v3 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_v3)
_v2 = _v3._v2
_mix = _v2._mix

__all__ = ["make_character", "REFINED_NAMES", "PORTRAIT_BOX"]
REFINED_NAMES = tuple(_v3._FACES)
# 320 单位坐标，适合后续导出统一的方形头像；吕布翎羽保留于全身图。
PORTRAIT_BOX = (82, 35, 207, 160)


def _soft_shape(p, commands, color, blur=2, opacity=100, clip=None):
    """将局部软阴影合成到材质，避免全图滤镜改变轮廓。"""
    layer = _v2._Painter(round(p.resolution / 3))
    layer.path(commands, color)
    image = layer.image.filter(ImageFilter.GaussianBlur(blur * p.scale))
    alpha = image.getchannel("A").point(lambda value: value * opacity // 255)
    if clip is not None:
        alpha = ImageChops.multiply(alpha, clip)
    image.putalpha(alpha)
    p.image.alpha_composite(image)
    p.draw = ImageDraw.Draw(p.image)


def _face(p, name):
    skin, box, eye_y, eye_w, eye_h, tilt = _v3._FACES[name]
    lx, rx, nx, ny, my, mw = _v3._FEATURES[name]
    x1, y1, x2, y2 = box
    hair = "#D7D6BF" if name == "黄忠" else "#3F4C44"
    p.ellipse((x1 - 6, y1 - 10, x2 + 5, y2 + 7), hair)
    p.rounded((132, y2 - 14, 160, y2 + 8), 8, _mix(skin, "#80634E", .24))
    for x in (x1 - 6, x2 - 5):
        p.ellipse((x, 107, x + 12, 132), skin)
        p.curve((x + 7, 113), (x + 2, 111), (x + 3, 122), (x + 7, 125), _mix(skin, "#9B705C", .42), 1)
    if name in ("张飞", "吕布", "赵云"):
        shape = [("M", x1 + 9, y1 + 6), ("C", x1 + 25, y1 - 3, x2 - 20, y1 - 3, x2 - 6, y1 + 10),
                 ("L", x2, 124), ("C", x2, 146, 168, y2, 145, y2),
                 ("C", 120, y2, x1, 147, x1, 126), ("L", x1 + 3, 99)]
    else:
        shape = [("M", 145, y1), ("C", x2 + 6, y1, x2 + 2, 118, x2 - 3, 135),
                 ("C", x2 - 8, 149, 157, y2 - 1, 145, y2),
                 ("C", 127, y2 - 2, x1 + 3, 146, x1, 128),
                 ("C", x1 - 6, 101, x1 + 4, y1, 145, y1)]
    p.path(shape, skin, _mix(skin, "#95745B", .45), .8)
    mask = _v2._Painter(round(p.resolution / 3))
    mask.path(shape, "white")
    clip = mask.image.getchannel("A")
    _soft_shape(p, [("M", 179, 87), ("C", 198, 115, 191, 147, 155, y2 + 6),
                   ("L", 170, 126), ("L", 174, 94)], _mix(skin, "#735A46", .67), 5, 108, clip)
    _soft_shape(p, [("M", 113, 95), ("C", 123, 79, 145, 83, 151, 99),
                   ("C", 148, 111, 127, 113, 113, 110)], _mix(skin, "#FFF3DA", .6), 5, 150, clip)
    # 眉骨与鼻侧构成缩小后仍能辨认的面部体积。
    for x in (lx, rx):
        p.curve((x - 10, eye_y - 6), (x - 4, eye_y - 9), (x + 5, eye_y - 8),
                (x + 10, eye_y - 4), _mix(skin, "#A87E60", .26), 2.2)
    p.path([("M", nx + 1, eye_y - 1), ("L", nx + 7, ny - 1),
            ("C", nx + 3, ny + 2, nx - 1, ny + 1, nx - 2, ny - 1)], _mix(skin, "#90644E", .25))
    p.curve((nx - 1, eye_y + 3), (nx - 2, eye_y + 10), (nx - 5, ny), (nx + 2, ny),
            _mix(skin, "#8E614E", .53), 1)
    p.curve((nx - 2, eye_y + 2), (nx - 3, eye_y + 8), (nx - 6, ny - 4),
            (nx - 3, ny - 3), _mix(skin, "#FBE4C7", .46), 1.4)
    for x in (116, 167):
        _soft_shape(p, [("M", x - 7, 127), ("C", x + 1, 123, x + 11, 128, x + 10, 133),
                       ("C", x + 5, 137, x - 5, 134, x - 7, 127)], "#BB7E69", 3, 41, clip)
    _v3._brows(p, name, eye_y - (11 if name == "张飞" else 10), (lx, rx))
    _v3._eye(p, lx, eye_y, eye_w, eye_h, tilt, name == "张飞")
    _v3._eye(p, rx, eye_y, eye_w, eye_h, -tilt, name == "张飞")
    for x in (lx, rx):
        p.curve((x - eye_w / 2 + 2, eye_y + eye_h * .65), (x - 2, eye_y + eye_h * .82),
                (x + 3, eye_y + eye_h * .8), (x + eye_w / 2 - 1, eye_y + eye_h * .55),
                _mix(skin, "#F9E4C8", .38), .8)
    if name == "黄忠":
        for x, sign in ((110, -1), (176, 1)):
            p.line([(x, 119), (x + sign * 5, 117)], "#AF8D70", .7)
            p.line([(x, 124), (x + sign * 4, 127)], "#AF8D70", .7)
        p.curve((124, 102), (137, 100), (156, 100), (165, 102), "#B69878", .65)
    _v3._beard(p, name)
    _beard_finish(p, name)
    p.curve((nx - mw / 2, my), (nx - 3, my - .8), (nx + 4, my + .6),
            (nx + mw / 2, my - .3), "#8D4940" if name == "关羽" else _mix(skin, "#8E554B", .6), 1.4)


def _beard_finish(p, name):
    white = name == "黄忠"
    shadow, shine = ("#ACB09F", "#F6EFDA") if white else ("#32423B", "#899786")
    if name in ("关羽", "黄忠"):
        end = 209 if name == "关羽" else 187
        p.path([("M", 166, 151), ("C", 168, 170, 155, end - 9, 146, end),
                ("C", 162, end - 4, 174, 170, 175, 146)], shadow)
        for index, x in enumerate((121, 127, 134, 141, 150, 159, 166)):
            tip = 146 + (x - 146) * .22
            p.curve((x, 151 + index % 2 * 2), (x - 3, 166), (tip, end - 17),
                    (tip, end - 5 - index % 2 * 2), shine, .6)
    elif name == "张飞":
        for x, y in ((114, 145), (120, 151), (128, 157), (139, 160), (150, 160), (162, 156), (173, 146)):
            p.curve((x, y), (x + 3, y + 3), (x + 4, y + 6), (x + 1, y + 10), shine, .7)
        p.curve((164, 152), (165, 160), (157, 170), (151, 178), shadow, 2.1)
    elif name == "诸葛亮":
        for x, tip in ((132, (135, 188)), (145, (146, 199)), (158, (157, 188))):
            p.curve((x - 1, 156), (x - 2, 169), (tip[0] - 1, 182), tip, shine, .55)


def _cloth_and_armor(p, name, palette):
    main, light, dark = (palette[key] for key in ("coat", "light", "dark"))
    cape = palette["cape"]
    # 内侧披风褶皱压暗，保留上缘与外沿的纸雕亮面。
    p.path([("M", 179, 164), ("C", 189, 191, 187, 224, 205, 253),
            ("L", 191, 255), ("C", 180, 229, 180, 201, 174, 179)], _mix(cape, "#3D4F47", .35))
    p.curve((105, 179), (100, 205), (94, 235), (84, 251), _mix(cape, "#F2ECD6", .31), 1.3)
    p.curve((108, 183), (103, 213), (101, 238), (96, 255), _mix(cape, "#4F5B4D", .18), 1)
    if palette["armor"]:
        armor = _mix(main, _v2.STEEL, .35)
        if name == "吕布":
            armor = "#89796D"
        # 甲片上下明暗分离；小铆钉维持较低对比，避免远看变成噪点。
        for x, tilt in ((111, -4), (129, -1), (149, 0), (169, 4)):
            p.polygon([(x + 2, 218), (x + 14, 218), (x + 16 + tilt, 246),
                       (x + 8 + tilt, 250), (x + tilt, 244)], armor)
            p.line([(x + 1, 216), (x + 14, 216), (x + 15 + tilt, 244)], _mix(armor, dark, .55), .9)
            for y in (225, 233, 241):
                p.line([(x + 2, y), (x + 13, y)], _mix(armor, light, .52), .85)
                p.line([(x + 3, y + 1), (x + 13, y + 1)], _mix(armor, dark, .27), .7)
            for xx in (x + 3, x + 11):
                p.ellipse((xx, 219, xx + 1.4, 220.4), "#DAC493")
        p.curve((120, 175), (135, 184), (153, 184), (176, 175), _mix(armor, dark, .6), 1.5)
        p.curve((122, 173), (135, 181), (151, 181), (173, 173), _mix(light, "#F9F0D5", .55), 1)
        for row in range(3):
            for column in range(5):
                x, y = 123 + column * 10 + (row % 2) * 3, 182 + row * 8
                if 137 < x < 159:
                    continue
                p.line([(x + 1, y), (x + 6, y)], _mix(armor, light, .5), .7)
                p.ellipse((x + 3, y + 2, x + 4.3, y + 3.3), _mix(armor, dark, .3))
        for x in (100, 175):
            p.curve((x - 3, 158), (x + 6, 155), (x + 15, 157), (x + 20, 162), _mix(light, "#F5E7C2", .4), .9)
        p.curve((142, 183), (143, 180), (150, 180), (153, 183), "#F2DDB0", .8)
        p.line([(139, 218), (153, 218)], "#E7CFA1", .7)
    else:
        # 鹤氅交领投影与垂直丝绸折光，少量线条延续原稿风格。
        p.path([("M", 147, 169), ("L", 161, 153), ("L", 166, 157), ("L", 148, 181)], _mix(main, dark, .6))
        p.curve((128, 219), (121, 240), (126, 253), (122, 263), _mix(light, "#F9F3DE", .5), 1.5)
        p.curve((157, 217), (159, 235), (158, 250), (161, 266), _mix(main, dark, .38), 1.2)
        p.curve((101, 260), (125, 270), (175, 270), (195, 262), "#C5C8AA", 1)


def _weapon_finish(p, name):
    if name in ("关羽", "赵云", "张飞", "吕布"):
        for y in (118, 126, 134, 142, 150):
            x = 235 - (y - 72) * 6 / 203
            p.line([(x - 2.2, y), (x + 2.2, y + 1)], "#9D8864", .9)
        p.line([(231, 209), (229.3, 264)], "#D8C595", .7)
    if name == "关羽":
        p.curve((240, 88), (260, 75), (269, 53), (270, 30), "#FFFAE7", 1)
        p.curve((238, 58), (253, 55), (261, 46), (265, 41), "#4D7B6C", .9)
    elif name in ("赵云", "吕布"):
        x = 235 if name == "赵云" else 239
        p.line([(x, 24), (x - 2, 57)], "#FBF6E2", 1)
    elif name == "张飞":
        p.curve((241, 32), (233, 45), (251, 48), (239, 68), "#EFF3E7", .9)
    elif name == "黄忠":
        p.curve((221, 84), (255, 106), (268, 156), (251, 202), "#EDD5A4", .9)
        for y in range(150, 178, 4):
            p.line([(249, y), (255, y + 1)], "#8C704D", .65)


def _headwear_finish(p, name):
    if name in ("赵云", "黄忠"):
        steel = name == "赵云"
        light, dark = ("#F5F3DF", "#6D9388") if steel else ("#F4E1AC", "#8B744C")
        p.curve((104, 89), (103, 71), (119, 56), (137, 54), light, 1.3)
        p.curve((171, 70), (180, 78), (183, 89), (183, 96), dark, 1.4)
        for x in (109, 122, 167, 180):
            p.ellipse((x, 95, x + 2, 97), light)
        p.line([(140, 57), (139, 84)], light, 1)
    elif name == "诸葛亮":
        p.curve((111, 67), (116, 63), (123, 60), (128, 61), "#A5B6A0", 1)
        p.line([(130, 64), (122, 88)], "#68887B", .8)
        p.line([(170, 64), (179, 87)], "#8BA18D", .8)
    elif name == "关羽":
        p.curve((110, 83), (117, 73), (123, 69), (133, 65), "#ABC1A1", .9)
        p.ellipse((140, 85, 144, 88), "#F9E6B8")
    elif name in ("周瑜", "吕布"):
        for offset in (0, 4, 8):
            p.curve((105 + offset, 92), (116 + offset, 80), (124 + offset, 77), (135 + offset * .6, 76), "#67786A", .65)
        if name == "吕布":
            p.line([(125, 65), (129, 50), (136, 56)], "#F2DCAB", .8)
            p.ellipse((141, 61, 145, 64), "#F5E7C2")
    elif name == "张飞":
        for x, y in ((108, 86), (123, 74), (141, 65), (166, 71), (180, 85)):
            p.line([(x, y), (x + 3, y - 6)], "#77877A", .8)


def make_character(name: str, size: int = 256, enemy: bool = False) -> Image.Image:
    """返回透明全身棋子。七位精修，曹操等原有角色复用第三稿。"""
    if not isinstance(name, str):
        raise TypeError("人物名称必须是字符串")
    if not isinstance(size, int) or isinstance(size, bool) or size < 1:
        raise ValueError("图片尺寸必须是正整数")
    if name not in REFINED_NAMES:
        return _v3.make_character(name, size, enemy)
    palette = dict(_v2.PROFILES.get(name, _v2.PROFILES["张辽"]))
    if name == "吕布":
        palette.update(coat="#A76D5D", light="#CB9A7D", dark="#65534B", cape="#AD705F")
    palette["skin"] = _v3._FACES[name][0]
    p = _v2._Painter(size)
    _v2._base(p, enemy)
    _v2._weapon_back(p, "张辽" if name == "吕布" else name)
    if name == "吕布":
        p.path([("M", 233, 64), ("C", 221, 64, 213, 60, 206, 53),
                ("C", 205, 73, 217, 87, 233, 88)], "#BAC3AE", "#7C826E", .8)
        p.curve((210, 58), (210, 73), (221, 81), (232, 82), "#EFF0DD", 1.1)
    _weapon_finish(p, name)
    _v2._garments(p, name, palette)
    _cloth_and_armor(p, name, palette)
    if name == "吕布":
        _v3._lubu_armor(p)
    _v2._arms(p, name, palette)
    _face(p, name)
    _v3._lubu_crown(p) if name == "吕布" else _v2._headwear(p, name, palette)
    _headwear_finish(p, name)
    _v2._accessories(p, name)
    return p.image.resize((size, size), Image.Resampling.LANCZOS)


if __name__ == "__main__":
    # 仅显式执行时生成一次审图板；供复核大图细节与约 150 像素的战场轮廓。
    from PIL import ImageFont

    root = Path(__file__).resolve().parent
    sheet = Image.new("RGB", (2100, 900), "#193E37")
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc", 28)
    body = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    for index, name in enumerate(REFINED_NAMES):
        x = index * 300
        draw.rounded_rectangle((x + 12, 35, x + 288, 855), 20, fill="#EEE6D1")
        art = make_character(name, 600, name == "吕布")
        sheet.paste(art.resize((350, 350), Image.Resampling.LANCZOS), (x - 25, 65), art.resize((350, 350), Image.Resampling.LANCZOS))
        draw.text((x + 150, 420), name, font=title, fill="#193E37", anchor="mm")
        face = art.crop(tuple(round(v * 600 / 320) for v in PORTRAIT_BOX)).resize((200, 200), Image.Resampling.LANCZOS)
        sheet.paste(face, (x + 50, 460), face)
        small = make_character(name, 150, name == "吕布")
        sheet.paste(small, (x + 75, 680), small)
    output = root / "character-check-v4.png"
    sheet.save(output)
    print(f"七位人物精修审图板：{output}")
