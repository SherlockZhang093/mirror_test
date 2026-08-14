from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

import build_mirror_bird_idle_from_regenerated_wings as bird
import build_mirror_bird_idle_content as idle


ROOT = Path(__file__).resolve().parents[1]
PARTS = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePart_v1"
WINGS = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleWingPoses_v1"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/HorizontalFlightFivePart_v1"

CELL = (192, 128)
FPS = 10
PREVIEW_SCALE = 6
POSE_SEQUENCE = [2, 3, 4, 5, 4, 3]
NEAR_WING_DISPLAY_OFFSET = (4, 0)
TAIL_ROOT = (101, 77)
UPPER_TAIL_TIP_OFFSETS = [(0, 0), (0, 1), (1, 1), (1, 0), (0, -1), (-1, -1)]
LOWER_TAIL_TIP_OFFSETS = [(-1, -1), (0, 0), (0, 1), (1, 1), (1, 0), (0, -1)]


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
    body = bird.load(str(PARTS / "MirrorBird_Normalized_body_v1.png"))
    upper_tail = bird.load(str(PARTS / "MirrorBird_Normalized_upper_tail_v1.png"))
    lower_tail = bird.load(str(PARTS / "MirrorBird_Normalized_lower_tail_v1.png"))

    far_poses = [
        bird.load(str(WINGS / f"MirrorBird_FarWingPose{pose:02d}_v1.png"))
        for pose in POSE_SEQUENCE
    ]
    near_poses = [
        bird.shift_layer(
            bird.load(str(WINGS / f"MirrorBird_NearWingPose{pose:02d}_v1.png")),
            NEAR_WING_DISPLAY_OFFSET,
        )
        for pose in POSE_SEQUENCE
    ]
    upper_tail_poses = bird.animate_tail(
        upper_tail,
        bird.UPPER_TAIL_SOURCE_POINTS,
        UPPER_TAIL_TIP_OFFSETS,
    )
    lower_tail_poses = bird.animate_tail(
        lower_tail,
        bird.LOWER_TAIL_SOURCE_POINTS,
        LOWER_TAIL_TIP_OFFSETS,
    )
    shoulder_poses = [
        bird.build_shoulder_feathers(bird.SHOULDER_FEATHER_TIPS[pose - 1])
        for pose in POSE_SEQUENCE
    ]
    blank = Image.new("RGBA", CELL)
    frames = [
        bird.assemble(upper, lower, far, near, body, blank, shoulder)
        for upper, lower, far, near, shoulder in zip(
            upper_tail_poses,
            lower_tail_poses,
            far_poses,
            near_poses,
            shoulder_poses,
        )
    ]

    sheet = Image.new("RGBA", (CELL[0] * len(frames), CELL[1]))
    keyframes = Image.new("RGBA", (CELL[0] * 3 * 4, CELL[1] * 2 * 4))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (index * CELL[0], 0))
        keyframes.alpha_composite(
            bird.display(frame, 4),
            ((index % 3) * CELL[0] * 4, (index // 3) * CELL[1] * 4),
        )

    sheet_path = OUTPUT / "MirrorBird_HorizontalFlight_FivePart_v1.png"
    gif_path = OUTPUT / "MirrorBird_HorizontalFlight_FivePart_v1_6x.gif"
    keyframes_path = OUTPUT / "MirrorBird_HorizontalFlight_FivePartKeyframes_v1_4x.png"
    sheet.save(sheet_path)
    save_gif(frames, gif_path)
    keyframes.save(keyframes_path)

    body_alpha = body.getchannel("A")
    mismatch = 0
    for frame, near, shoulder in zip(frames, near_poses, shoulder_poses):
        for y in range(CELL[1]):
            for x in range(CELL[0]):
                point = (x, y)
                if (
                    body_alpha.getpixel(point)
                    and not near.getchannel("A").getpixel(point)
                    and not shoulder.getchannel("A").getpixel(point)
                    and frame.getpixel(point) != body.getpixel(point)
                ):
                    mismatch += 1

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
        "version": 1,
        "action": "HorizontalFlight",
        "frames": len(frames),
        "fps": FPS,
        "canvas": list(CELL),
        "parts": ["far_wing", "near_wing", "body", "upper_tail", "lower_tail"],
        "pose_sequence": POSE_SEQUENCE,
        "body_layer_source_modified": False,
        "unoccluded_body_mismatch_pixels_across_all_frames": mismatch,
        "near_wing_display_root": [120, 64],
        "tail_root": list(TAIL_ROOT),
        "upper_tail_root_distances": [
            idle.nearest_alpha_distance(frame.getchannel("A"), TAIL_ROOT)
            for frame in upper_tail_poses
        ],
        "lower_tail_root_distances": [
            idle.nearest_alpha_distance(frame.getchannel("A"), TAIL_ROOT)
            for frame in lower_tail_poses
        ],
        "gif_decoded_frames": decoded_frames,
        "gif_alpha_ok": gif_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_HorizontalFlight_FivePart_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
