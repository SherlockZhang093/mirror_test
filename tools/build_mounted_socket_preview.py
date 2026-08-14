from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SHEETS = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Sheets"
OUTPUT = ROOT / "Assets/MirrorTrial/Art/Boss/AerialMount/Previews/MountedSocket_v1"
RIDER_SHEET = SHEETS / "MirrorHunter_MountedRider.png"
SEAT_PIVOT = (48, 68)
TINT = (188, 255, 171)

TRACKS = {
    "Turn": [(108, 72), (108, 72), (106, 71), (99, 69),
             (93, 69), (86, 71), (84, 72), (84, 72)],
    "Hit": [(108, 72), (105, 73), (101, 72), (108, 72)],
}
FPS = {"Turn": 16, "Hit": 12}


def tint_rider(image: Image.Image) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (
                r * TINT[0] // 255,
                g * TINT[1] // 255,
                b * TINT[2] // 255,
                a,
            )
    return result


def build(sequence: str, seats: list[tuple[int, int]], rider: Image.Image) -> None:
    sheet = Image.open(SHEETS / f"MirrorBird_{sequence}.png").convert("RGBA")
    frames = []
    for index, seat in enumerate(seats):
        frame = sheet.crop((index * 192, 0, (index + 1) * 192, 128))
        frame.alpha_composite(rider, (seat[0] - SEAT_PIVOT[0], seat[1] - SEAT_PIVOT[1]))
        frames.append(frame.resize((768, 512), Image.Resampling.NEAREST))
    duration = round(1000 / FPS[sequence])
    frames[0].save(
        OUTPUT / f"MountedSocket_{sequence}_v1_4x.gif",
        save_all=True,
        append_images=frames[1:],
        duration=duration,
        loop=0,
        disposal=2,
        transparency=0,
    )
    contact = Image.new("RGBA", (768 * len(frames), 512), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        contact.alpha_composite(frame, (index * 768, 0))
    contact.save(OUTPUT / f"MountedSocket_{sequence}_v1_frames_4x.png")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    rider_sheet = Image.open(RIDER_SHEET).convert("RGBA")
    # Level-row frame 7, matching the Unity installer.
    rider = tint_rider(rider_sheet.crop((7 * 96, 84, 8 * 96, 168)))
    for sequence, seats in TRACKS.items():
        build(sequence, seats, rider)


if __name__ == "__main__":
    main()
