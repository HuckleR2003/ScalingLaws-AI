# Tester reports

The list promised in writing on 2026-10-28, after a report arrived that had already been made once
and lost. **That is what this file is for.** A report that lives only in an inbox gets fixed when
somebody remembers it, and the one that was forgotten came back as "you still havent fixed this".

Rules for this file:

- **Nothing leaves without a line here.** Open, fixed, or declined with a reason.
- **A fix lands with a test**, named in the row, or it is not a fix. The hiring one came back
  because the first fix was a memory.
- **Declined is a legitimate answer** and has to say why, in a sentence somebody else can argue with.
- No email addresses and no personal details: this repository is public.

---

## Francisco, 2026-10-27 and 2026-10-28, against build 0.7.6

Twenty nine messages in eighty one minutes, then two more the next day.

| # | Report | State | Where |
|---|---|---|---|
| 1 | Skill cards say "more lower / less lower". Should read **lower cost / higher cost**, keeping the arrows as the strength indicator | **fixed 2026-10-31** | two written phrases per effect, arrows carry strength only |
| 2 | Emil's fold chevron cannot be found | **fixed, but the fix is UNRELEASED.** He is on 0.7.6 and cannot have it | see below |
| 3 | The phone shows four options while Emil is speaking and gives two | **fixed 2026-11-01** | `PhoneWelcomeTests` |
| 4 | Map filters do nothing. The office-for-rent locator does work | **fixed 2026-11-01** | the two nameless pins were named |
| 5 | Three places on the map need a circle placing | **fixed 2026-11-01** | `MapPinTests`, now green |
| 6 | Rival HQ circles are too faint, one especially | **fixed 2026-10-31** | the column is a share of the building now |
| 7 | Hiring opens on the last selected job rather than the one clicked. **Reported twice** | **fixed 2026-10-31** | `HiringRouteTests` |
| 8 | A renewal offer disappears when you switch tabs, and cannot be renewed | **already fixed, row was stale** | `AllianceTests`; see below |
| 8b | Auto-renew, asked for in the same message | open, **a design question not a patch** | nothing is built. See below |
| 9 | "it doesnt work", 14:31 | **fixed if it was 12**, unconfirmed | one question to him |
| 10 | Entering the create-model tab changes the compute without the compute page being opened | **reproduced and fixed 2026-11-01**, two faults | `RentCeilingTests`, `CreatorTouchesNothingTests` |
| 11 | The lease button on the map does nothing | **fixed 2026-11-01** | same cause as 4 and 5, confirmed |
| 12 | The empty PREVIEW slot on a site card | **fixed 2026-10-31** | no art, no frame |
| 13 | Renting is required even with enough parts bought, or runs take far too long | **cause found, half fixed** | `ComputeTierGateTests`; second half unmeasured |
| 14 | The compute-buying agreement with other companies still does nothing | **he is right, measured** | see below. Needs a decision, not a patch |
| 15 | Late game is unsurvivable: too cutthroat, tokens too cheap | **he is right, and it was already measured in this repo**, see below | `CampaignProbe~/report.md`, `PlayabilityTests` |
| 16 | Model types should have **different market prices**, and quality should let you charge more. Token-usage maths needs revisiting | **half already true, half a real gap**, measured, see below | `CampaignProbe~/report.md` |
| 17 | Port Motors shows a price row with a dash and a dead Buy button | **not a bug**, see below | the card already says the rules are not written |

---

## Three reports, one cause: the pins

Reports 4, 5 and 11 look like three faults in three systems. Measured against `City.unity` and
`MapSiteCatalog` on 2026-10-31:

| | |
|---|---|
| `MapSitePin` components in the city | **91** |
| of those, with a **blank** siteId | **2** |
| distinct places actually pinned | **46** |
| places in the catalog | **48** |

A pin is what `MapSiteBeacons` draws a ring and a column for, and what the card and the filters
read. **No pin means no circle, no card, and nothing for a filter to light up**, which from the
player's seat is indistinguishable from a filter that does not work.

So: **two pins in the city mark a building and name no place**, and they can never do anything.
That is the half that is unambiguously wrong and `MapPinTests` now fails on it.

The two catalog places with no building, `plant.coastal` and `plant.riverside`, both carry
`systemExists: false`, so the test exempts them on purpose. Thirty three sites are marked that way
and putting a marker on each would promise thirty three doors that do not open.

## Named 2026-11-01, and it did not need the editor

The line that stood here said this was five minutes in the editor and could not be done from code.
**That was wrong, and it was wrong because nobody had looked at the two pins.**

They are not stray components on arbitrary buildings. Read out of the scene:

| | pin one | pin two |
|---|---|---|
| GameObject | `Riverside Plant` | `Coastal Plant` |
| `kind` | 4, `PowerPlantStake` | 4, `PowerPlantStake` |
| `category` | 5, `Energy` | 5, `Energy` |
| catalog entry of that exact name and kind | `plant.riverside` | `plant.coastal` |

Both pins already stand on the right building, carry the right kind and the right category, and the
catalog's display names are the GameObject names to the letter. **The only thing missing was the
id**, and the two catalog places with no pin were these two. The match is one to one in both
directions, so there is nothing to choose between.

Written straight into the scene file. The diff is two lines, the `PrefabInstance` count is
unchanged at 9,781, and the file is the same length it was. `MapPinTests` is green, and it is
green because the pins were named rather than because the guard was softened.

