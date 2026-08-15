# Art the game is waiting on

What the code already loads and cannot find. Every one of these has a graceful fallback, so nothing
here is a crash: the game draws a plate, a caption, or a word instead. That is the point of the list.
None of it blocks play, all of it is visible.

Sorted by how much it costs to look at.

---

## 1. The accelerator parts, for the COMPUTE stage

**This is the one that unblocks a mechanic rather than tidying one.** The COMPUTE stage of the model
creator is a single rented-petaflops slider today, and the design agreed for it is a visual choice of
which silicon the run is planned around, headlined by its memory. Real parts, real photographs, a
strong header. The code cannot be built to draw pictures that nobody has specified, so these come
first.

Put them in `Assets/_ScalingLaws/Resources/Silicon/`, named after the generation:

| File | Part | Memory it carries |
|---|---|---|
| `silicon_a100.png` | data centre accelerator, 2020 generation | HBM2E, 80 GB |
| `silicon_h100.png` | data centre accelerator, 2022 generation | HBM3, 80 GB |
| `silicon_h200.png` | data centre accelerator, 2024 generation | HBM3E, 141 GB |
| `silicon_b200.png` | data centre accelerator, 2024 generation | HBM3E, 192 GB |
| `silicon_next.png` | one generic "not shipped yet" board | for projection entries |

**Shoot or render them as parts, not as products.** A board on a dark surface, three quarter view,
the heatsink and the memory stacks visible. No vendor logos and no product names in the image: the
game uses parody names for labs and the same caution applies to silicon.

**1024 x 640, and the art direction rule applies**: these sit under a card with white text on them,
so evenly dark, low internal contrast, nothing bright near the edges. The test is to put white
uppercase over the top-left corner and check it still reads.

## 2. The lab marks, and how a supplied logo becomes a badge

The author's four logos live in `Art/Logos/` and the badges the game loads are generated from them
into `Resources/Labs/`. **Re-run `scratchpad/badges.py` if a logo is replaced**, because two things
happen in that step and neither is optional:

- **The wordmark is cropped off.** The ranking badge is 42x42 and a name under a symbol is a grey
  smear at that size. The split is found, not assumed: the first band of empty rows that still
  leaves most of the artwork above it. Assuming a fraction cut the S mark in half, because that one
  has no wordmark and its widest internal gap is inside the letter itself.
- **Dark marks are lifted.** The badge is six percent white over black, so the near-black A and the
  navy G read as holes at their drawn values. Only value moves, toward white, which keeps the hue
  and the orange and peach accents. A multiply drives the orange straight to yellow.

A logo drawn light, as a symbol with no wordmark, needs neither step.

## 3. Done, kept here as the reference for the next set

`hud_news.png` and `hud_mail.png` are drawn and in. They were the only two of fifteen slots without
art, which read as broken rather than unfinished.

They are flat single colour silhouettes in `rgb(221, 231, 245)` at 128x128 on transparent, which is
what the thirteen that already existed are. `Resources/Hud/hud_business.png` is the reference. If you
redraw them, keep that: the bar draws them at 44x44 and detail below about three pixels disappears.

`research_sharding.png`, `research_pipeline.png` and `research_ultrareadiness.png` are drawn and in
too, matched to the twenty two existing research icons: dark ink line work in a ring, 300x300,
transparent. They are honest placeholders. If you want to redraw them, they are the three rungs of
the parameter ceiling and they should read as: **one block of state cut into four and handed out**,
**four stages in a chain**, and **a run that survives losing machines**.

## 4. Two of the three places have no photograph

`Assets/_ScalingLaws/Resources/Offices/`

| File | Place | Draws instead |
|---|---|---|
| `office_smallhub.png` | LVL 1, Small office hub, 8 desks, $210k/mo | "PHOTOGRAPH OF THIS PLACE GOES HERE" |
| `office_bighub.png` | LVL 2, Big company hub, 20 desks, $300k/mo | the same caption |

`office_house.png` (LVL 0) is done and is the reference for how to prepare one:

- **2.33:1**, output at **1072x460**. The chooser draws the picture at 40% of the row, and 1072 is
  exactly twice that, so mip 1 lands on the drawn size instead of being resampled twice.
- One Lanczos pass from the original, then a light unsharp. Two lossy steps is what made the tab
  banners blurry and it took a while to find.
- Crop for the middle of the room. The empty floor at the bottom and the void above the ceiling are
  what the crop is for getting rid of.

The obvious way to make these is the same way `office0.png` was made: build the room, point the
orthographic camera at it, and render.

## 5. Nice to have, not missing

- The world map. `UI/WorldMapElement.cs` draws coarse polygons with `Painter2D` and is deliberately
  not a texture, so there is nothing missing here. If it is ever replaced with a real map, an
  equirectangular Natural Earth render at `Art/Ui/worldmap.png` is the thing to start from.
- `newmodel_2.png` is a duplicate of a card that is already in use. Nothing loads it. It is the
  author's call whether to delete it or swap it in.

---

## The rule these all follow

`UI/PageArt.cs` and every loader beside it return null rather than throwing, and every caller draws
something usable when they do. **Do not add a loader that assumes the file is there.** A screen that
fails to render over a piece of art that has not been drawn yet is a screen that cannot be worked on
until the art exists, and that is the wrong way round.
