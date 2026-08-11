from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw

import build_mirror_bird_idle_content as idle


ROOT = Path(__file__).resolve().parents[1]
PARTS = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePart_v1"
WINGS = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleWingPoses_v1"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePart_v10"

CELL = (192, 128)
FRAME_COUNT = 6
FPS = 8
PREVIEW_SCALE = 6
NEAR_WING_DISPLAY_OFFSET = (4, 0)
TAIL_ROOT = (101, 77)
UPPER_TAIL_SOURCE_POINTS = [(101, 77), (87, 78), (70, 77), (53, 76)]
LOWER_TAIL_SOURCE_POINTS = [(101, 77), (87, 80), (70, 81), (53, 82)]
UPPER_TAIL_TIP_OFFSETS = [(0, 0), (-1, 1), (-1, 2), (0, 3), (1, 2), (1, 1)]
LOWER_TAIL_TIP_OFFSETS = [(0, 1), (-1, 2), (0, 3), (1, 2), (1, 1), (0, 0)]
SHOULDER_FEATHER_TIPS = [
    [(115, 59), (113, 62)],
    [(114, 60), (112, 63)],
    [(112, 62), (110, 65)],
    [(113, 67), (111, 69)],
    [(112, 65), (111, 68)],
    [(115, 60), (113, 63)],
]
# Dense, one-pixel-deep feather clusters distributed around the torso contour.
# They hug the body instead of forming detached strokes. Head, neck and saddle
# are deliberately excluded.
TORSO_FRINGE_TUFTS = [
    ((109, 62), (-1, -1)),
    ((106, 64), (-1, -1)),
    ((103, 67), (-1, 0)),
    ((102, 71), (-1, 0)),
    ((104, 75), (-1, 1)),
    ((108, 78), (-1, 1)),
    ((112, 80), (0, 1)),
    ((117, 81), (0, 1)),
    ((122, 81), (0, 1)),
    ((127, 80), (1, 1)),
    ((131, 78), (1, 1)),
    ((134, 75), (1, 0)),
]
TORSO_FRINGE_PHASES = [0, 1, 2, 1, 0, 2]


def load(name: str) -> Image.Image:
    return Image.open(name).convert("RGBA")


def exact_overlay(base: Image.Image, layer: Image.Image) -> Image.Image:
    presence = layer.getchannel("A").point(lambda value: 255 if value else 0)
    return Image.composite(layer, base, presence)


def shift_layer(layer: Image.Image, offset: tuple[int, int]) -> Image.Image:
    shifted = Image.new("RGBA", CELL)
    shifted.alpha_composite(layer, offset)
    return shifted


def animate_tail(
    source: Image.Image,
    source_points: list[tuple[int, int]],
    tip_offsets: list[tuple[int, int]],
) -> list[Image.Image]:
    frames = []
    for offset_x, offset_y in tip_offsets:
        target_points = [
            source_points[0],
            source_points[1],
            (
                source_points[2][0] + round(offset_x * 0.5),
                source_points[2][1] + round(offset_y * 0.5),
            ),
            (source_points[3][0] + offset_x, source_points[3][1] + offset_y),
        ]
        frame = idle.keep_largest_connected_component(
            idle.warp_from_skeleton(source, source_points, target_points)
        )
        if not frame.getchannel("A").getpixel(TAIL_ROOT):
            nearest = min(
                (
                    (x, y)
                    for y in range(CELL[1])
                    for x in range(CELL[0])
                    if frame.getchannel("A").getpixel((x, y))
                ),
                key=lambda point: (point[0] - TAIL_ROOT[0]) ** 2
                + (point[1] - TAIL_ROOT[1]) ** 2,
            )
            frame.putpixel(TAIL_ROOT, frame.getpixel(nearest))
        frames.append(frame)
    return frames


def build_shoulder_feathers(tips: list[tuple[int, int]]) -> Image.Image:
    layer = Image.new("RGBA", CELL)
    draw = ImageDraw.Draw(layer)
    bases = [(121, 64), (120, 66)]
    for index, (tip, base) in enumerate(zip(tips, bases)):
        draw.line((tip, base), fill=idle.DARK, width=3)
        draw.line((tip, base), fill=idle.MID, width=1)
        if index == 0:
            draw.point(tip, fill=idle.LIGHT)
    return layer


