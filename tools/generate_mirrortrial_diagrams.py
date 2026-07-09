from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Docs" / "generated_images"
OUT.mkdir(parents=True, exist_ok=True)

FONT_REG = "C:/Windows/Fonts/msyh.ttc"
FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, size)


def canvas(width: int, height: int) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGB", (width, height), "#f7f8fb")
    return image, ImageDraw.Draw(image)


def rounded(draw: ImageDraw.ImageDraw, box, fill, outline, width=3, radius=22):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def text(draw: ImageDraw.ImageDraw, xy, value, size=24, fill="#172033", bold=False, anchor=None):
    draw.text(xy, value, font=font(size, bold), fill=fill, anchor=anchor)


def centered(draw: ImageDraw.ImageDraw, box, lines, title_size=25, body_size=18, title_color="#172033"):
    x1, y1, x2, y2 = box
    total = title_size + 8 + max(0, len(lines) - 1) * (body_size + 8)
    y = y1 + ((y2 - y1) - total) / 2
    text(draw, ((x1 + x2) / 2, y), lines[0], title_size, title_color, True, "ma")
    y += title_size + 18
    for line in lines[1:]:
        text(draw, ((x1 + x2) / 2, y), line, body_size, "#526071", False, "ma")
        y += body_size + 10


def arrow(draw: ImageDraw.ImageDraw, start, end, color="#5c6f89", width=4):
    x1, y1 = start
    x2, y2 = end
    draw.line((x1, y1, x2, y2), fill=color, width=width)
    angle = math.atan2(y2 - y1, x2 - x1)
    length = 16
    spread = 0.45
    p1 = (x2 - length * math.cos(angle - spread), y2 - length * math.sin(angle - spread))
    p2 = (x2 - length * math.cos(angle + spread), y2 - length * math.sin(angle + spread))
    draw.polygon([(x2, y2), p1, p2], fill=color)


def curved_arrow(draw: ImageDraw.ImageDraw, points, color="#5c6f89", width=4):
    draw.line(points, fill=color, width=width, joint="curve")
    arrow(draw, points[-2], points[-1], color=color, width=width)


def save(image: Image.Image, name: str):
    path = OUT / name
    image.save(path)
    print(path)


def core_loop():
    image, draw = canvas(1280, 520)
    text(draw, (60, 58), "核心体验循环：镜子阻断 -> 镜像试炼 -> 夺回能力", 34, bold=True)

    cards = [
        ((70, 150, 260, 290), "#ffffff", "#ccd5e2", ["现实层推进", "横版前进", "遭遇敌人"]),
        ((360, 150, 550, 290), "#edf8ff", "#64a6d8", ["镜子门", "阻挡前路", "进入镜中"]),
        ((650, 150, 840, 290), "#fff2f2", "#d96969", ["镜像 Boss", "展示能力", "逼迫学习"]),
        ((940, 150, 1130, 290), "#f2fff7", "#61b77c", ["夺回能力", "能力解锁", "战斗方式变化"]),
    ]
    for box, fill, stroke, lines in cards:
        rounded(draw, box, fill, stroke)
        centered(draw, box, lines)

    arrow(draw, (270, 220), (345, 220))
    arrow(draw, (560, 220), (635, 220))
    arrow(draw, (850, 220), (925, 220))
    curved_arrow(draw, [(1035, 305), (1035, 390), (165, 390), (165, 310)])
    text(draw, (640, 415), "带着新能力回到现实层，解决之前更难处理的威胁", 18, "#6b7788", anchor="ma")
    rounded(draw, (480, 315, 800, 410), "#fff8e8", "#d3a332")
    centered(draw, (480, 315, 800, 410), ["最终综合考验", "用夺回的能力", "对抗完整镜像自我"], 24, 18)
    save(image, "mirrortrial_core_loop_cn_fixed.png")


def world_structure():
    image, draw = canvas(1280, 600)
    text(draw, (60, 58), "关卡结构：现实层与镜像层交替推进", 34, bold=True)

    rounded(draw, (70, 130, 1210, 290), "#ffffff", "#d6deea", width=2)
    text(draw, (100, 176), "现实层", 25, bold=True)
    text(draw, (100, 214), "承担推进、教学、敌人组合和能力验证", 18, "#566477")

    rounded(draw, (70, 355, 1210, 515), "#ffffff", "#d6deea", width=2)
    text(draw, (100, 401), "镜像层", 25, bold=True)
    text(draw, (100, 439), "承担 Boss 战、能力夺回和主题化演出", 18, "#566477")

    top = [
        ((270, 160, 440, 248), "#edf8ff", "#66a6d9", ["镜子门", "第一次阻断"]),
        ((555, 160, 725, 248), "#edf8ff", "#66a6d9", ["镜子门", "第二次阻断"]),
        ((840, 160, 1010, 248), "#edf8ff", "#66a6d9", ["尾声", "进入最终试炼"]),
    ]
    bottom = [
        ((270, 385, 440, 473), "#fff0f0", "#d76161", ["刃镜", "镜刃主题"]),
        ((555, 385, 725, 473), "#f3f0ff", "#7a6fd8", ["回声", "冲刺与残影"]),
        ((840, 385, 1060, 473), "#fff8e6", "#cda13a", ["镜域之主", "综合能力考验"]),
    ]
    for item in top + bottom:
        rounded(draw, item[0], item[1], item[2], width=3, radius=16)
        centered(draw, item[0], item[3], 24, 18)

    arrow(draw, (170, 205), (255, 205))
    arrow(draw, (445, 205), (540, 205))
    arrow(draw, (730, 205), (825, 205))
    arrow(draw, (440, 430), (540, 430))
    arrow(draw, (725, 430), (825, 430))
    for x in (355, 640, 925):
        draw.line((x, 250, x, 372), fill="#8a97a8", width=3)
        arrow(draw, (x, 360), (x, 372), color="#8a97a8", width=3)
    save(image, "mirrortrial_world_structure_cn_fixed.png")


