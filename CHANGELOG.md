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

**Two products stopped being one product printed twice.** A company selling a second model watched
both corner banners report the same audience to the last decimal, because the archive asked the
market how many people were on that *kind* of model and handed the whole of that answer to every
model of that kind. There is one split now, it divides a kind's audience between the lines selling
it by capability, and the parts add up to exactly what the company holds.

**And the screen stopped taking itself away from you every second and a half.** A day rolling
over rebuilds whatever page is open, which is right, and it was throwing away everything the
player was in the middle of: the card opened to read about a four month programme, the "(i)"
explaining the control under the cursor, and one rendered frame of the reading position. All
three survive a redraw now. So does the figure at the top of the screen, which used to be the
opposite problem: it only moved when a day went past, so money spent while paused did not
show at all.

**And the game can be played without a mouse.** Half these screens are taller than the window
and there was no bar to drag and no key that moved anything, so a section continuing below the
fold read as a section that had been cut off. The arrows scroll now, and there is a bar down
the right edge that is drawn over the page rather than beside it, so nothing on the page moves
when it appears.

**And the type is a size you can read.** Fifty eight rules in the stylesheet were setting text
between 8 and 10.5px, including the day count on an effect badge and the captions under every
chart on COMPUTE. There is a floor now, and a test that holds it. The effect icons are 30% larger
with nothing moved to make room, and the bar across the top reads like a set of figures rather
than grey text with the money shouting over it.

**The server room says what every cabinet is doing, in five colours.** A cabinet with nothing in
it used to be drawn the same green as one working happily under its rating, so a half empty room
read as a room that was fine and there was nothing to click. White is a cabinet producing nothing,
light blue is one working with space for half as many cards again, green is normal, yellow is full
output with no headroom left, and red is a cabinet past what it can cool and losing work you are
still paying the power bill for. The key is printed above the floor, the cabinet you open carries
the same band, and the corner banner reads the same word.

### Fixed

- **Nothing in the balance probe had ever owned an accelerator.** Both scripted operators rented,
  every year of every seed, so fourteen years of measurement reported a power draw of zero and a
  power bill of nothing, and the room, the cabinets, the heat and the electricity were the part of
  this economy that no measurement could see. There is a third operator now that buys its silicon
  and fills a basement, and it found the thing the tariff question was really about: a company
  that owns its compute stops growing at **2,500 kW** at the end of its third year, because that
  is what the colocated tier supplies and it does not rise however much is bought. Power runs
  between 0.8% and 15% of the fleet bill and sits around 3%. The rate is not the problem.

- **A fan was cooling that cost nothing.** The cabinet panel said a fan takes a slot and it did,
  right up until the next day: the nightly refill put a full set of cards back into the cabinet
  around it, so the floor held as many accelerators with fans as without. The whole cooling trade,
  one card given up for one fan, was never actually charged, and the catalog's own table promising
  "3 cards + 1 fan" on a 2027 generation was describing a game the code was not playing. One
  reading of how many cards a cabinet holds now, and everything asks it.
- **The parts shop refused to sell without saying why.** Silicon for the room is bought at the
  colocated tier, which asks for one shipped model and five million in the bank, so a company
  handed the room at the end of the tutorial had every BUY button in the shop fail at the till.
  The shop reads the gate now, says which condition is missing in the tier's own words, and the
  buttons are off rather than dead.
- **The corner banner's heat colour turned amber at a different load from the floor.** It compared
  a ratio that had already been scaled to the throttling point against thresholds written for the
  unscaled one. There is one palette now and one place that picks from it, and a test reads the
  five colours back out of the stylesheet and compares them with the ones the room paints.
- **There are only so many people.** The author said the user numbers felt wrong against how many
  people really use AI, and he was right, in the opposite direction: measured over a played
  campaign, the game's whole market reached 1.86 billion users at the end of 2024, 6.79 billion in
  2026 and **11.99 billion in 2028**, which is half again every human alive. A user is token demand
  divided by how much one person gets through, demand grows faster than appetite does, and nothing
  ever said there was a limit. Each audience now carries the most people it can ever be, and the
  count approaches it instead of walking past: consumer 4 billion, enterprise 900 million as seats
  rather than companies, creative 350 million, agentic 250 million because those are organisations
  and not people, developers 45 million because that is what the industry surveys count. The same
  campaign now reads 1.30 billion at the end of 2024 and 3.27 billion in 2028.
- **And "the unserved share of the market" meant nothing.** It divided the derived user count by a
  number that was the same derived user count, so it could only ever read zero. It is measured
  against the population now, which is what its name always said.
- **Two of the fifteen bottom bar buttons were unreachable on a narrow window.** The row is aligned
  to the right and clips what does not fit, and the slots were a fixed 88px that could not shrink,
  so on anything under about 1,860 virtual pixels the left-hand ones were laid out past the edge of
  the row and clipped away. On a 16:10 window, and in the panel a test runs in, SITE and MODEL were
  simply not on the bar and nothing said so. They give way now, down to 62px, which fits all
  fifteen with room for the clock.
- **The phone stood on the bottom bar and took four categories with it.** It is above the bar now.
- **A day stopped costing more the longer you played.** Measured: one simulated day cost 0.14ms at
  ten live models and **9.7ms at 339**, which is more than half a frame, on every day the clock
  turns over, at a speed that runs three of them a second. All of it was four places that walked
  every model the company had ever released to answer a question about one of them, inside a loop
  over every model. The rule they answer is unchanged; it is worked out once per pass now. 339
  models: **9.7ms to 2.0ms**, and the growth is linear rather than square. Sixty staff and a full
  basement cost nothing measurable, then and now.
- **The model creator took no clicks at all, and neither did the corner of the site screen.**
  Reported as totally critical: the whole BRANDING page was dead, including the name field and
  NEXT, while the stage rail above it and the bar below it worked. Two invisible things were lying
  on top of them. The corner banner column was given a bottom edge so it could not reach the bottom
  bar, which also gave it a fixed height on every screen: measured, it covered x 1,309 to 1,625 of
  a 1,646 wide panel from top to bottom, so the right third of every page took no clicks. It no
  longer takes a click where it draws nothing, and it is not there at all on screens with no
  banners. The phone is the other one: 330 by 660 in the same corner, over NEXT and over all four
  site icons. Walking to another screen puts it down, and an unanswered call from the cousin
  becomes "call me back", which already existed.
- **A new company is told what a run would cost before it has rented anything.** PROJECTED
  CAPABILITY, TIME TO TRAIN and CASH IT BURNS all read "-" until the player happened to walk onto
  the compute page and move a slider, and nothing said that was what the screen was waiting for.
  Everything there is derived from the fleet and a company on day one owns none. The creator prices
  the plan against a proposed 150 petaflops and **bills nothing for it**: the rent is a proposal
  until the player moves the handle or starts the run.
- **The model creator threw away what you set the moment you looked at another page.** Reported by
  Francisco, 1,391 days into a campaign: set the parameters and the tokens, step over to safety,
  come back, and both are at their opening values again. Every control in the creator is a shared
  instance so that it survives a page change, which is the design. The page around it is rebuilt on
  every visit, which is also right, because the figures printed beside it have moved. But the
  builder configured the control as well as placing it, so each visit put the handle back where it
  opened. The model name did the same thing, back to "Muse 1".
