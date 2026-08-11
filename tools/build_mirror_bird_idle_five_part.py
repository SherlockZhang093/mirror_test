from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

import build_mirror_bird_idle_content as idle


ROOT = idle.ROOT
SOURCE_DIR = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePartModel_v2"
SKELETON_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleSkeleton_v3/MirrorBird_AirIdle_Skeleton_v3.json"
MASTER_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePart_v1"

CELL = (192, 128)
FRAME_COUNT = 10
FPS = 8
PREVIEW_SCALE = 6
GLOBAL_SCALE = 0.118

PART_PATHS = {
    "far_wing": SOURCE_DIR / "MirrorBird_Part1_far_wing_v2.png",
    "near_wing": SOURCE_DIR / "MirrorBird_Part2_near_wing_v2.png",
    "body": SOURCE_DIR / "MirrorBird_Part3_body_v2.png",
    "upper_tail": SOURCE_DIR / "MirrorBird_Part4_upper_tail_v2.png",
    "lower_tail": SOURCE_DIR / "MirrorBird_Part5_lower_tail_v2.png",
}

# One global scale is used for every part. These integer offsets only restore
# each exploded part to the approved skeleton anchors on the common canvas.
PART_OFFSETS = {
    "far_wing": (56, 30),
    "near_wing": (6, 35),
    "body": (-20, 31),
    "upper_tail": (46, -3),
    "lower_tail": (-11, -3),
}

SOURCE_POINTS = {
    "far_wing": [(114, 65), (100, 63), (82, 62), (62, 65)],
    "near_wing": [(116, 64), (103, 72), (86, 86), (67, 96)],
    "upper_tail": [(101, 77), (91, 78), (72, 77), (53, 76)],
    "lower_tail": [(101, 77), (91, 79), (72, 80), (53, 82)],
}

TARGET_NAMES = {
    "far_wing": [
        "far_wing_shoulder",
        "far_wing_elbow",
        "far_wing_wrist",
        "far_wing_hand",
    ],
    "near_wing": [
        "near_wing_shoulder",
        "near_wing_elbow",
        "near_wing_wrist",
        "near_wing_hand",
    ],
    "upper_tail": ["tail_root", "tail_fork", "upper_tail_mid", "upper_tail_tip"],
    "lower_tail": ["tail_root", "tail_fork", "lower_tail_mid", "lower_tail_tip"],
}


def normalize_part(source: Image.Image, offset: tuple[int, int], master: Image.Image) -> Image.Image:
    resized = source.resize(
        (
            round(source.width * GLOBAL_SCALE),
            round(source.height * GLOBAL_SCALE),
        ),
        Image.Resampling.NEAREST,
    )
    normalized = Image.new("RGBA", CELL)
    normalized.alpha_composite(resized, offset)
    return idle.project_palette(master, normalized)


def exact_overlay(base: Image.Image, layer: Image.Image) -> Image.Image:
    presence = layer.getchannel("A").point(lambda value: 255 if value else 0)
    return Image.composite(layer, base, presence)


def assemble(
    upper_tail: Image.Image,
    lower_tail: Image.Image,
    far_wing: Image.Image,
    near_wing: Image.Image,
    body: Image.Image,
) -> Image.Image:
    result = Image.new("RGBA", CELL)
    for layer in (upper_tail, lower_tail, far_wing, near_wing, body):
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


