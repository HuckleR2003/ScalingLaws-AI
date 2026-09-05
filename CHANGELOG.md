# Changelog

Every release of Scaling Laws, newest first.

**Scaling Laws** is a tycoon game about running an AI lab. You start on 1 January 2022 with $12M and
no models, in a world where the real ones are about to arrive on the dates they actually arrived.
You train models, sell tokens, buy compute, and try to still be here in 2036.

The spine of it is one sentence: **upgrades are not purchased, they are timed.** Hardware ages, the
frontier moves every month, and capital spent a year early sits in an asset losing roughly a quarter
of its value with every successor launch. There is no guaranteed profit anywhere in the game: the
price of a token falls by about half a year, demand saturates, and a company that ships one model
and coasts is bankrupt inside three years. That is the design, not a balance problem.

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

Nothing yet. The next thing that changes goes here.

---

## [0.2.0] - 2026-09-06

**A government will put a country on your models, and it will look at five years first.** Era five
opens once you have built something worth trusting: eight sectors of a state, from filing permits to
national defence, each paying more in a day than most of this campaign earns in a month. Each one
also holds capacity your paying customers no longer have, and each one fails at a price measured in
billions rather than in reputation.

The gate is a five-year safety record, and it is the only one in this game money cannot move. One
severe incident costs about three years of it. There is nothing to buy, no research that shortens
it, and no way through except not having done the thing.

**And the game has music.** Three loops where there was one, so the office is no longer silent, with
their own slider beside the one for interface sound. Every note is computed at startup rather than
loaded, so the download does not grow by a byte.

The other thing you will feel first: **a rival you smeared now has lawyers.** Get traced and their
counsel rings, a notice before action lands in the inbox with a figure and thirty days, and refusing
it can put you in front of a court. Nobody reacted to that before.

Underneath all of it: a real world map instead of six hand-drawn blobs, a phone that keeps what was
said, short guided tours you can ask for again, the people you hire finally standing in the office
you pay for, corporation tax with a clock on it, and a BUSINESS page that opens on one screen instead
of three. The founder can be a woman, which forty six Polish lines had been quietly assuming
otherwise.

### Added

- **The founder can be a woman.** Polish puts gender in the past tense, so forty six lines were
  telling the player what *he* had done: "Sprzedałeś firmę", "Gdzie utknąłeś?". All of them are
  rewritten in the present tense or impersonally, which needs no gender field, no question at the
  creator and no save migration. English was already neutral and is untouched.

- **Music, and a game that is not silent.** Three loops where there was one: the menu, the office,
  and the model creator, each computed from sine waves at startup so the project still carries no
  audio files and the build does not grow. Four new cues besides: a page turning under the notices
  that put a verdict in front of you, a message arriving on the phone, and the phone being picked up
  and put down. Written by Gosia; the loops are hers, the crossfade below is not.
- **SOUND and MUSIC, two sliders on one line.** Sound is the clicks and the rest of the feedback,
  music is the menu, the office and the creator, and neither is inside the other. A player who wants
  their own podcast on takes music to zero and keeps the interface answering them.

- **One rival watches what you sell and copies it.** `FastFollower` has been a name in the code with
  nothing behind it since the field was written: no lab, no product line, no release pace. Alibaba
  Qwen takes the brief, out of the four labs that were all cost leaders. It is the only strategy in
  the game that answers to the player instead of the calendar, so specialising is now something a
  rival can take off you. It ships sooner than anybody, every 180 days, and gains least per release,
  because copying an answer is cheaper than finding one and it does not put you in front.

- **A lab you smeared can write, ring and sue you.** Paying to make a rival look bad used to move two
  numbers and end there: nobody on the other end ever reacted. A campaign that is traced back now
  brings a notice before action from their lawyers with a sum on it and thirty days to answer, a
  phone call from their counsel the same day, and a case in front of a court if you refuse or say
  nothing. Refusing is 45% to end up in court; ignoring the letter is 60%, because a refusal is an
  answer and silence is not. A campaign that lands is still only a suspicion, and a suspicion is not
  a case.