- **And the same fault was cancelling the fleet.** He reported that as a second thing without
  knowing it was the same one: the capacity goes to 100% and no amount of renting brings it back
  down, until it suddenly does. The rent slider is the one control in the creator that writes to
  the company rather than describing a plan, and it opened on 150 petaflops. Worse, a fresh slider
  runs 0 to 10 until the compute page is built, so a company renting four thousand had its handle
  clamped to ten and that ten written straight back, in the constructor, before the screen could
  tell it anything. Opening the creator at all was enough. The handle is seeded from what the
  company actually rents before anything is built, its range is set before its value is ever read,
  and it keeps whatever the player leaves it at.
- **Every product on sale reports its own audience.** Reported by Natalia. Two general models on
  sale both read 1,962,294 users on a measured seventy day campaign; they now read 1,229,389 and
  732,905, and the two add up to the 1,962,294 the company actually has. The same split feeds the
  daily revenue attribution, so a product's takings follow the people who are actually on it.
- **A headcount is no longer drawn as money.** The follower banner passed its user count through the
  slot the lead banner uses for the month's net, so 1.96M people were printed as `+$1.96M` in the
  green a profit gets. The two cells under a product are now that product's own takings: everything
  since release, and the last 31 days.
- **The MODEL table agreed with nothing.** It divided the company's audience and the month's
  subscriptions by a weight it computed for itself, so the table and the banner beside it quoted
  different figures for the same product and nothing said which was right. Both now read the split
  and the daily record. Its four column headings were also English on a Polish build, and the last
  of them said NET INCOME over a column that has only ever held subscription takings.
- **The MODEL table listed models that are not on sale.** A model superseded inside its own line
  earns nothing and is chosen by nobody, and the old weight handed it a share of both, under a
  heading that says ON SALE.
- **BY DAY in WHERE THE MONEY WENT did nothing, twice.** Reported by the author. The big figure
  stayed on the month whichever toggle was lit, and the lines underneath added up all thirty one day
  buckets, which is the month again. It reports one day now, the last one recorded: its own
  headline, its own lines, and bars that are days rather than months. Both toggles and the sentence
  under the headline were English literals on a Polish build.
- **The management desk's four figures were about four different things.** On a product tab the
  third tile showed that model's whole lifetime take under a caption reading THIS MONTH, and the
  fourth showed its user count as money. The row is now this product's users, its payers, its last
  31 days, and the company's net, which its own footer already said it was.

- **A day going past no longer takes the screen away from you.** The research card you opened to
  read about a four month programme stayed open for about a second and a half, because a day rolling
  over rebuilds the page and the card was thrown away with it. The same for the "(i)" cards. Both
  survive a redraw now and are rebuilt rather than frozen, so the points a card is quoting keep
  climbing while you read it. Both still close when you leave the screen.
- **The page no longer jumps when the day changes.** The reading position was captured and put back
  one frame later, which left a single rendered frame at the top of the page every day. The page
  keeps its scroller through a redraw now, so the position is never lost and there is nothing to put
  back.
- **Money spent shows as spent, without waiting for tomorrow.** Only a day rolling over redrew the
  bar across the top, so buying a $24.5M office or paying a demand left the figure up there showing
  what the company had beforehand. Paused, which is when most spending happens, it never caught up
  at all.
- **The model you just finished training can be improved before you release it.** Reported by
  Natalia. With one model on sale and a second finished and waiting, UPGRADE offered only the first,
  so the only way to reach the new one was to release it, which is the decision the upgrade work is
  there to inform. It is on the picker now, marked as not released, and the work starts straight
  away rather than going through the release planner: there is no version to name and no price to
  set for something nobody can buy yet. The programmes ship with the model.
- **The way out of an empty UPGRADE screen has never been drawn.** The button that takes a company
  with no models to the creator was wired inside the handler that opens the release planner, so it
  only existed once the player had already commissioned a release. Misplaced by one line on
  2026-08-25.
- **A refusal on the UPGRADE screen was an English sentence on a Polish build**, and it is the one
  the player reads at the moment the button turns them down.
- **Every page can be moved with the keyboard, and shows a bar saying there is more of it.**
  Reported by the author, who plays on a laptop with no mouse. Half the screens here are taller than
  the window, and the scrollers were built with the theme's own scrollbar switched off, because that
  bar takes its width out of the content and would re-flow every page in the game the moment one got
  long enough to need it. So there was nothing to drag and no key that moved anything, and a section
  continuing below the fold read as a section that had been cut off. Arrows scroll, PGUP and PGDN
  move a screenful with a sliver of overlap, HOME and END go to the ends, and the bar down the right
  edge is drawn over the page rather than beside it, so nothing moves when it appears or goes away.
  It hides itself whenever the page fits.
- **The arrows stay out of the way of the controls they would fight.** A slider and a dropdown are
  both driven with the arrows and both are everywhere in this game, so the page does not move while
  one of them has the keyboard. Nor does it move behind the pause menu.
- **The list of keys exists.** `KeyboardShortcuts.All` has said since it was written that the
  interface reads it, so a key cannot be bound in one place and described in another. Nothing read
  it: the only caller anywhere was a test. The four keys the game already had were undiscoverable,
  which is the half of "unplayable without a mouse" that adding more keys would have made worse.
  They are on the settings page now, printed from the table that binds them, in both languages.
- **Nothing in the game is set smaller than 11px any more.** Reported as the single biggest problem
  with the game, from a laptop. Fifty eight rules were between 8 and 10.5px: the day count on an
  effect badge was 8px, the research tree drew its own node names at 11 and the chart captions on
  COMPUTE at 10. Forty seven more were raised on top of that floor across the nine screens named in
  the report: the upgrade strip, MODEL, RESEARCH, ARCHITECTURE, UPGRADE, COMPUTE, RIVALS, INTEL and
  the news. The news section headings went from 12px to 17.
- **The effect badges are 30% larger and nothing moved to make room.** The day count used to sit
  under the icon, which left the art 23px and the number 8px inside a 46px bar, so neither could
  grow without the badge outgrowing the bar. It sits beside the icon now, so the badge grows
  sideways into empty bar rather than downward into the page.
- **The bar across the top reads like a set of figures rather than grey text.** The money was the
  only thing on it set at a readable size; REP and the following were 11px captions floating between
  the money and the effects. They are chips now, the research points icon is larger, and the bar is
  52px rather than 46 to carry it.
- **Thirteen English labels on Polish screens.** INCOME, COSTS, FROM SUBSCRIPTIONS and SUBSCRIBERS
  on MODEL, named in the report; WAGE and WORTH on HIRING; five readings on the management desk; and
  SCANDALS and PREMIERES, which were the two English words on a news page whose stories were already
  Polish. **Six of them already had Polish in the phrase book and no reader at all**, which is the
  third time this month that a duplicate-key failure has pointed at a screen that was never wired
  rather than at redundant text.
- **The INTEL screen sold three memberships and made the case for none of them.** The news page has
  carried a SEE BENEFITS button since it was written; the screen whose whole job is choosing between
  the three desks had a name, a sentence about what the outlet is, and a price. The pitch and the
  benefits answer different questions, and the second one is what somebody with their hand on $400k a
  month is actually asking. Both screens read one copy of it now.
