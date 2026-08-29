# Server room art: what is needed and how to ask for it

Written 2026-08-29, against the reference the author supplied: a rack drawn front-on, with
distinguishable sleds slotted into it, a large multi-bay unit, and a cooling row.

The room already works. `ServerRoomScreen` draws the floor, `RackEditorPanel` opens one cabinet and
draws its slots **as bars**, and every one of those bars is currently a coloured rectangle. This
document is the list of pictures that would replace those rectangles, and the prompt for each.

---

## The rules every one of these has to follow

These are not style preferences. Each one comes from something this project has already got wrong.

1. **Front-on, orthographic, no perspective.** The bars sit in a vertical stack inside a cabinet.
   Anything drawn at an angle will not tile with the one above it.
2. **Fixed aspect, 512 x 96 px, transparent background.** One slot is one unit high. A part that
   occupies four slots is drawn at 512 x 384 and says so in its file name.
3. **Evenly dark, no bright focal point.** The art direction rule for this whole project: the
   interface sits on top. The test is putting white 10px uppercase text over the left third. If it
   is hard to read, it is too bright.
4. **The status light is drawn by the game, never by the picture.** Heat, throttling and whether a
   slot is occupied are simulation state. A green LED baked into a texture is a lie the moment a
   cabinet overheats.
5. **No text, no logos, no numbers anywhere in the image.** Ever. Labels are drawn as UI on top so
   they can be translated. This project already shipped a screen of raw phrase-book keys once.
6. **Read at 96px tall.** Every one of these will be looked at small. Detail that only resolves at
   full size is detail nobody will see.

---

## What the game actually has, and therefore what art it needs

### 1. The four cabinets (`ServerRack`)

Drawn as the empty container the slots sit inside. **1024 x 1400 px**, transparent, front-on.

| Id | In game | Slots | Cooling |
|---|---|---|---|
| `OpenFrame` | Open frame | fewest | room does the work |
| `Enclosed` | Enclosed rack | middle | doors and internal fans |
| `HighDensity` | High density | many | rear door heat exchanger |
| `Immersion` | Immersion tank | most | dielectric fluid |

> **Open frame.** A bare four-post aluminium server rack frame, front elevation, orthographic, no
> perspective. Two vertical posts with square mounting holes down each side, three horizontal
> cross-braces, open on all sides with nothing behind it. Brushed dark grey metal, matte, evenly
> lit from the front with no hotspot and no reflections. Empty: no equipment mounted. Isolated on a
> transparent background. Industrial product illustration, flat clean shading, no text, no labels,
> no branding, no numbers.

> **Enclosed rack.** A closed 42U server cabinet, front elevation, orthographic, no perspective.
> Perforated front door with a fine hexagonal mesh, a slim handle on the right edge, solid side
> panels, levelling feet at the base. Dark graphite grey, matte powder coat, evenly lit with no
> hotspot. Door shown open to reveal empty mounting rails inside. Isolated on a transparent
> background. Industrial product illustration, flat clean shading, no text, no labels, no branding.

> **High density.** A closed server cabinet with a rear door heat exchanger, front elevation,
> orthographic. Deeper and heavier than a standard cabinet, thick sealed side panels, a broad
> perforated front, two coolant pipes entering at the base in muted blue and muted red. Dark
> graphite with cool blue-grey accents. Evenly lit, matte, no hotspot. Empty inside. Transparent
> background, industrial product illustration, no text, no branding.

> **Immersion tank.** A horizontal single-phase immersion cooling tank, front elevation,
> orthographic. A long open-topped steel bath filled with clear dielectric fluid, faint fluid line
> visible near the top, a lid hinged open behind it, coolant manifold along the near edge. Muted
> steel and very desaturated teal fluid, evenly lit, matte, no caustics and no bright highlights.
> Empty: nothing submerged. Transparent background, industrial illustration, no text, no branding.

---

### 2. Accelerator sleds

This is the one the reference picture is really about: **the coloured bars in `RackEditorPanel`.**

`HardwareCatalog` carries roughly twenty two generations. **Do not draw twenty two pictures.** Draw
**four** and let the game tint and label them, because the difference between an A100 and an H100
at 96px tall is a sticker, and four eras is a difference anybody can see.

**512 x 96 px**, transparent, front elevation.