def build_torso_fringe(frame_index: int) -> Image.Image:
    layer = Image.new("RGBA", CELL)
    draw = ImageDraw.Draw(layer)
    phase = TORSO_FRINGE_PHASES[frame_index]
    for index, (base, outward) in enumerate(TORSO_FRINGE_TUFTS):
        tip = (base[0] + outward[0], base[1] + outward[1])
        # Two touching pixels make a compact feather wedge. Only the outer
        # pixel shade/side alternates, so the fringe shimmers by one pixel but
        # never turns into a dangling line.
        draw.point(base, fill=idle.DARK)
        draw.point(tip, fill=idle.MID)
        if (index + phase) % 3 == 0:
            tangent = (-outward[1], outward[0])
            side = (base[0] + tangent[0], base[1] + tangent[1])
            draw.point(side, fill=idle.DARK)
        elif (index + phase) % 3 == 1:
            draw.point(tip, fill=idle.LIGHT)
    return layer


def assemble(
    upper_tail: Image.Image,
    lower_tail: Image.Image,
    far_wing: Image.Image,
    near_wing: Image.Image,
    body: Image.Image,
    body_fur: Image.Image,
    shoulder_feathers: Image.Image,
) -> Image.Image:
    result = Image.new("RGBA", CELL)
    # Far wing is behind the body; near wing is the foreground wing and must
    # cover the visible side of the body at its shoulder root.
    for layer in (
        upper_tail,
        lower_tail,
        far_wing,
        body,
        body_fur,
        near_wing,
        shoulder_feathers,
    ):
        result = exact_overlay(result, layer)
    return result