**What a player sees now.** Both places carry `systemExists: false`, so clicking one opens a card
with a disabled button and the line *"Surveyed, but the rules behind this place are not written
yet."* That is exactly the Port Motors shape in report 17, which the author has already decided
stays. A power station standing in the city that answers a click with nothing was the worse of the
two states.

## Report 17: Port Motors, and why it stays

The card draws a Purchase price row with a dash, a Buy button that is disabled, and the line
*"Surveyed, but the rules behind this place are not written yet."*

That is the honesty flag working. The row exists so the place reads as something that will have a
price rather than something that has none, the button is off because the system is not written, and
the footnote says so in a sentence. Francisco guessed right that it was intentional.

**The open question is not the card, it is whether unfinished places belong on a tester's map at
all.** Thirty three of forty eight are marked not-written. That is the author's call, not a fix.

### Answered rather than fixed

| Question | Answer given 2026-10-28 |
|---|---|
| Does Venture Darling increase funding? | Yes, x1.30 on the pre-money valuation. The cheque is capped by the round, so it buys less dilution rather than more cash. Costs 5% brand and 8% operating costs |
| What does my own lab give me? | It used to give nothing: lowest reputation, no house trait, same money. **That is no longer true**, see the creator work of 2026-10-31 |
| Do founder and lab traits stack? | They did not: the duplicate was thrown away and three picks became two. **Fixed 2026-10-31** |
| What is RAW data cleaning for? | Nothing. It was strictly worse in every case. **Fixed 2026-10-30**; all three options now do something |
| How do General, Coding and the rest work? | Demand multipliers per audience, with their own serving costs. Table sent |
| OpenSI was missing from the lab answer | **Owed.** He is right and it was not answered |

---

## What is fixed and still needs his eyes

These went out in the work of 30 and 31 October and **none of them has been confirmed by anybody
holding a mouse**:

- The fold chevron (2)
- Founder and company traits no longer collide (his question, above)
- Your own company is now the only one you configure yourself (his question, above)
- RAW, Standard and Aggressive all do something (his question, above)
- Hiring opens on the job you clicked (7)

---

## The one that was lost, and why it is written here

Report 7 was made before 0.7.6, was not written down anywhere, and was therefore not fixed. The
second report said so.

The cause in the code was small: `HiringPortals` keeps a shortlist per channel **and per position**,
and the position was a field only the chips inside that screen ever wrote. `ShowPositionCard` knew
which job the player had clicked and `BuildPositionHiring` took no argument, so it was dropped on
the way through. Every route in now states the job, and `HiringRouteTests` reads the source and
fails if a new one does not.

The cause outside the code was that there was no list. There is one now.

---

## Report 10: measured three ways and not reproduced

`CreatorTouchesNothingTests` opens the creator the way a player does and reads the pool on the
other side. Measured 2026-10-31, all three green:

| Path | Before | After |
|---|---|---|
| Owned hardware, nothing rented, open the creator | 0 | 0 |
| A rent set on the fleet page, open the creator | 640 | 640 |
| Fleet agreed in the creator, rent changed on the fleet page, back into the creator | 150 | 150 |

The third is the one that matters. It is the only path on which the creator writes the rent at
all, and it is the path a player who has started a run is permanently on.

**`SetRentedPetaflops` is the only company-level compute the creator can touch.** Everything
else it writes sits behind an explicit button: adopting an architecture, buying a corpus. So the
figure he watched move was either the blueprint's own compute setting, which belongs to the run
rather than to the company, or it has been fixed since 0.7.6.

**The next step is one question, not a fourth guess.** Which number changed, and from what to
what. Three readings have been eliminated by measurement; a fourth guess is worse value than
asking him.

## The suite is deliberately one test red

`MapPinTests.EveryPinInTheCityNamesAPlace` fails on the two pins with no siteId. **That is the
report, not a broken test**, and it stays red until the scene is fixed in the editor. The other
two tests in that class pass: no pin names a place the catalog does not have, and every place
whose rules exist has a building standing for it.

---

## Reports 13 and 14 are one situation: a player who owns hardware and rents nothing

### 13. The fleet starves its own cards and nothing said so

`ComputePool.BalanceFactor` takes the **worst** of three ratios: host processors, node memory
and cluster fabric, each against the number of owned accelerators. Rented capacity arrives fully
provisioned, which is part of what the hourly rate buys. Owned cards have to be fed.

`MinimumBalanceFactor` is **0.15**, so a player who buys cards and no hosts gets fifteen per cent
of the compute they paid for.

**The rule is right. The silence was the bug.** The factor has been on `ComputeProfile` since the
rule was written and was read by exactly one test and no screen. What the player saw was the
downstream symptom, "45.9% of what you pay for", which is also the thing he screenshotted and
asked "What does this actually mean?" about. **Two of his reports, one cause.**

Fixed 2026-10-31: the profile now names the class that is short and how many cards it can feed,
and the fleet page says it in a sentence. The guard went into `ComputeTierGateTests`, which
already built a starved fleet and checked only the number.

**Still unmeasured:** the second half of his sentence, that the figure in the create-model window
can differ from reality. Same suspected cause, not probed, not guessed at.

### 14. The capacity purchase delivers nothing to the simulation

`AlliedPetaflops()` has one consumer in the whole project:

| Where | What |
|---|---|
| `CompanySimulation.Alliances.cs:418` | the definition |
| `GameShell.cs:3487` | a sentence on the alliance card |
| `AllianceTests.cs:221` | asserts the number is above zero |

**Nothing in `Simulation/` reads it.** The petaflops are computed, printed, and never enter the
fleet. The deal charges its fee, sits on the list as live, shows a figure, and adds nothing to
anything that decides how long a run takes.

That is the tenth complete mechanism in this project with no path into the rules, and it has a
passing test of its own: `AllianceTests` proves the arithmetic works and proves nothing about
whether anybody receives it. Same shape as `ServerHall`, which sat unreachable for months with a
green fixture over it.

**And a second fault in the same method:**

```
most = Math.Max(most, State.Pool.RentedPetaflops * share);
```

Allied capacity is a share of **what you rent**. Rent nothing and the deal is worth zero even
once it is wired up. That is why he said it was fixed at 14:34 and broken again at 15:04: he had
come off the rent in between.

**Two fixes, and only the first is obvious:**

1. Allied petaflops enter `ComputePool.BuildProfile` as a fourth source, the way the basement
   server room already does. Unambiguous.
2. The base stops being the rent. What it becomes instead is a balance decision that lands on
   top of reports 15 and 16, which are about late-game economy, so changing both at once would
   make neither measurable.

---

## Report 8 was two requests and the row only tracked one

### The bug half was already fixed, and this file said otherwise

Measured 2026-11-01. The renewal is carried by `AllianceBadge` for the whole fortnight it stays
open, the badge takes the click and accepts it, `CompanySimulation.OpenRenewals` returns every
lab waiting rather than one, and `State.Renewals` is a list persisted in **save v72**.
`AllianceTests` holds the badge going up when an offer lands and down when it is taken.

The code even carries the report in its own comment: *"the only control that could accept it
existed for the length of one card on the day it arrived: pressing not yet threw away the answer
while the offer went on sitting in the save."*

**So the row in this file was stale, which is the one thing this file exists to prevent.** It was
written as open on 2026-10-31 and the fix was already in. A tracker that is wrong in the
safe direction still costs somebody an afternoon, and if he had been told it was open he would
have been told something untrue.

### Auto-renew is not built, and it should not be built without a decision

There is no `AutoRenew` anywhere in the project. Zero occurrences.

**It is worth saying why this is not just a tick box.** The spine of this game is that upgrades
are timed rather than purchased, and a standing arrangement that renews itself is a cost that
arrives without the player weighing it. Two shapes, and they are different games:

1. **A convenience.** It renews on the same terms and the player can turn it off. This removes a
   chore and nothing else, and the chore is real: fourteen days to click yes on terms that have
   not changed is not a decision.
2. **A commitment.** It renews and charges whatever the balance is, so a company that stopped
   watching pays for an arrangement it can no longer afford. That is a decision, and it is the
   shape the rest of this game is written in.

The second is the better fit and it is also a balance change, which the author has deferred until
the rest is finished. **His call, not mine.**

---

## Report 3: four options offered, two given

The phone's opening screen draws the chat app's home list, **numbered one to four**: Contact List,
Messages, ToDo list, Achievements. A second later Messages lights up on its own and the
conversation opens.

Three of those four name features this game genuinely has somewhere else, and the numbering is the
universal signal to pick one. They are `VisualElement`, so none of them has its own press, but the
handset carries a universal skip on `ClickEvent` which bubbles: **tapping any of the four ran the
skip and opened the conversation.** Choosing Achievements and choosing Contact List produced the
same two buttons.

So it was not a dead control, which would at least read as unfinished. It was a control that
answered every choice identically, which reads as the game ignoring the player.

**Making the rows work was the wrong fix.** This is the first minute of a campaign, inside a
scripted boot of somebody else's phone, and a live Achievements row here drops a new player into a
page of empty trophies before they have a company.

Fixed by making the screen read as what it is. The numbers are gone, because a list without numbers
is a phone's home screen and a list with them is a question. The three rows nobody took fade when
one is chosen, so what is on screen is a selection being made rather than four things being
offered. `PhoneWelcomeTests` reads the source, because the menu is built on the handset's own
scheduler and an EditMode test has no panel for it to ever run in.

---

## Report 13, second half: measured 2026-11-01 and it did not reproduce

His sentence was that the figure in the create-model window can differ from reality. It was left
unmeasured and unguessed on 2026-10-31, deliberately. Traced properly today, and the honest answer
is **the path he describes is sound**, so this is not the fix he is owed.

What was checked, in order, because each one of these has been the bug before in this project:

| Suspicion | What the code does |
|---|---|
| The screen prices a fleet the run will not get | `StartTraining` commits the rent before `TryStartTraining`, and its own comment records that bug being fixed |
| The quoted days evaporate | `TrainingRun` runs two clocks and the safety weeks are a floor. Same, already fixed |
| The run trains on tokens that were never there | the planner trains on `min(requested, supplied)`, and an under-supplied mix **refuses the run** rather than quietly shrinking it |

**So the window and the run agree.** If he saw a number move, it is something this pass did not
find, and the next step is his screenshot rather than a fourth guess. Same answer as report 10, and
for the same reason.

### Two things that did come out of looking

**`TrainingRun.ActualTokensBillions` is a third-shape dead mechanism.** It is computed, clamped in
the constructor, **written to the save file**, and read by nothing anywhere in the project. Its own
summary promises something to the reader: *"tokens the data mix could actually supply, which may be
under what was requested"*. It can never be under what was requested, because a shortfall blocks
the run, and the value stored is the whole supply rather than the amount trained on. A field that
is wrong, saved, and unread.