- **A stalled research node says what to do about it.** Running out of cluster is the most common
  state in the opening hour, because a new company has rented nothing, and the strip reported it as a
  fact with no way out. It carries a button that opens COMPUTE and lights the rent panel for two
  seconds.
- **The BANK tab said BANK and the screen it opens said BANK AND DONATIONS.** The tab agrees with the
  page now.
- **Corporation tax is a decision now, and both answers are one click away.** Reported by Natalia.
  The demand arrived as a strip and a letter: POSTPONE was a button on the strip and PAY meant
  opening the inbox and finding the letter, so the dearer answer was the easier one. The clock stops
  and a card asks, with the two answers the same size, because making one of them bigger would be
  the game telling the player which is correct.
- **Postponing says what it cost, on the day.** Two lines: the rate and what it added, then the new
  total and the new date. It used to happen the instant the button was pressed and the price
  appeared in next January's assessment, which is where a player stops connecting the two.
- **Three years of postponement now ends the way the report asked.** Running the allowance out and
  then waiting used to cost nine per cent and buy another year, with the three postponements
  restored against the new demand: the ceiling was a closed door standing beside an open one, and
  corporation tax was rollable forever for a predictable annual fee. Past it the revenue takes the
  whole arrears with a 20% refusal penalty, on the day, whether or not the account can stand it.
  The card warns on the postponement that earns it rather than afterwards.
- **The three-postponement ceiling was one day out of reach.** `LongestDeferralDays` was 913 and
  three steps of 304 reach 912, so a fourth postponement was accepted: it moved the date by a single
  day, charged another 8.6% on the whole balance, and read as a button that did nothing. The ceiling
  is derived from the number of postponements now, so the two cannot drift.
- **A model goes on sale on terms you chose.** Clicking a model on RELEASE shipped it, on the spot,
  at whatever price the company happened to be charging. Two hundred days of training and most of
  the company's cash went on sale in one click with no screen in between, and the tutorial tells you
  at step 44 to click it and set something. There is a card now: what it costs a month, whether
  there is a free tier, and how much of one.
- **The release tiles show the model.** They were a title and two grey lines. They carry the silicon
  plate the upgrade screen draws, with the company mark on it and the model's name stamped across
  it.
- **The card says that the price belongs to the company, not to the model.** One subscription price
  covers everything on sale, and a player who learns that by watching an existing product's revenue
  move has learned it the expensive way. The line only appears when there is something already
  selling.

- **The tutorial says when it is waiting for the clock rather than for you.** At step 40 Emil
  says nothing happens for months, and then nothing on screen said anything was happening. Those
  steps carry no button, on purpose, because the next thing is the game's move and not the
  player's, and from the chair that is indistinguishable from a tour that has frozen. There is a
  line and a bar under what he says now, on every step that is waiting on work.
- **The architecture step is a decision instead of a demonstration.** He explained the house
  family, set the five sliders, and walked on, so the player was left looking at a page they had
  just been told decides the next five years of every model they build, having done nothing with
  it, and the tour never came back to it. Two answers now: start it, which quotes how long it
  takes and actually commissions it, or not now. Saying yes keeps him there until the family
  lands. Saying no walks straight on.
- **You can get back out of a region on the map.** Reported. The map leans in on whatever you pick,
  which is right for reading one region and takes the other two off the picture, so the only move
  left was to choose one of the countries in front of you. The three regions are named above the
  map now, always, along with the whole world, so switching is one click rather than a zoom control
  that has to be found first.
- **The opening is skippable.** Reported: thirty seconds of text you have already read. Clicking
  anywhere puts the rest of it up at once, and anybody who has been through it before is offered the
  whole opening skipped from the first frame. Whether you have seen it belongs to the player rather
  than to a campaign, so it is remembered the way the achievements are.
- **Anything running can be abandoned, and abandoning it costs.** Asked for by a tester who gave the
  numbers with the request: he misclicked into a two hundred day run he did not want and had to
  watch it finish. All three could already be thrown away, which was the opposite fault, because
  they could be thrown away for nothing at all.
- **A research node keeps four fifths of itself.** Abandon one two hundred days in and it comes back
  at one hundred and sixty, which is his own example. The money it was started with does not come
  back, so stopping and starting is never free.
- **A run, an upgrade or a family programme costs a tenth to walk away from**, charged on what it
  has already spent rather than on what it was going to cost. A misclick noticed on the first
  morning is nearly free, which is the case he was describing, and abandoning a run six months in
  is expensive.
- **Upgrades could not be stopped at all.** Four months and most of a quarter, committed, with no
  way out. There is an abandon on every programme in the corner strip now, armed before it fires.
- **The calendar is never handed back.** What a cancel buys is not spending the remaining days on a
  plan you no longer want. The days already on the cluster are gone whatever you do, which is what
  keeps this inside the spine rather than turning it into a reroll on every decision in the game.
- **Furnishing the office yourself is the cheap way, and the pack is the lazy one.** It used to be
  the other way round: the standard fit-out undercut the same pieces by nine per cent. That held
  while there was nothing else to do with the room. The build mode is permanent now, so arranging
  the place is the game and the pack is the way out of playing it, at two and a half times what the
  pieces cost one at a time. $43k by hand, $107.5k for not having to choose.
- **And a furnished office is still a room you can rearrange.** Everything the pack delivers is
  ordinary furniture on the ordinary plan: it can be lifted, stood somewhere else and sold back like
  anything bought by hand.
- **The buy and move buttons on the premises page were invisible.** Reported by two testers, one of
  them on his first day, both as clicking a bigger office doing nothing. It did nothing because
  there was nothing there: the row was a fixed 202 pixels that clips, the buttons are the last thing
  in it, and they were drawn sixty four pixels below the bottom edge. They were in the page, laid
  out and enabled, the whole time. The row sizes to what is in it now.
- **And the card itself opens the deal.** One of the two said plainly that he assumed you buy an
  office by clicking the office. He was reaching for the obvious control and there was not one.
- **The tower had no room in it.** It was added to the office ladder in August, and the lookup that
  answers "which room am I looking at" falls back to the house when a tier has no entry. So a
  company that leased the largest building in the game, at $380M, walked into an empty frame.
  Nothing failed: the stage loaded nothing and drew the floor it already had.
- **Buying an office outright works too.** Reported by a tester as not working. Two of the six
  places carry a purchase price and the screen draws a buy button only for those, so the likeliest
  reading is a button that was never there rather than one that refused. Both halves are now held by
  a test: the two that are for sale really change hands and stop charging rent, and the four that
  are not refuse with a reason.
- **Moving out of the first office works, and now there are tests that say so.** Players report that
  the starting house makes hiring impossible, which is true and is the design: it has no desks. What
  had never been checked is that leaving is affordable on day one with the money the company is
  handed, that the move actually hands over the desks, and that somebody can then be hired into one.
  All three hold.
- **GRANTS AND DONATIONS stopped being the ugly half of that page.** Reported. The tier was an 11px
  gold caption carrying two facts under the title; it is a rank on the heading line now, right
  aligned, with what opens the next rung on the hover rather than competing with the line above it.
