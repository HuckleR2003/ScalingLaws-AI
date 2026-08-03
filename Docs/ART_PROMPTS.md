# Art prompt pack

Final. Everything here is for an image model. The office is **not** in this document: it is a 3D
scene built from free CC0 kits, and generated images cannot be used for it. See
`SCENES_AND_ART_WORKFLOW.md` for that decision and why.

What still needs generating: card art, tab icons, parody logos.

---

## The rule that outranks every prompt

**This art sits underneath an interface. It is background, not subject.**

Every image here is covered by text, buttons and numbers. If it competes for attention the
interface stops being readable and no amount of overlay tuning fixes it afterwards. So:

- **Low contrast inside the image.** No bright highlights, no strong focal point in the centre.
- **Dark, and evenly dark.** Aim for something that looks slightly underexposed on its own.
- **No sharp detail across the whole frame.** One object in focus, everything else soft.
- **Nothing near the edges.** Titles sit top-left, prices sit bottom-left.
- **Never any text, numbers, logos, watermarks or UI elements** inside a generated image. The game
  draws its own.

If an image looks great on its own, it is probably wrong for this. The test is: put a white
uppercase label over the top-left corner and a price over the bottom. If either is hard to read,
regenerate darker.

---

## Card art

**Size 1024x512. Opaque JPG or PNG.** Used behind upgrade, research and hardware cards, under a
dark overlay the game applies.

Shared style block, paste unchanged into every prompt:

```
Photorealistic product photography, single hero object placed slightly right of centre, three
quarter view from above, shallow depth of field with the background fully out of focus. Deep navy
plain backdrop, near black at the frame edges. One soft cold blue rim light from the left, one
weak warm fill from the right, both low intensity. Underexposed by one stop, muted desaturated
colour grade, no bright highlights, no reflections into the lens. No text, no logos, no branding,
no people, no UI. Clean and technical.
```

### Hardware and infrastructure

| File | Subject line to append |
|---|---|
| `card_gpu.png` | A datacenter accelerator board, exposed heatsink fins and gold edge contacts, unbranded. |
| `card_cpu.png` | A large server processor package resting on its socket frame. |
| `card_ram.png` | Four server memory modules standing in a row, plain heat spreaders. |
| `card_storage.png` | Two enterprise solid state drives stacked at a slight offset. |
| `card_network.png` | An optical switch faceplate, rows of small transceiver cages, two fibre cables curving out of frame. |
| `card_rack.png` | A single black server rack seen from the front, populated with identical units, dim status lights. |
| `card_datacenter.png` | A datacenter cold aisle receding into darkness, floor grating, empty of people. |
| `card_cloud.png` | A fibre patch panel, hundreds of terminated cables fanned into an ordered arc. |
| `card_power.png` | An industrial breaker panel and busway, thick copper conductors. |
| `card_office.png` | An empty meeting room seen through glass at night, chairs pushed in, lights off. |

### The eleven upgrade traits

Same style block. Keep the subjects concrete; abstract prompts drift.

| File | Subject |
|---|---|
| `trait_reasoning.png` | A glass chess set mid-game, lit from below. |
| `trait_knowledge.png` | Library stacks receding into darkness, spines out of focus. |
| `trait_coding.png` | A terminal window reflected in dark glass, the text unreadable. |
| `trait_multilingual.png` | Metal printing press type blocks in several writing systems, arranged in a grid. |
| `trait_multimodal.png` | A camera sensor and a studio microphone side by side on a dark surface. |
| `trait_context.png` | A long paper roll unspooling into darkness. |
| `trait_safety.png` | A heavy steel interlock mechanism, closed. |
| `trait_speed.png` | A corridor of ceiling lights in motion blur. |
| `trait_efficiency.png` | Immaculate cable management inside a rack, every run cut to identical length. |
| `trait_tools.png` | A robotic arm end effector holding a small circuit board. |
| `trait_ecosystem.png` | A patch panel where one port is lit and forty are dark. |

### Research eras, 1024x512

Four banners, one per era of the technology tree. Same style block, but these carry more mood.

| File | Subject |
|---|---|
| `era_foundations.png` | A single desk lamp lighting a cluttered research desk in an otherwise dark room. |
| `era_scaling.png` | An endless row of identical server racks vanishing into darkness. |
| `era_autonomy.png` | A robotic arm at rest in an empty industrial bay, one status light on. |
| `era_superintelligence.png` | An empty control room at night, banks of dark monitors, one screen faintly lit. |

---

## Parody logos

Four rival companies, deliberate near misses of labs that existed in 2022. **Recognisable as a joke,
never a copy.** The names are already parodies; the marks should be too.

**Size 512x512, transparent PNG.** These render at 34 pixels next to text, so they must be flat and
geometric. Photorealism at that size becomes mud.