Not patched today. Deciding whether it should be shown or deleted is the author's call, and it is
exactly what the `Simulation/` sweep agreed on 2026-10-31 exists to surface.

**Three refusals were in English in a Polish game**, sitting beside three that were keyed properly:
the architecture not yet published, the data mix being short, and the run exceeding the day limit.
`Append` joins them, so a Polish player could get a half-translated sentence explaining why their
run would not start. `LocalisationTests` cannot see them because they were interpolated literals
rather than keys, which is the same blind spot that shipped a screen of raw keys once before.
Keyed in both languages now, with invariant formatting on all four numbers.

---

## Report 2 is open again, and the reply sat unread for sixteen hours

He answered the 2026-10-31 mail at 20:37 the same evening:

> regarding the chevron I believe there isnt anything next to his head actually

**That is a confirmation, and it confirms the fix did not work.** This file said "fixed 2026-10-30,
unconfirmed by him" when the confirmation had already arrived and said the opposite. The same
failure as report 8 in the other direction: one row claimed open when it was fixed, this one
claimed fixed when it was not.

**Nobody is going to find this by reading the code again.** On this side the control exists, it is
a 46px pill with a 3px yellow border, a drawn chevron and the word HIDE, it is added to the
speaker block, and it is covered by tests. That is exactly the state the project has been in
seventeen times, and the standing note on it says the lesson in one line: **a geometry test is not
a substitute for looking at a rendered frame.** Asked him for an uncropped screenshot.

**Rendered the same day, and the reading above was wrong.** `ScreenProofTests.TheGuideStrip
DrawsSomethingToFoldHimAwayWith` writes `ScreenProof~/guide_strip.png`. The control is exactly
where every description says it is: a yellow pill with a chevron and the word SCHOWAJ, immediately
to the right of the portrait, beside his head. `speaker` is a row. It was read as a column.

**The real answer is the build, and it was in this file the whole time.** The fix is dated
2026-10-30 and sits in `## [Unreleased]`. His reports are against **0.7.6, released 2026-10-26**.
In the build he actually has, the control is what the changelog entry describes as the fault:
*"a 46px yellow disc with no label beside a 96px portrait"*. So there is something next to Emil's
head on his screen, it just does not read as a control, which is the report.

**Report 2 is fixed and he cannot see it yet.** Nothing to do but ship the build.

Two lessons, and the second one cost a round with the tester:

1. The frame settled in one look what two passes of code reading got backwards. Seventeen times
   now.
2. **Check which build a report is against before treating a reply as a confirmation.** The header
   of this very file says 0.7.6. A fix dated after the build a tester is holding cannot be
   confirmed or denied by that tester, and asking them to look is asking them to waste their time.

---

## Report 9 answered: it was the booking button, and there were two faults

He replied on 2026-11-01: *"When I said it doesnt work, it had a video attached"*, then sent it
again: *"it was referring to the booking button"*. Three seconds, 1366x768, recorded 2026-10-27.

**The guess in this file was wrong.** It read "the PREVIEW slot, see 12" for five days. Asking took
one line and settled it; the guess would have closed the report on the wrong fault.

What the video shows: the city map, the card for Media Plaza, a press on BOOK A STAND, the map
closing, and the office. Nothing opens.

### Fault one, and it is a four line ordering bug

The map holds no reference to the company on purpose, so the press records which venue was asked
about and leaves. The desk opens the window on the way back in. That handover was being run from
**inside `BuildTree`**:

```
AnswerAnyStandRequest();   // switches to the news desk
...
Show(Screen.Site);         // four lines later, puts the office back
```

A frame after that, the stand window mounted itself into a page the player was no longer looking
at. The handover is the one thing in the build that is allowed to overrule the opening screen, so
it has to run after it. It does now, and `StandRequestRouteTests` holds both the order and the fact
that there is exactly one caller: the request clears itself when read, so a second caller would
decide the outcome by whichever ran first.

### Fault two, on the same card, found by looking at the same frame

The card showed a dash for the stand hire and a dash for the capacity. `EventVenueCatalog` holds
24,000 visitors and prestige 0.68 for Media Plaza and derives the hire from them, which is $39,648.
The card had both rows hard-coded to the dash.

Fixed the way the power plant row above it already works, including for a venue whose door is not
open: real figures, action disabled, footnote saying the rules are written and the door is not.
`MapSiteCardTests.AVenueShowsWhatAStandCostsAndHowManyWillSeeIt` asserts against the catalog rather
than against 39,648, so changing the venue changes the expected number instead of breaking a test
that memorised one.

### The footnote was covering for a real bug

Media Plaza carries `systemExists: true`, so the button was enabled and the footnote read *"Not
open yet. The rules behind this place are written, the door is not."* The door was written. What
was broken was walking through it, and a player reading that line would reasonably conclude the
game was telling them so on purpose. **An honesty flag that happens to describe a bug is worse than
no flag**, because it stops the report being made. This one was made anyway.

---

## Report 10: he named the number, and it still does not reproduce

Asked on 2026-11-01, answered the same hour: *"First: the compute I rent from the slider thing."*
So the figure that moved is the rented petaflops. That narrows it and it does not close it.

Four paths measured, all clean:

