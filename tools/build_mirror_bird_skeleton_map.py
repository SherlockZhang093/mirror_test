from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/SkeletonMap_v3"

CELL = (192, 128)
SCALE = 4

JOINTS = {
    "sprite_root": (96, 66),
    "pelvis": (112, 75),
    "chest": (128, 72),
    "neck_base": (139, 66),
    "head": (146, 62),
    "beak": (153, 66),
    "near_wing_shoulder": (116, 64),
    "near_wing_elbow": (107, 47),
    "near_wing_wrist": (95, 28),
    "near_wing_hand": (77, 15),
    "far_wing_shoulder": (114, 65),
    "far_wing_elbow": (105, 50),
    "far_wing_wrist": (92, 32),
    "far_wing_hand": (73, 22),
    "tail_root": (101, 77),
    "tail_fork": (91, 80),
    "upper_tail_mid": (63, 85),
    "upper_tail_tip": (28, 87),
    "lower_tail_mid": (67, 93),
    "lower_tail_tip": (40, 97),
    "rider_seat": (117, 65),
}

CHAINS = {
    "core": ["pelvis", "chest", "neck_base", "head", "beak"],
    "near_wing": [
        "near_wing_shoulder",
        "near_wing_elbow",
        "near_wing_wrist",
        "near_wing_hand",
    ],
    "far_wing": [
        "far_wing_shoulder",
        "far_wing_elbow",
        "far_wing_wrist",
        "far_wing_hand",
    ],
    "tail_base": ["pelvis", "tail_root", "tail_fork"],
    "upper_tail": ["tail_fork", "upper_tail_mid", "upper_tail_tip"],
    "lower_tail": ["tail_fork", "lower_tail_mid", "lower_tail_tip"],
}

COLORS = {
    "core": (255, 96, 72, 255),
    "near_wing": (255, 214, 64, 255),
    "far_wing": (92, 155, 255, 255),
    "tail_base": (71, 236, 220, 255),
    "upper_tail": (71, 236, 220, 255),
    "lower_tail": (71, 236, 220, 255),
}

LABEL_ORDER = [
    "pelvis",
    "chest",
    "neck_base",
    "head",
    "beak",
    "near_wing_shoulder",
    "near_wing_elbow",
    "near_wing_wrist",
    "near_wing_hand",
    "far_wing_shoulder",
    "far_wing_elbow",
    "far_wing_wrist",
    "far_wing_hand",
    "tail_root",
    "tail_fork",
    "upper_tail_mid",
    "upper_tail_tip",
    "lower_tail_mid",
    "lower_tail_tip",
]


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = (
        Path("C:/Windows/Fonts/arial.ttf"),
        Path("C:/Windows/Fonts/segoeui.ttf"),
    )
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def checkerboard(size: tuple[int, int], tile: int = 32) -> Image.Image:
    image = Image.new("RGBA", size, (36, 39, 48, 255))
    draw = ImageDraw.Draw(image)
    alternate = (48, 52, 63, 255)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=alternate)
    return image


def scaled(point: tuple[int, int]) -> tuple[int, int]:
    return point[0] * SCALE, point[1] * SCALE


