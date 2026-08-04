# Scaling Laws - Architecture & Mechanisms Map

One page that answers: "does a mechanism for this already exist?"
Read it BEFORE building anything new. Extend an existing mechanism instead of
creating a second one. That rule is borrowed from PC Workman, where it is the
reason there is one hardware compatibility engine instead of six.

Updated: 2026-08-02. Unity 6000.4.11f1, no render pipeline chosen yet because
there is no scene yet.

---

## What the game is

An AI company tycoon starting January 2022. The player trains models, sells
tokens, and buys compute. The spine of the design is that upgrades are not
purchased, they are timed. Hardware ages, the frontier moves every month, and
capital spent too early sits in an asset losing a quarter of its value with
every successor launch.

There is no guaranteed profit. Price per token falls by roughly half a year,
demand saturates, and a company that ships one model and coasts is bankrupt
inside three years. That is intentional.

---

## Folder map

| Folder | Role |
|---|---|
| `Assets/_ScalingLaws/Scripts/Core/` | Date, clock, random, units. No game rules, no UnityEngine. |
| `Assets/_ScalingLaws/Scripts/Data/` | Pure data libraries plus lookups. No economics, no state. |
| `Assets/_ScalingLaws/Scripts/Simulation/` | The rules. Also no UnityEngine, so tests run in milliseconds. |
| `Assets/_ScalingLaws/Scripts/Persistence/` | Save format, migration, PlayerPrefs I/O. The only folder that imports UnityEngine. |
| `Assets/_ScalingLaws/Scripts/UI/` | Empty on purpose. Nothing here until the simulation is settled. |
| `Assets/_ScalingLaws/Tests/EditMode/` | 60 tests. No scene is loaded by any of them. |

Two assemblies: `ScalingLaws.Runtime` and `ScalingLaws.Tests.EditMode`. The test
assembly is Editor only and constrained on `UNITY_INCLUDE_TESTS`, same setup as
BakaBakeBakery.

---

## Mechanism 1 - One library per subject

Every catalog is a static class of pure data plus lookups, with a
`CatalogVersion` string that saves record. Extend the tables in place. Do not
start a second library.

| Library | Holds |
|---|---|
| `HardwareCatalog` | 22 generations of accelerator, host CPU, node memory and fabric, with ship dates, prices, power and memory. |
| `ArchitectureCatalog` | 6 architecture families from dense transformer to hybrid state space. |
| `DatasetCatalog` | 8 corpora, plus the rule for blending them into one run. |
| `CompetitorCatalog` | 21 real model releases from November 2022 to February 2026, scored on the same capability scale as the player. |
| `ComputeTierCatalog` | 3 compute tiers and their gates. |

Numbers come from public vendor specifications and release dates, rounded.

## Mechanism 2 - The honesty flag

Taken directly from PC Workman's `cpu_temp_src="est"` rule, where estimated
temperatures never enter history or learning.

- `HardwareGeneration.IsProjection` and `CompetitorRelease.IsProjection` are
  true for entries past the point where real products are known. The UI must
  label them.
- `TrainingProjection` is a separate type from `DeployedModel`. A projection
  never becomes a capability. Only a finished run writes
  `DeployedModel.Capability`, and it lands near the projection rather than on
  it (`TrainingOutcomeStandardDeviation`, 1.2 points).
- `CompanyState.BestCapability` reads deployed models only, so a projection can
  never unlock a dataset or satisfy a gate.

**If you add a number that is estimated, give it a flag and keep it out of the
fields measurements live in.**

## Mechanism 3 - The scaling law is one function

`ScalingLaw` is the only place model quality is computed.

    L(N, D) = E + A / N^alpha + B / D^beta

Constants are the corrected Chinchilla fit, not the values printed in the
original paper, because those do not reproduce the paper's own compute-optimal
ratio. These land it at 12 to 32 tokens per parameter across the whole budget
range, which is the lesson the game teaches.

Capability is linear in log reducible loss: ten times the compute buys exactly
10.0 points on a 0 to 100 scale, at every scale. That is what makes the
treadmill honest, and it lines up with the competitor table: the frontier goes
from 31 in November 2022 to 72 in February 2026, which is 41 points, which is
about 10 times more effective compute per year. The index is a coarse relative
class, never a benchmark.

Training cost is `C = 6 * N * activeFraction * D` FLOP, converted to
petaflop/s-days. Guarded by `ScalingLawTests`.

## Mechanism 4 - Hardware loses value two ways

`HardwareValuation` is the only place value falls.

