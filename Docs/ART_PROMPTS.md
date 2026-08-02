# Art prompt pack

I cannot generate images. This is the next best thing: a prompt pack built so an image model
produces a set that looks like one set, rather than nineteen unrelated pictures.

The failure mode with generated game art is inconsistency. Everything below shares one lighting
setup, one camera angle, one background and one palette, and those four lines are repeated verbatim
in every prompt. Do not paraphrase them between images.

---

## The shared style block

Paste this into every prompt, unchanged:

```
Photorealistic product photography, single hero object centred, three-quarter view from slightly
above, shallow depth of field. Cold blue rim light from the left, warm key light from the right,
deep navy seamless background (#0A2440), soft floor reflection. Clean, technical, no text, no logos,
no people, no clutter. 4k, sharp focus on the object, muted colour grade.
```

Then append one subject line from below. Nothing else.

## Card art, 1024x512, used behind upgrade and hardware cards

| File | Subject line to append |
|---|---|
| `card_gpu.png` | A single modern datacenter accelerator board, exposed heatsink fins and gold contacts, no branding. |
| `card_cpu.png` | A large server processor package resting on its socket frame, integrated heat spreader catching the rim light. |
| `card_ram.png` | Four server memory modules standing in a row, heat spreaders reflecting the blue rim light. |
| `card_storage.png` | Two enterprise NVMe drives stacked at a slight offset, one showing its controller side. |
| `card_network.png` | A high-density optical switch faceplate, rows of transceiver cages, three fibre cables curving out of frame. |
| `card_rack.png` | One open server rack seen from the front, populated with identical sled chassis, status lights glowing faintly. |
| `card_datacenter.png` | A datacenter cold aisle receding into darkness, two rows of racks, floor grating, no people. |
| `card_cloud.png` | A cable trunk entering a patch panel, hundreds of terminated fibres fanned into an ordered arc. |
| `card_power.png` | An industrial busway and breaker panel, thick copper conductors, cold blue reflections. |
| `card_users.png` | A wall of small identical screens showing abstract blue waveform patterns, receding out of focus. |

## Trait card art, 1024x512

Same style block. These are less literal, so keep the subject lines short and concrete.

| File | Subject |
|---|---|
| `trait_reasoning.png` | A glass chess board mid-game lit from below. |
| `trait_knowledge.png` | Library stacks receding into darkness, spines out of focus. |
| `trait_coding.png` | A terminal window reflected in dark glass, text unreadable. |
| `trait_multilingual.png` | Printing press type blocks in several writing systems, arranged in a grid. |
| `trait_multimodal.png` | A camera sensor and a studio microphone side by side on a dark surface. |
| `trait_context.png` | A long paper roll unspooling into darkness. |
| `trait_safety.png` | A heavy steel interlock mechanism, closed. |
| `trait_speed.png` | A corridor of ceiling lights in motion blur. |
| `trait_efficiency.png` | Immaculate cable management in a rack, every run identical length. |
| `trait_tools.png` | A robotic arm end effector holding a small circuit board. |
| `trait_ecosystem.png` | A fibre patch panel where one port is lit and forty are dark. |

## Icons

Do not generate these. Use **Lucide** (lucide.dev, ISC licence). The list of 26 with their exact
uses is in `UI_RESEARCH_AND_ASSETS.md`. Generated icon sets do not stay consistent at 24 pixels and
a real icon font will always look better than an image model's attempt at one.

The one exception is the `projection` marker, which marks every estimated number so a guess is never
mistaken for a measurement. Draw it by hand: a dashed circle, one pixel stroke, nothing inside.

## Company marks

The four opening companies currently render as a letter in their house colour, which reads fine and
costs nothing. If you want real marks, keep them geometric and flat, no gradients, no photorealism,
because they sit next to text at 34 pixels:

- **OpenSI** green `#12B886`, two overlapping open rings
- **Antropic** orange `#D9822B`, a single angular A with the crossbar removed
- **DeepSearch** blue `#4C6EF5`, three nested chevrons pointing down
- **HuggyFace** amber `#F59F00`, two curved brackets facing each other

## Wiring art in once it exists

Drop the files in `Assets/_ScalingLaws/Art/Cards/`. The stylesheet already gives every `.card` a
background slot and a dark overlay, so a card picks its image up from one line of USS per class with
no layout change and no C# change. That is why the cards are colour blocks today rather than
something that would need rebuilding later.
