using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The desk waits for the opening tour, and then says it.
    ///
    /// **The first repair for this was wrong in a way worth keeping written down.** A tester
    /// watched the cousin telephone about the support desk in the middle of his own tutorial; the
    /// call offers a walkthrough, taking it replaced the tour, and the tour never came back. The
    /// shell was taught to refuse the call, which stopped the collision and **lost the call**:
    /// `SupportAnnounced` had already been set, so nothing would ever say it again.
    ///
    /// The gate is in the simulation now, so the news waits rather than evaporating. It asks
    /// `IsWalkingTheTour` rather than `IsStillRunning`, because somebody who never answers the
    /// opening call sits at `Unseen` for the whole campaign and must still be told about the post.
    /// </summary>
    public sealed class SupportCallOrderTests
    {
        /// <summary>
        /// A company with a model on sale and a cluster to serve it from, at one tour stage.
        ///
        /// **The desk is fed by the day, from users the company is actually serving.** The first
        /// version of this fixture poked `BacklogHours` and measured nothing: it is a reading
        /// rather than a setting, and the day calls `Support.Advance` with the company's own
        /// served users before it announces anything.
        /// </summary>
        private static CompanySimulation Serving(GuideStage stage)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", 404));
            simulation.SetRentedPetaflops(80.0);

            simulation.State.Guide.Stage = stage;

            simulation.State.AddDeployedModel(new DeployedModel(
                "Atlas One", ArchitectureId.DenseTransformer, 48.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            return simulation;
        }

        private static int Announcements(CompanySimulation simulation)
        {
            var said = 0;

            while (simulation.State.TryDequeueEvent(out var raised))
            {
                if (raised.Type == CompanyEventType.FirstSupportTicket)
                {
                    said++;
                }
            }

            return said;
        }

        private static int Run(CompanySimulation simulation, int days)
        {
            var said = 0;

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
                said += Announcements(simulation);
            }

            return said;
        }

        [Test]
        public void TheDeskSaysNothingWhileThePlayerIsMidTutorial()
        {
            foreach (var stage in new[]
                     {
                         GuideStage.Talking, GuideStage.Touring, GuideStage.Paused
                     })
            {
                var simulation = Serving(stage);

                var said = Run(simulation, 150);

                Assert.That(said, Is.Zero,
                    "At " + stage + " the desk announced itself " + said + " times over the top of "
                    + "the opening conversation. That call offers a walkthrough, and taking it "
                    + "replaces the tour with no way back to it.");

                Assert.That(simulation.State.SupportAnnounced, Is.False,
                    "At " + stage + " the flag was set, so even once the tour ends nothing will "
                    + "ever say it. That is the first repair's bug, not a fix for it.");
            }
        }

        /// <summary>
        /// **And it is held, not thrown away.** This is the half the first repair got wrong.
        /// </summary>
        [Test]
        public void TheDeskSaysItOnceTheTourIsOutOfTheWay()
        {
            var simulation = Serving(GuideStage.Touring);

            Assert.That(Run(simulation, 150), Is.Zero,
                "The desk announced itself during the tutorial.");

            simulation.State.Guide.Stage = GuideStage.Finished;

            Assert.That(Run(simulation, 5), Is.EqualTo(1),
                "The tour finished and the desk never said anything. The news was skipped rather "
                + "than held, so this player is never told the post has started arriving and the "
                + "first they know of it is a fifth of their happiness gone.");

            Assert.That(simulation.State.SupportAnnounced, Is.True);
        }

        /// <summary>
        /// **`Unseen` is not mid-tutorial**, and silencing it would be permanent for anybody who
        /// never answers the opening call.
        /// </summary>
        [Test]
        public void ACompanyThatNeverAnsweredTheOpeningCallIsStillToldAboutThePost()
        {
            var simulation = Serving(GuideStage.Unseen);

            Assert.That(Run(simulation, 150), Is.EqualTo(1),
                "A player who never picked up the opening call is never told the support desk has "
                + "started taking post. They sit at Unseen for the rest of the campaign, so this "
                + "is not a wait, it is silence.");
        }

        [Test]
        public void ItIsSaidOnceAndNotEveryDayAfterwards()
        {
            var simulation = Serving(GuideStage.Finished);

            Assert.That(Run(simulation, 150), Is.EqualTo(1),
                "A finished tour and a desk taking post said nothing, or said it more than once.");

            var again = Run(simulation, 60);

            Assert.That(again, Is.Zero,
                "The desk announced its first ticket " + again + " more times in the two months "
                + "after it had already done so, so the phone rings again and again.");
        }
    }
}
