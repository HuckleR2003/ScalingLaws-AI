# Simulation system audit

Written 2026-08-04, before the Foundation and Scale integration work.

Status vocabulary:

- **COMPLETE** works, is connected, has tests, player and rivals share the rule
- **SHALLOW** works but the numbers barely move an outcome
- **DISCONNECTED** exists and is computed, but nothing downstream reads it
- **EXTEND** correct as far as it goes and has to grow for this work
- **MISSING** not present

---

## Mechanism map

| Mechanism | Owner | Persisted | Status | Note |
|---|---|---|---|---|
| Scaling law / capability | `ScalingLaw` | no | COMPLETE | Corrected Chinchilla fit. The one quality formula. Do not add a second. |
| Training projection | `TrainingProjection` | in-flight run only | COMPLETE | Separate type from the outcome, by design. |
| Finished outcome | `CompanySimulation.CompleteRun` | shelf | COMPLETE | Rolls near the projection, never on it. |
| Model creation | `ModelBlueprint` | run | EXTEND | Carries type since v12. Needs family. |
| Architecture | `ArchitectureCatalog`, `ArchitectureDesigner` | custom families | COMPLETE | Six public plus in-house designs. |
| Model families (product line) | none | no | **MISSING** | `ArchitectureBlueprint.BaseFamily` is an *architecture* lineage, not a product line. Different subject. |
| Model upgrades | `ModelUpgradeProject`, `ModelTraitSet` | yes | COMPLETE | Par-relative, decays. |
| Datasets | `DatasetCatalog`, `DatasetBlend` | owned mask | COMPLETE | Quality feeds the projection. |
| Research | `ResearchTree`, `ResearchProject` | unlocked set | EXTEND | Model types hang off it since v12; the type line needs to be a real progression. |
| Compute pool | `ComputePool`, `ComputeProfile` | assets + rented PF | COMPLETE | |
| Hardware generations / ageing | `HardwareCatalog`, `HardwareValuation` | purchase dates | COMPLETE | Two decay tracks. The spine of the game. |
| Hardware bottlenecks | `ComputePool.BuildProfile` | derived | COMPLETE | Balance factor, scaling efficiency, inference utilisation. |
| Serving | `CompanySimulation.ServeMarket` | no | COMPLETE | One serving cost path. Do not add a second. |
| Pricing / monetization | `MonetizationPolicy` | yes | COMPLETE | Free tier, subscription, marketing. |
| Demand | `MarketModel.DemandOn` | no | EXTEND | One global Gompertz pool. Authoritative total; needs splitting by segment. |
| Market share | `MarketShareModel` | no | **EXTEND, and the important one** | Multinomial logit, recomputed from scratch every day. **Share teleports to equilibrium.** No users, no inertia, no segments. |
| Audience segments | `AudienceCatalog` | no | DISCONNECTED | Five segments with curves to 2036. Nothing consumes the shares except the type reach factor. |
| Model types | `ModelTypeCatalog` | v12 | SHALLOW | Reach and tolerance are real but collapse to two scalars on a global pool. |
| Competitors | `CompetitorField`, `CompetitorAgent` | yes | **EXTEND** | Agents have capability, brand, price, release timing. **No type, no scale, no serving burden, no users.** They are scored by a different, thinner rule than the player. |
| Competitor strategies | `CompetitorStrategy` | yes | SHALLOW | Affects release timing only. |
| Intelligence | `IntelligenceService` | yes | COMPLETE | Paid signals, can be wrong. |
| Funding | `FundingMarket`, `CapTable` | yes | COMPLETE | |
| Debt | `LoanBook`, `LoanCatalog` | yes | COMPLETE | |
| Valuation | `CompanySimulation.CurrentValuationUsd` | derived | COMPLETE | |
| Reputation / brand | `CompanyState.Reputation` + contributions | yes | COMPLETE | |
| Founder traits | `FounderProfile` | yes | COMPLETE | |
| Founder skills | `SkillSet` | v10 | COMPLETE | Seven, baseline 20 is neutral. |
| Region / country | `WorldRegionCatalog` | v11 | COMPLETE | Four axes, all land on existing rules. |
| Safety / incidents | `IncidentModel` | v8 | COMPLETE | |
| Staff / office | `StaffRoster`, `OfficeCatalog` | v8 | COMPLETE | |
| Ranking | `RankingBoard` | no | COMPLETE | |
| Saves | `SaveData` v12, `SaveMigration` | yes | COMPLETE | One step per version, loop runner. |

