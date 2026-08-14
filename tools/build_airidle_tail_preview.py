from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_AirIdle.png"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/MirrorBirdAirIdleTail_v1"

CELL = (192, 128)
FRAME_COUNT = 10
FPS = 8

DARK = (42, 32, 56, 255)
MID = (80, 67, 99, 255)
LIGHT = (99, 84, 119, 255)

# This polygon follows the old two-streamer alpha footprint and deliberately
# stops before the stable body mass. It does not use an automatic tight crop.
CLEAR_POLYGON = (
    (0, 84),
    (44, 77),
    (62, 75),
    (62, 88),
    (48, 98),
    (27, 115),
    (0, 118),
)

# Root is invariant. Motion begins only after the branch at x=51.
ROOT_OUTLINE = (
    (69, 77),
    (62, 76),
    (55, 79),
    (49, 83),
    (48, 87),
    (53, 90),
    (59, 87),
    (65, 83),
    (70, 81),
)
ROOT_FILL = (
    (66, 78),
    (61, 78),
    (56, 80),
    (51, 84),
    (51, 86),
    (54, 87),
    (58, 85),
    (63, 82),
    (67, 80),
)

UPPER_BASE = (
    (52, 85),
    (47, 88),
    (41, 91),
    (34, 94),
    (27, 96),
    (20, 97),
    (13, 97),
    (7, 96),
)
LOWER_BASE = (
    (52, 86),
    (48, 91),
    (43, 96),
    (37, 101),
    (30, 105),
    (23, 108),
    (16, 109),
    (10, 107),
)

UPPER_TIP_OFFSETS = (0, -1, -2, -1, 0, 1, 2, 1, 0, -1)
LOWER_TIP_OFFSETS = (1, 0, -1, -2, -1, 0, 1, 2, 1, 0)


def clear_old_tail(frame: Image.Image) -> Image.Image:
    cleaned = frame.copy()
    mask = Image.new("L", CELL)
    ImageDraw.Draw(mask).polygon(CLEAR_POLYGON, fill=255)
    cleaned.paste((0, 0, 0, 0), (0, 0), mask)
    return cleaned


def offset_path(
    points: tuple[tuple[int, int], ...], tip_offset: int
) -> list[tuple[int, int]]:
    last = len(points) - 1
    result = []
    for index, (x, y) in enumerate(points):
        # The first two joint points are fixed; curvature increases toward tip.
        weight = max(0.0, (index - 1) / (last - 1))
        result.append((x, y + round(tip_offset * weight)))
    return result


def draw_streamer(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]]) -> None:
    # Broad at the base, then progressively tapered to a one-pixel endpoint.
    draw.line(points, fill=DARK, width=2, joint="curve")
    draw.line(points[:-2], fill=DARK, width=4, joint="curve")
    draw.line(points[:-1], fill=MID, width=2, joint="curve")
    # One-pixel highlight stops before the root and tip so the feather tapers.
    draw.line(points[2:-1], fill=LIGHT, width=1)
    tip_x, tip_y = points[-1]
    draw.point((tip_x, tip_y), fill=MID)


