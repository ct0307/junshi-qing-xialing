"""生成三国沙盘战斗页的静态视觉概念图。"""

from pathlib import Path
import math
import random
from PIL import Image, ImageDraw, ImageFont, ImageFilter


ROOT = Path(__file__).resolve().parent
SCALE = 2
WIDTH, HEIGHT = 1600, 900
COLORS = {
    "ink": "#214C42", "jade": "#4E8B70", "grass": "#DCE6CB",
    "road": "#DDC69B", "paper": "#F6F4EC", "red": "#B95446",
    "muted": "#7D8B7D", "gold": "#B68B45", "line": "#D9DFD0",
}
canvas = Image.new("RGBA", (WIDTH * SCALE, HEIGHT * SCALE), COLORS["paper"])
draw = ImageDraw.Draw(canvas)
font_paths = {
    "body": "C:/Windows/Fonts/msyh.ttc",
    "bold": "C:/Windows/Fonts/msyhbd.ttc",
    "title": "C:/Windows/Fonts/simkai.ttf",
    "number": "C:/Windows/Fonts/segoeuib.ttf",
}
fonts = {}


def font(size, role="body"):
    key = (size, role)
    if key not in fonts:
        fonts[key] = ImageFont.truetype(font_paths[role], round(size * SCALE))
    return fonts[key]


def box(values):
    return tuple(round(value * SCALE) for value in values)


def rounded(rect, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box(rect), round(radius * SCALE), fill=fill,
                           outline=outline, width=round(width * SCALE))


def circle(x, y, radius, fill, outline=None, width=1):
    draw.ellipse(box((x-radius, y-radius, x+radius, y+radius)), fill=fill,
                 outline=outline, width=round(width * SCALE))


def line(points, fill, width=1):
    draw.line([(round(x*SCALE), round(y*SCALE)) for x, y in points],
              fill=fill, width=round(width*SCALE), joint="curve")


def polygon(points, fill, outline=None):
    draw.polygon([(round(x*SCALE), round(y*SCALE)) for x, y in points],
                 fill=fill, outline=outline)


def text(x, y, value, size=22, fill=None, role="body", anchor="lt"):
    draw.text((round(x*SCALE), round(y*SCALE)), value, font=font(size, role),
              fill=fill or COLORS["ink"], anchor=anchor)


def shadow(rect, radius=24, offset=6, opacity=23, blur=12):
    global draw
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    painter = ImageDraw.Draw(layer)
    x1, y1, x2, y2 = rect
    painter.rounded_rectangle(box((x1, y1+offset, x2, y2+offset)),
                              radius=round(radius*SCALE), fill=(30, 65, 42, opacity))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur*SCALE)))
    draw = ImageDraw.Draw(canvas)


def smooth_route(points, radius=38):
    result = [points[0]]
    for index in range(1, len(points)-1):
        previous, corner, following = points[index-1:index+2]
        length_a = math.dist(previous, corner)
        length_b = math.dist(corner, following)
        length = min(radius, length_a/3, length_b/3)
        before = tuple(corner[k] + (previous[k]-corner[k])*length/length_a for k in (0, 1))
        after = tuple(corner[k] + (following[k]-corner[k])*length/length_b for k in (0, 1))
        result.append(before)
        for step in range(1, 13):
            t = step/12
            result.append(tuple((1-t)**2*before[k]+2*(1-t)*t*corner[k]+t*t*after[k]
                                for k in (0, 1)))
    result.append(points[-1])
    return result


def point_distance(point, route):
    best = 10000.0
    for start, end in zip(route, route[1:]):
        dx, dy = end[0]-start[0], end[1]-start[1]
        fraction = ((point[0]-start[0])*dx+(point[1]-start[1])*dy)/(dx*dx+dy*dy)
        fraction = max(0, min(1, fraction))
        closest = start[0]+fraction*dx, start[1]+fraction*dy
        best = min(best, math.dist(point, closest))
    return best