---

## The three findings that drive this work

### 1. Market share teleports

`MarketShareModel.PlayerShare` is a pure function of today's models. Ship a better model on Tuesday
and you own the market on Tuesday. There is no user base, no switching cost, no momentum, and
therefore **no reason for a segment to behave differently from any other segment**. This is the
single biggest gap between what the game claims and what it computes.

### 2. Competitors are scored by a thinner rule than the player

`RivalModel` is capability, brand, price, release date. The player's model additionally carries
architecture, active parameters, traits, efficiency, serving cost and type. The logit compares them
as if they were the same kind of object. Rivals cannot be reached by any mechanic that reads a
property they do not have, which quietly makes several player-side systems player-only advantages.

### 3. `AudienceCatalog` is disconnected

Five segments with fifteen years of curves, consumed by exactly one thing: a scalar reach ratio.
The segments never actually hold users, so "who is buying" is not a question the simulation can
answer, and the Foundation screen has nothing truthful to display.

---

## Parameters that look cosmetic today

| Parameter | Why it is weak |
|---|---|
| `CompetitorStrategy` | Only shifts release timing. Never changes what a lab builds. |
| `ModelType` | Two scalars applied to a global pool. Cannot produce a niche. |
| Audience `WillingnessToPay` | Folded into one tolerance number, invisible per segment. |
| Model `Traits` beyond capability | Feed `EffectiveCapability` and nothing segment-specific. |
| Serving burden | Real cost, but no player-facing number and no design pressure at creation time. |

---

## Rules this work must not break

1. One quality formula: `ScalingLaw`.
2. One demand total: `MarketModel.DemandOn`.
3. One serving cost path: `ServeMarket`.
4. Projection is never the outcome.
5. `Simulation/` never imports UnityEngine.
6. Anything persisted gets a one-step migration and a test.


---

## Bugs found while integrating (2026-08-04)

The segmented market made rival quality feed the player's demand directly for the first time. That
exposed four things the save had never written down, all in `CompetitorAgent`, all pre-existing:

| Field | What it is | What was lost |
|---|---|---|
| `drift` | capability crept since release | a restored lab handed back a model it had already improved on |
| `pendingCapabilityAdjustment` | the roll already made against the next release | re-rolled, so the plan changed on load |
| `pending` | the release being worked toward | **the whole thing.** Past the end of the reference table a lab invents its next model with a random gain; that invention was never saved |
| `WaitingFor` | which accelerator generation a patient lab holds out for | a waiting lab forgot what it was waiting for |

All four are now written and restored, plus the plan queue length, which is recorded rather than
reconstructed by date because inferring it was wrong in both directions.

### Still open

`PlayabilityTests.AYearFourSaveRunsIdenticallyThroughYearFive` still fails on its last assertion.

Everything on the player side now replays bit-identically across a save: date, cash, lifetime
revenue, operating cost, tax, fines and capability all match exactly. **The rival frontier does
not**: 77.358 continuous against 78.315 restored, stable across the last three fixes.

The four fixes above each moved the number (73.815, then 75.892, then 78.315) and then it stopped
moving, which says there is at least one more piece of agent state that is not written down. It has
not been found yet.

**The assertion has deliberately not been loosened.** A red test naming a real bug is worth more than
a green suite that lies, and this one is doing exactly the job it was written for.