- **Corporation tax has a clock on it.** An orange strip under the grants panel names the year, the
  amount and the days left, and opens the letter. Miss the date and the file closes with a card
  saying so: the whole sum goes into next year's assessment with a **nine per cent surcharge for
  postponing it without asking**, and the figure is on the card. Asking to defer is untouched and
  still the better answer, which is the point of having both.
- **Each product on sale has its own chart.** The second and third banners in the corner drew the
  meters and no graph at all, so with two models there was no way to see which one was carrying the
  company. Every model now keeps its own last month of trading and draws it.
- **The last seven catalogs speak Polish.** The founder's seven skills, the twelve safety tiers, the
  eight founder traits, the six funding rounds, the three hiring channels, the six marketing
  programmes and the three compute tiers all stored their words in the file that holds their numbers,
  so they kept whichever language the game started in. That is the creator, the SAFETY stage, the
  funding page, the hiring card, BUSINESS and COMPUTE. All eighteen catalogs in the game now read the
  phrase book at the moment they are asked, and a test walks every entry of the last seven in both
  languages so the claim can stay true rather than being believed.
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
  the weights leak, the copyright suit, the first regime that covers the whole field, the day the company selling
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
- **The phone keeps what was said.** Opening it and choosing Emil used to fire a question and he
  answered it, every time, with nothing kept. Now there is a Messager dIn with the whole thread in
  it, each message stamped with the campaign day it was sent on, and a Write / Call button under it
  rather than a conversation that has already happened without you.
- **Emil's guides live in the messenger.** Under the composer is what he can walk you through, so a
  walkthrough waved away in the corner is still somewhere you can go and find it. He describes it
  and asks whether to do it now, because starting one holds the interface shut and doing that to
  somebody who tapped a list item to read what it was is the trap the lock exists to prevent.
- **A real world map.** Natural Earth outlines, 177 countries, Robinson projection, dark grey on
  thick white borders. Pick a region by clicking anywhere on it, or click one of the sixteen
  countries and settle both at once. The five countries too small to find with a cursor get a
  marker; the rest are found by their own shape. It replaced six hand-typed blobs.
- **Era five: the state programme.** A government will put parts of a country on your models once it
  has watched you for five years. Eight sectors from bureaucracy to defence, each paying more in a
  day than most of the campaign earns in a month, each holding capacity the paying public no longer
  has, and each with a failure priced in billions rather than in reputation.
- **A five-year safety record.** What a government sees when it looks you up. It is the only gate in
  the game money cannot move: one severe incident costs about three years of it, and the only way
  back is time.
- **Messager dIn.** The phone keeps the conversation now, with the campaign day beside every
  message. Opening it used to fire a question and Emil answered it before you had read anything.
  There is a Write / Call button instead, and under it the guides he can walk you through.
- **Short guided tours you can ask for again**, offered from a green card in the corner and from the
  phone. The first walks the server room end to end: buy a cabinet, stand it, open it, fit a fan.
  While one is running the bottom bar stays on that screen.
- **Emil names a number.** He asks you to keep the server rent under $80,000 during the compute act
  and says there is something coming that gets you off renting, which is the basement he hands over
  at the end.
- **The people you hire are in the office.** One model each, their name over their head, and
  clicking one opens who they are. The room has had a staff group since the day it was generated and
  only ever held the founder.
- **Your name over your head**, and theirs. One line, no plate, no border.
- **Furnishing the office actually places things.** The shop was switched off in August because
  buying a piece put it wherever the plan felt like. Right click to pick something up, left click a
  lit square to put it down, right click anywhere to put it in storage. The same grammar the server
  room already uses, and the squares only light up while you are carrying something.
- **A way out of the basement.** Every other screen is left through the bottom bar it is standing on.
  A room needs a door you can see.
- **The name over somebody's head says what they do**, in the colour of the job, with a hairline
  under the name. The founder gets their own colour and "CEO of <company>".
- **Research nodes you can start now look like it.** A node that is ready and one that needs two
  others first were a hue apart, which is not a distinction anybody makes across fifty of them.
