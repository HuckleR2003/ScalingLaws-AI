# Contributing

The most useful thing you can send right now is **not code**. It is forty minutes of play and a note
about the exact moment you got confused. Open an
[issue](https://github.com/HuckleR2003/ScalingLaws-AI/issues) and say where you stopped.

If you do want to work on the code, everything below is the standard the existing files are held to.

---

## The layer rule

```
Scripts/Core/         Date, clock, deterministic random, units.  No game rules. No UnityEngine.
Scripts/Data/         Pure data libraries plus lookups.          No economics.  No state.
Scripts/Simulation/   All the rules.                             No UnityEngine.
Scripts/Persistence/  Save format, migration, PlayerPrefs.
Scripts/UI/           UI Toolkit panels. Consumers only.
```

**`Simulation/` must never import `UnityEngine`.** That single constraint is why the whole game is
testable in seconds without opening a scene, and why balance can be tuned from a test rather than by
clicking. If a change appears to require `UnityEngine` inside `Simulation/`, the design is wrong, not
the constraint.

`Data/` may not depend on `Simulation/`. A category is data; a rule is not.

---

## One mechanism per subject

**Read [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) before building anything new, and extend the
existing mechanism instead of starting a second one.**

This is not style. A server hall existed in this project for months, fully written, with its own
passing tests, and a second one was started beside it before a name collision caught it. Two
mechanisms for one subject means two places that can disagree, and they eventually do.

---

## Tests

Add the test in the **same commit** as the behaviour. When you fix a bug, add the test that would
have caught it, so it cannot come back quietly.

Three things are worth knowing before you write one.

**A green suite does not prove a player can reach the feature.** EditMode tests drive the simulation
directly, so a mechanism that is complete in `Simulation/` and has no control in `UI/` passes
everything and is unreachable. That has happened eight times here, once to an entire progression
system the player paid for and never received, with 526 tests green. If you add a mechanism, check in
the same commit that the value travels from the control into the state rather than defaulting in a
constructor.

**A green suite is blind to layout.** Every visual fault in this project has been found by rendering
a page to a PNG and looking at it, never by an assertion. If you change anything on screen, run the
PlayMode pass and look at `TabProof~/` before and after.

**Balance is a test, not an opinion.** `PlayabilityTests` plays a scripted four-year campaign. If an
economic change turns it red, the change is probably wrong. Do not loosen the assertion to make it
pass; that fixture has caught three faults that no unit test would have.

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayResults.xml
```

Do not add `-quit` to `-runTests`. Unity finishes the import, never runs the tests, and exits 0.

---

## Saves

Every change to what is stored needs a version bump and **one migration step for that version**, plus
a migration test. The rules:

1. Old shapes are kept verbatim, so the upgrade path reads a real historical structure rather than
   guessing what used to be written.
2. One step per version. Each method moves a file forward exactly one version and stamps its own
   number, never the newest one.
3. Reconstruction is declared, never disguised. Where a migration has to invent a value the old
   format never stored, pick the least flattering assumption that is still defensible.
4. Clamp and validate every field on load. Validate enums with `Enum.IsDefined` and fall back to a
   legal default. A corrupt or hand-edited save must never crash the game.

**Something that looks derived is often causal.** Yesterday's service quality, version adoption
shares and an open regulatory inspection all look computed and all have to be saved, because the next
day reads them. That mistake has been made five times in this project. If dropping a field changes
how the next day plays, it is state.

---

## Numbers

**Never invent a specification.** Hardware figures and competitor release dates are public vendor
information, rounded. If a real figure cannot be found, mark the entry as a projection rather than
guessing quietly, and the interface will label it.

The same rule applies to fiction. The one invented company on the board carries a projection flag on
every release, because the honesty flag is about not passing invention off as record.

---

## Style

- `sealed class` unless inheritance is designed for. `readonly struct` for snapshots passed to the UI.
- **Clamp in constructors.** No input should be able to produce a division by zero, a negative
  duration or a NaN. `Math.Clamp` passes NaN straight through, so use `SimUnits.Finite` first.
- Enums over magic strings and magic ints.
- `DeterministicRandom` for anything that must replay identically. Never `UnityEngine.Random` inside
  `Simulation/`.
- XML doc comments on anything non-obvious, explaining **why** rather than what. `SaveMigration` is
  the standard to match.
- **Every string the player reads is a phrase-book key.** Never build a key by concatenation: the
  localisation test can only read literals, and a key it cannot see ships as raw text on screen.
- **Never use a raw format string for a number.** `:N2` and `:0.0` follow the machine's locale, which
  has printed `$20,00` and `22,6 kW` in this project three times. Use `UiFormat`.
- **When you add a USS class from C#, grep the stylesheet for it.** A class that does not exist takes
  default flex and silently collapses whatever it is on.

---

## Text that ships

No em-dashes, no marketing adjectives, real verifiable numbers only, short plain sentences. That
covers the README, the changelog, release notes and anything the game prints.

The reason is practical rather than aesthetic: this project is openly AI assisted, and prose that
reads as generated invites accusations the engineering does not deserve.

---

## Commits

One subject per commit. The message says what changed and **why it was wrong before**; the diff
already says what the code does now. Look at the existing history for the shape.
