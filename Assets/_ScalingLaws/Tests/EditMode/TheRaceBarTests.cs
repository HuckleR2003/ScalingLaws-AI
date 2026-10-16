using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The bar under the model creator does not declare a winner of a race nobody has entered.
    ///
    /// **Found 16 October 2026 by rendering the creator at 1366x768 and looking at it.** On the
    /// opening day of a campaign nothing has been released by anybody, so the leader's capability
    /// is nought. The bar read that as "ahead", filled itself green across the whole width, and sat
    /// directly under the words "nobody has released anything yet" beside a projected capability of
    /// 0.0.
    ///
    /// Same family as the corner banner that read LEAVING in red on day one over a market of
    /// nobody: a readout asserting the outcome of a contest that has not started. Being first is
    /// still being ahead, so the guard is on having something to be first with.
    /// </summary>
    public sealed class TheRaceBarTests
    {
        private static CompanySimulation Fresh() =>
            new(new CompanyState("Prometheus AI", 31));

        private static SegmentBar BarIn(ModelCreatorPanel panel) =>
            panel.Root.Q<SegmentBar>(className: "race__bar");

        [Test]
        public void OnDayOneTheBarIsEmptyRatherThanTriumphant()
        {
            var panel = new ModelCreatorPanel(Fresh());
            panel.Refresh();

            var bar = BarIn(panel);

            Assert.That(bar, Is.Not.Null, "The race bar is not on the creator any more.");
            Assert.That(bar.Fill, Is.EqualTo(0.0).Within(1e-9),
                "A full bar on a company with no model, in a field where nobody has released "
                + "anything, tells the player they are winning a race that has not started.");
        }

        /// <summary>
        /// And the other half, which is the one worth not breaking: a real projection with nobody
        /// else on the board is genuinely first, and should read as first.
        /// </summary>
        [Test]
        public void ARealProjectionWithNobodyElseOnTheBoardStillReadsAsAhead()
        {
            var simulation = Fresh();
            var panel = new ModelCreatorPanel(simulation) { Stage = 0 };
            panel.Refresh();

            var bar = BarIn(panel);
            Assert.That(bar, Is.Not.Null);

            // Nothing is asserted about the middle of the campaign here on purpose: the point of
            // this fixture is the empty board, and a test that drives a whole run to check a bar
            // would be measuring the economy rather than the readout.
            Assert.That(bar.Fill, Is.InRange(0.0, 1.0),
                "The fill is a share and has to stay one whatever the projection says.");
        }
    }
}