- **The office has its own desks in it.** Every tier already carried a desk count, it is what caps
  hiring, the rent pays for it, and nothing drew it: LVL 1 said ten desks over an empty floor.
- **People keep the hours you give them.** Set somebody to 8 to 16 and after 16 their figure goes,
  leaving a dimmed marker with their job and the time they are back; at 8 they are in again. The
  schedule control existed and had never been visible anywhere.
- **BANK says what the company owns**: cash, hardware at what it would fetch today, property owned
  outright, furniture at resale, and a band that carries the valuation with the book value beside
  it. The band's green covers the share of the price your own assets actually cover.
- **Grant cards read like grants.** The name on torn white, who is offering it with weight, and the
  four terms on their own coloured plates: what arrives now, what arrives on completion, the
  research points money cannot buy, and how long there is.
- **Forty seven achievements, and a page in the Escape menu that shows them.** Ten groups, from
  the first hundred million to the year 2036, and they are about this game rather than about
  tycoons in general: one is for a cabinet where seven cards and a fan out-deliver eight cards.

  They live with the player rather than with the company, in the same place as the settings, so
  deleting a campaign does not take them back and the count of companies you have run into the
  ground survives the company running into the ground. **The save format does not change.**

  Each one carries the identifier Steam would use, so connecting it later is one line beside the
  line that writes it today.
- **Research about the building, not the model.** A fourth track, OPERATIONS, with four nodes for
  the server room, on its own row under each era. Every node in this game had been about the model:
  bigger, cheaper, safer, better shaped. The room you actually stand in, click on and pay for had
  nothing behind it at all.

  - **Rack telemetry** puts the next card's heat and speed on the cabinet panel *before* you fit it.
    It is the only node in the game that buys information rather than a number.
  - **Airflow modelling** adds 1.2 kW of cooling to every cabinet on the floor at once. Wide and
    shallow, against the fan's narrow and deep: a fan rescues one cabinet and costs it a slot.
  - **Liquid loops** flatten the throttle curve for immersion tanks alone, from 2.2 to 1.1. The
    dearest cabinet in the catalogue used to age on exactly the same curve as the cheapest one, and
    this is the reason to buy it that it was missing.
  - **Own substation** takes the room off the household meter, $0.19 a kilowatt hour down to $0.11.
    The power bill is the one cost that grows with the thing you are proudest of.

  All four are optional technology: they are worth a great deal with a basement and nothing at all
  without one.
- **Corpora you do not own are on the DATA page, with what they cost.** The stage listed only what
  the company already had, so seven of the eight corpora were not merely unbuyable, they were
  invisible: nothing in the game named licensed video, priced it, or said what would open it. Each
  row now carries the price and, when it cannot be bought, the reason. Four of them are genuinely
  purchasable with money alone.

### Changed

- **A training run no longer takes your product off the screen.** Starting a second model used to
  replace the corner banner with the run for the two hundred days it takes: no name, no users, no
  mood, no way through to the management desk, while the company still had something on sale the
  whole time. The run is a strip under the product now. With nothing on sale it is still the whole
  banner, which is the opening of every campaign.
- **The world map fills the panel it is drawn in.** Choosing Europe drew 459 pixels of a 780 pixel
  box and left the rest empty, which a playtest measured by eye as "about 40% on the right"; it was
  41%. The view is fitted to the shape of the panel by taking in more map rather than more nothing,
  and where there is no more map to take in it crops instead: the opening view trims the empty
  Pacific margins and draws every continent seventeen per cent larger. A region is capped at two
  thirds of the world, so leaning in on the Americas means something even though that region runs
  from the Canadian arctic to Tierra del Fuego. And choosing Europe used to draw the Sahara across
  the bottom of the panel, because a region's extent is the box around its countries' outlines and
  France's outline includes French Guiana: six countries that span seventeen degrees of latitude
  were framed as though they spanned fifty eight.
