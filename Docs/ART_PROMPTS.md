# Art prompt pack

I cannot generate images. This is the brief and the prompt set, built so an image model produces
one coherent set rather than twenty unrelated pictures.

Read the layout section first. The proportions here are decided, and generating art before they are
settled is how you end up with a beautiful room that does not fit.

---

## The layout the art has to fit

The isometric office is the **background of the whole game screen**, not a panel inside it. Panels
float over it, the same way the reference screenshots work.

```
+--------------------------------------------------------------+
| top bar  46px   money / valuation        company / date       |  <- overlays the art
+--------------------------------------------------------------+
|          |                                                    |
|  rail    |         the office fills everything                |
|  252px   |         panels slide over the right side           |
|          |                                                    |
+--------------------------------------------------------------+
| toolbar  58px   speed controls                    save / menu |  <- overlays the art
+--------------------------------------------------------------+
```

What this means for whoever draws the room:

- Design at **2560x1440**, 16:9. It is scaled to fit and cropped on odd aspects.
- The **top 60 pixels and bottom 80 pixels** (at that size) are covered by bars. Nothing important
  goes there.
- The **left 340 pixels** are covered by the rail. Put the room's empty floor there, not the desks.
- Keep the interesting content in the **centre and right**, biased toward the right third, because
  that is where the player's eye already is and where panels open over it.
- Leave the image **darker at the edges than in the middle**. UI text sits on top of it and needs
  the contrast. If the art is uniformly bright the interface becomes unreadable and no amount of
  overlay tuning fixes it.

## Two kinds of asset, and why

**Room shells** are one image per office tier. Five of them. They are the floor, walls, windows and
fixed architecture, with no furniture and no people.

**Sprites** are the things that change: desks, chairs, people, server racks. The game places them on
an isometric grid according to how many people are actually employed and what they do, so the room
fills up as the company grows. That is the whole point, and it is why this is not one painting per
office tier: five tiers times every plausible headcount is hundreds of images, and the version with
sprites is twenty.

---

## The shared style block

Paste this into **every** prompt, unchanged. Do not paraphrase it between images. Consistency
between assets matters far more than the quality of any single one.

```
Isometric game asset, true 2:1 dimetric projection, camera fixed at 30 degrees, orthographic, no
perspective distortion. Clean semi-realistic style, soft matte surfaces, subtle ambient occlusion.
Single light source from the upper left at 45 degrees, cool daylight, soft shadows falling down and
to the right. Muted cool palette: greys, warm wood, desaturated blues. No text, no logos, no
signage, no watermarks. Sharp edges, high detail, flat even lighting across the whole asset.
```

For sprites add:

```
Single object centred on a fully transparent background, PNG with alpha, no ground plane, no
backdrop, tight crop with 8 pixels of padding.
```

---

## Room shells, 2560x1440, opaque

One per office tier. They must read as the same building style getting bigger, not five different
worlds. Generate them in this order and feed the previous one back as a reference if the tool
supports it.

| File | Subject line to append |
|---|---|
| `room_garage.png` | A small converted garage interior, bare concrete floor, one roller door letting in daylight from the left, exposed roof beams, a single radiator, empty of furniture. |
| `room_loft.png` | An open plan loft interior, worn oak floorboards, tall industrial windows along the left wall, exposed brick on the far wall, ceiling ducting, empty of furniture. |
| `room_floor.png` | A modern office floor interior, pale grey carpet tiles, floor to ceiling glazing along the left, a glass meeting room in the far corner, suspended ceiling panels, empty of furniture. |
| `room_campus.png` | A large open plan office interior with a mezzanine level, polished concrete floor, a double height glazed wall on the left, planted dividers, empty of furniture. |
| `room_multisite.png` | A very large open plan office interior, two structural columns, polished concrete, glazed wall on the left, a glass stairwell on the right, empty of furniture. |

## Furniture and equipment sprites, transparent PNG

Sizes are guidance at the 2560x1440 room scale.

| File | Size | Subject line to append |
|---|---|---|
| `desk_empty.png` | 320x260 | An empty light oak office desk with black metal legs, seen isometrically. |
| `desk_workstation.png` | 340x300 | An office desk with two dark monitors on a stand, a keyboard and a closed laptop, no chair. |
| `chair.png` | 180x220 | A dark grey mesh office chair, seen isometrically, slightly turned. |
| `server_rack.png` | 260x420 | A black half height server rack with rack mounted units and faint blue status lights. |
| `server_rack_tall.png` | 260x600 | A black full height server rack, densely populated, faint blue status lights, cable arms at the rear. |
| `whiteboard.png` | 300x280 | A freestanding whiteboard on castors, blank surface. |
| `plant_small.png` | 140x180 | A potted fiddle leaf fig in a plain concrete planter. |
| `plant_large.png` | 200x300 | A tall potted palm in a plain concrete planter. |
| `sofa.png` | 380x220 | A low two seat sofa in dark grey fabric. |
| `coffee_table.png` | 220x140 | A small round coffee table in light oak. |
| `cabinet.png` | 240x260 | A low storage cabinet in white laminate. |
| `cardboard_boxes.png` | 200x200 | Three stacked plain cardboard moving boxes. |

