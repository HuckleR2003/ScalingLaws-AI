using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Which of the world's numbers an event moves.
    ///
    /// **Only the four the market already computes.** Every one of these is a curve in
    /// `MarketModel` that the simulation reads every day, so an event is a bump on a line that
    /// already exists rather than a new system beside it. Adding a fifth lever means adding a fifth
    /// curve first, and the moment there are two ways to make hardware expensive this file stops
    /// being trustworthy.
    /// </summary>
    public enum WorldLever
    {
        None = 0,

        /// <summary>
        /// Accelerator supply pressure.
        ///
        /// **This one pays for itself twice**, which is why a hardware shock needs no second entry.
        /// `ScarcityIndex` drives the purchase price at a 0.35 markup and the rental price at 1.4,
        /// so renting reacts four times harder than buying. That is the right shape: a cloud passes
        /// a shortage to its tenants the same week, and somebody who already bought paid last year's
        /// price.
        /// </summary>
        Scarcity = 1,

        /// <summary>Tokens the whole world wants each day. The size of the pie, not your slice.</summary>
        Demand = 2,

        /// <summary>The going rate per million tokens, which everybody is measured against.</summary>
        TokenPrice = 3,

        /// <summary>How much capability the same FLOPs buy. A tailwind, or the loss of one.</summary>
        Efficiency = 4
    }

    /// <summary>
    /// Something that happened to everybody.
    ///
    /// **Shocks decay, they do not switch off.** A supply crunch that ends on a Tuesday at full
    /// strength is a cliff no real market has had, and the player would learn the exact day rather
    /// than the shape. Strength is 1.0 on the day and falls linearly to nothing at the end, so the
    /// interesting part is the middle and the tail is a memory.
    ///
    /// A permanent change is written as a very long window, not as a special case. Export controls
    /// did not wear off; they became the new floor, and eight years of slow decay is what that looks
    /// like from inside a company.
    /// </summary>
    public readonly struct WorldEvent
    {
        public WorldEvent(GameDate on, int days, WorldLever lever, double magnitude, string key,
            bool isProjection = false)
        {
            On = on;
            Days = Math.Clamp(days, 1, 6000);
            Lever = lever;
            Magnitude = Math.Clamp(magnitude, -0.95, 4.0);
            Key = key ?? string.Empty;
            IsProjection = isProjection;
        }

        public GameDate On { get; }
        public int Days { get; }
        public WorldLever Lever { get; }

        /// <summary>Signed, as a share of the curve. +0.30 is thirty per cent dearer at the peak.</summary>
        public double Magnitude { get; }

        /// <summary>Phrase-book stem. `.head` is the headline, `.body` is the paragraph.</summary>
        public string Key { get; }

        /// <summary>
        /// True when this is the game's guess rather than something that happened.
        ///
        /// The same honesty flag `HardwareGeneration` and `CompetitorRelease` carry, and it is not
        /// decoration: the roster here is real history, so the line between "this is on the record"
        /// and "this is where we think it goes" has to survive a player who was there for it. A
        /// projected event says so in its own news item.
        /// </summary>
        public bool IsProjection { get; }

        public bool IsLiveOn(GameDate date) =>
            date.IsOnOrAfter(On) && date.DayIndex < On.DayIndex + Days;

        /// <summary>How much of the shock is still working, 1.0 on the day and 0.0 at the end.</summary>
        public double StrengthOn(GameDate date)
        {
            if (!IsLiveOn(date))
            {
                return 0.0;
            }

            var gone = date.DayIndex - On.DayIndex;
            return Math.Clamp(1.0 - gone / (double)Days, 0.0, 1.0);
        }

        public override string ToString() =>
            $"{On} {Key} {Lever} {Magnitude:+0.0%;-0.0%} for {Days}d";
    }

    /// <summary>
    /// The world happening around the company, on the dates it actually happened.
    ///
    /// **Everything here is a function of the date and nothing else.** That is the same promise
    /// `MarketConditions` already makes: *nothing the player does moves these numbers, which is what
    /// makes timing a skill rather than a stat*. An event nobody can prevent, arriving on a date
    /// nobody can move, is the purest form of that. Nothing is stored, nothing is rolled, no save
    /// version is needed, and a campaign replays identically. Same design as `RivalExpansion`.
    ///
    /// **Every date below is real unless the entry says otherwise**, and the ones that are not are
    /// marked. That rule is why this file is a catalog rather than a generator: the honesty flag
    /// forbids inventing a specification, and a dated world event is a specification about history.
    ///
    /// Deliberately sparse. Twenty three entries across fourteen years is roughly one every seven
    /// months, and it thins out after 2025 because that is where the record thins out. A world where
    /// something happens every three weeks is a world where nothing happens.
    /// </summary>
    public static class WorldEventCatalog
    {
        /// <summary>Bump when the table changes enough that a saved campaign would read differently.</summary>
        public const string CatalogVersion = "2026.09.03";

        /// <summary>
        /// The most any single lever may be moved, in either direction, once everything live is
        /// multiplied together.
        ///
        /// A guard rather than a taste: two shocks landing on the same curve compound, and demand at
        /// four times or a quarter would put the market somewhere none of the balance was measured.
        /// `WorldEventTests` checks the whole calendar day by day against this.
        /// </summary>
        public const double MostAnyLeverMoves = 0.60;

        private static readonly WorldEvent[] Entries =
        {
            // ---- 2022: the supply chain, then the earthquake ---------------------------------

            // Neon is a lithography input and a large share of it came out of Ukraine. The invasion
            // is the first thing in this game's window that made silicon harder to get.
            new(GameDate.FromCalendar(2022, 2, 24), 420, WorldLever.Scarcity, 0.16, "world.ukraine"),

            // Two months of lockdown across the city that assembles a great deal of the world's
            // electronics. The lead times in this game are the thing it lengthens.
            new(GameDate.FromCalendar(2022, 3, 28), 240, WorldLever.Scarcity, 0.22, "world.shanghai"),

            // Open weights arrive in public for the first time. The going rate for a token has a
            // free alternative underneath it from this day on.
            new(GameDate.FromCalendar(2022, 8, 22), 700, WorldLever.TokenPrice, -0.10, "world.openweights"),

            // Export controls on advanced accelerators. This one never really ends, which is what
            // the long window is for.
            new(GameDate.FromCalendar(2022, 10, 7), 1500, WorldLever.Scarcity, 0.30, "world.embargo"),

            // **The day the industry changed.** Everything after this is a different game, and the
            // demand curve says so.
            new(GameDate.FromCalendar(2022, 11, 30), 900, WorldLever.Demand, 0.55, "world.chatlaunch"),

            // ---- 2023: money in, prices down ------------------------------------------------

            // The two largest software companies in the world put assistants in their search boxes
            // within a day of each other. Every lab's compute bill went up that quarter.
            new(GameDate.FromCalendar(2023, 2, 7), 300, WorldLever.Scarcity, 0.18, "world.searchrace"),

            // An order of magnitude off the API rate, in one announcement.
            new(GameDate.FromCalendar(2023, 3, 1), 540, WorldLever.TokenPrice, -0.22, "world.apicut"),

            // A frontier model's weights escape onto the open internet. The floor under the price
            // stops being theoretical.
            new(GameDate.FromCalendar(2023, 3, 3), 620, WorldLever.TokenPrice, -0.14, "world.weightsleak"),

            // Open weights with a commercial licence. The same again, on purpose this time.
            new(GameDate.FromCalendar(2023, 7, 18), 700, WorldLever.TokenPrice, -0.12, "world.openlicence"),

            // Safety becomes a procurement requirement rather than a research interest.
            new(GameDate.FromCalendar(2023, 11, 1), 500, WorldLever.Efficiency, -0.06, "world.summit"),

            // A newspaper sues over training data, and every corpus in the industry becomes a
            // question somebody might have to answer in court.
            new(GameDate.FromCalendar(2023, 12, 27), 640, WorldLever.Demand, -0.10, "world.copyright"),

            // ---- 2024: regulation, and who is actually getting paid --------------------------

            // The first comprehensive regime. Compliance is time before a release, and time is the
            // one thing this game charges for.
            new(GameDate.FromCalendar(2024, 3, 13), 520, WorldLever.Efficiency, -0.08, "world.aiact"),

            // The company selling the shovels becomes the most valuable in the world. If the player
            // has not worked out who is winning this gold rush, this is the day it is spelt out.
            new(GameDate.FromCalendar(2024, 6, 18), 460, WorldLever.Scarcity, 0.24, "world.picksandshovels"),

            // Reasoning at inference time. The same silicon suddenly buys more.
            new(GameDate.FromCalendar(2024, 9, 12), 900, WorldLever.Efficiency, 0.30, "world.reasoning"),

            // A hyperscaler restarts a nuclear plant to run a datacenter. Small consumers are not
            // who the grid is being rebuilt for.
            new(GameDate.FromCalendar(2024, 9, 20), 800, WorldLever.Scarcity, 0.14, "world.nuclear"),

            // ---- 2025: the cheap model, and the half-trillion answer to it -------------------

            // **The stolen quarter.** A model trained for a fraction of the going rate matches the
            // expensive ones, and the price of everything follows it down.
            new(GameDate.FromCalendar(2025, 1, 20), 720, WorldLever.TokenPrice, -0.30, "world.cheapmodel"),

            new(GameDate.FromCalendar(2025, 1, 21), 1100, WorldLever.Scarcity, 0.26, "world.stargate"),

            // The market works out that efficiency is not the same as demand, violently, in a day.
            new(GameDate.FromCalendar(2025, 1, 27), 260, WorldLever.Demand, -0.12, "world.selloff"),

            // General-purpose model obligations start to bite.
            new(GameDate.FromCalendar(2025, 8, 2), 620, WorldLever.Efficiency, -0.07, "world.gpai"),

            // ---- 2026 and beyond: the game's guesses, and they say so -------------------------

            // High quality public text runs out and corpora have to be made rather than found.
            new(GameDate.FromCalendar(2026, 6, 1), 900, WorldLever.Efficiency, -0.12, "world.datawall",
                isProjection: true),

            // Liquid cooling stops being exotic. Density goes up and so does what a floor can hold.
            new(GameDate.FromCalendar(2026, 11, 2), 1000, WorldLever.Scarcity, -0.16, "world.liquidcooling",
                isProjection: true),

            // Researchers leave the large labs for small ones. Wages are not a lever this game has
            // yet, so this lands where it can be felt: the frontier slows for a year.
            new(GameDate.FromCalendar(2027, 4, 5), 520, WorldLever.Efficiency, -0.10, "world.braindrain",
                isProjection: true),

            // Agents start doing work rather than answering about it. The largest demand event in
            // the calendar, and the one most likely to be wrong.
            new(GameDate.FromCalendar(2028, 1, 17), 1400, WorldLever.Demand, 0.45, "world.agents",
                isProjection: true)
        };

        public static IReadOnlyList<WorldEvent> All => Entries;

        /// <summary>Everything running on a given day, in the order it started.</summary>
        public static List<WorldEvent> LiveOn(GameDate date)
        {
            var live = new List<WorldEvent>();

            foreach (var entry in Entries)
            {
                if (entry.IsLiveOn(date))
                {
                    live.Add(entry);
                }
            }

            return live;
        }

        /// <summary>Everything that starts on a given day. What the wire reports.</summary>
        public static List<WorldEvent> StartingOn(GameDate date)
        {
            var today = new List<WorldEvent>();

            foreach (var entry in Entries)
            {
                if (entry.On == date)
                {
                    today.Add(entry);
                }
            }

            return today;
        }

        /// <summary>
        /// What to multiply one of the world's curves by today.
        ///
        /// Multiplied rather than added, so two shocks on the same lever compound the way two
        /// shortages actually do, and clamped so they cannot compound past the band the balance was
        /// measured in.
        /// </summary>
        public static double MultiplierOn(WorldLever lever, GameDate date)
        {
            var total = 1.0;

            foreach (var entry in Entries)
            {
                if (entry.Lever != lever)
                {
                    continue;
                }

                var strength = entry.StrengthOn(date);

                if (strength > 0.0)
                {
                    total *= 1.0 + entry.Magnitude * strength;
                }
            }

            return Math.Clamp(total, 1.0 - MostAnyLeverMoves, 1.0 + MostAnyLeverMoves);
        }
    }
}
