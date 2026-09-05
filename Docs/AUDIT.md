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

**Closed 2026-09-04 for the corpus, and it was worse than this said.** The DATA stage of the
creator listed only corpora the company **already owned**, so an unowned one was not merely
unbuyable: nothing anywhere in the game named it, priced it, or said what would open it. A player
learned that licensed video existed when a research node happened to hand it over.

The stage now lists every published corpus the company does not hold, with what it costs and, when
it cannot be bought, the simulation's own reason. `CanAcquireDataSource` is the check half of
`TryAcquireDataSource`, extracted rather than copied, so the row and the button read one body.

Four of the eight corpora turn out to be genuinely purchasable with money alone, and that is a
finding rather than a decision: `CuratedWeb`, `CodeCorpus`, `LicensedBooks` and `VideoAndAudio` are
each unlocked by a node that unlocks two or three at once, so `GateForData` finds no single gate for
them and `HasResearch(None)` is true. The cash route the design always had exists for exactly those.

**`TryAdoptArchitecture` stays unreachable, and the reason is structural.** Every family except the
starting dense transformer is gated by a node, and completing that node grants the family directly,
so there is no state in which the method can succeed. It is not missing a button; it is missing an
ungated family. Left in place, because the day one is added it is the mechanism that sells it.

**The dead finding did not survive being checked:**

- `CompetitorAgent.TryGetLiveModel` **has a caller**: `CompetitorField.cs:156`. The sweep that
  reported it dead did not read the whole repository. Not deleted.

Everything else the sweep flagged is `ServerHall` and `ServerStock` plumbing, called by
`CompanySimulation` rather than by the interface. That is the intended layering: the hall owns
placement, the simulation owns the till.

## The sweep's blind spot, found 2026-09-05

An outside pass reported **`TryAnswerSmearThreat` as having no caller anywhere**: a letter in the
inbox with a countdown printed on it and no button. It is a good finding shape and it is wrong, and
the reason is worth writing down here because this sweep will produce it again.

The route is **inbox → `TryActOnMail` → `AnswerThreatLetter` → `TryAnswerSmearThreat`**. Nothing in
`Scripts/UI/` names the method, so searching `UI/` for it comes back empty even though a player can
press the button. **The entry point and the destination are two different public methods**, and this
sweep only sees the ones the interface names.

The same shape produced the `TryGetLiveModel` entry above. Two false positives from one blind spot is
a property of the method rather than bad luck: it proves a name is mentioned, never that a click
arrives.

What the finding was right about is that **nothing had built the screen with that letter in it**.
`MailScreenReachTests` now does, and walks every `MailKind` failing any letter that reports
`NeedsAnswer` and draws no control. That is the check this sweep cannot do.

**Closed the same day:** `GameShell.recentEvents`, a sixty-item buffer appended to and trimmed and
never read anywhere in `Scripts/` or `Tests/`, is deleted.

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

- **`Cards/chip_model`** — named by `UpgradeGridPanel`, absent from disk. **Reclassified on
  2026-09-04 as an art hook rather than a defect.** The fallback beside it draws a die with contact
  rails in USS and reads as a chip; it is better than a generated placeholder would be, and the
  `Resources.Load` is the line that lets the author drop real art in without touching code. Tracked
  in `Docs/NeededGraphics.md`, which is where art gaps belong.

`Docs/NeededGraphics.md` is the generated list and stays the source of truth for art gaps.

---

## 5. Catalog members nothing uses

- **`CompetitorStrategy.FastFollower` is assigned to no lab.** Documented previously and still
  true: nothing in the game runs that brief. Fourteen labs, six strategies, and this is the one
  nobody has. **Still the author's call and deliberately left alone on 2026-09-04**: assigning it
  changes how a real rival behaves for fourteen years of campaign, which is a balance decision and
  not a cleanup, and it would move `PlayabilityTests` on the eve of a long playtest.
- `LabTrait.Imitator` was removed for the same reason and its enum value is left as a documented gap.

---

## 6. Public constants nothing reads

**Four of these six were wrong, and the reason is worth more than the finding.** The sweep read
`Scripts/` and stopped. `Editor/` is a first-class part of this project - it builds the city, the
basement and the office - and it reads four of the constants listed here twenty times between them.

| Constant | Verdict on 2026-09-04 |
|---|---|
| `CompanySimulation.ReputationDailyDecay` | dead, superseded by `Standing.DailyDrift`. **Removed** |
| `CompanySimulation.ReputationServiceGain` | dead, superseded by `Standing.ServiceGain`. **Removed** |
| `GrantCatalog.OfferOpenDays`, `MostOpenOffers` | dead: an offer board that lapses, and the flow has neither. **Removed** |
| `StaffCatalog.DiminishingReturnsAfter` | dead, superseded by `SaturationMultiplier`. **Removed** |
| `BasementFloor.CeilingHeight` | **read twice** by `Editor/BasementBuilder`, which builds the walls from it. Kept |
| `CityLayout.SeaLevel`, `HeightmapResolution`, `SplatResolution` | **read eighteen times** across the terrain builder, the dressing builder and the flight. Kept |

The removals were each re-checked against Scripts, Editor and Tests together before anything was
taken out, and the script refused to write until every name had exactly one reference left: its own
declaration.

**The lesson for the next sweep: a repository is not `Scripts/`.** An audit that reads part of the
tree reports live code as dead with exactly the confidence it reports dead code as dead.

---

## What this pass changed

- `LocalisationCoverageTests` — closes the 368-key blind spot.
- `EnumIdentityTests` — added the same day after two silent enum-value collisions in one session
  (`GeneralIntelligence` over `ShardedOptimizerStates`, `StateProgramme` over `GrantRepaid`). Both
  are written into saves, where a collision is not a bug but a format in which one number means two
  things. Sixteen enums are now walked.

## What is left, in order of what it costs a player

Updated 2026-09-04, after working through the list.

1. `CompetitorStrategy.FastFollower` runs for nobody. **A design decision, not a defect**, and it is
   the author's.
2. `TryAdoptArchitecture` cannot succeed, because every family is gated and the gate grants it. The
   fix is an ungated family, not a button.
3. Phrases and selectors nothing names by a literal. **The published figures of 223 and 183 do not
   survive re-measurement and neither does the method.** Counted across Scripts, Editor and Tests on
   2026-09-04: 609 of 1,985 English keys and 120 of the sheet's class selectors are never named by a
   literal.

   That larger number is the point. A key reached as `KeyFor(id) + ".desc"` is invisible to this
   instrument exactly as it is invisible to `LocalisationTests`, and that shape covers every node
   description, every grant name and every technology note - hundreds of keys that are alive. So the
   count is an upper bound on an upper bound, and **deleting on it would take the research tree's
   descriptions out with the rubbish.** Nothing is removed until there is an instrument that can
   follow a stem, which is the same instrument `LocalisationCoverageTests` would need.

### Closed on 2026-09-04

- The corpus cash route has a control, and the DATA stage names what the company does not own.
- Five genuinely dead constants removed; four wrongly listed ones kept and the entry corrected.
- `CompetitorAgent.TryGetLiveModel` is not dead. It has a caller.
- `Cards/chip_model` reclassified as an art hook with a working fallback.
