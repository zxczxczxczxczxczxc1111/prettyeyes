"""Готовит глифы эмодзи из папки с исходниками в ассеты приложения.

Исходники приходят как есть из генератора: больше мегабайта на файл, поля
вокруг рисунка от нуля до двухсот пикселей, изредка чёрный фон вместо альфы.
Ни одно из этого приложению не годится: на снимке глиф рисуется максимум в
192 пикселя, в панели выбора - в 64, а одинаковый размер метки при разных
полях даёт глифы разного визуального размера.

Скрипт делает три вещи: вырезает фон, если он есть, обрезает по рисунку и
вписывает в квадрат с одинаковым полем, сохраняет в двух размерах. Руками это
повторять не надо: следующая пачка иконок придёт так же.

Запуск:  python tools/emoji-prep.py [папка-с-исходниками]
"""
import os
import sys
from collections import deque

from PIL import Image, ImageFilter

# Куда и в каком размере. Большой размер читается в момент, когда глиф ставят
# на снимок, маленький - панелью выбора, которая показывает всю сетку сразу.
BIG = 256
SMALL = 64

# Поле вокруг рисунка, долей от стороны. Без него глифы с полями встык
# выглядят крупнее тех, у кого поля были.
PAD = 0.03

DEFAULT_SOURCE = r"C:\Users\xd\Desktop\icons"
ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "PrettyEyes.App", "Assets", "Emoji")

# Имя файла в папке исходников -> код глифа.
#
# Код юникодный там, где смысл совпал со стандартным эмодзи: он попадает в
# settings.json, и человек, у которого в настройках уже стоит 1f480, не должен
# после обновления остаться без своего черепа. Варианты одного и того же
# получают суффикс.
NAMES = {
    "17d22292-8a90-45cd-8620-1bdbe7f7df63.png": "1f435",      # обезьяна
    "22f8dced-9179-410f-8dc6-38004618a9ab.png": "1f62d",      # рыдает
    "8efe3380-b608-4270-8f34-f254d9cd8641.png": "1f61b",      # язык
    "ChatGPT Image 17 сент. 2026 г., 03_23_08 (2).png": "1f4a9",   # какашка
    "ChatGPT Image 17 сент. 2026 г., 03_23_08 (3).png": "1f47d",   # пришелец
    "ChatGPT Image 17 сент. 2026 г., 03_23_08 (4).png": "1f648",   # обезьяна закрыла глаза
    "ChatGPT Image 17 сент. 2026 г., 03_23_09 (5).png": "1f92e",   # тошнит
    "ChatGPT Image 17 сент. 2026 г., 03_23_09 (6).png": "1f635",   # спирали в глазах
    "ChatGPT Image 17 сент. 2026 г., 03_23_09 (7).png": "1f971",   # зевает
    "ChatGPT Image 17 сент. 2026 г., 03_23_09 (8).png": "1f44d",   # палец вверх
    "ChatGPT Image 17 сент. 2026 г., 03_23_09 (9).png": "1f64f",   # ладони
    "ChatGPT Image 17 сент. 2026 г., 03_23_10 (10).png": "1f44e",  # палец вниз
    "ChatGPT Image 17 сент. 2026 г., 03_23_27 (2).png": "1f602",   # смех со слезами
    "ChatGPT Image 17 сент. 2026 г., 03_23_27 (3).png": "1f602-2", # смех со слезами, вариант
    "ChatGPT Image 17 сент. 2026 г., 03_23_27 (4).png": "1f970",   # с сердечками
    "ChatGPT Image 17 сент. 2026 г., 03_23_28 (5).png": "1f60d",   # глаза-сердца
    "ChatGPT Image 17 сент. 2026 г., 03_23_28 (7).png": "1f60e",   # очки
    "ChatGPT Image 17 сент. 2026 г., 03_24_22 (1).png": "1f921",   # клоун
    "ChatGPT Image 17 сент. 2026 г., 03_24_22 (2).png": "1f61c",   # подмигивает с языком
    "ChatGPT Image 17 сент. 2026 г., 03_30_50 (2).png": "1f47f",   # чёрт
    "ChatGPT Image 17 сент. 2026 г., 03_30_50 (3).png": "1f622",   # плачет
    "ChatGPT Image 17 сент. 2026 г., 03_30_50 (4).png": "1fa77",   # розовое сердце
    "ChatGPT Image 17 сент. 2026 г., 03_30_51 (5).png": "1f97a",   # умоляет
    "ChatGPT Image 17 сент. 2026 г., 03_30_51 (6).png": "1f601",   # широкая улыбка
    "ce527f2b-b27b-4e48-99ba-4699e58e6b56.png": "1f921-2",    # клоун, вариант
    "db5e97d2-a73b-416f-9144-11c2fc5fa7f5.png": "1f61d",      # улыбка с языком
    "skull.png": "1f480",                                      # череп
    "skull2.png": "1f480-2",                                   # череп с блёстками
    "skull3.png": "1f480-3",                                   # череп простой
}