1. Time. Resale halves every `ValueHalfLifeDays` (730 for accelerators).
2. Successors. Each newer part of the same class takes a further 25 percent,
   phased over 180 days rather than all at once, and only counting launches
   that happened after the purchase. A launch already public on the day of
   purchase was priced into what was paid and cannot bite twice.

Buying an H100 at launch in October 2022 and holding to June 2026 returns about
14 percent of the money. Buying a B200 in January 2025 and selling on the same
day returns about 53 percent. That gap is the game.

Separately, `PerformancePerDollarIndex` reports how far the fleet has fallen
behind what money buys today.

## Mechanism 5 - The compute ladder, visible from day one

`ComputeTierCatalog.EvaluateAll` always returns all three tiers, locked ones
included, each with a `LockReason` naming only the requirements that are
actually unmet.

| Tier | Lead time | Gate |
|---|---|---|
| Rented cloud | 0 days | open immediately |
| Colocated servers | 45 days | 5M cash, 1 released model |
| Own datacenter | 300 days | 80M cash, 2 models, 200M lifetime revenue, not before 2024 |

Renting costs the most per FLOP and can be handed back tomorrow. Owning costs
about a third of that and bills whether the cluster is busy or idle. Rented
capacity always tracks whatever the clouds offer, which is the frontier part
delayed by `CloudAvailabilityLagDays` (180). Owned capacity is frozen at the
generation it was bought as.

## Mechanism 6 - Support hardware is not decoration

Accelerators produce FLOPs. Host CPUs, node memory and fabric feed them.
`ComputePool.BuildProfile` computes a `BalanceFactor` as the worst ratio of
support capacity to owned accelerators, floored at 0.15. A cluster with no
hosts runs at 15 percent of its rating.

Rented capacity arrives provisioned, which is part of what the hourly rate pays
for, so the balance penalty applies only to the owned half of the fleet.

Two more efficiency terms, and they are deliberately asymmetric:
- `ScalingEfficiency` costs 3.5 percent per doubling past 256 accelerators.
  Training only.
- `InferenceUtilization` is 0.06 against 0.42 or better for training, because
  serving is memory-bandwidth bound. Serving is embarrassingly parallel, so no
  fabric tax applies to it.

## Mechanism 7 - One tick, one order

`CompanySimulation` is the only thing allowed to mutate `CompanyState`. The
order inside `AdvanceDay` is fixed:

    deliveries land -> run consumes compute -> market splits demand ->
    bills come out -> gates re-checked -> solvency checked

Player actions are `Try...` methods that return false with a reason string
rather than throwing. A refused action never moves money.

Demand is split by a multinomial logit over the player's live models and every
rival's current best, scored on capability, brand, price and age
(`MarketShareModel`). An incumbent term keeps the first model from taking the
whole market. A model nobody replaces decays out on the age term alone.

## Mechanism 8 - Determinism

`DeterministicRandom` (xorshift32) is the only source of randomness and its
state is saved. `UnityEngine.Random` is global mutable state that cannot be
replayed, so the simulation never touches it. Two campaigns with the same seed
and the same inputs produce identical numbers, which
`CompanySimulationTests.TwoCampaignsWithTheSameSeedRunIdentically` checks.

`GameDate` is an integer day offset from 2022-01-01, with ten years of negative
range so a V100 from 2017 still sorts before an A100 from 2020.

## Mechanism 9 - Trait upgrades are measured against market par, not zero

`ModelTraitCatalog` holds eleven upgradeable traits. Each has a `ExpectedLevelOn(date)`, which is
the level buyers treat as normal that day and which rises on its own timer.

**Every trait effect is the difference between your level and par, never the absolute level.** A
model ships at par on every trait, so a fresh model scores no bonus and takes no penalty. Par then
keeps rising. Doing nothing is a slow slide, not a stable position.

Three separate effects, and they do not overlap:
- `CapabilityBonus` moves the quality number
- `BrandBonus` moves how buyers choose between equal models, and goes negative below par
- `EfficiencyMultiplier` moves serving cost, and goes above 1.0 below par

Efficiency and Latency have the shortest expectation timers (140 and 150 days), so a company that
leads on capability and never optimises is losing share and margin at the same time. That is the
intended shape: upgrades are maintenance, not a shopping list.

`ModelUpgradeProject` runs on the same cluster as training and takes both calendar days and compute.
Three programmes at once, maximum.

## Mechanism 10 - Release timing is a decision

A finished run produces a `TrainedModel` on `CompanyState.Shelf`, not a live product.
`CompanySimulation.TryReleaseModel` ships it at a chosen price.