- **The UPGRADE tiles say what the market is at.** A model ships level with the market on every
  trait, so a first release opens this screen already carrying levels nobody bought. The number it
  is measured against only appeared once a model had fallen behind it, as a red badge, so the two
  states a player meets first both drew a level with no ruler beside it. Every tile now reads
  `LEVEL 4 - MARKET 4`, and a line under the model's name says where those levels came from.
- **The Polish build stops addressing every hire as a man.** Three of the five first names in the
  candidate pool are female or unisex, and the notes said *dostał*, *przyjął*, *odszedł* for all of
  them. Present tense carries no gender in Polish, so this needed no gender field and no migration.
- **Counted nouns take the right form.** Polish has three and these printed the genitive at every
  count, so a two-desk piece of furniture read as *2 biurek* and a grant asked for *1 modeli*.
- **BANK opens on one screen.** It was three full-width panels stacked and the grants began most of
  a screen below the fold.
- **The world map picks a region first, then leans in on it.** Choosing a country off a whole world
  map means hunting for Switzerland at four pixels across. Right click steps back out.
- **COMPUTE fits more on a line.** The user charts moved up beside the load dial at a third of the
  width, and the cluster split moved under the capacity band it decides, from three sections higher
  up the page.
- **The lab page lost its strap**, which restated what four labelled tiles and a map already said and
  pushed START below the fold on a short window.
- **BUSINESS opens on one screen.** It was three full-width panels stacked, each showing everything
  it had all the time, so the staff benefits began about two screens down and most players never
  found them. Three short cards in a row now, saying what is running and what it costs, and the
  panel each replaced opens underneath when you pick it. Nothing was removed.
- **The benefits figure says what it is per.** It printed a price per employee without ever saying
  so.
- **A research scientist now does research.** The role shortened a node's calendar and did nothing
  to research points, which are the gate money cannot open: a salesperson moved them exactly as much
  as a scientist did. The contribution is weighted by role now, and researchers work something out
  on their own between runs, so a lab full of them is a lab rather than a payroll.
- **The corner cards only appear where the corner is free.** They were drawn over the business
  page's own figures.
- **The green guide card pulls the phone out** rather than starting the walkthrough on its own. A
  tutorial that begins because your cousin rang is a favour; one that begins because a panel
  appeared is a feature.
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

- **Nothing tested that clicking a person in the office picks the right one.** The panel was
  covered and the arithmetic that decides who is under the cursor was not, which is how a playtest
  came to report it as doing nothing. Five tests now hold it, including the one that matters most:
  somebody off shift keeps their place in the roster, because dropping them renumbers everybody
  behind and opens the wrong person's card.

- **The music stepped up in volume every time a loop turned over.** Reported as the loops not
  looping. Measured: there is no click, the join is continuous to the sample. What there was is a
  step in density, because the arrangement thins out towards the end and the opening comes back full:
  the creator loop ended at 26% of its peak and restarted at 67%, every twenty four seconds. The end
  of each loop is now crossfaded with its own beginning, so the edge becomes a two second ramp.
- **The creator called the same skill two different things.** The "(i)" card said PROGRAMOWANIE and
  the row beside it said ROZWÓJ, because yesterday's catalog pass gave the skills a second set of
  names in the phrase book beside the set that had been there since August. One fact at two
  addresses; it now reads the older key, which is the one whose Polish was written with the
  explanation in front of it.
- **Sixty events were kept in memory for a screen that was never built.** `recentEvents` was appended
  to and trimmed and never once read.

- **The official page was always about the newest model.** With two products on sale, clicking the
  other one still gave the name and the figures of the flagship, because every reading on the page
  came from `Flagship()`. There is a row of tabs above it now, one per product, drawn only from two
  upward.
- **A smear campaign reached the news as nothing at all.** The event fell through to the wire's
  default arm, so the loudest thing a player can do to a rival was the one thing the reader was never
  told about. One that lands is a story about the target with nobody named; one that is traced back
  is a scandal about your own company and says so.
- **Ignoring a tax demand grew it at 35% a year, silently, forever.** Nothing announced it and
  nothing outside the inbox showed it, so the penalty was worse than the new one and nobody ever saw
  it arrive.
