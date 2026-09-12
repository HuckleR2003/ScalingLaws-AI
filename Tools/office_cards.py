# -*- coding: utf-8 -*-
"""The two office cards that have been named in the catalogue and missing from disk.

Cropped from the room's own snapshot rather than drawn: the card is a picture of the
place the player is about to move into, and a render of the actual room is the only
version of that which cannot go out of date when the room changes.

Sized to match office_house.png exactly (1072x460), because that one was resampled
once at the drawn size so mip 1 lands on it.
"""
import io
import os

from PIL import Image, ImageEnhance

ROOT = r"C:/Users/kemat/Desktop/HCK_Labs/ScalingLaws"
OUT = os.path.join(ROOT, "Assets/_ScalingLaws/Resources/Offices")

WIDTH, HEIGHT = 1072, 460
ASPECT = WIDTH / float(HEIGHT)


def card(source, target):
    im = Image.open(os.path.join(ROOT, "HubProof~", source)).convert("RGB")

    # The room, without the background it was rendered on. The clear colour is nearly
    # black, so anything above it is geometry.
    grey = im.convert("L")
    box = grey.point(lambda v: 255 if v > 26 else 0).getbbox()

    if box is None:
        raise SystemExit("nothing in " + source)

    left, top, right, bottom = box

    # Grow the crop to the card's aspect around the room's own centre, clamped to the
    # frame. Taller than the card, so the crop takes width rather than cutting the room.
    cx = (left + right) / 2.0
    cy = (top + bottom) / 2.0

    height = max(bottom - top, 1)
    width = height * ASPECT

    if width > im.width:
        width = im.width
        height = width / ASPECT

    left = max(0, min(im.width - width, cx - width / 2.0))
    top = max(0, min(im.height - height, cy - height / 2.0))

    crop = im.crop((int(left), int(top), int(left + width), int(top + height)))
    crop = crop.resize((WIDTH, HEIGHT), Image.LANCZOS)

    # A touch down, because the row lays a 0.62 scrim over this and the art rule is that
    # the picture sits under the interface rather than competing with it.
    crop = ImageEnhance.Brightness(crop).enhance(0.88)

    crop.save(os.path.join(OUT, target))

    tone = sum(crop.convert("L").resize((64, 28)).getdata()) / float(64 * 28)
    print("%-22s %dx%d  mean %.1f" % (target, WIDTH, HEIGHT, tone))


card("small_hub.png", "office_smallhub.png")
card("big_hub.png", "office_bighub.png")
