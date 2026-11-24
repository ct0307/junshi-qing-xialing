"""导出原创2D重绘样稿与静态对比，不修改正式游戏素材。"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs/ui/art-refinement-v6-2d"
RESOURCES = ROOT / "UnityGame/Assets/Resources/Game"
PAPER = "#F3EEDD"
INK = "#214B42"
MUTED = "#6B8172"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_source(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def font(size: int):
    return ImageFont.truetype(str(RESOURCES / "Fonts/Chinese.otf"), size)


def text(image: Image.Image, value: str, position: tuple[int, int], size=25, fill=INK):
    ImageDraw.Draw(image).text(position, value, font=font(size), fill=fill)


def fit(image: Image.Image, source: Image.Image, box: tuple[int, int, int, int]):
    art = source.convert("RGBA").copy()
    art.thumbnail((box[2] - box[0], box[3] - box[1]), Image.Resampling.LANCZOS)
    image.alpha_composite(art, (box[0] + (box[2] - box[0] - art.width) // 2,
                                box[1] + (box[3] - box[1] - art.height) // 2))


def compare(folder: Path):
    canvas = Image.new("RGBA", (1920, 1400), PAPER)
    text(canvas, "2D国风 · 人物与地图重绘", (48, 28), 42)
    text(canvas, "黄巾郊野 / 关羽 / 赵云", (50, 89), 23, MUTED)
    draw = ImageDraw.Draw(canvas)
    draw.line((48, 132, 1872, 132), fill="#CCD5BD", width=2)
    for left, path, title in (
        (42, RESOURCES / "Maps/huangjin.png", "当前地图"),
        (984, folder / "huangjin.png", "2D地图重绘"),
    ):
        text(canvas, title, (left + 9, 153), 25)
        with Image.open(path) as art:
            fit(canvas, art.crop((0, 126, 1920, 866)), (left, 197, left + 894, 548))
    for index, (slug, name) in enumerate((("guan_yu", "关羽"), ("zhao_yun", "赵云"))):
        for version in range(2):
            left = 38 + (index * 2 + version) * 473
            draw.rounded_rectangle((left, 587, left + 442, 1190), radius=16,
                                   fill="#E4E8D3" if version else "#E9E5D5")
            text(canvas, name + (" · 2D重绘" if version else " · 当前素材"), (left + 20, 601), 26)
            path = folder / (slug + ".png") if version else RESOURCES / "Characters" / (slug + ".png")
            with Image.open(path) as art:
                fit(canvas, art, (left - 7, 653, left + 449, 1147))
                fit(canvas, art, (left + 164, 1208, left + 277, 1321))
    text(canvas, "下排按游戏内约113像素方框显示，检查轮廓、神态与配色。", (50, 1348), 24, MUTED)
    canvas.save(folder / "comparison-overview.png")


def gameplay(folder: Path, level: dict):
    """只合成美术效果，不模拟运行状态或冒充游戏截图。"""
    source = ROOT / "docs/ui/runtime-ui-refinement-20261108/battle-deployed-1920.png"
    with Image.open(source) as original:
        canvas = original.convert("RGBA")
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((32, 184, 1887, 823), fill="#9BAE88")
    with Image.open(folder / "huangjin.png") as original:
        terrain = original.convert("RGBA").crop((38, 144, 1882, 842))
    terrain = terrain.resize((1844, round(698 * .67)), Image.Resampling.LANCZOS)
    fade = Image.new("L", (1, terrain.height))
    fade.putdata([round(255 * min(1, y / (terrain.height * .1),
                                  (terrain.height - 1 - y) / (terrain.height * .04)))
                  for y in range(terrain.height)])
    terrain.putalpha(ImageChops.multiply(terrain.getchannel("A"), fade.resize(terrain.size)))
    canvas.alpha_composite(terrain, (38, round(218 + 144 * .67)))
    draw = ImageDraw.Draw(canvas)
    for slot in level["Slots"]:
        x, y = slot["Position"]["X"], 218 + slot["Position"]["Y"] * .67
        draw.ellipse((x - 47, y - 16, x + 47, y + 16), fill="#9DAE7B", outline="#C7D2A2", width=1)
    for slug, name, x in (("guan_yu", "关羽", 170), ("zhao_yun", "赵云", 340)):
        y = 218 + 215 * .67
        with Image.open(folder / (slug + ".png")) as original:
            sprite = original.convert("RGBA").resize((170, 170), Image.Resampling.LANCZOS)
        canvas.alpha_composite(sprite, (round(x - 170 * 360 / 768), round(y - 170 * 660 / 768)))
        draw = ImageDraw.Draw(canvas)
        draw.rounded_rectangle((x - 55, y + 18, x + 55, y + 54), radius=15, fill=PAPER)
        text(canvas, name, (round(x - 24), round(y + 17)), 24)
    # 静态合成中的两张对应头像也采用新版，避免同一武将在画面内出现两套脸。
    for slug, bounds, sample in (
        ("guan_yu", (57, 919, 143, 1020), (58, 920)),
        ("zhao_yun", (536, 919, 620, 1020), (536, 918)),
    ):
        background = canvas.getpixel(sample)
        ImageDraw.Draw(canvas).rectangle(bounds, fill=background)
        with Image.open(folder / (slug + ".png")) as original:
            portrait = original.convert("RGBA").crop((216, 21, 468, 350))
        fit(canvas, portrait, bounds)
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((49, 837, 1307, 897), radius=12, fill="#2D5746")
    text(canvas, "2D重绘样稿 · 静态合成，尚未接入试玩包", (66, 847), 25, PAPER)
    canvas.save(folder / "gameplay-art-sample-1920.png")
    canvas.resize((1280, 720), Image.Resampling.LANCZOS).save(folder / "gameplay-art-sample-1280.png")


def segment_intersects_box(a, b, box):
    """独立检查装饰矩形与扩张道路相交，不依赖绘图模块的放置判断。"""
    low, high = 0.0, 1.0
    for axis in (0, 1):
        delta = b[axis] - a[axis]
        if abs(delta) < 1e-8:
            if not box[axis] <= a[axis] <= box[axis + 2]:
                return False
            continue
        first = (box[axis] - a[axis]) / delta
        last = (box[axis + 2] - a[axis]) / delta
        low, high = max(low, min(first, last)), min(high, max(first, last))
        if low > high:
            return False
    return True


def verify_layout(report: dict, level: dict):
    if not report or not isinstance(report.get("Decorations"), list):
        raise ValueError("地图缺少装饰放置记录")
    points = sum(len(route["Points"]) for route in level["Routes"])
    if report.get("RoutePointCount") != points or report.get("SlotCount") != len(level["Slots"]):
        raise ValueError("地图布局记录与正式数据不符")
    for item in report["Decorations"]:
        x1, y1, x2, y2 = item["Bounds"]
        if not (all(math.isfinite(v) for v in (x1, y1, x2, y2)) and x1 < x2 and y1 < y2):
            raise ValueError("无效装饰范围：" + item["Name"])
        for slot in level["Slots"]:
            x, y = slot["Position"]["X"], slot["Position"]["Y"]
            if x1 < x + 55 and x2 > x - 55 and y1 < y + 42 and y2 > y - 42:
                raise ValueError("装饰侵入部署区域：" + item["Name"])
        if item.get("AllowRoad", False):
            if item.get("Role") != "terminal_gate":
                raise ValueError("只有终点城楼允许接触道路")
            continue
        expanded = (x1 - 43, y1 - 43, x2 + 43, y2 + 43)
        for route in level["Routes"]:
            route_points = [(point["X"], point["Y"]) for point in route["Points"]]
            if any(segment_intersects_box(a, b, expanded) for a, b in zip(route_points, route_points[1:])):
                raise ValueError("装饰侵入道路：" + item["Name"])


def baseline_unchanged():
    path = ROOT / "UnityGame/Builds/UIRefinement-20261108/source-verification.json"
    baseline = json.loads(path.read_text(encoding="utf-8-sig"))
    for entry in baseline["Files"]:
        if digest(ROOT / "UnityGame" / entry["Path"]) != entry["Sha256"]:
            raise ValueError("正式工程偏离原UI版：" + entry["Path"])
    return len(baseline["Files"])


def validate(folder: Path, layout_report: dict, sources: list, level: dict, anchor):
    verify_layout(layout_report, level)
    records = []
    for slug in ("guan_yu", "zhao_yun"):
        path = folder / (slug + ".png")
        with Image.open(path) as art:
            art.load()
            if art.mode != "RGBA" or art.size != (768, 768):
                raise ValueError("人物应为768透明方图：" + slug)
            alpha = art.getchannel("A")
            bounds = alpha.point(lambda v: 255 if v > 12 else 0).getbbox()
            if alpha.getextrema() != (0, 255) or not bounds or min(bounds[:2]) < 10 or max(bounds[2:]) > 758:
                raise ValueError("人物透明安全边不足：" + slug + str(bounds))
            if not 560 <= bounds[3] - bounds[1] <= 725:
                raise ValueError("人物有效高度偏离可用范围：" + slug + str(bounds))
            records.append({"Id": slug, "AlphaBounds": bounds, "DeclaredAnchor": anchor})
    with Image.open(folder / "huangjin.png") as image:
        if image.size != (1920, 1080):
            raise ValueError("地图应为1920×1080")
    for entry in sources:
        if digest(ROOT / entry["Path"]) != entry["Sha256"]:
            raise ValueError("生成期间输入变化：" + entry["Path"])
    files = []
    for path in sorted(folder.glob("*.png")):
        with Image.open(path) as image:
            dimensions = image.size
            image.verify()
        files.append({"Path": path.name, "Size": dimensions, "Sha256": digest(path)})
    report = {"Stage": "2D重绘方向样稿，未接入游戏", "Method": "原创可编辑2D曲线及Pillow分层设色",
              "Inputs": sources, "Characters": records, "MapLayout": layout_report,
              "FormalSourceFilesUnchanged": baseline_unchanged(), "Files": files,
              "PreviewIsStaticComposite": True, "NewGameBuildCreated": False,
              "RuntimeUiTestedWithNewArt": False, "AiImageGenerationUsed": False,
              "NewDependenciesInstalled": False}
    (folder / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("2D样稿技术检查通过：6张PNG、70路线点、32阵位、正式407文件未变；美感另行目视评审")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revision", required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_-]{0,40}", args.revision):
        raise ValueError("无效样稿版本名")
    folder = OUTPUT / args.revision
    if folder.exists():
        raise ValueError("样稿版本已存在，请使用新版本号")
    baseline_unchanged()
    input_paths = [ROOT / "tools/art2d/characters.py", ROOT / "tools/art2d/terrain.py", Path(__file__).resolve(),
                   RESOURCES / "database.json", RESOURCES / "Fonts/Chinese.otf",
                   RESOURCES / "Characters/guan_yu.png", RESOURCES / "Characters/zhao_yun.png",
                   RESOURCES / "Maps/huangjin.png",
                   ROOT / "docs/ui/runtime-ui-refinement-20261108/battle-deployed-1920.png"]
    input_paths += [ROOT / "docs/ui" / name for name in ("portrait_art_v2.py", "portrait_art_v3.py", "portrait_art_v4.py",
                                                          "terrain_art_v2.py", "terrain_art_v3.py", "terrain_art_v4.py")]
    sources = [{"Path": path.relative_to(ROOT).as_posix(), "Sha256": digest(path)} for path in input_paths]
    database = json.loads((RESOURCES / "database.json").read_text(encoding="utf-8-sig"))
    level = next(level for level in database["Levels"] if level["Id"] == "huangjin")
    characters = load_source("_game_art_2d_characters", ROOT / "tools/art2d/characters.py")
    terrain = load_source("_game_art_2d_terrain", ROOT / "tools/art2d/terrain.py")
    anchor = tuple(characters.CHARACTER_ANCHOR)
    if anchor != (360, 660):
        raise ValueError("人物声明的台座锚点不符")
    folder.mkdir(parents=True)
    for slug in ("guan_yu", "zhao_yun"):
        characters.make_character(slug, 768).save(folder / (slug + ".png"))
    landscape = terrain.make_terrain(level)
    layout_report = landscape.info.get("layout_report", {})
    landscape.save(folder / "huangjin.png")
    compare(folder)
    gameplay(folder, level)
    validate(folder, layout_report, sources, level, anchor)


if __name__ == "__main__":
    sys.dont_write_bytecode = True
    main()