- **The corner banner spoke English on a Polish game.** "Current Subs.", "Nothing on sale yet.", the
  days-left line and the whole management tooltip. The age reads through the plural helper, so it is
  no longer possible for it to print `1 dni`.
- **The training strip printed its percentage in the machine's culture.** Fourth time that fault has
  turned up in this project; it now goes through `UiFormat` like everything else.
- **The two tiles asking where you want to look for people were never translated**, nor were the
  paragraphs under them, and the Polish for both captions had been sitting in the phrase book unread
  since the day it was written. The card passed the English straight in. Found by a duplicate-key
  failure on an unrelated change, which is the third time that guard has paid for itself.
- **The letter asking where you got stuck was never in the game.** Two different texts were filed
  under one phrase key: the letter, and the body of the report window. The second silently replaced
  the first, so the mail from HCK Labs showed the report window's text and the letter reached
  nobody. It has its own key now.
- **Twenty seven strings the interface drew without the phrase book**, including `START TRAINING` on
  the busiest button in the game, `QUIET` on the news screen, and `online` twice in the tutorial
  phone. Two of them already had a translation nothing was calling.
- **Every node in era five was called "Fine-tuning and prompting".** All five Statecraft nodes - the
  end of the game and the most expensive research in it - drew with era one's name and era one's
  description, on the tree, in the completion event and in the news item that announces one. They
  had no entry in the table that maps a node to its words, and the table has a default.
- **A third of the game was still English on a Polish machine, and the cause was one fault
  repeated eleven times.** A catalog that stores a display string is built once at start-up and
  keeps whatever language it was built in, however much else is translated. The research tree was
  fixed this way in August; nothing else was.

  Now translated: the four cabinets in the server room, the ten cards on the creator's SCALE and
  DATA stages, the three hosting packages, the six marketing channels, the five model types, the
  six architecture families, the five staff roles, the five audience segments, the seven jobs, the
  ten pieces of furniture, the eight corpora with the sentences that say why one cannot be bought,
  and the three regions and sixteen countries a new campaign picks from before it has seen anything
  else. Plus ten panel headings, the BACK button and the sentence that
  says why a training run cannot start.

  **No catalog in the game holds a player-facing string any more.** The one deliberate exception is
  a family you designed yourself, which is called whatever you typed.
- **A message written for the feedback form had never been on screen.** Two different texts were
  filed under one key, so the second silently replaced the first every time the game started. The
  phrase book had one other key written twice as well, with the same string both times, which is how
  the first one stayed hidden.
- **The basement floor never emptied.** Moving a cabinet left one behind on the old square that
  could not be clicked or moved, because the code that clears the floor was looking in a group
  nothing is ever put in. It had also been stacking cabinets on their own squares, dozens deep, on
  every repaint.
- **Every tooltip with a short card threw an exception on hover.** Not visible in the game and a
  steady stream in the log.
- **The tutorial argued with you.** Six steps said "click COMPUTE" and then went on waiting for a
  button after you had clicked COMPUTE. The mechanism for this was written months ago and nothing
  ever called it.
- **Buying a hosting package changed no number you could see.** The packages did reach the fleet;
  the figure that says how many accounts your capacity holds was reading the rent slider alone.
- **The name plates faced a camera nobody was looking through**, so they read edge-on and often
  invisible.
- **Two enum values each meant two things.** A new research node landed on the same number as the
  scale-ceiling ladder, so three nodes silently became unreachable and a fourth reported a
  prerequisite dated seven years after itself. A new ledger line landed on the same number as grant
  repayments, so both wrote into one slot and every figure in that row was the sum of two unrelated
  things. Neither failed to compile. Both are written into saves, where a collision is not a bug but
  a format in which one number means two things.
- **Your product page was signed by somebody called Anonymous again.** The founder name field
  stopped being pre-filled with that word after a playtest found it; the fallback did not, so
  leaving the field empty produced exactly the same page by a different route. A blank name now
  reads as "the founder" wherever it is shown, resolved when it is drawn rather than written into
  the save, because a translated word stored in a file freezes the language it was made in.
