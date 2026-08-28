using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One person on somebody else's payroll.</summary>
    public readonly struct RivalStaffMember
    {
        public RivalStaffMember(int id, CompetitorId employer, string name, PlayerSkill position,
            int rating, GameDate joinedOn)
        {
            Id = id;
            Employer = employer;
            Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            Position = position;
            Rating = Math.Clamp(rating, 1, 100);
            JoinedOn = joinedOn;
        }

        /// <summary>Stable for the life of a campaign, so a poached person can be recorded.</summary>
        public int Id { get; }

        public CompetitorId Employer { get; }
        public string Name { get; }

        /// <summary>The discipline they lead in. What the list sorts and filters on.</summary>
        public PlayerSkill Position { get; }

        /// <summary>Nought to a hundred. The visible list is capped well below the top.</summary>
        public int Rating { get; }

        public GameDate JoinedOn { get; }

        public int YearsAt(GameDate today) =>
            Math.Max(0, (today.DayIndex - JoinedOn.DayIndex) / 365);

        /// <summary>
        /// How attached they are, on the same curve the player's own staff sit on.
        ///
        /// Tenure only, because nobody outside a company knows what it pays or what it offers. That
        /// is the honest limit of what the player can see, and it is also what makes the mechanic
        /// work: the people who have been somewhere two months are the ones who answer the phone.
        /// </summary>
        public double Loyalty(GameDate today)
        {
            var years = Math.Max(0, today.DayIndex - JoinedOn.DayIndex) / 365.0;

            var tenure = Simulation.Loyalty.TenureCeiling
                * (1.0 - Math.Exp(-years / (Simulation.Loyalty.TenureYearsToFull / 3.0)));

            // A rival is assumed to pay and treat people about averagely, so the terms the player
            // cannot see sit at the middle of their range rather than at zero. Assuming the worst
            // would make everybody poachable and the mechanic free.
            return Math.Clamp(Simulation.Loyalty.Base + tenure + 9.0, 0.0, 100.0);
        }

        /// <summary>
        /// What this person becomes if they take the offer.
        ///
        /// The hundred-point rating collapses to the five-band skill the payroll actually uses,
        /// because the company has one scale for people and adding a second would mean two numbers
        /// describing the same person.
        /// </summary>
        public int SkillBand => Rating >= 88 ? 5
            : Rating >= 74 ? 4
            : Rating >= 58 ? 3
            : Rating >= 40 ? 2
            : 1;
    }

    /// <summary>
    /// Who works for the other labs.
    ///
    /// **Generated from the lab and a seed, never stored.** A roster of fourteen companies with a
    /// dozen people each is a hundred and seventy people the save would have to carry, and none of
    /// it is a decision anybody made. What the save does record is the short list of people who
    /// left, because that is the only part the player changed.
    ///
    /// The list a player can see stops at <see cref="VisibleCeiling"/>. The people above it exist
    /// and are hidden, which is what the top intelligence membership is selling: not better numbers
    /// on the same people, but the ones who were never on the list.
    /// </summary>
    public static class RivalStaff
    {
        /// <summary>How many people each lab has on the list the player can reach.</summary>
        public const int RosterSize = 12;

        /// <summary>The best rating an ordinary listing will ever show.</summary>
        public const int VisibleCeiling = 80;

        /// <summary>And the best that exists, behind the top membership.</summary>
        public const int HiddenCeiling = 97;

        /// <summary>
        /// The roster for one lab, in the order a player would read it: best first.
        ///
        /// Deterministic in the lab and the campaign seed, so the same company has the same people
        /// every time it is opened, and two campaigns on the same seed agree.
        /// </summary>
        public static List<RivalStaffMember> RosterFor(CompetitorId lab, GameDate today,
            uint campaignSeed, IReadOnlyCollection<int> gone = null)
        {
            var random = new DeterministicRandom(Mix(campaignSeed, (uint)lab));
            var roster = new List<RivalStaffMember>(RosterSize);

            for (var index = 0; index < RosterSize; index++)
            {
                var id = ((int)lab * 1000) + index;

                if (gone != null && Holds(gone, id))
                {
                    continue;
                }

                // A few strong people and a long tail, which is the shape of every real team. A
                // flat distribution would make every lab interchangeable and every list boring.
                var roll = random.NextDouble();
                var rating = (int)Math.Round(28 + Math.Pow(roll, 0.55) * (HiddenCeiling - 28));

                // Tenure is drawn against the lab's own age rather than the campaign's, so a lab
                // founded in 2019 has people with six years behind them and a 2022 startup does not.
                var maximumDays = Math.Max(120, today.DayIndex - FoundedDayIndex(lab));
                var joined = new GameDate(
                    Math.Max(0, today.DayIndex - (int)(random.NextDouble() * maximumDays)));

                roster.Add(new RivalStaffMember(
                    id, lab, CandidateNames.Roll(random), PositionFor(random), rating, joined));
            }

            roster.Sort((left, right) => right.Rating.CompareTo(left.Rating));
            return roster;
        }

        /// <summary>What the player is allowed to see, given what they are paying for.</summary>
        public static List<RivalStaffMember> Visible(IReadOnlyList<RivalStaffMember> roster,
            bool hasTopMembership)
        {
            var visible = new List<RivalStaffMember>(roster.Count);

            foreach (var member in roster)
            {
                if (hasTopMembership || member.Rating <= VisibleCeiling)
                {
                    visible.Add(member);
                }
            }

            return visible;
        }

        /// <summary>How many people are being kept off the list at the moment.</summary>
        public static int HiddenCount(IReadOnlyList<RivalStaffMember> roster)
        {
            var hidden = 0;

            foreach (var member in roster)
            {
                if (member.Rating > VisibleCeiling)
                {
                    hidden++;
                }
            }

            return hidden;
        }

        /// <summary>
        /// Whether a set already holds an id.
        ///
        /// Written out because `IReadOnlyCollection` has no `Contains` of its own and the extension
        /// the compiler reaches for first is the one on spans of characters, which compiles into
        /// something entirely unrelated.
        /// </summary>
        private static bool Holds(IReadOnlyCollection<int> ids, int id)
        {
            foreach (var value in ids)
            {
                if (value == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static PlayerSkill PositionFor(DeterministicRandom random)
        {
            var roll = random.NextDouble();

            // Weighted toward the disciplines a frontier lab is actually mostly made of.
            return roll < 0.34 ? PlayerSkill.Development
                : roll < 0.56 ? PlayerSkill.Concept
                : roll < 0.72 ? PlayerSkill.DataEngineering
                : roll < 0.85 ? PlayerSkill.Software
                : roll < 0.94 ? PlayerSkill.Safety
                : PlayerSkill.Management;
        }

        private static int FoundedDayIndex(CompetitorId lab) =>
            LabDossiers.TryGet(lab, out var dossier)
                ? dossier.Founded.DayIndex
                : GameDate.Start.DayIndex;

        /// <summary>
        /// One seed from two, spread out.
        ///
        /// Adding the two together would give neighbouring labs neighbouring seeds and neighbouring
        /// seeds give visibly similar rosters, which reads as a bug rather than as a coincidence.
        /// </summary>
        private static uint Mix(uint seed, uint lab)
        {
            unchecked
            {
                var value = seed ^ (lab * 2654435761u);
                value ^= value >> 15;
                value *= 2246822519u;
                value ^= value >> 13;
                return value == 0 ? 0x9E3779B9u : value;
            }
        }
    }
}
