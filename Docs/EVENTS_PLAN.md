# City events: venues, stands, bidding

The design for the thing the map has been pointing at since the sites were drawn. Written before any
of it is built, the same way `RELATIONS_PLAN.md` came before `LabAlliances`, and for the same
reason: this one has a mechanism in it that the project's own rules say should not exist, so the
argument has to be settled on paper rather than discovered half way through.

Asked for on 2026-10-24.

---

## What is already here, measured rather than remembered

**Six venues exist on the map and nothing books any of them.** `MapSiteCatalog` carries
`MapSiteKind.EventVenue`, six entries, every one with `systemExists: false`, which is the flag that
says the pin is drawn and the system behind it is not built.

**Seven events are named, and they are named only in prose.** The venue blurbs mention the AI
Frontier Expo, the Global Model Awards, the Compute & Infrastructure Expo, the Creator Intelligence
Festival, the Responsible AI Forum, the Model Research Summit and the Capital & AI Forum. Grepped
for across the whole of `Scripts/`: **every one of them appears in `MapSiteCatalog.cs` and nowhere
else.** They are a design somebody wrote down inside a description field.

**There is a paid news desk named after events that has never carried one.**
`NewsSection.EventHunter` is described in its own enum as "what is coming and when", it is sold by
National Press for $20,000 a month, and it is gated behind a TrendSearch membership as well, so it
is the most expensive thing to unlock on the news screen. What actually reaches it is
`IntelSignal` at `IntelTier.NationalPress`, which is a **hardware launch rumour**. A player who buys
both memberships to open EVENT HUNTER gets advance warning about accelerators.

So the honest summary is that events are unfinished rather than missing, and the half that exists is
the half that is hardest to change later: the places, their districts and their positions on a map
the author has already drawn and furnished.

---

## The one rule this feature argues with, and how it is settled

`CLAUDE.md` says rivals carry no economy, and it says why: giving every lab a fleet and a payroll
would mean inventing two dozen uncheckable numbers per company for a simulation nobody sees inside
of. An auction needs bidders with money, so taken plainly, bidding cannot be built.

**It can, because a bidder does not need a budget. It needs a willingness.**

A lab's willingness to go one more round is a **pure function of what the game already knows**: the
users the market says that lab holds, the price the market says they charge, how recently they
shipped, and how well this venue's audience matches what they sell. All four are read, none is
invented, and the whole thing replays identically from the seed. That makes it the **seventh derived
mechanism** in this project, after `RivalExpansion`, `LabTraits`, `WorldEventCatalog`,
`SafetyRecord`, `RoomUpgrades` and `FastFollower`, and it follows their rule exactly: no save field,
no migration, and it cannot drift from the news item that announced it.

What a rival does **not** get is a bank balance that an auction could drain. Fatigue is the brake
instead, and that is the author's own framing: a longer auction tires a bidder and raises the chance
they let it go. So the question an auction answers is never "who has more money", which this game
cannot answer, but "who wants it more and for how long", which it can.

**Written down because it will be asked again:** if a later session reads the no-economy rule and
proposes giving labs an events budget, the answer is no, and this paragraph is why.

---

## The venues, and the biggest crowd is deliberately not the best address

Six, matching the six pins that already exist, with their districts and blurbs taken from the map
rather than rewritten.

| Venue | District | Average visitors | Prestige | Stands |
|---|---|---|---|---|
| Valley Plaza | silicon | 42,000 | **1.00** | 3 |
| Terrace Park | innovation | 18,000 | 0.86 | 3 |
| Civic Square | civic | 12,000 | 0.80 | 2 |
| Bayview Park | park | **85,000** | 0.72 | 8 |
| Media Plaza | media | 24,000 | 0.68 | 4 |
| Bayview Gallery | park | 31,000 | 0.62 | 5 |

**Visitors and prestige pull apart on purpose, and a test has to hold that.** Bayview Park draws
seven times the crowd of Civic Square and ranks below it, because a festival ground and a civic
square are different kinds of room. Valley Plaza is the best address in the city at half the
footfall of the park. This is the same shape as `NoChannelIsSimplyBetterThanAnother`: the moment one
venue is both the biggest and the most prestigious, six places collapse into one and the choice
stops existing.

**The two figures do different jobs**, which is what keeps them from being one number with two
labels:

- **Visitors** drive how many fans arrive, a little reputation, and the chance of a `Viral` result.
- **Prestige** drives how much reputation is earned and multiplies the fans.

So a company that needs to be *known* goes to the park, and one that needs to be *respected* goes to
the plaza, and neither is the right answer in general.

**Stand count is the venue's size** and it is what makes an auction happen at all: Civic Square
holds two, so the second lab that wants it is already in trouble, while Bayview Park holds eight and
is usually just a purchase.

---

## Stands: three tiers, and the big one is worse value

The author's figures: each tier is about 60% more effect and 100% more money.

