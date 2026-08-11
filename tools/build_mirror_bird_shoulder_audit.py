from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

import build_mirror_bird_idle_content as idle


OUTPUT = (
    idle.ROOT
    / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleShoulderAudit_v1"
)
FRAME_INDEX = 5
SHOULDER_CROP = (96, 48, 136, 92)
SCALE = 8


def checkerboard(size: tuple[int, int], square: int = 2) -> Image.Image:
    image = Image.new("RGBA", size, (31, 34, 43, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], square):
        for x in range(0, size[0], square):
            if (x // square + y // square) % 2:
                draw.rectangle(
                    (x, y, x + square - 1, y + square - 1),
                    fill=(46, 50, 62, 255),
                )
    return image


def present(layer: Image.Image) -> Image.Image:
    crop = layer.crop(SHOULDER_CROP)
    background = checkerboard(crop.size)
    background.alpha_composite(crop)
    return background.resize(
        (crop.width * SCALE, crop.height * SCALE), Image.Resampling.NEAREST
    )


def normalize_wing_sources(master: Image.Image) -> list[Image.Image]:
    reference = Image.open(idle.REFERENCE_PATH).convert("RGBA")
    down_reference = Image.open(idle.DOWN_REFERENCE_PATH).convert("RGBA")
    panels = idle.normalize_reference_panels(reference)
    wings = [
        idle.keep_largest_connected_component(
            idle.remove_thin_reference_strands(
                idle.project_palette(
                    master,
                    idle.erase_reference_tail(
                        idle.masked_sprite(panel, idle.polygon_mask(points)),
                        panel_index,
                    ),
                )
            )
        )
        for panel_index, (panel, points) in enumerate(
            zip(panels, idle.REFERENCE_WING_POLYGONS)
        )
    ]
    normalized_down = idle.normalize_down_reference(down_reference)
    wings.append(
        idle.keep_largest_connected_component(
            idle.remove_thin_reference_strands(
                idle.project_palette(
                    master,
                    idle.masked_sprite(
                        normalized_down,
                        idle.polygon_mask(idle.DOWN_REFERENCE_WING_POLYGON),
                    ),
                )
            )
        )
    )
    return wings


def build_frame_layers() -> tuple[dict[str, Image.Image], dict[str, object]]:
    master = Image.open(idle.MASTER_PATH).convert("RGBA")
    skeleton_data = json.loads(idle.SKELETON_PATH.read_text(encoding="utf-8"))
    skeleton_frames = [
        {name: tuple(point) for name, point in frame.items()}
        for frame in skeleton_data["frames"]
    ]

    old_wing = idle.masked_sprite(
        master, idle.polygon_mask(idle.MASTER_WING_POLYGON)
    )
    old_wing_alpha = old_wing.getchannel("A")
    trusted_body_alpha = ImageChops.subtract(master.getchannel("A"), old_wing_alpha)
    cleared_body = Image.new("RGBA", idle.CELL)
    cleared_body.paste(master, (0, 0), trusted_body_alpha)
    saddle = idle.masked_sprite(
        master, idle.polygon_mask(idle.SADDLE_FOREGROUND_POLYGON)
    )

    skeleton = skeleton_frames[FRAME_INDEX]
    reference_kind, _ = idle.REFERENCE_PLAN[FRAME_INDEX]
    source_layer = normalize_wing_sources(master)[reference_kind]
    source_points = list(idle.REFERENCE_CONTROL_POINTS[reference_kind])
    near_names = (
        "near_wing_shoulder",
        "near_wing_elbow",
        "near_wing_wrist",
        "near_wing_hand",
    )
    far_names = (
        "far_wing_shoulder",
        "far_wing_elbow",
        "far_wing_wrist",
        "far_wing_hand",
    )
    near_wing = idle.keep_largest_connected_component(
        idle.warp_from_skeleton(
            source_layer,
            source_points,
            [skeleton[name] for name in near_names],
        )
    )
    far_wing = idle.darken(
        idle.keep_largest_connected_component(
            idle.warp_from_skeleton(
                source_layer,
                source_points,
                [skeleton[name] for name in far_names],
            )
        )
    )
    far_wing = idle.visible_far_wing(far_wing, near_wing)

    final = Image.new("RGBA", idle.CELL)
    final.alpha_composite(idle.build_tail(skeleton))
    final.alpha_composite(cleared_body)
    final.alpha_composite(far_wing)
    final.alpha_composite(near_wing)
    final.alpha_composite(saddle)

    old_mask = old_wing.getchannel("A").point(lambda value: 255 if value else 0)
    saddle_mask = saddle.getchannel("A").point(lambda value: 255 if value else 0)
    overlap = ImageChops.multiply(old_mask, saddle_mask)
    ownership = Image.new("RGBA", idle.CELL)
    ownership_pixels = ownership.load()
    master_alpha = master.getchannel("A")
    overlap_coordinates: list[list[int]] = []
    old_only_coordinates: list[list[int]] = []
    for y in range(SHOULDER_CROP[1], SHOULDER_CROP[3]):
        for x in range(SHOULDER_CROP[0], SHOULDER_CROP[2]):
            if not master_alpha.getpixel((x, y)):
                continue
            in_old = bool(old_mask.getpixel((x, y)))
            in_saddle = bool(saddle_mask.getpixel((x, y)))
            if in_old and in_saddle:
                ownership_pixels[x, y] = (255, 45, 210, 255)
                overlap_coordinates.append([x, y])
            elif in_old:
                ownership_pixels[x, y] = (255, 68, 55, 255)
                old_only_coordinates.append([x, y])
            elif in_saddle:
                ownership_pixels[x, y] = (255, 211, 60, 255)
            else:
                ownership_pixels[x, y] = (63, 210, 255, 255)

    layers = {
        "01_original": master,
        "02_body_after_old_wing_clear": cleared_body,
        "03_far_wing_frame05": far_wing,
        "04_near_wing_frame05": near_wing,
        "05_saddle_restore": saddle,
        "06_final_frame05": final,
        "07_mask_ownership": ownership,
    }
    report = {
        "audit_version": 1,
        "source_compositor_version": 7,
        "frame": FRAME_INDEX,
        "shoulder_crop": list(SHOULDER_CROP),
        "mask_legend": {
            "magenta": "opaque source pixel selected by both old-wing clear and saddle restore",
            "red": "opaque source pixel selected by old-wing clear only",
            "yellow": "opaque source pixel selected by saddle restore only",
            "cyan": "opaque source pixel selected by neither mask",
        },
        "old_wing_and_saddle_overlap_pixel_count_in_crop": len(overlap_coordinates),
        "old_wing_only_pixel_count_in_crop": len(old_only_coordinates),
        "overlap_coordinates": overlap_coordinates,
        "formal_unity_assets_modified": False,
    }
    return layers, report


def save_board(layers: dict[str, Image.Image]) -> None:
    panel_width = (SHOULDER_CROP[2] - SHOULDER_CROP[0]) * SCALE
    panel_height = (SHOULDER_CROP[3] - SHOULDER_CROP[1]) * SCALE
    label_height = 26
    columns = 4
    rows = 2
    board = Image.new(
        "RGBA",
        (panel_width * columns, (panel_height + label_height) * rows),
        (20, 22, 28, 255),
    )
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default()
    for index, (name, layer) in enumerate(layers.items()):
        column = index % columns
        row = index // columns
        x = column * panel_width
        y = row * (panel_height + label_height)
        board.alpha_composite(present(layer), (x, y + label_height))
        draw.text((x + 6, y + 7), name, fill=(238, 241, 248, 255), font=font)
    board.save(OUTPUT / "MirrorBird_AirIdle_ShoulderLayers_Frame05_v1_8x.png")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    layers, report = build_frame_layers()
    for name, layer in layers.items():
        layer.save(OUTPUT / f"{name}.png")
    save_board(layers)
    (OUTPUT / "MirrorBird_AirIdle_ShoulderAudit_v1.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
