"""Builds the Inno Setup wizard bitmaps from the app icon."""
from PIL import Image

ICON = "icon-clean.png"
BACKGROUND = (0, 0, 0)


def build(target: str, size: tuple[int, int], icon_fraction: float) -> None:
    canvas = Image.new("RGB", size, BACKGROUND)
    icon = Image.open(ICON).convert("RGBA")

    side = int(min(size) * icon_fraction)
    icon = icon.resize((side, side), Image.LANCZOS)

    canvas.paste(icon, ((size[0] - side) // 2, (size[1] - side) // 2), icon)
    canvas.save(target)
    print(f"{target}: {size[0]}x{size[1]}")


build("installer/assets/wizard-large.bmp", (164, 314), 0.55)
build("installer/assets/wizard-small.bmp", (55, 58), 0.75)