Waiting costs nothing directly. `TrainedModel.ParSlippage` is what it actually costs: par keeps
rising while the model sits, so `CapabilityIfReleasedOn` falls every day. Holding is right when a
rival is about to launch into the same week, or when one more upgrade would change the reception.
It is wrong most of the rest of the time.

## Mechanism 11 - Rivals are agents, not a table

`CompetitorField` seeds `CompetitorAgent` per lab from `CompetitorCatalog`, so an untouched campaign
follows the real timeline. From there each agent can move off plan:

- **Wait.** A `PatientScaler` or `EnterpriseFocus` lab checks whether a new accelerator lands soon
  after its planned launch. If it does, it delays to `launch + HardwareRampDays` and gains
  `PatientWaitBonus` capability. Ship into that window and you lead for a season, then get overtaken
  by something that was waiting on purpose.
- **Rush.** A `FrontierRace` or `FastFollower` lab that the player has beaten by more than
  `RushTriggerGap` may pull its launch forward and pay `RushPenalty` capability for it.
- **Drift.** Between launches capability climbs at `DriftPerDay` up to `MaximumDrift`, because
  rivals run the same upgrade grid. The frontier is a slope, not a staircase.

Past the end of the table each agent generates its own releases on a strategy-dependent cadence.

## Mechanism 12 - Bought intelligence is confident before it is correct

`IntelligenceService` has three paid tiers on retainer. Each has a real `Accuracy` and a separate
`StatedConfidence` that is always higher, by most at the cheap end.

`IntelSignal.IsCorrect` is the truth and is never shown. `Confidence` is what the desk claims. A
rumour tier note is right 58 percent of the time and reads as if it were right 80 percent of the
time. Acting on a wrong signal has to hurt, or paying for information is a tax with a refund.

Signals can describe a hardware launch, a price collapse, a supply squeeze, or a rival deliberately
holding back. That last one is the expensive tier earning its retainer.

## Mechanism 13 - Funding is priced on the story, the numbers and the mood

`FundingMarket.PreMoneyValuationUsd` has two halves plus a multiplier:

    (frontier proximity to the fourth power * 2B  +  annual run rate * 20)  *  sentiment

`FundingCatalog.SentimentOn` is the AI investment cycle, 0.55 in early 2022 up to 2.20 in mid 2025.
It swings four to one across the campaign, which makes *when* you raise worth more than almost
anything else you can do that year. `CapTable` compounds dilution and never reverses it. Rounds
priced under the previous one cost `DownRoundPenalty` extra equity.

## Mechanism 14 - In-house architecture families

`ArchitectureDesigner` is the ONE place a company designs its own family. Same grammar as the model
creator: weight five research directions, set a budget, set a deadline, commit.

Three things govern the result:
- **power** budget and calendar as a geometric mean. Neither substitutes for the other.
- **focus** a programme chasing all five directions gets a fifth of the depth in each.
- **ceiling** the best published family of the day improved by `FieldCeilingMargin`. Nobody invents
  2026 techniques in 2022 however much they spend.

Unlike a training run, the outcome is genuinely uncertain: `Variance` falls from 0.42 to 0.05 as the
programme gets better funded and longer, so a cheap rushed family is close to a coin toss and the
creator screen shows the size of that coin toss before you buy it.

Six slots (`ArchitectureId.CustomFamilyA` to `F`). Families resolve through `IArchitectureSource`,
which `CompanyState` implements: house families first, then the catalog. Everything downstream
treats a designed family exactly like a published one.

Iterating an owned family costs 40 percent less, takes 40 percent less time, and reaches
`IterationDiminishing` of the previous gain. Families plateau; a clean sheet costs full price and
has no such ceiling.

## Mechanism 15 - Rented compute is contracted in petaflops

`ComputePool.RentedPetaflops`, not a unit count.

This was a real bug found by playtesting. With a unit count, the day the clouds moved from A100 to
H100 the bill tripled on its own: same 500 boxes, three times the price, no decision made, and no
extra revenue because serving is capped by demand rather than capacity. Renting is the option that
is supposed to hold no surprises. Ageing belongs to what you own.

## Mechanism 16 - Nobody serves the training artefact

`DeployedModel.ServingDistillationFactor` (0.15) is the share of training-active parameters that
production traffic actually costs.

Also found by playtesting, and it was the difference between a game and a spreadsheet. Serving a
frontier model at full active parameters let a company meet about a tenth of a percent of the demand
it attracted. Everyone was capacity bound, revenue equalled capacity times price regardless of how
good the model was, and capability stopped mattering to the business at all. Real labs serve
quantised, distilled, cached descendants and route most volume to small variants. With the factor in
place, capacity still binds, but capability drives revenue again.

## Mechanism 17 - The technology tree gates everything

