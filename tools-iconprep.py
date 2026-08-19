"""Build the prettyeyes .ico from the squircle tile artwork."""
import os
import struct
from collections import deque
from io import BytesIO

from PIL import Image, ImageDraw, ImageFilter

OUT = r"C:\Users\xd\AppData\Local\Temp\claude\D--claude11\1f958b33-f218-4929-87d2-6667477bccb0\scratchpad"
TILE_SRC = r"D:\claude11\screenshotDEV\1787159874572-01a01b07-5840-70e2-a4d5-7f7269b06580.png"

TILE_BG = (44, 42, 48, 255)
IRIS_OUTER = (128, 46, 214, 255)
IRIS_INNER = (170, 88, 242, 255)
LID = (188, 188, 200, 255)


def cut_backdrop(img, dark=22):
    """
    The artwork sits on a pure black backdrop. Flood from the corners across
    near-black pixels only: the tile body is around #2E2E2E and survives.
    """
    w, h = img.size
    px = img.load()
    seen = bytearray(w * h)
    q = deque([(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)])
    cleared = 0

    while q:
        x, y = q.popleft()
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
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    return cleared


def feather(img):
    r, g, b, a = img.split()
    blurred = a.filter(ImageFilter.GaussianBlur(1.0))
    a = Image.composite(a, blurred, a.point(lambda v: 255 if v > 250 else 0))
    return Image.merge("RGBA", (r, g, b, a))


def fit_square(img, pad_ratio=0.015, out=1024):
    box = img.getbbox()
    content = img.crop(box)
    cw, ch = content.size
    side = int(max(cw, ch) * (1 + pad_ratio * 2))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(content, ((side - cw) // 2, (side - ch) // 2), content)
    return canvas.resize((out, out), Image.LANCZOS)


def small_tile(size=256):
    """
    Same squircle, but drawn for tiny sizes: the eye fills far more of the tile
    and every detail that would smear below 32px is gone. Keeps the silhouette
    of the big icon so the two never look like different apps.
    """
    s = size * 8
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Squircle body, near-full bleed - margins are what kill legibility here.
    inset = s * 0.02
    d.rounded_rectangle([inset, inset, s - inset, s - inset],
                        radius=s * 0.22, fill=TILE_BG)

    cx, cy = s / 2, s / 2
    half_w, half_h = s * 0.42, s * 0.27
    steps = 80

    def lens(sign):
        return [
            (cx - half_w + 2 * half_w * t / steps,
             cy + sign * half_h * (1 - (2 * t / steps - 1) ** 2))
            for t in range(steps + 1)
        ]

    outline = lens(-1) + lens(1)[::-1]

    d.polygon(outline, fill=(16, 14, 22, 255))
    d.line(outline + [outline[0]], fill=LID, width=max(2, int(s * 0.035)), joint="curve")

    ir = half_h * 0.95
    d.ellipse([cx - ir, cy - ir, cx + ir, cy + ir], fill=IRIS_OUTER)
    d.ellipse([cx - ir * 0.78, cy - ir * 0.78, cx + ir * 0.78, cy + ir * 0.78],
              fill=IRIS_INNER)

    pr = ir * 0.44
    d.ellipse([cx - pr, cy - pr, cx + pr, cy + pr], fill=(8, 6, 14, 255))

    hr = ir * 0.20
    hx, hy = cx - ir * 0.32, cy - ir * 0.34
    d.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=(255, 255, 255, 240))

    return img.resize((size, size), Image.LANCZOS)


def write_ico(path, entries):
    blobs = []
    for size, im in entries:
        buf = BytesIO()
        im.resize((size, size), Image.LANCZOS).save(buf, format="PNG")
        blobs.append((size, buf.getvalue()))

    with open(path, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(blobs)))
        offset = 6 + 16 * len(blobs)
        for size, data in blobs:
            dim = 0 if size >= 256 else size
            f.write(struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset))
            offset += len(data)
        for _, data in blobs:
            f.write(data)


def sheet(rows, path):
    sizes = [256, 128, 64, 48, 32, 24, 20, 16]
    cell = 290
    label_w = 60
    head = 30
    img = Image.new("RGB", (label_w + cell * len(sizes), head + cell * len(rows)),
                    (245, 245, 247))
    d = ImageDraw.Draw(img)

    for i, size in enumerate(sizes):
        d.text((label_w + i * cell + 8, 8), f"{size}px", fill=(70, 70, 75))

    y = head
    for bg, pick in rows:
        d.rectangle([0, y, img.width, y + cell], fill=bg)
        for i, size in enumerate(sizes):
            icon = pick(size).resize((size, size), Image.LANCZOS)
            img.paste(icon, (label_w + i * cell + (cell - size) // 2,
                             y + (cell - size) // 2), icon)
        y += cell

    img.save(path)


tile = Image.open(TILE_SRC).convert("RGBA")
print(f"source: {tile.size}, backdrop cleared: {cut_backdrop(tile)}")
big = fit_square(feather(tile))
big.save(os.path.join(OUT, "tile-big.png"))

# User's call: the original artwork at every size, no redrawn variant.
# Below ~24px the eye inside the tile is barely more than a purple dot, which
# was the tradeoff accepted knowingly.
pick = lambda size: big

write_ico(os.path.join(OUT, "prettyeyes.ico"),
          [(s, pick(s)) for s in (256, 128, 64, 48, 32, 24, 20, 16)])

sheet([((245, 245, 247), pick), ((18, 18, 20), pick)],
      os.path.join(OUT, "tile-preview.png"))
print("done")
