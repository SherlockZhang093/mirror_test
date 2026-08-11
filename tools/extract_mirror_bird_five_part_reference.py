from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleFivePartModel_v2"
SOURCE = OUTPUT / "MirrorBird_FivePartReference_v2_alpha.png"

# Fixed full-canvas regions from the approved five-part exploded reference.
# Every saved part retains the same 1536x1024 canvas and global scale; there is
# no tight crop or per-part rescaling.
PART_BOXES = {
    "far_wing": (55, 190, 525, 390),
    "near_wing": (525, 180, 970, 545),
    "body": (1010, 200, 1470, 450),
    "upper_tail": (50, 625, 500, 730),
    "lower_tail": (520, 625, 980, 755),
}


def extract(source: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    layer = Image.new("RGBA", source.size)
    layer.alpha_composite(source.crop(box), (box[0], box[1]))
    return layer


def exact_composite(parts: dict[str, Image.Image]) -> Image.Image:
    result = Image.new("RGBA", next(iter(parts.values())).size)
    for name in ("upper_tail", "lower_tail", "far_wing", "near_wing", "body"):
        layer = parts[name]
        presence = layer.getchannel("A").point(lambda value: 255 if value else 0)
        result = Image.composite(layer, result, presence)
    return result


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    parts = {name: extract(source, box) for name, box in PART_BOXES.items()}
    for index, name in enumerate(
        ("far_wing", "near_wing", "body", "upper_tail", "lower_tail"), start=1
    ):
        parts[name].save(OUTPUT / f"MirrorBird_Part{index}_{name}_v2.png")

    reassembled = exact_composite(parts)
    reassembled.save(OUTPUT / "MirrorBird_FivePartReassembled_v2.png")
    mismatch_pixels = sum(
        source.getpixel((x, y)) != reassembled.getpixel((x, y))
        for y in range(source.height)
        for x in range(source.width)
    )
    report = {
        "version": 2,
        "part_count": 5,
        "parts": ["far_wing", "near_wing", "body", "upper_tail", "lower_tail"],
        "body_includes": ["head", "neck", "chest", "pelvis", "both_shoulders", "saddle"],
        "body_excludes": ["far_wing", "near_wing", "upper_tail", "lower_tail"],
        "source_canvas": list(source.size),
        "full_canvas_layers": True,
        "tight_crop_used": False,
        "per_part_scaling_used": False,
        "reassembly_mismatch_pixels": mismatch_pixels,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_FivePartReference_v2_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
