# Art the game is waiting on

What the code already loads and cannot find. Every one of these has a graceful fallback, so nothing
here is a crash: the game draws a plate, a caption, or a word instead. That is the point of the list.
None of it blocks play, all of it is visible.

Sorted by how much it costs to look at.

---

## 1. Two tabs on the bottom bar have no icon

`Assets/_ScalingLaws/Resources/Hud/`

| File | Slot | Draws instead |
|---|---|---|
| `hud_news.png` | NEWS | an empty grey plate with the word under it |
| `hud_mail.png` | @ MAIL | an empty grey plate with the word under it |

**This is the loudest one.** Thirteen of the fifteen categories have an icon and two do not, so the
two that do not read as broken rather than as unfinished. They also happen to be the two newest
screens, which is exactly the wrong impression to give.

Match the existing thirteen: single neon glyph, no plate, no text, transparent background, drawn to
be legible at **44x44** because that is the size the bar draws it at. `Resources/Hud/hud_site.png` is
the reference.

## 2. Two of the three places have no photograph

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

## 3. Nice to have, not missing

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
