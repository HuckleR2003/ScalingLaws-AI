using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The deduplication pass, and the fact that for as long as it existed only one of its three
    /// options was ever correct.
    ///
    /// **Found by a tester asking a question, not by a crash.** On 2026-10-27: "what would even be
    /// the advantage of choosing raw in this case?". There was none. Raw kept the same tokens as
    /// Standard and multiplied their worth by 0.92, so it was Standard with a penalty. Aggressive
    /// came to 0.80 x 1.14 = 0.912, worse still, **and it is gated behind a research node**, so a
    /// player could spend points and cash to unlock an option that made their models worse.
    ///
    /// The cause was where the pass was applied. It scaled the tokens after the corpus had been
    /// clamped to the request, and the compute bill was read from the figure before it, so all
    /// three passes trained the same tokens over the same days for the same money. Scrubbing is a
    /// fact about how many tokens the mix holds, so it now happens in `DatasetCatalog.Blend`
    /// beside the founder's supply multiplier, and it moves the ceiling on what a run can ask for.
    ///
    /// Written as three named regimes rather than as values: the claim worth holding is **that
    /// each option is the right answer somewhere**, not that a constant has a particular size.
    /// Same shape as `NoChannelIsSimplyBetterThanAnother`.
    /// </summary>
    public sealed class DeduplicationTests
    {
        private const int ParametersBillions = 5;

        private static TrainingProjection Project(
            double requestedTokensBillions, DeduplicationPass pass)
        {
            var market = MarketModel.Evaluate(GameDate.Start);
            var pool = new ComputePool();
            pool.SetRentedAcceleratorEquivalent(900, market.RentableGeneration);
            var profile = pool.BuildProfile(GameDate.Start, market);

            var blueprint = new ModelBlueprint(
                    "Probe", ArchitectureId.DenseTransformer,
                    ParametersBillions, requestedTokensBillions, DatasetSource.WebCrawl)
                .WithDeduplication(pass);

            return TrainingPlanner.Project(blueprint, profile, market, 0.0);
        }

        /// <summary>
        /// The starting corpus, read rather than typed.
        ///
        /// **Asked for far more than any corpus holds, on purpose.** The blend reports what it can
        /// supply against what was requested, so probing with a small request reports the request
        /// straight back. The first version of this fixture did exactly that, measured nothing,
        /// and failed in a way that read like the repair had not worked.
        /// </summary>
        private static double CorpusBillions() =>
            Project(100_000_000.0, DeduplicationPass.Standard).Blend.AvailableTokensBillions;

        private static double Kept(DeduplicationPass pass) =>
            TrainingChoiceCatalog.Get(pass).TokensKept;

        /// <summary>
        /// The whole balance of the game is tuned against a run that never opens this control, so
        /// the default has to be the reference on both axes. Pinned as values, because here the
        /// exact number is the claim.
        /// </summary>
        [Test]
        public void StandardIsExactlyNeutralOnBothAxes()
        {
            var standard = TrainingChoiceCatalog.Get(DeduplicationPass.Standard);

            Assert.That(standard.TokensKept, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(standard.Quality, Is.EqualTo(1.0).Within(1e-9));
        }

        /// <summary>
        /// Keeping the repeats leaves a bigger corpus than scrubbing them out. Stated on the
        /// catalogue so the ordering cannot be edited away without a failure, and the three regimes
        /// below are built from it rather than from numbers typed twice.
        /// </summary>
        [Test]
        public void KeepingTheRepeatsLeavesMoreCorpusThanScrubbingThem()
        {
            Assert.That(Kept(DeduplicationPass.None),
                Is.GreaterThan(Kept(DeduplicationPass.Standard)));
            Assert.That(Kept(DeduplicationPass.Aggressive),
                Is.LessThan(Kept(DeduplicationPass.Standard)));
        }

        /// <summary>
        /// **Regime one: corpus to spare.** Every pass can feed the run, so all three train the
        /// tokens that were asked for, over the same days, for the same money, and only what each
        /// token is worth differs. This is the regime the research node is sold for, and the one
        /// where it used to hand back a worse model than not having it.
        /// </summary>
        [Test]
        public void AggressiveWinsWhenThereIsCorpusToSpare()
        {
            var request = CorpusBillions() * Kept(DeduplicationPass.Aggressive) * 0.6;

            var raw = Project(request, DeduplicationPass.None);
            var standard = Project(request, DeduplicationPass.Standard);
            var aggressive = Project(request, DeduplicationPass.Aggressive);

            Assert.That(aggressive.IsFeasible, Is.True, aggressive.BlockingReason);

            Assert.That(aggressive.TrainingPetaflopDays,
                Is.EqualTo(standard.TrainingPetaflopDays).Within(1e-6),
                "With corpus to spare the scrub costs nothing in compute, so the bills must match "
                + "and the only difference left is quality.");

            Assert.That(aggressive.ProjectedCapability, Is.GreaterThan(standard.ProjectedCapability),
                $"Aggressive {aggressive.ProjectedCapability:0.000} against standard "
                + $"{standard.ProjectedCapability:0.000}. A node the player paid points and cash "
                + "for must not produce a worse model than never having researched it.");

            Assert.That(standard.ProjectedCapability, Is.GreaterThan(raw.ProjectedCapability),
                "With corpus to spare the repeats buy nothing and cost quality.");
        }

        /// <summary>
        /// **Regime two: more corpus than a hard scrub leaves, less than the crawl holds.** The
        /// scrubbed mix can no longer feed the run and is refused outright; the raw mix can feed it
        /// but every token is worth less. The untouched option wins, which is what makes it a
        /// sensible default rather than merely the neutral one.
        /// </summary>
        [Test]
        public void StandardWinsWhenAHardScrubWouldLeaveTooLittle()
        {
            var corpus = CorpusBillions();
            var request = corpus * (Kept(DeduplicationPass.Aggressive) + 1.0) * 0.5;

            var raw = Project(request, DeduplicationPass.None);
            var standard = Project(request, DeduplicationPass.Standard);
            var aggressive = Project(request, DeduplicationPass.Aggressive);

            Assert.That(aggressive.IsFeasible, Is.False,
                "A hard scrub has to be able to leave too little corpus, or it costs nothing.");
            Assert.That(aggressive.BlockingReason, Does.Contain("supplies"),
                "A refusal has to say how many tokens the mix actually holds.");

            Assert.That(standard.IsFeasible, Is.True, standard.BlockingReason);
            Assert.That(standard.ProjectedCapability, Is.GreaterThan(raw.ProjectedCapability),
                $"Standard {standard.ProjectedCapability:0.000} against raw "
                + $"{raw.ProjectedCapability:0.000}.");
        }

        /// <summary>
        /// **Regime three: a run bigger than the scrubbed corpus can feed.** This is what Raw is
        /// for and it is decisive rather than marginal: the other two passes cannot start the run
        /// at all. A company short of data gets a bigger model by not cleaning the crawl, and pays
        /// for it in what each token is worth.
        /// </summary>
        [Test]
        public void RawIsTheOnlyPassThatCanFeedARunBeyondTheScrubbedCorpus()
        {
            var corpus = CorpusBillions();
            var request = corpus * (Kept(DeduplicationPass.None) + 1.0) * 0.5;

            var raw = Project(request, DeduplicationPass.None);
            var standard = Project(request, DeduplicationPass.Standard);

            Assert.That(raw.IsFeasible, Is.True,
                $"Raw refused a run its own corpus can feed: {raw.BlockingReason}");
            Assert.That(standard.IsFeasible, Is.False,
                "If the scrubbed corpus can also feed this run then the regime does not exist and "
                + "Raw is a trap again.");

            Assert.That(raw.TokensPerParameter, Is.GreaterThan(0.0));
        }

        /// <summary>
        /// The guard that would have caught the original fault, stated as the thing a player cares
        /// about: no card on this screen may be the wrong click in every situation.
        /// </summary>
        [Test]
        public void EveryPassIsTheRightAnswerInExactlyOneOfTheThreeRegimes()
        {
            var corpus = CorpusBillions();

            var spare = corpus * Kept(DeduplicationPass.Aggressive) * 0.6;
            var middle = corpus * (Kept(DeduplicationPass.Aggressive) + 1.0) * 0.5;
            var stretched = corpus * (Kept(DeduplicationPass.None) + 1.0) * 0.5;

            Assert.That(Best(spare), Is.EqualTo(DeduplicationPass.Aggressive));
            Assert.That(Best(middle), Is.EqualTo(DeduplicationPass.Standard));
            Assert.That(Best(stretched), Is.EqualTo(DeduplicationPass.None));
        }

        /// <summary>The pass a player would pick for this run, knowing everything.</summary>
        private static DeduplicationPass Best(double requestedTokensBillions)
        {
            var winner = DeduplicationPass.Standard;
            var score = double.NegativeInfinity;

            foreach (var pass in new[]
                     {
                         DeduplicationPass.None,
                         DeduplicationPass.Standard,
                         DeduplicationPass.Aggressive
                     })
            {
                var projection = Project(requestedTokensBillions, pass);
                if (!projection.IsFeasible)
                {
                    continue;
                }

                if (projection.ProjectedCapability > score)
                {
                    score = projection.ProjectedCapability;
                    winner = pass;
                }
            }

            return winner;
        }
    }
}
