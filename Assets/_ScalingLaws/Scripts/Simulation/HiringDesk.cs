using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A candidate the company has reached out to, who has not answered yet.
    ///
    /// **Nobody is hired the moment they are clicked, and that is the point of this type.** The old
    /// system turned cash into headcount in one frame, which made hiring a purchase. Two to four
    /// days of nothing turns it into a plan: the player commits to wanting somebody before they
    /// know what that person will cost, and a company that leaves it until the desk is needed will
    /// be short a person for a week.
    /// </summary>
    public sealed class Approach
    {
        public Approach(Candidate candidate, int startedDayIndex, int daysNeeded)
        {
            Candidate = candidate;
            StartedDayIndex = startedDayIndex;
            DaysNeeded = Math.Clamp(daysNeeded, 1, 30);
        }

        public Candidate Candidate { get; }
        public int StartedDayIndex { get; }
        public int DaysNeeded { get; }

        public int DaysElapsed { get; private set; }

        public bool HasAnswered => DaysElapsed >= DaysNeeded;

        public int DaysLeft => Math.Max(0, DaysNeeded - DaysElapsed);

        public double Progress => DaysNeeded <= 0
            ? 1.0
            : Math.Clamp(DaysElapsed / (double)DaysNeeded, 0.0, 1.0);

        public void Advance() => DaysElapsed++;

        /// <summary>Rebuilds one from a save. Days elapsed is trusted as written.</summary>
        public void Restore(int daysElapsed) => DaysElapsed = Math.Max(0, daysElapsed);
    }

    /// <summary>
    /// Everything the company has in flight on the hiring side.
    ///
    /// Holds the approaches waiting to answer, the IThand partnership, and the counter that gives
    /// every candidate a stable id. It does not hold the people already hired — that is the
    /// roster's job, and a desk that also owned the payroll would be two things.
    /// </summary>
    public sealed class HiringDesk
    {
        /// <summary>Approaches that can be open at once. Beyond this the player is just spamming.</summary>
        public const int MaximumOpenApproaches = 6;

        private readonly List<Approach> approaches = new();

        public IReadOnlyList<Approach> Approaches => approaches;

        /// <summary>Bought from IThand.hck. Raises the remote ceiling and never expires.</summary>
        public bool HasRemotePartnership { get; set; }

        /// <summary>
        /// Hiring's own random stream, separate from the company's.
        ///
        /// **This is not tidiness, it is a bug that already happened.** Rolling candidates from the
        /// shared stream meant that opening the agency, or an applicant writing in unprompted,
        /// advanced the same sequence that decides market noise and incident rolls. Every number
        /// downstream shifted, and a marketing test that had been stable for months started
        /// reporting that advertising did nothing.
        ///
        /// Seeded from a constant rather than forked from the company's, because forking draws from
        /// the very stream this exists to leave alone. Its state is saved, so a campaign reloads
        /// the same people.
        /// </summary>
        public DeterministicRandom Random { get; } = new(SeedSalt);

        /// <summary>Arbitrary, fixed, and different from every other stream's seed.</summary>
        private const uint SeedSalt = 0x48495245;

        /// <summary>Next candidate id. Monotonic, so two people are never the same person.</summary>
        public int NextCandidateId { get; set; } = 1;

        public int OpenCount => approaches.Count;

        public bool CanApproach => approaches.Count < MaximumOpenApproaches;

        /// <summary>How many remote people the company may carry.</summary>
        public int RemoteSeats => HiringChannels.RemoteSeats(HasRemotePartnership);

        /// <summary>
        /// Starts talking to somebody.
        ///
        /// The delay is rolled here rather than passed in, so every route into hiring waits the
        /// same two to four days and no screen can quietly offer a faster one.
        /// </summary>
        public Approach Begin(Candidate candidate, GameDate today)
        {
            if (candidate == null || !CanApproach)
            {
                return null;
            }

            var days = Random.NextInt(HiringChannels.FastestContactDays,
                HiringChannels.SlowestContactDays + 1);

            var approach = new Approach(candidate, today.DayIndex, days);
            approaches.Add(approach);
            return approach;
        }

        /// <summary>
        /// Moves every open approach on a day and returns the ones that answered.
        ///
        /// Returned rather than acted on, because posting a letter costs money nowhere and the
        /// simulation is the only thing allowed to decide what happens next.
        /// </summary>
        public IReadOnlyList<Approach> Advance()
        {
            var answered = new List<Approach>();

            foreach (var approach in approaches)
            {
                approach.Advance();

                if (approach.HasAnswered)
                {
                    answered.Add(approach);
                }
            }

            foreach (var done in answered)
            {
                approaches.Remove(done);
            }

            return answered;
        }

        /// <summary>The one shown in the corner banner: whichever answers first.</summary>
        public Approach Soonest =>
            approaches.OrderBy(entry => entry.DaysLeft).FirstOrDefault();

        public void Restore(IEnumerable<Approach> saved, bool partnership, int nextId,
            uint randomState)
        {
            Random.State = randomState == 0u ? SeedSalt : randomState;

            approaches.Clear();

            if (saved != null)
            {
                approaches.AddRange(saved.Where(entry => entry != null));
            }

            HasRemotePartnership = partnership;
            NextCandidateId = Math.Max(1, nextId);
        }

        public void Clear() => approaches.Clear();
    }
}
