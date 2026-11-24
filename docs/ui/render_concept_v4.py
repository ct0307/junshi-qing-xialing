"""将新版人物、沙盘地形与游戏界面组合成静态设计稿。"""

from pathlib import Path
from effect_art_v4 import make_effect
import math
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from portrait_art_v4 import make_character
from terrain_art_v4 import make_terrain, EMPTY_SLOTS, DEPLOYED


ROOT = Path(__file__).resolve().parent
WIDTH, HEIGHT, SCALE = 1920, 1080, 2
INK, PAPER, JADE = "#193E37", "#F1EAD9", "#47795D"
RED, GOLD, MUTED = "#B65B49", "#D5B77C", "#A7BAA5"
canvas = make_terrain(scale=SCALE)
assert canvas.size == (WIDTH*SCALE, HEIGHT*SCALE)
assert canvas.mode == "RGBA"
draw = ImageDraw.Draw(canvas)
font_paths = {
    "body": "C:/Windows/Fonts/msyh.ttc",
    "bold": "C:/Windows/Fonts/msyhbd.ttc",
    "title": "C:/Windows/Fonts/simkai.ttf",
    "number": "C:/Windows/Fonts/segoeuib.ttf",
}
font_cache, art_cache = {}, {}


def font(size, role="body"):
    key = (size, role)
    if key not in font_cache:
        font_cache[key] = ImageFont.truetype(font_paths[role], round(size*SCALE))
    return font_cache[key]


def bounds(rect):
    return tuple(round(value*SCALE) for value in rect)