- **A fifth of the phrase book had nothing checking it.** 368 of 1,938 phrases are asked for by a
  stem plus a suffix rather than by name, which made them invisible to the guard that proves every
  phrase exists. A missing one renders as its own key on screen, which has shipped once before. All
  368 currently resolve; nothing was watching them.
- **The task strip drew on top of the basement's build rail**, and so did the new guide card. That
  screen owns its right edge, which is why the room's own corner banner already sat clear of it;
  nothing had ever told the two strips that live outside the screen.
- **A long conversation pushed the phone's buttons off the bottom** instead of scrolling.
- **The day beside a message was one grey on two colours of bubble**, and disappeared into the blue
  ones entirely.
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

Save format **v51**. A campaign started on 0.1.0 or any version since opens here and keeps
everything in it.

Two more things are new since v49. **Each model keeps its own last month of trading**, which is what
the second corner banner draws; an older file starts that empty, because a day's take is a share of
that day's revenue weighted by the users the model held and its capability against its siblings, and
the save records none of the three per day. Splitting the lifetime figure evenly across the last
month would draw a flat line for a product that may have been collapsing, and a flat line is worse
than an empty chart because it reads as a measurement. It fills itself in over a month of play.

And **an open legal threat**, which no older file can have, because a smeared lab never wrote back in
the game they were played in. Whether that lab goes to court is rolled on the day their letter runs
out rather than when it arrives, so the threat is a decision that has not been made yet and has to
survive a save: the seventh time in this project something that looked derived turned out to be
causal, and the save replay test is what says so. Every case in an older file was one the player
filed, which is recorded rather than assumed, because there was no other kind.

Three things are new. Which guided tours you have taken, and which you waved away: an older file has
taken none, because it was played in a game that had none, so the offer appears for an existing
campaign exactly as it does for a new one. The conversation with Emil, which arrives empty, because
every line in it is what he said about a company as it stood on one particular day and those figures
are gone. And the state programme, unsigned, because era five did not exist when that file was
played.

Being part-way through a guided tour is deliberately not saved. Quitting during one and coming back
to it half done, with the interface still held shut and no memory of why, is worse than starting the
two minutes again.

Two facts per person are new: a bonus paid, and the hours they work. Nobody has ever been paid a
bonus, so nobody is credited one; handing every existing employee two years of settling-in would
rewrite the loyalty of a whole payroll on load. The hours arrive at eight to four, which is not a
guess but the shift every campaign has implicitly been running.

The cluster split was already saved, so an older campaign arrives at the setting it has been running
on all along.

The four Operations nodes add nothing to the format. What they do to the room is worked out from the
research the company already holds, every time it is asked, so there is no cooling figure to save, no
tariff to save, and nothing that can drift out of step with the node that granted it. A campaign that
researches one and reloads gets exactly what it had.

### Under the hood

- 1076 EditMode tests across 113 fixtures, and 30 PlayMode across 8.
- 2,481 phrases in the book, both languages complete, none written twice.
- **The whole cash achievement ladder was unreachable, and now it is measured.** Fifty billion and
  two hundred and fifty billion were marked unverified; the probe found that even the *first* rung at
  a hundred million was above the best campaign anybody had played. Across five playing styles over
  fourteen years the richest run peaked at **$71M and 32,134 fans**. The ladder is now
  **$25M / $50M / $75M / $100M / $500M** and the fan award asks for a hundred thousand, all sitting
  deliberately above the probe rather than on it, because the scripted player is crude by design and
  a person beats it. The first rung had to clear the twelve million the company starts with, which
  `AFreshCompanyHasEarnedNothingButStarting` caught on the first run.
- The probe's report reads the thresholds out of the catalog now, after the first version printed
  them as literals and was stale within the hour.
- **A rival strategy either belongs to a lab or belongs to nobody, and now a test says which.**
  `CompetitorStrategy.FastFollower` has no lab, which is fine and deliberate. What was not fine is
  that giving it one tomorrow would have produced a General-only company on the middle numbers rather
  than the lab its own comment describes, because it has no rung on the type ladder, no serving cost,
  no cadence and no capability gain. Nothing would have failed. This is the same fault that left two
  of the five model types dead by construction for fourteen years of game time.
