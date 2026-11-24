"""青绿沙盘的透明反馈序列；只生成素材，不模拟游戏逻辑。"""

from __future__ import annotations

import math
from PIL import Image, ImageDraw, ImageFilter

EFFECTS = {
    "recruit": {"label": "招募入阵", "duration_ms": 480, "loop": False, "pivot": [128, 200]},
    "slash": {"label": "青龙斩击", "duration_ms": 240, "loop": False, "pivot": [128, 128]},
    "fire": {"label": "火攻余焰", "duration_ms": 640, "loop": True, "pivot": [128, 200]},
    "boss_warning": {"label": "敌将入场", "duration_ms": 800, "loop": False, "pivot": [128, 200]},
}


def _bezier(a, b, c, d, steps=32):
    return [tuple((1-t)**3*a[j] + 3*(1-t)**2*t*b[j] + 3*(1-t)*t*t*c[j] + t**3*d[j]
                  for j in (0, 1)) for t in (i/steps for i in range(steps+1))]


def make_effect(kind: str, frame: int, size: int = 256) -> Image.Image:
    """返回八帧之一；统一256设计坐标，四周保持透明，默认八帧不重复。"""
    if kind not in EFFECTS:
        raise ValueError(f"未知反馈素材：{kind}")
    if isinstance(frame, bool) or not isinstance(frame, int) or not 0 <= frame < 8:
        raise ValueError("frame 必须是 0～7 的整数")
    if isinstance(size, bool) or not isinstance(size, int) or size < 1:
        raise ValueError("size 必须是正整数")
    factor = size * 3 / 256
    pixels = size * 3
    ink = Image.new("RGBA", (pixels, pixels))
    glow = Image.new("RGBA", ink.size)
    draw, haze = ImageDraw.Draw(ink), ImageDraw.Draw(glow)
    t = frame / 7

    def box(rect):
        return tuple(round(v*factor) for v in rect)

    def line(points, color, width=1, target=draw):
        target.line([(round(x*factor), round(y*factor)) for x, y in points],
                    fill=color, width=max(1, round(width*factor)), joint="curve")

    def shape(points, color, target=draw):
        target.polygon([(round(x*factor), round(y*factor)) for x, y in points], fill=color)

    def oval(rect, color, outline=None, width=1, target=draw):
        target.ellipse(box(rect), fill=color, outline=outline, width=max(1, round(width*factor)))

    if kind == "recruit":
        ease = 1 - (1-t)**2
        radius = 24 + 75*ease
        alpha = round(230*(1-.88*t))
        oval((128-radius, 200-radius*.27, 128+radius, 200+radius*.27),
             (156, 201, 159, round(alpha*.17)), (200, 223, 169, alpha), 2.2)
        oval((128-radius+7, 202-radius*.27, 121+radius, 198+radius*.27),
             None, (113, 160, 122, round(alpha*.65)), 1)
        oval((53, 169, 203, 225), (157, 202, 133, round(alpha*.2)), target=haze)
        for index in range(7):
            angle = index*math.tau/7 + t*.9
            x = 128+math.cos(angle)*(radius*.79)
            y = 185+math.sin(angle)*radius*.22-62*math.sin(math.pi*t)*(.5+index%3*.15)
            height = 5+4*math.sin(math.pi*t)
            shape([(x, y-height), (x+2, y), (x, y+height), (x-2, y)], (239, 216, 153, alpha))
            line([(x, y+height+5), (x, y+height+15)], (180, 210, 157, round(alpha*.4)))
    elif kind == "slash":
        pulse = max(.08, math.sin(math.pi*(t*.92+.04)))
        alpha = round(250*pulse)
        shift = (t-.5)*13
        a = (42+shift, 197-20*t)
        b = (85, 222-42*t)
        c = (216, 145-30*t)
        d = (218-shift, 49+30*t)
        outer = _bezier(a, b, c, d)
        inner = _bezier(d, (192, 144-28*t), (100, 208-35*t), a)
        shape(outer+inner, (205, 232, 181, round(alpha*.9)))
        line(outer, (235, 245, 209, alpha), 2.6)
        line(outer, (165, 205, 139, round(alpha*.58)), 11, target=haze)
        trail = _bezier((53, 202), (115, 230-38*t), (223, 141), (228, 86))
        line(trail, (204, 185, 125, round(alpha*.7)), 1.1)
        for x, y in ((103, 208), (193, 149), (219, 103)):
            x += shift
            shape([(x, y-5), (x+1.7, y), (x, y+5), (x-1.7, y)], (244, 231, 180, alpha))
    elif kind == "fire":
        phase = frame/8*math.tau
        oval((49, 178, 208, 224), (214, 137, 65, 48), target=haze)
        oval((70, 192, 190, 218), (127, 95, 57, 32))
        for index, (x, base_y, height) in enumerate(((85, 200, 69), (130, 202, 111), (171, 200, 78))):
            wave = math.sin(phase+index*1.9)
            top = base_y-height-wave*9
            width = 21 if index != 1 else 28
            outer = (_bezier((x-width, base_y), (x-width-6, base_y-23), (x-7, top+38), (x+wave*7, top))
                     + _bezier((x+wave*7, top), (x+18, top+43), (x+width+12, base_y-22), (x+width, base_y))
                     + _bezier((x+width, base_y), (x+14, base_y+15), (x-13, base_y+14), (x-width, base_y)))
            shape(outer, (192, 103, 52, 213))
            inset = (_bezier((x-width*.6, base_y), (x-15, base_y-19), (x+8, top+43), (x+4, top+27))
                     + _bezier((x+4, top+27), (x+17, top+65), (x+20, base_y-8), (x+10, base_y+2)))
            shape(inset, (236, 174, 86, 235))
            core = (_bezier((x-7, base_y+3), (x-11, base_y-10), (x+3, base_y-30), (x+1, base_y-39))
                    + _bezier((x+1, base_y-39), (x+13, base_y-14), (x+13, base_y+3), (x-7, base_y+3)))
            shape(core, (255, 223, 143, 234))
        for index in range(5):
            step = (frame/8+index*.21)%1
            x = 85+index*21+math.sin(phase+index)*8
            y = 169-step*104
            oval((x-1.1, y-2.7, x+1.1, y+2.7), (242, 192, 104, round(160*(1-step))))
    else:
        radius = 65+29*t
        alpha = round(220*(1-.85*t))
        oval((128-radius, 200-radius*.29, 128+radius, 200+radius*.29),
             (171, 84, 63, round(alpha*.07)), (181, 91, 67, alpha), 2)
        oval((132-radius, 204-radius*.29, 124+radius, 196+radius*.29),
             None, (219, 177, 109, round(alpha*.8)), 1)
        oval((44, 171, 212, 228), (197, 90, 57, round(alpha*.17)), target=haze)
        for angle in (25, 155, 205, 335):
            rad = math.radians(angle)
            x, y = 128+math.cos(rad)*radius, 200+math.sin(rad)*radius*.29
            line([(x-4, y-4), (x, y), (x+4, y-4)], (230, 177, 111, alpha), 1.8)
    glow = glow.filter(ImageFilter.GaussianBlur(4*factor))
    glow.alpha_composite(ink)
    return glow.resize((size, size), Image.Resampling.LANCZOS)
