from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import build_mirror_bird_idle_content as idle


OUTPUT = (
    idle.ROOT
    / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePartModel_v1"
)
REFERENCE = (
    idle.ROOT
    / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleReassembly_v1/MirrorBird_ReassemblyReference_v1_alpha.png"
)
CELL = (192, 128)
REFERENCE_SCALE = 0.118
REFERENCE_OFFSET = (2, 14)
SCALE = 6

PART_ORDER = ("upper_tail", "lower_tail", "far_wing", "near_wing", "body")
PART_COLORS = {
    "far_wing": (91, 156, 255, 255),
    "near_wing": (255, 210, 61, 255),
    "body": (255, 91, 105, 255),
    "upper_tail": (58, 226, 209, 255),
    "lower_tail": (185, 104, 255, 255),
}


def normalize_target(reference: Image.Image, master: Image.Image) -> Image.Image:
    resized = reference.resize(
        (
            round(reference.width * REFERENCE_SCALE),
            round(reference.height * REFERENCE_SCALE),
        ),
        Image.Resampling.NEAREST,
    )
    normalized = Image.new("RGBA", CELL)
    normalized.alpha_composite(resized, REFERENCE_OFFSET)
    return idle.project_palette(master, normalized)


def line_mask(points: list[tuple[int, int]], width: int) -> Image.Image:
    mask = Image.new("L", CELL)
    ImageDraw.Draw(mask).line(points, fill=255, width=width, joint="curve")
    return mask


def classify_parts(target: Image.Image) -> tuple[dict[str, Image.Image], Image.Image]:
    upper_tail_mask = line_mask(
        [(25, 82), (44, 84), (63, 83), (80, 78), (96, 69), (109, 64)], 7
    )
    lower_tail_mask = line_mask(
        [(36, 94), (54, 94), (70, 90), (84, 82), (99, 71), (110, 66)], 7
    )
    far_wing_mask = idle.polygon_mask(
        ((43, 52), (112, 52), (116, 61), (111, 78), (91, 79), (66, 72), (43, 64))
    )

    target_pixels = target.load()
    upper_pixels = upper_tail_mask.load()
    lower_pixels = lower_tail_mask.load()
    far_pixels = far_wing_mask.load()
    owners: dict[str, set[tuple[int, int]]] = {name: set() for name in PART_ORDER}

    for y in range(CELL[1]):
        for x in range(CELL[0]):
            if not target_pixels[x, y][3]:
                continue

            # The complete head-neck-chest-pelvis silhouette and saddle are one
            # body part. The boundary deliberately includes both shoulder sockets.
            if x >= 110 or (x >= 105 and 60 <= y <= 87):
                owner = "body"
            # Two independent streamer corridors run all the way to the tail root.
            elif upper_pixels[x, y] and y <= 86:
                owner = "upper_tail"
            elif lower_pixels[x, y] and y >= 84:
                owner = "lower_tail"
            elif far_pixels[x, y]:
                owner = "far_wing"
            else:
                owner = "near_wing"
            owners[owner].add((x, y))

    parts = {name: Image.new("RGBA", CELL) for name in PART_ORDER}
    ownership = Image.new("RGBA", CELL)
    for name, coordinates in owners.items():
        part_pixels = parts[name].load()
        ownership_pixels = ownership.load()
        for x, y in coordinates:
            part_pixels[x, y] = target_pixels[x, y]
            ownership_pixels[x, y] = PART_COLORS[name]
    return parts, ownership


def exact_composite(parts: dict[str, Image.Image]) -> Image.Image:
    result = Image.new("RGBA", CELL)
    for name in PART_ORDER:
        layer = parts[name]
        presence = layer.getchannel("A").point(lambda value: 255 if value else 0)
        result = Image.composite(layer, result, presence)
    return result


def checkerboard() -> Image.Image:
    image = Image.new("RGBA", CELL, (29, 32, 40, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, CELL[1], 4):
        for x in range(0, CELL[0], 4):
            if (x // 4 + y // 4) % 2:
                draw.rectangle((x, y, x + 3, y + 3), fill=(43, 47, 58, 255))
    return image


def present(layer: Image.Image) -> Image.Image:
    image = checkerboard()
    image.alpha_composite(layer)
    return image.resize((CELL[0] * SCALE, CELL[1] * SCALE), Image.Resampling.NEAREST)


def save_board(target: Image.Image, parts: dict[str, Image.Image], ownership: Image.Image, assembled: Image.Image) -> Path:
    panels = [
        ("confirmed target", target),
        ("1 far wing", parts["far_wing"]),
        ("2 near wing", parts["near_wing"]),
        ("3 body", parts["body"]),
        ("4 upper tail", parts["upper_tail"]),
        ("5 lower tail", parts["lower_tail"]),
        ("five-part ownership", ownership),
        ("reassembled", assembled),
    ]
    panel_width = CELL[0] * SCALE
    panel_height = CELL[1] * SCALE
    label_height = 28
    columns = 4
    rows = 2
    board = Image.new(
        "RGBA",
        (panel_width * columns, (panel_height + label_height) * rows),
        (18, 20, 26, 255),
    )
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default()
    for index, (label, layer) in enumerate(panels):
        x = index % columns * panel_width
        y = index // columns * (panel_height + label_height)
        draw.text((x + 8, y + 8), label, fill=(240, 243, 249, 255), font=font)
        board.alpha_composite(present(layer), (x, y + label_height))
    output = OUTPUT / "MirrorBird_FivePartModel_v1_6x.png"
    board.save(output)
    return output


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    master = Image.open(idle.MASTER_PATH).convert("RGBA")
    reference = Image.open(REFERENCE).convert("RGBA")
    target = normalize_target(reference, master)
    parts, ownership = classify_parts(target)
    assembled = exact_composite(parts)

    target.save(OUTPUT / "MirrorBird_FivePartTarget_v1.png")
    for index, name in enumerate(("far_wing", "near_wing", "body", "upper_tail", "lower_tail"), start=1):
        parts[name].save(OUTPUT / f"MirrorBird_Part{index}_{name}_v1.png")
    ownership.save(OUTPUT / "MirrorBird_FivePartOwnership_v1.png")
    assembled.save(OUTPUT / "MirrorBird_FivePartReassembled_v1.png")
    board_path = save_board(target, parts, ownership, assembled)

    mismatch_pixels = sum(
        target.getpixel((x, y)) != assembled.getpixel((x, y))
        for y in range(CELL[1])
        for x in range(CELL[0])
    )
    part_counts = {
        name: sum(1 for value in layer.getchannel("A").getdata() if value)
        for name, layer in parts.items()
    }
    report = {
        "version": 1,
        "part_count": 5,
        "parts": ["far_wing", "near_wing", "body", "upper_tail", "lower_tail"],
        "body_includes": ["head", "neck", "chest", "pelvis", "both_shoulders", "saddle"],
        "canvas": list(CELL),
        "ppu_target": 32,
        "sampling": "nearest-neighbour",
        "part_pixel_counts": part_counts,
        "reassembly_mismatch_pixels": mismatch_pixels,
        "transparent_corners": all(
            assembled.getpixel(point)[3] == 0
            for point in ((0, 0), (191, 0), (0, 127), (191, 127))
        ),
        "board": str(board_path.relative_to(idle.ROOT)).replace("\\", "/"),
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_FivePartModel_v1_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