- **The torn plates are gone.** Every award title and all four of its figures sat on a drawn ragged
  shape. A torn edge is a voice, and it reads as handwriting on the tutorial and as damage on a page
  of contracts. The titles are rectangles that run out to the card's own edge, so the title and the
  coloured rule down its side are one object instead of two things near each other.
- **And the four figures were being clipped.** The plate used to overhang its own box, so at 46px
  the descender came off every number. They are 56px and readable.
- **You can see who owns your company.** Reported. The screen gave a founder percentage and nothing
  else: no names, no idea what anybody paid, no sense that an investor is a person on your board
  rather than an abstraction you dilute yourself against. There is a bar now, always the whole
  company, with the names under it and what each slice is worth.
- **Emil holds two per cent from the first day.** Somebody believed in this before there was
  anything to believe in. It is seeded rather than raised, priced at nothing, and it is the favour
  the whole tutorial stands on.
- **Every investor says what they have made from you, today and this month.** Their share of the
  profit rather than of the revenue, so a bad month costs them too and the figure goes red.
- **BROWSE OFFERS, and there is more than one.** There used to be a single term sheet from nobody,
  which is a yes or a no rather than a decision. Twelve named firms now turn up unasked, between two
  and twenty days apart, up to ten standing at once, and none of them is the same money: the ones
  that pay the most want the most of you and give you a fortnight, and a pension board will lowball
  you and then wait five months for an answer.
- **Who turns up replays identically.** The schedule is derived from the campaign seed and the day
  of the last call, so reloading cannot re-roll the firm that just offered you a bad price.
- **What the company is worth stopped reading like a cheat.** Reported. A lab that shipped one model
  at the frontier in its first year was priced at $1,236,036,036 while holding twenty million
  dollars and having never invoiced anybody, and then collapsed once rivals shipped. Both halves
  were the same fault: the price was a story about one model and nothing else held it up.
  Investors now pay for that story in proportion to how far they trust the company telling it,
  measured on the two numbers the game already keeps. The same lab is priced at $177,000,360, and
  once it has earned a reputation and four hundred thousand followers, $878,003,936.
- **Revenue is not discounted and never was a story.** A company being paid by real customers has
  already proved the thing reputation stands in for, so its run rate counts in full whoever it is.
  Being unknown costs you the story, once, rather than twice.
- **Falling behind still costs, and no longer erases.** A company that stands still while the
  frontier moves has to lose value, which is the spine of this game. What changed is the size of
  the drop: revenue and a following are the floor under it now, so six months of standing still is
  a serious loss rather than the company nearly ceasing to exist.
- **You can see who works for you.** Reported. The only way to reach a person was to open one
  discipline at a time, so a company of six was six clicks and no way to compare anybody with
  anybody. There is a list now: who, their role, how long they have been here, how loyal they are,
  their level and what they cost an hour. Pressing a heading orders the list by it.
- **The job tiles are a quarter of the height and half the width.** Reported: they were too big and
  over half of each one was empty. Eight of them now sit in two rows of four in a narrow column on
  the right, icon and name side by side with the count and the hourly rate, and the sentence that
  used to be squeezed onto the tile moved to the card that opens when you press one.
- **A job nobody holds can be opened.** The tile used to disable itself when the count was zero,
  which is exactly the state where a player wants to know what the job is and what it costs. The
  card says what the job does, offers full time and remote, and lists whoever is in it or says
  plainly that nobody is.
- **Hiring moved onto the card for the job being filled.** It was a bar under the grid that said
  nothing about which discipline it was going to fill, so choosing a job and hiring somebody were
  two unrelated actions on one screen. Full time is refused with the reason named when there are no
  desks, rather than sitting there grey.
- **SUPPORT is the eighth job.** The cheapest position in the game, and it counts where the rest of
  the customer-facing work counts, so hiring one does something real rather than waiting for a
  system that has not been built yet. It is a job the company hires and not an eighth founder
  skill: the creator still asks for two hundred points across seven.
- **The price you set when shipping a model is the price the company charges.** It was not.
  A new company billed per token, and the monthly fee is read only on a subscription, so the
  figure asked for in the creator and again on the release card reached the version list, was
  printed back on the release screen as what people pay, and never entered the takings. The
  company charged the market rate whatever the card said. A company now opens on a subscription,
  which is the one thing every other screen in the game asks about.
- **And the opening fee is the market rate, not a quiet discount.** The old default of $20 a
  month converts to a quarter of what the market charges, so switching billing models on its own
  would have handed every new campaign a 75% price cut nobody chose. It opens at the figure that
  charges exactly the going rate on day one, derived from the two numbers behind it rather than
  typed in.
- **A consequence worth knowing before you meet it:** the going rate falls by about half a year
  and a monthly fee does not, so a price left alone becomes an expensive one on its own, and far
  enough past the market it gets written about. The page says where the fee sits against the
  market, in a sentence, every time you open it.
- **The price cannot be changed by brushing the control.** The slider is locked. CHANGE opens it,
  APPLY commits it, and a card comes up first saying the subscription is one price for everything
  the company sells. That card appears every time and not only on ALL MODELS, because the change
  is always the whole catalogue: the game prices one service, on purpose.
- **The four figures from the official page are on it.** Registered, paying, the last 31 days and
  the month's net, under the fee rather than a screen away from it, and a picker above them says
  which product they are about. It is the same element the official page draws, so the two cannot
  drift apart.
- **Both marketing sections moved to MARKETING.** A tab named after the subject and a spend panel
  on the pricing page were two doors into one decision. BUSINESS is now money coming in and the
  standing cost of the people who make it.
- **The architecture screen was quoting a length the programme does not run for.** DURATION read
  the designer's own figure, and the founder, the research staff and the home country all move a
  programme's length before it reaches the calendar. A default company opening the screen was told
  365 days and would have got 317. Both the reading and the new button now say what the
  calendar will do.
- **The corner stopped printing three things on top of each other.** Reported by Natalia, who had
  models on sale, a run going and a node running at the same time and got all of it in one place.
  The product banners grew downward from the top of that corner while the research strip was pinned
  at 214px and the upgrade strip at 458px, so the more the company was doing the worse it got. That
  corner is one column now, in a fixed order: the products, the node, the upgrade, the way out of
  it, and whatever walkthrough is on offer. Filling one part cannot land on another.
- **And the column can no longer run off the bottom of the screen.** It is bounded by the window
  rather than by how much is happening, and the products half scrolls, because that is the half
  with no limit on it: there is one banner per product and no rule about how many a company may
  sell. The node and the upgrade keep their places whatever is above them.
- **The two figures under each product were drawn over each other.** `EARNED ALL TIME` and
  `LAST 31 DAYS` were side by side on a 268px line and captions do not shrink below their own text,
  so the second pair printed across the first figure and the lead banner cut `SUBS. EARNINGS` off
  at the card edge. Caption above figure now.
- **The way out of an upgrade was laid out underneath the thing it stops.** A row is a column
  unless it says otherwise, so the abandon button sat below the programme name inside a box 40px
  tall, squashing the name to half a line to make room for it. The button is beside the days now,
  the row is as tall as what is written in it, and anything still too long ends in an ellipsis
  rather than mid-word.
