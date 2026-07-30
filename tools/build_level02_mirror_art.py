from collections import deque
from pathlib import Path
from PIL import Image, ImageFilter
import random


SOURCE = Path(r"Assets/MirrorTrial/Art/Level02_Jungle/MirrorGate/level02_jungle_mirror_frame_chroma.png")
FRAME = Path(r"Assets/MirrorTrial/Art/Level02_Jungle/MirrorGate/level02_jungle_mirror_frame.png")
SURFACE = Path(r"Assets/MirrorTrial/Art/Level02_Jungle/MirrorGate/level02_jungle_mirror_surface.png")


def is_key(pixel):
    r, g, b, _ = pixel
    return r > 215 and b > 190 and g < 75 and r - g > 150 and b - g > 125


def build_surface():
    image = Image.open(SOURCE).convert("RGBA")
    width, height = image.size
    pixels = image.load()
    start = (width // 2, height // 2)
    if not is_key(pixels[start[0], start[1]]):
        raise RuntimeError("The center of the generated frame is not chroma-key colored.")

    visited = bytearray(width * height)
    queue = deque([start])
    visited[start[1] * width + start[0]] = 1
    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height:
                continue
            index = ny * width + nx
            if visited[index] or not is_key(pixels[nx, ny]):
                continue
            visited[index] = 1
            queue.append((nx, ny))

    mask = Image.new("L", image.size, 0)
    mask_pixels = mask.load()
    for y in range(height):
        for x in range(width):
            if visited[y * width + x]:
                mask_pixels[x, y] = 255
    mask = mask.filter(ImageFilter.GaussianBlur(1.0))

    random.seed(20402)
    surface = Image.new("RGBA", image.size, (0, 0, 0, 0))
    out = surface.load()
    alpha = mask.load()
    for y in range(height):
        t = y / max(1, height - 1)
        for x in range(width):
            a = alpha[x, y]
            if not a:
                continue
            diagonal = (x / width) * 0.20 + (1.0 - t) * 0.10
            shimmer = 7 if ((x + y * 2) // 53) % 7 == 0 else 0
            noise = random.randrange(-3, 4)
            out[x, y] = (
                max(0, min(255, int(20 + diagonal * 22 + noise))),
                max(0, min(255, int(105 + diagonal * 55 + shimmer + noise))),
                max(0, min(255, int(118 + diagonal * 70 + shimmer + noise))),
                int(a * 0.82),
            )
    surface.save(SURFACE)


if __name__ == "__main__":
    build_surface()