def display_frame(frame: Image.Image, scale: int = PREVIEW_SCALE) -> Image.Image:
    display = checkerboard()
    display.alpha_composite(frame)
    return display.resize((CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST)


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
    master = Image.open(MASTER_PATH).convert("RGBA")
    skeleton_data = json.loads(SKELETON_PATH.read_text(encoding="utf-8"))
    skeleton_frames = [
        {name: tuple(point) for name, point in frame.items()}
        for frame in skeleton_data["frames"]
    ]
    if len(skeleton_frames) != FRAME_COUNT:
        raise ValueError(f"Expected {FRAME_COUNT} skeleton frames")

    normalized = {
        name: normalize_part(
            Image.open(path).convert("RGBA"), PART_OFFSETS[name], master
        )
        for name, path in PART_PATHS.items()
    }
    for name, layer in normalized.items():
        layer.save(OUTPUT / f"MirrorBird_Normalized_{name}_v1.png")

    frames: list[Image.Image] = []
    component_frames: dict[str, list[Image.Image]] = {
        name: [] for name in ("far_wing", "near_wing", "upper_tail", "lower_tail")
    }
    anchor_distances: list[dict[str, float]] = []
    for skeleton in skeleton_frames:
        animated: dict[str, Image.Image] = {}
        for name in component_frames:
            targets = [skeleton[key] for key in TARGET_NAMES[name]]
            animated[name] = idle.keep_largest_connected_component(
                idle.warp_from_skeleton(normalized[name], SOURCE_POINTS[name], targets)
            )
            component_frames[name].append(animated[name])

        frame = assemble(
            animated["upper_tail"],
            animated["lower_tail"],
            animated["far_wing"],
            animated["near_wing"],
            normalized["body"],
        )
        frames.append(frame)
        anchor_distances.append(
            {
                name: round(
                    idle.nearest_alpha_distance(
                        animated[part].getchannel("A"), skeleton[target_name]
                    ),
                    3,
                )
                for name, part, target_name in (
                    ("far_shoulder", "far_wing", "far_wing_shoulder"),
                    ("near_shoulder", "near_wing", "near_wing_shoulder"),
                    ("upper_tail_root", "upper_tail", "tail_root"),
                    ("lower_tail_root", "lower_tail", "tail_root"),
                )
            }
        )

    sheet = Image.new("RGBA", (CELL[0] * FRAME_COUNT, CELL[1]))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (index * CELL[0], 0))
    sheet_path = OUTPUT / "MirrorBird_AirIdle_FivePart_v1.png"
    sheet.save(sheet_path)

    gif_path = OUTPUT / "MirrorBird_AirIdle_FivePart_v1_6x.gif"
    save_gif(frames, gif_path)

    keyframes = Image.new("RGBA", (CELL[0] * 5 * 3, CELL[1] * 2 * 3))
    for index, frame in enumerate(frames):
        enlarged = display_frame(frame, 3)
        keyframes.alpha_composite(
            enlarged,
            ((index % 5) * CELL[0] * 3, (index // 5) * CELL[1] * 3),
        )
    keyframes_path = OUTPUT / "MirrorBird_AirIdle_FivePartKeyframes_v1_3x.png"
    keyframes.save(keyframes_path)

    body_presence = normalized["body"].getchannel("A")
    body_mismatch_pixels = 0
    for frame in frames:
        for y in range(CELL[1]):
            for x in range(CELL[0]):
                if body_presence.getpixel((x, y)) and frame.getpixel((x, y)) != normalized["body"].getpixel((x, y)):
                    body_mismatch_pixels += 1

    consecutive_differences = [
        sum(a != b for a, b in zip(frames[i - 1].tobytes(), frames[i].tobytes()))
        for i in range(1, FRAME_COUNT)
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

    finite_distances = [
        value
        for frame in anchor_distances
        for value in frame.values()
        if math.isfinite(value)
    ]
    report = {
        "version": 1,
        "part_count": 5,
        "parts": ["far_wing", "near_wing", "body", "upper_tail", "lower_tail"],
        "canvas": list(CELL),
        "ppu_target": 32,
        "frames": FRAME_COUNT,
        "fps": FPS,
        "global_scale": GLOBAL_SCALE,
        "per_frame_scaling_used": False,
        "sampling": "nearest-neighbour",
        "body_mismatch_pixels_across_all_frames": body_mismatch_pixels,
        "anchor_distances": anchor_distances,
        "maximum_anchor_distance_to_art_pixels": max(finite_distances),
        "consecutive_frame_difference_bytes": consecutive_differences,
        "gif_decoded_frames": decoded_frames,
        "gif_alpha_ok": gif_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_AirIdle_FivePart_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
