# Scaling Laws

<img width="1920" height="400" alt="SCALING LAWS Banner" src="https://github.com/user-attachments/assets/851a78d3-a820-41a0-92de-a0d62400217f" />

**An AI company tycoon.** You start on 1 January 2022 with twelve million dollars, no product and no
team, and you run a frontier lab through the four years that followed.

Rent compute you cannot really afford. Pick a model size you can supervise rather than the largest
one you can pay for. Decide whether three weeks of safety evaluation is worth delaying a release, and
find out later that a regulator judges you on what you had switched on the day you shipped.

[![Build](https://img.shields.io/badge/build-v0.2.0-blue)](https://github.com/HuckleR2003/ScalingLaws-AI/releases)
[![Tests](https://img.shields.io/badge/tests-1079%20EditMode%20%2B%2031%20PlayMode-brightgreen)](#testing)
[![Unity](https://img.shields.io/badge/Unity-6000.5.8f1-black)](#running-it)
[![Languages](https://img.shields.io/badge/languages-EN%20%2F%20PL-lightgrey)](#)
[![Licence](https://img.shields.io/badge/licence-PolyForm%20Noncommercial-orange)](LICENSE.md)

> **Download the first public build:** [Releases](https://github.com/HuckleR2003/ScalingLaws-AI/releases)
> · What changed in each version: [CHANGELOG.md](CHANGELOG.md)

---

## Contents

- [The idea](#the-idea)
- [Three things you will not have seen in another tycoon](#three-things-you-will-not-have-seen-in-another-tycoon)
- [What is simulated](#what-is-simulated)
- [Screens](#screens)
- [The honest state of it](#the-honest-state-of-it)
- [Running it](#running-it)
- [Layout](#layout)
- [Testing](#testing)
- [Contributing and feedback](#contributing-and-feedback)
- [Notes](#notes)

---

## The idea

> **Upgrades are not bought. They are timed.**

Hardware ages. An accelerator bought on launch day loses value to calendar time and again to every
successor that ships after it, so capital committed too early sits in an asset that is worth less
every month. Token prices fall by roughly half a year, permanently. The frontier moves whether or not
you do.

There is no guaranteed profit anywhere in the design. A company that ships one model and then makes
no further decision goes under, and an automated test fails the build if it is still trading eight
years later.

---

## Three things you will not have seen in another tycoon

**Shipping an update does not move everybody onto it.** Release a new version and a quarter of your
users take it on day one. The rest drift across at 12 per cent of the remaining gap per day, and if
the new version is worse they never come. The market's view of your company is what its users are
*running*, not the best thing you ever shipped.

**A regulator gives you five days.** A serious safety incident does not fine you. It opens a file,
says nothing has been decided, and puts a banner across the top of whatever screen you are on. Five
days later the verdict is rolled against the protections that model shipped with. The outcome is
decided either way, and the five days are the point: they are the difference between a hard game and
an arbitrary one.

**Seven cards beat eight.** Fill a server cabinet completely and the heat throttles everything in it.
On the 2029 generation, in an enclosed rack, eight accelerators deliver 58.86 PF and seven
accelerators plus one fan deliver 91.00. Cabinets do not age; chips get hotter, so a room you filled
in 2023 and never revisited is quietly throttling by 2027 without you having touched anything.

---

## What is simulated

**Model quality** comes from the Chinchilla parametric loss form, using the corrected fit rather than
the constants printed in the original paper, because those do not reproduce the paper's own
compute-optimal ratio. Around twenty tokens per parameter is optimal, and ten times the compute buys
about 10.0 capability points at every scale. Undertrain or overtrain and the same bill buys a worse
model.

**Hardware** is 22 real generations of accelerator, host CPU, node memory and fabric, with ship
dates, launch prices, board power and on-package memory. Buying an H100 at launch in October 2022 and
holding it to June 2026 returns about 14 per cent of the money. Buying a B200 in January 2025 and
selling on the same day returns about 53 per cent.

**Compute** is rented by the petaflop, bought as reserved capacity, or owned outright in a basement
you fill yourself. Renting tracks whatever the clouds offer, 180 days behind launch, and never ages.
Owning is roughly a third of the price per FLOP and bills whether the cluster is busy or idle.

**The market** is tracked at audience by owner by model type, which is 225 numbers rather than one,
so "who is winning programming" and "who is winning programmers" are genuinely different questions.
Every audience also weighs an outside option, because an enterprise in 2022 was not choosing between
vendors, it was choosing not to buy anything.

**Rivals** are fourteen labs, seeded from the real 2022 to 2026 release timeline and then free to
deviate. A patient lab that sees better silicon landing shortly after its planned launch will hold,
train on the newer hardware and come out measurably stronger. Three of them come apart during the
campaign, over the same exposures that can end your company.

**Research** is a tree of 50 nodes across four eras and three tracks. Every architecture, corpus,
upgrade line and compute tier sits behind one. Money is a bad gate because money compounds; a node
also costs calendar, and calendar cannot be compounded away.

**Capital** is priced on proximity to the frontier to the fourth power, plus revenue at a multiple,
multiplied by investor sentiment. Sentiment runs from 0.55 in early 2022 to 2.20 in mid 2025. The
same company is worth four times as much depending only on when the term sheet was signed.

**Where you register** changes four numbers: hardware price, corporate tax, research speed and local
competition. Tax is charged on daily operating profit rather than on turnover, so a loss-making year
is not made worse by geography. No country wins on all four axes and a test asserts it.

---

## Screens

<!-- Drop screenshots here. Upload them to a GitHub issue or release to get a permanent URL,
     then paste it in. Recommended set, all four already render out of TabProof~/ :
       tab_room.png     the basement, cabinets and the heat banner
       tab_create.png   the model creator
       pl_family.png    architecture, with the locked sliders naming their research
       tab_fleet.png    compute, the load dial and the rented-capacity meters       -->

Fifteen screens reachable from the bottom bar: the office, model design, research, architecture,
upgrades, the team, compute, business, release, capital, the board, intelligence, marketing, news and
mail. Plus a model creator in eight stages and a server room you can walk into.

The interface is UI Toolkit, built in C# against a single stylesheet, in **English or Polish**,
switchable at any time including mid-conversation.

---

## The honest state of it

The art is partial. Several screens are still plain. The world map in character creation is a
placeholder. There are mechanics that are built and not yet reachable, and that is a measured number
rather than a feeling: a sweep lists every public method in the simulation layer and searches the
interface layer for a caller. It has found **twelve** so far, one of them an entire progression system
the player paid research points, cash and four months of in-game calendar for and never received.

The build exists now anyway, and the reason is narrow: the economy has to be judged from a player's
seat rather than from a test report, and nobody who has not built this thing has an opinion about it
yet.

---

## Running it

**Unity 6000.5.8f1.** Not 6000.4. The manifest carries `com.unity.modules.physicscore2d`, which does
not exist before 6000.5, so an earlier editor fails package resolution and reports `Aborting
batchmode` with no results at all. That failure looks exactly like tests not being picked up.

```bash
# EditMode: no scene is loaded, runs in seconds
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml

# PlayMode: loads scenes, renders every screen to TabProof~/
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayResults.xml
```

**Do not add `-quit` to `-runTests`.** Unity finishes the import, never runs the tests, and exits 0.

The menu scene is generated by the editor builder. **The game scene is not**: it holds 107
hand-placed prefab instances and the builder refuses to regenerate it, because it destroyed that work
once already.

A fresh clone shows the office furniture and the character models as missing references. Those are
imported Asset Store packs; their licences forbid redistribution, so they are gitignored and this
repository is public. Everything that decides anything is in the repository.

---

## Layout

```
Scripts/Core/         Date, clock, deterministic random, units.  No game rules. No UnityEngine.
Scripts/Data/         Pure data libraries plus lookups.          No economics.  No state.
Scripts/Simulation/   All the rules.                             No UnityEngine.
Scripts/Persistence/  Save format, migration, PlayerPrefs.
Scripts/UI/           UI Toolkit panels. Consumers only.
```

**`Simulation/` never imports `UnityEngine`.** That single constraint is why the whole game can be
tested in seconds without opening a scene, and why balance is tuned from tests rather than by
clicking.

| Folder | Role |
|---|---|
| `Assets/_ScalingLaws/Scripts/` | 257 C# files across the five layers above |
| `Assets/_ScalingLaws/Editor/` | Scene generation, rig setup, the city flyover |
| `Assets/_ScalingLaws/Tests/EditMode/` | 1079 tests across 113 fixtures. None load a scene. |
| `Assets/_ScalingLaws/Tests/PlayMode/` | 31 tests that do load a scene, and render pages to PNG |
| `Docs/` | The mechanism map, the art brief, the world map plan |

[`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) is the mechanism map. Read it before adding anything,
and extend an existing catalog rather than starting a second one.

Saves carry a version and **one migration step per version**, currently at v51. Old shapes are kept
verbatim so the upgrade path reads a real historical structure instead of guessing. Where a migration
has to invent a value the old format never stored, it picks the least flattering assumption that is
still defensible and records what it did.

---

## Testing

Three fixtures do work the others cannot.

**`PlayabilityTests`** runs a scripted baseline player, deliberately ordinary, for four campaign
years. It asserts the player survives, ships models, stays within reach of the frontier, beats a
company that shipped once and coasted, and does not end up dominating. It found three balance faults
no unit test would have: rental billed by unit count tripled on a hardware generation change, trait
decay made a shipped model worthless in two years for no decision the player made, and serving at
full training-active parameters made market share decorative because everyone was capacity bound.

**`ConsistencyTests`** checks that nothing anywhere produces a NaN, a negative where a negative is
nonsense, a value outside its own declared range, or a dangling reference between two catalogs. It
found a research node that opened six months before its own prerequisite, which made it permanently
unreachable.

**`TabProofTests`** (PlayMode) renders every screen to a PNG against a furnished two-year campaign
and writes them to `TabProof~/`. It exists because a green suite is blind to layout: **every visual
fault in this project has been found by looking at a picture, never by an assertion.** One pass over
the contact sheet found a page that rendered as a single button, three purchase options that read as
empty input fields, and money printed as `$20,00` because a raw format string follows the machine's
locale.

There is also the reachability sweep described under [the honest state of
it](#the-honest-state-of-it). A test proves a function does what you expect when you call it. It
proves nothing about whether anything calls it, and those are different questions.

---

## Contributing and feedback

**Feedback on the build is worth more to this project than a pull request right now.** If you play
it, the one thing worth telling me is the exact moment you got confused. Open an
[issue](https://github.com/HuckleR2003/ScalingLaws-AI/issues) or use the feedback link inside the
game.

If you want to work on the code, read [CONTRIBUTING.md](CONTRIBUTING.md) first. The short version:
the layer rule above is not negotiable, every behaviour change ships with the test that would have
caught its absence, and anything visual needs a render rather than an assertion.

---

## Notes

This project is **openly AI assisted**, and here is the honest cost of that: it makes writing a
function that satisfies a description much faster, and does nothing at all for whether anything calls
that function. The reachability sweep exists because of it.

Prose in this repository is written to be read as engineering notes: no em-dashes, no marketing
adjectives, real verifiable numbers only.

Company and product names in the game are deliberate near misses of real ones. Hardware
specifications and competitor release dates are public vendor and product information, rounded, used
as reference data for a simulation. Real companies' documented histories appear as dated chapters;
anything the game invents carries a projection flag, and no individual person is ever named.

Built solo by Marcin "HCK" Firmuga ([HCK Labs](https://pcworkman.dev)), who also builds
[PC Workman](https://github.com/HuckleR2003/PC_Workman_HCK).

## Licence

[PolyForm Noncommercial 1.0.0](LICENSE.md). Read it, learn from it, run it, fork it, change it, share
your fork, for any purpose that is not commercial. Selling it, or a game built from it, needs a
separate agreement: kematex2202@gmail.com.

The imported Asset Store packs used for the office furniture and character models are not in this
repository and are not covered by that licence.
