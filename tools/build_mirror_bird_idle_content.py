from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
MASTER_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png"
SKELETON_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleSkeleton_v3/MirrorBird_AirIdle_Skeleton_v3.json"
REFERENCE_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleContent_v7/MirrorBird_AirIdle_WingPoseReference_v7_alpha.png"
DOWN_REFERENCE_PATH = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleContent_v7/MirrorBird_AirIdle_DownstrokeReference_v7_alpha.png"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/AirIdleContent_v7"

CELL = (192, 128)
FRAME_COUNT = 10
FPS = 8
SCALE = 4

MASTER_WING_POLYGON = (
    (22, 0),
    (124, 0),
    (127, 68),
    (117, 76),
    (91, 76),
    (53, 67),
    (25, 50),
)

# The saddle/harness is a foreground attachment, not part of the bird body or
# wing action masks. Restore it independently after the current wings.
SADDLE_FOREGROUND_POLYGON = (
    (108, 61),
    (123, 61),
    (124, 71),
    (119, 77),
    (118, 89),
    (110, 89),
    (110, 76),
    (107, 71),
)

REFERENCE_SCALE = 0.283
REFERENCE_PANEL_ORIGINS = ((0, 0), (627, 0), (0, 627), (627, 627))
REFERENCE_BROW_BOXES = (
    (482, 309, 511, 324),
    (447, 317, 475, 331),
    (485, 218, 511, 230),
    (449, 245, 473, 255),
)
REFERENCE_WING_POLYGONS = (
    ((42, 4), (125, 4), (128, 75), (106, 81), (57, 73), (42, 46)),
    ((64, 0), (126, 0), (128, 75), (105, 80), (70, 59)),
    ((105, 58), (128, 58), (130, 127), (54, 127), (62, 108), (75, 91), (92, 74)),
    ((42, 24), (128, 24), (130, 82), (104, 82), (84, 67), (57, 56), (42, 48)),
)

REFERENCE_CONTROL_POINTS = (
    ((112, 65), (105, 50), (92, 32), (75, 18)),
    ((112, 65), (108, 45), (100, 24), (91, 5)),
    ((112, 65), (104, 83), (90, 96), (90, 115)),
    ((112, 65), (98, 55), (78, 48), (55, 43)),
    ((112, 64), (98, 65), (78, 62), (56, 58)),
)

DOWN_REFERENCE_SCALE = 0.143
DOWN_REFERENCE_BROW_BOX = (1112, 433, 1165, 459)
DOWN_REFERENCE_WING_POLYGON = (
    (34, 44),
    (126, 44),
    (130, 82),
    (98, 82),
    (70, 77),
    (34, 70),
)

# target frame -> (reference kind, approved base skeleton frame)
# kind -1 uses the trusted master wing; 0-3 use the generated pose-reference panels.
REFERENCE_PLAN = (
    (-1, 0),
    (1, 2),
    (1, 2),
    (0, 3),
    (3, 7),
    (4, 5),
    (4, 5),
    (3, 7),
    (0, 3),
    (-1, 0),
)

DARK = (42, 32, 56, 255)
MID = (80, 67, 99, 255)
LIGHT = (99, 84, 119, 255)


def polygon_mask(points: tuple[tuple[int, int], ...]) -> Image.Image:
    mask = Image.new("L", CELL)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def masked_sprite(sprite: Image.Image, mask: Image.Image) -> Image.Image:
    result = Image.new("RGBA", CELL)
    combined_alpha = Image.new("L", CELL)
    combined_alpha = Image.composite(sprite.getchannel("A"), combined_alpha, mask)
    result.paste(sprite, (0, 0), combined_alpha)
    return result


def darken(layer: Image.Image, numerator: int = 3, denominator: int = 4) -> Image.Image:
    pixels = []
    for red, green, blue, alpha in layer.getdata():
        pixels.append(
            (
                red * numerator // denominator,
                green * numerator // denominator,
                blue * numerator // denominator,
                alpha,
            )
        )
    result = Image.new("RGBA", layer.size)
    result.putdata(pixels)
    return result


