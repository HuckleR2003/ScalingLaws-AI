# The research tree: what it covers, and what it could

Written 2026-09-04, as a proposal to argue with rather than a plan to execute.

---

## What is there now, measured

55 nodes.

| Era | Nodes |
|---|---|
| Foundations | 17 |
| Scaling | 17 |
| Autonomy | 13 |
| Superintelligence | 3 |
| Statecraft | 5 |

| Track | Nodes |
|---|---|
| Capability | 30 |
| Model improvement | 13 |
| Safety | 12 |

They hand over: 7 model traits, 5 architecture families, 4 corpora, 2 compute tiers.

---

## The finding

**Every node in the tree researches the model.** Bigger, cheaper, safer, better shaped, or a new
family to build it from. That is one system, deeply covered, and it is the system a player spends
the least clock time in: a training run is a decision made in a few minutes and then waited on for
two hundred days.

Six systems the player is in constantly have **no research behind them at all**:

| System | Nodes today |
|---|---|
| The server room: racks, cooling, heat, power | none |
| Rented capacity and hosting packages | none |
| Staff, hiring and payroll | none |
| Intelligence and rivals | none |
| Finance, debt and grants | none |
| Model versions and the release ladder | none |

Serving is the one non-model system with any coverage: quantised serving and speculative decoding
sit on the model improvement track and lower what a token costs to produce.

**That is the answer to "what could we research next".** Not more nodes about the model. The tree
should reach the parts of the game the player actually touches every day.

---

## The rule any new node has to obey

Every node in this game already follows it and it is what keeps the tree from becoming a shopping
list:

> A node moves a number that already exists. It never introduces a mechanic.

`ScaleCeiling` raises a cap the slider already had. `ArchitectureCeiling` opens travel on sliders
that were already drawn. The safety modules lower a risk that was already being rolled. If a
proposed node needs a new system underneath it, that system is the work and the node is decoration.

So each proposal below names **the constant it moves**. If a proposal cannot name one, it is not
ready.

---

## Proposals, by system

### 1. The server room

The room is a real mechanic with real decisions and nothing in the tree touches it. A room filled in
2023 is throttling by 2027 and the only answer is a fan in a slot.

| Node | Moves | Why it is a decision |
|---|---|---|
| **Liquid loops** | `ServerRackCatalog.ThrottlePenalty` down for immersion cabinets only | Makes the dearest cabinet the one that ages best, which is the trade it should already have |
| **Airflow modelling** | `ServerHall.HeatRatio` denominator up by a small share | Buys a slot back in every cabinet at once. Wide and shallow, against the fan's narrow and deep |
| **Own substation** | the domestic tariff (0.19/kWh) toward the industrial one (0.058) | The basement's power bill is the one cost that scales with the thing the player is proudest of |
| **Rack telemetry** | nothing directly; shows the heat number before a card is fitted | A node that buys information rather than a number. The tree has none of these and it should |

### 2. Rented capacity

The rent slider is the control that bills every day whether or not anything is training, and the
one the tutorial warns about by name.

| Node | Moves | Why it is a decision |
|---|---|---|
| **Spot scheduling** | `Market.RentPricePerPetaflopDayUsd` down, `ComputeProfile.UtilizationCeiling` down with it | Cheaper capacity that is less reliably there. A real trade rather than a discount |
| **Reserved contracts** | `HostingCatalog` package reservation up a step | Deepens the three packages instead of adding a fourth |
| **Multi-cloud** | removes part of `LocalCompetitionMultiplier`'s effect on hardware price | Ties the map choice to something the player can escape rather than live with forever |

### 3. Staff

The role weights arrived this week and there is nothing to research about people at all.

| Node | Moves | Why it is a decision |
|---|---|---|
| **Structured onboarding** | `Loyalty.TenureYearsToFull` down | The company that hires fast currently pays for it forever |
| **Internal mobility** | lets one hire change role for a fee | The only node here that adds a verb; worth arguing about |
| **Research culture** | `ResearchBudget.PointsPerDayPerScientist` up, capped | Makes a research-heavy payroll a strategy rather than a preference |
| **Remote practice** | `StaffRoster` seat cost per head down | Attacks the desk cap, which is the hardest wall in the early game |

### 4. Intelligence

Three memberships, no research. A lab that studies its rivals should get better at it.

| Node | Moves | Why it is a decision |
|---|---|---|
| **Signal processing** | `IntelligenceService` error band narrower | The product being sold is being wrong; this buys less wrong |
| **Open source watch** | one free membership's worth of signal | Free information against paid, which is the whole shape of that screen |

### 5. Finance

| Node | Moves | Why it is a decision |
|---|---|---|
| **Audited books** | `LoanDefinition.MonthlyCommissionRate` down across the board | The fee nobody notices until they have four facilities |
| **Grant office** | `GrantCatalog.MostOpenOffers` up | More on the board at once, which is more to choose between rather than more money |

### 6. Versions

`ReleaseLine` is one of the best mechanics in the game and nothing researches it.

| Node | Moves | Why it is a decision |
|---|---|---|
| **Staged rollout** | `ReleaseLine.DayOneAdoption` up | Move people onto a new version faster, which is only good when the version is good |
| **Long term support** | `RetireAfterVersions` up | Keeps old versions on sale. A hoarding strategy the market currently forbids |

---

## Where they go

**Not a sixth era.** The eras are a calendar and have to stay one: Foundations is 2022, Statecraft is
2029. A node about cooling belongs in whichever year the technique was real, which for most of these
is 2023 to 2026, so they land in Scaling and Autonomy and make the middle of the tree the fullest
part rather than the thinnest.

**A fourth track.** `Capability`, `ModelImprovement` and `Safety` are all about the model. The
proposals above are about the company, and mixing them into the existing three would say they are the
same kind of decision. `ResearchTrack.Operations` is the name I would use.

That is one enum value, appended, never renumbered, and a fourth band under each era.

---

## The Civilization question

The author asked for connected nodes: a node that visibly requires two others, with lines between
them.

**The data is already there.** Every node carries `Prerequisites`, and `ConsistencyTests` already
proves the chain is acyclic and correctly dated. What is missing is only the drawing: the tree renders
each era as a row and never draws an edge.

Two honest options:

1. **Lines between the pips as they are laid out now.** Cheap, and the layout was not designed for it,
   so half the lines would cross each other. It would look like a diagram of a mistake.
2. **Lay the era out by depth first, then draw.** Each node's column is its longest chain from a root,
   which is one pass over the prerequisites. Nodes with no prerequisite sit at the left, everything
   else sits one column right of its deepest parent, and the lines then mostly do not cross because
   the layout was chosen to make that true.

The second is the real answer and it is a day of work rather than an afternoon: the layout pass, the
edge drawing with Painter2D (USS cannot draw a line between two elements), and the zoom and pan the
map already has so a fifty node era is navigable.

**It should wait until the nodes above exist.** Laying out a tree and then adding fifteen nodes to it
means laying it out twice, and the second time with the layout already committed to.

---

## What I would do first

1. The `Operations` track and the four server room nodes. It is the system with the most decisions
   and the least support, and the player is in it constantly.
2. The three staff nodes, because the role weights just landed and there is nothing to build on them.
3. Then the Civilization layout, once the middle of the tree is worth navigating.

Nothing here is committed. Every row is a proposal with a number beside it so it can be argued with
on the numbers rather than on the idea.