> **Accelerator sled, early era.** The front face of a 1U GPU server sled, front elevation,
> orthographic, no perspective. A wide flat rectangular faceplate with two rows of small round
> ventilation perforations, a recessed pull handle at each end, a narrow row of unlit indicator
> apertures near the left. Dark gunmetal grey, matte, evenly lit front-on with no hotspot and no
> reflections. All indicators dark and unlit. Isolated on a transparent background. Technical
> product illustration, flat clean shading, no text, no logos, no numbers, no stickers.

Then the same shape three more times, changing only the density of the detail so the eras read as a
progression:

- **mid era**: replace the perforation rows with a single wide honeycomb intake, add two small
  latch tabs.
- **modern era**: a full-width fine mesh intake, four indicator apertures, a thin bezel lip.
- **frontier era**: a solid faceplate with a narrow slot intake, two thick coolant quick-connects
  at the right, visibly heavier than the others.

---

### 3. The large multi-bay unit

The reference shows one part occupying several slots with a row of drive bays in it. In this game
that is the natural picture for a **storage and data shelf**.

**512 x 384 px** (four slots), transparent.

> **Storage shelf, front elevation, orthographic, no perspective.** A 4U rack-mount chassis whose
> entire front face is a grid of twelve identical drive carriers in three rows of four, each carrier
> a narrow rectangular tray with a small recessed latch on its left edge and a thin unlit indicator
> aperture. Dark graphite carriers on a slightly darker chassis, matte, evenly lit with no hotspot.
> All indicators dark and unlit. Isolated on a transparent background. Technical product
> illustration, flat clean shading, no text, no labels, no branding, no numbers.

---

### 4. Support hardware

`HardwareClass` has three besides the accelerator, and they are visibly different objects.

**512 x 96 px** each.

> **Host CPU node.** The front face of a 1U dual-socket compute server, front elevation,
> orthographic. Flat faceplate divided into three shallow recessed sections, a narrow row of
> unlit indicator apertures on the left, a small blanked port aperture on the right, thin pull tabs
> at each end. Dark gunmetal, matte, even front lighting, no hotspot. Transparent background,
> technical product illustration, no text, no logos, no numbers.

> **Memory shelf.** The front face of a 1U memory expansion chassis, front elevation, orthographic.
> A repeating pattern of sixteen narrow vertical slot covers across the whole face, a slim handle at
> each end. Dark graphite with a faint cool tint, matte, evenly lit, no hotspot. Transparent
> background, technical product illustration, no text, no branding.

> **Fabric switch.** The front face of a 1U network switch, front elevation, orthographic. Two rows
> of sixteen small square cage ports across the face, all empty and unlit, a narrow management port
> at the far left. Dark graphite, matte, evenly lit, no hotspot. Transparent background, technical
> product illustration, no text, no logos, no port numbers.

---

### 5. Cooling

`ServerRackCatalog.FanCoolingKilowatts` is 2.4 and a fan costs $2,600. It occupies one slot and it
is the decision the whole cabinet turns on, so it has to be **instantly distinguishable** from every
sled above.

**512 x 96 px.**

> **Rack fan unit, front elevation, orthographic, no perspective.** A 1U cooling module whose face is
> almost entirely two large circular fan guards side by side, concentric wire grilles with visible
> blades behind them, a narrow solid margin around the outside. Dark graphite frame, slightly
> lighter grey blades, matte, evenly lit with no hotspot and no motion blur. Stationary. Isolated on
> a transparent background. Technical product illustration, flat clean shading, no text, no
> branding.

Also worth one picture, for a slot that is deliberately empty:

> **Blanking panel.** A plain 1U rack blanking plate, front elevation, orthographic. Featureless flat
> rectangle with a shallow lip along the top and bottom edge and two small screw recesses at each
> end. Dark graphite, matte, evenly lit, no hotspot. Transparent background, no text, no branding.

---

## The count

**Fourteen images.** Four cabinets, four accelerator eras, one storage shelf, three support units,
one fan, one blank.

That is the whole room. Everything else on that screen is state the game already computes and draws
on top: heat colour, throttle state, occupancy, the slot bars themselves.

---

## Where they go

`Assets/_ScalingLaws/Resources/Racks/`, named for what they are:
`rack_openframe`, `rack_enclosed`, `rack_highdensity`, `rack_immersion`,
`sled_era1` through `sled_era4`, `shelf_storage`,
`node_cpu`, `node_memory`, `node_fabric`, `part_fan`, `part_blank`.

**Every loader in this project falls back to a plate rather than throwing**, so these can be added
one at a time and the room keeps working with none of them. That is also why nothing announces a
missing file: `Docs/NeededGraphics.md` is generated by walking `Resources.Load` calls against what
is on disk, and it should be regenerated once these are named in code.
