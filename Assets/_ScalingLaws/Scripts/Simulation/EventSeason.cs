using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    // **Lifted out of `UI/` on the way in.** This is a snapshot handed to a screen, and
    // `Simulation/` may not depend on `UI/`: that single constraint is why the whole game
    // can be tested in milliseconds without opening a scene. The first version of
    // `CompanySimulation.UpcomingEvents` returned a UI type and broke it.
    /// <summary>
    /// One line of the upcoming list: which event, how long is left, and what is still free.
    ///
    /// **Handed in, not worked out.** How many stands are gone is a question the rules answer,
    /// and a list that answered it a second way would be a second truth on a screen whose whole
    /// job is to be worth paying for.
    /// </summary>
    public readonly struct UpcomingEventRow
    {
        public UpcomingEventRow(CityEventId id, int year, int daysLeft, int freeStands,
            int sellableStands)
        {
            Id = id;
            Year = year;
            DaysLeft = Math.Max(0, daysLeft);
            SellableStands = Math.Max(1, sellableStands);
            FreeStands = Math.Clamp(freeStands, 0, SellableStands);
        }

        public CityEventId Id { get; }
        public int Year { get; }

        /// <summary>Days until registration closes, which is the day the doors open.</summary>
        public int DaysLeft { get; }

        public int FreeStands { get; }
        public int SellableStands { get; }

        /// <summary>Nothing left but the one stand that is held back, so it goes to the room.</summary>
        public bool GoesToAuction => FreeStands <= 0;
    }

    /// <summary>
    /// How badly one lab wants a stand at one event, on a scale of 0 to 1.
    ///
    /// **This is a willingness, not a budget, and the difference is the whole of why this feature
    /// is allowed to exist.** `CLAUDE.md` says rivals carry no economy and gives the reason: a fleet
    /// and a payroll per company would mean inventing two dozen uncheckable numbers for a
    /// simulation nobody sees inside of. An auction does not need a bank balance. It needs to know
    /// who wants the thing and for how long, and both of those are read from what the market
    /// already says about that lab.
    /// </summary>
    public readonly struct LabAppetite
    {
        public LabAppetite(CompetitorId lab, double strength)
        {
            Lab = lab;
            Strength = Math.Clamp(SimUnits.Finite(strength), 0.0, 1.0);
        }

        public CompetitorId Lab { get; }

        /// <summary>
        /// How well they are doing, which is what makes them want to be seen.
        ///
        /// Built by the caller out of the users the market says they hold and how recently they
        /// shipped. It lives outside this class so the arithmetic that reads the market stays in
        /// one place and this one stays testable with a number typed into a fixture.
        /// </summary>
        public double Strength { get; }
    }

    /// <summary>
    /// The city's event calendar: when each one runs, when it is announced, and how full it is.
    ///
    /// **Derived from end to end, like `RivalExpansion`, `LabTraits`, `WorldEventCatalog`,
    /// `SafetyRecord`, `RoomUpgrades` and `FastFollower` before it.** Nothing here is stored, so it
    /// replays identically from the seed, needs no migration, and cannot drift from the news item
    /// that announced it. The one thing that is genuinely causal, which stand the **player** has
    /// bought, is not here: that is money leaving the account and it belongs in `CompanyState`.
    ///
    /// `Docs/EVENTS_PLAN.md` is the design.
    /// </summary>
    public static class EventSeason
    {
        /// <summary>Its own stream, so adding this does not shift every other draw in the game.</summary>
        private const uint SeedSalt = 0x45565453;

        /// <summary>
        /// Which day of its month an event runs on. The same day every year, so a player learns
        /// the shape of the year rather than re-reading a calendar.
        /// </summary>
        public const int DayOfMonth = 14;

        /// <summary>
        /// How far ahead the cheapest membership shows an event, when the player has asked for it.
        ///
        /// **The author's idea and it is the best thing about this feature.** Events are free to
        /// read about: nobody is cut off from the content. What twenty thousand a month buys is a
        /// month of head start, which in a game whose spine is "upgrades are timed, not purchased"
        /// is the same mechanic wearing a new subject. It also finally makes EVENT HUNTER carry an
        /// event, which it never has.
        /// </summary>
        public const int EarlyAccessDays = 30;

        /// <summary>
        /// The share of rivals who also get the early look, so being early is an edge rather than
        /// a certainty. The author's figure.
        /// </summary>
        public const double RivalsWithEarlyAccess = 0.5;

        /// <summary>
        /// How far the participation bar falls as the industry grows up.
        ///
        /// **Measured, not chosen.** With a flat bar at 0.55 of the venue prestige,
        /// `EventPressureTests` found Bayview Park coming back with all seven stands free on
        /// the public announcement day in nine years out of ten, whatever the field was doing.
        /// Brand strength across the roster sits mostly under 0.4, so that bar filtered out
        /// almost everybody for ever and the city never got harder to get into.
        ///
        /// At an empty field the bar is 0.55 of prestige and at a full one 0.20, which is the
        /// difference between "only the confident bother" and "everybody goes".
        /// </summary>
        public const double BarAtEmptyField = 0.55;

        /// <summary>
        /// The bar once every lab has something on sale.
        ///
        /// **0.08 rather than 0.20, and the number was measured rather than chosen.** At 0.20
        /// the late years swung between nought and seven stands free on the public day, because
        /// how many labs clear the bar, hold the early look and draw an early decision is a
        /// small sample and a small sample is noisy. The brief was that a grown-up industry
        /// should reliably push a late player into the auction, so the bar drops to where a lab
        /// with anything at all on sale turns up. That is also the honest reading of a mature
        /// market: everybody goes, and not going is the decision that needs a reason.
        /// </summary>
        public const double BarAtFullField = 0.08;

        /// <summary>
        /// How much sooner a crowded field decides.
        ///
        /// Being keen moves one lab earlier in its own window; a crowded industry moves
        /// everybody earlier again, because the stands are known to go. Without this the
        /// decision days stayed spread across the whole window however many labs wanted in,
        /// and the public announcement day always found an empty floor.
        /// </summary>
        public const double HasteAtFullField = 0.45;

        /// <summary>The day the event itself runs.</summary>
        public static GameDate RunsOn(CityEventId id, int year)
        {
            var definition = CityEventCatalog.Get(id);

            return GameDate.FromCalendar(year, definition.Month, DayOfMonth);
        }

        /// <summary>
        /// The day everybody is told, which is also the day registration opens.
        ///
        /// Each event carries its own window, so the same lateness means different things at
        /// different events and a player cannot learn one number and stop reading.
        /// </summary>
        public static GameDate AnnouncedOn(CityEventId id, int year)
            => RunsOn(id, year).AddDays(-CityEventCatalog.Get(id).RegistrationOpensDaysBefore);

        /// <summary>The day a member with the early look is told instead.</summary>
        public static GameDate EarlyAnnouncedOn(CityEventId id, int year)
            => AnnouncedOn(id, year).AddDays(-EarlyAccessDays);

        /// <summary>Registration closes when the doors open.</summary>
        public static GameDate RegistrationClosesOn(CityEventId id, int year) => RunsOn(id, year);

        /// <summary>
        /// Everything a company can see on a given day, soonest first.
        ///
        /// <paramref name="early"/> is the membership's early look. It widens the window rather
        /// than changing anything about the event, which is what keeps the two views of one
        /// calendar from being two calendars.
        /// </summary>
        public static List<(CityEventId Id, int Year)> VisibleOn(GameDate today, bool early)
        {
            var found = new List<(CityEventId, int)>();

            // This year and next, because an event announced four months out crosses the new year.
            for (var year = today.Year; year <= today.Year + 1; year++)
            {
                foreach (var definition in CityEventCatalog.All)
                {
                    var opens = early
                        ? EarlyAnnouncedOn(definition.Id, year)
                        : AnnouncedOn(definition.Id, year);

                    if (today.IsOnOrAfter(opens)
                        && !today.IsOnOrAfter(RegistrationClosesOn(definition.Id, year).AddDays(1)))
                    {
                        found.Add((definition.Id, year));
                    }
                }
            }

            found.Sort((left, right) =>
                RunsOn(left.Item1, left.Item2).DayIndex
                    .CompareTo(RunsOn(right.Item1, right.Item2).DayIndex));

            return found;
        }

        /// <summary>
        /// Whether one lab gets the early look this year, for this event.
        ///
        /// Derived rather than stored, and **per event rather than per lab for the campaign**: a
        /// lab that always saw everything first would be an opponent the player could never beat
        /// to a stand, and one that never did would be no opponent at all.
        /// </summary>
        public static bool LabSeesEarly(CompetitorId lab, CityEventId id, int year, uint seed)
        {
            var random = StreamFor(seed, id, year, (int)lab + 7919);

            return random.NextChance(RivalsWithEarlyAccess);
        }

        /// <summary>
        /// The day a lab decides, or null when it never wants this one.
        ///
        /// **A lab that is doing badly does not take a stand**, which is the half that stops the
        /// venue filling up the same way every year regardless of what is happening in the market.
        /// Appetite is tested against the venue: a prestigious address asks more of whoever stands
        /// at it, so a weak lab turns up at the festival ground and not at Valley Plaza.
        /// </summary>
        public static GameDate? DecidesOn(LabAppetite lab, CityEventId id, int year, uint seed,
            double fieldMaturity)
        {
            var definition = CityEventCatalog.Get(id);
            var venue = EventVenueCatalog.Get(definition.Venue);
            var random = StreamFor(seed, id, year, (int)lab.Lab);

            // **The bar falls as the industry fills up.** Early on only a confident lab thinks a
            // stand is worth it; once everybody has something live, turning up is simply what
            // companies do and a weak year stops being a reason to stay home.
            var maturity = Math.Clamp(SimUnits.Finite(fieldMaturity), 0.0, 1.0);
            var bar = venue.Prestige
                * (BarAtEmptyField - (BarAtEmptyField - BarAtFullField) * maturity);

            if (lab.Strength < bar)
            {
                return null;
            }

            var opensAt = LabSeesEarly(lab.Lab, id, year, seed)
                ? EarlyAnnouncedOn(id, year)
                : AnnouncedOn(id, year);

            var closesAt = RegistrationClosesOn(id, year);
            var window = Math.Max(1, closesAt.DayIndex - opensAt.DayIndex);

            // **Keenness moves the decision earlier, it does not make it certain.** A lab at full
            // appetite still lands somewhere in the first third rather than always on day one,
            // which is what leaves a fast player room to get in ahead of them.
            // A crowded field moves everybody earlier again, which is what turns registering
            // late from a choice into an auction once the industry has grown up.
            var share = random.NextRange(0.0,
                (1.0 - 0.6 * lab.Strength) * (1.0 - HasteAtFullField * maturity));

            return opensAt.AddDays((int)Math.Round(window * Math.Clamp(share, 0.0, 1.0)));
        }

        /// <summary>
        /// How many stands are gone by a given day, and who is still hoping for one.
        ///
        /// The count is what the venue window reveals after the player has picked a size and
        /// asked. Registering early is simply cheaper, because what is left late is one contested
        /// stand and an auction.
        /// </summary>
        public static (int Taken, List<LabAppetite> Unseated) StandsTakenBy(CityEventId id,
            int year, GameDate on, uint seed, IReadOnlyList<LabAppetite> labs,
            double fieldMaturity = 1.0)
        {
            var definition = CityEventCatalog.Get(id);
            var venue = EventVenueCatalog.Get(definition.Venue);

            var decided = new List<(GameDate When, LabAppetite Lab)>();

            foreach (var lab in labs ?? Array.Empty<LabAppetite>())
            {
                var when = DecidesOn(lab, id, year, seed, fieldMaturity);

                if (when != null && on.IsOnOrAfter(when.Value))
                {
                    decided.Add((when.Value, lab));
                }
            }

            // First come, first served, and a tie goes to the keener lab.
            decided.Sort((left, right) =>
            {
                var byDay = left.When.DayIndex.CompareTo(right.When.DayIndex);

                return byDay != 0
                    ? byDay
                    : right.Lab.Strength.CompareTo(left.Lab.Strength);
            });

            // One stand is always held back for the player to buy or bid for, or an event that
            // filled up before they heard of it would be a screen that can only ever say no.
            var sellable = Math.Max(1, venue.Stands - 1);

            // **At most half the floor can go before everybody is told, and this rule exists
            // because the measurement demanded it.** Without it, five of the seven events in
            // the year were sold out on the day of the public announcement once the field had
            // grown up: at a thirty day window the member's window is sixty, so the public day
            // falls halfway through it and the early holders had already taken everything.
            //
            // That turned the cheapest membership from an edge into a requirement, which is a
            // different product from the one that was designed. Half is the whole of the rule:
            // being early is worth a real head start and is never worth the entire floor.
            var publicDay = AnnouncedOn(id, year);
            var beforeTheNews = sellable / 2;

            var taken = 0;
            var early = 0;

            foreach (var (when, _) in decided)
            {
                if (taken >= sellable)
                {
                    break;
                }

                if (when.IsBefore(publicDay))
                {
                    if (early >= beforeTheNews)
                    {
                        continue;
                    }

                    early++;
                }

                taken++;
            }

            var unseated = new List<LabAppetite>();

            for (var index = taken; index < decided.Count; index++)
            {
                unseated.Add(decided[index].Lab);
            }

            return (taken, unseated);
        }

        /// <summary>
        /// One stream per event, per year, per lab, all of them off this system's own salt.
        ///
        /// A new mechanism drawing from a shared stream shifts every later draw and takes the
        /// distant-future tests apart. This project has that written down and has been caught by
        /// it.
        /// </summary>
        private static DeterministicRandom StreamFor(uint seed, CityEventId id, int year, int lab)
        {
            unchecked
            {
                var mixed = seed ^ SeedSalt;
                mixed = mixed * 16777619u + (uint)((int)id * 31 + 17);
                mixed = mixed * 16777619u + (uint)year;
                mixed = mixed * 16777619u + (uint)lab;

                // **Avalanche before use, and this was a real bug.** Mixing the lab index in
                // by multiply-and-add leaves fourteen labs holding seeds a few units apart,
                // and a generator seeded with near neighbours gives correlated *first*
                // draws. `EventPressureTests` found the shape of it: the floor came back
                // either untouched or completely full, year after year, on identical
                // inputs, because every lab drew nearly the same number. This is murmur3's
                // finalizer, which is the cheap standard answer to exactly that.
                mixed ^= mixed >> 16;
                mixed *= 0x85EBCA6Bu;
                mixed ^= mixed >> 13;
                mixed *= 0xC2B2AE35u;
                mixed ^= mixed >> 16;

                return new DeterministicRandom(mixed == 0u ? SeedSalt : mixed);
            }
        }
    }
}
