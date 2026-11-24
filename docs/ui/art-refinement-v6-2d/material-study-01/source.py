"""独立局部材质实验：只输出研究图，不写正式地图或既有样稿。"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "UnityGame/Assets/Resources/Game"
CROP = (290, 242, 1000, 582)
SIZE = (CROP[2] - CROP[0], CROP[3] - CROP[1])
SCALE = 3


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_source(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Brush:
    """世界坐标局部笔刷，细节超采样后统一缩小。"""

    def __init__(self, image=None):
        self.image = image if image is not None else Image.new("RGBA", (SIZE[0] * SCALE, SIZE[1] * SCALE))
        self.draw = ImageDraw.Draw(self.image)

    def points(self, points):
        return [(round((x - CROP[0]) * SCALE), round((y - CROP[1]) * SCALE)) for x, y in points]

    def polygon(self, points, fill):
        self.draw.polygon(self.points(points), fill=fill)

    def line(self, points, fill, width=1):
        self.draw.line(self.points(points), fill=fill, width=max(1, round(width * SCALE)), joint="curve")

    def ellipse(self, bounds, fill):
        points = self.points((bounds[:2], bounds[2:]))
        self.draw.ellipse((*points[0], *points[1]), fill=fill)

    def curve(self, points, fill, width=1):
        a, b, c, d = points
        sampled = []
        for step in range(19):
            t = step / 18
            sampled.append(tuple((1-t)**3*a[i] + 3*(1-t)**2*t*b[i] + 3*(1-t)*t*t*c[i] + t**3*d[i] for i in (0, 1)))
        self.line(sampled, fill, width)


def blend(canvas, brush, blur=0):
    layer = brush.image
    if blur:
        layer = layer.convert("RGBa").filter(ImageFilter.GaussianBlur(blur * SCALE)).convert("RGBA")
    canvas.alpha_composite(layer)


def blade(brush, root, tip, width, color):
    x, y = root
    tx, ty = tip
    brush.polygon(((x, y), ((x+tx)/2-width, (y+ty)/2), (tx, ty), ((x+tx)/2+width*.3, (y+ty)/2+1)), color)


def grass(brush, x, y, rng, height=9, light=False):
    colors = ("#547645", "#7C974F", "#B7BA70") if light else ("#557544", "#809653", "#A4AE67")
    for index in range(rng.randint(3, 6)):
        dx = rng.uniform(-5, 5)
        h = height * rng.uniform(.55, 1.15)
        blade(brush, (x+dx, y+rng.uniform(-1, 1)), (x+dx+rng.uniform(-6, 5), y-h), rng.uniform(.9, 1.7), colors[index % 3])


def wheat(brush, x, y, rng):
    """以有宽度的叶、谷粒和前后明暗保住缩略轮廓，不靠亚像素细线。"""
    for index in range(5):
        dx = (index-2)*2.3 + rng.uniform(-1, 1)
        h = rng.uniform(20, 30)
        lean = dx * .5 + rng.uniform(-2, 2)
        tx, ty = x+dx+lean, y-h
        brush.line(((x+dx, y), (tx, ty+3)), "#8E7139", 1.6)
        blade(brush, (x+dx, y-2), (x+dx-4, y-h*.58), 1.5, "#BD963E")
        blade(brush, (x+dx, y-4), (x+dx+6, y-h*.72), 1.45, "#E0B752")
        for grain in range(4):
            yy = ty+grain*3.1
            bend = math.sin(grain*.5)*1.8
            brush.ellipse((tx+bend-2.2, yy, tx+bend+.7, yy+4.4), "#B88A34")
            brush.ellipse((tx+bend-.9, yy-.6, tx+bend+1.7, yy+3.7), "#F4D47B" if index % 2 else "#E9C060")
        brush.line(((tx-.3, ty+2), (tx-.2, ty-2)), "#F6D98A", .8)


def field(brush, center, width, rows, rng):
    x, y = center
    # 裸土只在根部露出，不先画高亮的完整田板。
    for row in range(rows):
        yy = y+row*11
        start = x-width/2+rng.uniform(0, 6)
        end = x+width/2-rng.uniform(0, 8)
        brush.curve(((start, yy+3), (x-24, yy+9), (x+32, yy+6), (end, yy-1)), "#7B7842", 7)
        brush.curve(((start+5, yy+2), (x-24, yy+6), (x+32, yy+4), (end-7, yy-2)), "#9A8B49", 3)
        count = int(width/11)
        for column in range(count):
            xx = start+5+column*(end-start-8)/max(1, count-1)
            xx += rng.uniform(-1.6, 1.6)
            wheat(brush, xx, yy+rng.uniform(-1.6, 1.6), rng)
    for xx, yy in ((x-width/2-4, y+rows*6), (x+width/2+1, y+rows*8), (x-width/2+14, y+rows*10+5)):
        grass(brush, xx, yy, rng, 8)


def vegetables(brush, x, y, rng):
    for row in range(3):
        yy = y+row*12
        brush.curve(((x, yy+3), (x+30, yy+9), (x+63, yy+4), (x+100, yy-3)), "#7D754A", 8)
        brush.curve(((x+2, yy+1), (x+30, yy+5), (x+65, yy+1), (x+97, yy-4)), "#A18B57", 3)
        for column in range(8):
            xx = x+7+column*12+rng.uniform(-1, 1)
            by = yy+math.sin(column*.45)*2
            for dx, dy, width, color in ((-8,-9,3.3,"#4A6F45"),(7,-11,3.4,"#4B7C4A"),(-4,-15,3,"#6A9854"),(3,-13,2.8,"#A4B567")):
                blade(brush, (xx, by+2), (xx+dx, by+dy), width, color)


def study(level):
    rng = random.Random(2026110819)
    base = load_source("_material_original_ground", ROOT / "docs/ui/terrain_art_v2.py")
    # 沿用正式黄巾底材的连续低频结构及暖色洗染，不在阵位周围挖空。
    ground, _ = base._ground(1, random.Random(20261104 + sum(map(ord, "huangjin"))))
    image = ground.crop(CROP).convert("RGBA").resize((SIZE[0]*SCALE, SIZE[1]*SCALE), Image.Resampling.BICUBIC)
    image.alpha_composite(Image.new("RGBA", image.size, (177, 165, 101, 55)))

    washes = Brush()
    for _ in range(140):
        x, y = rng.uniform(285, 1010), rng.uniform(345, 590)
        rx, ry = rng.uniform(7, 31), rng.uniform(3, 11)
        washes.ellipse((x-rx, y-ry, x+rx, y+ry), rng.choice(((111,132,73,22),(184,181,105,23),(63,107,68,16))))
    blend(image, washes, 3)

    # 道路中心和宽度取正式数据，仅研究材料；清晰路肩与草根相互咬合。
    road = Brush()
    road.line(((280, 302), (1010, 302)), "#9D8D5A", 85)
    road.line(((280, 300), (1010, 300)), "#C4A270", 80)
    road.line(((280, 300), (1010, 300)), "#D8B786", 74)
    blend(image, road)
    detail = Brush()
    for _ in range(85):
        x, y = rng.uniform(285, 1010), rng.uniform(266, 335)
        detail.ellipse((x-6, y-1, x+rng.uniform(4, 20), y+1.6), rng.choice(((242,213,160,42),(147,108,61,29))))
    blend(image, detail, 1.2)
    shoulder = Brush()
    for side in (-1, 1):
        for x in range(289, 1010, 9):
            yy = 300+side*40+rng.uniform(-1, 1.5)
            shoulder.polygon(((x-4, yy+side*3), (x+rng.uniform(-2,2), yy-side*rng.uniform(0,3)), (x+6, yy+side*2)), "#8D9D65")
            if rng.random() < .22:
                grass(shoulder, x, yy+side*5, rng, 5)
    blend(image, shoulder)

    # 高对比实体仍避让阵位，只有低对比底材可以连续穿过保护区。
    shadow = Brush()
    for x,y,rx,ry in ((431,423,106,35),(670,420,62,26),(909,395,64,22)):
        shadow.ellipse((x-rx,y-ry,x+rx,y+ry), (65,87,40,53))
    blend(image, shadow, 8)
    crops = Brush()
    field(crops, (430,397), 168, 4, rng)
    field(crops, (672,401), 91, 3, rng)
    vegetables(crops, 859, 387, rng)
    blend(image, crops)
    surface = Brush()
    for _ in range(95):
        x,y = rng.uniform(295,995),rng.uniform(352,578)
        if any(abs(x-s["Position"]["X"])<57 and abs(y-s["Position"]["Y"])<44 for s in level["Slots"]):
            continue
        if 328<x<535 and 365<y<457 or 616<x<725 and 369<y<447 or 845<x<970 and 365<y<440:
            continue
        if rng.random()<.32:
            grass(surface,x,y,rng,7)
        else:
            surface.line(((x-2,y),(x+2,y-1),(x+7,y)), (190,191,131,95), .8)
    blend(image,surface)
    return image.resize(SIZE,Image.Resampling.LANCZOS), [
        {"Name":"金稻主丛","Bounds":[335,365,525,446]},
        {"Name":"金稻小丛","Bounds":[617,368,726,440]},
        {"Name":"菜垄","Bounds":[848,369,970,427]},
    ]


def font(size):
    return ImageFont.truetype(str(RESOURCES / "Fonts/Chinese.otf"), size)


def board(images):
    canvas = Image.new("RGB", (1500, 1010), "#F1ECDD")
    draw = ImageDraw.Draw(canvas)
    draw.text((38,24), "局部材质实验 / 草地 · 土路 · 作物",font=font(32),fill="#23483D")
    draw.text((40,71), "同一地图区域，研究稿未接入游戏；原版与否决稿不作修饰",font=font(18),fill="#6C7866")
    labels = ("正式原图", "v07 · 已否决", "新局部实验")
    for index,(label,art) in enumerate(zip(labels,images)):
        x=35+index*493
        draw.text((x,123),label,font=font(25),fill="#244B40")
        # 与1280预览一致：先0.67纵缩，再横纵同时乘以2/3。
        native=art.resize((round(SIZE[0]*2/3),round(SIZE[1]*.67*2/3)),Image.Resampling.LANCZOS)
        canvas.paste(native,(x,168))
        draw.text((x,331),"1280画面中的显示尺寸",font=font(17),fill="#737B67")
        enlarged=art.resize((round(SIZE[0]*.65),round(SIZE[1]*.65)),Image.Resampling.LANCZOS)
        canvas.paste(enlarged,(x,399))
        draw.text((x,635),"未纵缩局部，便于检查画法",font=font(17),fill="#737B67")
    draw.line((36,688,1460,688),fill="#D0CFBA",width=1)
    draw.text((40,715),"研究目标",font=font(24),fill="#244B40")
    draw.text((40,758),"地表保持连续层次；作物以成束叶穗和根部阴影形成体积；路肩使用断续草齿。",font=font(20),fill="#465B49")
    draw.text((40,806),"局部放大检查 · 新实验",font=font(20),fill="#244B40")
    detail=images[-1].crop((40,111,258,214)).resize((436,206),Image.Resampling.LANCZOS)
    canvas.paste(detail,(805,741))
    draw.text((40,854),"范围仅为材质研究，人物和完整战场未重画。",font=font(18),fill="#737B67")
    draw.text((40,891),"尚未达到参考图的手绘完成度，不作为最终美术。",font=font(18),fill="#737B67")
    return canvas


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",default="material-study-01")
    args=parser.parse_args()
    if not args.output.replace("-", "").isalnum():
        raise ValueError("输出名只能含字母数字与连字符")
    folder=ROOT/"docs/ui/art-refinement-v6-2d"/args.output
    if folder.exists():
        raise ValueError("保留已有结果，请指定新的实验目录")
    exporter=load_source("_material_protection",ROOT/"tools/build_2d_art_samples.py")
    count=exporter.baseline_unchanged()
    inputs=[RESOURCES/"Maps/huangjin.png",RESOURCES/"database.json",RESOURCES/"Fonts/Chinese.otf",
            ROOT/"docs/ui/terrain_art_v2.py",ROOT/"docs/ui/art-refinement-v6-2d/v07/huangjin.png",Path(__file__).resolve()]
    before={str(p.relative_to(ROOT)):digest(p) for p in inputs}
    data=json.loads((RESOURCES/"database.json").read_text(encoding="utf-8-sig"))
    level=next(l for l in data["Levels"] if l["Id"]=="huangjin")
    experiment,decorations=study(level)
    layout={"RoutePointCount":sum(len(r["Points"]) for r in level["Routes"]),"SlotCount":len(level["Slots"]),"Decorations":decorations}
    exporter.verify_layout(layout,level)
    with Image.open(inputs[0]) as source:
        original=source.convert("RGB").crop(CROP)
    with Image.open(inputs[4]) as source:
        rejected=source.convert("RGB").crop(CROP)
    folder.mkdir()
    experiment.save(folder/"material-patch.png")
    board((original,rejected,experiment.convert("RGB"))).save(folder/"comparison.png")
    actual=experiment.resize((round(SIZE[0]*2/3),round(SIZE[1]*.67*2/3)),Image.Resampling.LANCZOS)
    actual.save(folder/"material-patch-1280.png")
    records=[]
    for path in sorted(folder.glob("*.png")):
        with Image.open(path) as art:
            art.load()
            records.append({"Path":path.name,"Size":list(art.size),"Sha256":digest(path)})
    if any(digest(ROOT/name)!=value for name,value in before.items()):
        raise ValueError("输入文件被意外改动")
    if exporter.baseline_unchanged()!=count:
        raise ValueError("正式工程基线条目变化")
    report={"Stage":"局部材质研究，未达到最终美术","Crop":list(CROP),"SourceCoordinates":"正式1920×1080地图坐标",
            "Projection":{"VerticalScale":.67,"PreviewScale":2/3},"Layout":layout,"Inputs":before,"Files":records,
            "FormalSourceFilesUnchanged":count,"RuntimeTested":False,"FormalResourcesWritten":False,
            "AiImageGenerationUsed":False,"NewDependenciesInstalled":False}
    (folder/"verification.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(f"局部实验已导出：{len(records)}张PNG；实体净空通过；{count}项正式基线未变。美术效果需独立目视判断。")


if __name__=="__main__":
    sys.dont_write_bytecode=True
    main()
