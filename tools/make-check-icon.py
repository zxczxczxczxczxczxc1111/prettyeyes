"""Зелёный глаз для проверочной сборки: сдвиг тона боевой иконки.

Своего рисунка нет намеренно. Иконку дал человек, и перерисовывать её ради
служебной сборки значит заводить второй источник правды: поправят одну,
забудут другую.
"""
import sys

from PIL import Image

# Windows-консоль иначе роняет скрипт на печати УЖЕ записанного файла, и отказ
# выглядит как провал там, где всё сработало.
sys.stdout.reconfigure(encoding="utf-8")

SRC = r"src\PrettyEyes.App\Assets\prettyeyes.ico"
LOGO_SRC = r"src\PrettyEyes.App\Assets\logo.png"
LOGO_OUT = r"src\PrettyEyes.App\Assets\logo-check.png"
OUT = [r"src\PrettyEyes.App\Assets\prettyeyes-check.ico", r"prettyeyes-check.ico"]
SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]

# Замерено по кадру 256: доминанта фиолетового около 270 градусов. Сдвиг уводит
# её к 110-115, это зелёный, не бирюза и не салат.
HUE_SHIFT = 205


def recolour(frame):
    """Тон крутим, насыщенность и яркость не трогаем: рисунок остаётся тем же."""
    hsv = frame.convert("RGB").convert("HSV")
    h, s, v = hsv.split()
    h = h.point(lambda value: (value + int(HUE_SHIFT * 255 / 360)) % 256)
    out = Image.merge("HSV", (h, s, v)).convert("RGB")
    out.putalpha(frame.getchannel("A"))
    return out


def main():
    source = Image.open(SRC)
    frames = []

    for size in SIZES:
        source.size = size
        source.load()
        frames.append(recolour(source.convert("RGBA")))

    # Each frame keeps its own artwork: the 16x16 in the icon is drawn, not
    # scaled down from 256, and that is the one the tray shows.
    for path in OUT:
        frames[-1].save(path, format="ICO", sizes=SIZES, append_images=frames[:-1])
        print("written:", path)

    # The settings window shows a big eye of its own, and it is a PNG rather
    # than the icon: left alone, the check build wears a purple logo.
    recolour(Image.open(LOGO_SRC).convert("RGBA")).save(LOGO_OUT)
    print("written:", LOGO_OUT)


if __name__ == "__main__":
    main()
