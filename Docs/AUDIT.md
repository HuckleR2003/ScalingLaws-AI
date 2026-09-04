# Audit, 2026-09-04

A sweep for things that exist and never run. Six passes over the source, every number measured
rather than remembered. The script is in the session scratchpad; the passes are described here so
they can be re-run.

The reason this exists: **a green suite proves the rules work, never that a player can reach them.**
EditMode tests drive the simulation directly, so a mechanism that is complete in `Simulation/` and
has no control in `UI/` passes everything. That has now happened eleven times in this project.

---

## 1. Operations with no control in the interface

Every `Try*` / `Cancel*` on the simulation, checked against every file in `Scripts/UI/`.

**Two genuinely player-facing, both still unreachable:**

| Method | What the player cannot do |
|---|---|
| `TryAcquireDataSource` | Buy a corpus with money instead of researching it |
| `TryAdoptArchitecture` | Adopt a family with money instead of researching it |

Both are complete, both are tested, neither has a button. They were downgraded from critical to
optional when the research tree started granting corpora and families directly, and that reasoning
still holds: nothing is unreachable *through the tree*. What is missing is the cash route, which is
a real decision the design once had and currently does not offer.

**One dead:**

- `CompetitorAgent.TryGetLiveModel` has no caller anywhere in the repository, tests included.

Everything else the sweep flagged is `ServerHall` and `ServerStock` plumbing, called by
`CompanySimulation` rather than by the interface. That is the intended layering: the hall owns
placement, the simulation owns the till.

---

## 2. The phrase book, and a blind spot worth 19% of it

1,923 keys. Measured three ways:

| | Count | |
|---|---|---|
| Asked for by name | 1,332 | a literal in the source; the existing guard covers these |
| **Built by concatenation** | **368** | **invisible to every check in the project** |
| Nothing asks for at all | 223 | dead copy from rewritten screens |

**The 368 is the finding.** `LocalisationTests.EveryKeyTheInterfaceAsksForExists` reads source text,
so it can only see keys written as literals. `TechNotes` does `From("tech.awareness")` and then
`Loc.T(stem + ".title")`; neither half is the key. `Loc.T` does not throw on a miss, it renders the
key, which is how `arch.tilte` once shipped as the visible word on a screen.

Where the blind spots are:

| Source | Keys |
|---|---|
| `TechNotes` — the "(i)" card on every control in the game | 195 |
| `WorldEventCatalog` — headlines and bodies | 66 |
| `SkillNotes` — the seven skills the creator explains | 35 |
| `GrantCatalog` — name, body and terms per grant | 36 |
| `LabTraits` — badges and their notes | 30 |

**Fixed** by `LocalisationCoverageTests`, which checks behaviour rather than source text: it resolves
what the catalogs actually return, in both languages, and fails on anything that came back as its
own key. That covers all 368 at once and keeps working as they grow, which writing 368 literals out
by hand would not.

All 368 currently resolve. The copy was there; nothing was guarding it.

The 223 dead keys are harmless and worth a cleanup pass someday: `trait.*` (22), `noun.*` (22),
`labtrait.*_note` duplicates (15), `loan.*` (12), `hire.*` (13).

---

## 3. Stylesheet

1,935 class selectors, **183 that nothing ever adds**. Dead styles, no runtime cost, worth a pass
when the sheet is next touched. The opposite direction is already guarded:
`StylesheetTests.EveryClassTheInterfaceUsesIsStyled` catches a class named from C# and absent from
the sheet, which is the one that actually breaks a screen — it caught `era--statecraft` during this
session.

---

## 4. Resources

13 literal `Resources.Load` paths, 4 with nothing behind them. Three are directory stems used to
build a path at runtime and are fine. One is real:

- **`Cards/chip_model`** — named in the catalog, absent from disk, still a drawn stand-in.

`Docs/NeededGraphics.md` is the generated list and stays the source of truth for art gaps.

---

## 5. Catalog members nothing uses

- **`CompetitorStrategy.FastFollower` is assigned to no lab.** Documented previously and still true:
  nothing in the game runs that brief. The author's call whether to reassign or delete.
- `LabTrait.Imitator` was removed for the same reason and its enum value is left as a documented gap.

---

## 6. Public constants nothing reads

| Constant | Reading |
|---|---|
| `CompanySimulation.ReputationDailyDecay` | superseded by `Standing.DailyDrift` |
| `CompanySimulation.ReputationServiceGain` | superseded by `Standing.ServiceGain` |
| `GrantCatalog.OfferOpenDays`, `MostOpenOffers` | the grant offer flow reads its own values |
| `BasementFloor.CeilingHeight` | kept from before the ceiling was removed; see the note on occlusion |
| `CityLayout.SeaLevel`, `HeightmapResolution`, `SplatResolution` | terrain values the builder no longer reads |
| `StaffCatalog.DiminishingReturnsAfter` | superseded by `SaturationMultiplier` |

Two per pair, and in every case the live number lives somewhere else. None of them is wrong, all of
them are a second place somebody could read the wrong figure from.

---

## What this pass changed

- `LocalisationCoverageTests` — closes the 368-key blind spot.
- `EnumIdentityTests` — added the same day after two silent enum-value collisions in one session
  (`GeneralIntelligence` over `ShardedOptimizerStates`, `StateProgramme` over `GrantRepaid`). Both
  are written into saves, where a collision is not a bug but a format in which one number means two
  things. Sixteen enums are now walked.

## What is left, in order of what it costs a player

1. The two cash routes (`TryAcquireDataSource`, `TryAdoptArchitecture`) have no control.
2. `CompetitorStrategy.FastFollower` runs for nobody.
3. 223 dead phrases and 183 dead selectors.
4. Six constants that are a second copy of a live number.