def project_palette(master: Image.Image, layer: Image.Image) -> Image.Image:
    palette = sorted({
        (red, green, blue)
        for red, green, blue, alpha in master.getdata()
        if alpha > 0
    })
    cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    output = Image.new("RGBA", layer.size)
    projected = []
    for red, green, blue, alpha in layer.getdata():
        if alpha < 128:
            projected.append((0, 0, 0, 0))
            continue
        source = (red, green, blue)
        mapped = cache.get(source)
        if mapped is None:
            mapped = min(
                palette,
                key=lambda color: (
                    (color[0] - red) ** 2
                    + (color[1] - green) ** 2
                    + (color[2] - blue) ** 2
                ),
            )
            cache[source] = mapped
        projected.append((*mapped, 255))
    output.putdata(projected)
    return output


def remove_thin_reference_strands(layer: Image.Image) -> Image.Image:
    """Remove generated tail cords while retaining the thicker feather fan."""
    alpha = layer.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    opened = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    keep = ImageChops.multiply(alpha, opened.filter(ImageFilter.MaxFilter(3)))
    cleaned = Image.new("RGBA", CELL)
    cleaned.paste(layer, (0, 0), keep)
    return cleaned


def keep_largest_connected_component(layer: Image.Image) -> Image.Image:
    alpha = layer.getchannel("A")
    pixels = alpha.load()
    remaining = {
        (x, y)
        for y in range(CELL[1])
        for x in range(CELL[0])
        if pixels[x, y] > 0
    }
    components: list[set[tuple[int, int]]] = []
    while remaining:
        seed = remaining.pop()
        component = {seed}
        stack = [seed]
        while stack:
            x, y = stack.pop()
            for neighbor_y in range(max(0, y - 1), min(CELL[1], y + 2)):
                for neighbor_x in range(max(0, x - 1), min(CELL[0], x + 2)):
                    neighbor = (neighbor_x, neighbor_y)
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        stack.append(neighbor)
        components.append(component)
    if not components:
        return layer
    keep_points = max(components, key=len)
    keep_mask = Image.new("L", CELL)
    keep_pixels = keep_mask.load()
    for x, y in keep_points:
        keep_pixels[x, y] = 255
    cleaned = Image.new("RGBA", CELL)
    cleaned.paste(layer, (0, 0), keep_mask)
    return cleaned


def visible_far_wing(far_wing: Image.Image, near_wing: Image.Image) -> Image.Image:
    near_cover = near_wing.getchannel("A").filter(ImageFilter.MaxFilter(3))
    outside_near = Image.eval(near_cover, lambda value: 255 - value)
    visible_alpha = ImageChops.multiply(far_wing.getchannel("A"), outside_near)
    visible = Image.new("RGBA", CELL)
    visible.paste(far_wing, (0, 0), visible_alpha)
    return keep_largest_connected_component(visible)


def erase_reference_tail(layer: Image.Image, panel_index: int) -> Image.Image:
    if panel_index not in (2, 3):
        return layer
    erase = Image.new("L", CELL)
    draw = ImageDraw.Draw(erase)
    if panel_index == 2:
        draw.line(((109, 75), (82, 82), (56, 88), (29, 90)), fill=255, width=6)
        draw.line(((109, 78), (86, 88), (61, 99), (40, 105)), fill=255, width=6)
    else:
        draw.line(((110, 72), (82, 78), (56, 83), (29, 85)), fill=255, width=6)
        draw.line(((110, 75), (86, 85), (62, 95), (40, 100)), fill=255, width=6)
    keep = Image.eval(erase, lambda value: 255 - value)
    cleaned = Image.new("RGBA", CELL)
    cleaned.paste(layer, (0, 0), keep)
    return cleaned


