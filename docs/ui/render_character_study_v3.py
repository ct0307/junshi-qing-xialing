"""输出角色容貌细化展示板，供比对原著特征与画面。"""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from portrait_art_v3 import make_character


ROOT = Path(__file__).resolve().parent
WIDTH, HEIGHT, SCALE = 1920, 760, 2
image = Image.new("RGB", (WIDTH*SCALE, HEIGHT*SCALE), "#F1EDE2")
draw = ImageDraw.Draw(image)
font_paths = {"title": "C:/Windows/Fonts/simkai.ttf", "body": "C:/Windows/Fonts/msyh.ttc"}


def text(x, y, value, size, fill="#264D40", role="body", anchor="lt"):
    font = ImageFont.truetype(font_paths[role], size*SCALE)
    draw.text((x*SCALE, y*SCALE), value, fill=fill, font=font, anchor=anchor)


text(52, 35, "演义人物 · 容貌细化", 44, role="title")
text(54, 101, "从原著文字提取脸型、眉眼与冠服特征，保持统一的微缩棋兵风格。", 22, "#77836E")
characters = [
    ("关羽", "重枣面 · 丹凤眼", "卧蚕眉 · 长髯"),
    ("张飞", "豹头环眼", "燕颔虎须"),
    ("赵云", "浓眉大眼", "阔面重颐"),
    ("周瑜", "姿质风流", "仪容秀丽"),
    ("诸葛亮", "面如冠玉", "纶巾 · 鹤氅"),
    ("黄忠", "年近六旬 · 勇武", "白髯为美术取法"),
    ("吕布", "紫金冠 · 红锦袍", "兽面铠 · 画戟"),
]
for index, (name, first_line, second_line) in enumerate(characters):
    x, y, width, height = 52+index*262, 160, 244, 515
    draw.rounded_rectangle((x*SCALE, y*SCALE, (x+width)*SCALE, (y+height)*SCALE),
                           18*SCALE, fill="#F9F6EE", outline="#D8DDCE", width=SCALE)
    art = make_character(name, 960, name == "吕布")
    factor = art.width/320
    art = art.crop(tuple(round(value*factor) for value in (32, 2, 284, 235)))
    art.thumbnail((226*SCALE, 290*SCALE), Image.Resampling.LANCZOS)
    px = (x+width/2)*SCALE-art.width/2
    py = (y+24)*SCALE+(290*SCALE-art.height)/2
    image.paste(art, (round(px), round(py)), art)
    text(x+width//2, 502, name, 34, role="title", anchor="mm")
    text(x+width//2, 559, first_line, 20, anchor="mm")
    text(x+width//2, 597, second_line, 20, "#7B836F", anchor="mm")

text(54, 710, "原著依据：第1、5、7、15、38、53回。神态、材质与部分服饰细节为游戏美术改编。", 19, "#78816D")
output = ROOT / "character-study-v3.png"
image.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS).save(output)
print(f"角色细节板：{output}")