| Path | Result |
|---|---|
| Open the creator renting nothing | 0 to 0 |
| Open the creator renting 640 | 640 to 640 |
| Fleet committed, rent changed elsewhere to 150, return and refresh | 150 to 150 |
| **Walk every creator page twice with the fleet committed** | **0 movements** |

The fourth is new and was written because the first three all stop at the door. The rent handle is
configured on the COMPUTE page rather than when the screen opens, `ConfigureSlider` rewrites its
range every time that page is built, and a Slider clamps its value into the range it is given. If
that ever clamped, the change callback would fire, set `rentCommitted`, reprice, and repricing
writes the handle to the company.

**It never clamps, because `RentReadout.CeilingPetaflops` takes the company's current rent as a
floor and ratchets.** That was written for exactly this reason and the comment on it says so.

So the hypothesis was wrong, which is the third confident code reading overturned by measurement
in one day. The others were the fold chevron's position and the stand card's dashes being the
whole of report 9.

### What is left, and why it is a question rather than a fifth guess

One thing in the creator still writes the fleet: the unblock button, which raises the rent to
clear a run that cannot start. It is a labelled button the player presses and it says what it
does, so it is not "without the compute page being opened" unless he pressed it without reading
it, which is a different report.

Everything else either describes a plan or is already covered above. **The next step is one
question about when, not which**: what he did in the minute before he noticed. The first question
cost one line and turned "it doesnt work" into a fixed bug the same afternoon, so this is cheap.

**Nothing has been patched for this report.** A fix for a fault nobody can produce is a change
with no way of knowing whether it worked.

---

## Report 8b: auto-renew, built 2026-11-01 in the shape he chose

Asked him which of two versions he wanted. He picked the first and gave a better reason for it
than either option carried:

> I believe the first one is more appropriate as the first cost of setting up the contract being
> like a fee or something, like an advance, and then they get the profit from their shares

So the cash on one of these arrangements is a setup fee, and what the other lab is really in it for
comes after. A company that has paid that once is continuing something rather than buying it again,
which is why rolling over on the same terms is honest and a version that charges whatever the
balance is would not be.

**What was built**

| | |
|---|---|
| Scope | one lab, not the company. Saying yes to Gohere says nothing about anybody else |
| Default | off. Save v77, and a v76 file rolls nothing over, because it was never asked |
| When it cannot pay | nothing is charged, the offer falls back to the table for its fortnight |
| Noise | a sentence in the feed when it rolls over **and** when it could not be paid for |
| Where the switch is | under the list of what you have going with that lab, in the rival panel |

`TakeRenewal` is one body with two ways in, the player pressing RENEW IT and a term rolling over,
because two bodies are two places to get the charge wrong. Same rule as opening the server room.

**The price was not touched.** His sentence reads as an argument for why the same terms are fair,
not as a request for a discount on renewals. Changing what a renewal costs is a balance change and
the author has deferred balance until the rest is finished. If he did mean a discount it is one
constant and one line.

### Two red tests, and both were mine

`NoPolishPhraseTellsThePlayerWhatTheyDidInAGender` caught the new Polish line: it said *"Sam
prosiłeś"*, which addresses a man, and the founder can be a woman. Rewritten impersonally. No
review would have found that, because the sentence is correct Polish.

The other was a sloppy assertion in the new fixture: it compared cash against the opening balance
over four simulated days, and the company pays for its fleet and its staff on every one of them. It
measured the daily burn and called it a roll-over. It compares against the same four days with the
switch off now, which is the only way to isolate the thing being tested. The fixture beside it had
already learned this and said so in a comment.

### The English proof frame of that card was Polish, and had always been

Found while looking at the new switch. `ScreenProofTests.TheRivalCardDraws` writes
`rival_together_en.png` and `rival_together_pl.png` so the pair can be compared, and its own
comment says why: the offer bodies are the longest sentences in the game and the Polish ones are
longer again, so a card that is going to deform deforms in the second frame.

**The two files came out byte for byte identical.** The English capture never asked for English, it
took whatever `Loc.Current` happened to be, and the machine this project is built on runs in
Polish. Every other language pair in that fixture sets the language for both halves. This one set
it only for the Polish half.

So the English rendering of that card had never been looked at, by anybody, while a file named
`_en` sat in the folder implying it had. Fixed, and the two frames now differ.

**Worth keeping for the same reason as the rest of today:** the fixture was green the whole time.
It rendered a frame, asserted the frame was not one flat colour and that nothing ran past the
right edge, and both of those were true of the wrong picture.

---

## Report 10: found 2026-11-01, and it needed the answer he thought was useless

He was asked when rather than what, and he replied: *"Yeah, I dont really remember, but I had been
playing for a while."* That is the answer the question named as useful in advance, and it is what
found this. **Every path measured until then started on a fresh screen.** This one needs a company
renting more than the interface will offer, which is late and is reached one way only.

### Two faults, and the first one is four words on one line

`RentReadout.CeilingPetaflops` takes the company's current rent as a floor, because a ceiling that
fell would clamp the player's own setting down without asking. Its own doc comment says so and
`RentCeilingTests` asserted it. Then the last line was:

```csharp
return Math.Clamp(ceiling, OpeningCeilingPetaflops, FullScalePetaflops);
```

`FullScalePetaflops` is 40,000. **The clamp threw the floor away again.** A company renting more
than that was handed a range ending below its own fleet.