- Achievements are three files and one call site: a table in `Data/`, a pure function over the
  campaign in `Simulation/`, and `PlayerPrefs` in `Persistence/`. No rule reads any of them, so a
  change there can move what a player is told and cannot move what a player is charged.
- **Two thresholds are not verified.** The top two cash achievements ask for $50B and $250B, and the
  fan achievement asks for a million. Nobody has finished a campaign to see whether those are
  reachable. Each is one number, and the name and the Steam id stay whatever happens to it.
- Two of the three achievements that arrived unwired turned out to be readable rather than moments:
  a cabinet where the fan beats the card it displaced is a fact about the floor, and a month under
  water at load is the debt counter next to yesterday's utilisation. The third, coming through an
  inspection, genuinely leaves no trace, so the rules announce it into a list the shell drains on
  the same tick. That list is not saved: by the end of the tick it is already in `PlayerPrefs`.
- 59 research nodes across five eras and four tracks; 33 world events.
- The map is a 43 kB binary baked from public-domain data by `Tools/bake_world_map.py`, rather than
  parsed at runtime: the source is 725 kB of JSON whose coordinates nest four deep, which Unity's
  own reader cannot express at all.
- `Docs/AUDIT.md` records a sweep for mechanisms that exist and never run, with what is still open.
- New guards: no two names in a saved enum share a value; every phrase a catalog builds resolves in
  both languages; every step of a guided tour that waits for the player can actually be satisfied by
  a screen; and every presence the shell builds is also driven - the last of those written after
  building a thirteenth unreachable mechanism while fixing the twelfth.
- `CompetitorStrategy.FastFollower` is assigned to no lab, so nothing in the game runs that brief.
  Found by a guard that asks whether every rival trait can actually occur.
- The unreachable-mechanism sweep was run again over every public mutator on the simulation and the
  company, and two methods with no caller anywhere in the repository were deleted. One remained:
  buying a corpus for cash, complete and tested since the day it was written and called from
  nowhere. It has a control now.
- **The audit was audited, and four of its findings did not survive.** Its sweep read `Scripts/` and
  stopped, so it reported four constants as unread that `Editor/` reads twenty times between them to
  build the city and the basement, and one method as having no caller when it has one. Five
  genuinely dead constants were removed; the four live ones were kept and the document corrected.
  A repository is not `Scripts/`.
- The published counts of dead phrases and selectors do not survive re-measurement either, and
  neither does the method: 609 of 1,985 keys are never named by a literal, but a key reached as a
  stem plus a suffix is invisible to that instrument exactly as it is to the localisation guard, and
  that shape covers every research description and every grant name. Nothing was deleted on a number
  that would have taken the research tree's own text out with the rubbish.
- New guards: the four Operations nodes each move the constant they claim to and nothing else; no
  two research nodes share a name or a description, which is what era five was doing; no phrase is
  written twice in the book, which has to read the source because the duplicate is gone by the time
  the dictionary exists; and the ten training choices resolve their words in both languages, which
  they now need because they read the book by a stem and the literal-reading guard cannot follow
  one, and the same for every other catalog that stopped storing its English.
- The DATA stage has a proof frame of its own. Neither contact sheet reached it - the tab sheet
  opens the creator on its first page and the research sheet stops above era two - and rendering it
  is what found the English panel in the middle of a Polish page.
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
- The conversation stores its text rather than a phrase key. What he says about his own company is
  read off the board on the day he says it, so resolving a key on load would replay a first-year
  message as a tenth-year opinion, and nothing would report it.
- Four faults found by rendering the screens and looking at them, none of which any test could see:
  a proof render photographing the page before the one under test, because a panel's scheduler does
  not tick until the element is in a panel; a flex child that grew but never shrank; a container
  sizing to its own widest label on a screen that centres its children; and three absolutely
  positioned strips sharing one corner.

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
