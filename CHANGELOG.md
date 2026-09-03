# Changelog

Every release of Scaling Laws, newest first.

Dates are the day the build went public. Versions follow `MAJOR.MINOR.PATCH`: the minor number moves
when the game gains something a player can do, the patch number when it does not.

<details>
<summary><strong>How an entry here gets written</strong> (kept in the file on purpose)</summary>

A changelog for a game is not a git log. A player does not care that a method moved; they care what
is different when they sit down. So every release here has the same four parts, in the same order:

1. **The headline.** One change, three sentences, and it names the mechanism rather than the file.
   If a release has no headline, it is a patch and it says so.
2. **Added, Changed, Fixed.** Grouped by what a player touches. Each line says what is different,
   not what was edited.
3. **Save compatibility.** Always stated, even when nothing changed, because a player mid-campaign
   reads this line first.
4. **Under the hood.** Short. Test counts, refactors, anything that only matters to somebody reading
   the source.

Numbers are measured, never estimated. If a figure cannot be checked in the repository it does not
go in. This file is the draft for the store update post, so anything vague here becomes vague there.
</details>

---

## [Unreleased]

**The interface stopped saying two things about the same number.** Six places in the game stated
something that could not also be true: the inbox described one letter three different ways, the
business page blamed a slider for a giveaway it could not move, the model creator printed a
confident zero before anything was chosen, and the cluster was quietly doing 170 per cent of its own
work. Every one of them is now read from a single source, with a test that fails if a second reading
appears.

Two mechanisms that were finished in the simulation and impossible to reach also got their controls:
the split between building and serving, and the whole own-datacenter tier.

### Added

- **The cluster split, on COMPUTE.** How much of the fleet goes to training, upgrades, architecture
  programmes and research, and how much is left for the people paying. The number has existed and
  been saved since the beginning, was set once by a test fixture, and no screen had ever offered it.
- **Commissioning your own datacenter.** $80M, 300 days from signature to the first token, its own
  power contract at a third of what a colocation charges, and 40MW of capacity. All of it existed,
  gated and costed, with no button anywhere in the game.
- **Status badges in the header.** Small squares saying what is temporarily true about the company:
  a viral window, the clean slate a new lab gets, a year with nothing going wrong, a backlash after
  a penalty, a campaign running. These have been multiplying demand by between 0.15x and 4.0x every
  day since they were written, and nothing has ever told the player they existed. Each carries what
  it is pulling right now and **an estimate** of how long is left.
- **The remaining time on a badge is a guess, wrong by up to 40 per cent either way.** Nobody inside
  a company knows how long a wave of attention or a bad quarter is going to last, and a badge that
  counted down exactly would turn a story into a timer. The guess is fixed for the life of the
  effect, so it counts down smoothly and cannot be averaged out over a week, and the badge
  disappears when the effect really ends rather than when the guess runs out.
- **The world happens to everybody, on the dates it actually happened.** Twenty three events across
  the campaign: the invasion that closed a neon supply line, the Shanghai lockdown, export controls
  on the best accelerators, the day a chat assistant went public, the search race, the price cuts,
  the weights leak, the copyright suit, the first comprehensive regime, the day the company selling
  the shovels became the most valuable in the world, reasoning models, a datacenter restarting a
  nuclear plant, and the cheap model that took a third off the going rate. Each one moves one of the
  four curves the market already computes, arrives on the wire the day it starts, and cannot be
  prevented, delayed or caused. Everything dated 2026 or later is the game's guess and says so in
  its own news item.
- **Rivals say what kind of company they are.** Up to three badges on a lab's card: fearless,
  patient, undercutting, open handed, institutional, deep pockets, expanding, wobbling, absorbed,
  hostile. Every one is worked out from what the lab has actually done rather than written on it,
  so a badge cannot contradict the behaviour it describes, and none of them can mention something
  that has not happened yet.
- **One person, opened.** Clicking somebody on the team page opens a card with three tabs: who
  they are, when they work, and what the job is. Portrait, tenure, wage, skill, where they were
  found, and a loyalty band with a bar. DISMISS, BONUS, and TALK drawn and disabled because
  conversations are their own thing and are coming later.
