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
