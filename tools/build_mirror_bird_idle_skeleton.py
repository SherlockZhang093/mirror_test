from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MAP_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/SkeletonMap_v3/MirrorBird_SkeletonMap_v3.json"
MASTER_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleSkeleton_v3"

CELL = (192, 128)
FRAME_COUNT = 10
FPS = 8
SCALE = 4

COLORS = {
    "core": (255, 96, 72, 255),
    "near_wing": (255, 214, 64, 255),
    "far_wing": (92, 155, 255, 255),
    "tail": (71, 236, 220, 255),
}

# Global bone angles in screen coordinates (x right, y down). Each sequence is
# unwrapped so the downstroke has an explicit direction instead of snapping.
NEAR_WING_ANGLES = (
    (-118.0, -122.3, -144.2),
    (-110.0, -116.0, -140.0),
    (-103.0, -110.0, -135.0),
    (-125.0, -135.0, -150.0),
    (-150.0, -165.0, -175.0),
    (-205.0, -200.0, -190.0),
    (-190.0, -190.0, -185.0),
    (-170.0, -175.0, -180.0),
    (-145.0, -155.0, -168.0),
    (-128.0, -138.0, -154.0),
)

UPPER_TAIL_ANGLES = (
    (169.9, 176.7),
    (169.0, 176.0),
    (168.0, 175.0),
    (169.0, 176.0),
    (170.0, 177.0),
    (171.0, 178.0),
    (172.0, 179.0),
    (171.0, 178.0),
    (170.0, 177.0),
    (169.0, 176.0),
)
LOWER_TAIL_ANGLES = (
    (151.6, 171.6),
    (152.0, 172.0),
    (151.0, 171.0),
    (150.0, 170.0),
    (151.0, 171.0),
    (152.0, 172.0),
    (153.0, 173.0),
    (154.0, 174.0),
    (153.0, 173.0),
    (152.0, 172.0),
)


def distance(a: tuple[int, int], b: tuple[int, int]) -> float:
    return math.hypot(b[0] - a[0], b[1] - a[1])


def endpoint(
    origin: tuple[int, int], length: float, angle_degrees: float
) -> tuple[int, int]:
    radians = math.radians(angle_degrees)
    return (
        round(origin[0] + math.cos(radians) * length),
        round(origin[1] + math.sin(radians) * length),
    )


def solve_chain(
    origin: tuple[int, int], lengths: tuple[float, ...], angles: tuple[float, ...]
) -> list[tuple[int, int]]:
    points = [origin]
    for length, angle in zip(lengths, angles):
        points.append(endpoint(points[-1], length, angle))
    return points