- **People arrive with expectations.** Most want nothing in particular; the rest asked for one or
  two of the benefits the company can already offer, decided the moment they were hired and never
  changing. Meeting all of them makes somebody settle in **a quarter faster**; asking for something
  and not getting it costs a little loyalty every month. It means the same payroll buys more
  loyalty at one company than another, and it is the reason a person is worth reading rather than
  a row.
- **A bonus buys time.** One month or three of somebody's salary, credited as tenure, capped at two
  years across a career. Money can shorten how long somebody takes to settle in and can never
  replace it; past the cap the payment is refused rather than quietly taken.
- **A working day per person**, eight to four by default, drawn as twenty four cells so two people
  can be compared at a glance. Recorded before anything reads it, so the day a role earns its own
  mechanic there is a schedule waiting rather than a field to add and migrate.
- **A confirmation card for a premises deal.** The rent, the fit-out that is never refunded, the
  desks that cap hiring, and the price to own it outright, with RENT and BUY OUTRIGHT side by side.
  It replaces two buttons that each had to be pressed twice and neither of which said what the
  other cost.
- **Five more rival traits**: veteran, newcomer, scarred, leading and quiet, so a lab can carry two
  badges that are actually about it rather than one label.
- **A card on every stage of the model creator**, saying what that page decides.
- **A 24-hour clock in the bottom bar** on every screen that is a page rather than a room.
- **The person walking around your office has their name over their head.** One line, no plate
  behind it, no border. The room is grey boxes and low-poly furniture, and a label with a background
  would be the loudest thing in the frame.
- **Tutorials you can ask for again.** Short walkthroughs of one screen, offered from a green card in
  the corner rather than being part of the opening. The first walks the server room: buy a cabinet,
  stand it on the floor, open it, put a fan in it. While one is running the bottom bar is held on
  that screen, because a three-minute walkthrough somebody wanders out of halfway is worse than none
  at all. STOP is always there and marks nothing finished, so the offer comes back.
- **A step that asks you to do something has no button to skip it with.** "Click the cabinet" is
  finished by clicking the cabinet. A NEXT beside it would be a way to complete a tutorial without
  ever touching the thing it is about.
- **A corner for things waiting on you**, above the task list and separate from it. Today it holds
  the one walkthrough offer. Clicking the card starts it, clicking the x puts it away for good.
- **Emil names a number.** During the compute act he asks you to keep the server rent under $80,000
  and says there is something coming that will get you off renting, which is the basement he hands
  over at the end. The rent slider is the one control in the game that bills every day whether or not
  anything is training, and a figure is easier to hold onto than a warning.

### Changed

- **A backlash after a safety penalty runs four to thirteen months, drawn on the day it starts.** It
  was 63 to 113 days and derived from the severity, so a player who had seen one severe incident
  knew exactly how long the next one would last and could plan the release calendar around it.
- **A backlash takes the fan base as well as the demand**, up to a quarter of it at the worst
  severity. It presses on what the fan base is pulling toward rather than on the count, so the
  ordinary things that earn a following genuinely fight it: a company that works through a bad year
  keeps more of its people than one that waits it out. Fans drift at 0.12 per cent a day, so how
  much of the damage actually lands depends on the length the incident drew.
- **The clock disc is for rooms now.** It overhangs the bar by about 170px and no page reserved for
  it, so it covered the bottom-left corner of every document screen: the brand line on TEAM, the end
  of the marketing sentence on BUSINESS, a cabinet hint in the basement. The office and the server
  room keep it; everything else gets the rectangular reading in the bar.
- **The research map opens showing the whole era.** It has claimed to since it was written, and FIT
  set the zoom to 100 per cent, which is a default rather than a fit.
- **Research node captions are sized from the longest word in the tree.** In Polish about a third of
  era one was breaking inside the word: `SPECJALIZACJ / A KODOWA`.
- **The map's zoom controls sit beside the era heading** instead of on top of the last node of the
  row, which was clickable in about half its area.