The rent handle in the creator is **one shared instance**, re-ranged by `SyncRentCeiling` on every
reprice, which runs when the creator opens whatever page is showing. A `Slider` clamps its value
into the range it is given, so the handle was pulled down to 40,000, and `Reprice` writes the handle
to the company. **The fleet was cut by opening a tab, with the compute page never opened**, which is
the report word for word.

Reachable because `SmallestCapacityThatWorks` walks capacity up to **400,000** petaflops to find
the smallest fleet that clears a blocked run, and the unblock button commits whatever it finds. A
late-game run needs more than 40,000. So: press the labelled button once, and from then on the
creator quietly takes the fleet back every time it is opened.

The second fault is the one that would have outlived the first. The handle's change callback is how
the player agrees to a fleet, and it sets `rentCommitted`. **A clamp raises exactly the same event
a drag raises, and nothing told them apart**, so the interface could agree to a fleet on the
player's behalf and then bill them for it. That is reachable without the unblock button at all: a
company renting nothing, with the handle parked high as a proposal, whose audience then shrinks,
gets a lower ceiling, a clamped handle and a fleet nobody asked for.

### Why four fixtures missed it

Every path measured for this report rented 0, 150, 640 or 900 petaflops. `RentCeilingTests` held
the ratchet promise at 9,000. **Every one of those is under 40,000, and above 40,000 is the only
place the fault lives.** A guard that cannot reach the fault is not a guard, and this one read as
one for weeks.

| Fix | Where |
|---|---|
| The hard cap never falls below the fleet already held | `RentReadout.CeilingPetaflops` |
| A range sync is not the player agreeing to a fleet | `ModelCreatorPanel.rangingRentHandle` |
| The ratchet measured above the cap, at four values | `RentCeilingTests` |
| The cap still caps everybody who has not passed it | `RentCeilingTests` |
| The report, from the player's seat, at 65,536, with the fleet committed | `CreatorTouchesNothingTests` |
| The interface agreeing on the player's behalf, driven through the panel | `CreatorTouchesNothingTests` |

### Both fixes were then shown to be load-bearing, and one test was not

The ceiling line was reverted and the two fixtures run against it.
`TheCeilingStillDoesNotFallWhenTheFleetIsLargerThanTheSliderWouldOffer` went red, which is what a
guard is for. **The PlayMode test stayed green**, and that mattered: it had been written without
committing the fleet, so repricing wrote nothing, the clamp had no way through to the company, and
the fixture would have been quoted as proof the report was closed. It sets the fleet as committed
now, which is what pressing the unblock button and then starting the run does, and the second test
drives the consent fault through the panel rather than through the flag that fixes it.

**This is the sixth time in this project a test has passed for the wrong reason**, and the only
reason it was caught is that the fix was deliberately taken out to see the test fail.

---

## The third sweep, written 2026-11-01

`NumbersTheRulesNeverReadTests`. The gap named in CLAUDE.md on 2026-10-31 and the first step of
the three agreed there.

A number the rules compute, print on a screen, and never use. It is the worst of the three shapes of
unreachable because everything about it looks right, and it has now happened twice in two days:
`AlliedPetaflops` on the 31st and `TrainingRun.ActualTokensBillions` on the 1st.

**Measured, not guessed: 875 public computations in `Simulation/`, 130 of them read nowhere inside
`Simulation/`.** Most of those 130 are correct by design, because `CompanySnapshot`, `DayReport`,
`ModelRow` and `ProductStanding` exist to be read by the interface and nothing else. The
difference between one of those and `AlliedPetaflops` is whether the number claims to do something,
and no regex makes that judgement. So the fixture is **a ratchet, not a cleanup**: the baseline is
today's measurement and it fails on the hundred and thirty first.

Two things it does that the first attempt did not:

- **A write is not a read.** The first version counted the constructor line that clamps
  `ActualTokensBillions` as a use, and therefore reported the one member known to be dead as
  healthy. A sweep with a silent hole is worse than no sweep, because it gets quoted as cover.
- **It asserts it still catches that member**, so it cannot pass by finding nothing.

---

## Reports 15 and 16: he is right, and most of it was already written down here

### 15. The late game

**This was measured in this repository on 2026-09-13 and the row above said "open, balance" with an
empty Where column**, which is the stale row this file exists to prevent. Four facts, all from the
repo rather than from an opinion:

| Measured | Where it was already recorded |
|---|---|
| At year five the player holds **0.038%** of the market | `PlayabilityTests`, in a comment |
| The company peaks near 17 million users in early 2024, then falls to **sixteen thousand by 2035 while one rival holds 84%** | the same comment |
| Content density peaks in 2024 and **reaches zero in 2029**, while the audience curves keep growing to 2036 | `GAME_GEMS.md` |
| The market share floor in the five-year test was lowered to 0.0001 **to catch erasure, not to certify health** | `PlayabilityTests`, stated in the comment |

And two measured on 2026-11-01 for this report:

**Every scripted style that plays the game is insolvent in 2026 or 2027.** The one that survives all
fourteen years is `Passive`, which releases one model and does nothing else. That inverts the
spine: the design says a company that ships once and coasts is bankrupt inside three years.

**The probe reports fourteen years and simulated five.** `AdvanceDay` returns an empty report once
the company is insolvent, so every year after the death year was a copy of the row above it, and the
table printed those as data. The late game therefore read as measured and flat. It now says how many
years were actually simulated, marks the rest as not simulated, and prints a parity column.