- **The walkthrough card no longer lands on a banner.** It was pinned at `top: 448px` in the same
  corner, which is the middle of the column on any company selling more than one thing. It is the
  last card in the column on the site and keeps its own place under the room banner in the
  basement, which is the one screen where that corner belongs to something else.
- **The card a product is managed and sold from is not from a different application any more.**
  It was the last near-white surface in the game and the one on screen the longest: a cream card
  with a maroon slab across the top, in a column beside the research strip, the upgrade card and
  the offer card, every one of which is dark with a hairline and a coloured kicker. Same palette
  as those and as the news and grant banners on the other side of the screen. The two figures
  under it are larger and their green and red are the outline and the tint rather than a filled
  block, because five filled chips in one column is the brightest thing on the page.
- **The topicality bar was drawn at nothing.** The two meters shared the row evenly and each bar
  took whatever its caption left, so a reading like OUTDATED took the whole of the right-hand
  half and one of the two products meters had no bar at all. Caption above bar now, with the
  reading on the caption line, so both are full width and the same length.
- **The scrollbar could hide itself and never come back.** It measures the track from its own
  box and it was hidden by being taken out of layout, which makes that box zero, so once down it
  stayed down however long the page grew. Every page escaped it by building a new bar each time.
  The corner column keeps one for the life of the game, and it sat hidden over a list three times
  its own height. It is hidden by visibility now, which leaves the box it measures.
- **Every desk in the office could be put into storage with people sitting at them.** The rule
  against it was written when the furniture shop was, and the build mode never asked: it moves
  pieces directly and the rule lived behind a method nothing called. Desks are the one thing
  that caps hiring, which is why the rule exists. Storing one now says who is sitting there and
  how many desks the company has. Moving a desk around the room is still free, because a piece
  on the cursor has not left the floor until it is put somewhere.
- **Moving office left the old one on screen.** The company moved into the loft and the sofa,
  the nightstand, two vases and a gamepad from the house were still drawn, through the new
  floor: two offices in one frame. Hiding the old room walked the things parented under one
  transform, and the house was furnished by hand, so whatever was placed outside it stayed lit.
  What is in the room is now decided by where it stands rather than by what it hangs off.
  Nobody reported this because until this build nobody could move.
- **And a row of white boxes stood in the dark beside the new office.** Moving in stood the desks
  the lease pays for, on top of the ones the floor is built with, and laid them out backwards from
  the room origin, which is off the floor. The lease pays for the desks the room already has, so it
  stands none of its own; a tier that ever promises more than its room builds now stands the
  difference inside the room rather than behind it.

### Changed

- **The parts have a shop.** Reported: the silicon could not be browsed or bought from the room it
  goes into, and the only door anybody found was a pair of cards at the foot of the compute page.
  The room did have a shop, showing three of the twenty two generations in a rail 306 pixels wide,
  which is a shelf. There is a window now, opened from the build rail and drawn over the floor the
  parts stand on: every generation on sale, ordered by whichever column the player asks for
  (newest, power, price, petaflops per million, memory, heat), bought one, four, sixteen or sixty
  four at a time, with the batch total under the unit price. Silicon the game invented rather than
  remembers keeps its projection mark.
- **The price on the row is the price at the till.** It is the same call the purchase makes, with
  the founder and the home country on it, so scarcity cannot move one without the other.
- **The research tree is a board you read rather than a row of circles.** Reported by Francisco:
  *"can you simplify what each research node does? Sometimes you dont really know what something
  does"*. Every node is a card now with its name, its picture, and a row of small icons saying what
  it hands over: a family, a corpus, a compute tier, an upgrade line, a kind of model, or a ceiling
  lifted. **Those icons are read off the node**, not written beside it, so a node that starts
  unlocking something new says so without anybody remembering to edit a description.
- **And the lines between nodes are drawn.** Every node has had its prerequisites in the catalogue
  since the tree was written and the screen had never drawn one of them, so the order a player was
  meant to read the tree in existed only inside the rules.
- **The board has a key.** Four colours and six icons a player has never seen, explained in one line
  above the first era: done, researching, can start, needs something first, then what each reward
  icon means. Built from the same list the cards read, so a seventh kind of reward cannot appear on
  the board and be missing from the key.
- **A node title is 13px and drawn at full size.** The board for era one is about 1,500 pixels wide
  and the band it sits in is about 1,200, so the map was shrinking the whole era to 85% to show all
  of it, which took an 11px title to 9.4px. This project set itself a floor of 11px after a report
  from somebody playing on a laptop, and the board went under it in the one way a stylesheet test
  cannot see: the rule was obeyed in the sheet and broken by a transform. The board opens at full
  size and is scrolled along, which is what an era of cards is for; DOPASUJ still gives the
  overview.
- **Each era reads left to right and opens where you left off.** The board is laid out from the
  prerequisites: the column is how deep a node sits in the chain, and no board is more than three
  lanes deep, so an era grows sideways and is scrolled along rather than stacked into a tower.
  Opening RESEARCH centres the era on whatever is being researched.
- **The furniture in both rented floors is furniture.** Desks, office chairs in eight colours,
  monitors, a five metre kitchen run with a microwave and a water cooler, shelves, a canteen table,
  a conference table with six chairs, a printer and planted pots, all from packs already in the
  project. Every piece is scaled from its own measured height rather than from a guess, because a
  pack models its desk in whatever unit its author liked: the first pass asked a five metre kitchen
  run to fit inside eighty centimetres of depth and it drew as a conveyor belt lying against the
  wall. **A machine without the packs still gets a complete room**, in boxes, which is the same
  rule every art loader here follows.
- **Both rented floors are rooms now rather than grey boxes.** The author sent two isometric
  cutaway references and the difference between them and what the game had was not the objects,
  it was everything around them: one flat slab of floor, one blue stripe for a window, desks at
  even spacing. So the floor changes material where the room changes purpose (stone at the
  kitchen, tile under the desks, boards along the route between them), both glazed walls are
  floor to ceiling with posts every two and a half metres, the kitchen has a stone feature wall
  behind it, the desks are in benches of four facing each other, both glass rooms are framed and
  the big floor's second room has a return so it reads as a room rather than a pane of glass
  standing on the floor, and there are plants in the corners the shop cannot reach.
- **Both rented floors have a photograph on the premises page.** `office_smallhub` and
  `office_bighub` have been named in the catalogue and missing from disk since the tiers were
  written, so two of the three rows a player chooses between carried a caption saying the picture
  did not exist yet. They are a crop of the room's own render, which is the only version of that
  picture that cannot go out of date when the room changes: it comes out of the same prefab the
  player moves into. `Tools/office_cards.py` makes them.
- **The floor the player furnishes was left alone on purpose.** Everything above is placed
  against the room, because the open ground in the middle belongs to the build mode and a
  builder that drops a plant in it would have the shop growing a coffee bar through it.

### Who found what

Asked for on 2026-09-13, and it is worth keeping: most of the list above came from somebody
sitting down with the build rather than from reading the code. Every entry in this file already
names its source in its own line; this is the same information gathered so a reader can see the
shape of it.

- **Francisco** played 1,391 days in one sitting and reported two things that turned out to be one:
  the model creator losing the parameters and the tokens on a page change, and the capacity
  sticking at 100% however much compute was rented. Both are the same rebuilt control. He also
  asked, in plain words, what each research node actually does, which is why the research tree is
  a board with reward icons on it now instead of a row of circles.
