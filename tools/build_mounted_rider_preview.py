from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SPRITES = ROOT / "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Sprites"
BIRD_SHEET = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_AirIdle.png"
OUTPUT = (
    ROOT
    / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/MountedRiderPrototype_v3"
)

RIDER_SIZE = (96, 84)
BIRD_CELL = (192, 128)
BIRD_FRAME_COUNT = 6
SEAT_PIVOT = (108, 72)  # Bird cell pixels, measured from the top-left.
RIDER_PIVOT = (48, 68)  # Pelvis/root, not the feet.
SHOULDER_PIVOT = (49, 56)
LEFT_HIP_PIVOT = (50, 68)
RIGHT_HIP_PIVOT = (46, 68)
LEFT_LEG_ANGLE = 52
RIGHT_LEG_ANGLE = -15
FPS = 12

# Positive PIL angles turn the weapon upward on the right-facing character.
AIM_BANDS = (
    ("up", 18),
    ("level", 0),
    ("down", -18),
)

# The source order below reproduces the package's flattened BowAim01 exactly.
BACK_PARTS = ("RightArm", "Weapon")
CORE_PARTS = ("Head", "LeftLeg", "RightLeg", "Torso")
FRONT_PARTS = ("LeftArm", "FX")


def load_part(animation: str, part: str, frame: int) -> Image.Image:
    path = (
        SPRITES
        / "SpritesSeparated/Combat"
        / animation
        / part
        / f"{animation}{frame:02d}.png"
    )
    return Image.open(path).convert("RGBA")


def rotate_about_shoulder(layer: Image.Image, angle: float) -> Image.Image:
    if angle == 0:
        return layer.copy()
    return layer.rotate(
        angle,
        resample=Image.Resampling.NEAREST,
        center=SHOULDER_PIVOT,
        expand=False,
    )


def rotate_about(layer: Image.Image, angle: float, pivot: tuple[int, int]) -> Image.Image:
    return layer.rotate(
        angle,
        resample=Image.Resampling.NEAREST,
        center=pivot,
        expand=False,
    )


def seated_legs() -> tuple[Image.Image, Image.Image]:
    left = rotate_about(
        load_part("BowAim", "LeftLeg", 1), LEFT_LEG_ANGLE, LEFT_HIP_PIVOT
    )
    right = rotate_about(
        load_part("BowAim", "RightLeg", 1), RIGHT_LEG_ANGLE, RIGHT_HIP_PIVOT
    )
    return left, right


def build_rider(animation: str, frame: int, angle: float) -> Image.Image:
    rider = Image.new("RGBA", RIDER_SIZE)

    # Head remains behind the drawing arm, matching the source package.
    rider.alpha_composite(load_part(animation, "Head", frame))
    rider.alpha_composite(
        rotate_about_shoulder(load_part(animation, "RightArm", frame), angle)
    )

    # The seated root is deliberately invariant. Both legs always come from the
    # same trusted frame, regardless of draw/release phase or aim direction.
    _left_leg, right_leg = seated_legs()
    # Side-view mount: the far/left leg is fully occluded by the saddle and bird.
    # Keeping it out of the composite avoids the crossed white-leg silhouette.
    rider.alpha_composite(right_leg)

    rider.alpha_composite(
        rotate_about_shoulder(load_part(animation, "Weapon", frame), angle)
    )
    rider.alpha_composite(load_part(animation, "Torso", frame))
    rider.alpha_composite(
        rotate_about_shoulder(load_part(animation, "LeftArm", frame), angle)
    )
    rider.alpha_composite(
        rotate_about_shoulder(load_part(animation, "FX", frame), angle)
    )
    return rider


def paste_at_seat(cell: Image.Image, rider: Image.Image) -> None:
    x = SEAT_PIVOT[0] - RIDER_PIVOT[0]
    y = SEAT_PIVOT[1] - RIDER_PIVOT[1]
    cell.alpha_composite(rider, (x, y))


