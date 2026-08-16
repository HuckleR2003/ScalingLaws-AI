using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Somebody who might come and work here.
    ///
    /// **Generated once and then fixed.** A candidate whose asking price was recomputed each time
    /// the screen redrew would be a slot machine the player could reroll by changing tabs, and the
    /// negotiation below only means anything because the number on the other side of it does not
    /// move on its own.
    ///
    /// Pure: no UnityEngine, so a test can hire a hundred people without a scene.
    /// </summary>
    public sealed class Candidate
    {
        public Candidate(int id, string name, PlayerSkill position, int advertisedLevel,
            HireSource source, double askingHourlyUsd, double reservationHourlyUsd, int portraitSeed)
        {
            Id = id;
            Name = name ?? "Unnamed";
            Position = position;
            AdvertisedLevel = Math.Clamp(advertisedLevel, 1, PlayerSkillLimits.MaximumLevel);
            Source = source;
            AskingHourlyUsd = Math.Max(1.0, askingHourlyUsd);
            ReservationHourlyUsd = Math.Clamp(reservationHourlyUsd, 1.0, AskingHourlyUsd);
            PortraitSeed = portraitSeed;
        }

        public int Id { get; }
        public string Name { get; }
        public PlayerSkill Position { get; }

        /// <summary>What the advert says, before the channel's discount. One to a hundred.</summary>
        public int AdvertisedLevel { get; }

        public HireSource Source { get; }

        /// <summary>
        /// What they are asking now. Always above what they would actually take.
        ///
        /// Not readonly, because a candidate who has been haggled with and refused sometimes comes
        /// back wanting more. See <see cref="Negotiation.ImpatienceRaise"/>.
        /// </summary>
        public double AskingHourlyUsd { get; private set; }

        /// <summary>
        /// The lowest hourly they will sign for. Never shown.
        ///
        /// This is the whole reason haggling is a decision rather than a button: the player is
        /// guessing at a hidden number, and guessing too low costs them the candidate.
        /// </summary>
        public double ReservationHourlyUsd { get; private set; }

        /// <summary>Picks the face. Stable, so the same person looks the same in every screen.</summary>
        public int PortraitSeed { get; }

        /// <summary>
        /// They got another offer while the company was thinking about it.
        ///
        /// Raises what they ask and, with it, the floor they will take — a candidate who talks
        /// themselves up does not then sign for the old number. Returns what they now want, so the
        /// caller can tell the player rather than letting the figure change silently.
        /// </summary>
        public double RaiseTheAsk(double fraction)
        {
            var step = 1.0 + Math.Max(0.0, fraction);

            AskingHourlyUsd = Math.Round(AskingHourlyUsd * step, 2);
            ReservationHourlyUsd = Math.Round(ReservationHourlyUsd * step, 2);

            return AskingHourlyUsd;
        }

        public PositionDefinition Definition => PositionCatalog.Get(Position);

        /// <summary>
        /// What they are actually worth once the channel is applied.
        ///
        /// Remote loses sixty per cent of the advertised level, the agency thirty, and a specialist
        /// gains half again. The letter prints both numbers, so the player is never surprised.
        /// </summary>
        public int TrueLevel => Math.Clamp(
            (int)Math.Round(AdvertisedLevel * HiringChannels.Get(Source).QualityMultiplier),
            1, PlayerSkillLimits.MaximumLevel);

        /// <summary>The one-to-five band the roster's effects are written against.</summary>
        public int RoleSkill => PositionCatalog.RoleSkillFor(TrueLevel);

        public long AnnualSalaryUsd(double hourlyUsd) =>
            (long)Math.Round(hourlyUsd * PositionCatalog.PaidHoursPerYear);

        /// <summary>
        /// Makes one person.
        ///
        /// The spread around the advertised level is what stops a search returning seven identical
        /// people, and the spread on the wage is what stops the player learning one number and
        /// never reading a letter again.
        /// </summary>
        public static Candidate Roll(int id, PlayerSkill position, HireSource source,
            int centreLevel, DeterministicRandom random)
        {
            var definition = PositionCatalog.Get(position);
            var channel = HiringChannels.Get(source);

            var swing = random.NextDouble() * 2.0 - 1.0;
            var level = Math.Clamp((int)Math.Round(centreLevel + swing * 14.0), 4,
                PlayerSkillLimits.MaximumLevel);

            // The wage follows the advertised level, not the true one: nobody discounts themselves
            // for being worse than they claim, which is exactly why the cheap channels are a trap
            // as well as a bargain.
            var byLevel = definition.BaseHourlyWageUsd
                * (0.55 + 1.15 * (level / (double)PlayerSkillLimits.MaximumLevel));

            var asking = byLevel * channel.WageMultiplier * (0.92 + random.NextDouble() * 0.22);

            // They will come down between four and eighteen per cent, and how far is theirs to know.
            var give = 0.04 + random.NextDouble() * 0.14;

            return new Candidate(id, CandidateNames.Roll(random), position, level, source,
                Math.Round(asking, 2), Math.Round(asking * (1.0 - give), 2),
                (int)(random.NextDouble() * 100000));
        }
    }

    /// <summary>
    /// How an offer is landing, before it is made.
    ///
    /// **This is the one piece of the hidden number the player is allowed to see.** The candidate's
    /// floor stays secret — that is what makes haggling a decision — but negotiating blind against
    /// an invisible threshold is not a decision, it is a coin toss. The face tells the player warm
    /// or cold without telling them the number, which is exactly what a real negotiation gives you.
    /// </summary>
    public enum OfferMood
    {
        /// <summary>Comfortably above what they would take. They will sign.</summary>
        Delighted = 0,

        /// <summary>Above the floor, but not by much.</summary>
        Pleased = 1,

        /// <summary>Within a whisker either way. Could go either way.</summary>
        Neutral = 2,

        /// <summary>Under the floor. They will push back rather than walk.</summary>
        Cool = 3,

        /// <summary>Far enough under that they will leave.</summary>
        Insulted = 4
    }

    /// <summary>
    /// The result of putting an offer in front of somebody.
    ///
    /// Three outcomes rather than two, because "no" and "no, and I am leaving" are different
    /// answers and a player who cannot tell them apart cannot learn to haggle.
    /// </summary>
    public enum OfferVerdict
    {
        /// <summary>Signed.</summary>
        Accepted = 0,

        /// <summary>Not enough, but they are still at the table.</summary>
        HeldFirm = 1,

        /// <summary>Insulted, or out of patience. The candidate is gone.</summary>
        WalkedAway = 2
    }

    /// <summary>
    /// What a candidate does when an offer lands.
    ///
    /// Kept out of <see cref="Candidate"/> so the rule is one readable block rather than a method
    /// on a data object, and so a test can state the whole negotiation model in one place.
    /// </summary>
    public static class Negotiation
    {
        /// <summary>Offers a candidate will sit through before walking. The fourth is the last.</summary>
        public const int Patience = 3;

        /// <summary>
        /// A signing bonus is worth this fraction of itself as hourly, spread over the first year.
        ///
        /// One over the paid hours in a year, which is simply what a lump sum is per hour. It is
        /// stated as a constant because it is the reason a bonus is a real lever rather than
        /// decoration: twenty thousand dollars is about ten dollars an hour, and on a four hundred
        /// dollar rate that closes a two and a half per cent gap.
        /// </summary>
        public static double HourlyValueOfBonus(long signingBonusUsd) =>
            signingBonusUsd / PositionCatalog.PaidHoursPerYear;

        /// <summary>
        /// How far below the reservation an offer can be before it is an insult rather than a bid.
        ///
        /// Below this the candidate leaves rather than counters, which is what makes lowballing
        /// cost something. Above it they hold firm and the player gets another go.
        /// </summary>
        public const double InsultFraction = 0.72;

        /// <summary>
        /// What their face would say about this offer.
        ///
        /// Reads the same numbers <see cref="Judge"/> does, so the face can never disagree with the
        /// answer: anything Delighted or Pleased is accepted, anything Insulted walks. The bands
        /// between are the honest ambiguity.
        /// </summary>
        public static OfferMood MoodFor(Candidate candidate, double offeredHourlyUsd,
            long signingBonusUsd)
        {
            if (candidate == null)
            {
                return OfferMood.Neutral;
            }

            var effective = offeredHourlyUsd + HourlyValueOfBonus(signingBonusUsd);
            var ratio = effective / Math.Max(0.01, candidate.ReservationHourlyUsd);

            return ratio switch
            {
                >= 1.12 => OfferMood.Delighted,
                >= 1.0 => OfferMood.Pleased,
                >= 0.94 => OfferMood.Neutral,
                >= InsultFraction => OfferMood.Cool,
                _ => OfferMood.Insulted
            };
        }

        /// <summary>The face, and what it is thinking. Both come from here so they cannot drift.</summary>
        public static (string Face, string Says) Portrait(OfferMood mood) => mood switch
        {
            OfferMood.Delighted => (":D", "They would take this happily."),
            OfferMood.Pleased => (":)", "This is enough. They would sign."),
            OfferMood.Neutral => (":|", "Borderline. It could go either way."),
            OfferMood.Cool => (":/", "Short. They will push back rather than leave."),
            _ => (">:(", "Insulting. Send this and they are gone.")
        };

        /// <summary>
        /// How much a candidate who has been made to wait raises their price.
        ///
        /// **Somebody who is worth hiring is being talked to by somebody else.** A negotiation the
        /// player could drag out for free is one where the optimal play is always another round;
        /// this is what makes holding firm cost the company something rather than only costing the
        /// candidate patience.
        /// </summary>
        public const double ImpatienceRaise = 0.06;

        /// <summary>How often a refused round produces one. Not every time, or it is just a tax.</summary>
        public const double ImpatienceChance = 0.45;

        public static OfferVerdict Judge(Candidate candidate, double offeredHourlyUsd,
            long signingBonusUsd, int roundsAlreadyUsed)
        {
            if (candidate == null)
            {
                return OfferVerdict.WalkedAway;
            }

            var effective = offeredHourlyUsd + HourlyValueOfBonus(signingBonusUsd);

            if (effective >= candidate.ReservationHourlyUsd)
            {
                return OfferVerdict.Accepted;
            }

            if (effective < candidate.ReservationHourlyUsd * InsultFraction)
            {
                return OfferVerdict.WalkedAway;
            }

            return roundsAlreadyUsed + 1 >= Patience
                ? OfferVerdict.WalkedAway
                : OfferVerdict.HeldFirm;
        }
    }
}