def bush(x, y, scale=1):
    circle(x, y+7*scale, 22*scale, "#BDD1AC")
    circle(x-12*scale, y, 17*scale, "#A9C49A")
    circle(x+13*scale, y-4*scale, 21*scale, "#B2CAA2")
    circle(x+2*scale, y-11*scale, 19*scale, "#BCD2AA")
    line([(x-6*scale, y-16*scale), (x+6*scale, y-19*scale)], "#C9DAB8", 2)


def figurine(x, y, name, size=1, enemy=False):
    """用统一的几何语言绘制概念棋子，兵器用于区分轮廓。"""
    coats = {"关羽": "#477B5A", "赵云": "#D5DFD9", "张飞": "#354C50",
             "黄忠": "#BA9559", "周瑜": "#9B5E54", "诸葛亮": "#D6D7C0",
             "曹操": "#4D6A76", "貂蝉": "#A8758B", "庞统": "#90724F",
             "华佗": "#84A58D", "张辽": "#81575A"}
    coat = COLORS["red"] if enemy else coats.get(name, COLORS["jade"])

    def pos(px, py):
        return x+(px-50)*size, y+(py-76)*size

    def local_polygon(points, color):
        polygon([pos(px, py) for px, py in points], color)

    def local_line(points, color, width=2):
        line([pos(px, py) for px, py in points], color, width*size)

    def local_circle(px, py, radius, color, outline=None):
        cx, cy = pos(px, py)
        circle(cx, cy, radius*size, color, outline)

    local_circle(50, 89, 23, "#C0C9AE")
    local_circle(50, 85, 24, "#F3EBD6", "#A5B397")
    local_polygon([(30, 49), (66, 49), (76, 83), (62, 91), (35, 89), (24, 80)], coat)
    highlight = tuple(round(int(coat[index:index+2], 16)*.82+245*.18) for index in (1, 3, 5))
    local_polygon([(33, 52), (39, 52), (38, 82), (30, 81)], highlight)
    local_line([(30, 77), (67, 77)], "#D0B67D", 5)
    local_circle(49, 34, 18, "#EAC6A2", "#806C54")
    local_polygon([(30, 30), (32, 16), (64, 16), (68, 30), (61, 26), (38, 26)],
                  "#33483E" if name != "黄忠" else "#947947")
    local_circle(43, 35, 1.5, "#2A3A33")
    local_circle(55, 35, 1.5, "#2A3A33")
    local_line([(43, 45), (52, 47), (58, 44)], "#9C6B53", 1.2)
    if name in ("关羽", "张飞", "黄忠", "曹操", "庞统", "华佗"):
        beard = "#E4E4CE" if name in ("黄忠", "华佗") else "#344138"
        local_polygon([(36, 45), (59, 45), (57, 65 if name == "关羽" else 56),
                       (48, 78 if name == "关羽" else 64), (38, 58)], beard)
    if name == "关羽":
        local_polygon([(32, 19), (38, 8), (60, 10), (66, 23)], "#477B5A")
        local_line([(81, 17), (78, 92)], "#746442", 4)
        local_polygon([(82, 11), (99, 6), (100, 25), (88, 35), (81, 30)], "#8CAFA0")
        local_line([(84, 12), (96, 10)], "#D4E4D5", 2)
    elif name in ("张飞", "赵云", "张辽"):
        local_line([(81, 18), (77, 95)], "#746442", 3)
        local_polygon([(81, 3), (73, 20), (86, 22)], "#9BAEA3")
        if name == "张飞":
            local_polygon([(33, 16), (39, 8), (48, 16), (58, 8), (65, 20)], "#354C50")
        else:
            local_polygon([(31, 25), (42, 9), (58, 9), (68, 25)], "#DCE4D9" if not enemy else "#6E5A58")
    elif name == "黄忠":
        arc = [pos(78+17*math.sin(t*math.pi), 19+62*t) for t in [i/24 for i in range(25)]]
        line(arc, "#8D6741", 3*size)
        local_line([(78, 19), (77, 81)], "#EADBC4", 1)
        local_line([(62, 47), (98, 47)], "#AA8D61", 2)
        local_polygon([(98, 47), (91, 43), (91, 51)], "#879F92")
    elif name == "诸葛亮":
        local_polygon([(33, 23), (38, 7), (58, 7), (65, 24)], "#526B67")
        for angle in (-45, -22, 0, 22, 45):
            rad = math.radians(angle)
            tip = (80+math.sin(rad)*20, 60-math.cos(rad)*27)
            local_line([(80, 69), tip], "#F9F7EC", 7)
        local_line([(80, 69), (76, 84)], "#9B8964", 3)
    elif name == "周瑜":
        local_polygon([(35, 21), (39, 9), (59, 9), (64, 24)], "#6E4840")
        local_line([(78, 85), (88, 30)], "#B3C7BB", 4)
        local_line([(75, 69), (87, 71)], "#B68B45", 3)
    elif name == "貂蝉":
        local_circle(31, 23, 8, "#3B4142")
        local_circle(65, 23, 8, "#3B4142")
        local_circle(32, 20, 4, "#D8A7AD")
        local_polygon([(70, 52), (93, 40), (98, 57), (75, 66)], "#E4BCC1")
    elif name == "华佗":
        local_circle(77, 72, 13, "#D1B582")
        local_line([(77, 65), (77, 79)], "#7F9E7B", 3)
        local_line([(70, 72), (84, 72)], "#7F9E7B", 3)
    else:
        local_polygon([(34, 20), (38, 8), (61, 8), (65, 23)], "#374F50")
        local_polygon([(73, 49), (91, 44), (92, 67), (74, 72)], "#D9CCA7")


