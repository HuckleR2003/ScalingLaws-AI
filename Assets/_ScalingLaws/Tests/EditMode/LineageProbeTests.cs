using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Eight releases in one product line, each better than the last, measured rather than assumed.
    ///
    /// **Asked on 2026-10-24, in four parts**: do the figures actually improve as the models do,
    /// does the audience really move onto the newer one, does a successor inherit the interest its
    /// predecessor earned, and does reaching for newer technology pay. Every fixture around this
    /// one tests two models. `ReleaseLineTests` holds the drift between two versions,
    /// `CompanySimulationTests` ships one, and `PlayabilityTests` runs a scripted campaign whose
    /// operator was rewritten twice to stop it tripping over its own controls.
    ///
    /// Nothing walks a long line of successors, which is what a player who is doing well does, and
    /// it is the one shape where a small per-release error compounds into the late game this
    /// project keeps being asked about.
    ///
    /// **Every figure is printed, not only asserted.** The question was about how the game thinks,
    /// and a green bar cannot answer that. Two of the assertions here were written after reading
    /// the first table rather than before it, and the comments say which.
    /// </summary>
    public sealed class LineageProbeTests
    {
        private const string Line = "Aurora";

        /// <summary>Far enough apart that the market has settled before the next one lands.</summary>
        private const int DaysBetweenReleases = 180;

        /// <summary>
        /// A fleet large enough that capacity is never the binding constraint.
        ///
        /// **This is a measurement about interest, not about queueing.** A company short of compute
        /// has its reliability cut, which cuts attractiveness, which would show up here as the
        /// market failing to notice a better model when what actually happened is that it could
        /// not be served. `RoomOffloadTests` is where load is the subject.
        /// </summary>
        private const double RentedPetaflops = 3_000.0;

        private static CompanySimulation Company(long cash = 8_000_000_000)
        {
            var state = new CompanyState("Prometheus AI", 4242)
            {
                Date = GameDate.FromCalendar(2023, 1, 1),
                CashUsd = cash
            };

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(RentedPetaflops);

            return simulation;
        }

        /// <summary>
        /// Capability that keeps pace with its own year rather than a flat ladder.
        ///
        /// The table's frontier runs about 47 in 2023 and about 75 by 2026, so a line that climbs
        /// 45 to 80 over three and a half years stays a real competitor throughout. A flat ladder
        /// would measure a company falling off the board, which is a different question.
        /// </summary>
        private static double CapabilityOf(int release) => 45.0 + 5.0 * release;

        /// <summary>
        /// One release out of the archive, by name.
        ///
        /// **`ModelHistory()` is sorted newest first and says so in its own comment.** The first
        /// version of this fixture read `.Last()` as "the one just shipped", which is the
        /// oldest, so it measured the superseded predecessor and reported zero users for a
        /// successor holding 2.3 million. Both tests failed and the game was right both times.
        /// A name cannot be reordered.
        /// </summary>
        private static ModelRecord Named(IEnumerable<ModelRecord> rows, int release)
            => rows.First(row => row.Model.Name == "Aurora " + (release + 1));

        private static DeployedModel Release(int release, GameDate on, string family)
            => new DeployedModel(
                "Aurora " + (release + 1),
                ArchitectureId.DenseTransformer,
                CapabilityOf(release),
                on,
                2e10 * (1.0 + 0.25 * release),
                1.0,
                ModelType.General,
                family);

        [Test]
        public void EightReleasesInOneLineClimbAndOnlyTheNewestIsOnSale()
        {
            var simulation = Company();
            var state = simulation.State;

            var rows = new List<string>();

            for (var release = 0; release < 8; release++)
            {
                state.AddDeployedModel(Release(release, state.Date, Line));

                // AddDeployedModel only adds to the list; LastReleaseDate is set by the release path and nothing else. Without this a fixture runs a company the game reads as having shipped nothing since day zero, which is -0.0016 a day of staleness for ever.
                state.LastReleaseDate = state.Date;
                simulation.Advance(DaysBetweenReleases);

                var history = simulation.ModelHistory();
                var marketed = history.Where(record => record.IsMarketed).ToList();
                var newest = Named(history, release);

                rows.Add("LINE " + (release + 1)
                    + " cap=" + newest.Model.Capability.ToString("0.0")
                    + " users=" + newest.Users.ToString("0")
                    + " onSale=" + marketed.Count
                    + " rep=" + state.Reputation.ToString("0.000")
                    + " fans=" + state.Fans.ToString("0")
                    + " served=" + state.LastQuality.Demanded.ToString("0.0")
                    + " load=" + state.LastQuality.Utilisation.ToString("0.000"));

                Assert.That(marketed.Count, Is.EqualTo(1),
                    "One line is one product, so only its strongest live model reaches the market. "
                    + "After release " + (release + 1) + " the company is selling "
                    + marketed.Count + " of them: "
                    + string.Join(", ", marketed.Select(record => record.Model.Name)));

                Assert.That(marketed[0].Model.Name, Is.EqualTo(newest.Model.Name),
                    "The newest release is the strongest and should be the one on sale.");
            }

            UnityEngine.Debug.Log(string.Join("\n", rows));

            // **Written after reading the table, and it is the weaker half of the claim on
            // purpose.** The strong version, that each release holds more people than the last,
            // is not true and should not be: the audience grows and the rivals climb too, so a
            // release can be better than its predecessor and still hold fewer people than that
            // predecessor did in an easier year. What has to be true is that the line never goes
            // quiet while it keeps shipping better models.
            var finalHistory = simulation.ModelHistory();

            Assert.That(Named(finalHistory, 7).Users, Is.GreaterThan(0.0),
                "Eight increasingly capable releases and the newest one holds nobody. The line has "
                + "gone silent while still shipping, which is the late-game report this fixture "
                + "exists to catch.");

            Assert.That(state.Fans, Is.GreaterThan(0.0),
                "A company that shipped eight models people were using has no following at all.");
        }

        /// <summary>
        /// The inheritance question, and it is the one with a real answer either way.
        ///
        /// Two identical companies build the same following on the same first model. One ships its
        /// successor **into that line**, where it supersedes the predecessor and takes the
        /// audience; the other starts a **new line**, where both stay on sale and the catalogue
        /// nesting scores the pair at less than two singles.
        ///
        /// This is not a test that one is correct. It is the measurement of what the choice costs,
        /// which is what the player is actually deciding.
        /// </summary>
        [Test]
        public void ASuccessorInTheSameLineTakesOverRatherThanSplittingTheAudience()
        {
            var sameLine = Company();
            var newLine = Company();

            foreach (var simulation in new[] { sameLine, newLine })
            {
                simulation.State.AddDeployedModel(Release(0, simulation.State.Date, Line));
                simulation.State.LastReleaseDate = simulation.State.Date;
                simulation.Advance(2 * DaysBetweenReleases);
            }

            var earnedFans = sameLine.State.Fans;

            Assert.That(newLine.State.Fans, Is.EqualTo(earnedFans).Within(1e-6),
                "The two companies have to be identical up to the fork, or nothing after it can "
                + "be attributed to the fork.");

            sameLine.State.AddDeployedModel(Release(1, sameLine.State.Date, Line));
            sameLine.State.LastReleaseDate = sameLine.State.Date;

            newLine.State.AddDeployedModel(Release(1, newLine.State.Date, "Borealis"));
            newLine.State.LastReleaseDate = newLine.State.Date;

            sameLine.Advance(DaysBetweenReleases);
            newLine.Advance(DaysBetweenReleases);

            var sameRows = sameLine.ModelHistory();
            var newRows = newLine.ModelHistory();

            var sameTotal = sameRows.Where(r => r.IsMarketed).Sum(r => r.Users);
            var newTotal = newRows.Where(r => r.IsMarketed).Sum(r => r.Users);

            // Release 1 is the successor and release 0 is the model it was shipped after.
            var sameSuccessor = Named(sameRows, 1);
            var samePredecessor = Named(sameRows, 0);
            var newSuccessor = Named(newRows, 1);
            var newPredecessor = Named(newRows, 0);

            UnityEngine.Debug.Log(
                "FORK fansAtFork=" + earnedFans.ToString("0")
                + "\nFORK sameLine onSale=" + sameRows.Count(r => r.IsMarketed)
                + " successor=" + sameSuccessor.Users.ToString("0")
                + " predecessor=" + samePredecessor.Users.ToString("0")
                + " total=" + sameTotal.ToString("0")
                + " rep=" + sameLine.State.Reputation.ToString("0.000")
                + " fans=" + sameLine.State.Fans.ToString("0")
                + "\nFORK newLine  onSale=" + newRows.Count(r => r.IsMarketed)
                + " successor=" + newSuccessor.Users.ToString("0")
                + " predecessor=" + newPredecessor.Users.ToString("0")
                + " total=" + newTotal.ToString("0")
                + " rep=" + newLine.State.Reputation.ToString("0.000")
                + " fans=" + newLine.State.Fans.ToString("0"));

            Assert.That(sameRows.Count(r => r.IsMarketed), Is.EqualTo(1),
                "A successor in the same line has to replace its predecessor on the shelf.");

            Assert.That(newRows.Count(r => r.IsMarketed), Is.EqualTo(2),
                "A genuinely separate line keeps both products on sale, which is the whole reason "
                + "to start one.");

            // **The successor does not start from nothing.** Reputation and the following are
            // company-wide and both companies carry them across the fork, so the question is
            // whether the model that replaced its predecessor arrives holding people rather than
            // having to earn them again from zero.
            Assert.That(sameSuccessor.Users, Is.GreaterThan(0.0),
                "The successor took over the line and holds nobody, so taking over a line hands "
                + "on none of the interest the line had earned.");

            Assert.That(samePredecessor.Users, Is.EqualTo(0.0),
                "The superseded predecessor is still holding people, so the company is being "
                + "paid twice for one product.");
        }
    }
}