def checkerboard() -> Image.Image:
    image = Image.new("RGBA", CELL, (29, 32, 40, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, CELL[1], 4):
        for x in range(0, CELL[0], 4):
            if (x // 4 + y // 4) % 2:
                draw.rectangle((x, y, x + 3, y + 3), fill=(43, 47, 58, 255))
    return image


def display(frame: Image.Image, scale: int) -> Image.Image:
    image = checkerboard()
    image.alpha_composite(frame)
    return image.resize((CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST)


def save_gif(frames: list[Image.Image], path: Path) -> None:
    enlarged = [
        frame.resize(
            (CELL[0] * PREVIEW_SCALE, CELL[1] * PREVIEW_SCALE),
            Image.Resampling.NEAREST,
        )
        for frame in frames
    ]
    enlarged[0].save(
        path,
        save_all=True,
        append_images=enlarged[1:],
        duration=round(1000 / FPS),
        loop=0,
        disposal=2,
        transparency=0,
    )


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    body = load(str(PARTS / "MirrorBird_Normalized_body_v1.png"))
    upper_tail = load(str(PARTS / "MirrorBird_Normalized_upper_tail_v1.png"))
    lower_tail = load(str(PARTS / "MirrorBird_Normalized_lower_tail_v1.png"))
    far_poses = [
        load(str(WINGS / f"MirrorBird_FarWingPose{index:02d}_v1.png"))
        for index in range(1, FRAME_COUNT + 1)
    ]
    near_poses = [
        shift_layer(
            load(str(WINGS / f"MirrorBird_NearWingPose{index:02d}_v1.png")),
            NEAR_WING_DISPLAY_OFFSET,
        )
        for index in range(1, FRAME_COUNT + 1)
    ]
    upper_tail_poses = animate_tail(
        upper_tail, UPPER_TAIL_SOURCE_POINTS, UPPER_TAIL_TIP_OFFSETS
    )
    lower_tail_poses = animate_tail(
        lower_tail, LOWER_TAIL_SOURCE_POINTS, LOWER_TAIL_TIP_OFFSETS
    )
    shoulder_feather_poses = [
        build_shoulder_feathers(tips) for tips in SHOULDER_FEATHER_TIPS
    ]
    body_fur_poses = [build_torso_fringe(index) for index in range(FRAME_COUNT)]

    frames = [
        assemble(upper, lower, far, near, body, fur, feathers)
        for upper, lower, far, near, fur, feathers in zip(
            upper_tail_poses,
            lower_tail_poses,
            far_poses,
            near_poses,
            body_fur_poses,
            shoulder_feather_poses,
        )
    ]

    sheet = Image.new("RGBA", (CELL[0] * FRAME_COUNT, CELL[1]))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (index * CELL[0], 0))
    sheet_path = OUTPUT / "MirrorBird_AirIdle_RegeneratedWings_v10.png"
    sheet.save(sheet_path)

    gif_path = OUTPUT / "MirrorBird_AirIdle_RegeneratedWings_v10_6x.gif"
    save_gif(frames, gif_path)

    keyframes = Image.new("RGBA", (CELL[0] * 3 * 4, CELL[1] * 2 * 4))
    for index, frame in enumerate(frames):
        keyframes.alpha_composite(
            display(frame, 4),
            ((index % 3) * CELL[0] * 4, (index // 3) * CELL[1] * 4),
        )
    keyframes_path = OUTPUT / "MirrorBird_AirIdle_RegeneratedWingsKeyframes_v10_4x.png"
    keyframes.save(keyframes_path)

    body_alpha = body.getchannel("A")
    unoccluded_body_mismatch_pixels = 0
    for frame, near, fur, feathers in zip(
        frames, near_poses, body_fur_poses, shoulder_feather_poses
    ):
        near_alpha = near.getchannel("A")
        fur_alpha = fur.getchannel("A")
        feather_alpha = feathers.getchannel("A")
        for y in range(CELL[1]):
            for x in range(CELL[0]):
                if (
                    body_alpha.getpixel((x, y))
                    and not near_alpha.getpixel((x, y))
                    and not fur_alpha.getpixel((x, y))
                    and not feather_alpha.getpixel((x, y))
                    and frame.getpixel((x, y)) != body.getpixel((x, y))
                ):
                    unoccluded_body_mismatch_pixels += 1
    consecutive_differences = [
        sum(a != b for a, b in zip(frames[index - 1].tobytes(), frames[index].tobytes()))
        for index in range(1, FRAME_COUNT)
    ]
    decoded = Image.open(gif_path)
    decoded_frames = 0
    gif_alpha_ok = True
    try:
        while True:
            rgba = decoded.convert("RGBA")
            gif_alpha_ok &= rgba.getchannel("A").getextrema()[0] == 0
            decoded_frames += 1
            decoded.seek(decoded.tell() + 1)
    except EOFError:
        pass

    report = {
        "version": 10,
        "frames": FRAME_COUNT,
        "fps": FPS,
        "canvas": list(CELL),
        "parts": ["far_wing", "near_wing", "body", "upper_tail", "lower_tail"],
        "layer_order_back_to_front": [
            "upper_tail",
            "lower_tail",
            "far_wing",
            "body",
            "body_fur",
            "near_wing",
            "shoulder_feathers",
        ],
        "regenerated_wing_key_poses_used": True,
        "whole_wing_warp_used": False,
        "shoulder_feather_connector_added": True,
        "shoulder_feather_pixel_counts": [
            sum(value > 0 for value in frame.getchannel("A").getdata())
            for frame in shoulder_feather_poses
        ],
        "torso_perimeter_fringe_animated": True,
        "torso_perimeter_fringe_excludes_head_neck_saddle": True,
        "torso_perimeter_fringe_pixel_counts": [
            sum(value > 0 for value in frame.getchannel("A").getdata())
            for frame in body_fur_poses
        ],
        "torso_perimeter_fringe_phases": TORSO_FRINGE_PHASES,
        "near_wing_display_offset": list(NEAR_WING_DISPLAY_OFFSET),
        "near_wing_display_root": [
            116 + NEAR_WING_DISPLAY_OFFSET[0],
            64 + NEAR_WING_DISPLAY_OFFSET[1],
        ],
        "body_layer_source_modified": False,
        "unoccluded_body_mismatch_pixels_across_all_frames": unoccluded_body_mismatch_pixels,
        "tails_animated_in_this_preview": True,
        "tail_root": list(TAIL_ROOT),
        "upper_tail_tip_offsets": [list(offset) for offset in UPPER_TAIL_TIP_OFFSETS],
        "lower_tail_tip_offsets": [list(offset) for offset in LOWER_TAIL_TIP_OFFSETS],
        "upper_tail_root_distances": [
            idle.nearest_alpha_distance(frame.getchannel("A"), TAIL_ROOT)
            for frame in upper_tail_poses
        ],
        "lower_tail_root_distances": [
            idle.nearest_alpha_distance(frame.getchannel("A"), TAIL_ROOT)
            for frame in lower_tail_poses
        ],
        "consecutive_frame_difference_bytes": consecutive_differences,
        "gif_decoded_frames": decoded_frames,
        "gif_alpha_ok": gif_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_AirIdle_RegeneratedWings_v10_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