## People sprites, transparent PNG, 160x260

Five, one per role, so a full office reads as the team that is actually employed. They sit at desks,
so draw them seated and facing three quarters away from the camera, which is what an isometric
office actually looks like and avoids the uncanny detail problem with faces at this size.

Keep the silhouettes distinct at 160 pixels. Colour is the fastest read, so each role gets one.

| File | Subject line to append |
|---|---|
| `person_research.png` | A seated office worker in a deep blue jumper, three quarters from behind, leaning slightly toward a desk, no visible face. |
| `person_infra.png` | A seated office worker in a dark green work shirt with rolled sleeves, three quarters from behind, upright posture, no visible face. |
| `person_data.png` | A seated office worker in a mustard yellow shirt, three quarters from behind, one arm resting on the desk, no visible face. |
| `person_safety.png` | A seated office worker in a burgundy cardigan, three quarters from behind, leaning back, no visible face. |
| `person_sales.png` | A standing office worker in a light grey shirt, three quarters from behind, holding a phone to one ear, no visible face. |

## Card art, 1024x512, opaque

These sit behind upgrade and hardware cards under a dark overlay, so they only have to carry mood.
Use the **product photography** style block instead of the isometric one:

```
Photorealistic product photography, single hero object centred, three-quarter view from slightly
above, shallow depth of field. Cold blue rim light from the left, warm key light from the right,
deep navy seamless background, soft floor reflection. Clean, technical, no text, no logos, no
people. Sharp focus on the object, muted colour grade.
```

| File | Subject |
|---|---|
| `card_gpu.png` | A datacenter accelerator board, exposed heatsink fins and gold contacts, no branding. |
| `card_cpu.png` | A large server processor package on its socket frame. |
| `card_ram.png` | Four server memory modules standing in a row. |
| `card_storage.png` | Two enterprise NVMe drives stacked at a slight offset. |
| `card_network.png` | An optical switch faceplate, rows of transceiver cages, fibre curving out of frame. |
| `card_datacenter.png` | A datacenter cold aisle receding into darkness, floor grating, no people. |
| `card_cloud.png` | A patch panel with hundreds of terminated fibres fanned into an ordered arc. |
| `card_power.png` | An industrial busway and breaker panel, thick copper conductors. |

Eleven trait cards use the same block with short concrete subjects: a glass chess board lit from
below, library stacks receding into darkness, a terminal reflected in dark glass, printing press
type in several writing systems, a camera sensor beside a studio microphone, a paper roll unspooling
into darkness, a closed steel interlock, a corridor of lights in motion blur, immaculate rack cable
management, a robotic arm holding a circuit board, a patch panel with one lit port among forty dark.

## Icons

Do not generate these. Use **Lucide** (lucide.dev, ISC licence), which covers 25 of the 26 listed in
`UI_RESEARCH_AND_ASSETS.md`. Generated icon sets do not hold consistency at 24 pixels.

The exception is the `projection` marker, which flags every estimated number so a guess is never
mistaken for a measurement. Draw it by hand: a dashed circle, one pixel stroke, empty inside.

---

## Checking a batch before you commit to it

Generate the five room shells first and put them side by side. If the floor angle, the light
direction or the wall colour drifts between any two of them, regenerate rather than proceeding.
Everything else in the set is placed on top of those floors and inherits their mistakes.

Then generate one desk, one chair and one person, and composite them onto `room_loft.png` by hand
in any image editor. If the scale or the shadow direction is wrong you will see it immediately, and
you will have spent three images finding out instead of thirty.

## Where the files go

```
Assets/_ScalingLaws/Art/Office/     room shells and sprites
Assets/_ScalingLaws/Art/Cards/      card art
Assets/_ScalingLaws/Art/Icons/      the Lucide set
```

Import settings for everything: Texture Type **Sprite (2D and UI)**, Compression **High Quality**,
**uncheck** Generate Mip Maps. For sprites leave Mesh Type on Full Rect. Getting mip maps wrong is
the usual reason isometric art looks soft in a shipped build.