# 主界面以少量清晰区域承载信息。
rounded((0, 0, WIDTH, HEIGHT), 0, COLORS["paper"])
rounded((34, 28, 77, 74), 10, COLORS["red"])
text(55, 51, "演", 29, "#FFF7E5", "title", "mm")
text(94, 25, "三国小沙盘", 34, role="title")
text(96, 66, "第二章  ·  虎牢合战", 18, COLORS["muted"])
for x in (481, 720, 958, 1210):
    line([(x, 28), (x, 78)], COLORS["line"], 1)
circle(516, 48, 12, "#D8B46C")
circle(516, 48, 5, COLORS["paper"])
text(541, 25, "军饷", 17, COLORS["muted"])
text(540, 48, "320", 31, role="number")
rounded((748, 38, 771, 62), 5, COLORS["jade"])
text(789, 25, "城池耐久", 17, COLORS["muted"])
text(789, 48, "18 / 20", 29, role="number")
text(990, 25, "当前波次", 17, COLORS["muted"])
text(990, 48, "6 / 12", 29, role="number")
text(1241, 25, "下一敌将", 17, COLORS["muted"])
text(1241, 53, "吕布", 24, COLORS["red"], "bold")
rounded((1466, 27, 1566, 80), 17, "#E7EBE1")
line([(1507, 43), (1507, 64)], COLORS["ink"], 4)
line([(1523, 43), (1523, 64)], COLORS["ink"], 4)

board_rect = (30, 112, 1570, 714)
shadow(board_rect, 25, 5, 19, 9)
rounded(board_rect, 25, COLORS["grass"], "#CCD8BD", 2)
rounded((42, 124, 1558, 702), 20, None, "#EFF2E3", 2)

# 地形边缘保留装饰，中央留给道路和棋子。
polygon([(55, 185), (95, 141), (158, 163), (210, 136), (263, 177), (275, 204), (55, 204)], "#C0D3AF")
polygon([(760, 161), (821, 131), (886, 161), (929, 145), (992, 180), (760, 180)], "#CCDBB8")
polygon([(1300, 153), (1372, 130), (1444, 158), (1516, 141), (1548, 196), (1324, 193)], "#BFD0AB")
for bx, by, bs in [(73, 565, .9), (200, 698, .5), (775, 691, .7),
                   (1364, 177, .65), (1536, 325, .65), (1120, 685, .7)]:
    bush(bx, by, bs)

