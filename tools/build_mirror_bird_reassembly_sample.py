from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

import build_mirror_bird_idle_content as idle


OUTPUT = (
    idle.ROOT
    / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleReassembly_v1"
)
REFERENCE = OUTPUT / "MirrorBird_ReassemblyReference_v1_alpha.png"
CELL = (192, 128)
REFERENCE_SCALE = 0.118
REFERENCE_OFFSET = (2, 14)
WING_OFFSET = (4, 0)

# These masks classify only the generated pose reference. They never erase the
# trusted body. Their borders follow the visible feather fans and exclude the
# generated torso, saddle, head, and tail streamers.
FAR_WING_MASK = (
    (46, 54),
    (111, 54),
    (114, 61),
    (111, 73),
    (92, 76),
    (68, 70),
    (47, 63),
)
NEAR_WING_MASK = (
    (79, 68),
    (110, 59),
    (115, 66),
    (112, 82),
    (96, 96),
    (78, 111),
    (58, 110),
    (65, 96),
)


def shift_layer(layer: Image.Image, offset: tuple[int, int]) -> Image.Image:
    shifted = Image.new("RGBA", CELL)
    shifted.alpha_composite(layer, offset)
    return shifted


def build_clean_core(master: Image.Image) -> tuple[Image.Image, Image.Image]:
    """Remove only the visible raised wing above the authored back contour."""
    remove = Image.new("L", CELL)
    remove_pixels = remove.load()
    alpha = master.getchannel("A")
    for y in range(CELL[1]):
        for x in range(CELL[0]):
            if not alpha.getpixel((x, y)):
                continue
            if not (22 <= x <= 116 and 0 <= y <= 72):
                continue
            # Back contour: (53,72) -> (96,67) -> (116,61).
            if x <= 96:
                back_y = round(72 - (x - 53) * 5 / 43)
            else:
                back_y = round(67 - (x - 96) * 6 / 20)
            if y < back_y:
                remove_pixels[x, y] = 255

    keep = Image.eval(remove, lambda value: 255 - value)
    core = Image.composite(master, Image.new("RGBA", CELL), keep)
    return core, remove


def normalize_reference(reference: Image.Image) -> Image.Image:
    resized = reference.resize(
        (
            round(reference.width * REFERENCE_SCALE),
            round(reference.height * REFERENCE_SCALE),
        ),
        Image.Resampling.NEAREST,
    )
    normalized = Image.new("RGBA", CELL)
    normalized.alpha_composite(resized, REFERENCE_OFFSET)
    return normalized


def extract_wings(master: Image.Image, normalized: Image.Image) -> tuple[Image.Image, Image.Image]:
    far = idle.masked_sprite(normalized, idle.polygon_mask(FAR_WING_MASK))
    near = idle.masked_sprite(normalized, idle.polygon_mask(NEAR_WING_MASK))
    far = idle.keep_largest_connected_component(
        idle.project_palette(master, idle.darken(far))
    )
    near = idle.keep_largest_connected_component(idle.project_palette(master, near))
    far = shift_layer(far, WING_OFFSET)
    near = shift_layer(near, WING_OFFSET)
    return far, near


