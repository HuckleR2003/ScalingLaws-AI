using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The one claim the whole design rests on, asserted rather than believed.
    ///
    /// `CLAUDE.md` opens with it and the store page repeats it: a company that ships one model and
    /// coasts does not survive. **Nothing in this project actually checked that.** The campaign
    /// probe measures it and deliberately asserts nothing, `PlayabilityTests` asserts the opposite
    /// case (that a competent player lives), and between them a change that made coasting
    /// survivable would have gone in green.
    ///
    /// That is not a hypothetical. Before the probe was given a payroll it showed a company
    /// shipping one model in 2022 and still holding twelve million dollars in 2035.
    /// </summary>
    public sealed class SpineTests
    {
        /// <summary>
        /// How long a coasting company is allowed to last before this fails.
        ///
        /// Measured, not chosen: the probe's coasting style goes under in its fifth year with four
        /// people and an office. Eight is that with room for ordinary drift, and it is still far
        /// inside the fourteen-year campaign, which is what makes the guard mean something.
        /// </summary>
        public const int LongestCoastYears = 8;

        /// <summary>
        /// The company has to last at least this long, or the test is passing for the wrong reason.
        ///
        /// **Both bounds matter and this is the one that is easy to lose.** A scenario that folds
        /// in its first year is not demonstrating that coasting is fatal, it is demonstrating that
        /// the setup could not afford itself, and it would keep passing while the thing it exists
        /// to guard quietly stopped being true.
        /// </summary>
        public const int ShortestCoastYears = 2;

        /// <summary>Staff and an office, because a company with no costs is not a company.</summary>
        private const int CoastingHeadcount = 4;

        [Test]
        public void ShippingOneModelAndCoastingEndsTheCompany()
        {
            var state = new CompanyState("Coaster", 0x5C0A57u);
            var simulation = new CompanySimulation(state);

            // Enough compute to train at all, a place to sit, and people to pay. This is a real
            // company that stops making decisions, not a hermit with the lights off: the hermit
            // survives, and the design has never claimed otherwise.
            simulation.SetRentedAccelerators(24);
            simulation.TryMoveOffice(OfficeTier.Loft, out _);

            for (var index = 0; index < CoastingHeadcount; index++)
            {
                Employ(simulation, index);
            }

            ShipOneModel(simulation);

            Assert.That(state.ReleasedModelCount, Is.GreaterThan(0),
                "The scenario never shipped anything, so it is measuring an empty company rather "
                + "than a coasting one.");

            // And now nothing. No release, no research, no upgrade, no marketing.
            var years = 0;

            for (; years < LongestCoastYears && !state.IsBankrupt; years++)
            {
                simulation.Advance(365);
            }

            Assert.That(years, Is.GreaterThanOrEqualTo(ShortestCoastYears),
                $"The company was gone after {years} year(s), which is too fast to be about "
                + "coasting. Either the payroll and the office are unaffordable on the opening "
                + "balance, or the one model never earned anything, and in both cases this test is "
                + "no longer watching what it was written to watch.");

            Assert.That(state.IsBankrupt, Is.True,
                $"A company that shipped one model and then made no decision for "
                + $"{LongestCoastYears} years is still trading on {state.CashUsd:N0} dollars. "
                + "The spine at the top of CLAUDE.md, and the sentence on the store page, are no "
                + "longer true.");
        }

        /// <summary>
        /// Puts one person on the payroll through the real hiring chain.
        ///
        /// Through the chain rather than by writing into the roster, because the point of the test
        /// is a company carrying genuine costs and a hire poked in directly could stop costing
        /// money the day somebody changes how wages are billed.
        /// </summary>
        private static void Employ(CompanySimulation simulation, int index)
        {
            var shortlist = simulation.Shortlist(PlayerSkill.Development, HireSource.Agency, 40, 2);

            if (shortlist.Count == 0
                || !string.IsNullOrEmpty(simulation.TryApproach(shortlist[0])))
            {
                return;
            }

            // The approach answers by post a few days later, and the letter is where somebody
            // actually joins.
            simulation.Advance(30);

            foreach (var letter in simulation.State.Mail.All)
            {
                if (letter.IsClosed || letter.Kind != MailKind.JobOffer || letter.Candidate == null)
                {
                    continue;
                }

                simulation.Negotiate(letter, letter.Candidate.AskingHourlyUsd, 0L, out _);
            }
        }

        /// <summary>Trains and releases exactly one model, then never again.</summary>
        private static void ShipOneModel(CompanySimulation simulation)
        {
            var state = simulation.State;
            var profile = simulation.Profile;

            var blueprint = TrainingPlanner.OptimalBlueprintForBudget(
                "The only one",
                ArchitectureId.DenseTransformer,
                profile.EffectivePetaflops * state.TrainingComputeShare * 200.0,
                state.OwnedDataSources);

            if (blueprint.ParameterCountBillions <= 0.0
                || !simulation.TryStartTraining(blueprint, out _))
            {
                return;
            }

            // Long enough for the run to land on the shelf, then it goes on sale.
            for (var day = 0; day < 400 && state.Shelf.Count == 0; day += 10)
            {
                simulation.Advance(10);
            }

            if (state.Shelf.Count > 0)
            {
                simulation.TryReleaseModel(0, 1.0, out _);
            }
        }
    }
}
