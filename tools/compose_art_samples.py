"""制作美术对比板和明确标记的静态战场合成；不修改游戏素材。"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "docs/ui/art-refinement-v5"
RESOURCES = ROOT / "UnityGame/Assets/Resources/Game"
PAPER = "#F1EAD9"
INK = "#193E37"
GOLD = "#C4A25E"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(RESOURCES / "Fonts/Chinese.otf"), size)


def label(image: Image.Image, text: str, xy: tuple[int, int], size=28, fill=INK):
    ImageDraw.Draw(image).text(xy, text, font=font(size), fill=fill)


def place(canvas: Image.Image, source: Image.Image, box: tuple[int, int, int, int]):
    source = source.convert("RGBA")
    source.thumbnail((box[2] - box[0], box[3] - box[1]), Image.Resampling.LANCZOS)
    x = box[0] + (box[2] - box[0] - source.width) // 2
    y = box[1] + (box[3] - box[1] - source.height) // 2
    canvas.alpha_composite(source, (x, y))


def overview(folder: Path):
    image = Image.new("RGBA", (1920, 1450), PAPER)
    label(image, "青绿微缩 · 美术样例对比", (52, 30), 42)
    label(image, "黄巾郊野 / 关羽 / 赵云", (54, 93), 24)
    label(image, "原地图", (52, 147), 25)
    label(image, "微缩几何离线渲染样稿", (996, 147), 25)
    for source, left in ((RESOURCES / "Maps/huangjin.png", 40), (folder / "huangjin.png", 984)):
        terrain = Image.open(source).convert("RGBA").crop((0, 120, 1920, 870))
        place(image, terrain, (left, 197, left + 896, 570))
    names = (("guan_yu", "关羽"), ("zhao_yun", "赵云"))
    for hero_index, (slug, name) in enumerate(names):
        for version in range(2):
            column = hero_index * 2 + version
            left = 36 + column * 474
            panel = (left, 627, left + 444, 1223)
            ImageDraw.Draw(image).rounded_rectangle(panel, radius=18, fill="#E3DFC9")
            label(image, name + (" · 原图" if version == 0 else " · 体积样稿"), (left + 18, 640), 25)
            path = RESOURCES / f"Characters/{slug}.png" if version == 0 else folder / f"{slug}.png"
            sprite = Image.open(path).convert("RGBA")
            place(image, sprite, (left, 690, left + 444, 1150))
            place(image, sprite, (left + 165, 1240, left + 278, 1353))
    label(image, "下排按约113像素显示：检查小屏轮廓与明暗，不以放大图代替游戏尺寸。", (52, 1380), 23)
    image.save(folder / "comparison-overview.png")


def gameplay(folder: Path, database: dict):
    # 使用原界面框架作静态艺术合成，不调用运行时，也不当作游戏截图证据。
    source = ROOT / "docs/ui/runtime-ui-refinement-20261108/battle-deployed-1920.png"
    image = Image.open(source).convert("RGBA")
    draw = ImageDraw.Draw(image)
    draw.rectangle((32, 184, 1887, 823), fill="#94A47B")
    terrain = Image.open(folder / "huangjin.png").convert("RGBA").crop((38, 144, 1882, 842))
    terrain = terrain.resize((1844, round(698 * .67)), Image.Resampling.LANCZOS)
    # 对齐正式GroundEdgeFade的上下渐隐，避免截取边缘在合成图形成硬线。
    fade = Image.new("L", (1, terrain.height))
    fade.putdata([round(255 * min(1, row / (terrain.height * .1),
                                  (terrain.height - 1 - row) / (terrain.height * .04)))
                  for row in range(terrain.height)])
    terrain.putalpha(ImageChops.multiply(terrain.getchannel("A"), fade.resize(terrain.size)))
    image.alpha_composite(terrain, (38, round(218 + 144 * .67)))
    level = next(level for level in database["Levels"] if level["Id"] == "huangjin")
    draw = ImageDraw.Draw(image)
    for slot in level["Slots"]:
        x, y = slot["Position"]["X"], 218 + slot["Position"]["Y"] * .67
        draw.ellipse((x - 47, y - 16, x + 47, y + 16), fill="#8C9B67", outline="#AFB386", width=1)
    for slug, name, x in (("guan_yu", "关羽", 170), ("zhao_yun", "赵云", 340)):
        y = 218 + 215 * .67
        sprite = Image.open(folder / f"{slug}.png").convert("RGBA").resize((170, 170), Image.Resampling.LANCZOS)
        image.alpha_composite(sprite, (round(x - 170 * 360 / 768), round(y - 170 * 660 / 768)))
        draw = ImageDraw.Draw(image)
        draw.rounded_rectangle((x - 55, y + 18, x + 55, y + 54), radius=15, fill=PAPER)
        label(image, name, (round(x - 24), round(y + 17)), 24)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((49, 837, 1307, 897), radius=12, fill="#2D5746")
    label(image, "美术样例 · 静态合成，人物与地图尚未接入试玩包", (66, 847), 25, PAPER)
    image.save(folder / "gameplay-art-sample-1920.png")
    image.resize((1280, 720), Image.Resampling.LANCZOS).save(folder / "gameplay-art-sample-1280.png")


def validate(folder: Path, database: dict):
    render_manifest = json.loads((folder / "render-manifest.json").read_text(encoding="utf-8-sig"))
    if render_manifest["RenderExitCode"] != 0 or sha256(folder / "render.log") != render_manifest["LogSha256"]:
        raise ValueError("渲染日志或退出码不符")
    if sha256(RESOURCES / "database.json") != render_manifest["DatabaseSha256"]:
        raise ValueError("渲染输入数据库与当前合成报告不一致")
    for script in render_manifest["Scripts"]:
        if sha256(Path(script["Path"])) != script["Sha256"]:
            raise ValueError("样稿渲染后源码已变化，需要新版本重新渲染：" + script["Path"])
    records = []
    for slug in ("guan_yu", "zhao_yun"):
        path = folder / f"{slug}.png"
        with Image.open(path) as image:
            image.load()
            if image.size != (768, 768) or image.mode != "RGBA":
                raise ValueError("人物规格应为768方图RGBA：" + slug)
            alpha = image.getchannel("A")
            bounds = alpha.point(lambda value: 255 if value > 12 else 0).getbbox()
            if not bounds or min(bounds[:2]) < 5 or bounds[2] > 763 or bounds[3] > 763:
                raise ValueError("人物为空或触及透明安全边：" + slug + str(bounds))
            if alpha.getextrema() != (0, 255):
                raise ValueError("人物没有同时保留完整透明与不透明像素：" + slug)
            data = image.tobytes()
            invalid_material = sum(1 for r, g, b, a in zip(data[0::4], data[1::4], data[2::4], data[3::4])
                                   if a > 200 and r > 220 and b > 200 and g < 80)
            if invalid_material > 30:
                raise ValueError("人物存在失效材质的紫红像素：" + slug + "，共" + str(invalid_material))
            records.append({"Id": slug, "Path": str(path.relative_to(ROOT)), "Sha256": sha256(path),
                            "Size": [768, 768], "AlphaBounds": bounds, "Anchor": [360, 660]})
    with Image.open(folder / "huangjin.png") as terrain:
        terrain.load()
        if terrain.size != (1920, 1080):
            raise ValueError("地图规格不符")
    # 正式工程保持上一轮UI版的全部源码与资源，样稿只在独立工程中生成。
    baseline_path = ROOT / "UnityGame/Builds/UIRefinement-20261108/source-verification.json"
    baseline = json.loads(baseline_path.read_text(encoding="utf-8-sig"))
    for item in baseline["Files"]:
        if sha256(ROOT / "UnityGame" / item["Path"]) != item["Sha256"]:
            raise ValueError("正式工程偏离样例开始前版本：" + item["Path"])
    pngs = []
    for path in sorted(folder.glob("*.png")):
        with Image.open(path) as image:
            size = list(image.size)
            image.verify()
        pngs.append({"Path": path.name, "Size": size, "Sha256": sha256(path)})
    level = next(level for level in database["Levels"] if level["Id"] == "huangjin")
    report = {"Stage": "2.5D美术样例，未接入正式游戏", "Method": "现有Unity内置材质与网格离线渲染",
              "RenderManifest": render_manifest,
              "AiImageGenerationUsed": False, "NewDependenciesInstalled": False, "Characters": records,
              "Level": level["Id"], "Routes": level["Routes"], "SlotCount": len(level["Slots"]),
              "SourceFilesUnchanged": len(baseline["Files"]), "Files": pngs,
              "PreviewBoundary": "gameplay-art-sample为静态合成，不是运行截图；未验证手机或正式接入后的表现"}
    (folder / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("美术样稿检查通过：2张768透明人物、1920地图、" + str(len(pngs)) + "张PNG；正式工程" + str(len(baseline["Files"])) + "文件未变")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revision", default="v01")
    args = parser.parse_args()
    folder = (ART / args.revision).resolve()
    if not folder.is_relative_to(ART.resolve()) or not folder.is_dir():
        raise ValueError("样稿版本目录不存在或越界")
    database = json.loads((RESOURCES / "database.json").read_text(encoding="utf-8-sig"))
    overview(folder)
    gameplay(folder, database)
    validate(folder, database)


if __name__ == "__main__":
    main()