def rounded(rect, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(bounds(rect), round(radius*SCALE), fill=fill,
                           outline=outline, width=round(width*SCALE))


def ellipse(rect, fill, outline=None, width=1):
    draw.ellipse(bounds(rect), fill=fill, outline=outline, width=round(width*SCALE))


def line(points, fill, width=1):
    draw.line([(round(x*SCALE), round(y*SCALE)) for x, y in points],
              fill=fill, width=round(width*SCALE), joint="curve")


def polygon(points, fill):
    draw.polygon([(round(x*SCALE), round(y*SCALE)) for x, y in points], fill=fill)


def text(x, y, value, size=22, fill=PAPER, role="body", anchor="lt"):
    draw.text((round(x*SCALE), round(y*SCALE)), value, font=font(size, role),
              fill=fill, anchor=anchor)


def alpha_layer(painter, blur=0):
    global draw
    layer = Image.new("RGBA", canvas.size)
    painter(ImageDraw.Draw(layer))
    if blur:
        layer = layer.filter(ImageFilter.GaussianBlur(blur*SCALE))
    canvas.alpha_composite(layer)
    draw = ImageDraw.Draw(canvas)


def character_image(name, enemy=False):
    key = (name, enemy)
    if key not in art_cache:
        art = make_character(name, size=512, enemy=enemy).convert("RGBA")
        bbox = art.getchannel("A").getbbox()
        assert bbox, f"人物图不能为空：{name}"
        art_cache[key] = art.crop(bbox)
    return art_cache[key]


def paste_art(art, left, top, width, height, mode="contain"):
    global draw
    art = art.copy()
    target = (round(width*SCALE), round(height*SCALE))
    factor = min(target[0]/art.width, target[1]/art.height)
    if mode == "cover":
        factor = max(target[0]/art.width, target[1]/art.height)
    art = art.resize((round(art.width*factor), round(art.height*factor)), Image.Resampling.LANCZOS)
    if mode == "cover":
        cx, cy = (art.width-target[0])//2, (art.height-target[1])//2
        art = art.crop((cx, cy, cx+target[0], cy+target[1]))
    px = round(left*SCALE)+(target[0]-art.width)//2
    py = round(top*SCALE)+(target[1]-art.height)//2
    canvas.alpha_composite(art, (px, py))
    draw = ImageDraw.Draw(canvas)


def portrait(name, rect, enemy=False):
    """从同一全身图取上半身，保持头像与棋子的一致性。"""
    art = character_image(name, enemy)
    art = art.crop((0, 0, art.width, round(art.height*.77)))
    left, top, right, bottom = rect
    paste_art(art, left, top, right-left, bottom-top)


def unit(name, x, y, enemy=False, height=156):
    alpha_layer(lambda painter: painter.ellipse(bounds((x-43, y-4, x+43, y+19)),
                                                fill=(32, 56, 29, 48)), blur=6)
    ellipse((x-42, y-12, x+42, y+13), "#698F6A" if not enemy else "#986A54")
    ellipse((x-41, y-17, x+41, y+7), "#BDD0A1" if not enemy else "#D2A37D", "#819A6D", 1)
    art = character_image(name, enemy)
    width = height*art.width/art.height
    paste_art(art, x-width/2, y-height+9, width, height)
    label_width = max(62, len(name)*21+20)
    rounded((x-label_width/2, y+22, x+label_width/2, y+51), 10,
            "#EFE9D6" if not enemy else "#A85545")
    text(x, y+36, name, 20, PAPER if enemy else INK, "bold", "mm")


def coin(x, y, radius=12):
    ellipse((x-radius, y-radius+2, x+radius, y+radius+2), "#977244")
    ellipse((x-radius, y-radius-1, x+radius, y+radius-1), GOLD, "#EAD1A0", 1)
    rounded((x-3, y-5, x+3, y+3), 1, "#87734F")


def shield(x, y):
    polygon([(x-12, y-14), (x+12, y-14), (x+12, y+1), (x, y+15), (x-12, y+1)], "#89AD8C")
    line([(x-5, y-3), (x-1, y+2), (x+7, y-7)], "#E8EBD4", 2)


# 选中范围位于人物与道路下层，仅突出当前操作。
alpha_layer(lambda painter: painter.ellipse(bounds((876, 253, 1184, 561)),
                                            fill=(117, 169, 112, 25),
                                            outline=(70, 126, 79, 160), width=2*SCALE))
ellipse((984, 392, 1076, 427), None, "#C5B37C", 2)


# 敌兵与少量火攻反馈，让战场保持有节奏的动势。
def soldier(x, y, variant=0):
    ellipse((x-17, y+3, x+17, y+15), "#B8A37C")
    polygon([(x-13, y-18), (x+11, y-18), (x+16, y+8), (x-15, y+8)],
            "#9E5A48" if variant % 2 else "#A96850")
    line([(x-7, y+8), (x-9, y+18)], "#665A44", 5)
    line([(x+7, y+8), (x+10, y+17)], "#665A44", 5)
    ellipse((x-10, y-38, x+10, y-17), "#D5AD84", "#8B7758", 1)
    polygon([(x-14, y-30), (x-9, y-43), (x+7, y-45), (x+15, y-32)], "#786A53")
    line([(x-13, y-30), (x+14, y-31)], "#AD9D78", 2)
    ellipse((x-14, y-12, x+3, y+8), "#A48E62", "#D7BF8D", 1)
    line([(x+19, y-35), (x+19, y+14)], "#82714E", 2)
    polygon([(x+19, y-45), (x+15, y-33), (x+23, y-33)], "#ADB8A2")


for index, position in enumerate(((175, 350), (255, 350), (333, 354), (442, 412),
                                 (233, 720), (315, 720), (440, 677), (585, 620),
                                 (1324, 560), (1410, 560), (1495, 560))):
    soldier(*position, index)


def fire_patch(x, y):
    paste_art(make_effect('fire', 3, 512), x-76, y-126, 152, 152)


fire_patch(1410, 560)
deployed = [("关羽", 1030, 407), ("周瑜", 1365, 458),
            ("张飞", 810, 560), ("黄忠", 1585, 690)]
for name, x, y in deployed:
    unit(name, x, y, height=153 if name != "张飞" else 161)
unit("吕布", 1184, 524, enemy=True, height=184)

# 刀光使用同一套透明序列中的中间帧。
paste_art(make_effect('slash', 3, 512), 1025, 388, 205, 205)


# 顶部信息组：资源、城防、波次与当前敌将有不同视觉权重。
rounded((32, 25, 88, 92), 12, "#A85545", "#C58268", 1)
text(60, 59, "演", 36, "#F2DEB5", "title", "mm")
text(111, 22, "虎牢关", 40, PAPER, "title")
text(114, 77, "第二章  ·  关前演武", 18, MUTED)

rounded((482, 28, 677, 101), 17, "#2C5444", "#54705A", 1)
coin(514, 63, 13)
text(540, 39, "军饷", 16, "#B9CCB0")
text(538, 58, "320", 33, "#F2D89F", "number")

shield(719, 65)
text(745, 31, "城池耐久", 17, MUTED)
text(745, 56, "18 / 20", 30, PAPER, "number")
rounded((745, 93, 884, 97), 2, "#315348")
rounded((745, 93, 870, 97), 2, "#8CA67F")

text(933, 31, "当前波次", 17, MUTED)
text(934, 56, "12 / 12", 30, PAPER, "number")
for index in range(12):
    rounded((934+index*13, 94, 941+index*13, 98), 2,
            "#C4B47D" if index < 12 else "#416052")

line([(1136, 31), (1136, 98)], "#416052", 1)
portrait("吕布", (1157, 28, 1223, 96), True)
text(1235, 29, "最终敌将", 15, "#D5AE8E")
text(1234, 53, "吕布", 27, PAPER, "bold")
text(1628, 34, "虎牢飞将", 17, "#B9BA9F", anchor="rt")
rounded((1334, 69, 1628, 79), 5, "#476051")
rounded((1334, 69, 1546, 79), 5, "#BC7056")
text(1628, 94, "4200 / 6000", 15, MUTED, "number", "rb")

for rect in ((1698, 29, 1770, 99), (1790, 29, 1862, 99)):
    rounded(rect, 20, "#2B5143", "#55725C", 1)
text(1734, 64, "1×", 24, PAPER, "number", "mm")
line([(1818, 51), (1818, 77)], PAPER, 4)
line([(1833, 51), (1833, 77)], PAPER, 4)


# 人物栏：当前选中、已出阵和可招募状态分别处理。
rounded((24, 881, 1896, 1056), 27, "#173B32", "#47634D", 1)
roster = ["关羽", "赵云", "张飞", "黄忠", "周瑜", "诸葛亮", "曹操", "貂蝉", "庞统", "华佗"]
roles = ["单体破阵", "直线穿透", "怒吼控制", "远程补漏", "持续火攻", "阵法减速"]
costs = [60, 65, 55, 70, 80, 75]
assert len(EMPTY_SLOTS) + len(deployed) == 34
assert len(set(EMPTY_SLOTS)) == len(EMPTY_SLOTS) == 30
assert {tuple(entry[1:]) for entry in deployed} == set(DEPLOYED)
assert len(roster) == len(set(roster)) == 10
assert not set(roster) & {"张辽", "吕布"}, "本关 Boss 不得进入己方阵容"
deployed_names = {entry[0] for entry in deployed}
for index, name in enumerate(roster[:6]):
    x, y, width, height = 48+index*214, 900, 200, 137
    selected = name == "关羽"
    recruited = name in deployed_names
    fill = "#497054" if selected else ("#DDDCC6" if recruited else "#EFE6D0")
    border = "#CEB77E" if selected else "#91A17B"
    rounded((x, y+3, x+width, y+height+3), 15, "#0F2A24")
    rounded((x, y, x+width, y+height), 15, fill, border, 2 if selected else 1)
    if selected:
        line([(x+10, y+26), (x+10, y+12), (x+24, y+12)], "#E4D09E", 2)
        line([(x+width-24, y+height-12), (x+width-10, y+height-12),
              (x+width-10, y+height-26)], "#E4D09E", 2)
    portrait(name, (x+4, y+10, x+91, y+133))
    text(x+100, y+17, name, 27, PAPER if selected else INK, "bold")
    text(x+102, y+56, roles[index], 16, "#D3DDC4" if selected else "#748268")
    if recruited:
        rounded((x+99, y+94, x+183, y+122), 8, "#3D6148" if selected else "#C2CFB0")
        text(x+141, y+108, "已出阵", 17, "#E6ECD7" if selected else "#4B7152", anchor="mm")
    else:
        coin(x+111, y+109, 8)
        text(x+128, y+91, str(costs[index]), 27, "#9B763C", "number")

rounded((1334, 900, 1410, 1037), 16, "#284E3E", "#4B6E50", 1)
text(1372, 946, "›", 47, PAPER, anchor="mm")
text(1372, 1009, "6 / 10", 17, "#B5C8A7", "number", "mm")
line([(1425, 905), (1425, 1034)], "#49644D", 1)

text(1450, 900, "关羽", 35, PAPER, "title")
rounded((1554, 906, 1644, 936), 8, "#31543F")
text(1599, 921, "贰阶", 19, "#CFC79F", anchor="mm")
text(1452, 950, "升至叁阶  ·  斩击伤害 +22%", 18, "#BECBAF")
rounded((1450, 988, 1855, 1036), 13, "#D2B77D", "#E2CD9A", 1)
text(1652, 1012, "升级  120", 26, INK, "bold", "mm")


output = ROOT / "battle-ui-concept-v4.png"
canvas.convert("RGB").resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS).save(output)
print(f"输出：{output}")
print(f"尺寸：{WIDTH}×{HEIGHT}，阵位：34，本局名单：{len(roster)}，同关 Boss：已排除")