| Tier | Effect | Cost |
|---|---|---|
| I, small | 1.00 | C |
| II, medium | 1.60 | 2C |
| III, large | 2.56 | 4C |

**Which means tier III buys 2.56x the result for 4x the price, and that is the point rather than a
mistake in the arithmetic.** It is the same trade the marketing channels make, where the dear
channels buy magnitude now and are poor value per dollar. A company that needs a result at this
event takes the large stand knowing it is paying over the odds for it; a company building slowly
takes the small one. If the tiers were linear the large stand would be strictly correct and the
three tiles would be one tile.

A test holds both halves: every tier up is a bigger effect, and every tier up is worse value per
dollar.

---

## Registering, and why being early is the whole decision

Every announced event carries a **registration window** of its own length. The author's rule, and it
is a good one because it turns a date into a decision:

- **Register early and the stand is simply bought** at the list price for its tier.
- **Register late and the free stands are gone**, so what is left is one contested stand and an
  auction against whichever labs also want it.

That is a timing pressure on a calendar the player can see, which is the spine of this game applied
to something other than hardware. It also means the EVENT HUNTER desk finally earns its name and its
price: paying for advance warning is worth exactly the difference between buying and bidding.

Window lengths vary by event so the player cannot learn one number and stop reading.

---

## The auction

Opened only when the free stands are taken. One contested stand, ascending, against the labs whose
willingness clears the current price.

- **A lab's willingness is derived**, as argued above: market users, price, recency of release, and
  how well the venue's audience matches what they sell.
- **Fatigue rises every round**, so a long auction is likelier to end in somebody dropping out than
  in a higher price. The author's framing, and it is also what stops a rival with a strong quarter
  from being unbeatable.
- **The player can walk at any round** and pays nothing, which is what makes the first bid safe to
  make.

**What must be tested:** a lab doing badly does not bid, a lab doing well does, the auction
terminates, and bidding every round against a tired field eventually wins. Also that registering
early is cheaper on average than bidding, or the window carries no decision.

---

## What the player sees

### The tag in the news

Every event story carries a **rectangular tag with the venue's name on it**, coloured by district,
modern and flat. Hovering a story lights it; clicking it opens the map with that venue marked.

That is the half that makes the whole feature findable, and it is cheap: the stories are already
filed through `NewsDesk`, which translates and never decides, so an event story is one more thing it
translates.

### The venue window

In the order the author asked for it:

1. A **panoramic banner**, with a placeholder plate until the art exists. Every loader in this
   project falls back rather than failing, and `Docs/NeededGraphics.md` is generated by walking
   `Resources.Load` calls against what is on disk, so a missing banner will be on that list.
2. The **venue name**, large and heavy.
3. The **organiser**, in cyan, as a subtitle.
4. **Average visitors and prestige on one line**, prestige as a bar, each saying what it does rather
   than only what it is.
5. **Three stand tiles on one line**, I / II / III, each with a picture, its effect and its price.
6. One button at the foot, **grey until a tier is picked and then strongly orange**: GET
   INFORMATION. Pressing it runs a three-dot animation for about a second and then reveals how many
   stands are free and what the field looks like.

**The reveal is a reveal rather than a reading**, and that is a design decision worth recording: the
free-stand count and the rival interest are information the EVENT HUNTER desk sells. Showing them
before the player has chosen a tier would make the paid desk worthless.

### The end of the event

A window that arrives **during the day the event closes**, not on the next tick, because it is an
occasion and the project already rations the gold notice for those.

- A **cutscene plate**, placeholder now, for a short animation of people at the stand.
- A **summary**: fans gained, reputation gained, and, when it happened, **VIRAL**, stated as a
  result rather than a number.
- One short line of comment from the stand, written per venue so the same event does not read
  identically twice.

---

## What is open and is the author's call

1. **Whether an event can be lost.** Nothing above costs the player anything except money and the
   stand. A bad showing that costs reputation would make the choice sharper and is not in the brief.
2. **Whether rivals' results are reported.** Knowing that a lab took the large stand at the park and
   went viral is a reason to read the news; it is also a second simulation to keep honest.
3. **Whether the player can host their own event.** The map has the venues for it. Out of scope
   here.
4. **Attendance cost beyond the stand**, such as travel or staff taken off the desk for the week.

---

## Build order

1. `Data/EventVenueCatalog.cs` — the six venues, their figures, their stand tiers. Pure data.
2. `Data/CityEventCatalog.cs` — the seven named events, each tied to a venue, with its month and its
   registration window.
3. `Simulation/EventStand.cs` — effects, the viral roll, its own `DeterministicRandom` stream.
4. `Simulation/EventAuction.cs` — derived willingness and fatigue.
5. The news tag and the map mark.
6. The venue window.
7. The closing window.

Steps 1 and 2 are testable with no interface at all, which is the project's rule: simulation before
UI, balance proven in tests first.
