# -*- coding: utf-8 -*-
"""The six glyphs that say what a research node gives you.

Drawn rather than generated, the same way hud_news and the three scaling research
icons were: flat single-colour silhouettes, supersampled and resampled once, so they
stay sharp at the 18 pixels they are actually drawn at.

One glyph per KIND of unlock, not per node. There are 59 nodes and six kinds, and the
kind is what a player needs to read at a glance: this one opens a corpus, that one
opens a family, the third raises the ceiling. The node's own name is written beside it.
"""
import io
import os

from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "Assets", "_ScalingLaws", "Resources", "Unlocks")

SIZE = 64
SS = 8                      # supersample, resampled once at the end
INK = (226, 234, 248, 255)


def canvas():
    return Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))


def finish(image, name):
    out = image.resize((SIZE, SIZE), Image.LANCZOS)
    out.save(os.path.join(OUT, name + ".png"))
    print(name)


def architecture():
    """A family of architectures: three nodes joined into a shape."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS
    top = (32 * s, 12 * s)
    left = (14 * s, 46 * s)
    right = (50 * s, 46 * s)

    for a, b in ((top, left), (top, right), (left, right)):
        d.line([a, b], fill=INK, width=3 * s)

    for point in (top, left, right):
        r = 7 * s
        d.ellipse([point[0] - r, point[1] - r, point[0] + r, point[1] + r], fill=INK)

    finish(im, "unlock_architecture")


def corpus():
    """A corpus: a stack of sheets."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS

    for index, top in enumerate((14, 27, 40)):
        inset = index * 3
        d.rounded_rectangle(
            [(12 + inset) * s, top * s, (52 - inset) * s, (top + 10) * s],
            radius=2 * s, outline=INK, width=3 * s)

    finish(im, "unlock_corpus")


def tier():
    """A compute tier: a rack of three."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS

    d.rounded_rectangle([14 * s, 10 * s, 50 * s, 54 * s], radius=4 * s,
                        outline=INK, width=3 * s)

    for y in (20, 32, 44):
        d.line([(21 * s, y * s), (43 * s, y * s)], fill=INK, width=3 * s)

    finish(im, "unlock_tier")


def upgrade():
    """An upgrade line: an arrow going up a step."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS

    d.line([(14 * s, 48 * s), (28 * s, 48 * s), (28 * s, 32 * s),
            (44 * s, 32 * s), (44 * s, 20 * s)], fill=INK, width=4 * s, joint="curve")

    d.polygon([(44 * s, 10 * s), (53 * s, 24 * s), (35 * s, 24 * s)], fill=INK)

    finish(im, "unlock_upgrade")


def model_type():
    """A kind of model: a die with a core in it."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS

    d.rounded_rectangle([16 * s, 16 * s, 48 * s, 48 * s], radius=5 * s,
                        outline=INK, width=3 * s)

    d.rounded_rectangle([26 * s, 26 * s, 38 * s, 38 * s], radius=2 * s, fill=INK)

    for offset in (22, 32, 42):
        d.line([(offset * s, 8 * s), (offset * s, 16 * s)], fill=INK, width=3 * s)
        d.line([(offset * s, 48 * s), (offset * s, 56 * s)], fill=INK, width=3 * s)
        d.line([(8 * s, offset * s), (16 * s, offset * s)], fill=INK, width=3 * s)
        d.line([(48 * s, offset * s), (56 * s, offset * s)], fill=INK, width=3 * s)

    finish(im, "unlock_type")


def ceiling():
    """A ceiling lifted: two bars moving apart."""
    im = canvas()
    d = ImageDraw.Draw(im)
    s = SS

    d.line([(14 * s, 16 * s), (50 * s, 16 * s)], fill=INK, width=4 * s)
    d.line([(14 * s, 48 * s), (50 * s, 48 * s)], fill=INK, width=4 * s)

    d.line([(32 * s, 24 * s), (32 * s, 40 * s)], fill=INK, width=3 * s)
    d.polygon([(32 * s, 20 * s), (39 * s, 30 * s), (25 * s, 30 * s)], fill=INK)
    d.polygon([(32 * s, 44 * s), (39 * s, 34 * s), (25 * s, 34 * s)], fill=INK)

    finish(im, "unlock_ceiling")


if not os.path.isdir(OUT):
    os.makedirs(OUT)

architecture()
corpus()
tier()
upgrade()
model_type()
ceiling()
