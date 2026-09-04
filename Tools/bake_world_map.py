# -*- coding: utf-8 -*-
"""Bakes the Natural Earth country outlines into the asset the game draws.

    python Tools/bake_world_map.py

Reads  Assets/_ScalingLaws/Resources/Map/ne_110m_admin_0_countries.json  (public domain)
Writes Assets/_ScalingLaws/Resources/Map/world.bytes

FORMAT (little-endian, version 2)
---------------------------------
    int32   magic 'SLMP'
    int32   version
    float32 aspect          height of the map when its width is 1
    int32   country count
    per country:
        int16   Country enum value, 0 when it is only scenery
        uint8   WorldRegion enum value, 0 when it is in none of the three
        uint8   name length, then that many UTF-8 bytes
        float32 pin x, pin y      area centroid of the largest ring
        int16   ring count
        per ring:
            int16 point count, then that many float32 x, y pairs

WHY THIS IS BAKED RATHER THAN PARSED AT RUNTIME
-----------------------------------------------
The source is 725 kB of GeoJSON with arbitrarily nested coordinate arrays, which `JsonUtility`
cannot express at all: it has no representation for `number[][][][]`. Writing a JSON parser into the
runtime to read a file that has not changed since 2012 would be a parser to maintain, a parse to pay
for on every load, and a second place for the projection to live.

The output is a flat little-endian binary the reader walks in one pass. Roughly 90 kB, no allocation
per point beyond the arrays themselves.

THE PROJECTION IS ROBINSON
--------------------------
Equirectangular is two lines of code and makes Canada and Russia enormous, which on a map whose whole
job is "pick where the company is registered" quietly says those are the important places. Robinson
is the compromise every atlas settled on: nothing is right, nothing is badly wrong, and it is the
shape people recognise as "a world map".

Antarctica is dropped. It is 661 points, six per cent of the file, it is a white band along the
bottom edge at every projection, and nobody is registering a company there.

TWO QUIRKS IN THE SOURCE, BOTH HANDLED EXPLICITLY
-------------------------------------------------
1. **France and Norway carry `ISO_A3` of `-99`.** This is a known Natural Earth quirk, not corrupt
   data: the field is disputed for territories, so those two fall back to `ADM0_A3`. Reading only
   `ISO_A3` silently loses France, which is one of the sixteen countries the game offers.
2. **Singapore is not in the 110m file at all.** It is genuinely too small for that resolution. It
   gets a hand-placed pin from its real coordinates, and so does anything else in the sixteen that
   turns out to be missing: the list is checked at the end and the bake fails loudly rather than
   shipping a country nobody can click.
"""
import io
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MAP = os.path.join(ROOT, "Assets", "_ScalingLaws", "Resources", "Map")
SRC = os.path.join(MAP, "ne_110m_admin_0_countries.json")
OUT = os.path.join(MAP, "world.bytes")

MAGIC = 0x504D4C53  # 'SLMP'
VERSION = 2

# ---- the sixteen the game offers ----------------------------------------------------------------
# Values are `ScalingLaws.Data.Country`. They are written into saves, so they are the identity here
# too, and a rename in C# without a rename here shows up as a country that cannot be picked.
AMERICA, EUROPE, ASIA = 1, 2, 3

PLAYABLE = {
    "USA": (1, AMERICA), "CAN": (2, AMERICA), "BRA": (3, AMERICA), "MEX": (4, AMERICA),
    "GBR": (10, EUROPE), "DEU": (11, EUROPE), "FRA": (12, EUROPE), "POL": (13, EUROPE),
    "IRL": (14, EUROPE), "CHE": (15, EUROPE),
    "JPN": (20, ASIA), "KOR": (21, ASIA), "TWN": (22, ASIA), "SGP": (23, ASIA),
    "IND": (24, ASIA), "CHN": (25, ASIA),
}

# Anything in PLAYABLE with no polygon in the source. Real coordinates, so the pin lands where the
# place actually is rather than where it would look tidy.
MISSING_PINS = {
    "SGP": (103.82, 1.35),
}

# Which drawn countries belong to which region, so selecting a region can light its whole
# neighbourhood rather than only the four names the player can pick. Natural Earth's own CONTINENT
# field, mapped: the game has three regions and the world has six populated continents.
CONTINENT_REGION = {
    "North America": AMERICA,
    "South America": AMERICA,
    "Europe": EUROPE,
    "Asia": ASIA,
}