routes = [
    [(75, 270), (350, 270), (350, 438), (620, 438)],
    [(75, 638), (500, 638), (500, 438), (620, 438)],
    [(620, 438), (886, 438), (886, 267), (1216, 267), (1216, 541), (1450, 541)],
]
for route in routes:
    path = smooth_route(route)
    line([(x, y+4) for x, y in path], "#BCC9A5", 72)
    line(path, "#CAB487", 66)
    line(path, COLORS["road"], 60)
    line([(x, y-2) for x, y in path], "#E5D2AE", 40)

# 少量路面细节让材质统一，保持低对比。
rng = random.Random(17)
for route in routes:
    for a, b in zip(route, route[1:]):
        steps = max(1, int(math.dist(a, b)/24))
        for index in range(steps):
            t = (index+.3)/steps
            px = a[0]+(b[0]-a[0])*t+rng.uniform(-12, 12)
            py = a[1]+(b[1]-a[1])*t+rng.uniform(-12, 12)
            circle(px, py, rng.choice((1, 1.5, 2)), "#D0BA93")

# 以 34 个固定位置表达“可选位置远多于上阵人数”。
deployed = [(719, 350, "关羽"), (1030, 385, "周瑜"),
            (424, 525, "张飞"), (1097, 628, "黄忠")]
occupied = [(x, y) for x, y, _ in deployed]
candidates = []
for y in (207, 292, 404, 516, 628):
    for x in range(127, 1510, 111):
        point = (x, y)
        if min(point_distance(point, route) for route in routes) < 59:
            continue
        if x > 1370 and y > 395:
            continue
        if any(math.dist(point, existing) < 86 for existing in occupied):
            continue
        candidates.append(point)
empty = []
while candidates and len(empty) < 30:
    chosen = max(candidates, key=lambda p: min(math.dist(p, q) for q in occupied+empty))
    empty.append(chosen)
    candidates.remove(chosen)
assert len(empty)+len(deployed) == 34, "概念图应包含 34 个阵位"
for x, y in empty:
    circle(x, y+3, 22, "#C8D5B7")
    circle(x, y, 21, "#E6EBD8", "#BBCBAB", 1.2)
    line([(x-5, y), (x+5, y)], "#B3C5A7", 1.3)
    line([(x, y-5), (x, y+5)], "#B3C5A7", 1.3)

# 选中范围只在一位武将周围出现。
range_layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
range_draw = ImageDraw.Draw(range_layer)
range_draw.ellipse(box((604, 235, 834, 465)), fill=(78, 139, 112, 21),
                   outline=(78, 139, 112, 115), width=2*SCALE)
canvas.alpha_composite(range_layer)
draw = ImageDraw.Draw(canvas)

# 入口旗帜与敌军。
for fx, fy in ((76, 229), (76, 597)):
    line([(fx, fy), (fx, fy+34)], "#8D7955", 3)
    polygon([(fx+1, fy), (fx+27, fy+3), (fx+20, fy+17), (fx+1, fy+15)], COLORS["red"])
for ex, ey in [(145, 270), (203, 270), (266, 272), (350, 335),
               (192, 638), (254, 638), (315, 638), (500, 559)]:
    circle(ex, ey+9, 10, "#BDAE88")
    rounded((ex-8, ey-7, ex+8, ey+10), 5, "#AB6553")
    circle(ex, ey-14, 8, "#D1A881")
    polygon([(ex-9, ey-17), (ex, ey-25), (ex+9, ey-17)], "#6E6554")
    line([(ex+12, ey-18), (ex+12, ey+10)], "#8D7955", 2)

figurine(611, 430, "张辽", .9, True)
rounded((558, 443, 667, 469), 9, "#F5E4D9")
text(612, 456, "敌将·张辽", 15, COLORS["red"], "bold", "mm")