### Why they die, named rather than guessed

Every funding gate is **a share of a frontier the design intends to run away**:

| Stage | Needs | Balanced style's parity |
|---|---|---|
| Series A | 55% of the frontier | 2022: 62% |
| Series B | 65% | 2023: 45% |
| Series C | 75% | 2024: 42% |
| Series D | 80% | 2025: 43% |
| Growth | 85% | 2026: 44% |

The player opens above the Series A gate, falls under it in year two, and is then locked out of
every round for the rest of the campaign. One round raised, ever. Capital availability falls exactly
as the need for it rises, which from the player's seat is "too cutthroat".

**This is a design decision and not a patch.** CLAUDE.md is explicit that anything which lets
capital skip a calendar gate works against the spine, and lowering these ratios is exactly that. The
honest alternatives, for the author to pick from:

1. Gate on **absolute capability** rather than parity, so catching the frontier is optional and
   building a real business is not.
2. Gate on **revenue and growth** rather than on how a rival is doing.
3. Keep parity and **let the ratios fall over the calendar**, which says investors stop expecting
   anybody to catch the leaders.
4. Leave it, and accept that the game is five years long and say so on the store page.

### 16. Types and quality

Both halves measured on 2026-11-01 and written into the probe report.

**Quality already lets you charge more, and nothing on any screen says so.** `Utility` carries
capability at 0.16 a point against price at 1.1 times its log, so a model one point ahead can charge
1.16x at the same appeal, five points ahead 2.07x, ten points 4.28x. He asked for a mechanism that
is already there and invisible, which is a copy job rather than an economy change.

**Type does change the price the audience will bear, and the till does not know.**
`ToleranceFactor` divides the price in the demand term, so a specialist holds its audience at a
higher price. Revenue is then served tokens times one market rate that does not read the type at
all. So a specialist can be priced high, keep its people, and be paid the general rate per token.
**That is the real gap in report 16**, it is one number rather than a system, and whether a coding
token is worth more than a general one is a design call.

**Tokens too cheap is arithmetic, and the table is in the probe report now.** Price decays at 0.80 a
year from $20 per million and floors at four cents, which it reaches in late 2029 and never leaves. The
whole-market revenue column says whether demand growth pays for a five-hundred-fold fall in price,
and nothing had ever multiplied the two.

---

## Report 15: the experiment, run 2026-11-01, and both halves have a number now

Two candidates had equal standing after the first measurement: the funding gate written as a share
of a frontier the design intends to run away, or the token price. **So the gate was switched off and
the probe run again**, which is one number instead of two opinions. Nothing was committed; the gate
is back.

| Style | Gate on | Gate off | Rounds raised | Accelerators bought |
|---|---|---|---|---|
| Balanced | 2027 | 2027 | 1 to 1 | 0 to 0 |
| Frugal | **2026** | **2030** | 0 to 2 | **128 to 696** |
| Reckless | **2027** | **2030** | 1 to 2 | 0 to 0 |
| Passive | survived | survived | 0 to 0 | - |
| Coaster | 2027 | 2027 | 0 to 0 | - |

**The gate is worth three to four years of campaign to two of the three styles that play.** Each
gains exactly one extra round, and Frugal turns it straight into five times the accelerators. That
is the gate being the mechanism, measured rather than argued.

**And it is not the cure.** Nobody reaches 2031 either way. Balanced does not move at all: it has
already taken its one round and is held by revenue or the cooldown rather than by parity.

### The second half, which the gate cannot reach

The market itself, with no company in it, taken from `MarketModel` across the whole calendar:

| Year | $/M tokens | Demand Btok/day | Whole market $/day |
|---|---|---|---|
| 2026 | 0.688 | 187,205 | 128,826,233 |
| **2027** | 0.365 | 397,710 | **145,207,604** |
| 2028 | 0.165 | 671,397 | 110,618,667 |
| 2029 | 0.074 | 1,286,178 | 95,059,249 |
| **2030** | 0.040 | 1,865,047 | **74,601,864** |
| 2031 | 0.040 | 2,476,122 | 99,044,898 |
| 2036 | 0.040 | 9,113,380 | 364,535,213 |

**The whole world market falls by 49% between 2027 and 2030**, because price decays faster than
demand grows, and it only turns back up once the price reaches its floor of four cents and stops
falling. Every scripted style dies inside that window.

So the tester's two sentences are one measurement. "Too cutthroat" is the gate: three to four years
of campaign and one round of capital. "Tokens too cheap" is the contraction: the four years where
the market every lab is selling into is shrinking by half.

### What this makes the decision

**Changing the gate alone cannot close report 15**, and that is the useful result. The honest shape
is two decisions rather than one:

1. **The gate.** Parity already costs the player in the price of money: `PreMoneyValuationUsd`
   raises the story half to the fourth power of parity, so a company at 44% raises on 3.7% of
   frontier-parity value. The gate charges a second time for the same fact, and the second charge is
   not a cost, it is erasure. This repository already settled that principle elsewhere, in those
   words: *"Falling behind still costs, and no longer erases."* The game's own dossier for Algho
   Alpha says half a billion arrived while the frontier tripled past it, so the research disagrees
   with the ladder.
2. **The contraction.** Either the decay shape, the floor, or the demand curve has to change so that
   the market is not shrinking through the exact years a player is trying to scale into it. That is
   the one that needs the author, because it is the spine: a price that halves every year is the
   reason timing matters at all, and the fix must not be "make it gentler" by reflex.