def normalize_reference_panels(reference: Image.Image) -> list[Image.Image]:
    panels = []
    target_brow_center = (142.5, 60.0)
    for (origin_x, origin_y), brow_box in zip(
        REFERENCE_PANEL_ORIGINS, REFERENCE_BROW_BOXES
    ):
        panel = reference.crop((origin_x, origin_y, origin_x + 627, origin_y + 627))
        resized = panel.resize(
            (
                round(panel.width * REFERENCE_SCALE),
                round(panel.height * REFERENCE_SCALE),
            ),
            Image.Resampling.NEAREST,
        )
        brow_center_x = (brow_box[0] + brow_box[2]) * 0.5 * REFERENCE_SCALE
        brow_center_y = (brow_box[1] + brow_box[3]) * 0.5 * REFERENCE_SCALE
        offset = (
            round(target_brow_center[0] - brow_center_x),
            round(target_brow_center[1] - brow_center_y),
        )
        normalized = Image.new("RGBA", CELL)
        normalized.alpha_composite(resized, offset)
        panels.append(normalized)
    return panels


def normalize_down_reference(reference: Image.Image) -> Image.Image:
    resized = reference.resize(
        (
            round(reference.width * DOWN_REFERENCE_SCALE),
            round(reference.height * DOWN_REFERENCE_SCALE),
        ),
        Image.Resampling.NEAREST,
    )
    brow_center_x = (
        (DOWN_REFERENCE_BROW_BOX[0] + DOWN_REFERENCE_BROW_BOX[2])
        * 0.5
        * DOWN_REFERENCE_SCALE
    )
    brow_center_y = (
        (DOWN_REFERENCE_BROW_BOX[1] + DOWN_REFERENCE_BROW_BOX[3])
        * 0.5
        * DOWN_REFERENCE_SCALE
    )
    normalized = Image.new("RGBA", CELL)
    normalized.alpha_composite(
        resized,
        (round(142.5 - brow_center_x), round(60.0 - brow_center_y)),
    )
    return normalized


def warp_from_skeleton(
    layer: Image.Image,
    source_points: list[tuple[int, int]],
    target_points: list[tuple[int, int]],
) -> Image.Image:
    """Inverse-distance skeleton warp with nearest-neighbour pixel sampling."""
    source_pixels = layer.load()
    output = Image.new("RGBA", CELL)
    output_pixels = output.load()
    for y in range(CELL[1]):
        for x in range(CELL[0]):
            exact_source = None
            weighted_dx = 0.0
            weighted_dy = 0.0
            total_weight = 0.0
            for source, target in zip(source_points, target_points):
                delta_x = x - target[0]
                delta_y = y - target[1]
                distance_squared = delta_x * delta_x + delta_y * delta_y
                if distance_squared == 0:
                    exact_source = source
                    break
                weight = 1.0 / (distance_squared * distance_squared)
                weighted_dx += weight * (source[0] - target[0])
                weighted_dy += weight * (source[1] - target[1])
                total_weight += weight

            if exact_source is not None:
                source_x, source_y = exact_source
            else:
                source_x = round(x + weighted_dx / total_weight)
                source_y = round(y + weighted_dy / total_weight)
            if 0 <= source_x < CELL[0] and 0 <= source_y < CELL[1]:
                output_pixels[x, y] = source_pixels[source_x, source_y]
    return output


def draw_tapered_streamer(
    draw: ImageDraw.ImageDraw, points: list[tuple[int, int]]
) -> None:
    draw.line(points, fill=DARK, width=2, joint="curve")
    draw.line(points[:-1], fill=DARK, width=4, joint="curve")
    draw.line(points[:-1], fill=MID, width=2, joint="curve")
    if len(points) > 2:
        draw.line(points[1:-1], fill=LIGHT, width=1)
    draw.point(points[-1], fill=MID)