Style block for these, different from the card one:

```
Flat vector logo mark, single solid colour on a fully transparent background, geometric, symmetrical,
no gradient, no shadow, no bevel, no text, no letters, thick even strokes, readable at 32 pixels,
centred with even padding.
```

| File | Colour | Subject line to append |
|---|---|---|
| `logo_opensi.png` | `#12B886` | Two overlapping open rings forming a knot, one stroke weight throughout. |
| `logo_antropic.png` | `#D9822B` | A single angular chevron shape resembling a stylised letter A with no crossbar. |
| `logo_deepsearch.png` | `#4C6EF5` | Three nested chevrons pointing downward, evenly spaced. |
| `logo_huggyface.png` | `#F59F00` | Two curved brackets facing each other with a small gap between them. |

If the model insists on adding letters, generate in black and recolour in an editor. Letters are the
failure mode with logo prompts and they are also the legal risk, so check every output for them.

The player's own company currently renders as a letter in a chosen colour, which reads fine and
costs nothing. Leave it that way until everything else is done.

---

## Which tool for which job

Not everything should come from the same generator. This split costs nothing and gives the best
result per hour spent.

| Asset | Tool | Why |
|---|---|---|
| Card, trait and era art | **Grok** | Photographic, forgiving, and it sits under an overlay anyway. |
| Parody logos | **Ideogram** free tier | Best at flat vector marks and, more importantly, the least likely to bolt on random letters. Letters are both the ugliest failure and the legal risk. |
| Tab and UI icons | **Lucide** (lucide.dev) | Not generated at all. See below. |
| Hardware icons | **Rendered, not generated** | See below. |

Free alternatives if a tier runs out: **Bing Image Creator** (DALL-E 3, free with daily limits) is
the strongest free photographic option. **Leonardo.ai** has a daily free allowance. Running **Flux**
or **SDXL** locally through ComfyUI is free and unlimited if the machine can take it.

---

## Interface icons

**Do not generate these.** Use **Lucide** (lucide.dev, ISC licence, free commercially, no
attribution). It covers 25 of the 26 listed in `UI_RESEARCH_AND_ASSETS.md`.

Generated icon sets drift: stroke weights wander, corner radii disagree, and at 24 pixels those
differences are the only thing the eye sees. An icon family is designed as a family, and no prompt
recovers that.

Download as SVG, then in Unity import as a sprite at 128x128 with **Alpha Is Transparency** on. The
interface tints them, so generate or export them **white on transparent**, never coloured.

The one exception is the `projection` marker, which flags every estimated number so a guess is never
mistaken for a measurement. It does not exist in any icon set. Draw it by hand: a dashed circle, one
pixel stroke, empty inside.

---

## Hardware icons: render them, do not generate them

The processor, memory, drive, rack and datacenter icons all have a free path that beats generation
outright, and it costs about twenty minutes.

The 3D kits already being imported for the office scene contain a server rack, a monitor, a desktop
tower and similar. Place one in an empty Unity scene, point the same orthographic camera at it
(`X 30, Y 45`), one directional light, transparent background, and take a screenshot.

Three reasons this wins:

1. **They match the office scene exactly**, because they are the same models under the same camera
   and the same light. A generated icon next to a rendered room always looks borrowed.
2. **Consistency is free.** Ten icons from ten renders share everything automatically.
3. **You can redo one** without regenerating a set.

Set the camera's **Background Type** to **Solid Color** with alpha 0, tick **Post Processing** off
for these, and use **Edit > Render > Screenshot** or just a 512x512 render texture.

If a shape genuinely is not in any kit, then generate it, using the flat vector block from the logo
section rather than the photographic one.

---

## Doing it in batches

Generate **four card images first**, from different sections: `card_gpu`, `trait_safety`,
`era_scaling`, `card_datacenter`. Put them side by side.

Check three things before generating anything else:
1. Are they the same darkness? If one is noticeably brighter, the style block is being ignored and
   needs to be restated more forcefully.
2. Does white uppercase text read over the top-left of all four?
3. Does any of them have text, a logo or a watermark in it? Regenerate; do not paint it out.

Fixing the style block after twenty images means regenerating twenty images.

---

## Where the files go

```
Assets/_ScalingLaws/Art/Cards/     card, trait and era art
Assets/_ScalingLaws/Art/Logos/     the four parody marks
Assets/_ScalingLaws/Art/Icons/     the Lucide set
```

Import settings for all of it: Texture Type **Sprite (2D and UI)**, Compression **High Quality**,
**Generate Mip Maps off**. Mip maps on UI art is the usual reason it looks soft in a build.

The stylesheet already gives every `.card` a background slot and a dark overlay, so wiring art in is
one line of USS per class and no C# change. That is why the cards are colour blocks today rather
than something that would need rebuilding later.