Nothing in either is patched. Both are measured.

---

## Report 15 answered, 2026-11-01: three suspects acquitted and the real one named

Decision one of the two was taken and the parity gate is gone from the funding ladder. Then the
second decision was measured rather than guessed, and **every candidate offered for it turned out
to be innocent.** Three tables did that, all in `CampaignProbe~/report.md`.

### Serving a token is profitable in every year of the campaign

A 200B-active dense model, no distillation, neutral skills, on rented compute at the market rate:

| Year | Serving recipes | $/M to serve | $/M to sell | Cost over price |
|---|---|---|---|---|
| 2022 | 1.0x | 0.9068 | 20 | 0.05x |
| 2026 | 20.0x | 0.0193 | 0.6882 | 0.03x |
| 2030 | 80.0x | 0.0011 | 0.04 | 0.03x |
| 2036 | 80.0x | 0.0011 | 0.04 | 0.03x |

**Between two and nine per cent of the sale price, every year.** The 5.7x figure in the comment on
`MarketModel.ServingEfficiencyOn` is from before that curve existed and is fully fixed. So the
price decay is not the fault, the four cent floor is not the fault, and the demand curve is not the
fault. All three were on my list and all three are wrong.

### The market does contract, and it is not what kills anybody

The whole world market falls 49 per cent between 2027 and 2030, from $145M a day to $74.6M. True,
measured, and secondary: **a player holding 0.038 per cent of $145M a day is earning $55 a day.**
Halving a market you do not hold is not what ends the campaign.

### Share is an exponential, and that is the whole late game

`SegmentMarket` scores every model on `exp(Utility)` and `Utility` carries capability at
`CapabilityWeight` a point, so a capability gap is multiplied rather than subtracted. Measured off
the Balanced style's own capability track, everything except capability held equal:

| Year | Player | Frontier | Gap | Frontier model is |
|---|---|---|---|---|
| 2022 | 19.3 | 31.1 | 11.9 | 7x more attractive |
| 2023 | 21.5 | 47.6 | 26.1 | 65x more attractive |
| 2024 | 25.3 | 60.5 | 35.2 | 281x more attractive |
| 2025 | 29.4 | 68.7 | 39.3 | 539x more attractive |
| 2026 | 34.1 | 77.2 | 43.1 | **984x more attractive** |
| 2027 | 34.7 | 83.8 | 49.1 | **2,592x more attractive** |

That is what 0.038 per cent of a market is, and it is the same number that the fixture comment in
`PlayabilityTests` had already recorded on 2026-09-13: sixteen thousand users by 2035 while one
rival holds 84 per cent.

### So the decision is one constant, and it is the author's

`MarketShareModel.CapabilityWeight = 0.16`, inside an exponential. **It does two jobs with one
number and they pull in opposite directions.** It is why a better model can charge more, which is
what report 16 asked for and what is now printed on the price control. It is also why a lab forty
points behind the frontier holds nothing at all.

Four things worth knowing before choosing, none of them a recommendation:

1. The exponential is the correct shape for a demand split. It is a logit and that is what logits
   do. The question is the weight, not the shape.
2. Price can offset it. `PriceWeight` is 1.1 against a log, so cutting the price by a factor of a
   thousand buys about 7.6 of utility, which offsets 47 capability points. A laggard undercutting
   massively is a strategy the arithmetic allows, and nothing in the game teaches it.
3. Lowering the weight makes quality cheaper to ignore, which works against the spine directly.
   Twenty points of lead currently carries 18.34x on price; that is the same constant.
4. Nothing can be measured about any of it until the operator survives past 2027, which is step 2
   of the plan in CLAUDE.md and is not written.

**Nothing here is patched.** Report 15 is answered rather than fixed, and that is the honest end of
it: the fault is one constant in the centre of the economy, and moving it is not a bug fix.

### What was actually changed, and what it cost me to be sure

The parity gate came out of `FundingMarket.Evaluate`, with the refusal enum member, the stage
requirement and the six catalog arguments that went with it, plus **two parameters of `Evaluate`
that stopped being read**, which is the dead-code shape the third sweep was written for the same
day.

`FallingBehindCostsTests` holds both halves. And it caught two errors of mine in one run:

- It measured a company at **90 per cent** of the frontier and called it behind. A model shipped in
  February 2022 is competitive, because the frontier that year is 31. Falling behind takes years, so
  the fixture has to let the years pass.
- **My justification for the change was overstated and the test said so.** I argued the valuation
  already punishes parity severely, from the story half alone, and forgot the revenue half I had
  read minutes earlier. Measured, for a $250M round in 2026:

| Annual revenue | Sold behind | Sold at front | Times worse |
|---|---|---|---|
| $0 | 79.8% | 12.9% | 6.2x |
| $20M | 24.3% | 9.4% | 2.6x |
| $100M | 6.4% | 4.5% | 1.4x |
| $300M | 2.3% | 2.0% | 1.1x |
| $1B | 0.7% | 0.7% | **1.0x** |

**The penalty has a shape rather than a value**: severe while a lab is a story, and exactly nothing
once it is a business on a billion of revenue. That is defensible, because early investors price the
story and later ones price the company. It is also the second open question for the author, and it
is recorded here and asserted nowhere, because blessing either answer with a test would be deciding
it without being asked.
