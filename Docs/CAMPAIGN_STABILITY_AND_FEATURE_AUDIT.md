# Campaign Stability and Feature Audit

Audit date: 2026-08-07.

This is a delivery order, not a wishlist. The game already has more simulation than its current
screens expose. The next work should make the existing decisions legible and prove that a campaign
survives the full intended arc before adding a second economy.

## What already carries the game

The core decision is intact: upgrades are timed, not simply bought.

- Hardware has dates, delivery lag, depreciation, support bottlenecks and a rent versus own trade.
- Training has a real compute-optimal shape, data limits, architecture efficiency and an uncertain
  result that is separate from its projection.
- Release timing matters because a completed model sits on a shelf while market par rises beneath it.
- The market has capability, brand, price, ageing, specialised audiences and serving capacity.
- Research, funding, debt, staff, skills, safety and geography all land on existing rules rather
  than becoming detached bonuses.
- Rivals are stateful agents. They follow the known timeline at first and continue after it.
- Saves carry migrations, the deterministic random state, the revenue window and in-flight work.

These are not places to add another percentage modifier. They are the systems to surface in play.

## Five-year risks now guarded

The previous playability floor ended at four years. That was enough to establish the opening and
mid-game, but not enough for the post-reference period, late audience shifts or long-lived saves.

`PlayabilityTests` now adds two ratchets:

1. A competent scripted player must remain solvent, commercially active and within reach of the
   frontier after five calendar years.
2. A real year-four save must produce the same state after one more year as the uninterrupted
   simulation.

The tests deliberately check state that players feel: cash, revenue, operating cost, tax, fines,
capability and rival frontier. A test that only proves a save deserialises is not enough.

## Delivery order

### P0 - Finish the stability harness

- Run the new EditMode suite whenever Unity has a valid local licence.
- Add a five-year seed matrix after the first baseline is stable. Use several fixed seeds, not random
  seeds chosen at test time, so every failure reproduces.
- Add an invariant check at each year boundary for finite numbers, legal ranges and valid save data.
- Keep the five-year test independent from the optimal player. It is a floor for an ordinary player,
  not a proof that every strategy must win.

### P1 - Expose existing decisions

- Build the compute and hardware page before another economic system. The simulation already has the
  buy, sell, delivery and tier rules, but the player cannot inspect or use them through the game.
- Build the staff and office page as a causal view. Every employee card should say which live rule it
  changes and show the next diminishing-return threshold.
- Add a campaign ledger with annual snapshots: cash, burn, revenue, fleet value, market share,
  frontier gap and dilution. This makes a five-year story readable without turning the game into a
  spreadsheet.

### P2 - Make timing visible

- Add a frontier calendar. It should show known hardware and rival release windows, label projections
  as projections and state what the current plan loses by waiting.
- Add a model postmortem when a model is released or retired. It should explain whether the company
  lost on capability, price, serving cost, safety or market timing.
- Add an end-of-quarter board memo that reports only changed facts and the largest pressure for the
  next quarter. It should never prescribe one correct move.

### P3 - Distinctive features worth building

- **Counterfactual memo:** after a major decision, show one deterministic alternate path such as
  "renting for 90 more days would have preserved cash but missed the release window". Use the saved
  seed and the existing pure simulation. This teaches the game through the player's own run.
- **Commitment ledger:** every active programme gets a date, daily cash burn, compute share and the
  opportunity cost of delaying the other programmes. This turns hidden queue contention into a
  deliberate portfolio decision.
- **Industry memory:** quarterly press cards should react to recorded events, not random flavour.
  A debt default, safety incident, missed demand or well-timed release should alter the wording and
  the player-facing explanation of reputation.

## Do not add yet

- A second currency, a generic XP tree or idle rewards. Each weakens calendar pressure.
- Random incidents without a visible cause. The safety model already has a causal chain and should
  remain the standard.
- Forecast data presented as historical fact. The existing projection flags must stay visible in UI.
- More rival knobs until the player can inspect why a rival waited, rushed or overtook them.
