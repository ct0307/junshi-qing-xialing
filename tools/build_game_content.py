"""生成演义沙盘运行时内容；只写自己的输出，不改 v3/v4 源稿或 Fonts。

运行：现有 Python tools/build_game_content.py
复核：现有 Python tools/build_game_content.py --verify-only
图形在 320 单位人物画布、1920×1080 地图上编辑，Pillow 超采样导出。
角色和技能为演义沙盘游戏改编，数值为可继续试玩调整的初始配置。
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import random
import shutil
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "docs/ui"
OUTPUT = ROOT / "UnityGame/Assets/Resources/Game"
MANIFEST = ROOT / "docs/engineering/game-content-manifest.json"
ROSTER_REVIEW = ROOT / "docs/engineering/content-roster.png"
MAP_REVIEW = ROOT / "docs/engineering/maps-review.png"
SIZE = 768


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ART / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


V2 = module("_content_portrait", "portrait_art_v2.py")
V4 = module("_content_refined_portrait", "portrait_art_v4.py")
TERRAIN = module("_content_terrain", "terrain_art_v4.py")
EFFECTS = module("_content_effects", "effect_art_v4.py").EFFECTS
mix = V2._mix


class MapPainter(TERRAIN._Painter):
    """在独立脚本补充描边参数，不更改原地形 Painter。"""

    def polygon(self, points, fill, outline=None, width=1):
        super().polygon(points, fill)
        if outline:
            self.line(list(points) + [points[0]], outline, width)

# 每项字段：身份、姓名、称号、职能、技能、军饷、射程、伤害、攻击间隔、颜色、玩法说明。
HERO_ROWS = [
    ("guan_yu", "关羽", "美髯公", "定点破阵", "focus", 130, 215, 25, .95, "#527C61", "连续攻击同一目标积累刀势，转移目标后重置。"),
    ("huang_zhong", "黄忠", "定军神射", "定点破阵", "sniper", 110, 330, 30, 1.35, "#AC9568", "超远程点射，优先攻击最接近城门的敌人。"),
    ("xu_chu", "许褚", "虎痴", "定点破阵", "duelist", 125, 170, 32, 1.15, "#9B7A54", "范围内单敌时重击，多敌时挥动重锤横扫。"),
    ("taishi_ci", "太史慈", "双弦雄风", "定点破阵", "twin", 120, 265, 18, 1.15, "#9F8250", "双弦分射两个目标，分担两路小股敌军。"),
    ("lv_bu", "吕布", "飞将", "定点破阵", "cleave", 160, 200, 46, 1.8, "#AF6F5B", "持续普攻并独立蓄力，挥动画戟发动近距重斩。"),
    ("zhao_yun", "赵云", "常山龙胆", "群体清场", "pierce", 120, 245, 23, 1.1, "#9DB9AF", "枪势沿目标方向穿透排成一线的敌军。"),
    ("zhou_yu", "周瑜", "江东都督", "群体清场", "fire", 135, 245, 17, 1.3, "#AE6C5D", "火攻留下短暂燃烧区，适合压制密集慢兵。"),
    ("lu_xun", "陆逊", "儒将火计", "群体清场", "spread_fire", 115, 250, 18, 1.15, "#76966F", "点燃目标；目标倒下后，火种传给附近敌军。"),
    ("yuan_shao", "袁绍", "四世三公", "群体清场", "rain", 145, 290, 23, 1.65, "#8F7C98", "周期箭雨覆盖小片区域，兼顾并行敌群。"),
    ("guo_jia", "郭嘉", "奇佐", "群体清场", "mark", 115, 255, 20, 1.3, "#8BA1A3", "留下延迟爆裂印记，目标倒下时提前引爆。"),
    ("zhang_fei", "张飞", "燕人", "路线拦截", "stun", 120, 185, 24, 1.15, "#586A61", "周期怒喝短暂震慑敌群，Boss 受到较短控制。"),
    ("zhuge_liang", "诸葛亮", "卧龙", "路线拦截", "slow", 135, 255, 12, 1.25, "#9CAF94", "八阵风场持续减速，延长同伴有效输出时间。"),
    ("ma_chao", "马超", "锦马超", "路线拦截", "knockback", 125, 210, 28, 1.3, "#B9B9A6", "蓄力枪击击退普通敌人，Boss 仅受到伤害。"),
    ("zhang_jiao", "张角", "太平道人", "路线拦截", "chain", 130, 255, 22, 1.4, "#BBA45C", "雷光在邻近敌人间跳跃，造成极短停顿。"),
    ("lv_meng", "吕蒙", "白衣渡江", "路线拦截", "ambush", 105, 210, 21, .95, "#A7B7A1", "敌军首次进入伏击区时受到额外袭击。"),
    ("cao_cao", "曹操", "魏武", "协同谋略", "buff", 130, 240, 16, 1.2, "#59727B", "提升附近武将伤害，增益不叠加到控制频率。"),
    ("diao_chan", "貂蝉", "月下离间", "协同谋略", "vulnerable", 100, 250, 12, 1.2, "#AA95AF", "对强敌施加短时易伤，为同伴创造破阵机会。"),
    ("pang_tong", "庞统", "凤雏", "协同谋略", "link", 110, 250, 16, 1.25, "#9C8769", "连环连接两个敌人，分担伤害且不循环触发。"),
    ("xun_yu", "荀彧", "王佐", "协同谋略", "assist", 110, 245, 12, 1.15, "#B6BDA6", "附近同伴命中时触发独立冷却的协同箭击。"),
    ("sima_yi", "司马懿", "鹰视", "协同谋略", "disrupt", 125, 260, 19, 1.25, "#6E7B78", "周期干扰敌军特殊技能，使其延后发动。"),
    ("sun_shangxiang", "孙尚香", "弓腰姬", "灵活补位", "execute", 100, 270, 20, .9, "#B57568", "优先攻击残血敌人，击退后迅速转火。"),
    ("zhang_liao", "张辽", "威震逍遥", "灵活补位", "splash", 120, 225, 26, 1.15, "#A96A5C", "攻击队首，并向其身后造成扇形溅射。"),
    ("jiang_wei", "姜维", "幼麟", "灵活补位", "adaptive", 120, 245, 23, 1.05, "#6A9387", "自动交替近距枪阵与远程弓阵，应对距离变化。"),
    ("hua_tuo", "华佗", "青囊", "灵活补位", "heal", 90, 220, 12, 1.2, "#9CAF95", "每波结束恢复少量城防，整局恢复设有上限。"),
    ("lu_su", "鲁肃", "江东粮台", "灵活补位", "supply", 90, 225, 12, 1.15, "#B19C74", "每波结束补充少量军饷，整局收益设有上限。"),
]

# 固定游戏归属的唯一来源；不随关卡历史时间变化。
FACTION_HEROES = {
    "wei": ("xu_chu", "guo_jia", "cao_cao", "xun_yu", "sima_yi", "zhang_liao"),
    "shu": ("guan_yu", "huang_zhong", "zhao_yun", "zhang_fei", "zhuge_liang", "ma_chao", "pang_tong", "jiang_wei"),
    "wu": ("taishi_ci", "zhou_yu", "lu_xun", "lv_meng", "sun_shangxiang", "lu_su"),
    "qun": ("lv_bu", "yuan_shao", "zhang_jiao", "diao_chan", "hua_tuo"),
}
HERO_FACTIONS = {hero_id: faction for faction, members in FACTION_HEROES.items() for hero_id in members}
assert sum(map(len, FACTION_HEROES.values())) == len(HERO_FACTIONS) == 25

# 每项为普攻间隔、普攻威力比例、独立技能冷却、技能威力比例。
# 保留既有Damage作为基准，给更低频的技能重新分配预算，避免只叠加快普攻。
# 普攻间隔按第二轮试玩反馈缩至首版70%；单次普攻、特色技能与经济预算保持不变。
HERO_COMBAT_BUDGETS = {
    "guan_yu": (.525, .50, 3.0, 2.53),
    "huang_zhong": (.665, .40, 3.5, 2.07),
    "xu_chu": (.665, .40, 3.5, 2.43),
    "taishi_ci": (.490, .35, 3.0, 2.09),
    "lv_bu": (.700, .32, 4.8, .66),
    "zhao_yun": (.455, .35, 3.2, 2.33),
    "zhou_yu": (.630, .30, 3.2, 1.97),
    "lu_xun": (.595, .30, 3.5, 2.43),
    "yuan_shao": (.770, .30, 4.5, 1.21),
    "guo_jia": (.700, .30, 3.5, 1.12),
    "zhang_fei": (.630, .35, 4.5, .90),
    "zhuge_liang": (.735, .25, 3.5, 2.24),
    "ma_chao": (.525, .40, 3.5, .92),
    "zhang_jiao": (.700, .30, 3.5, 2.0),
    "lv_meng": (.525, .35, 3.0, .80),
    "cao_cao": (.700, .35, 4.0, 2.67),
    "diao_chan": (.665, .30, 3.0, 2.0),
    "pang_tong": (.700, .30, 3.5, 2.24),
    "xun_yu": (.735, .35, 1.5, .80),
    "sima_yi": (.735, .30, 4.5, .90),
    "sun_shangxiang": (.420, .40, 2.5, 2.22),
    "zhang_liao": (.560, .35, 3.0, 2.09),
    "jiang_wei": (.490, .40, 3.0, 2.29),
    "hua_tuo": (.770, .32, 4.0, 2.67),
    "lu_su": (.770, .32, 4.0, 2.78),
}

REFINED = {"guan_yu", "huang_zhong", "lv_bu", "zhao_yun", "zhou_yu", "zhang_fei", "zhuge_liang"}
LEGACY = {"cao_cao", "diao_chan", "pang_tong", "hua_tuo", "zhang_liao"}

# 新增人物为可编辑的原创造型参数；脸型、冠帽与装备独立于玩法数值。
LOOKS = {
    "xu_chu": dict(coat="#907652", light="#BDA47A", dark="#5E5A43", cape="#7A6C51", skin="#D4AD86", armor=True, face=(98, 76, 191, 157), eye=116, brow=4, head="tiger", beard="short", weapon="mace"),
    "taishi_ci": dict(coat="#AA9365", light="#D1BC8A", dark="#6F7056", cape="#7E8A66", skin="#DFBC99", armor=True, face=(105, 78, 183, 152), eye=115, brow=3, head="wing", beard="moustache", weapon="double_bow"),
    "lu_xun": dict(coat="#85A085", light="#D7DFC5", dark="#527A69", cape="#B8C5A5", skin="#E8C7A8", armor=False, face=(112, 77, 179, 153), eye=116, brow=1.5, head="ribbon", beard="none", weapon="torch"),
    "yuan_shao": dict(coat="#8C7B94", light="#BEB0BC", dark="#5B5C67", cape="#A697AC", skin="#DAB292", armor=True, face=(103, 75, 187, 153), eye=115, brow=3, head="gold_crown", beard="long", weapon="banner"),
    "guo_jia": dict(coat="#93A9AC", light="#D5DDD2", dark="#617E80", cape="#A8B9AF", skin="#E3C9AC", armor=False, face=(111, 77, 179, 155), eye=117, brow=1.5, head="soft_cap", beard="fine", weapon="cup"),
    "ma_chao": dict(coat="#C2C9BD", light="#F0EBD7", dark="#7E9C92", cape="#B1A26F", skin="#E1BC99", armor=True, face=(106, 80, 183, 152), eye=115, brow=3, head="lion", beard="none", weapon="lance"),
    "zhang_jiao": dict(coat="#C2AD66", light="#E7D99B", dark="#8B8354", cape="#A7935E", skin="#D3B28D", armor=False, face=(111, 78, 181, 157), eye=119, brow=3, head="turban", beard="wild", weapon="staff"),
    "lv_meng": dict(coat="#D7DDD0", light="#F0EBD7", dark="#799585", cape="#C5D0BE", skin="#DDB898", armor=True, face=(107, 79, 183, 153), eye=116, brow=2.6, head="hood", beard="short", weapon="sabre"),
    "xun_yu": dict(coat="#CBD1BA", light="#F1EBD4", dark="#7B9485", cape="#A7B69E", skin="#E0BFA0", armor=False, face=(112, 77, 178, 155), eye=117, brow=1.8, head="minister", beard="fine", weapon="scroll"),
    "sima_yi": dict(coat="#586D6A", light="#A3B3A8", dark="#354C49", cape="#6C7B73", skin="#D6BCA2", armor=False, face=(112, 78, 179, 158), eye=118, brow=3.2, head="dark_crown", beard="long", weapon="cane"),
    "sun_shangxiang": dict(coat="#B6796B", light="#D6A894", dark="#7C5C59", cape="#C69A85", skin="#E9C9AC", armor=True, face=(112, 79, 181, 151), eye=117, brow=1.5, head="buns", beard="none", weapon="bow"),
    "jiang_wei": dict(coat="#6D968A", light="#B6C9B2", dark="#446E65", cape="#91AC95", skin="#E2BE9D", armor=True, face=(106, 77, 183, 151), eye=115, brow=2.7, head="crest", beard="none", weapon="crescent"),
    "lu_su": dict(coat="#B2A17C", light="#DAD0AA", dark="#7D8062", cape="#A4AC83", skin="#DFBC98", armor=False, face=(101, 79, 188, 155), eye=118, brow=2.2, head="round_cap", beard="round", weapon="ledger"),
}


def spear(p, x=239, top=25, bottom=268, crescent=False):
    p.line([(x, top + 24), (x - 7, bottom)], "#796C50", 5)
    p.line([(x - 1, top + 25), (x - 8, bottom)], "#C4B68D", 1.3)
    p.polygon([(x, top), (x - 11, top + 27), (x, top + 47), (x + 11, top + 26)], "#BACBC2", "#719085", .9)
    p.polygon([(x, top + 2), (x, top + 43), (x - 8, top + 27)], "#F3F0DC")
    p.line([(x - 6, top + 48), (x + 7, top + 48)], V2.GOLD, 3)
    if crescent:
        p.path([("M", x + 1, top + 39), ("C", x + 24, top + 35, x + 28, top + 20, x + 27, top + 14),
                ("C", x + 38, top + 42, x + 22, top + 57, x + 1, top + 58)], "#B7C8BB", "#719085", .9)
    for offset in (-3, 3):
        p.curve((x + offset, top + 49), (x + 15, top + 60), (x - 8, top + 70), (x + 4, top + 82), "#AD705D", 2)


def bow(p, x, y, scale=1, color="#AA8B58"):
    p.curve((x, y), (x + 42 * scale, y + 20 * scale),
            (x + 42 * scale, y + 101 * scale), (x, y + 130 * scale), "#6A654A", 5 * scale)
    p.curve((x, y), (x + 39 * scale, y + 20 * scale),
            (x + 39 * scale, y + 101 * scale), (x, y + 130 * scale), color, 2.7 * scale)
    p.line([(x, y), (x + 3 * scale, y + 66 * scale), (x, y + 130 * scale)], "#EFE1B9", .9)


def weapon_back(p, kind):
    if kind in ("lance", "crescent"):
        spear(p, crescent=kind == "crescent")
    elif kind in ("bow", "double_bow"):
        bow(p, 222, 77, 1.1)
        if kind == "double_bow":
            bow(p, 69, 94, .82, "#BFA777")
        p.polygon([(75, 153), (91, 149), (103, 230), (86, 234)], "#85714F", V2.GOLD, .7)
        for index in range(4):
            x, y = 69 + index * 7, 117 + index * 3
            p.line([(x, y), (x + 16, y + 68)], "#CBB68A", 1.7)
            p.polygon([(x, y), (x - 4, y + 10), (x + 4, y + 15), (x + 4, y + 3)], "#D9DDC3")
    elif kind == "mace":
        p.line([(236, 83), (229, 270)], "#786A51", 8)
        p.line([(234, 89), (227, 269)], "#BAA67C", 1.5)
        p.ellipse((214, 46, 263, 101), "#9B9F87", "#637B6E", 1.2)
        for x, y in ((215, 58), (228, 45), (248, 48), (260, 67), (251, 89), (220, 90)):
            p.polygon([(x, y), (x - 7, y - 11), (x + 7, y - 6)], "#BFC5A8", "#7A8B77", .7)
        p.curve((222, 58), (229, 50), (245, 48), (252, 59), "#E0D9B7", 2)
    elif kind == "banner":
        p.line([(241, 24), (235, 271)], "#887855", 5)
        p.polygon([(244, 35), (287, 43), (280, 64), (287, 87), (242, 79)], "#9C7E97", "#D4C294", 1.2)
        p.line([(251, 44), (265, 68), (276, 49)], V2.GOLD_LIGHT, 2.1)
        p.polygon([(241, 17), (236, 29), (247, 29)], V2.GOLD_LIGHT)
    elif kind in ("staff", "cane"):
        p.curve((235, 269), (230, 188), (239, 113), (238, 70), "#786D50", 6)
        p.curve((233, 264), (229, 188), (237, 113), (236, 71), "#B0A378", 1.3)
        p.curve((238, 79), (214, 58), (238, 35), (253, 56), "#83734E", 5)
        if kind == "staff":
            p.ellipse((229, 49, 247, 66), "#C8B75F", V2.GOLD_LIGHT, 1.3)
            p.line([(225, 80), (250, 80)], V2.GOLD_LIGHT, 3)
            p.path([("M", 240, 82), ("L", 260, 93), ("L", 251, 112), ("L", 267, 128)], outline="#DDD096", width=4)
            p.path([("M", 241, 85), ("L", 252, 102), ("L", 246, 113)], outline="#9B7D42", width=1)
        else:
            p.ellipse((235, 54, 244, 63), "#879B81", "#C6C8A5", .8)
    elif kind == "torch":
        p.line([(234, 101), (227, 267)], "#8E7453", 5)
        p.rounded((227, 85, 242, 116), 4, "#A38455", V2.GOLD, .8)
        p.path([("M", 230, 93), ("C", 210, 72, 239, 68, 232, 42),
                ("C", 267, 68, 246, 83, 242, 98)], "#C78156", "#D6A96E", 1)
        p.path([("M", 233, 91), ("C", 226, 77, 242, 72, 239, 62),
                ("C", 252, 78, 239, 91, 233, 91)], "#E9CB88")
    elif kind == "sabre":
        p.line([(236, 154), (227, 267)], "#6D8375", 8)
        p.line([(234, 160), (225, 263)], "#BDD0B7", 1.3)
        p.line([(227, 147), (246, 151)], V2.GOLD, 4)
        p.line([(236, 129), (235, 149)], "#746B50", 6)


def character_head(p, look):
    x1, y1, x2, y2 = look["face"]
    skin, brow, ey = look["skin"], look["brow"], look["eye"]
    hair = "#D8D1AE" if look["head"] == "turban" else V2.HAIR
    p.ellipse((x1 - 8, y1 - 9, x2 + 6, y2 + 9), hair)
    p.rounded((132, y2 - 13, 159, y2 + 9), 8, mix(skin, "#967759", .18))
    p.ellipse((x1 - 6, 110, x1 + 8, 132), skin)
    p.ellipse((x2 - 7, 110, x2 + 7, 132), skin)
    p.path([("M", x1 + 4, y1 + 19), ("C", x1 + 3, y1 - 8, x2 - 3, y1 - 8, x2 - 1, y1 + 23),
            ("L", x2 - 1, 130), ("C", x2 - 7, y2 - 1, 154, y2 + 5, 144, y2),
            ("C", x1 + 8, y2, x1, 139, x1 + 2, 121)], skin, mix(skin, "#90745A", .4), .8)
    p.path([("M", x1 + 8, y1 + 11), ("C", 124, y1 - 4, 146, y1 + 2, 147, y1 + 17),
            ("L", 140, 131), ("C", 121, 139, x1 + 8, 126, x1 + 8, y1 + 11)], mix(skin, V2.PAPER, .16))
    p.path([("M", x2 - 10, y1 + 17), ("C", x2 + 3, 119, x2 - 5, 145, 155, y2),
            ("L", 167, 127)], mix(skin, "#9A795D", .2))
    for x in (123, 164):
        slope = (1 if x == 123 else -1) * (2.7 if brow > 2.4 else .7)
        p.curve((x - 9, ey - 9 - slope), (x - 4, ey - 11), (x + 4, ey - 10),
                (x + 10, ey - 8 + slope), hair, brow)
        if look["head"] == "buns":
            p.curve((x - 7, ey), (x - 2, ey - 3), (x + 5, ey - 3), (x + 8, ey), "#5F514A", 1.6)
            p.line([(x + 7, ey - 1), (x + 10, ey - 4)], "#5F514A", 1)
            p.ellipse((x - 1, ey - 1, x + 3, ey + 4), "#4D5148")
        elif look["head"] in ("dark_crown", "minister", "soft_cap"):
            p.curve((x - 7, ey), (x, ey - 1.8), (x + 5, ey - 1), (x + 8, ey + .5), V2.INK, 1.8)
            p.ellipse((x, ey - 1, x + 2.5, ey + 2), V2.INK)
        else:
            p.ellipse((x - 6, ey - 3, x + 7, ey + 5), "#ECE5CE")
            p.ellipse((x - 1, ey - 3, x + 4, ey + 4), V2.INK)
            p.ellipse((x, ey - 2, x + 1.3, ey - .6), "#F4EFDC")
        p.curve((x - 6, ey + 8), (x, ey + 10), (x + 4, ey + 9), (x + 7, ey + 7), mix(skin, "#9C755A", .2), .7)
    p.curve((145, ey + 3), (141, ey + 11), (140, 129), (147, 129), mix(skin, "#84674E", .5), 1)
    p.curve((136, 140), (143, 142), (149, 142), (155, 138), "#A17660", 1.2)
    beard = look["beard"]
    if beard != "none":
        for x, s in ((140, -1), (149, 1)):
            p.curve((x, 136), (x + s * 4, 135), (x + s * 10, 140), (x + s * 15, 139), hair, 2)
        if beard == "long":
            p.path([("M", 124, 147), ("C", 130, 162, 135, 181, 146, 193),
                    ("C", 160, 175, 165, 155, 164, 145), ("C", 148, 157, 137, 154, 124, 147)], hair)
        elif beard == "wild":
            for index, x in enumerate(range(118, 174, 8)):
                p.path([("M", x, 142), ("C", x - 8, 159, x + 7, 176, 147 + (x - 146) * .7, 199 - abs(x - 145) * .5),
                        ("L", x + 8, 146)], "#A6A386", "#D1C8A6", .6)
        elif beard == "fine":
            p.path([("M", 137, 150), ("C", 138, 165, 148, 174, 145, 184),
                    ("C", 156, 171, 153, 157, 152, 150)], hair)
        elif beard == "round":
            p.path([("M", 116, 137), ("C", 118, 169, 168, 173, 176, 137),
                    ("L", 166, 150), ("C", 145, 159, 133, 152, 116, 137)], hair)
        elif beard == "short":
            p.path([("M", 118, 137), ("C", 126, 160, 141, 168, 148, 169),
                    ("C", 162, 165, 171, 151, 176, 138), ("L", 165, 149),
                    ("C", 143, 158, 134, 150, 118, 137)], hair)
        if beard != "moustache":
            for x in (135, 145, 155):
                p.curve((x, 153), (x - 2, 160), (146 + (x - 146) * .4, 171),
                        (146 + (x - 146) * .3, 176 if beard in ("long", "fine") else 159), "#7F8C75", .5)


def headwear(p, kind):
    gold, pale, ink = V2.GOLD, V2.GOLD_LIGHT, V2.HAIR
    if kind in ("tiger", "wing", "lion", "crest"):
        p.path([("M", 99, 100), ("C", 91, 72, 113, 58, 145, 58),
                ("C", 178, 56, 199, 74, 190, 101), ("L", 177, 91),
                ("L", 168, 78), ("L", 119, 79), ("L", 110, 94)], "#A0B4A4" if kind != "tiger" else "#9D956E", "#6B8476", 1.1)
        p.curve((101, 91), (116, 61), (172, 60), (188, 92), pale, 1.9)
        p.polygon([(140, 64), (149, 64), (154, 92), (145, 98), (135, 91)], gold, pale, 1)
        p.ellipse((140, 80, 149, 89), "#6F9585", pale, .8)
        if kind == "lion":
            for x, sign in ((106, -1), (185, 1)):
                p.path([("M", x, 79), ("C", x + sign * 17, 70, x + sign * 25, 53, x + sign * 13, 38),
                        ("C", x + sign * 11, 60, x + sign * 7, 61, x, 65)], "#C9D0BD", "#899E8A", .9)
            p.curve((145, 59), (139, 48), (149, 37), (146, 29), "#C4AC75", 5)
        elif kind == "wing":
            for x, sign in ((101, -1), (187, 1)):
                p.polygon([(x, 86), (x + sign * 23, 61), (x + sign * 22, 89), (x + sign * 5, 102)], "#C4B389", "#8B8766", .9)
                p.line([(x + sign * 5, 88), (x + sign * 18, 72)], pale, 1.1)
        elif kind == "tiger":
            p.polygon([(111, 67), (100, 48), (124, 57)], gold, pale, 1)
            p.polygon([(177, 66), (188, 47), (164, 57)], gold, pale, 1)
            for x, sign in ((131, -1), (159, 1)):
                p.line([(x, 78), (x + sign * 11, 71)], "#58694F", 2.2)
        else:
            p.curve((145, 58), (152, 36), (135, 28), (153, 20), "#749287", 9)
            p.curve((147, 56), (154, 38), (139, 28), (153, 22), "#B8C6AA", 2)
    elif kind in ("gold_crown", "minister", "dark_crown"):
        color = "#4C5E55" if kind == "minister" else "#5A645A" if kind == "dark_crown" else "#A18C5E"
        p.polygon([(112, 84), (113, 58), (126, 42), (166, 42), (179, 59), (178, 85)], color, gold, 1.1)
        p.rounded((109, 79, 182, 91), 4, "#697C64" if kind == "minister" else "#7F805D", gold, 1)
        for x in (125, 136, 147, 158, 169):
            p.line([(x, 49), (x, 77)], pale if kind == "gold_crown" else "#849276", .9)
        if kind == "gold_crown":
            for x, y in ((120, 43), (144, 30), (169, 43)):
                p.polygon([(x - 6, 47), (x, y - 12), (x + 6, 47)], gold, pale, .9)
            p.ellipse((139, 75, 152, 88), "#A57359", pale, 1)
        elif kind == "minister":
            p.polygon([(114, 77), (79, 59), (79, 73), (112, 87)], color, gold, .8)
            p.polygon([(177, 77), (210, 59), (209, 73), (178, 87)], color, gold, .8)
        else:
            p.polygon([(124, 47), (126, 28), (163, 28), (167, 47)], "#3F554E", "#91A18B", .9)
            p.line([(136, 32), (136, 42), (154, 42), (154, 32)], gold, 1)
    elif kind == "turban":
        p.path([("M", 103, 88), ("C", 97, 68, 120, 60, 137, 62),
                ("C", 165, 54, 190, 69, 186, 91), ("L", 178, 99),
                ("C", 150, 88, 121, 99, 103, 88)], "#CDBD76", "#9C9157", 1.1)
        p.curve((107, 84), (132, 67), (161, 76), (184, 87), "#E2D69B", 4)
        p.path([("M", 177, 85), ("C", 204, 101, 184, 137, 207, 150),
                ("L", 198, 157), ("C", 177, 129, 190, 105, 173, 94)], "#D3C17A", "#A99C63", .8)
        p.ellipse((138, 78, 151, 89), "#9A8860", V2.GOLD_LIGHT, 1)
    elif kind == "buns":
        for x in (104, 183):
            p.ellipse((x - 12, 62, x + 13, 89), ink, "#71816C", .9)
            p.curve((x - 10, 72), (x - 4, 63), (x + 6, 64), (x + 10, 73), "#A9885D", 2)
            p.curve((x, 82), (x - 8, 99), (x + 8, 106), (x + 3, 124), "#BC8C78", 3)
        p.path([("M", 107, 91), ("C", 108, 61, 171, 58, 181, 91),
                ("C", 165, 88, 153, 83, 144, 76), ("C", 135, 88, 120, 90, 107, 91)], ink)
        p.ellipse((138, 72, 151, 83), "#B8846D", V2.GOLD_LIGHT, 1)
    elif kind == "hood":
        p.path([("M", 99, 111), ("C", 80, 70, 115, 48, 145, 49),
                ("C", 184, 49, 207, 81, 191, 117), ("L", 182, 96),
                ("C", 178, 67, 111, 65, 108, 97)], "#D8DFCC", "#8CA695", 1)
        p.curve((101, 108), (91, 63), (186, 53), (191, 112), "#F2EBD5", 2.5)
        p.rounded((108, 78, 183, 90), 3, "#6B8B78", V2.GOLD, .8)
    elif kind == "ribbon":
        p.ellipse((128, 44, 162, 72), ink, "#788A72", .9)
        p.rounded((107, 75, 184, 88), 4, "#7F9D80", V2.GOLD_LIGHT, 1)
        p.line([(129, 49), (158, 64)], "#AFB692", 2.5)
        p.curve((176, 83), (206, 76), (211, 113), (234, 115), "#B8C4A1", 5)
        p.curve((176, 86), (194, 87), (188, 114), (208, 128), "#DBDEC1", 4)
    elif kind == "soft_cap":
        p.path([("M", 106, 87), ("L", 112, 57), ("C", 127, 47, 168, 50, 178, 62),
                ("L", 182, 87)], "#728F8D", "#A4B7A8", 1)
        p.curve((110, 80), (135, 73), (163, 81), (180, 77), "#CED5BF", 2)
        p.polygon([(111, 81), (96, 107), (110, 110), (124, 84)], "#879E94", "#AEBDA8", .7)
    else:
        p.path([("M", 103, 87), ("C", 103, 48, 185, 47, 186, 87),
                ("L", 178, 94), ("L", 110, 94)], "#7F8C6D", "#B9B58D", 1)
        p.rounded((102, 82, 186, 94), 4, "#9AA783", V2.GOLD_LIGHT, 1)
        p.ellipse((139, 80, 150, 91), "#D6C796", "#8F946B", .8)


def accessories(p, kind):
    if kind in ("scroll", "ledger", "cup"):
        p.polygon([(78, 185), (133, 190), (135, 228), (80, 225)], "#E4D8AE", "#A79A73", 1)
        for x in range(87, 130, 8):
            p.line([(x, 193), (x + 2, 221)], "#B5A87D", .7)
        p.line([(80, 190), (132, 195)], "#B9A36F", 3)
        if kind == "scroll":
            p.line([(208, 180), (246, 149)], "#7D7759", 3)
            p.path([("M", 244, 151), ("C", 249, 140, 254, 141, 259, 139),
                    ("C", 256, 150, 250, 156, 244, 151)], "#E5DEC3", "#9BA98D", .6)
        elif kind == "ledger":
            p.ellipse((194, 215, 235, 256), "#927E54", "#D4C38D", 1)
            p.rounded((206, 209, 223, 220), 4, "#A6A475", V2.GOLD, .8)
            p.ellipse((205, 229, 223, 244), "#C6B57E", V2.GOLD_LIGHT, 1)
            p.rounded((212, 234, 217, 239), 1, "#85774F")
        else:
            p.polygon([(213, 186), (239, 186), (234, 203), (217, 203)], "#B5C4B0", V2.GOLD_LIGHT, 1)
            p.line([(224, 204), (225, 211)], V2.GOLD, 2)
            p.ellipse((216, 208, 235, 212), "#B8C3A9")
    elif kind == "cane":
        V2._fan(p)
        p.polygon([(87, 175), (113, 177), (114, 210), (88, 211)], "#889D88", "#C1C7A8", .8)
        p.line([(95, 184), (105, 185), (97, 200)], "#425F50", 1.5)
    elif kind == "torch":
        p.rounded((79, 189, 124, 223), 3, "#E6D5AA", "#AA9C76", 1)
        for yy in (197, 205, 213):
            p.line([(86, yy), (116, yy)], "#8D9A79", .9)
        p.line([(80, 186), (80, 227)], "#A99A6C", 5)
    elif kind == "sabre":
        p.path([("M", 86, 180), ("L", 118, 185), ("L", 119, 221),
                ("C", 112, 236, 100, 241, 87, 226)], "#9EAE8E", "#D5D8B5", 1.3)
        p.line([(101, 191), (102, 229)], "#D9DABC", 2)
    elif kind == "double_bow":
        p.line([(87, 191), (124, 208)], "#BCA273", 3)
        p.line([(87, 197), (124, 214)], "#D1BD8C", 2)


def new_character(hero_id, enemy=False):
    look = LOOKS[hero_id]
    p = V2._Painter(SIZE)
    V2._base(p, enemy)
    weapon_back(p, look["weapon"])
    V2._garments(p, "新增人物", look)
    if hero_id == "xu_chu":
        # 重甲肩甲与宽腰建立魁梧轮廓，区别于长袍文士。
        for x in (101, 180):
            p.rounded((x - 15, 150, x + 27, 179), 13, "#A9A17A", "#6D7960", 1)
            p.line([(x - 9, 157), (x + 19, 158)], V2.GOLD_LIGHT, 2)
        p.rounded((111, 197, 185, 218), 7, "#777A5A", V2.GOLD, 1)
    elif hero_id == "guo_jia":
        p.path([("M", 106, 150), ("L", 122, 145), ("L", 146, 165),
                ("L", 171, 145), ("L", 188, 156), ("L", 172, 174),
                ("L", 146, 184), ("L", 120, 171)], "#E4E2C9", "#B3BEA7", .8)
    elif hero_id == "sima_yi":
        p.path([("M", 112, 146), ("L", 96, 160), ("L", 77, 252),
                ("L", 112, 258), ("L", 134, 168)], "#526B62", "#849B83", 1)
        p.path([("M", 174, 146), ("L", 191, 164), ("L", 218, 252),
                ("L", 184, 260), ("L", 156, 169)], "#42594F", "#7D9581", 1)
    elif hero_id == "sun_shangxiang":
        p.polygon([(112, 222), (183, 222), (190, 251), (103, 251)], "#C6907B", "#8C665C", 1)
        for x in (120, 137, 154, 171):
            p.line([(x, 226), (x - 3, 247)], V2.GOLD_LIGHT, .8)
    V2._arms(p, "新增人物", look)
    character_head(p, look)
    headwear(p, look["head"])
    accessories(p, look["weapon"])
    return p.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def enemy_art(kind):
    p = V2._Painter(SIZE)
    V2._base(p, True)
    coat = "#9C7260" if kind != "healer" else "#AAB09A"
    palette = dict(coat=coat, light=mix(coat, V2.PAPER, .4), dark=mix(coat, "#3E5146", .45),
                   cape="#927962", skin="#D3AC8C", armor=kind != "healer")
    if kind == "cavalry":
        # 马匹独立侧影，保持与步兵在手机缩图上的明显差别。
        p.ellipse((66, 192, 221, 255), "#897C64", "#5C6855", 1)
        for x in (88, 116, 181, 207):
            p.polygon([(x, 233), (x + 12, 233), (x + 5, 271), (x - 4, 271)], "#716A54", "#505D4D", .8)
        p.path([("M", 196, 215), ("L", 213, 155), ("L", 242, 146),
                ("L", 260, 165), ("L", 256, 182), ("L", 232, 181),
                ("L", 224, 231)], "#A3906D", "#676A53", 1)
        p.polygon([(216, 156), (216, 135), (229, 148)], "#89785C", "#5E6651", 1)
        p.polygon([(232, 149), (239, 134), (243, 153)], "#89785C", "#5E6651", 1)
        p.ellipse((241, 159, 246, 164), "#35483E")
        p.curve((212, 159), (200, 176), (207, 201), (196, 217), "#4D5948", 6)
        p.curve((69, 208), (35, 202), (52, 247), (34, 247), "#5C624C", 9)
        p.line([(232, 166), (255, 177), (208, 194)], "#C6B691", 1.8)
        p.rounded((117, 183, 185, 219), 8, "#A46E5B", V2.GOLD, 1)
        rider = V2._Painter(SIZE)
        V2._garments(rider, "敌兵", palette)
        V2._arms(rider, "敌兵", palette)
        look = dict(skin="#D3AC8C", face=(105, 80, 183, 153), eye=117, brow=2.5, head="crest", beard="none")
        character_head(rider, look)
        headwear(rider, "crest")
        reduced = rider.image.resize((round(p.resolution * .60), round(p.resolution * .60)), Image.Resampling.LANCZOS)
        p.image.alpha_composite(reduced, (round(56 * p.scale), round(25 * p.scale)))
        p.draw = ImageDraw.Draw(p.image)
        spear(p, 244, 44, 236)
    else:
        if kind == "archer":
            bow(p, 229, 81, 1.05)
        elif kind in ("infantry", "elite"):
            spear(p, 232, 36, 270, kind == "elite")
        elif kind == "healer":
            p.line([(231, 109), (229, 269)], "#8C8059", 4)
            p.polygon([(232, 112), (265, 120), (260, 150), (232, 145)], "#C3CBA8", "#7D9C7C", .8)
        V2._garments(p, "敌兵", palette)
        V2._arms(p, "敌兵", palette)
        look = dict(skin="#D3AC8C", face=(105, 80, 183, 153), eye=117, brow=2.5,
                    head="hood" if kind == "healer" else "tiger" if kind == "elite" else "wing", beard="none")
        character_head(p, look)
        if kind == "healer":
            headwear(p, "hood")
            p.rounded((80, 190, 124, 227), 5, "#D9DDC1", "#9EAC85", .9)
            p.line([(92, 208), (113, 208)], "#7C9B78", 4)
            p.line([(102, 198), (102, 218)], "#7C9B78", 4)
        else:
            headwear(p, "tiger" if kind == "elite" else "crest")
            p.rounded((102, 80, 187, 90), 3, "#A56B57", V2.GOLD, .8)
        if kind == "shield":
            p.path([("M", 72, 165), ("L", 125, 165), ("L", 128, 218),
                    ("C", 119, 240, 101, 252, 76, 226)], "#8D9271", "#C9BA88", 2)
            p.line([(97, 171), (101, 238)], "#D7C997", 4)
            for y in (180, 205, 225):
                p.ellipse((83, y, 87, y + 4), V2.GOLD_LIGHT)
        elif kind == "elite":
            for x in (100, 180):
                p.rounded((x - 12, 152, x + 28, 174), 8, "#84917D", V2.GOLD, 1)
            p.polygon([(107, 167), (180, 167), (180, 205), (144, 219), (107, 205)], "#6D8270", "#ADB695", 1.2)
    return p.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def point(xy):
    return {"X": round(xy[0], 3), "Y": round(xy[1], 3)}


def route(route_id, points):
    return {"Id": route_id, "Points": [point(p) for p in TERRAIN._v3._curve(points)]}


def slot_rows(points):
    return [{"Id": index + 1, "Position": point(p)} for index, p in enumerate(points)]


def make_levels():
    common = tuple(TERRAIN.ROUTES[2]) + ((1752, 560),)
    hulao_routes = [route("north", TERRAIN.ROUTES[0] + common[1:]),
                    route("south", TERRAIN.ROUTES[1] + common[1:])]
    plans = [
        ("hulao", "虎牢合战", "两路合流 · 关前列阵", "山口战鼓再起。先以两翼消耗来敌，再于关前截住张辽与吕布。", "山口关城", ["zhang_liao", "lv_bu"],
         hulao_routes, list(TERRAIN.EMPTY_SLOTS + TERRAIN.DEPLOYED),
         ["guan_yu", "zhao_yun", "zhang_fei", "huang_zhong", "zhou_yu", "zhuge_liang", "sun_shangxiang", "cao_cao", "hua_tuo", "lu_su"]),
        ("huangjin", "黄巾演武", "单路回转 · 长线牵制", "黄旗穿过郊野，法坛守着回转长路。把握往返两段的共同覆盖，破除张角的纸兵阵势。", "黄土郊野", ["zhang_jiao"],
         [route("u_road", [(70, 300), (1470, 300), (1700, 415), (1700, 605), (1550, 700), (180, 700)])],
         [(170, 215), (340, 215), (510, 215), (680, 215), (850, 215), (1020, 215), (1190, 215), (1400, 215),
          (1650, 250), (1480, 420), (1800, 465), (1540, 595), (1740, 720), (1600, 795),
          (1410, 790), (1210, 790), (1010, 790), (810, 790), (610, 790), (410, 790), (240, 790),
          (430, 590), (690, 590), (950, 590), (1210, 590),
          (300, 410), (560, 410), (820, 410), (1080, 410), (1340, 410)],
         ["guan_yu", "huang_zhong", "zhao_yun", "zhou_yu", "lu_xun", "zhang_fei", "zhuge_liang", "ma_chao", "hua_tuo", "lu_su"]),
        ("guandu", "官渡沙盘", "长短双弧 · 分路预备", "粮营与浅河之间，两股来军分走长短道路。预留后段军饷，迎击袁绍的援军。", "浅河粮营", ["yuan_shao"],
         [route("long_arc", [(70, 500), (340, 300), (680, 270), (1040, 290), (1370, 340), (1580, 480), (1752, 510)]),
          route("short_arc", [(70, 500), (390, 660), (800, 690), (1160, 650), (1500, 570), (1752, 510)])],
         [(150, 350), (350, 195), (540, 185), (730, 175), (920, 185), (1120, 205), (1320, 235), (1515, 300), (1690, 375),
          (180, 610), (315, 735), (500, 770), (700, 790), (900, 780), (1100, 750), (1300, 705), (1510, 670), (1690, 640),
          (370, 425), (560, 410), (750, 405), (940, 420), (1130, 445), (1320, 470), (1490, 350), (1590, 650),
          (390, 550), (590, 570), (790, 575), (990, 555), (1190, 550), (1410, 520),
          (1800, 735), (1650, 785), (1430, 800), (1180, 815)],
         ["guan_yu", "huang_zhong", "taishi_ci", "zhao_yun", "zhou_yu", "lu_xun", "zhuge_liang", "lv_meng", "pang_tong", "lu_su"]),
        ("chibi", "赤壁两岸", "两岸并进 · 中台支援", "江面隔开两岸，栈桥把来军导向同一座城。先破许褚重甲，再拆曹操护卫的援护。", "江岸水寨", ["xu_chu", "cao_cao"],
         [route("north_bank", [(70, 335), (440, 335), (820, 315), (1220, 335), (1480, 390), (1630, 520), (1752, 520)]),
          route("south_bank", [(70, 700), (430, 690), (790, 690), (1180, 680), (1450, 645), (1630, 520), (1752, 520)])],
         [(170, 230), (350, 230), (540, 220), (730, 215), (920, 215), (1110, 225), (1300, 245), (1490, 285), (1685, 360),
          (170, 800), (365, 800), (560, 800), (755, 800), (950, 795), (1145, 790), (1340, 755), (1535, 735), (1730, 655),
          (205, 435), (420, 435), (625, 425), (830, 425), (1040, 435), (1250, 445), (1460, 495),
          (210, 590), (425, 590), (640, 585), (850, 585), (1060, 575), (1270, 565),
          (770, 505), (950, 505), (1130, 505), (1815, 760), (1810, 270)],
         ["guan_yu", "huang_zhong", "taishi_ci", "zhao_yun", "zhou_yu", "zhuge_liang", "ma_chao", "diao_chan", "sima_yi", "hua_tuo"]),
        ("finale", "群雄终局", "三路围城 · 英杰同守", "三方旌旗向中央城池汇聚。关羽先行压阵，终场吕布冲锋与司马懿援护交错而来。", "中央石城", ["guan_yu", "lv_bu", "sima_yi"],
         [route("northwest", [(70, 270), (470, 270), (710, 350), (860, 440), (1080, 520)]),
          route("southwest", [(70, 750), (470, 750), (700, 680), (850, 595), (1080, 520)]),
          route("east", [(1850, 350), (1570, 350), (1450, 410), (1350, 520), (1080, 520)])],
         [(160, 180), (350, 180), (540, 180), (730, 230), (920, 285), (1110, 300), (1300, 285), (1490, 250), (1690, 250), (1810, 470),
          (160, 370), (350, 370), (540, 390), (700, 445), (825, 535), (1000, 645), (1190, 645), (1370, 625), (1540, 550), (1720, 550),
          (170, 650), (350, 650), (530, 620), (700, 575), (850, 680), (1030, 765), (1220, 765), (1410, 740), (1600, 710), (1790, 620),
          (160, 820), (360, 820), (560, 820), (740, 785), (1060, 200), (1250, 190), (1440, 180), (1460, 650), (1610, 450), (1850, 610)],
         ["huang_zhong", "lu_xun", "zhou_yu", "cao_cao", "guo_jia", "taishi_ci", "zhuge_liang", "sun_shangxiang", "zhao_yun", "diao_chan"]),
    ]
    levels = []
    for index, (slug, name, subtitle, story, theme, bosses, routes, positions, roster) in enumerate(plans):
        levels.append(dict(Id=slug, Name=name, Subtitle=subtitle, Story=story, Theme=theme,
                           MapResource=f"Game/Maps/{slug}", InitialGold=330 + index * 15,
                           IncomePerSecond=2.3, CastleMaxHealth=30, BossHeroIds=bosses,
                           RecommendedRoster=roster, Routes=routes, Slots=slot_rows(positions),
                           Waves=make_waves(index, slug, [r["Id"] for r in routes])))
        if slug == "huangjin":
            levels[-1]["CombatRuleVersion"] = "sensation-hj-mn-v1"
    return levels


def group(enemy, route_id, count, interval=1.65, delay=0, health=1, speed=1):
    return dict(EnemyId=enemy, RouteId=route_id, Count=count, Interval=interval, Delay=delay,
                HealthScale=round(health, 3), SpeedScale=round(speed, 3))


def huangjin_batches(source):
    """主兵拆为三至四人的短批，首兵与末兵保持原出场时刻。"""
    sizes = [3] * (source["Count"] // 3)
    for offset in range(source["Count"] % 3):
        sizes[-1 - offset] += 1
    interval = .55
    last_at = source["Delay"] + (source["Count"] - 1) * source["Interval"]
    last_start = last_at - (sizes[-1] - 1) * interval
    return [dict(source, Count=count, Interval=interval,
                 Delay=round(source["Delay"] + (last_start - source["Delay"]) * index / (len(sizes) - 1), 6))
            for index, count in enumerate(sizes)]


def make_waves(difficulty, slug, route_ids):
    names = ["前哨试探", "左右鸣鼓", "盾列推进", "快马传锋", "中军压阵", "整备再战", "弓盾相随", "精锐突阵", "三鼓齐进", "主将临关"]
    waves = []
    for index, name in enumerate(names):
        if slug in ("huangjin", "chibi", "finale") and index in (1, 5):
            continue
        if slug == "guandu" and index == 1:
            continue
        health = 1 + difficulty * .17 + index * .075
        if slug == "chibi":
            health *= .72
        elif slug == "guandu":
            health *= .85
        elif slug == "finale":
            health *= .56 if index < 2 else .60
        amount = 9 + index // 2 + difficulty
        kind = "infantry" if index < 2 else "shield" if index in (2, 6) else "cavalry" if index in (3, 8) else "elite" if index == 7 else "infantry"
        groups = []
        for ri, rid in enumerate(route_ids):
            count = amount if len(route_ids) == 1 else max(5, amount - 3)
            if slug == "finale":
                count = 4 + index // 3
            delay = ri * (10 if slug == "finale" else 3.5)
            interval = 1.6 if slug == "huangjin" else 1.8
            main_group = group(kind, rid, count, interval if kind != "cavalry" else 1.35, delay, health)
            if slug == "huangjin" and len(waves) >= 3:
                if len(waves) == 3:
                    # 第四波四批各四人，保持原0/17.6首末时刻；其他波不改。
                    last_start = (count - 1) * main_group["Interval"] - 3 * .55
                    groups.extend(dict(main_group, Count=4, Interval=.55, Delay=round(delay + n * last_start / 3, 6)) for n in range(4))
                else:
                    groups.extend(huangjin_batches(main_group))
            else:
                groups.append(main_group)
            if index >= 5:
                groups.append(group("archer", rid, 3 if slug == "finale" else 3 + difficulty, 2.4, 12 + ri * 2, health * .95))
        if index in (6, 8) and difficulty >= 2:
            groups.append(group("healer", route_ids[-1], 2, 6, 8, health))
        if slug == "hulao" and index == 4:
            groups.append(group("boss_zhang_liao", route_ids[0], 1, delay=8))
        elif slug == "chibi" and index == 4:
            groups.append(group("boss_xu_chu", route_ids[-1], 1, delay=8, health=.52))
        elif slug == "finale" and index == 4:
            groups.append(group("boss_guan_yu", route_ids[0], 1, delay=9, health=.32, speed=.80))
        if index == 9:
            boss = {"hulao": "lv_bu", "huangjin": "zhang_jiao", "guandu": "yuan_shao", "chibi": "cao_cao", "finale": "lv_bu"}[slug]
            groups.append(group("boss_" + boss, route_ids[0], 1, delay=12,
                                health=.75 if slug in ("finale", "guandu") else .52 if slug == "chibi" else 1))
            if slug == "finale":
                groups.append(group("boss_sima_yi", route_ids[-1], 1, delay=22, health=.60))
        waves.append(dict(Name=name, StartDelay=2 if index == 0 else 12 if slug == "hulao" else 8, Groups=groups))
    return waves


def make_database():
    heroes = [dict(Id=slug, Name=name, Faction=HERO_FACTIONS[slug], Title=title, Role=role, Description=description, Skill=skill,
                   SpriteId=f"Game/Characters/{slug}", Accent=accent, Cost=cost, Range=reach,
                   Damage=damage, AttackInterval=interval)
              for slug, name, title, role, skill, cost, reach, damage, interval, accent, description in HERO_ROWS]
    for hero in heroes:
        interval, basic_scale, cooldown, skill_scale = HERO_COMBAT_BUDGETS[hero["Id"]]
        hero.update(BasicAttackInterval=interval, BasicDamageScale=basic_scale,
                    SkillCooldown=cooldown, SkillDamageScale=skill_scale)
    enemies = []
    for slug, name, skill, health, speed, armor, castle, reward in [
        ("infantry", "执矛步兵", "normal", 66, 53, 0, 1, 7),
        ("shield", "持盾甲兵", "shield", 108, 43, .18, 1, 10),
        ("cavalry", "轻骑先锋", "runner", 76, 82, 0, 2, 10),
        ("archer", "弓手", "normal", 58, 58, 0, 1, 8),
        ("elite", "重甲精锐", "armored", 190, 39, .28, 2, 16),
        ("healer", "军医旗兵", "healer", 92, 45, .05, 1, 12),
    ]:
        enemies.append(dict(Id=slug, Name=name, CharacterId="", SpriteId=f"Game/Enemies/{slug}", Skill=skill,
                            MaxHealth=health, Speed=speed, Armor=armor, CastleDamage=castle, GoldReward=reward, IsBoss=False))
    hero_names = {hero["Id"]: hero["Name"] for hero in heroes}
    for slug, skill, health, speed, armor, reward in [
        ("zhang_liao", "charge", 1500, 42, .10, 130), ("lv_bu", "charge", 2800, 36, .12, 240),
        ("zhang_jiao", "summon", 2350, 36, .05, 220), ("yuan_shao", "summon", 3000, 39, .10, 260),
        ("xu_chu", "guard", 2500, 32, .25, 180), ("cao_cao", "guard", 3300, 36, .15, 270),
        ("guan_yu", "rage", 3300, 37, .14, 220), ("sima_yi", "disrupt", 3000, 34, .10, 260),
    ]:
        enemies.append(dict(Id="boss_" + slug, Name=hero_names[slug], CharacterId=slug,
                            SpriteId=f"Game/Bosses/{slug}", Skill=skill, MaxHealth=health, Speed=speed,
                            Armor=armor, CastleDamage=15, GoldReward=reward, IsBoss=True))
    return dict(SchemaVersion=1, Heroes=heroes, Enemies=enemies, Levels=make_levels())


def font(size, title=False):
    paths = ["C:/Windows/Fonts/simkai.ttf" if title else "C:/Windows/Fonts/msyh.ttc",
             "C:/Windows/Fonts/msyh.ttc"]
    for path in paths:
        if Path(path).is_file():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def castle(p, x, y, central=False):
    """入口位置是路线终点；建筑向上生长，不遮挡进城中心线。"""
    scale = 1.25 if central else 1
    def pts(values):
        return [(x + a * scale, y + b * scale) for a, b in values]
    p.ellipse((x - 70 * scale, y - 20 * scale, x + 84 * scale, y + 31 * scale), "#6C805E")
    p.polygon(pts([(-51, -56), (37, -79), (67, -58), (67, 5), (-13, 29), (-51, 6)]), "#A6AC8A")
    p.polygon(pts([(-51, -56), (-13, -35), (-13, 29), (-51, 6)]), "#829879")
    p.polygon(pts([(-13, -35), (67, -58), (67, 5), (-13, 29)]), "#929E7E")
    p.ellipse((x - 14, y - 19, x + 14, y + 9), "#2F5146")
    p.polygon([(x - 14, y - 5), (x + 14, y - 5), (x + 14, y + 18), (x - 14, y + 18)], "#2F5146")
    for yy in (-42, -25, -8):
        p.line(pts([(17, yy), (59, yy - 12)]), "#6D856C", .8)
    p.polygon(pts([(-44, -71), (17, -87), (48, -75), (48, -51), (-12, -34), (-44, -46)]), "#A6885E")
    p.polygon(pts([(-12, -62), (41, -77), (41, -57), (-12, -42)]), "#61735B")
    for xx in (-38, -13, 12, 41):
        p.line(pts([(xx, -68), (xx, -47)]), "#D0B883", 2)
    p.polygon(pts([(-69, -72), (-15, -107), (63, -82), (77, -63), (-12, -40)]), "#466F59")
    p.polygon(pts([(-69, -72), (-15, -107), (25, -86), (-12, -45)]), "#759879")
    p.line(pts([(-69, -72), (-12, -42), (77, -63)]), "#C3C99B", 2)
    p.line(pts([(-15, -107), (25, -86), (63, -82)]), "#CDD0A2", 1.2)
    p.line(pts([(31, -83), (31, -135)]), "#A09264", 2)
    p.polygon(pts([(32, -134), (64, -128), (60, -111), (32, -116)]), "#579784", "#AAC8A1", .9)


def map_art(level):
    if level["Id"] == "hulao":
        # v4 内部引用 v3 模块；只临时替换内存元组，finally 恢复，磁盘源稿永不写入。
        previous = TERRAIN._v3.EMPTY_SLOTS
        try:
            TERRAIN._v3.EMPTY_SLOTS = ()
            return TERRAIN.make_terrain(1)
        finally:
            TERRAIN._v3.EMPTY_SLOTS = previous
    scale = 2
    rng = random.Random(20261104 + sum(map(ord, level["Id"])))
    canvas = Image.new("RGBA", (1920 * scale, 1080 * scale), "#193E37")
    painter = MapPainter(scale)
    painter.rounded((25, 139, 1898, 871), 29, (7, 29, 24, 100))
    canvas.alpha_composite(painter.blur(9))
    painter = MapPainter(scale)
    painter.rounded((24, 128, 1896, 858), 27, "#384E39", "#6F8258", 1)
    painter.rounded((30, 133, 1890, 849), 25, "#718358")
    canvas.alpha_composite(painter.image)
    ground, board_mask = TERRAIN._v3._ground(scale, rng)
    if level["Id"] == "huangjin":
        tint = Image.new("RGBA", canvas.size, (177, 165, 101, 55))
        tint.putalpha(board_mask.point(lambda v: v * 55 // 255))
        ground.alpha_composite(tint)
    canvas.alpha_composite(ground)
    p = MapPainter(scale)
    if level["Id"] == "chibi":
        river = [(45, 464), (340, 467), (565, 451), (740, 469), (900, 449), (1220, 467),
                 (1450, 490), (1555, 518), (1440, 559), (1190, 585), (870, 560), (650, 572), (360, 558), (45, 562)]
        p.polygon([(x, y + 8) for x, y in river], "#617F6A")
        p.polygon(river, "#6E9B8D")
        p.line(river[:8], "#B5C5A0", 3)
        p.line(river[8:] + [river[0]], "#91B297", 2)
        for i in range(28):
            x, y = rng.randint(70, 1460), rng.randint(492, 542)
            p.line([(x, y), (x + rng.randint(16, 40), y - 1)], "#9EB9A1", .8)
        p.polygon([(711, 475), (1151, 476), (1220, 515), (1139, 542), (731, 542), (687, 510)], "#A0B28B")
        p.line([(711, 475), (1151, 476), (1220, 515)], "#C2CCA1", 2)
        # 水寨小舟放在河流边缘，保持中心支援高台可点击。
        for x, y in ((290, 515), (1350, 525)):
            p.polygon([(x - 43, y), (x - 26, y + 16), (x + 34, y + 11), (x + 47, y - 5)], "#8A7E57")
            p.line([(x - 34, y), (x + 36, y - 3)], "#D0BC86", 2)
            p.line([(x, y + 6), (x, y - 38)], "#A69665", 2)
            p.polygon([(x + 1, y - 36), (x + 30, y - 18), (x + 1, y - 14)], "#D4CC9C")
    elif level["Id"] == "guandu":
        p.line([(105, 175), (240, 280), (260, 425), (550, 510), (930, 500), (1350, 650), (1550, 830)], "#6C9D8A", 30)
        p.line([(105, 175), (240, 280), (260, 425), (550, 510), (930, 500), (1350, 650), (1550, 830)], "#95B69B", 18)
    canvas.alpha_composite(p.image)
    # 路面与运行时路径严格取同一采样点，不烘焙阵位、人物或文字。
    for width, color, offset in ((85, "#A49E70", (0, 3)), (77, "#D9C69A", (0, 0)), (68, "#DECCA3", (0, 0))):
        p = MapPainter(scale)
        for path in level["Routes"]:
            points = [(q["X"] + offset[0], q["Y"] + offset[1]) for q in path["Points"]]
            p.line(points, color, width)
            for x, y in (points[0], points[-1]):
                p.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), color)
        canvas.alpha_composite(p.image)
    p = MapPainter(scale)
    # 草丛与石块避让阵位、道路；边角保留环境层次。
    for _ in range(360):
        x, y = rng.randint(65, 1850), rng.randint(160, 820)
        if any(math.hypot(x - s["Position"]["X"], y - s["Position"]["Y"]) < 53 for s in level["Slots"]):
            continue
        if any(distance_to_route((x, y), path) < 55 for path in level["Routes"]):
            continue
        p.line([(x - 3, y + 1), (x + 1, y - 3), (x + 6, y)], "#7D9B6B", .8)
    # 不同关卡的远景建筑与标识轮廓。
    if level["Id"] == "huangjin":
        for x, y in ((340, 505), (650, 505), (960, 500), (1260, 505)):
            p.ellipse((x - 30, y - 10, x + 35, y + 12), "#929968")
            for k in (-18, -6, 6, 18):
                p.line([(x + k, y - 4), (x + k + 3, y - 27)], "#B9AC65", 2)
                p.line([(x + k, y - 18), (x + k - 5, y - 26)], "#D1BF7B", 2)
        p.line([(90, 610), (90, 527)], "#998753", 3)
        p.polygon([(92, 529), (137, 540), (131, 567), (92, 563)], "#D0B86C", "#E2CF8D", 1)
    elif level["Id"] == "guandu":
        for x, y in ((480, 470), (650, 490), (1200, 360), (1380, 775)):
            p.ellipse((x - 25, y, x + 30, y + 16), "#7D906A")
            p.rounded((x - 19, y - 32, x + 22, y + 8), 5, "#B9AF7A", "#8B9164", 1)
            p.polygon([(x - 24, y - 30), (x, y - 49), (x + 27, y - 28)], "#C9BB83", "#8D9263", .8)
            p.line([(x - 12, y - 25), (x - 12, y + 4)], "#D7CCA0", 1)
    elif level["Id"] == "finale":
        for x, y, color in ((102, 520, "#B07C60"), (1750, 175, "#8A90A2"), (880, 180, "#AFAB72")):
            p.line([(x, y), (x, y - 45)], "#9A8C61", 3)
            p.polygon([(x + 2, y - 44), (x + 38, y - 38), (x + 33, y - 17), (x + 2, y - 22)], color, "#D4C89F", 1)
    terminal = level["Routes"][0]["Points"][-1]
    castle(p, terminal["X"], terminal["Y"], level["Id"] == "finale")
    p.image.putalpha(ImageChops.multiply(p.image.getchannel("A"), board_mask))
    canvas.alpha_composite(p.image)
    return canvas.resize((1920, 1080), Image.Resampling.LANCZOS)


def distance_to_route(position, path):
    distance = 10000
    px, py = position
    for a, b in zip(path["Points"], path["Points"][1:]):
        ax, ay, bx, by = a["X"], a["Y"], b["X"], b["Y"]
        length = (bx - ax) ** 2 + (by - ay) ** 2
        t = max(0, min(1, ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / length)) if length else 0
        distance = min(distance, math.hypot(px - ax - t * (bx - ax), py - ay - t * (by - ay)))
    return distance


def avatar(art, slug):
    crop = (42, 0, 250, 202) if slug == "lv_bu" else (77, 23, 215, 181)
    art = art.crop(tuple(round(v * SIZE / 320) for v in crop))
    art.thumbnail((304, 304), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (320, 320))
    result.alpha_composite(art, ((320 - art.width) // 2, (320 - art.height) // 2))
    return result


def save_image(image, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    image.save(target)
    with Image.open(target) as check:
        check.verify()


def review_roster(heroes):
    sheet = Image.new("RGB", (2000, 2220), "#F1EDE1")
    d = ImageDraw.Draw(sheet)
    d.text((40, 28), "演义沙盘 · 二十五将", fill="#244E41", font=font(62, True))
    d.text((42, 109), "7 位沿用 v4 精修，5 位沿用专属造型，13 位新增原创造型；仅作为运行时首版内容。", fill="#74816C", font=font(25))
    for index, hero in enumerate(heroes):
        x, y = 34 + index % 5 * 393, 171 + index // 5 * 401
        d.rounded_rectangle((x, y, x + 375, y + 384), 18, fill="#FAF7ED", outline="#D7DCCB", width=2)
        art = Image.open(OUTPUT / "Characters" / (hero["Id"] + ".png")).convert("RGBA")
        art.thumbnail((302, 302), Image.Resampling.LANCZOS)
        sheet.paste(art, (x + (375 - art.width) // 2, y + 7), art)
        d.text((x + 187, y + 315), hero["Name"], anchor="mm", fill="#294F41", font=font(39, True))
        d.text((x + 187, y + 356), hero["Title"] + " · " + hero["Role"], anchor="mm", fill="#7D826D", font=font(20))
    d.text((43, 2190), "头像、全身图与身份使用同一ID；脚下台座中心统一为320设计坐标(150,275)。", fill="#78816D", font=font(22), anchor="lb")
    save_image(sheet, ROSTER_REVIEW)


def review_maps(levels):
    sheet = Image.new("RGB", (1920, 1870), "#F1EDE1")
    d = ImageDraw.Draw(sheet)
    d.text((35, 25), "五关地图 · 路线与阵位检视", fill="#244E41", font=font(45, True))
    for index, level in enumerate(levels):
        x, y = (index % 2) * 960, 95 + (index // 2) * 585
        image = Image.open(OUTPUT / "Maps" / (level["Id"] + ".png")).convert("RGBA")
        overlay = ImageDraw.Draw(image)
        for slot in level["Slots"]:
            sx, sy = slot["Position"]["X"], slot["Position"]["Y"]
            overlay.ellipse((sx - 24, sy - 10, sx + 24, sy + 10), fill="#BED09D", outline="#ECDFC0", width=2)
            overlay.text((sx, sy - 1), str(slot["Id"]), anchor="mm", font=font(16), fill="#365C49")
        for path in level["Routes"]:
            points = [(p["X"], p["Y"]) for p in path["Points"]]
            overlay.line(points, fill="#A45C4D", width=4)
        sheet.paste(image.resize((960, 540), Image.Resampling.LANCZOS), (x, y + 40))
        d.text((x + 27, y + 13), f'{index + 1:02} {level["Name"]} · {len(level["Slots"])}阵位 · {len(level["Routes"])}路线',
               fill="#355B46", font=font(25), anchor="lm")
    save_image(sheet, MAP_REVIEW)


def legacy_fingerprint():
    paths = list((ART / "assets-v4").rglob("*"))
    paths += [ART / name for name in ("battle-ui-concept-v3.png", "character-study-v3.png", "terrain_art_v4.py", "terrain_art_v3.py", "portrait_art_v4.py", "portrait_art_v3.py")]
    return {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths if path.is_file()}


def verify_database(database):
    errors = []
    hero_ids = {h["Id"] for h in database["Heroes"]}
    enemies = {e["Id"]: e for e in database["Enemies"]}
    if len(hero_ids) != 25 or len(database["Heroes"]) != 25:
        errors.append("角色数量或唯一性错误")
    if len(database["Levels"]) != 5:
        errors.append("关卡数量错误")
    # 用实际 C# 字段检查全部 JSON 键，避免 JsonUtility 静默丢字段。
    source = (ROOT / "UnityGame/Assets/Scripts/Domain/Definitions.cs").read_text(encoding="utf-8-sig")
    import re
    schemas = {}
    for name in ("GameDatabase", "HeroDefinition", "EnemyDefinition", "LevelDefinition", "RouteDefinition", "SlotDefinition", "WaveDefinition", "SpawnGroup", "Point2"):
        body = re.search(r"(?:class|struct)\s+" + name + r"\s*\{(.*?)(?=\n    \})", source, re.S).group(1)
        schemas[name] = set(re.findall(r"public\s+(?:List<\w+>|\w+)\s+(\w+)\s*(?:;|=)", body))
    children = {"GameDatabase": {"Heroes": "HeroDefinition", "Enemies": "EnemyDefinition", "Levels": "LevelDefinition"},
                "LevelDefinition": {"Routes": "RouteDefinition", "Slots": "SlotDefinition", "Waves": "WaveDefinition"},
                "RouteDefinition": {"Points": "Point2"}, "SlotDefinition": {"Position": "Point2"},
                "WaveDefinition": {"Groups": "SpawnGroup"}}
    def check_keys(value, schema):
        optional = {"CombatRuleVersion"} if schema == "LevelDefinition" else set()
        if set(value) - schemas[schema] or schemas[schema] - set(value) - optional:
            errors.append(f"{schema}字段不同：{set(value) ^ schemas[schema]}")
        for key, next_schema in children.get(schema, {}).items():
            sequence = value[key] if isinstance(value[key], list) else [value[key]]
            for item in sequence:
                check_keys(item, next_schema)
    check_keys(database, "GameDatabase")
    for hero in database["Heroes"]:
        if hero.get("Faction") != HERO_FACTIONS.get(hero["Id"]) or hero["Id"] not in HERO_FACTIONS:
            errors.append("角色固定阵营错误：" + hero["Id"])
        if hero["Cost"] <= 0 or hero["Range"] <= 0 or hero["AttackInterval"] <= 0:
            errors.append("角色数值非法：" + hero["Id"])
        for field in ("BasicAttackInterval", "BasicDamageScale", "SkillCooldown", "SkillDamageScale"):
            if not math.isfinite(hero[field]) or hero[field] <= 0:
                errors.append("角色独立节奏预算非法：" + hero["Id"] + "/" + field)
    for level in database["Levels"]:
        if not 30 <= len(level["Slots"]) <= 40 or len({s["Id"] for s in level["Slots"]}) != len(level["Slots"]):
            errors.append("阵位数量或ID错误：" + level["Id"])
        if len(level["RecommendedRoster"]) > 10 or set(level["BossHeroIds"]) & set(level["RecommendedRoster"]):
            errors.append("推荐阵容包含本关Boss或超过10人：" + level["Id"])
        if not set(level["BossHeroIds"] + level["RecommendedRoster"]) <= hero_ids:
            errors.append("关卡引用不存在角色")
        routes = {r["Id"] for r in level["Routes"]}
        if not 8 <= len(level["Waves"]) <= 12:
            errors.append("波次数量错误")
        used_bosses = set()
        for wave in level["Waves"]:
            for spawn in wave["Groups"]:
                if spawn["EnemyId"] not in enemies or spawn["RouteId"] not in routes:
                    errors.append("出兵引用错误")
                    continue
                enemy = enemies[spawn["EnemyId"]]
                if enemy["IsBoss"]:
                    used_bosses.add(enemy["CharacterId"])
                if spawn["Count"] < 1 or spawn["Interval"] <= 0 or spawn["HealthScale"] <= 0:
                    errors.append("出兵数值错误")
        if used_bosses != set(level["BossHeroIds"]):
            errors.append("Boss情报未覆盖实际全部Boss：" + level["Id"])
        endpoints = {(r["Points"][-1]["X"], r["Points"][-1]["Y"]) for r in level["Routes"]}
        if len(endpoints) != 1:
            errors.append("路线没有汇至同一城池：" + level["Id"])
        for slot in level["Slots"]:
            x, y = slot["Position"]["X"], slot["Position"]["Y"]
            if not 60 <= x <= 1860 or not 175 <= y <= 825:
                errors.append("阵位超出可点击区域：" + level["Id"])
            legacy_approved = level["Id"] == "hulao" and (x, y) == (810, 560)
            if not legacy_approved and min(distance_to_route((x, y), path) for path in level["Routes"]) < 45:
                errors.append(f'{level["Id"]}阵位{slot["Id"]}压住道路')
            if min(distance_to_route((x, y), path) for path in level["Routes"]) > 330:
                errors.append(f'{level["Id"]}阵位{slot["Id"]}超出全部角色射程')
        for index, first in enumerate(level["Slots"]):
            for second in level["Slots"][index + 1:]:
                if math.dist(first["Position"].values(), second["Position"].values()) < 54:
                    errors.append(f'{level["Id"]}阵位点击区域重叠')
        cheapest = sorted(next(h["Cost"] for h in database["Heroes"] if h["Id"] == slug) for slug in level["RecommendedRoster"])
        if level["InitialGold"] < sum(cheapest[:2]):
            errors.append("初始军饷不足部署2人")
    if errors:
        raise RuntimeError("内容验证失败：\n" + "\n".join(errors))


def verify_resources(database):
    resources = [(h["SpriteId"], (SIZE, SIZE), True) for h in database["Heroes"]]
    resources += [(f'Game/Portraits/{h["Id"]}', (320, 320), True) for h in database["Heroes"]]
    resources += [(e["SpriteId"], (SIZE, SIZE), True) for e in database["Enemies"]]
    resources += [(l["MapResource"], (1920, 1080), False) for l in database["Levels"]]
    resources += [("Game/Effects/" + slug + "_atlas", (2048, 256), True) for slug in EFFECTS]
    for resource, expected_size, transparent in resources:
        path = OUTPUT.parent / (resource + ".png")
        with Image.open(path) as art:
            art.load()
            if art.size != expected_size or art.mode != "RGBA":
                raise RuntimeError("素材规格错误：" + resource)
            if transparent:
                low, high = art.getchannel("A").getextrema()
                if low != 0 or high < 1 or ("/Effects/" not in resource and high != 255):
                    raise RuntimeError("透明度错误：" + resource)
                corners = [(0, 0), (art.width - 1, 0), (0, art.height - 1), (art.width - 1, art.height - 1)]
                if any(art.getpixel(p)[3] for p in corners):
                    raise RuntimeError("透明素材四角不透明：" + resource)
                if resource.startswith(("Game/Characters/", "Game/Bosses/", "Game/Enemies/")):
                    bbox = art.getchannel("A").getbbox()
                    if min(bbox[:2]) < 1 or bbox[2] >= art.width or bbox[3] >= art.height:
                        raise RuntimeError("全身素材碰到画布边缘：" + resource)
                    expected_base = (240, 233, 208, 255) if "/Characters/" in resource else (217, 166, 139, 255)
                    if art.getpixel((360, 660)) != expected_base:
                        raise RuntimeError("敌我台座颜色错误：" + resource)
    return len(set(resource for resource, _, _ in resources))


def export():
    before = legacy_fingerprint()
    database = make_database()
    verify_database(database)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for hero in database["Heroes"]:
        slug = hero["Id"]
        if slug in REFINED and slug != "lv_bu":
            target = OUTPUT / "Characters" / (slug + ".png")
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ART / "assets-v4/characters" / (slug + ".png"), target)
            art = Image.open(target).convert("RGBA")
        elif slug == "lv_bu":
            art = V4.make_character(hero["Name"], SIZE, False)
            save_image(art, OUTPUT / "Characters" / (slug + ".png"))
        elif slug in LEGACY:
            art = V2.make_character(hero["Name"], SIZE)
            save_image(art, OUTPUT / "Characters" / (slug + ".png"))
        else:
            art = new_character(slug)
            save_image(art, OUTPUT / "Characters" / (slug + ".png"))
        save_image(avatar(art, slug), OUTPUT / "Portraits" / (slug + ".png"))
    for enemy in database["Enemies"]:
        if not enemy["IsBoss"]:
            save_image(enemy_art(enemy["Id"]), OUTPUT / "Enemies" / (enemy["Id"] + ".png"))
        else:
            slug = enemy["CharacterId"]
            if slug in REFINED:
                art = V4.make_character(enemy["Name"], SIZE, True)
            elif slug in LEGACY:
                art = V2.make_character(enemy["Name"], SIZE, True)
            else:
                art = new_character(slug, True)
            save_image(art, OUTPUT / "Bosses" / (slug + ".png"))
    for level in database["Levels"]:
        save_image(map_art(level), OUTPUT / "Maps" / (level["Id"] + ".png"))
    for slug in EFFECTS:
        target = OUTPUT / "Effects" / (slug + "_atlas.png")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ART / "assets-v4/effects" / (slug + "_atlas.png"), target)
    (OUTPUT / "database.json").write_text(json.dumps(database, ensure_ascii=False, indent=2), encoding="utf-8")
    review_roster(database["Heroes"])
    review_maps(database["Levels"])
    asset_count = verify_resources(database)
    if before != legacy_fingerprint():
        raise RuntimeError("已有视觉稿指纹变化")
    assets = []
    for folder in ("Characters", "Portraits", "Bosses", "Enemies", "Maps", "Effects"):
        for path in sorted((OUTPUT / folder).glob("*.png")):
            with Image.open(path) as art:
                assets.append(dict(Path=str(path.relative_to(ROOT)).replace("\\", "/"), Size=list(art.size),
                                   Mode=art.mode, Sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    manifest = dict(SchemaVersion=1, State="Unity运行时首版内容，等待实际试玩调参", HeroCount=25,
                    LevelCount=5, EnemyCount=len(database["Enemies"]), ResourceCount=asset_count,
                    DatabaseSha256=hashlib.sha256((OUTPUT / "database.json").read_bytes()).hexdigest(),
                    LogicalCanvas=[1920, 1080], Origin="左上角，Y向下", CharacterSize=[SIZE, SIZE],
                    CharacterPivotPixels=[360, 660], CharacterPivotUnity=[360 / SIZE, 1 - 660 / SIZE],
                    PreservedRefined=sorted(REFINED), PreservedDedicated=sorted(LEGACY), NewDedicated=sorted(LOOKS),
                    Effects=[dict(Id=slug, Resource=f"Game/Effects/{slug}_atlas", FrameSize=[256, 256], FrameCount=8, **spec) for slug, spec in EFFECTS.items()],
                    Levels=[dict(Id=l["Id"], Name=l["Name"], Slots=len(l["Slots"]), Routes=len(l["Routes"]), Waves=len(l["Waves"]), Bosses=l["BossHeroIds"], Castle=list(l["Routes"][0]["Points"][-1].values())) for l in database["Levels"]],
                    Notes=["所有地图不烘焙阵位、文字或作战人物", "虎牢沿用已认可全部34阵位，其中(810,560)在原稿即距南路中心13像素，其余新增图阵位距路中心至少45像素", "6位v4己方全身图与4组特效图集逐字节拷贝；吕布按同一v4绘图源以己方浅色台座重新导出", "Characters下25位均为己方浅色台座；实际8位Boss单独导出到Bosses并使用朱砂台座", "只重生成本脚本拥有的输出，不清理Fonts或其他目录", "现有Windows字体仅用于检视图栅格渲染，不复制字体文件", "波间StartDelay以清空上一波为起点，单局时长需用实际Domain及手机试玩复核"],
                    LegacyFingerprints=before, Assets=assets,
                    ReviewFiles=[dict(Path=str(path.relative_to(ROOT)).replace("\\", "/"),
                                      Sha256=hashlib.sha256(path.read_bytes()).hexdigest())
                                 for path in (ROSTER_REVIEW, MAP_REVIEW)])
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"生成并验证：25角色 / 5地图 / {len(database['Enemies'])}敌人配置 / {asset_count}个运行时图像资源")
    print("阵位：" + "、".join(f'{l["Name"]}{len(l["Slots"])}' for l in database["Levels"]))
    print(f"原视觉稿及素材 {len(before)} 个文件 SHA-256 未变；输出 {OUTPUT}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true", help="只验证现有内容，不写文件")
    parser.add_argument("--data-only", action="store_true", help="仅重生成数值配置；路线、阵位或地图资源变化时拒绝运行")
    parser.add_argument("--huangjin-sensation", action="store_true", help="仅生成已确认的黄巾体验配置与本关地形避让，保护其他素材")
    options = parser.parse_args()
    if options.verify_only:
        database = json.loads((OUTPUT / "database.json").read_text(encoding="utf-8"))
        verify_database(database)
        count = verify_resources(database)
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        if manifest["LegacyFingerprints"] != legacy_fingerprint():
            raise RuntimeError("历史美术基准指纹与内容清单不同")
        if manifest["DatabaseSha256"] != hashlib.sha256((OUTPUT / "database.json").read_bytes()).hexdigest():
            raise RuntimeError("配置文件与清单指纹不同")
        for entry in manifest["Assets"] + manifest["ReviewFiles"]:
            if hashlib.sha256((ROOT / entry["Path"]).read_bytes()).hexdigest() != entry["Sha256"]:
                raise RuntimeError("输出素材与清单指纹不同：" + entry["Path"])
        print(f"验证通过：25角色、5关、{sum(len(l['Waves']) for l in database['Levels'])}波、{sum(len(l['Slots']) for l in database['Levels'])}阵位、{count}个资源；历史美术基准未变")
    elif options.huangjin_sensation:
        previous = json.loads((OUTPUT / "database.json").read_text(encoding="utf-8"))
        database = make_database()
        for key in ("Heroes", "Enemies", "SchemaVersion"):
            if database[key] != previous[key]:
                raise RuntimeError("专项导出拒绝连带修改：" + key)
        for current, old in zip(database["Levels"], previous["Levels"]):
            if current["Id"] != "huangjin" and current != old:
                raise RuntimeError("专项导出拒绝修改其他关：" + current["Id"])
        verify_database(database)
        before = legacy_fingerprint()
        level = next(row for row in database["Levels"] if row["Id"] == "huangjin")
        target = OUTPUT / "database.json"
        target.write_text(json.dumps(database, ensure_ascii=False, indent=2), encoding="utf-8")
        map_path = OUTPUT / "Maps/huangjin.png"
        save_image(map_art(level), map_path)
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        manifest["DatabaseSha256"] = hashlib.sha256(target.read_bytes()).hexdigest()
        for entry in manifest["Levels"]:
            if entry["Id"] == "huangjin":
                entry["Slots"] = len(level["Slots"])
        for entry in manifest["Assets"]:
            if entry["Path"] == map_path.relative_to(ROOT).as_posix():
                entry["Sha256"] = hashlib.sha256(map_path.read_bytes()).hexdigest()
        if before != legacy_fingerprint():
            raise RuntimeError("专项导出影响了历史美术")
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        print("仅更新黄巾配置/地形草丛避让与清单；其他关卡、人物及旧检视图保留。")
    elif options.data_only:
        previous = json.loads((OUTPUT / "database.json").read_text(encoding="utf-8"))
        database = make_database()
        verify_database(database)
        for current, old in zip(database["Levels"], previous["Levels"]):
            for key in ("Id", "Routes", "Slots", "MapResource"):
                if current[key] != old[key]:
                    raise RuntimeError("地图几何已变化，请运行完整生成：" + current["Id"])
        count = verify_resources(database)
        target = OUTPUT / "database.json"
        target.write_text(json.dumps(database, ensure_ascii=False, indent=2), encoding="utf-8")
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        manifest["DatabaseSha256"] = hashlib.sha256(target.read_bytes()).hexdigest()
        for entry, current in zip(manifest["Levels"], database["Levels"]):
            entry["Waves"] = len(current["Waves"])
            entry["Bosses"] = current["BossHeroIds"]
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"仅更新数值配置；{count}个图像资源验证通过，地图几何未变")
    else:
        export()


if __name__ == "__main__":
    main()
