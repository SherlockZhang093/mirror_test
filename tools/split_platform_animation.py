from pathlib import Path
from PIL import Image
import numpy as np


SOURCE = Path(r"C:\Users\ZHARKZ~1\AppData\Local\Temp\codex-clipboard-900e8abf-07f0-4a42-8847-32dbfcf05bb9.png")
OUT = Path(r"G:\mirror_test\output\platform_animation_v3")


def line_runs(mask):
    runs = []
    start = None
    for i, value in enumerate(mask):
        if value and start is None:
            start = i
        elif not value and start is not None:
            runs.append((start, i - 1))
            start = None
    if start is not None:
        runs.append((start, len(mask) - 1))
    return runs


def remove_magenta(img):
    a = np.array(img.convert("RGBA"), dtype=np.float32)
    rgb = a[..., :3]
    # Remove the saturated pink key and its antialiased halo. Dark purple vine
    # pixels remain because their luminance is below this key range.
    rb = (rgb[..., 0] + rgb[..., 2]) / 2
    chroma = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]
    key = (rb > 82) & (chroma > 45) & (np.abs(rgb[..., 0] - rgb[..., 2]) < 105)
    a[..., 3] = np.where(key, 0, 255)
    # Despill any surviving bright pink boundary pixels.
    fringe = (~key) & (rb > 70) & (chroma > 28) & (np.abs(rgb[..., 0] - rgb[..., 2]) < 80)
    neutral = np.maximum(rgb[..., 1], np.minimum(rgb[..., 0], rgb[..., 2]) * 0.48)
    a[..., 0][fringe] = np.minimum(a[..., 0][fringe], neutral[fringe])
    a[..., 2][fringe] = np.minimum(a[..., 2][fringe], neutral[fringe] * 1.08)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def largest_component_bounds(mask):
    """Return the bbox of the largest 8-connected foreground component."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    best = None
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        stack = [(int(sx), int(sy))]
        seen[sy, sx] = True
        minx = maxx = int(sx)
        miny = maxy = int(sy)
        count = 0
        while stack:
            x, y = stack.pop()
            count += 1
            minx, maxx = min(minx, x), max(maxx, x)
            miny, maxy = min(miny, y), max(maxy, y)
            for ny in range(max(0, y - 1), min(h, y + 2)):
                for nx in range(max(0, x - 1), min(w, x + 2)):
                    if mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((nx, ny))
        if best is None or count > best[0]:
            best = (count, (minx, miny, maxx + 1, maxy + 1))
    return best[1] if best else (0, 0, w, h)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    frames_dir = OUT / "frames_png"
    frames_dir.mkdir(exist_ok=True)

    sheet = Image.open(SOURCE).convert("RGB")
    arr = np.array(sheet)
    # The supplied contact sheet has irregular cell widths/heights. These are
    # the actual divider coordinates (rather than a lossy equal-size split).
    x_lines = [184, 384, 588, 797, 1024]
    y_lines = [332, 650, 961]
    x_edges = [-1] + x_lines + [sheet.width]
    y_edges = [-1] + y_lines + [sheet.height]

    if len(x_edges) != 7 or len(y_edges) != 5:
        raise RuntimeError(f"Unexpected grid: x={x_lines}, y={y_lines}")

    raw_frames = []
    for row in range(4):
        for col in range(6):
            left, right = x_edges[col] + 2, x_edges[col + 1] - 1
            top, bottom = y_edges[row] + 2, y_edges[row + 1] - 1
            cell = sheet.crop((left, top, right, bottom))
            # Remove the printed frame number before keying.
            px = np.array(cell)
            px[:35, :36] = np.array([239, 0, 235], dtype=np.uint8)
            cell = Image.fromarray(px, "RGB")
            keyed = remove_magenta(cell)
            raw_frames.append(keyed)

    # One fixed 1:1 scale and one integer root anchor for every frame.
    frames = []
    canvas_size = (320, 360)
    target_root = (160, 342)
    anchors = []
    scale_factors = []
    for index, keyed in enumerate(raw_frames):
        alpha = np.array(keyed.getchannel("A"))
        bbox = largest_component_bounds(alpha > 127)
        # Once the platform is assembled, correct scale drift already present
        # in the source sheet by locking the connected core to one width.
        scale = 1.0
        if index >= 7:
            core_width = max(1, bbox[2] - bbox[0])
            scale = float(np.clip(184.0 / core_width, 0.88, 1.22))
            if abs(scale - 1.0) > 0.005:
                keyed = keyed.resize(
                    (round(keyed.width * scale), round(keyed.height * scale)),
                    Image.Resampling.NEAREST,
                )
                alpha = np.array(keyed.getchannel("A"))
                bbox = largest_component_bounds(alpha > 127)
        scale_factors.append(round(scale, 3))
        root = ((bbox[0] + bbox[2]) // 2, bbox[3] - 1)
        anchors.append(root)
        canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
        pos = (target_root[0] - root[0], target_root[1] - root[1])
        canvas.alpha_composite(keyed, pos)
        frames.append(canvas)
        canvas.save(frames_dir / f"frame_{len(frames):02d}.png")

    durations = [42] * 23 + [250]
    frames[0].save(
        OUT / "platform_build_transparent.gif",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        transparency=0,
        optimize=False,
    )
    frames[0].save(
        OUT / "platform_build_transparent.png",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
    )
    frames[0].save(
        OUT / "platform_build_transparent.webp",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        lossless=True,
        method=6,
    )
    print(f"grid x={x_lines}, y={y_lines}")
    print(f"saved {len(frames)} frames to {OUT}")
    print(f"root anchors={anchors}")
    print(f"scale factors={scale_factors}")


if __name__ == "__main__":
    main()