def ability_reuse():
    image, draw = canvas(1280, 560)
    text(draw, (60, 58), "能力复用思路：同一个能力，不同使用者版本", 34, bold=True)

    rounded(draw, (455, 185, 825, 345), "#ffffff", "#95a3b8")
    centered(draw, (455, 185, 825, 345), ["AbilityConfig", "描述一个能力的共同逻辑", "例如：镜刃 / 回声冲刺"], 25, 18)

    items = [
        ((90, 155, 360, 285), "#fff1f1", "#d46a6a", ["Boss 版", "前摇清晰，可读可躲", "用于教学和压迫"]),
        ((920, 155, 1190, 285), "#eef8ff", "#5d9fd2", ["玩家版", "响应更快，反馈更强", "表达夺回后的成长"]),
        ((90, 355, 360, 475), "#f2fff5", "#65b97a", ["敌人版", "简单、稳定、低成本"]),
        ((920, 355, 1190, 475), "#fff8e7", "#cda146", ["反馈配置", "动画、特效、音效、顿帧"]),
    ]
    for box, fill, stroke, lines in items:
        rounded(draw, box, fill, stroke)
        centered(draw, box, lines, 24, 18)

    arrow(draw, (455, 245), (375, 245))
    arrow(draw, (825, 245), (905, 245))
    arrow(draw, (455, 310), (375, 405))
    arrow(draw, (825, 310), (905, 405))
    save(image, "mirrortrial_ability_reuse_cn_fixed.png")


def game_flow():
    image, draw = canvas(1280, 720)
    text(draw, (60, 58), "《镜中试炼》游戏流程图", 34, bold=True)
    text(draw, (60, 95), "现实层推进与镜像 Boss 战交替，形成学习、夺回、验证的闭环", 19, "#566477")

    steps = [
        ("现实层推进", "移动 / 跳跃 / 基础战斗"),
        ("清理敌人", "近战压力 + 远程干扰"),
        ("镜子门阻挡", "前路被封，提示进入镜中"),
        ("镜像层试炼", "挑战能力主题 Boss"),
        ("击败镜像 Boss", "观察招式，抓住反击窗口"),
        ("夺回能力", "镜刃 / 回声冲刺"),
        ("返回现实层", "新能力解决新威胁"),
        ("镜子碎裂", "打开前路，进入下一段"),
        ("最终综合考验", "用夺回能力对抗镜域之主"),
    ]
    positions = [
        (70, 150), (335, 150), (600, 150),
        (865, 150), (865, 330), (600, 330),
        (335, 330), (70, 330), (420, 535),
    ]
    size = (215, 105)
    colors = ["#ffffff", "#ffffff", "#edf8ff", "#fff2f2", "#fff2f2", "#f2fff7", "#ffffff", "#edf8ff", "#fff8e8"]
    strokes = ["#ccd5e2", "#ccd5e2", "#64a6d8", "#d96969", "#d96969", "#61b77c", "#ccd5e2", "#64a6d8", "#d3a332"]
    boxes = []
    for (x, y), (title, body), fill, stroke in zip(positions, steps, colors, strokes):
        box = (x, y, x + size[0], y + size[1])
        boxes.append(box)
        rounded(draw, box, fill, stroke, radius=18)
        centered(draw, box, [title, body], 23, 16)

    connectors = [
        ((285, 202), (320, 202)), ((550, 202), (585, 202)), ((815, 202), (850, 202)),
        ((972, 255), (972, 315)), ((865, 382), (830, 382)), ((600, 382), (565, 382)),
        ((335, 382), (300, 382)), ((177, 435), (495, 535)), ((707, 435), (707, 535)),
    ]
    for start, end in connectors:
        arrow(draw, start, end)
    save(image, "mirrortrial_game_flow_cn_fixed.png")


if __name__ == "__main__":
    core_loop()
    world_structure()
    ability_reuse()
    game_flow()
