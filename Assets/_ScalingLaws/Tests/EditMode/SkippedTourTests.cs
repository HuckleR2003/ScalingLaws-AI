using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Skipping the tour must not cost the free research node.
    ///
    /// **Reported plainly.** The favour is handed over on the step where the cousin offers it, so a
    /// player who pressed SKIP on the first screen never reached it: the task list went on saying
    /// to start researching, the tree said it needed points a new company cannot have, and there
    /// was nothing on the screen to click. Nothing about that reads as a choice the player made.
    /// </summary>
    public sealed class SkippedTourTests
    {
        private static CompanySimulation Company() =>
            new(new CompanyState("Prometheus AI", 4242));

        [Test]
        public void SkippingHandsTheFavourOverInsteadOfLosingIt()
        {
            var simulation = Company();

            Assert.That(simulation.State.Guide.FreeResearchOwed, Is.False,
                "Nothing has been given yet.");

            Assert.That(simulation.TryGiveTheSkippedFavour(), Is.True);

            Assert.That(simulation.State.Guide.FreeResearchOwed, Is.True,
                "The node the cousin promised is still owed, whether or not his tour was watched.");

            Assert.That(simulation.State.Guide.FavourGranted, Is.True);
        }

        [Test]
        public void HeWritesToSayWhy()
        {
            var simulation = Company();
            var before = simulation.State.Mail.All.Count;

            simulation.TryGiveTheSkippedFavour();

            Assert.That(simulation.State.Mail.All.Count, Is.EqualTo(before + 1),
                "One letter, which is the whole of what was asked for: a player who skipped the "
                + "tour has no other way to learn the node is paid for.");

            var letter = simulation.State.Mail.All[^1];

            Assert.That(letter.Sender, Is.EqualTo(Loc.T("guide.name")));
            Assert.That(letter.Body, Is.Not.Empty);
        }

        /// <summary>
        /// Twice is not two favours, and after the tour gave it there is nothing left to give.
        /// </summary>
        [Test]
        public void ItCanOnlyHappenOnce()
        {
            var simulation = Company();
            var letters = simulation.State.Mail.All.Count;

            Assert.That(simulation.TryGiveTheSkippedFavour(), Is.True);
            Assert.That(simulation.TryGiveTheSkippedFavour(), Is.False);

            Assert.That(simulation.State.Mail.All.Count, Is.EqualTo(letters + 1),
                "A second skip must not post a second letter.");
        }

        [Test]
        public void TheTourGivingItFirstLeavesNothingToHandOver()
        {
            var simulation = Company();

            // The way the tour does it: walk to the step that offers the favour.
            Assert.That(simulation.State.Guide.GrantGiftsUpTo(GuideScript.Steps.Count - 1), Is.True);

            Assert.That(simulation.TryGiveTheSkippedFavour(), Is.False,
                "He does not owe it twice.");
        }

        [Test]
        public void TheFavourStillBuysANodeWithNoPointsAtAll()
        {
            var simulation = Company();
            simulation.TryGiveTheSkippedFavour();

            Assert.That(simulation.State.ResearchPoints, Is.Zero,
                "A company on day one has none, which is the whole reason the favour exists.");

            var node = ResearchTree.All[0].Id;

            foreach (var standing in simulation.ResearchBoard())
            {
                if (!standing.CanStart)
                {
                    continue;
                }

                node = standing.Node.Id;
                break;
            }

            Assert.That(simulation.TryStartResearch(node, out var why), Is.True, why);

            Assert.That(simulation.State.Guide.FreeResearchOwed, Is.False,
                "And it is spent, so the second node is paid for like everybody else's.");
        }

        [Test]
        public void TheLetterReadsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (Language language in System.Enum.GetValues(typeof(Language)))
                {
                    Loc.Current = language;

                    Assert.That(Loc.T("guide.favour.subject"),
                        Is.Not.EqualTo("guide.favour.subject"), language.ToString());

                    Assert.That(Loc.T("guide.favour.body"),
                        Is.Not.EqualTo("guide.favour.body"), language.ToString());
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
