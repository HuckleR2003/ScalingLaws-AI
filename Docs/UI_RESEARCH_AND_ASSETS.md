# UI research and asset list

Research done 2026-08-02 against Devices Tycoon, Smartphone Tycoon and Mobiles Tycoon, plus the two
reference screenshots. This file is the brief for whoever makes the art.

---

## What those games actually do

**Devices Tycoon** builds the OS in stages: pick a name and a logo, employees develop a base
version, then design elements and technologies get implemented on top. Its stated design tension is
the one worth stealing: "the more complex your animations and styles are, the more system resources
they will require, so you must keep a balance between visual appeal and efficiency." Every visual
upgrade has a performance cost. Our Efficiency and Latency traits are that same idea.

**Smartphone Tycoon** is where the reference screenshot comes from: an eleven card OS upgrade grid,
each card a photo with a dark overlay, an uppercase title, a LEVEL line and a price, and a separate
RATING OS tab. Left rail with two entries, outlined UPGRADE button pinned bottom left.

**The office screen** in the first screenshot: money and a soft currency top left, company name and
in-game date top right, a row of round icon buttons along the bottom, a counter bottom right.

## What the reviews say to avoid

These come from user reviews of Smartphone Tycoon and they are the most useful part of the research:

| Complaint | What we did about it |
|---|---|
| "gives phones you designed random ratings rather than take into account any of the specs" | `RankingBoard.Score` is a fixed function of capability, share and brand, and each of those is computed by the same code the economy runs on. There is no separate rating number to drift. A test pins this. |
| "you can't really make a phone series like budget line, middle class and premium" | Price is per model (`DeployedModel.PriceMultiplier`), and several models can be live at once, each with its own price and trait levels. A cheap high volume model alongside a premium one is a supported strategy, not a workaround. |
| "working on your own OS feels like it is taking way too long" | Trait upgrades are 11 to 30 days at level 0, and the grid always shows days and cost before you commit. The long pole is deliberately the training run, not the upgrade. |

---

## Screens built so far

| Screen | Class | State |
|---|---|---|
| New model (the creator) | `ModelCreatorPanel` | Done. Live projection panel, log sliders, data toggles, rented compute slider. |
| Upgrade model | `UpgradeGridPanel` | Done. Eleven cards, level against market par, red left border when behind. |
| Release | `GameShell.BuildReleaseScreen` | Done, basic. Shelf cards showing what a model would ship at today. |
| Funding | `GameShell.BuildFundingScreen` | Done, basic. Valuation, term sheet, sign button. |
| Ranking | `GameShell.BuildRankingScreen` | Done, basic. Sorted board. |
| Intelligence | `GameShell.BuildFeedScreen` | Done, basic. Tier picker and note feed. |
| Office / world view | not built | The isometric room from the first screenshot. Needs art direction first. |
| Compute and hardware | not built | Buy, sell, tier ladder. The simulation is complete; only the screen is missing. |

All structure is C# against `Assets/_ScalingLaws/UI/ScalingLaws.uss`. Every colour, radius and size
is a token at the top of that file. Restyling is a stylesheet edit.

---

## Icons needed

Flat line icons, single colour, meant to be tinted white at 70 to 100 percent opacity. 64x64 SVG or
PNG at 2x. One set, one weight, one corner radius.

**Bottom toolbar, round buttons (44px)**

1. `pause` - two vertical bars
2. `play` - single triangle
3. `play-fast` - double triangle
4. `play-turbo` - triple triangle or triangle with trailing lines
5. `save` - floppy or downward arrow into tray

**Left rail, one per screen (24px)**

6. `model-new` - a node graph or a chip with branching lines
7. `model-upgrade` - upward chevron stack
8. `release` - a rocket or an outward arrow from a box
9. `funding` - a bank column or a rising bar with a dollar
10. `ranking` - a podium or a sorted bar chart
11. `intelligence` - a radar sweep or an eye over a chart
12. `compute` - a server rack
13. `office` - an isometric room outline

**Inline, next to numbers (16 to 20px)**

14. `cash` - dollar sign in a circle
15. `valuation` - a diamond or an upward trend line
16. `capability` - a brain or a rising step curve
17. `market-share` - a pie slice
18. `brand` - a star
19. `tokens` - three stacked chevrons or a text cursor
20. `petaflop` - a lightning bolt
21. `power-kw` - a plug
22. `memory` - a RAM stick outline
23. `clock` - a clock face, for lead times and durations
24. `warning-behind` - triangle with an exclamation, for the "N BEHIND" badge
25. `locked` - a padlock, for gated tiers and unavailable traits
26. `projection` - a dashed circle or a question mark in a circle. **This one matters.** It marks
    every estimated number so a projection is never mistaken for a measurement.

**Suggested source:** Lucide (lucide.dev, ISC licence) covers 1 to 25 almost exactly and is free for
commercial use. Phosphor Icons (MIT) is the alternative if a heavier weight reads better at 24px.
Number 26 will probably need drawing.

---

## Card art needed

The upgrade grid is eleven cards at 268x134, displayed with a dark overlay so the art only has to
carry mood, not detail. Anything at 536x268 or larger works. Sober and technical, not stock photo.

| Card | Suggested image |
|---|---|
| Reasoning | a branching decision tree, or a chess board mid game |
| Knowledge | library stacks, or a dense text field out of focus |
| Coding | a terminal or a diff view, heavily blurred |
| Multilanguage | script fragments in several writing systems |
| Multimodal | a spectrogram over an image histogram |
| Context length | a long horizontal scroll or a ribbon receding |
| Safety | a lock over a circuit, or a containment field |
| Speed | motion blur down a corridor |
| Optimisation | a descending cost curve, or tightly packed cable management |
| Tool use | a robot arm, or an API socket diagram |
| Ecosystem | a node graph with many small satellites |

Plus six rail headers at 1200x300 for the page banners, and one 512x512 placeholder company logo.

**Suggested source:** Unsplash for photography (free, no attribution required), or generate them.
Consistency matters far more than quality here: pick one treatment, apply it to all eleven.

---

## Fonts

The reference uses a condensed grotesque, uppercase, with wide letter spacing on titles. Inter,
Barlow Condensed or Archivo all land close and all have open licences. The stylesheet already sets
`letter-spacing` on titles and rail items, so swapping the font is a single `-unity-font-definition`
line in `.root`.

---

## Scene setup, once the assets exist

There is no scene yet, on purpose. To wire one up:

1. New scene, add a GameObject with `UIDocument`.
2. Panel Settings asset, scale mode "Scale with screen size", reference 1920x1080.
3. Add `GameShell` to the same GameObject and drop `ScalingLaws.uss` into its Theme field.
4. Press play. The simulation starts paused on 2022-01-01; the bottom toolbar starts the clock.

No other scene content is required. The shell builds its own tree.
