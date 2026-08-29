# Making the model creator look like a thing being built

Written 2026-08-29, after the author asked why the creator in Smartphone Tycoon reads as physical
and ours reads as a form.

## The diagnosis, and it is not "we need better art"

Their screens and ours have almost the same information on them. The difference is structural, and
it is three things:

**1. They always show the object. We never do.**

Every one of those screens has the phone, the laptop, the graphics card or the die **on the right,
at half the screen, all the time**, and it changes as you change settings. Ours has a readout. A
player choosing a CPU there is looking at a phone with a Snapdragon in it; a player choosing
precision here is looking at a number that went from 0.94 to 0.91.

**We already have the parts for this and they are not being used.** `ChipPreview` draws a die and is
currently a small plate beside the controls. `BrandMark` draws the company's mark. `PortraitStudio`
already proves this project can render a live 3D object into a UI panel and update it as choices
change. Nothing new has to be invented.

**2. Their categories are icons across the top. Ours are a rail of words.**

Look at the row in screenshots 2, 3 and 4: a drafting compass, a palette, sliders, tools, a camera,
a chip, a puzzle piece, a speaker, a box. Nine icons, one lit. You always know where you are and how
much is left. Our stage rail is text, so it reads as a wizard rather than as a workbench, and a
wizard implies there is a correct order and an end.

**3. Their outcomes are four permanent bars. Ours change per page.**

POWER, PERFORMANCE, GRAPHICS EXPERIENCE, ENERGY EFFICIENCY sit in the same place on every screen in
that game and move as you touch anything. That is the single strongest idea in those screenshots:
**you can see what a change did without having read the change.** Ours shows a delta against the
previous repricing, on some pages, in a banner.

Screenshot 6 is the one the author said "wins", and it is the same idea taken further: the readouts
are on the left, the object is in the middle, the controls are on the right, and the thing being
built is a die you are looking *at* rather than a form you are filling in.

---

## What I would build, in order, smallest first

### Step 1 · The permanent outcome bars

Four bars, same four, same place, every stage. They already exist as numbers.

Nothing about the simulation changes. This is `ModelCreatorPanel` taking the effect banner it
already has and making it a fixed column rather than a strip that changes with the page.

**Why first:** it is the cheapest of the three and it is the one that changes how the screen feels.
A slider that visibly moves four bars is a decision; a slider that changes a number in a paragraph
is a form field.

### Step 2 · Icons for the stages

Eight glyphs, flat single colour, 128x128, exactly the way `hud_news` and the research icons were
drawn with PIL rather than generated. The rail becomes a row of icons with the current one lit, and
the stage name moves under the icon.

**Why second:** it is art rather than architecture, it can be done in an afternoon, and it makes the
creator legible at a glance.

### Step 3 · The object, live, at half the screen

This is the real one and it is the most work.

The right half of every creator page shows **the model as a die**, drawn with `ChipPreview` at four
or five times its current size, and it responds:

| Stage | What visibly changes on the die |
|---|---|
| Branding | the company mark on the package, the die colour |
| Foundation | the architecture family as the pattern of blocks on the die |
| Scale | the die physically grows, and the block grid gets denser |
| Data | the corpus shows as bands of texture across the substrate |
| Compute | precision shows as the fineness of the grid; a throttled run runs warm |
| Safety | a border ring around the die, thicker with the tier |

Screenshot 6 is exactly this: a die with labelled regions and a temperature map over it. We already
draw a temperature map in the server room, on a grid, with the same colour ramp.

**None of it needs 3D.** `ChipPreview` is Painter2D, and everything in that table is a change to how
it draws. The world map, the time dial, the brand mark and the torn name plate in the tutorial are
all Painter2D for the same reason: it stays sharp, it needs no asset, and it cannot go out of step
with the data because it is drawn from the data.

### Step 4 · One photograph per stage behind the controls

The upgrade tiles already do this: `Art/Pages/Upgrade/*.jpg` processed to 660x320 with the fade
**baked into the alpha channel**, because USS has no gradients and no masks. Eight more pictures at
that treatment, one per stage, sitting behind the left column at low contrast.

Calibrate against the tile, not against the file. The first pass of the upgrade tiles was darkened
once for the art rule and again by the tile's own scrim and came out as eleven black rectangles.

---

## What I would not copy

- **Their toggle rows.** PORTRAIT MODE / NIGHT MODE as bare switches with no consequence shown is
  the thing this project's "(i)" cards exist to avoid. Every control here has to be able to say what
  its low end is for, and a control whose low end has nothing to say is a chore with a switch on it.
- **Their colour.** Those screens are magenta and orange throughout. This game has one accent used
  as slices of a single gradient, and the rule in `CLAUDE.md` is not to flood it with colour.
- **Sixteen resolution buttons.** Screenshot 4 offers ten photo resolutions. That is a list, not a
  decision.

---

## The honest cost

Step 1 is a day and changes the most. Step 2 is a day. Step 3 is the interesting one and it is
several days of `ChipPreview` work plus a proof render per stage, because every one of those visual
mappings has to be checked by looking at it. Step 4 depends on eight photographs existing.

**Step 3 has a real risk worth naming**: a die that changes with every control is a second place
that claims to describe the model, and if the mapping is even slightly wrong it will contradict the
bars. The rule that keeps it honest is the one this project already uses everywhere: the picture
must be drawn *from* the same blueprint the bars are computed from, never from a parallel set of
numbers kept beside it.