def rebuild_frame(frame: Image.Image, index: int) -> tuple[Image.Image, Image.Image]:
    cleaned_core = clear_old_tail(frame)
    tail_layer = Image.new("RGBA", CELL)
    draw = ImageDraw.Draw(tail_layer)

    draw.polygon(ROOT_OUTLINE, fill=DARK)
    draw.polygon(ROOT_FILL, fill=MID)
    draw.line(((63, 79), (57, 82), (53, 85)), fill=LIGHT, width=1)

    upper = offset_path(UPPER_BASE, UPPER_TIP_OFFSETS[index])
    lower = offset_path(LOWER_BASE, LOWER_TIP_OFFSETS[index])
    draw_streamer(draw, upper)
    draw_streamer(draw, lower)

    # Tail stays behind the body and saddle; the untouched core restores their
    # original occlusion above the newly connected root.
    result = tail_layer.copy()
    result.alpha_composite(cleaned_core)
    return result, tail_layer


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGBA")
    expected_size = (CELL[0] * FRAME_COUNT, CELL[1])
    if source.size != expected_size:
        raise ValueError(f"Expected {expected_size}, found {source.size}")

    originals = [
        source.crop((i * CELL[0], 0, (i + 1) * CELL[0], CELL[1]))
        for i in range(FRAME_COUNT)
    ]
    rebuilt: list[Image.Image] = []
    tail_layers: list[Image.Image] = []
    for index, frame in enumerate(originals):
        result, tail = rebuild_frame(frame, index)
        rebuilt.append(result)
        tail_layers.append(tail)

    sheet = Image.new("RGBA", expected_size)
    for index, frame in enumerate(rebuilt):
        sheet.alpha_composite(frame, (index * CELL[0], 0))
    sheet_path = OUTPUT / "MirrorBird_AirIdle_TailRoot_v1.png"
    sheet.save(sheet_path)

    enlarged = [
        frame.resize((CELL[0] * 4, CELL[1] * 4), Image.Resampling.NEAREST)
        for frame in rebuilt
    ]
    gif_path = OUTPUT / "MirrorBird_AirIdle_TailRoot_v1_4x.gif"
    enlarged[0].save(
        gif_path,
        save_all=True,
        append_images=enlarged[1:],
        duration=round(1000 / FPS),
        loop=0,
        disposal=2,
        transparency=0,
    )

    comparison_frames = []
    for old, new in zip(originals, rebuilt):
        pair = Image.new("RGBA", (CELL[0] * 2, CELL[1]))
        pair.alpha_composite(old, (0, 0))
        pair.alpha_composite(new, (CELL[0], 0))
        comparison_frames.append(
            pair.resize((pair.width * 3, pair.height * 3), Image.Resampling.NEAREST)
        )
    comparison_path = OUTPUT / "MirrorBird_AirIdle_TailRoot_v1_compare_3x.gif"
    comparison_frames[0].save(
        comparison_path,
        save_all=True,
        append_images=comparison_frames[1:],
        duration=round(1000 / FPS),
        loop=0,
        disposal=2,
        transparency=0,
    )

    root_box = (53, 79, 61, 87)
    root_reference = tail_layers[0].crop(root_box).tobytes()
    root_mismatch = sum(
        tail.crop(root_box).tobytes() != root_reference for tail in tail_layers[1:]
    )

    clear_mask = Image.new("L", CELL)
    ImageDraw.Draw(clear_mask).polygon(CLEAR_POLYGON, fill=255)
    outside_mask = Image.eval(clear_mask, lambda value: 255 - value)
    unchanged_mismatch = 0
    for original, new, tail in zip(originals, rebuilt, tail_layers):
        # The authored action mask is the old tail footprint plus every new tail
        # pixel. All bytes outside that exact union must remain untouched.
        action_mask = ImageChops.lighter(clear_mask, tail.getchannel("A"))
        invariant_mask = Image.eval(action_mask, lambda value: 255 - value)
        diff = ImageChops.difference(original, new)
        invariant_diff = Image.new("RGBA", CELL)
        invariant_diff.paste(diff, (0, 0), invariant_mask)
        unchanged_mismatch += sum(value != 0 for value in invariant_diff.tobytes())

    decoded = Image.open(gif_path)
    decoded_count = 0
    gif_alpha_ok = True
    try:
        while True:
            rgba = decoded.convert("RGBA")
            gif_alpha_ok &= rgba.getchannel("A").getextrema()[0] == 0
            decoded_count += 1
            decoded.seek(decoded.tell() + 1)
    except EOFError:
        pass

    tail_differences = [
        sum(a != b for a, b in zip(tail_layers[i - 1].tobytes(), tail_layers[i].tobytes()))
        for i in range(1, FRAME_COUNT)
    ]
    report = {
        "version": 1,
        "frames": FRAME_COUNT,
        "fps": FPS,
        "cell": list(CELL),
        "fixed_root_box": list(root_box),
        "fixed_root_mismatch_frames": root_mismatch,
        "unchanged_core_mismatch_bytes": unchanged_mismatch,
        "consecutive_tail_difference_bytes": tail_differences,
        "gif_decoded_frames": decoded_count,
        "gif_alpha_ok": gif_alpha_ok,
        "nearest_neighbor_scale": 4,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_AirIdle_TailRoot_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