- **The server room says it houses cards rather than producing compute.** A full basement with an
  empty fleet delivers nothing but its upkeep, and the caption invited the opposite reading.
- **The founder must be named.** Leaving the field empty used to sign the company's public page
  "Anonymous".
- **Polish reaches three more screens**: the model creator, the official page and the archive, and
  the shell's own headers, banners and tooltips.

### Fixed

- **The basement could not be bought.** The button was enabled on cash alone, at $70,000, while the
  operation also requires the colocation tier: a released model and $5M. A player with the money
  pressed a live button and nothing happened at all, because the refusal went into a discarded
  argument. The screen and the operation ask the same question now, and the answer is on screen.
- **The benefits on BUSINESS printed two unlabelled amounts**, a per-head price and a payroll
  total, one above the other with nothing naming either. Both say what they are now.
- **Nine lab logos were exported with the transparency checkerboard baked into the pixels.** Over a
  dark card that reads as a grey plate behind the mark, on the founding screen, the ranking board
  and every rival card.
- **The fleet was doing 170 per cent of its work.** Serving took the whole cluster whenever no
  training run was in flight, while a research node, an upgrade programme or an architecture
  programme went on taking the training share regardless.
- **The inbox said "No reply needed" over a letter with two answer buttons on it**, and counted the
  same letter under NEEDS AN ANSWER in the filter above.
- **BUSINESS showed 8 per cent of tokens given away over a free tier set to zero.** The figure was
  right: that is what trials cost a company offering no free tier at all. The screen now names it
  separately from the part the slider controls.
- **The model creator printed PROJECTED CAPABILITY 0.0 beside FRONTIER TODAY 45.0** before anything
  was chosen, along with a run that would apparently take no time and cost nothing.
- **The server room's capacity figure was worked out from a card the company does not own** —
  whatever the clouds happened to be renting that month, rather than the fleet actually in the room.
- **The bottom bar kept whatever language it was built in.**
- **Eighteen readings printed with a comma on a Polish machine** (`0,70x`, `$20,00`, `1 234`), across
  the model creator, the inbox, the team page and the office.

### Save compatibility

Save format **v47**. A campaign started on 0.1.0 or on v46 opens here and keeps everything in it.

One thing is new: which walkthroughs you have finished, and which you have waved away. An older file
has taken none of them, because it was played in a game that had none, so the offer appears for an
existing campaign exactly as it does for a new one. Marking them complete would have quietly
withheld a tutorial from everybody already playing.

Being part-way through a walkthrough is deliberately not saved. Quitting during one and coming back
to it half done, with the interface still held shut and no memory of why, is worse than starting the
two minutes again.

Two facts per person are new: a bonus paid, and the hours they work. Nobody has ever been paid a
bonus, so nobody is credited one; handing every existing employee two years of settling-in would
rewrite the loyalty of a whole payroll on load. The hours arrive at eight to four, which is not a
guess but the shift every campaign has implicitly been running.

The cluster split was already saved, so an older campaign arrives at the setting it has been running
on all along.

### Under the hood

- 955 EditMode tests across 94 fixtures, and 23 PlayMode across 7.
- 1,861 phrases in the book, both languages complete.
- `CompetitorStrategy.FastFollower` is assigned to no lab, so nothing in the game runs that brief.
  Found by a guard that asks whether every rival trait can actually occur.
- The unreachable-mechanism sweep was run again over every public mutator on the simulation and the
  company. Nothing player-facing is left without a control, and two methods with no caller anywhere
  in the repository were deleted.
- Two operations moved out of `CompanySimulation` into the test assembly. Neither had a caller
  outside a fixture, and a unit-count entry point beside a capacity-denominated contract is one edit
  away from acquiring a slider.
- New guards: every compute tier has a way in, every letter's three readings agree, every effect has
  words in both languages, the interface actually draws the effects, every rival trait can occur,
  no trait gives away how a lab ends, every world event has a headline in both languages and reaches
  the wire, everything past the record is marked as a guess, and no two shocks compound a curve past
  the band the balance was measured over.
- `MarketModel` now separates the published trend from the world acting on it, for scarcity and for
  algorithmic efficiency. A test that pins the doubling law reads the law; one that asks what
  efficiency is today reads the law plus the calendar.
