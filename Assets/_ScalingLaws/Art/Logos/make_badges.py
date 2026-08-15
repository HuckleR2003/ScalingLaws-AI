"""Turn the author's four logos into ranking badges.

Two things have to happen and both are forced by where the art is drawn.

**The badge is 42x42.** A wordmark under a symbol is unreadable at that size and turns into a grey
smear, so the symbol is cropped out on its own. The split is found rather than assumed: the first
horizontal band of empty rows that still leaves most of the artwork above it is where the wordmark
starts. Guessing a fraction would have cut the S mark in half, because that one has no wordmark at
all and its widest internal gap is inside the letter.

**Two of the four are dark by design**: a near-black A with an orange accent, and a navy G with a
peach swoosh. The badge is six percent white over black, so both would read as a hole. Value is
lifted where it is too dark and only value, so hue and the accent colours come through. Same idea as
the skill icons, which are re-tinted rather than redrawn.
"""

import numpy as np
from PIL import Image

SRC = "C:/Users/kemat/Desktop/HCK_Labs/ScalingLaws/Assets/_ScalingLaws/Art/Logos/"
DST = "C:/Users/kemat/Desktop/HCK_Labs/ScalingLaws/Assets/_ScalingLaws/Resources/Labs/"

# Below this a mark disappears into the badge. The blue infinity sits just over it and is left alone.
MIN_MEAN_LUMA = 96.0

# Where a lifted mark lands. High enough to read, low enough to keep its own internal contrast.
TARGET_LUMA = 152.0


def empty_bands(rows, top, bottom, least=6):
    """Runs of blank rows inside the occupied range, in order."""
    bands, start, run = [], None, 0
    for y in range(top, bottom + 1):
        if rows[y] == 0:
            start = y if run == 0 else start
            run += 1
        else:
            if run >= least:
                bands.append((start, run))
            run = 0

    if run >= least:
        bands.append((start, run))

    return bands


def symbol_only(im):
    """The mark without its wordmark."""
    a = np.array(im)
    rows = (a[:, :, 3] > 24).sum(axis=1)
    filled = np.where(rows > 0)[0]
    if len(filled) == 0:
        return im

    top, bottom = int(filled[0]), int(filled[-1])
    height = bottom - top

    # The first band that still leaves most of the artwork above it. Taking the widest band instead
    # picked the gap under the wordmark on one of these and kept the text.
    for start, _ in empty_bands(rows, top, bottom):
        if height > 0 and (start - top) / height >= 0.40:
            bottom = start - 1
            break

    cols = (a[top:bottom + 1, :, 3] > 24).sum(axis=0)
    used = np.where(cols > 0)[0]
    left, right = (int(used[0]), int(used[-1])) if len(used) else (0, a.shape[1] - 1)

    return im.crop((left, top, right + 1, bottom + 1))


def luma_of(a, mask):
    return (0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2])[mask].mean()


def lift(im, name):
    """Raise value until the mark reads on a dark badge. Hue and saturation are untouched."""
    a = np.array(im).astype(np.float32)
    solid = a[:, :, 3] > 90
    if not solid.any():
        return im

    mean = luma_of(a, solid)
    if mean >= MIN_MEAN_LUMA:
        print(f"      {name}: luma {mean:.0f}, left alone")
        return im

    # Toward white by a fraction chosen to land on TARGET_LUMA. Blending toward white rather than
    # multiplying keeps the accent colours: a multiply drives the orange straight to yellow.
    towards = min(0.62, (TARGET_LUMA - mean) / max(1.0, 255.0 - mean))
    a[:, :, :3] += (255.0 - a[:, :, :3]) * towards
    print(f"      {name}: luma {mean:.0f} -> {luma_of(a, solid):.0f}")

    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def square(im, size=256, pad=0.08):
    inner = int(size * (1.0 - pad * 2))
    scale = min(inner / im.width, inner / im.height)
    small = im.resize((max(1, int(im.width * scale)), max(1, int(im.height * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(small, ((size - small.width) // 2, (size - small.height) // 2), small)
    return out


for source, target in [
    ("logo_stableai", "lab_stableai"),
    ("logo_introduceAI", "lab_introduceai"),
    ("logo_alghoalpha", "lab_alghoalpha"),
    ("logo_gohere", "lab_gohere"),
]:
    im = Image.open(SRC + source + ".png").convert("RGBA")
    mark = symbol_only(im)
    print(f"{source} {im.size} -> symbol {mark.size}")
    square(lift(mark, target)).save(DST + target + ".png")

sheet = Image.new("RGB", (600, 210), (13, 15, 20))
for i, n in enumerate(["lab_stableai", "lab_introduceai", "lab_alghoalpha", "lab_gohere",
                       "lab_antropic"]):
    im = Image.open(DST + n + ".png").convert("RGBA")
    big = im.resize((100, 100), Image.LANCZOS)
    sheet.paste(big, (12 + i * 116, 18), big)

    plate = Image.new("RGBA", (42, 42), (28, 30, 36, 255))
    small = im.resize((42, 42), Image.LANCZOS)
    plate.paste(small, (0, 0), small)
    sheet.paste(plate, (41 + i * 116, 140))

sheet.save("C:/Users/kemat/AppData/Local/Temp/claude/C--Users-kemat-Desktop-HCK-Labs/"
           "6823fa3d-3d38-4e65-ad3b-0faa7ea98566/scratchpad/badges.png")
print("contact sheet written")