def build_tail(frame: dict[str, tuple[int, int]]) -> Image.Image:
    layer = Image.new("RGBA", CELL)
    draw = ImageDraw.Draw(layer)
    base = [frame[name] for name in ("pelvis", "tail_root", "tail_fork")]
    draw.line(base, fill=DARK, width=7, joint="curve")
    draw.line(base, fill=MID, width=4, joint="curve")
    draw.line(base[1:], fill=LIGHT, width=1)
    upper = [frame[name] for name in ("tail_fork", "upper_tail_mid", "upper_tail_tip")]
    lower = [frame[name] for name in ("tail_fork", "lower_tail_mid", "lower_tail_tip")]
    draw_tapered_streamer(draw, upper)
    draw_tapered_streamer(draw, lower)
    return layer


def nearest_alpha_distance(alpha: Image.Image, point: tuple[int, int]) -> float:
    opaque = alpha.load()
    best = float("inf")
    px, py = point
    for radius in range(0, 17):
        left = max(0, px - radius)
        right = min(CELL[0] - 1, px + radius)
        top = max(0, py - radius)
        bottom = min(CELL[1] - 1, py + radius)
        for x in range(left, right + 1):
            for y in (top, bottom):
                if opaque[x, y] > 0:
                    return math.hypot(x - px, y - py)
        for y in range(top + 1, bottom):
            for x in (left, right):
                if opaque[x, y] > 0:
                    return math.hypot(x - px, y - py)
    return best


def nearest_alpha_point(alpha: Image.Image, point: tuple[int, int]) -> tuple[int, int]:
    opaque = alpha.load()
    px, py = point
    best_point = point
    best_distance = float("inf")
    for y in range(CELL[1]):
        for x in range(CELL[0]):
            if opaque[x, y] == 0:
                continue
            candidate = math.hypot(x - px, y - py)
            if candidate < best_distance:
                best_distance = candidate
                best_point = (x, y)
    return best_point