- One tutorial system, not two. A walkthrough is a different list of the same steps the opening tour
  is built from, fed to the same strip, the same highlight and the same lock. A second system would
  have been a second place to fix the bug that ate four playtest clicks.
- A guard fails the build if any step that waits for the player names an action no screen ever
  reports. Because a walkthrough holds the bottom bar shut, that particular gap would not be a
  cosmetic fault: it would be a player sealed inside one screen.

---

## [0.1.0] - 2026-08-30

**The first public build.** Everything before this was source only: you needed Unity and a
checkout to see any of it. This is the first version of Scaling Laws that anybody can download and
run, and it plays end to end, from the cold open in January 2022 to a company that is either still
trading or is not.

It is deliberately unpolished. The art is partial, several screens are still plain, and the point of
shipping now is to find out whether the economy is interesting before any more time goes into how it
looks.

### Added

- **A campaign you can finish.** Fifteen screens, reachable from the bottom bar: the office, model
  design, research, architecture, upgrades, the team, compute, business, release, capital, the
  board, intelligence, marketing, news and mail.
- **A model creator in eight stages.** Branding, foundation, scale, data, compute, safety, review,
  and what happens after training. Each page explains the trade it is asking about.
- **The tutorial.** Emil, the founder's cousin, walks through the opening hour in 53 steps across
  six acts. He can be skipped at any point, he can be asked to call back later, and the tour resumes
  from where it stopped rather than from the beginning.
- **A server room.** Four cabinets in a basement, a floor of sixteen squares, and cooling that costs
  a slot. It is the first compute the company physically owns.
- **Fifty research nodes** across four eras and three tracks. Every architecture, corpus, upgrade line
  and compute tier sits behind one.
- **Fourteen rival labs**, each with a dated history. Three of them come apart during the campaign,
  over the same exposures that can end the player's company.
- **Two languages.** Polish and English, 1,321 phrases, switchable from settings at any time including
  mid-conversation.
- **Interface audio.** Synthesised at runtime rather than sampled, so the build carries no licensed
  audio and a missing file cannot break a screen.
- **A way to tell me where you got stuck.** One letter arrives in the mailbox, once per campaign, on
  a first release, an insolvency, or day 120, whichever comes first. It opens a form in your browser
  carrying the build number and how far into the campaign you were, and nothing else. The game has no
  networking of its own and never sends anything on its own.

### Changed

- Post-training work is commissioned as one programme rather than one per improvement. Picking four
  upgrades used to start four jobs that each counted the same calendar down in parallel, so all four
  landed on the same day and filled the mail with four separate completions.
- The free allowance slider stops at 250,000 tokens, which is where its effect saturates. It ran to
  400,000, so the top 37 per cent of the travel changed nothing and still billed for every token in
  it.
- Reserved capacity, rented capacity and the server room now report through the same meters, so the
  three ways to have compute are read the same way.

### Fixed

- A training run quoted at twenty-one days announced four, then displayed "0 days" while the
  calendar kept running. The countdown watched one of the run's two clocks and divided it by the
  size of the whole fleet rather than by the share reaching the run.
- The tutorial's free research node was not always handed over. Whether the player received it
  depended on whether that step happened to trigger a repaint.
- Asking Emil to call back restarted the tour instead of resuming it.
- Kilowatt and millisecond readings printed with a comma on machines with a Polish locale.

### Save compatibility

Save format **v44**. This is the first public build, so there is nothing older to load. Every future
version will carry a migration step, and a campaign started here will keep opening.

### Under the hood

- 888 EditMode tests and 21 PlayMode tests, across 88 and 7 fixtures.
- Unity 6000.5.8f1. Earlier 6000.4 editors cannot open the project.
- 225 C# files under `Scripts/`, and 337 counting the tests and the editor tooling.
  `Simulation/` imports no UnityEngine, which is why the suite runs without loading a scene.
- 170 commits since 2 August 2026.

[0.1.0]: https://github.com/HuckleR2003/ScalingLaws-AI/releases/tag/v0.1.0