def dashed_line(
    draw: ImageDraw.ImageDraw,
    start: tuple[int, int],
    end: tuple[int, int],
    fill: tuple[int, int, int, int],
    width: int,
) -> None:
    x1, y1 = start
    x2, y2 = end
    steps = max(abs(x2 - x1), abs(y2 - y1))
    if steps == 0:
        return
    for step in range(0, steps, 12):
        end_step = min(step + 7, steps)
        a = step / steps
        b = end_step / steps
        draw.line(
            (
                round(x1 + (x2 - x1) * a),
                round(y1 + (y2 - y1) * a),
                round(x1 + (x2 - x1) * b),
                round(y1 + (y2 - y1) * b),
            ),
            fill=fill,
            width=width,
        )


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sprite = Image.open(SOURCE).convert("RGBA")
    if sprite.size != CELL:
        raise ValueError(f"Expected master pose {CELL}, found {sprite.size}")

    left_size = (CELL[0] * SCALE, CELL[1] * SCALE)
    legend_width = 700
    canvas = checkerboard((left_size[0] + legend_width, left_size[1]))
    canvas.alpha_composite(
        sprite.resize(left_size, Image.Resampling.NEAREST), (0, 0)
    )
    draw = ImageDraw.Draw(canvas)

    for chain_name, joint_names in CHAINS.items():
        color = COLORS[chain_name]
        for first, second in zip(joint_names, joint_names[1:]):
            start = scaled(JOINTS[first])
            end = scaled(JOINTS[second])
            if chain_name == "far_wing":
                dashed_line(draw, start, end, color, width=5)
            else:
                draw.line((start, end), fill=color, width=6)

    number_font = load_font(18)
    for index, name in enumerate(LABEL_ORDER):
        x, y = scaled(JOINTS[name])
        radius = 8
        draw.ellipse(
            (x - radius, y - radius, x + radius, y + radius),
            fill=(12, 14, 20, 255),
            outline=(255, 255, 255, 255),
            width=3,
        )
        draw.text((x + 9, y - 12), str(index), font=number_font, fill=(255, 255, 255, 255))

    title_font = load_font(27)
    body_font = load_font(19)
    small_font = load_font(16)
    legend_x = left_size[0] + 28
    draw.text(
        (legend_x, 18),
        "Mirror Bird Skeleton Map v3",
        font=title_font,
        fill=(255, 255, 255, 255),
    )
    draw.text(
        (legend_x, 56),
        "Cell 192x128 | PPU 32 | canvas anchor (96,66; not a bone)",
        font=small_font,
        fill=(196, 202, 218, 255),
    )

    column_x = (legend_x, legend_x + 340)
    for index, name in enumerate(LABEL_ORDER):
        column = 0 if index < 11 else 1
        row = index if column == 0 else index - 11
        x = column_x[column]
        y = 92 + row * 29
        coordinate = JOINTS[name]
        draw.text(
            (x, y),
            f"{index:02d}  {name}  {coordinate}",
            font=small_font,
            fill=(232, 235, 243, 255),
        )

    notes_y = 420
    notes = (
        "Solid red: anatomical core, beginning at pelvis",
        "Solid yellow: near wing; dashed blue: far wing",
        "Cyan: fixed tail base, then two articulated streamers",
        "Canvas/rider anchors are metadata only; never bones",
    )
    for index, note in enumerate(notes):
        draw.text(
            (legend_x, notes_y + index * 22),
            note,
            font=small_font,
            fill=(205, 210, 224, 255),
        )

    overlay_path = OUTPUT / "MirrorBird_SkeletonMap_v3_4x.png"
    canvas.save(overlay_path)

    specification = {
        "version": 3,
        "reference": str(SOURCE.relative_to(ROOT)).replace("\\", "/"),
        "cell": list(CELL),
        "ppu": 32,
        "sprite_pivot_top_left": [96, 66],
        "coordinates": "top-left origin, integer pixels",
        "joints": {name: list(point) for name, point in JOINTS.items()},
        "chains": CHAINS,
        "fixed_canvas_anchors": ["sprite_root"],
        "non_anatomical_anchors": ["sprite_root"],
        "fixed_attachment_anchors": ["rider_seat"],
        "invariant_core": [
            "pelvis",
            "chest",
            "near_wing_shoulder",
            "far_wing_shoulder",
            "tail_root",
        ],
        "articulated_core": ["neck_base", "head"],
        "action_chains": ["near_wing", "far_wing", "upper_tail", "lower_tail"],
        "rules": {
            "tail_motion_starts_after": "tail_fork",
            "wing_motion_starts_at": ["near_wing_shoulder", "far_wing_shoulder"],
            "rider_seat_usage": "metadata-only attachment reference; no bone connection",
            "scale_per_frame": 1,
            "subpixel_translation_allowed": False,
            "runtime_sprite_rotation_allowed": False,
        },
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_SkeletonMap_v3.json").write_text(
        json.dumps(specification, indent=2), encoding="utf-8"
    )
    print(json.dumps(specification, indent=2))


if __name__ == "__main__":
    main()