DROP = {"ATA"}  # Antarctica


# ---- Robinson ------------------------------------------------------------------------------------
# The published table, at five degree steps. Interpolated linearly between rows, which is what every
# implementation does and is well inside the error of 110m outlines.
ROBINSON_X = [1.0000, 0.9986, 0.9954, 0.9900, 0.9822, 0.9730, 0.9600, 0.9427, 0.9216,
              0.8962, 0.8679, 0.8350, 0.7986, 0.7597, 0.7186, 0.6732, 0.6213, 0.5722, 0.5322]
ROBINSON_Y = [0.0000, 0.0620, 0.1240, 0.1860, 0.2480, 0.3100, 0.3720, 0.4340, 0.4958,
              0.5571, 0.6176, 0.6769, 0.7346, 0.7903, 0.8435, 0.8936, 0.9394, 0.9761, 1.0000]


def robinson(lon, lat):
    """Longitude/latitude in degrees to Robinson x/y in arbitrary but consistent units."""
    sign = -1.0 if lat < 0 else 1.0
    a = min(abs(lat), 90.0)

    i = int(a / 5.0)
    if i >= 18:
        i, t = 17, 1.0
    else:
        t = (a - i * 5.0) / 5.0

    xs = ROBINSON_X[i] + (ROBINSON_X[i + 1] - ROBINSON_X[i]) * t
    ys = ROBINSON_Y[i] + (ROBINSON_Y[i + 1] - ROBINSON_Y[i]) * t

    return 0.8487 * xs * math.radians(lon), 1.3523 * ys * sign


# ---- simplification ------------------------------------------------------------------------------
def douglas_peucker(points, epsilon):
    """Classic. Keeps the shape of a coastline while dropping the points nobody can see."""
    if len(points) < 3:
        return points

    ax, ay = points[0]
    bx, by = points[-1]

    dx, dy = bx - ax, by - ay
    span = math.hypot(dx, dy)

    worst, at = -1.0, 0

    for index in range(1, len(points) - 1):
        px, py = points[index]

        if span < 1e-12:
            d = math.hypot(px - ax, py - ay)
        else:
            d = abs(dy * px - dx * py + bx * ay - by * ax) / span

        if d > worst:
            worst, at = d, index

    if worst <= epsilon:
        return [points[0], points[-1]]

    left = douglas_peucker(points[:at + 1], epsilon)
    right = douglas_peucker(points[at:], epsilon)

    return left[:-1] + right


def ring_area(points):
    """Twice the signed area. Used to find a country's main body, never for anything exact."""
    total = 0.0
    for index in range(len(points)):
        x0, y0 = points[index]
        x1, y1 = points[(index + 1) % len(points)]
        total += x0 * y1 - x1 * y0
    return abs(total) * 0.5


def centroid(points):
    """Area centroid of a ring, which is where a pin belongs. Falls back to the mean for slivers."""
    cx = cy = area = 0.0

    for index in range(len(points)):
        x0, y0 = points[index]
        x1, y1 = points[(index + 1) % len(points)]
        cross = x0 * y1 - x1 * y0
        area += cross
        cx += (x0 + x1) * cross
        cy += (y0 + y1) * cross

    if abs(area) < 1e-12:
        return (sum(p[0] for p in points) / len(points),
                sum(p[1] for p in points) / len(points))

    area *= 0.5
    return cx / (6.0 * area), cy / (6.0 * area)