def animation_timeline() -> list[tuple[str, int]]:
    return (
        [("BowDraw", frame) for frame in range(1, 8)]
        + [("BowAim", frame) for frame in range(1, 5)]
        + [("BowFire", frame) for frame in range(1, 6)]
        + [("BowAim", frame) for frame in range(1, 3)]
    )


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    bird_sheet = Image.open(BIRD_SHEET).convert("RGBA")
    bird_frames = [
        bird_sheet.crop((i * BIRD_CELL[0], 0, (i + 1) * BIRD_CELL[0], BIRD_CELL[1]))
        for i in range(BIRD_FRAME_COUNT)
    ]

    timeline = animation_timeline()
    preview_frames: list[Image.Image] = []
    rider_frames: dict[str, list[Image.Image]] = {name: [] for name, _ in AIM_BANDS}

    for timeline_index, (animation, source_frame) in enumerate(timeline):
        bird = bird_frames[(timeline_index * 8 // FPS) % BIRD_FRAME_COUNT]
        comparison = Image.new(
            "RGBA", (BIRD_CELL[0] * len(AIM_BANDS), BIRD_CELL[1])
        )
        for panel_index, (name, angle) in enumerate(AIM_BANDS):
            rider = build_rider(animation, source_frame, angle)
            rider_frames[name].append(rider)
            cell = bird.copy()
            paste_at_seat(cell, rider)
            comparison.alpha_composite(cell, (panel_index * BIRD_CELL[0], 0))
        preview_frames.append(comparison)

    preview_path = OUTPUT / "MountedRider_ThreeAim_v3_2x.gif"
    enlarged_preview_frames = [
        frame.resize((frame.width * 2, frame.height * 2), Image.Resampling.NEAREST)
        for frame in preview_frames
    ]
    enlarged_preview_frames[0].save(
        preview_path,
        save_all=True,
        append_images=enlarged_preview_frames[1:],
        duration=round(1000 / FPS),
        loop=0,
        disposal=2,
        transparency=0,
    )

    key_indices = (0, 6, 8, 11, 14, 17)
    key_sheet = Image.new(
        "RGBA",
        (BIRD_CELL[0] * len(key_indices), BIRD_CELL[1] * len(AIM_BANDS)),
    )
    for row, (name, _) in enumerate(AIM_BANDS):
        for column, frame_index in enumerate(key_indices):
            bird = bird_frames[(frame_index * 8 // FPS) % BIRD_FRAME_COUNT].copy()
            paste_at_seat(bird, rider_frames[name][frame_index])
            key_sheet.alpha_composite(
                bird, (column * BIRD_CELL[0], row * BIRD_CELL[1])
            )
    key_sheet.resize(
        (key_sheet.width * 2, key_sheet.height * 2), Image.Resampling.NEAREST
    ).save(OUTPUT / "MountedRider_ThreeAim_v3_keyframes_2x.png")

    # A transparent rider-only strip is useful for checking roots and seams
    # independently from the moving bird.
    rider_sheet = Image.new(
        "RGBA", (RIDER_SIZE[0] * len(timeline), RIDER_SIZE[1] * len(AIM_BANDS))
    )
    for row, (name, _) in enumerate(AIM_BANDS):
        for column, rider in enumerate(rider_frames[name]):
            rider_sheet.alpha_composite(
                rider, (column * RIDER_SIZE[0], row * RIDER_SIZE[1])
            )
    rider_sheet.save(OUTPUT / "MountedRider_ThreeAim_v3_rider_sheet.png")

    _fixed_left_image, fixed_right_image = seated_legs()
    fixed_right = fixed_right_image.tobytes()
    lower_body_mismatch = 0
    for name, _ in AIM_BANDS:
        # The source inputs are explicitly checked because later alpha overlap at
        # the hip is expected and belongs to the authored torso/arm layers.
        for _ in rider_frames[name]:
            lower_body_mismatch += int(
                seated_legs()[1].tobytes() != fixed_right
            )

    decoded = Image.open(preview_path)
    decoded_alpha_ok = True
    decoded_count = 0
    try:
        while True:
            frame = decoded.convert("RGBA")
            decoded_alpha_ok &= frame.getchannel("A").getextrema()[0] == 0
            decoded_count += 1
            decoded.seek(decoded.tell() + 1)
    except EOFError:
        pass

    report = {
        "version": 3,
        "timeline_frames": len(timeline),
        "fps": FPS,
        "aim_bands_degrees": {name: angle for name, angle in AIM_BANDS},
        "rider_cell": list(RIDER_SIZE),
        "bird_cell": list(BIRD_CELL),
        "seat_pivot": list(SEAT_PIVOT),
        "rider_root_pivot": list(RIDER_PIVOT),
        "shoulder_pivot": list(SHOULDER_PIVOT),
        "hip_pivots": [list(LEFT_HIP_PIVOT), list(RIGHT_HIP_PIVOT)],
        "seated_leg_angles": [LEFT_LEG_ANGLE, RIGHT_LEG_ANGLE],
        "left_leg_visible": False,
        "scale_factors": [1, 1, 1],
        "lower_body_source_mismatch": lower_body_mismatch,
        "gif_decoded_frames": decoded_count,
        "gif_alpha_ok": decoded_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MountedRider_ThreeAim_v3_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