# 城池使用同一浅色材料，屋顶突出终点。
circle(1455, 574, 62, "#C6D0B3")
rounded((1401, 491, 1512, 582), 9, "#B8BDA6", "#899A84", 2)
for tx in (1399, 1492):
    rounded((tx, 477, tx+31, 565), 5, "#CFD0B7", "#9EA88E", 1)
    polygon([(tx-7, 485), (tx+16, 465), (tx+39, 485)], "#5D7A68")
rounded((1438, 537, 1478, 585), 18, "#756C55")
polygon([(1414, 518), (1455, 487), (1500, 518)], "#6D8971")
line([(1415, 518), (1498, 518)], "#A7B797", 3)
line([(1454, 491), (1454, 453)], "#7A795E", 3)
polygon([(1455, 453), (1486, 457), (1478, 474), (1455, 470)], COLORS["jade"])
text(1457, 606, "守住城池", 18, COLORS["ink"], "bold", "mm")

for x, y, name in deployed:
    if name == "关羽":
        circle(x, y+12, 33, "#9FC4A5")
        circle(x, y+10, 30, "#E6F0D9", COLORS["jade"], 2)
    figurine(x, y, name, 1.05)
    rounded((x-34, y+29, x+34, y+54), 8, "#F5F3E8")
    text(x, y+41, name, 16, COLORS["ink"], "bold", "mm")
    for index in range(3):
        circle(x-10+index*10, y+64, 3, COLORS["jade"] if index < 2 else "#BCC9B0")

# 底部人物牌保持统一节奏，选中与上阵状态都可见。
line([(34, 727), (1566, 727)], "#DDE2D3", 1)
roster = ["关羽", "赵云", "张飞", "黄忠", "周瑜", "诸葛亮", "曹操", "貂蝉", "庞统", "华佗"]
costs = [60, 65, 55, 70, 80, 75, 85, 65, 75, 60]
for index, name in enumerate(roster[:6]):
    x = 35+index*188
    selected = name == "关羽"
    rounded((x, 745, x+173, 874), 15,
            "#E6EEE0" if selected else "#FEFDF8",
            COLORS["jade"] if selected else "#E1E4D9", 2 if selected else 1)
    figurine(x+44, 825, name, .78)
    text(x+85, 772, name, 24, COLORS["ink"], "bold")
    if name in {item[2] for item in deployed}:
        text(x+86, 817, "已出阵", 19, COLORS["jade"])
    else:
        circle(x+93, 836, 6, "#C7A45C")
        text(x+107, 818, str(costs[index]), 25, COLORS["gold"], "number")

rounded((1166, 745, 1238, 874), 15, "#E9EDDF")
text(1202, 789, "›", 43, COLORS["ink"], "body", "mm")
text(1202, 843, "6 / 10", 16, COLORS["muted"], "number", "mm")

rounded((1253, 745, 1565, 874), 18, "#E7EBDD")
text(1273, 758, "关羽 · 贰阶", 23, role="bold")
text(1274, 791, "刀势更强，集中破阵", 16, COLORS["muted"])
rounded((1273, 822, 1545, 861), 12, COLORS["ink"])
text(1408, 841, "升级  120", 22, "#FFF9E9", "bold", "mm")

# Boss 生命条放在地图上缘，避免压住交战区域。
rounded((596, 131, 1010, 181), 17, "#F5F4E9", "#D4DDC7")
text(614, 145, "中场敌将 · 张辽", 17, COLORS["red"], "bold")
rounded((802, 150, 991, 160), 5, "#E5DCD0")
rounded((802, 150, 938, 160), 5, COLORS["red"])

output = ROOT / "battle-ui-concept.png"
canvas.convert("RGB").resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS).save(output)
print(f"概念图：{output}")
print(f"尺寸：{WIDTH}×{HEIGHT}；阵位：{len(empty)+len(deployed)}；己方名单：{len(roster)}")