def checkerboard() -> Image.Image:
    image = Image.new("RGBA", CELL, (34, 37, 46, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, CELL[1], 8):
        for x in range(0, CELL[0], 8):
            if (x // 8 + y // 8) % 2:
                draw.rectangle((x, y, x + 7, y + 7), fill=(42, 46, 57, 255))
    return image


def skeleton_overlay(
    content: Image.Image, frame: dict[str, tuple[int, int]]
) -> Image.Image:
    background = checkerboard()
    background.alpha_composite(content)
    draw = ImageDraw.Draw(background)
    chains = (
        (("pelvis", "chest", "neck_base", "head", "beak"), (255, 96, 72, 255)),
        (("near_wing_shoulder", "near_wing_elbow", "near_wing_wrist", "near_wing_hand"), (255, 214, 64, 255)),
        (("far_wing_shoulder", "far_wing_elbow", "far_wing_wrist", "far_wing_hand"), (92, 155, 255, 255)),
        (("pelvis", "tail_root", "tail_fork"), (71, 236, 220, 255)),
        (("tail_fork", "upper_tail_mid", "upper_tail_tip"), (71, 236, 220, 255)),
        (("tail_fork", "lower_tail_mid", "lower_tail_tip"), (71, 236, 220, 255)),
    )
    for names, color in chains:
        points = [frame[name] for name in names]
        draw.line(points, fill=color, width=1)
        for x, y in points:
            draw.ellipse((x - 1, y - 1, x + 1, y + 1), fill=(255, 255, 255, 255))
    return background


def save_gif(frames: list[Image.Image], path: Path) -> None:
    enlarged = [
        frame.resize((CELL[0] * SCALE, CELL[1] * SCALE), Image.Resampling.NEAREST)
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
    reference = Image.open(REFERENCE_PATH).convert("RGBA")
    down_reference = Image.open(DOWN_REFERENCE_PATH).convert("RGBA")
    skeleton_data = json.loads(SKELETON_PATH.read_text(encoding="utf-8"))
    skeleton_frames = [
        {name: tuple(point) for name, point in frame.items()}
        for frame in skeleton_data["frames"]
    ]
    if len(skeleton_frames) != FRAME_COUNT:
        raise ValueError(f"Expected {FRAME_COUNT} skeleton frames")

    master_wing = masked_sprite(master, polygon_mask(MASTER_WING_POLYGON))
    # No fixed-body polygon is used. Start from the complete trusted sprite and
    # remove the old wing through its actual alpha footprint only.
    old_wing_alpha = master_wing.getchannel("A")
    trusted_body_alpha = ImageChops.subtract(master.getchannel("A"), old_wing_alpha)
    trusted_body = Image.new("RGBA", CELL)
    trusted_body.paste(master, (0, 0), trusted_body_alpha)
    saddle = masked_sprite(master, polygon_mask(SADDLE_FOREGROUND_POLYGON))
    normalized_references = normalize_reference_panels(reference)
    reference_wings = [
        keep_largest_connected_component(
            remove_thin_reference_strands(
                project_palette(
                    master,
                    erase_reference_tail(
                        masked_sprite(panel, polygon_mask(points)), panel_index
                    ),
                ),
            )
        )
        for panel_index, (panel, points) in enumerate(
            zip(normalized_references, REFERENCE_WING_POLYGONS)
        )
    ]
    normalized_down = normalize_down_reference(down_reference)
    reference_wings.append(
        keep_largest_connected_component(
            remove_thin_reference_strands(
                project_palette(
                    master,
                    masked_sprite(
                        normalized_down,
                        polygon_mask(DOWN_REFERENCE_WING_POLYGON),
                    ),
                )
            )
        )
    )
    rest = skeleton_frames[0]
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
    outputs: list[Image.Image] = []
    overlays: list[Image.Image] = []
    wing_alpha_frames: list[Image.Image] = []
    wing_alignment: list[dict[str, float]] = []

    base_reference_alignment: list[dict[str, float]] = []
    for target_index, skeleton in enumerate(skeleton_frames):
        reference_kind, base_frame_index = REFERENCE_PLAN[target_index]
        source_layer = (
            master_wing if reference_kind == -1 else reference_wings[reference_kind]
        )
        base_skeleton = skeleton_frames[base_frame_index]
        source_alpha = source_layer.getchannel("A")
        source_wing_points = (
            [rest[name] for name in near_names]
            if reference_kind == -1
            else list(REFERENCE_CONTROL_POINTS[reference_kind])
        )
        near_wing = keep_largest_connected_component(
            warp_from_skeleton(
                source_layer,
                source_wing_points,
                [skeleton[name] for name in near_names],
            )
        )
        far_wing = darken(
            keep_largest_connected_component(
                warp_from_skeleton(
                    source_layer,
                    source_wing_points,
                    [skeleton[name] for name in far_names],
                )
            )
        )
        far_wing = visible_far_wing(far_wing, near_wing)
        wing_alpha_frames.append(
            ImageChops.lighter(near_wing.getchannel("A"), far_wing.getchannel("A"))
        )

        result = Image.new("RGBA", CELL)
        result.alpha_composite(build_tail(skeleton))
        result.alpha_composite(trusted_body)
        result.alpha_composite(far_wing)
        result.alpha_composite(near_wing)
        result.alpha_composite(saddle)
        outputs.append(result)
        overlays.append(skeleton_overlay(result, skeleton))

        alpha = near_wing.getchannel("A")
        wing_alignment.append(
            {
                name: round(nearest_alpha_distance(alpha, skeleton[name]), 3)
                for name in near_names
            }
        )
        base_reference_alignment.append(
            {
                name: round(nearest_alpha_distance(source_alpha, point), 3)
                for name, point in zip(near_names, source_wing_points)
            }
        )

    sheet = Image.new("RGBA", (CELL[0] * FRAME_COUNT, CELL[1]))
    for index, frame in enumerate(outputs):
        sheet.alpha_composite(frame, (index * CELL[0], 0))
    sheet_path = OUTPUT / "MirrorBird_AirIdle_SkeletonDriven_v7.png"
    sheet.save(sheet_path)

    gif_path = OUTPUT / "MirrorBird_AirIdle_SkeletonDriven_v7_4x.gif"
    overlay_path = OUTPUT / "MirrorBird_AirIdle_SkeletonDrivenOverlay_v7_4x.gif"
    save_gif(outputs, gif_path)
    save_gif(overlays, overlay_path)

    key_sheet = Image.new("RGBA", (CELL[0] * 2 * 5, CELL[1] * 2 * 2))
    for index, frame in enumerate(overlays):
        enlarged = frame.resize((CELL[0] * 2, CELL[1] * 2), Image.Resampling.NEAREST)
        key_sheet.alpha_composite(
            enlarged,
            ((index % 5) * CELL[0] * 2, (index // 5) * CELL[1] * 2),
        )
    key_sheet.save(OUTPUT / "MirrorBird_AirIdle_SkeletonDrivenKeyframes_v7_2x.png")

    # Validate body pixels never touched by any animated wing. They must remain
    # byte-identical to the trusted source in every frame.
    all_wing_alpha = Image.new("L", CELL)
    for wing_alpha in wing_alpha_frames:
        all_wing_alpha = ImageChops.lighter(all_wing_alpha, wing_alpha)
    opaque_trusted_body_alpha = trusted_body_alpha.point(
        lambda value: 255 if value == 255 else 0
    )
    protected_body_alpha = ImageChops.subtract(
        opaque_trusted_body_alpha, all_wing_alpha
    )
    protected_body_alpha = ImageChops.subtract(
        protected_body_alpha, saddle.getchannel("A")
    )
    expected_protected_body = masked_sprite(trusted_body, protected_body_alpha)
    trusted_body_source_mismatch = 0
    for frame in outputs:
        actual_protected_body = masked_sprite(frame, protected_body_alpha)
        trusted_body_source_mismatch += sum(
            actual != expected
            for actual, expected in zip(
                actual_protected_body.tobytes(), expected_protected_body.tobytes()
            )
        )

    consecutive_differences = [
        sum(a != b for a, b in zip(outputs[index - 1].tobytes(), outputs[index].tobytes()))
        for index in range(1, FRAME_COUNT)
    ]
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

    finite_alignment = [
        value
        for frame in wing_alignment
        for value in frame.values()
        if math.isfinite(value)
    ]
    report = {
        "version": 7,
        "frames": FRAME_COUNT,
        "fps": FPS,
        "reference_skeleton": str(SKELETON_PATH.relative_to(ROOT)).replace("\\", "/"),
        "trusted_core": str(MASTER_PATH.relative_to(ROOT)).replace("\\", "/"),
        "wing_sources": [
            str(MASTER_PATH.relative_to(ROOT)).replace("\\", "/"),
            str(REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
            str(DOWN_REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
        ],
        "imagegen_prompt_summary": "2x2 transition references plus a dedicated frame-5 horizontal downstroke reference; generated bodies discarded",
        "reference_global_scale": REFERENCE_SCALE,
        "reference_plan": [list(item) for item in REFERENCE_PLAN],
        "wing_deformation": "inverse-distance skeleton warp, nearest-neighbour",
        "fixed_body_polygon_mask_used": False,
        "old_wing_removed_by_source_alpha_footprint": True,
        "saddle_composited_as_independent_foreground": True,
        "generated_colors_projected_to_trusted_palette": True,
        "trusted_body_source_mismatch_bytes": trusted_body_source_mismatch,
        "consecutive_frame_difference_bytes": consecutive_differences,
        "wing_joint_distance_to_art_pixels": wing_alignment,
        "base_reference_joint_distance_to_art_pixels": base_reference_alignment,
        "maximum_wing_joint_distance_to_art_pixels": max(finite_alignment),
        "gif_decoded_frames": decoded_count,
        "gif_alpha_ok": gif_alpha_ok,
        "formal_unity_assets_modified": False,
    }
    (OUTPUT / "MirrorBird_AirIdle_SkeletonDriven_v7_validation.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
