# Scaling Laws 0.2.0

Draft of the GitHub release. **Paste the part below the line into the release body.** The full
technical record is in [CHANGELOG.md](../CHANGELOG.md); this is the version a player reads.

Written to the house rules: no em-dashes, minimal emoji, no marketing adjectives, and every number
in it is measured rather than estimated.

---

# 0.2.0 - The one where the world answers back

Six weeks since the first build. The short version: **the game has music, your rivals fight back, and
the state will hand you a country to run.** Save files from 0.1.0 open here and keep everything.

## The big three

**Era five: a government puts a country on your models.** Eight sectors of a state, from filing
permits to national defence, each paying more in a day than most of the campaign earns in a month.
Each one also eats capacity your paying customers no longer have, and failing one costs billions
rather than reputation. The gate is a five year safety record and it is the only thing in this game
money cannot buy. One severe incident costs about three years of it.

**Smear a rival and their lawyers turn up.** Paying for a story about a competitor used to move two
numbers and end there. Get traced and their counsel rings your phone, a notice before action lands in
the inbox with a figure and thirty days, and refusing it can put you in front of a court. Refusing is
45 per cent to reach a hearing. Ignoring the letter is 60, because a refusal is an answer and silence
is not. A campaign that lands cleanly is still only a suspicion, and nobody sues on a suspicion.

**The game has music.** Three loops instead of one, so the office is no longer silent: a piano in the
menu, mallets in the office, something higher in the model creator. Plus four new sounds for the
page turning, the phone and a message arriving. Every note is computed when the game starts rather
than loaded from a file, so the download does not grow by a byte.

## Added

- **A real world map.** Natural Earth outlines, 177 countries, replacing six hand-drawn shapes.
- **The basement is a room you walk into**, drawn in 3D, with cabinets you buy, carry, place and sell.
  Cabinets do not age, but chips get hotter: a room filled in 2023 is quietly throttling by 2027.
- **Four Operations research nodes**, the first that improve the room rather than the model.
- **47 achievements**, with a screen in the Escape menu.
- **A rival that copies you.** One lab now builds whatever you are selling rather than following the
  calendar, so specialising is something a competitor can take off you.
- **Corporation tax has a clock on it.** An orange strip names the year, the amount and the days
  left. Miss the date and the bill moves to next year with nine per cent on top, and the game tells
  you so on a card instead of letting it grow quietly.
- **Separate SOUND and MUSIC sliders**, side by side, neither inside the other.
- **A phone that keeps what was said**, and short guided tours you can ask for again at any time.
- **The people you hire stand in the office you pay for**, and clicking one opens who they are.
- **The creator names the architecture families you do not have yet**, with what each would cost
  and which research opens it. It used to list only the ones you already had, so a family you had
  not researched was not named anywhere in the game: you found out hybrid state space existed when
  a node handed it over.

## Changed

- **A training run no longer hides the product you are selling.** Starting a second model used to
  replace the corner panel for the two hundred days the run takes.
- **Every product on sale has its own chart**, so with two models you can see which one is carrying
  the company.
- **The world map fills its panel.** Choosing Europe used to leave 41 per cent of the box empty.
- **The upgrade tiles say what the market is at**, so the levels a new model ships with stop looking
  like they came from nowhere.
- **The cash achievements are reachable.** The old ladder started at a hundred million dollars, which
  measurement showed was above anything a full campaign reaches.

## Fixed

Around forty, and these are the ones you would have noticed:

- **The founder can be a woman.** Forty six Polish lines were telling the player what *he* had done.
- **A founder you did not name signed the company page "Anonymous"**, which reads as a real person
  with an odd name rather than as a blank.
- **The official company page always showed the newest model**, whichever one you clicked.
- **Clicking a person in the office did nothing** in the case where the raycast missed them.
- **A letter from the game's author was never in the game.** Two different texts shared one address
  and the second silently replaced the first.
- **Eighteen readings printed with a comma** on a Polish machine: `0,70x`, `$20,00`.
- **The tutorial repeated itself and skipped steps**, because the strip was being rebuilt between
  your click going down and coming up.

## Saves

**Format v51.** A campaign from 0.1.0 or anything since opens here and keeps everything in it.

Two things cannot be recovered for an older file and are left honestly empty rather than invented:
the month of trading history each model now keeps, which fills itself in over a month of play, and
the conversation with Emil, because every line in it was about a company as it stood on one
particular day.

## Under the hood

- 1079 EditMode tests across 113 fixtures, 31 PlayMode across 8.
- 2,489 phrases, English and Polish, both complete.
- Eighteen catalogs stopped storing text, so nothing on screen is stuck in the language the game
  started in.

## Known and not fixed

- The scripted campaigns in the balance probe peak around $71M. If you get far past that, the top of
  the achievement ladder may still be out of reach, and it will be measured again rather than guessed.
- Licensing a family with money is not a route you can take. Every family is opened by research
  that hands it over, so the price on the creator's list is information rather than an offer.
- The furniture shop is suspended in favour of the furnished-move option.