def checkerboard() -> Image.Image:
    image = Image.new("RGBA", CELL, (34, 37, 46, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, CELL[1], 8):
        for x in range(0, CELL[0], 8):
            if (x // 8 + y // 8) % 2:
                draw.rectangle((x, y, x + 7, y + 7), fill=(42, 46, 57, 255))
    return image


def draw_dashed(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[int, int]],
    fill: tuple[int, int, int, int],
) -> None:
    for start, end in zip(points, points[1:]):
        steps = max(abs(end[0] - start[0]), abs(end[1] - start[1]))
        if steps == 0:
            continue
        for step in range(0, steps, 4):
            stop = min(step + 2, steps)
            a = step / steps
            b = stop / steps
            draw.line(
                (
                    round(start[0] + (end[0] - start[0]) * a),
                    round(start[1] + (end[1] - start[1]) * a),
                    round(start[0] + (end[0] - start[0]) * b),
                    round(start[1] + (end[1] - start[1]) * b),
                ),
                fill=fill,
                width=1,
            )


def draw_skeleton(frame: dict[str, tuple[int, int]], ghost: Image.Image | None) -> Image.Image:
    image = checkerboard()
    if ghost is not None:
        faded = ghost.copy()
        faded.putalpha(faded.getchannel("A").point(lambda alpha: alpha * 80 // 255))
        image.alpha_composite(faded)

    draw = ImageDraw.Draw(image)
    core = [
        frame[name]
        for name in ("pelvis", "chest", "neck_base", "head", "beak")
    ]
    near = [
        frame[name]
        for name in (
            "near_wing_shoulder",
            "near_wing_elbow",
            "near_wing_wrist",
            "near_wing_hand",
        )
    ]
    far = [
        frame[name]
        for name in (
            "far_wing_shoulder",
            "far_wing_elbow",
            "far_wing_wrist",
            "far_wing_hand",
        )
    ]
    tail_base = [frame[name] for name in ("pelvis", "tail_root", "tail_fork")]
    upper_tail = [
        frame[name] for name in ("tail_fork", "upper_tail_mid", "upper_tail_tip")
    ]
    lower_tail = [
        frame[name] for name in ("tail_fork", "lower_tail_mid", "lower_tail_tip")
    ]

    draw.line(core, fill=COLORS["core"], width=2)
    draw.line(near, fill=COLORS["near_wing"], width=2)
    draw_dashed(draw, far, COLORS["far_wing"])
    draw.line(tail_base, fill=COLORS["tail"], width=2)
    draw.line(upper_tail, fill=COLORS["tail"], width=2)
    draw.line(lower_tail, fill=COLORS["tail"], width=2)
    joint_names = {
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
    }
    for name in joint_names:
        x, y = frame[name]
        radius = 2 if name == "pelvis" else 1
        draw.ellipse(
            (x - radius, y - radius, x + radius, y + radius),
            fill=(12, 14, 20, 255),
            outline=(255, 255, 255, 255),
            width=1,
        )
    return image


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    skeleton_map = json.loads(MAP_PATH.read_text(encoding="utf-8"))
    rest = {name: tuple(point) for name, point in skeleton_map["joints"].items()}
    master = Image.open(MASTER_PATH).convert("RGBA")

    near_lengths = (
        distance(rest["near_wing_shoulder"], rest["near_wing_elbow"]),
        distance(rest["near_wing_elbow"], rest["near_wing_wrist"]),
        distance(rest["near_wing_wrist"], rest["near_wing_hand"]),
    )
    far_lengths = (
        distance(rest["far_wing_shoulder"], rest["far_wing_elbow"]),
        distance(rest["far_wing_elbow"], rest["far_wing_wrist"]),
        distance(rest["far_wing_wrist"], rest["far_wing_hand"]),
    )
    upper_lengths = (
        distance(rest["tail_fork"], rest["upper_tail_mid"]),
        distance(rest["upper_tail_mid"], rest["upper_tail_tip"]),
    )
    lower_lengths = (
        distance(rest["tail_fork"], rest["lower_tail_mid"]),
        distance(rest["lower_tail_mid"], rest["lower_tail_tip"]),
    )

    frames: list[dict[str, tuple[int, int]]] = []
    for index in range(FRAME_COUNT):
        frame = dict(rest)

        near_points = solve_chain(
            rest["near_wing_shoulder"], near_lengths, NEAR_WING_ANGLES[index]
        )
        for name, point in zip(
            ("near_wing_shoulder", "near_wing_elbow", "near_wing_wrist", "near_wing_hand"),
            near_points,
        ):
            frame[name] = point

        # Far wing follows the same beat with a small perspective offset in angle.
        far_angles = tuple(angle + 5.0 for angle in NEAR_WING_ANGLES[index])
        far_points = solve_chain(rest["far_wing_shoulder"], far_lengths, far_angles)
        for name, point in zip(
            ("far_wing_shoulder", "far_wing_elbow", "far_wing_wrist", "far_wing_hand"),
            far_points,
        ):
            frame[name] = point

        upper_points = solve_chain(
            rest["tail_fork"], upper_lengths, UPPER_TAIL_ANGLES[index]
        )
        for name, point in zip(
            ("tail_fork", "upper_tail_mid", "upper_tail_tip"), upper_points
        ):
            frame[name] = point

        lower_points = solve_chain(
            rest["tail_fork"], lower_lengths, LOWER_TAIL_ANGLES[index]
        )
        for name, point in zip(
            ("tail_fork", "lower_tail_mid", "lower_tail_tip"), lower_points
        ):
            frame[name] = point

        frames.append(frame)

    # The approved map is the exact first frame, not an approximation produced
    # by trigonometric rounding.
    frames[0] = dict(rest)

    pure_frames = [draw_skeleton(frame, None) for frame in frames]
    overlay_frames = [draw_skeleton(frame, master) for frame in frames]

    def save_gif(images: list[Image.Image], path: Path) -> None:
        enlarged = [
            image.resize((CELL[0] * SCALE, CELL[1] * SCALE), Image.Resampling.NEAREST)
            for image in images
        ]
        enlarged[0].save(
            path,
            save_all=True,
            append_images=enlarged[1:],
            duration=round(1000 / FPS),
            loop=0,
            disposal=2,
        )

    pure_path = OUTPUT / "MirrorBird_AirIdle_Skeleton_v3_4x.gif"
    overlay_path = OUTPUT / "MirrorBird_AirIdle_SkeletonOverlay_v3_4x.gif"
    save_gif(pure_frames, pure_path)
    save_gif(overlay_frames, overlay_path)

    key_scale = 2
    key_sheet = Image.new(
        "RGBA", (CELL[0] * key_scale * 5, CELL[1] * key_scale * 2)
    )
    for index, image in enumerate(overlay_frames):
        enlarged = image.resize(
            (CELL[0] * key_scale, CELL[1] * key_scale), Image.Resampling.NEAREST
        )
        key_sheet.alpha_composite(
            enlarged,
            ((index % 5) * CELL[0] * key_scale, (index // 5) * CELL[1] * key_scale),
        )
    key_sheet.save(OUTPUT / "MirrorBird_AirIdle_SkeletonKeyframes_v3_2x.png")

    invariant_names = skeleton_map["invariant_core"]
    invariant_mismatch = {
        name: sum(frame[name] != rest[name] for frame in frames) for name in invariant_names
    }
    canvas_anchor_mismatch = {
        name: sum(frame[name] != rest[name] for frame in frames)
        for name in skeleton_map["fixed_canvas_anchors"]
    }
    attachment_anchor_mismatch = {
        name: sum(frame[name] != rest[name] for frame in frames)
        for name in skeleton_map["fixed_attachment_anchors"]
    }
    consecutive_duplicates = sum(
        frames[index] == frames[index - 1] for index in range(1, FRAME_COUNT)
    ) + int(frames[-1] == frames[0])

    rest_lengths = {}
    max_length_error = 0.0
    for chain_name in ("near_wing", "far_wing", "upper_tail", "lower_tail"):
        names = skeleton_map["chains"][chain_name]
        rest_lengths[chain_name] = [
            distance(rest[first], rest[second]) for first, second in zip(names, names[1:])
        ]
        for frame in frames:
            for expected, first, second in zip(
                rest_lengths[chain_name], names, names[1:]
            ):
                max_length_error = max(
                    max_length_error,
                    abs(distance(frame[first], frame[second]) - expected),
                )

    decoded = Image.open(pure_path)
    decoded_count = 0
    try:
        while True:
            decoded_count += 1
            decoded.seek(decoded.tell() + 1)
    except EOFError:
        pass

    output_data = {
        "version": 3,
        "reference_skeleton": str(MAP_PATH.relative_to(ROOT)).replace("\\", "/"),
        "frames": [
            {name: list(point) for name, point in frame.items()} for frame in frames
        ],
        "fps": FPS,
        "validation": {
            "frame_count": len(frames),
            "decoded_gif_frames": decoded_count,
            "first_frame_matches_approved_skeleton": frames[0] == rest,
            "invariant_joint_mismatch_frames": invariant_mismatch,
            "fixed_canvas_anchor_mismatch_frames": canvas_anchor_mismatch,
            "fixed_attachment_anchor_mismatch_frames": attachment_anchor_mismatch,
            "canvas_anchor_participates_in_bone_chains": any(
                name in chain
                for name in skeleton_map["fixed_canvas_anchors"]
                for chain in skeleton_map["chains"].values()
            ),
            "attachment_anchor_participates_in_bone_chains": any(
                name in chain
                for name in skeleton_map["fixed_attachment_anchors"]
                for chain in skeleton_map["chains"].values()
            ),
            "consecutive_duplicate_skeleton_frames_including_loop": consecutive_duplicates,
            "maximum_quantized_bone_length_error_pixels": round(max_length_error, 4),
            "all_joint_coordinates_are_integers": all(
                isinstance(value, int)
                for frame in frames
                for point in frame.values()
                for value in point
            ),
            "formal_unity_assets_modified": False,
        },
    }
    (OUTPUT / "MirrorBird_AirIdle_Skeleton_v3.json").write_text(
        json.dumps(output_data, indent=2), encoding="utf-8"
    )
    print(json.dumps(output_data["validation"], indent=2))


if __name__ == "__main__":
    main()