- **Natalia** found the corner banners printing on top of each other, two products reporting the
  same audience to the last decimal, UPGRADE ignoring a model that had finished training and was
  waiting on the shelf, and the tax demand that grew in an envelope nobody opened.
- **Two testers** reported the buy and move buttons on the premises page as not working. They were
  drawn and they were invisible.
- **The author** asked whether the user numbers were credible (they were not, by a factor of
  three), reported BY DAY in the finance report doing nothing, asked for the game to be playable
  without a mouse, and asked the questions that produced the third balance operator and the five
  colours in the server room.

### Save compatibility

**Save v54.** What an abandoned research node kept, plus everything v53 added below.
A v53 campaign banks nothing, which is the only true reading: the old rule threw the whole node
away and left no record it had ever been started.

**Save v53.** The register of who owns the company, and the term sheets on the table.
A v52 campaign that has never raised gets Emil's two per cent stated; one that has already raised
does not, because a v52 file records one number and nothing about who holds the rest, and naming
them after the fact would be inventing figures nobody can check. Everything else is unchanged and
an existing campaign reads correctly the moment it loads.

**Previously unchanged, v51.** Nothing new is stored: the split is derived from the market standing and the
model history that saves already carry, so an existing campaign reads correctly the moment it loads.

### Under the hood

- **1,215 EditMode tests and 50 PlayMode**, up from 1,116 and 31, across 133 fixtures.
- **An audit pass over everything added this week, reached the way a campaign reaches it.** Not by
  calling the new methods: by playing until the thing turns up. It found that investors really do
  call in a campaign rather than only in a unit test, that an offer that arrives can be taken and
  leaves a register that adds up, and that the abandon button on an upgrade shipped disabled
  because a constructor took a callback and never stored it. A green suite is not a played game,
  and that is twice this week. `TwoProductsTests` ships two models in separate lines
  and
  requires their figures to differ, requires them to add up to the company, and requires no user
  count to equal a money field. `FinanceDayViewTests` drives the real report panel.
- **The corner is four slots and a scroller, not four things each pinned to a number.** Filling a
  slot cannot reorder the column and cannot reach the slot below it, which is the difference
  between a layout that happens to fit today and one that cannot stop fitting.
  `TheCornerBannersNeverLandOnEachOther` builds the state that was reported (three products on
  separate lines, a node, an upgrade), opens the site and compares every visible banner with every
  other. It measures **what is on screen rather than what the layout holds**: the product half
  scrolls, so a banner below the fold keeps a rectangle running straight through the strip beneath
  it while being clipped to nothing, and reporting that is a fault in the test.
- **That guard has been seen to fail.** It passed on the first run with the fix in, which proves
  nothing, so it was checked against a broken layout and reported the overlap with both
  rectangles. It also counts the banners by kind rather than in total: three products with the
  research and upgrade strips missing entirely would satisfy a count.
- **The review sheet now photographs the state that was reported.** `TabProofCampaign` sells three
  products and has a node and an upgrade in flight, so `tab_site.png` shows that corner at its
  worst. Every pass until now reviewed a company doing one thing at a time, which is the state that
  needed no work. Two faults in this list were found by looking at the new frame.
- **Three passes over the two hub rooms, each one from looking at the render.** The first laid
  all three floor zones at the same height and came back with a band of stripes where two of
  them met, ran the stone half the room so it read as a hole, and slid the desk benches along
  one axis until the last of them hung off the front edge. The second put the benches on a grid
  and the grid ran through the big floor's second room, cutting a bench of four in half with a
  glass partition: two things that each know their own rectangle and nothing about each other.
  Every one of those was invisible in code and obvious in a picture.
- `MovingOfficeLeavesNoneOfTheOldRoomOnScreen` measures the strays against the loaded room's own
  bounds rather than by name, so it does not care what the author called the sofa. It named all
  eight of them on its first run.
- **The sweep for mechanisms a player cannot reach was run again over the whole of
  `Simulation/`.** 102 public mutators, 36 with no mention anywhere in `UI/`, and reading them
  is the job: most are plumbing called from inside the rules, and several are the known false
  positive where the route is one public method to another. Two were worth acting on. The desk
  rule above is one. The other is `CancelTraining` and `CancelArchitectureProgramme`, which have
  been dead since the paid versions were built: they are deleted rather than left as the free
  door somebody wires by mistake.
- `WhyFurnitureCannotBeStored` is the one place that decides, and a test asserts the seat
  arithmetic appears exactly once in the rules, so the screen and the simulation cannot come to
  different answers.
- **The scrollbar is hidden with `visibility`, not `display`.** Taking it out of layout zeroed
  the box it measures its own track from, which is a latch: hidden once, hidden forever. It also
  refreshes itself on attach and a frame later, which the shell was already doing by hand for
  the page bar with a comment saying why. The corner column was the second caller and found
  both.
- `ModelBanner.Meter` takes the reading as a trailing element on the caption line rather than
  having the caller append it beside the bar, so the bar is always the width of the cell.
- `PromptChips.MoveTo` hands the card to whichever parent owns the corner on that screen, and
  remembers where it was built. Recovering that from `panel.visualTree` instead put it above the
  element the stylesheet is attached to, and it drew as a full-width grey slab across the room:
  **USS reaches down a tree and not up it.**
- `CompanySimulation.AudienceOnSale` is the one answer to "how many people are on this model", and
  `MarketedNow` the one answer to "what is on sale". Three callers, where there were three separate
  calculations.
- `ProductStanding` carries `OwnLifetimeUsd` and `OwnRecentUsd` so a product's money and the
  company's month stop sharing two fields between four meanings.
- **One step serves both answers to the architecture question, with no branch in the script.**
  The step that follows the offer waits on `arch_built`, and `AlreadyDone` reports it satisfied
  when nothing is running, so a player who declined arrives with nothing in flight and unwinds
  through it. A branch would have been a second shape of step for one decision.
- **Every test of the premises page passed while it was broken**, because they all asked whether the
  button existed. It did. `EveryOfficeYouCanMoveIntoShowsItsButtons` measures the button against its
  own row's box in a real panel, which is the only question worth asking about a control inside
  something that clips.
- **A guard said it held this and could not see it.** `EveryTierHasARoomToLookAt` read the room for
  each tier and checked the camera and the desk count on whatever came back. The lookup falls back
  to the garage, which has both, so the test passed on another building's numbers every run while
  the tower had no entry at all. It asserts membership now, and the comment in `RoomCatalog` that
  claimed a test held this is finally true. Removing the fix turns both guards red, which was
  checked rather than assumed.
- **One term sheet type, two ways of being handed one.** A round the player goes looking for and a
  firm that turns up unasked are the same piece of paper with a different name at the top, and
  `CloseRound` is the only place equity moves. Building a second type for the second case is how a
  game ends up with two accept paths that disagree about what dilution means.
- **A round with nobody at the top of it diluted nothing at all.** New shares are issued to a name,
  so an unnamed term sheet left the founders owning the whole company while the money still landed.
  The test that asks whether signing dilutes caught it on the first run, and every round has a lead
  now.