`ResearchTree` is the ONE unlock layer. Every architecture, corpus, upgrade line and compute tier
in the game sits behind a node, and nothing can be bought without the node first.

This exists because money is a bad gate. Before the tree, a company bought a sparse mixture the day
it wanted one, and since money compounds the mid game was trivial. A node costs cash, a prerequisite
chain, and calendar, and the calendar is the part that cannot be bought out of. Adding the tree is
what moved the baseline player from finishing four years ahead of the whole field to finishing
behind it.

Four eras, seventeen nodes, ending on `ArtificialSuperintelligence`, which is visible on the board
from day one, carries a warning, and cannot be reached inside the first four years. Two rules that
`ConsistencyTests` pins: every node must be reachable from the root, and no node may open earlier
than its own prerequisite. The second one caught a real dead end where Autonomous Agents opened six
months before the mixture research it depends on.

## Mechanism 18 - The founder and the opening choice

`FounderProfile` folds the chosen traits into one set of multipliers applied at real points:
operating cost, research duration, training throughput, hardware price, data supply, valuation,
reputation gain, brand and a Safety head start. A trait with no downside is a bug, and a test
asserts every one of the eight carries a cost.

`CompanyIdentityCatalog` holds the four opening tiles plus the blank slate. Each sets starting cash,
reputation, corpora, default price and a house trait that stacks on top of the two the player picked.
The tiles carry a colour and a one or two character mark rather than a texture, so the opening screen
reads as four distinct companies with no art to import.

## Mechanism 19 - Debt is the opposite trade to equity

`LoanCatalog` holds four facilities. A funding round costs a permanent slice of the company and
never has to be repaid. A loan costs nothing permanent and has to be serviced daily on a schedule
that does not care whether the quarter went well. That is the entire reason both exist.

| Facility | Principal | Repays | Term | Gate |
|---|---|---|---|---|
| Bridge | 15M | 1.22x | 18 months | 45 percent of the frontier |
| Venture debt | 120M | 1.45x | 4 years | 40M run rate, scaling laws |
| Corporate bond | 900M | 1.62x | 7 years | 400M run rate, datacenter programme |
| Sovereign compute | 10B | **2.25x** | 11 years | 2B run rate, 90 percent of the frontier, recursive self-improvement |

The sovereign programme is the largest single sum in the game and the only one that can end a
campaign by itself: ten billion in, twenty two and a half billion out, and a government that will
not renegotiate.

Arrears are tracked rather than instantly fatal. A lender carries a good company through a bad
quarter and stops after `ArrearsBeforeDefault` days, at which point the default is called publicly
and costs standing that took years to build. Defaulting does not clear the debt.

## Mechanism 20 - Saves, versioning and the migration branch

`SaveStore.Parse` always runs three steps in this order: upgrade, sanitize,
build. A file that cannot be understood starts a new campaign instead of a
corrupt one.

`SaveMigration` holds one method per version step.

- **v1**: compute was two integers, a rented count and an owned count. No
  purchase dates, so depreciation had nothing to read.
- **v2**: owned compute became dated batches. Models went straight to market
  with no upgrade levels, and rivals were a static table.
- **v3**: trait levels, the release shelf, the cap table, rival agent state and
  the intelligence desk.
- **v4**: in-house architecture families and any programme in flight.
- **v5** (current): rented compute contracted in petaflops rather than units.

`UpgradeV1ToV2` reconstructs a batch from the bare count: the accelerator that
was current on the save date, bought half a value half-life earlier.

`UpgradeV2ToV3` does two real pieces of work. Trait levels are set to market par
on each model's own release date, because that is exactly where a v2 model
implicitly sat; setting them to zero would load a saved campaign several levels
behind through no decision the player made. The rival field is rebuilt by
replaying the reference timeline from day zero to the save date, deterministically.
Any deviation the original campaign caused is gone, because v2 never wrote it down.

`UpgradeV3ToV4` has genuinely no work to do and says so rather than being
skipped, so the chain stays uniform. `UpgradeV4ToV5` is the opposite: it converts
exactly, because the save records the date, the date determines which generation
the clouds were renting, and that determines the petaflops.

Every step says what it had to invent in `SaveMigration.LastMigrationNotes`. Each
method moves a file forward exactly one version and the runner chains them, so a
v1 file goes v1 to v2 to v3 to v4 to v5. When v6 arrives, add `UpgradeV5ToV6` and
chain it. Do not edit an existing reader.

`SaveStore.Sanitize` clamps every field on the way in, on the assumption the
file may have been hand edited or written by a build that no longer exists.

---