def cut_backdrop(img, dark=26):
    """Заливка от углов по тёмным и прозрачным пикселям.

    Часть исходников приходит на чёрном фоне вместо альфы. Заливка идёт только
    по околочёрному, поэтому тёмные детали внутри рисунка - зрачки, рот - её не
    пропускают: они окружены светлым.
    """
    w, h = img.size
    px = img.load()
    seen = bytearray(w * h)
    queue = deque([(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)])
    cleared = 0

    while queue:
        x, y = queue.popleft()

        if x < 0 or y < 0 or x >= w or y >= h:
            continue

        i = y * w + x

        if seen[i]:
            continue

        r, g, b, a = px[x, y]

        if a > 10 and max(r, g, b) > dark:
            continue

        seen[i] = 1

        if a > 0:
            px[x, y] = (r, g, b, 0)
            cleared += 1

        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    return cleared


def feather(img):
    """Сглаживает край после вырезания: срез по альфе даёт лесенку."""
    r, g, b, a = img.split()
    blurred = a.filter(ImageFilter.GaussianBlur(1.2))
    a = Image.composite(a, blurred, a.point(lambda v: 255 if v > 250 else 0))

    return Image.merge("RGBA", (r, g, b, a))


def fit_square(img, size):
    """Обрезает по рисунку и вписывает в квадрат с полем."""
    box = img.getbbox()

    if box is None:
        raise ValueError("картинка пустая")

    content = img.crop(box)
    cw, ch = content.size
    side = int(max(cw, ch) * (1 + PAD * 2))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(content, ((side - cw) // 2, (side - ch) // 2), content)

    return canvas.resize((size, size), Image.LANCZOS)


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SOURCE

    if not os.path.isdir(source):
        raise SystemExit(f"нет папки с исходниками: {source}")

    big_dir = os.path.abspath(ASSETS)
    small_dir = os.path.join(big_dir, "small")

    os.makedirs(big_dir, exist_ok=True)
    os.makedirs(small_dir, exist_ok=True)

    have = set(os.listdir(source))
    missing = [name for name in NAMES if name not in have]
    extra = [name for name in have if name.lower().endswith(".png") and name not in NAMES]

    if missing:
        raise SystemExit("в папке нет файлов из карты имён:\n  " + "\n  ".join(missing))

    total_big = 0
    total_small = 0

    for name, code in sorted(NAMES.items(), key=lambda pair: pair[1]):
        img = Image.open(os.path.join(source, name)).convert("RGBA")
        cleared = cut_backdrop(img)

        if cleared:
            img = feather(img)

        big_path = os.path.join(big_dir, f"{code}.png")
        small_path = os.path.join(small_dir, f"{code}.png")

        fit_square(img, BIG).save(big_path, optimize=True)
        fit_square(img, SMALL).save(small_path, optimize=True)

        total_big += os.path.getsize(big_path)
        total_small += os.path.getsize(small_path)

        note = f", вырезано фона {cleared} точек" if cleared else ""
        print(f"{code:10} <- {name[:44]}{note}")

    print()
    print(f"глифов: {len(NAMES)}")
    print(f"{BIG} px: {total_big / 1024:.0f} КБ, {SMALL} px: {total_small / 1024:.0f} КБ")

    if extra:
        print(f"в папке есть {len(extra)} файлов не из карты, они пропущены")


if __name__ == "__main__":
    main()