- `NoInvestorIsSimplyBetterThanAnother` failed on its first run against the catalogue it was written
  for: the state fund paid more, wrote more and waited longer than the angel fund, so nobody would
  ever have taken the second one. Same guard the marketing channels carry.
- `FundingCatalog.TrustIn` is the one place that decides how much of a story a cheque writer pays
  for. Reputation is weighted 60/40 against the following because an opinion can be bought back in
  a quarter and a following cannot, and the floor is 10% rather than zero, or the first rung of the
  funding ladder would be out of reach of every campaign that has not already succeeded.
- **The proof campaign had no staff, which is why the list of people took a year to exist.** Every
  frame of the team screen was reviewed against an empty roster, so there was nothing on screen to
  miss. It now hires six across six disciplines, and the strongest of them is deliberately not the
  dearest: the first cast had one person top every column and the new fixture refused to run,
  because ordering by wage and ordering by level gave the same list.
- `PositionCatalog.KeyFor` has every arm written out and throws on anything else. A `_` arm is how
  five research nodes shipped drawing another node's name, and it would have put SUPPORT on screen
  as "Coordinator".
- `MonetizationPolicy.OpeningSubscriptionUsdPerMonth` is written as the arithmetic that makes it
  neutral rather than as a number, so the two constants behind it cannot drift apart without it
  moving with them. Same rule as safety effort x1 and the skill baseline.
- `UiParts.KpiRow` and `KpiTile` moved out of `ManagementScreen` rather than being copied, and
  `MonetizationPolicy.PaidShareOfTokens` replaces the same subtraction written twice.
- **Two marketing mechanisms still exist and that is now visible on one screen.** Booked channels
  feed `Awareness`; the flat daily figures feed `MonetizationPolicy.ModelAwareness` and the brand
  directly. Folding them into one is a balance change rather than a screen change, so it has not
  been done, and the comment above the moved panel says so.
- `ArchitectureCreatorPanel.ProgrammeDurationDays` is the one answer to how long a family
  programme takes, and it is the scaled one. The reading and the tour's button read it.
- The tutorial strip has never been rendered into a frame until now, and it is the one thing in
  the game every new player reads every word of. `ScreenProofTests` writes `guide_offer.png` and
  `guide_waiting.png`.
- `Ledger` answers about a day the way it answers about a month: `DayCashFlow`, `DayIncome`,
  `DayCost`, `RecordedDays`.
- `DayRolloverTests` is a PlayMode fixture that needs no clock: `Show(current)` **is** the
  rollover, so opening the tab that is already open runs the identical path a day does.
- `CompanySimulation.UpgradeSubjects` is one list of everything that can be improved, on sale or
  on the shelf, so the screen never has to ask which of two types it is holding.
- `KeyboardShortcuts.ResolveScroll` and `PageScrollbar.ThumbGeometry` are the two halves that can
  be wrong without looking wrong, so both are pure and both are measured: a thumb that is subtly
  the wrong length still looks like a scrollbar.
- The page scroller is made unfocusable, because UI Toolkit gives a focused one arrow handling of
  its own and two things moving one page is indistinguishable from unreliable keys.
- `StylesheetTests.NothingIsSetSmallerThanTheFloor` is a floor rather than a table of sizes: a
  table goes stale at the speed the sheet grows and a floor cannot, so a rule added tomorrow at
  9px fails the first time anybody runs the suite.
- `BootTests.TheBottomBarReallyFits` measures the resolved layout. The guard that already existed
  adds up slot widths out of the stylesheet and charges the controls a hard-coded 360px; they are
  text buttons, so their real width is the width of the words in them, and raising two of them
  took that block to 451px without the old guard seeing a pixel of it.
- **2,754 phrases per language**, up from 2,723.

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

- **The server room sells what goes in the cabinets.** Everything the room offered was furniture: buy
  a cabinet, carry it, stand it, sell it, fit a fan. The accelerators those cabinets exist to hold
  were bought on a different screen and the room never mentioned them, so a basement opened early was
  four empty frames with no parts and no way to get any. The build rail now says how many cards the
  company owns, how many are standing down here and how many slots are empty, and sells the three
  newest generations straight from the room. It is the same purchase the fleet screen makes, so the
  price, the tier gate and the founder's discount are all decided where they already were.

- **Sound and music in the Escape menu.** Both sliders existed and neither was reachable from inside
  a campaign: they are on the main menu, so anybody who found the office loop loud had to leave the
  game they were playing to turn it down. Two independent controls side by side rather than one
  scaling the other, which is the shape they were built in.

- **When the room cannot sell you a card, it says why.** The basement can be opened on day one as
  Emil's gift, while accelerators need the colocated tier and its own gates, so there is a real
  stretch of the game where the room is furniture by design. It prints the tier's own reason rather
  than a padlock. A floor with no cabinets on it says that instead, because buying silicon for a room
  with nowhere to put it is money spent on nothing.


- **The founder can be a woman.** Polish puts gender in the past tense, so forty six lines were
  telling the player what *he* had done: "Sprzedałeś firmę", "Gdzie utknąłeś?". All of them are
  rewritten in the present tense or impersonally, which needs no gender field, no question at the
  creator and no save migration. English was already neutral and is untouched.

- **The creator names the architecture families the company does not have.** It listed only the
  families already held, so an unowned one was not merely unbuyable: nothing anywhere in the game
  named it, priced it, or said what would open it. A player learned that hybrid state space existed
  when a research node happened to hand it over. Each one is a row now, with the licence price and
  the node that opens it.

  **The cash route itself stays shut, and the row says so by drawing the price flat.** Every family
  is gated by a node that grants the family on completion, so a campaign started today never reaches
  a state where `TryAdoptArchitecture` can succeed, and a priced button on every row for the whole
  game would read as something broken. The button is built only where it could be pressed, which
  today is a save made before research delivered its own unlocks. Measured after the rows shipped
  with a button on every line, which is what `ReachabilityTests` now holds.

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

- **Missing a grant costs twice the advance, and a letter says so.** Handing back exactly what was
  taken made an advance an interest-free loan for the length of the term, so the arithmetic said to
  sign for everything on the board and give back whatever did not land. At twice, a programme has to
  be worth finishing before it is worth signing. The letter is there because the banner and the wire
  both scroll and this is a six figure charge on a day nobody was expecting one.

- **The task list rolls up away from the site.** Every task on it is something you do at the
  headquarters, so on the fleet screen or in the basement it was a list of instructions for
  somewhere you are not, at full weight, in the corner where the product and upgrade banners live.
  It becomes the counter and nothing else out there, at 35% more transparent, and a click opens it
  again. The click is forgotten on the way home. The rolled-up pill carries no dismiss cross,
  because at that size the cross and the counter are one target and only one of them cannot be
  undone.

- **A node blocked by another node says NEEDS FIRST, on a red band.** The other three states are a
  status and a colour is enough for those; this one is an instruction, and it is the same red the
  board paints the road in, so the banner on the card and the pips that just lit up behind it read
  as one thing.

- **Pressing NEW MODEL during the tutorial continues the tour.** The step describing the model hub
  rings that door and the step after it is the one waiting for it to be clicked, so a player who
  read the line and pressed the lit button was told to press NEXT and then told to click a door they
  were already through. One step of lookahead, deliberately not more: a screen change two steps
  ahead is somebody wandering off, not somebody keeping up.