def main():
    # Douglas-Peucker recurses once per point in the worst case and Canada's outline is 794 points,
    # which is close enough to the default limit to be worth not finding out about at 2am.
    sys.setrecursionlimit(10000)

    if not os.path.exists(SRC):
        sys.exit("no source at " + SRC)

    data = json.load(io.open(SRC, encoding="utf-8"))

    countries = []
    seen_playable = set()

    for feature in data["features"]:
        props = feature["properties"]

        # ISO_A3 is "-99" for France and Norway, which is a documented quirk rather than bad data.
        iso = props.get("ISO_A3") or ""
        if not iso or iso.startswith("-"):
            iso = props.get("ADM0_A3") or ""

        if iso in DROP:
            continue

        name = props.get("ADMIN") or props.get("NAME") or iso
        member, region = PLAYABLE.get(iso, (0, 0))

        if member:
            seen_playable.add(iso)
        else:
            region = CONTINENT_REGION.get(props.get("CONTINENT") or "", 0)

        geometry = feature["geometry"]
        polygons = (geometry["coordinates"] if geometry["type"] == "MultiPolygon"
                    else [geometry["coordinates"]])

        rings = []

        for polygon in polygons:
            # Only the outer ring of each part. Holes at this scale are two lakes and a border
            # enclave, and drawing them would need a fill rule the stroke then has to agree with.
            outer = polygon[0]

            projected = [robinson(lon, lat) for lon, lat in outer]

            # Epsilon is in projected units, where the whole world is about 5.1 wide. 0.004 is
            # roughly a pixel at the size this is drawn and takes about a third of the points.
            simplified = douglas_peucker(projected, 0.004)

            if len(simplified) >= 3:
                rings.append(simplified)

        if not rings:
            continue

        rings.sort(key=ring_area, reverse=True)
        countries.append({"iso": iso, "name": name, "member": member,
                          "region": region, "rings": rings})

    # ---- anything playable with no shape gets a pin and says so ---------------------------------
    for iso, (member, region) in PLAYABLE.items():
        if iso in seen_playable:
            continue

        if iso not in MISSING_PINS:
            sys.exit("%s is one of the sixteen, is not in the source, and has no hand-placed pin. "
                     "Add one to MISSING_PINS rather than shipping a country nobody can click."
                     % iso)

        lon, lat = MISSING_PINS[iso]
        x, y = robinson(lon, lat)

        # A small diamond, so it is a real shape the same code can fill, stroke and hit-test. A
        # special case for "countries with no polygon" would be a second drawing path forever.
        r = 0.030
        countries.append({
            "iso": iso, "name": iso, "member": member, "region": region,
            "rings": [[(x, y - r), (x + r, y), (x, y + r), (x - r, y)]],
        })

    # ---- normalise to 0..1, y down ---------------------------------------------------------------
    xs = [p[0] for c in countries for r in c["rings"] for p in r]
    ys = [p[1] for c in countries for r in c["rings"] for p in r]

    lo_x, hi_x = min(xs), max(xs)
    lo_y, hi_y = min(ys), max(ys)

    # **One scale for both axes, and the asset carries the shape it came out as.** Normalising each
    # axis to its own range would stretch the world and make the projection pointless; normalising
    # both by the longer one puts a 1.9:1 map inside a 1:1 box, which is what the first bake did and
    # it drew the world across the middle third of an empty square. So x runs 0..1, y runs 0..aspect,
    # and the element sizes itself to the number in the header.
    span = hi_x - lo_x
    aspect = (hi_y - lo_y) / span

    def place(point):
        x, y = point
        return ((x - lo_x) / span,
                # Robinson's y grows north; screens grow down.
                aspect - (y - lo_y) / span)

    blob = bytearray()
    blob += struct.pack("<ii", MAGIC, VERSION)
    blob += struct.pack("<f", aspect)
    blob += struct.pack("<i", len(countries))

    points_written = 0

    for country in countries:
        name = country["name"].encode("utf-8")[:60]

        blob += struct.pack("<hB", country["member"], country["region"])
        blob += struct.pack("<B", len(name)) + name

        pin = place(centroid(country["rings"][0]))
        blob += struct.pack("<ff", pin[0], pin[1])

        blob += struct.pack("<h", len(country["rings"]))

        for ring in country["rings"]:
            blob += struct.pack("<h", len(ring))
            for point in ring:
                x, y = place(point)
                blob += struct.pack("<ff", x, y)
                points_written += 1

    io.open(OUT, "wb").write(blob)

    playable = sum(1 for c in countries if c["member"])

    print("wrote %s" % OUT)
    print("  %d countries, %d of them playable, %d points, %.1f kB, aspect %.4f"
          % (len(countries), playable, points_written, len(blob) / 1024.0, aspect))

    if playable != len(PLAYABLE):
        sys.exit("expected %d playable countries, wrote %d" % (len(PLAYABLE), playable))


if __name__ == "__main__":
    main()