def checkerboard(size: tuple[int, int], square: int = 4) -> Image.Image:
    image = Image.new("RGBA", size, (29, 32, 40, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], square):
        for x in range(0, size[0], square):
            if (x // square + y // square) % 2:
                draw.rectangle(
                    (x, y, x + square - 1, y + square - 1),
                    fill=(43, 47, 58, 255),
                )
    return image


def present(layer: Image.Image, scale: int = 4) -> Image.Image:
    background = checkerboard(CELL)
    background.alpha_composite(layer)
    return background.resize(
        (CELL[0] * scale, CELL[1] * scale), Image.Resampling.NEAREST
    )


def save_comparison(master: Image.Image, core: Image.Image, far: Image.Image, near: Image.Image, assembled: Image.Image) -> Path:
    panels = (
        ("trusted original", master),
        ("clean connected core", core),
        ("far wing behind body", far),
        ("near wing", near),
        ("reassembled sample", assembled),
    )
    scale = 4
    panel_width = CELL[0] * scale
    panel_height = CELL[1] * scale
    label_height = 28
    board = Image.new(
        "RGBA",
        (panel_width * 3, (panel_height + label_height) * 2),
        (18, 20, 26, 255),
    )
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default()
    for index, (label, layer) in enumerate(panels):
        x = index % 3 * panel_width
        y = index // 3 * (panel_height + label_height)
        draw.text((x + 8, y + 8), label, fill=(240, 243, 249, 255), font=font)
        board.alpha_composite(present(layer, scale), (x, y + label_height))
    output = OUTPUT / "MirrorBird_ReassemblySample_v1_Comparison_4x.png"
    board.save(output)
    return output


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    master = Image.open(idle.MASTER_PATH).convert("RGBA")
    reference = Image.open(REFERENCE).convert("RGBA")
    core, removed_old_wing = build_clean_core(master)
    normalized = normalize_reference(reference)
    far, near = extract_wings(master, normalized)

    # The new wing roots go behind the complete connected body core. This is a
    # deliberate static assembly proof; later motion will retain these layers.
    assembled = Image.new("RGBA", CELL)
    assembled.alpha_composite(far)
    assembled.alpha_composite(near)
    # Exact overwrite keeps every trusted core pixel byte-identical, including
    # the source's partially transparent outline pixels.
    core_presence = core.getchannel("A").point(lambda value: 255 if value else 0)
    assembled = Image.composite(core, assembled, core_presence)

    core_alpha = core.getchannel("A")
    core_mismatch_pixels = sum(
        assembled.getpixel((x, y)) != core.getpixel((x, y))
        for y in range(CELL[1])
        for x in range(CELL[0])
        if core_alpha.getpixel((x, y))
    )
    trusted_palette = {
        (red, green, blue)
        for red, green, blue, alpha in master.getdata()
        if alpha
    }
    unexpected_colors = sorted(
        {
            (red, green, blue)
            for red, green, blue, alpha in assembled.getdata()
            if alpha and (red, green, blue) not in trusted_palette
        }
    )

    core.save(OUTPUT / "MirrorBird_CleanConnectedCore_v1.png")
    far.save(OUTPUT / "MirrorBird_FarWingPart_v1.png")
    near.save(OUTPUT / "MirrorBird_NearWingPart_v1.png")
    assembled.save(OUTPUT / "MirrorBird_ReassemblySample_v1.png")
    present(assembled, 8).save(OUTPUT / "MirrorBird_ReassemblySample_v1_8x.png")
    comparison = save_comparison(master, core, far, near, assembled)

    report = {
        "version": 1,
        "canvas": list(CELL),
        "ppu_target": 32,
        "sampling": "nearest-neighbour",
        "reference_scale": REFERENCE_SCALE,
        "reference_offset": list(REFERENCE_OFFSET),
        "wing_offset": list(WING_OFFSET),
        "layer_order": ["far_wing", "near_wing", "connected_core"],
        "connected_core_chain": ["pelvis", "chest", "neck_base", "head", "beak"],
        "sprite_root_is_art_layer": False,
        "rider_seat_is_art_layer": False,
        "removed_old_wing_pixels": sum(value > 0 for value in removed_old_wing.getdata()),
        "connected_core_mismatch_pixels": core_mismatch_pixels,
        "near_wing_shoulder_distance_to_art_pixels": round(
            idle.nearest_alpha_distance(near.getchannel("A"), (116, 64)), 3
        ),
        "far_wing_shoulder_distance_to_art_pixels": round(
            idle.nearest_alpha_distance(far.getchannel("A"), (114, 65)), 3
        ),
        "transparent_corners": all(
            assembled.getpixel(point)[3] == 0
            for point in ((0, 0), (191, 0), (0, 127), (191, 127))
        ),
        "unexpected_palette_colors": [list(color) for color in unexpected_colors],
        "output": str((OUTPUT / "MirrorBird_ReassemblySample_v1.png").relative_to(idle.ROOT)).replace("\\", "/"),
        "comparison": str(comparison.relative_to(idle.ROOT)).replace("\\", "/"),
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_ReassemblySample_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