## Mechanism 21 - Founder skills grow, traits do not

Traits are fixed for the campaign. Skills are the moving half of the same idea: seven of them,
level 0 to 100, all starting at 20 with 200 points to spend at creation at 10 per click.

Twenty is the neutral point rather than the floor, so `EffectAt` measures the distance from it and a
skill the player ignored is a real weakness instead of a bonus they failed to collect. `SkillSet`
exposes one named accessor per skill, each clamped, and those are the only way the simulation reads
a level. Nothing consumes the raw number.

Experience is awarded exclusively on completion: a finished training run, a finished upgrade, a
finished research node, a released model. Never for elapsed days, because a skill that grows while
the player waits turns the game into an idle timer and every timing decision stops mattering.

## Mechanism 22 - Where the company is registered

Three regions on a drawn world map, sixteen countries behind them, chosen once and never moved.
The region is only how the country is found; the country carries the numbers.

Four axes, and every one of them lands on a rule that already existed: hardware price, a tax on
daily operating profit, research duration, and the brand term in the demand split. No country wins
on all four, and the cheapest silicon is never in the emptiest market, which is the trade.

Tax is charged on profit and not on turnover, so a bad year is not made worse by geography. A
country that punished a loss would be a trap rather than a choice.

The map is drawn with `Painter2D` from coarse polygons rather than imported as a texture: nothing
to license, nothing to keep in step with the palette, and it stays sharp at any size.

## Commands

Unity lives in `C:\Program Files\Unity\Hub\Editor\6000.4.11f1`. In PowerShell,
build the path with `Join-Path ${env:ProgramFiles} ...` rather than a literal.

```
-batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testResults TestResults.xml
-batchmode -nographics -projectPath <project> -executeMethod ScalingLaws.Editor.ScalingLawsSceneBuilder.BuildAll -quit
```

Do not add `-quit` to `-runTests`. Unity finishes the import, never runs the
tests, and exits 0.

Two scenes, both generated by `ScalingLawsSceneBuilder`, both in build settings:
`MainMenu` and `Game`. Nothing in them is hand authored, so anything edited in
the scene view is lost on the next rebuild. Scene changes belong in the builder.

## Is it playable

`PlayabilityTests` runs a scripted baseline player, deliberately ordinary, for
four campaign years and asserts it survives, ships models, stays within reach of
the frontier, and beats a company that shipped once and coasted. It is the floor:
whatever a thoughtful human does should beat it.

It found three real problems no unit test would have:
1. Renting by unit count tripled the bill on a generation change (Mechanism 15).
2. Trait decay of 15.6 capability points made a shipped model worthless in two
   years for no decision the player made. Halved (`MaximumShortfallCounted`).
3. Serving at full training-active parameters made market share decorative
   (Mechanism 16).

`FourYearsLeavesTheBaselinePlayerCompetitiveAndNotDominant` is the difficulty
band, and it is an assertion rather than a note. After four years an ordinary
player must be solvent, no more than eight capability points ahead of the
frontier, under 75 percent market share, and short of both the end of the
technology tree and the ASI node. If it starts passing trivially the game has
gone soft. If it fails on the low side the game has gone unfair.

The tree is what fixed the difficulty. Before it, the baseline player finished
four years ahead of the entire field with two billion in the bank, because money
was the only gate and money compounds. Calendar cost cannot be compounded away.

---

## What is deliberately not here yet

Steps 1 to 4 are done, and the first screens of step 5 are up. Left open:

- **Player-designed architecture families.** The biggest remaining gap. Right
  now an architecture is adopted from `ArchitectureCatalog` for cash. The design
  is to mirror the model creator: an `ArchitectureProject` that spends research
  budget and calendar time on chosen directions (sparsity, attention, context,
  serving cost) and produces a custom `ArchitectureDefinition` with rolled stats,
  which then becomes the family every later model inherits from. It wants the
  same treatment the creator got and should not be rushed in beside something else.
- **Staff and offices.** There is no team. Employee skill is what drives review
  scores in Devices Tycoon and it is a natural multiplier on both training
  outcome variance and upgrade speed.
- **Safety incidents.** The Safety trait currently only pays into brand. It
  should also gate a tail risk: a model far below par on Safety can produce an
  incident that costs reputation and triggers regulatory attention.
- **Office and hardware screens.** The compute simulation is complete but has no
  UI. See `UI_RESEARCH_AND_ASSETS.md`.

## Style

Public-facing text, meaning this file and anything that ships with the game:
no em-dashes, minimal emoji, no marketing adjectives, real verifiable numbers,
short plain sentences. Same rule as PC Workman's docs, for the same reason.
