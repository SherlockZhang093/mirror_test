from __future__ import annotations

import json
import statistics
from pathlib import Path

from PIL import Image, ImageDraw

import build_mirror_bird_idle_content as idle


ROOT = idle.ROOT
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleWingPoses_v1"
NEAR_SOURCE = OUTPUT / "MirrorBird_NearWingPoses_v1_alpha.png"
FAR_SOURCE = OUTPUT / "MirrorBird_FarWingPoses_v1_alpha.png"
MASTER = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png"

CELL = (192, 128)
SOURCE_CELL = (512, 512)
POSE_COUNT = 6
GLOBAL_SCALE = 0.118
FPS = 8
PREVIEW_SCALE = 6
NEAR_ROOT = (116, 64)
FAR_ROOT = (114, 65)


def source_cells(sheet: Image.Image) -> list[Image.Image]:
    cells = []
    for index in range(POSE_COUNT):
        column = index % 3
        row = index // 3
        cells.append(
            sheet.crop(
                (
                    column * SOURCE_CELL[0],
                    row * SOURCE_CELL[1],
                    (column + 1) * SOURCE_CELL[0],
                    (row + 1) * SOURCE_CELL[1],
                )
            )
        )
    return cells


def find_root(cell: Image.Image) -> tuple[int, int]:
    alpha = cell.getchannel("A")
    points = [
        (x, y)
        for y in range(cell.height)
        for x in range(cell.width)
        if alpha.getpixel((x, y)) >= 128
    ]
    if not points:
        raise ValueError("Wing pose cell is empty")
    maximum_x = max(x for x, _ in points)
    root_band = [(x, y) for x, y in points if x >= maximum_x - 12]
    return maximum_x - 2, round(statistics.median(y for _, y in root_band))


def normalize_pose(
    cell: Image.Image,
    source_root: tuple[int, int],
    target_root: tuple[int, int],
    master: Image.Image,
) -> Image.Image:
    resized = cell.resize(
        (
            round(cell.width * GLOBAL_SCALE),
            round(cell.height * GLOBAL_SCALE),
        ),
        Image.Resampling.NEAREST,
    )
    scaled_root = (
        round(source_root[0] * GLOBAL_SCALE),
        round(source_root[1] * GLOBAL_SCALE),
    )
    offset = (target_root[0] - scaled_root[0], target_root[1] - scaled_root[1])
    normalized = Image.new("RGBA", CELL)
    normalized.alpha_composite(resized, offset)
    normalized = idle.project_palette(master, normalized)

    # Guarantee an exact art pixel at the authored shoulder anchor without
    # changing the wing silhouette elsewhere.
    if not normalized.getchannel("A").getpixel(target_root):
        nearest = min(
            (
                (x, y)
                for y in range(CELL[1])
                for x in range(CELL[0])
                if normalized.getchannel("A").getpixel((x, y))
            ),
            key=lambda point: (point[0] - target_root[0]) ** 2
            + (point[1] - target_root[1]) ** 2,
        )
        normalized.putpixel(target_root, normalized.getpixel(nearest))
    return normalized


def exact_overlay(base: Image.Image, layer: Image.Image) -> Image.Image:
    presence = layer.getchannel("A").point(lambda value: 255 if value else 0)
    return Image.composite(layer, base, presence)


def checkerboard() -> Image.Image:
    image = Image.new("RGBA", CELL, (29, 32, 40, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, CELL[1], 4):
        for x in range(0, CELL[0], 4):
            if (x // 4 + y // 4) % 2:
                draw.rectangle((x, y, x + 3, y + 3), fill=(43, 47, 58, 255))
    return image


def display(layer: Image.Image, root: tuple[int, int] | None = None, scale: int = 4) -> Image.Image:
    image = checkerboard()
    image.alpha_composite(layer)
    if root:
        draw = ImageDraw.Draw(image)
        draw.line((root[0] - 2, root[1], root[0] + 2, root[1]), fill=(255, 70, 70, 255))
        draw.line((root[0], root[1] - 2, root[0], root[1] + 2), fill=(255, 70, 70, 255))
    return image.resize((CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST)


def save_pose_sheet(poses: list[Image.Image], root: tuple[int, int], path: Path) -> None:
    scale = 4
    sheet = Image.new("RGBA", (CELL[0] * 3 * scale, CELL[1] * 2 * scale))
    for index, pose in enumerate(poses):
        sheet.alpha_composite(
            display(pose, root, scale),
            ((index % 3) * CELL[0] * scale, (index // 3) * CELL[1] * scale),
        )
    sheet.save(path)


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
    master = Image.open(MASTER).convert("RGBA")
    near_cells = source_cells(Image.open(NEAR_SOURCE).convert("RGBA"))
    far_cells = source_cells(Image.open(FAR_SOURCE).convert("RGBA"))
    near_roots = [find_root(cell) for cell in near_cells]
    far_roots = [find_root(cell) for cell in far_cells]
    near_poses = [
        normalize_pose(cell, root, NEAR_ROOT, master)
        for cell, root in zip(near_cells, near_roots)
    ]
    far_poses = [
        normalize_pose(cell, root, FAR_ROOT, master)
        for cell, root in zip(far_cells, far_roots)
    ]

    for index, pose in enumerate(near_poses, start=1):
        pose.save(OUTPUT / f"MirrorBird_NearWingPose{index:02d}_v1.png")
    for index, pose in enumerate(far_poses, start=1):
        pose.save(OUTPUT / f"MirrorBird_FarWingPose{index:02d}_v1.png")

    save_pose_sheet(
        near_poses, NEAR_ROOT, OUTPUT / "MirrorBird_NearWingPoseAudit_v1_4x.png"
    )
    save_pose_sheet(
        far_poses, FAR_ROOT, OUTPUT / "MirrorBird_FarWingPoseAudit_v1_4x.png"
    )

    pair_frames = []
    for far, near in zip(far_poses, near_poses):
        pair = exact_overlay(Image.new("RGBA", CELL), far)
        pair = exact_overlay(pair, near)
        pair_frames.append(pair)
    gif_path = OUTPUT / "MirrorBird_WingPairOnly_v1_6x.gif"
    save_gif(pair_frames, gif_path)

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

    areas = {
        "near": [sum(value > 0 for value in pose.getchannel("A").getdata()) for pose in near_poses],
        "far": [sum(value > 0 for value in pose.getchannel("A").getdata()) for pose in far_poses],
    }
    report = {
        "version": 1,
        "poses": POSE_COUNT,
        "fps": FPS,
        "global_scale": GLOBAL_SCALE,
        "per_pose_scaling_used": False,
        "near_root": list(NEAR_ROOT),
        "far_root": list(FAR_ROOT),
        "near_root_distances": [
            idle.nearest_alpha_distance(pose.getchannel("A"), NEAR_ROOT)
            for pose in near_poses
        ],
        "far_root_distances": [
            idle.nearest_alpha_distance(pose.getchannel("A"), FAR_ROOT)
            for pose in far_poses
        ],
        "opaque_pixel_areas": areas,
        "gif_decoded_frames": decoded_frames,
        "gif_alpha_ok": gif_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_WingPosePreview_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
