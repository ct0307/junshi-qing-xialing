"""导出第四稿透明素材、清单与检视图；不写入任何第三稿文件。"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

from effect_art_v4 import EFFECTS, make_effect
from portrait_art_v3 import make_character as make_baseline_character
from portrait_art_v4 import make_character
from terrain_art_v4 import make_terrain, make_gate_asset, make_tent_asset, EMPTY_SLOTS, DEPLOYED

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets-v4"
BASELINE_HASHES = {
    "battle-ui-concept-v3.png": "8bb067419141494eb4f9c3928399ff0a090f36226cc7fe2544ea4299806820c5",
    "character-study-v3.png": "0dd02f1a2b922f88745388e88ff8b0c473128cde0784d6c6570ad996e8bcde04",
}
CHARACTERS = [
    ("关羽", "guan_yu", "重枣面 · 丹凤眼", "卧蚕眉 · 长髯"),
    ("张飞", "zhang_fei", "豹头环眼", "燕颔虎须"),
    ("赵云", "zhao_yun", "浓眉大眼", "阔面重颐"),
    ("周瑜", "zhou_yu", "姿质风流", "仪容秀丽"),
    ("诸葛亮", "zhuge_liang", "面如冠玉", "纶巾 · 鹤氅"),
    ("黄忠", "huang_zhong", "年近六旬 · 勇武", "白须为年龄转译"),
    ("吕布", "lv_bu", "紫金冠 · 红锦袍", "兽面铠 · 画戟"),
]
FONT_PATHS = {"body": "C:/Windows/Fonts/msyh.ttc", "title": "C:/Windows/Fonts/simkai.ttf"}


def verify_baseline():
    for filename, expected in BASELINE_HASHES.items():
        actual = hashlib.sha256((ROOT / filename).read_bytes()).hexdigest()
        if actual != expected:
            raise RuntimeError(f"已认可的视觉基准发生改变：{filename}")


def avatar_crop(name):
    """设计坐标为320，头像裁剪保持冠帽，吕布额外留出双翎空间。"""
    return (42, 0, 250, 202) if name == "吕布" else (78, 26, 214, 176)


def make_avatar(art, name, size=320):
    ratio = art.width/320
    crop = art.crop(tuple(round(v*ratio) for v in avatar_crop(name)))
    crop.thumbnail((size-16, size-16), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (size, size))
    result.alpha_composite(crop, ((size-crop.width)//2, (size-crop.height)//2))
    return result


def save_asset(image, relative, entries, **metadata):
    output = ASSETS / relative
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output)
    with Image.open(output) as check:
        check.verify()
    entries.append({"path": relative, "size": list(image.size), "mode": image.mode,
                    "sha256": hashlib.sha256(output.read_bytes()).hexdigest(), **metadata})


def text(draw, xy, value, size=24, fill="#244E41", role="body", anchor="lt"):
    draw.text(xy, value, font=ImageFont.truetype(FONT_PATHS[role], size), fill=fill, anchor=anchor)


def paste_contain(canvas, art, rect):
    left, top, right, bottom = rect
    art = art.copy()
    art.thumbnail((right-left, bottom-top), Image.Resampling.LANCZOS)
    canvas.paste(art, (left+(right-left-art.width)//2, top+(bottom-top-art.height)//2), art)


def render_character_sheet(arts):
    canvas = Image.new("RGB", (2400, 1120), "#F1EDE2")
    draw = ImageDraw.Draw(canvas)
    text(draw, (44, 39), "演义沙盘 · 人物素材精修", 55, role="title")
    text(draw, (46, 118), "保留 v3 造型与原著特征，细化面部体积、衣甲材质与兵器刃口。下排对照使用相同裁剪。", 25, "#76816D")
    for index, (name, slug, first, second) in enumerate(CHARACTERS):
        x, width = 44+index*332, 320
        draw.rounded_rectangle((x, 186, x+width, 1007), 20, fill="#FAF7EF", outline="#D5DCCB", width=2)
        text(draw, (x+width//2, 228), name, 39, role="title", anchor="mm")
        paste_contain(canvas, arts[name], (x+5, 272, x+width-5, 582))
        draw.line((x+23, 607, x+width-23, 607), fill="#DDE0D3", width=2)
        before = make_avatar(make_baseline_character(name, 768, name == "吕布"), name)
        after = make_avatar(arts[name], name)
        paste_contain(canvas, before, (x+20, 639, x+152, 795))
        paste_contain(canvas, after, (x+168, 639, x+300, 795))
        text(draw, (x+86, 824), "v3 基准", 20, "#8A8C7A", anchor="mm")
        text(draw, (x+234, 824), "v4 精修", 20, "#47795D", anchor="mm")
        text(draw, (x+160, 900), first, 25, anchor="mm")
        text(draw, (x+160, 945), second, 23, "#7A826F", anchor="mm")
    text(draw, (46, 1051), "本轮精修 7 位示范人物；25 人角色池尚未全部制作。脸部光影、服饰纹样与具体神态属于原创美术转译。", 24, "#78816D")
    canvas.save(ROOT / "character-study-v4.png")


def render_effect_sheet():
    canvas = Image.new("RGB", (1920, 1020), "#193E37")
    draw = ImageDraw.Draw(canvas)
    text(draw, (48, 39), "演武反馈 · 透明序列素材", 49, "#F1EAD9", "title")
    text(draw, (50, 112), "每组 8 帧 / 单帧 256 × 256 / 此图为序列检视，实际时序待接入手机工程。", 23, "#B1C1A8")
    for row, (kind, spec) in enumerate(EFFECTS.items()):
        y = 183+row*194
        draw.rounded_rectangle((36, y, 1884, y+180), 16, fill="#254E3F", outline="#47634D", width=1)
        text(draw, (57, y+29), spec["label"], 28, "#E9DDC1", "title")
        text(draw, (58, y+86), f'{spec["duration_ms"]} ms', 22, "#B7C5AB")
        text(draw, (58, y+126), "循环" if spec["loop"] else "单次", 20, "#96AF98")
        for frame in range(8):
            x = 259+frame*200
            canvas.paste(make_effect(kind, frame, 168), (x, y-1), make_effect(kind, frame, 168))
            text(draw, (x+84, y+157), f"{frame+1:02}", 18, "#88A48C", anchor="mm")
    canvas.save(ROOT / "effects-study-v4.png")


def export():
    verify_baseline()
    assert len(EMPTY_SLOTS) == len(set(EMPTY_SLOTS)) == 30
    assert len(DEPLOYED) == 4 and len(set(EMPTY_SLOTS + DEPLOYED)) == 34
    entries, arts, sequences = [], {}, []
    for name, slug, _, _ in CHARACTERS:
        enemy = name == "吕布"
        art = make_character(name, 768, enemy)
        assert art.mode == "RGBA" and art.getchannel("A").getextrema() == (0, 255)
        arts[name] = art
        save_asset(art, f"characters/{slug}.png", entries, character=name, enemy=enemy,
                   pivot=[360, 660], pivot_note="台座中心设计坐标150,275，非可见像素边界", refined=True)
        save_asset(make_avatar(art, name), f"portraits/{slug}.png", entries,
                   character=name, source_crop_320=list(avatar_crop(name)))
    save_asset(make_terrain(1), "maps/hulao_pass.png", entries,
               logical_size=[1920, 1080], empty_slots=[list(p) for p in EMPTY_SLOTS],
               deployed_slots=[list(p) for p in DEPLOYED])
    save_asset(make_gate_asset(512), "buildings/hulao_gate.png", entries)
    save_asset(make_tent_asset(256), "buildings/camp_tent.png", entries)
    for kind, spec in EFFECTS.items():
        atlas = Image.new("RGBA", (2048, 256))
        for frame in range(8):
            art = make_effect(kind, frame)
            assert art.mode == "RGBA" and art.getchannel("A").getbbox()
            save_asset(art, f"effects/{kind}/{frame:02}.png", entries)
            # 图集保存原始RGBA字节；合成会清空全透明像素中的残留RGB。
            atlas.paste(art, (frame*256, 0))
        relative = f"effects/{kind}_atlas.png"
        save_asset(atlas, relative, entries)
        sequences.append({"id": kind, **spec, "atlas": relative, "frame_size": [256, 256],
                          "frame_count": 8, "frame_order": "从左到右", "frame_duration_ms": spec["duration_ms"]/8,
                          "texture_filter": "linear", "wrap": "clamp_to_edge"})
    manifest = {
        "version": 4, "baseline": BASELINE_HASHES, "state": "美术素材，尚未接入Android工程",
        "format": "PNG RGBA，直通透明度，普通alpha混合；地图为不透明背景",
        "lighting": "左上方", "asset_count": len(entries), "refined_characters": [c[0] for c in CHARACTERS],
        "not_completed": "其余18位角色素材、实际触控与动态时序、APK构建与手机实测",
        "character_pivot": "固定方形画布，以台座中心设计坐标150,275对齐阵位；裁剪后使用时需重算锚点",
        "sequences": sequences, "assets": entries,
    }
    (ASSETS / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    render_character_sheet(arts)
    render_effect_sheet()
    verify_baseline()
    print(f"素材导出：{ASSETS}")
    print(f"PNG {len(entries)} 张；精修人物 7 位；透明反馈 4 组 × 8 帧；阵位 34；v3 基准校验一致")


if __name__ == "__main__":
    export()