- **A finished research node is blue.** Done and startable were two greens a hue apart, which across
  fifty nine nodes is not a distinction anybody makes at a glance, and the player scanning for "what
  can I start" kept stopping on nodes already finished. Blue also fills the disc rather than tinting
  its edge, so a finished node reads from across the board.

- **Clicking a node you cannot start shows what is blocking it.** The prerequisites you do not have
  light red on the board, immediately, instead of being named in one sentence inside the card. A red
  node is still a node, so clicking it shows its own, and the board walks you back one rung at a time.


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

- **Clicking the founder opens the founder.** It used to open the company page, which is a
  reasonable answer to a different question, and it had been written that way for months behind a
  branch that could never run. The card is theirs now: the seven skills with the levels the creation
  points bought, what each one moves, and the traits picked at the start with what those do. None of
  the employee figures appear on it, because a founder has no wage, no tenure and no loyalty band,
  and inventing five of those for the one person the card is about would be worse than not having it.

- **Clicking the founder did nothing, and clicking anybody else worked.** The character packs ship
  with no collider, so the ray from the office camera went straight through them on every frame
  since the room was built. Every hire has had a capsule since the day people became clickable, and
  the founder never got one, so this was not a near miss or a bad angle: there was nothing there to
  hit. A hire also has a fallback that projects their position when the ray fails, and the founder
  had none of that either, so nothing caught it.

- **The research board had never drawn a single one of its state colours.** Ready, running, done and
  picked all set a border and a fill, and the base rule for a node sits later in the stylesheet with
  equal specificity, so it won every one of them. What survived was what the base does not set: the
  dimming on a locked node and the hover scale, which is why the tree looked like it had some states.
  Found by changing the "done" colour, rendering the board and measuring the pixel: it came back at
  the same plate as before.

- **The cabinet shop clipped its own rows.** Each card protected its children from being squeezed and
  nothing protected the card, so a full rail compressed the cards themselves and the last line of
  every cabinet, the price, disappeared under the next one. Section headings did the same.

- **The free research node was free straight away, not the next morning.** Emil pays for the
  company's first node, and a player standing on the research screen when he says so watched every
  node keep its price until a day rolled over. Nothing was broken underneath: the favour was granted
  the moment he offered it, and the tree they were looking at had been drawn a second earlier. The
  page is now rebuilt when a gift lands on it, which happens once in a campaign.

- **The tutorial no longer dies at step 44.** Three of the fifty seven steps wait for the player to
  do something rather than for a button, and they were waiting on the event rather than on the
  company. Release your first model while he is still explaining how to release one and the only
  `model released` this campaign will ever raise has gone past the step that needed it; the tour then
  waits forever for a second, on a step that draws no NEXT. Every step that waits now asks whether
  the thing has already been done, so a player running ahead of the tour is caught up rather than
  stranded. It also explains the oddest half of the report: starting an upgrade raised that event
  again, which is why he suddenly continued.

- **The game refuses in Polish now.** When you try something the game will not allow, it tells you
  why, and seventy nine of those sentences were English in the Polish build: the company is
  insolvent, a run is already in flight, that needs a research node first. They survived eleven
  passes over the text because they are not written where screens are drawn. They are written beside
  the rules, in a folder nobody had thought to search for player-facing words.

- **Three pictures that were already in the game and were never drawn.** The model creator has eight
  stages and showed art on two of them, while the illustrations for the data and compute stages sat
  in the same folder as the two being used. The compute screen was the one screen about the fleet
  with a bare heading, and its banner had been on disk the whole time. Found by walking every
  resource folder in both directions: what the code asks for against what is there, and what is
  there against what anybody asks for.

- **The menu, the banner, the creator, the team page, the bank, the site, research, compute and
  marketing** all read their words from the phrase book. That is the last of the large screens. The
  book is at 2,722 phrases a language, up from 2,505, and every one of them exists in both.

- **A grant could be signed, ignored, and paid out.** "Safe first release" asks that nothing goes
  wrong for ninety days, and nothing goes wrong at a company with no model, no users and nothing on
  sale. Accepting it on the first morning and walking away collected $400,000 and sixty research
  points three months later. Every sustained programme had the same hole for the same reason: they
  describe how a company is run, and an empty office complies with all of them perfectly. A
  sustained term now starts on the day the company has something to sell, and once it starts it
  runs; taking the product back down does not stop the clock.

- **The map opened zoomed into America.** Your lab's page picked a region and a country before you
  had looked at it, and that was not only a view: the country decides the tax rate, what
  accelerators cost, how fast research runs and how hard the local competition is, so a player who
  never touched the map was quietly given four American numbers. Nothing is chosen now, the map
  opens on the world, and the company cannot be founded until you have said where it sits. Same
  reasoning as the founder's name, which stopped being pre-filled for the same reason.

- **The Polish stopped assuming you are a man.** The game has no gender field and never asks, so
  eight sentences were guessing. Most of that was fixed a day earlier; what survived are the forms a
  search for past-tense endings cannot see, including the tutorial's skip button, which every player
  reads in their first minute.

- **Five sentences promised a number the game does not use.** A reasoning model's serving bill is
  2.60 and the text said two and a half. Single precision is 1.30 and the text said a third. The
  tutorial promised a zloty in a game denominated in dollars, and apartments and cars that do not
  exist in it. Token prices fall to 44.9% a year and the README called that roughly half. No
  constant moved; the sentences did.

- **The research card said LOCKED in English.** Four of its five states were written into the code
  rather than the phrase book, so a Polish player read them in English next to a Polish title. It
  also said the same word for two different things: short of points is a matter of waiting, and
  short of a prerequisite is another node to go and start.


- **A founder nobody named was called "Anonymous" on their own product page.** Reported by a
  playtester in August and answered in two places out of five: the name field stopped being
  pre-filled and the interface resolves a missing name to "the founder" in whatever language is
  being read. The save default, the sanitiser and the migration for the oldest files each went on
  writing the literal word, and a non-empty string is a name, so the fallback could never run. Found
  by rendering the creator and reading the mock of the company's own page.

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

Save format **v52**. A campaign started on 0.1.0 or any version since opens here and keeps
everything in it.

**v52 is one field: whether a grant's term has actually started running.** A sustained programme now
waits for the company to be trading before its clock starts, so a file from v51 opens with every
award it holds already running, which is what those awards were doing. The days they have spent are
the days the file records, and starting them again would hand back time already used on terms signed
under the old rule.

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

- 1116 EditMode tests across 117 fixtures, and 31 PlayMode across 8. Measured on the day of the
  build, not carried forward: three numbers in this project's own documents were wrong for months
  because they were quoted from a file instead of counted.
- 2,722 phrases in the book, both languages complete, none written twice.
- 59 research nodes across five eras and four tracks, and **every one of them now has an icon**. The
  last nine arrived as files in `Art/` rather than `Resources/`, which is the only folder
  `Resources.Load` can see, so all nine would have drawn an empty disc with no error and nothing in
  the log. The same mistake, with six files, is recorded in a comment directly above the table they
  were added to.
- 184 source files changed since 0.1.0: 30,575 lines added, 2,604 removed, across 96 commits.
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
