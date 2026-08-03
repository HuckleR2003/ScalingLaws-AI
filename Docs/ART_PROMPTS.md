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

### The first version of this document produced cinema instead of subjects

The original prompts put a thirty word photography brief first and the actual subject last. Image
models weight early tokens hardest, so they read the mood and ignored the object: a prompt for a
glass chess set returned a dark blue shape in front of a bookshelf. That was a fault in the prompt,
not in the tool.

Words like *underexposed*, *shallow depth of field*, *colour grade* and *cinematic* are the trigger.
They are cinematographer vocabulary, and a model that sees them makes a film still.

### The structure that works

**Subject first, in plain words. Style second, in as few words as possible. Negatives last.**

```
[what the object is, 5 to 10 plain words]. Studio product photo on a plain dark navy background.
Dim blue light from one side. Nothing else in frame. No text, no logo, no people.
```

That is the whole style block. It is deliberately short. Every adjective removed is one less thing
competing with the subject.

If an image still comes back atmospheric rather than literal, cut further, not more. `A server rack.
Plain dark navy background. Studio photo.` produces a better card than any paragraph.

### Ready to paste

Each line below is a complete prompt. Do not add anything to them.

| File | Prompt |
|---|---|
| `card_gpu.png` | A graphics card circuit board with black heatsink fins. Studio product photo on a plain dark navy background. Dim blue light from one side. Nothing else in frame. No text, no logo, no people. |
| `card_cpu.png` | A computer processor chip, square with gold pins. Studio product photo on a plain dark navy background. Dim blue light from one side. Nothing else in frame. No text, no logo, no people. |
| `card_ram.png` | Four computer memory sticks standing in a row. Studio product photo on a plain dark navy background. Dim blue light from one side. Nothing else in frame. No text, no logo, no people. |
| `card_storage.png` | Two small solid state drives stacked on top of each other. Studio product photo on a plain dark navy background. Dim blue light from one side. Nothing else in frame. No text, no logo, no people. |
| `card_network.png` | A network switch with rows of small ports and blue cables. Studio product photo on a plain dark navy background. Dim blue light from one side. No text, no logo, no people. |
| `card_rack.png` | A single black server rack cabinet full of servers. Studio product photo on a plain dark navy background. Dim blue light from one side. No text, no logo, no people. |
| `card_datacenter.png` | A row of server cabinets in a dark server room. Wide shot, dark, empty of people. No text, no logo. |
| `card_cloud.png` | A panel of hundreds of fibre optic cables. Studio photo on a plain dark navy background. Dim blue light. No text, no logo, no people. |
| `card_power.png` | An electrical breaker cabinet with thick copper bars. Studio photo on a plain dark navy background. Dim blue light. No text, no logo, no people. |
| `card_office.png` | An empty dark meeting room seen through a glass wall at night. No people, no text, no logo. |

Trait cards, same rule. Keep the subject blunt.

| File | Prompt |
|---|---|
| `trait_reasoning.png` | A glass chess set on a dark table. Studio photo on a plain dark navy background. Dim blue light. No text, no people. |
| `trait_knowledge.png` | Tall library shelves full of books, dark. No text, no people. |
| `trait_coding.png` | A computer screen showing blurred green code in a dark room. No readable text, no people. |
| `trait_multilingual.png` | Metal printing press letter blocks arranged in a grid. Studio photo on a plain dark navy background. Dim blue light. No people. |
| `trait_multimodal.png` | A camera lens and a microphone side by side. Studio photo on a plain dark navy background. Dim blue light. No text, no people. |
| `trait_context.png` | A long roll of white paper unrolling on a dark surface. Studio photo, dark navy background. No text, no people. |
| `trait_safety.png` | A heavy closed steel padlock. Studio photo on a plain dark navy background. Dim blue light. No text, no people. |
| `trait_speed.png` | A dark tunnel with streaks of light, motion blur. No text, no people. |
| `trait_efficiency.png` | Neatly bundled network cables in a rack, all the same length. Dark, dim blue light. No text, no people. |
| `trait_tools.png` | A robot arm gripper holding a small circuit board. Studio photo on a plain dark navy background. No text, no people. |
| `trait_ecosystem.png` | A dark patch panel where one port glows blue and the rest are dark. No text, no people. |

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

Not everything should come from the same place, and some of it should not be generated at all.

| Asset | Where from | Why |
|---|---|---|
| Hardware and datacenter cards | **Stock photos.** Unsplash or Pexels | See below. This is the recommendation. |
| Trait and era cards | **Bing Image Creator** (DALL-E 3, free) | These are abstract, so there is nothing to search for. DALL-E 3 follows a literal subject far better than Grok does. |
| Parody logos | **Ideogram** free tier | Best at flat vector marks and the least likely to bolt on random letters. Letters on a parody mark are both the ugliest failure and the legal risk. |
| Tab and UI icons | **Lucide** (lucide.dev) | Not generated at all. |
| Hardware icons | **Rendered from the 3D kits** | Matches the office scene by construction. |

### Stop generating photos of hardware. Search for them.

A photograph of a real graphics card already exists, thousands of times, taken properly, free to
use commercially. Fighting a prompt to approximate one is the slowest path to a worse result.

- **Unsplash** (unsplash.com) - the Unsplash licence allows commercial use with no attribution
  required. Search `server room`, `data center`, `circuit board`, `graphics card`, `fibre optic`,
  `server rack`.
- **Pexels** (pexels.com) - same terms, different library. Worth checking both.

Pick dark ones. Then in any editor, do the same two things to every image so they read as a set:
drop the exposure until it looks slightly too dark, and pull the saturation down. That is thirty
seconds per image and it does more for consistency than any prompt.

Generation is still the right answer for the eleven trait cards and the four era banners, because
`a glass chess set lit from below to represent reasoning` is not something anyone has photographed
and uploaded.

### If a free tier runs out

**Bing Image Creator** (DALL-E 3) is free with a daily allowance and is the strongest free option
for literal subjects. **Leonardo.ai** has a daily allowance. **Flux** or **SDXL** run locally
through ComfyUI are free and unlimited if the machine can take it.

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

Do **two images first**, not twenty: `trait_safety` and `trait_speed`. Put them side by side.

Then check, in this order:

1. **Is the subject actually in the picture?** A padlock and a light tunnel. If either came back as
   an atmospheric scene with no clear object, the prompt is still too long. Cut words until the
   object appears. This is the failure that wasted the first attempt.
2. Are they the same darkness? If one is noticeably brighter, fix it in an editor rather than
   regenerating. Exposure is faster to correct than to prompt for.
3. Does white uppercase text read over the top-left corner of both?
4. Any text, logo or watermark inside the image? Regenerate. Do not paint it out.

Only when two are right should the rest be generated. Fixing an approach after twenty images means
redoing twenty images.

## If it keeps fighting you

The cards are displayed at **268x134 pixels** under a dark overlay with a title and a price on top.
At that size almost nothing of a photograph survives except its overall tone.

So if an image is close enough that it reads as "something technical and dark", it is done. Judge
every candidate at the size it will actually appear, not full screen. A card that looks
disappointing at 1024 wide is usually fine in the game, and chasing perfection at full resolution is
time spent on pixels nobody will see.

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
