using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Who the rivals are, and the three that come apart.
    ///
    /// **The point of the arcs is that the player learns from somebody else's mistake.** A field
    /// where every rival is a success story teaches nothing and makes the player's own struggle look
    /// like a personal failing. Three of these companies fall over in front of them, for reasons the
    /// player is themselves exposed to, and these tests hold that it actually happens rather than
    /// being a paragraph nobody reads.
    /// </summary>
    public sealed class LabDossierTests
    {
        private static GameDate On(int y, int m, int d) => GameDate.FromCalendar(y, m, d);

        // ---- every lab is described ------------------------------------------------------------

        [Test]
        public void EveryLabOnTheBoardHasADossier()
        {
            foreach (CompetitorId lab in System.Enum.GetValues(typeof(CompetitorId)))
            {
                if (lab == CompetitorId.None)
                {
                    continue;
                }

                Assert.IsTrue(LabDossiers.TryGet(lab, out var dossier),
                    $"{lab} appears on the ranking board with nothing behind it. Clicking the row "
                    + "would open nothing.");

                Assert.IsNotEmpty(dossier.Name);
                Assert.IsNotEmpty(dossier.Positioning, $"{lab} has no reason to exist written down.");
                Assert.IsNotEmpty(dossier.Story);
                Assert.IsNotEmpty(dossier.Home);
            }
        }

        [Test]
        public void TheNameIsWrittenInExactlyOnePlace()
        {
            // CompetitorCatalog.NameOf used to be a switch, and before that there were two copies of
            // it. Two copies of a name list is how a lab is called one thing on the board and
            // another in the news.
            foreach (var dossier in LabDossiers.All)
            {
                Assert.AreEqual(dossier.Name, CompetitorCatalog.NameOf(dossier.Competitor));
            }
        }

        [Test]
        public void NobodyIsFoundedAfterTheirOwnFirstChapter()
        {
            foreach (var dossier in LabDossiers.All)
            {
                foreach (var chapter in dossier.Chapters)
                {
                    Assert.IsTrue(chapter.On.IsOnOrAfter(dossier.Founded),
                        $"{dossier.Name} has a chapter dated before the company existed.");
                }
            }
        }

        [Test]
        public void ChaptersAreInOrder()
        {
            foreach (var dossier in LabDossiers.All)
            {
                for (var index = 1; index < dossier.Chapters.Length; index++)
                {
                    Assert.IsTrue(
                        dossier.Chapters[index].On.IsOnOrAfter(dossier.Chapters[index - 1].On),
                        $"{dossier.Name} tells its story out of order, and the card prints them "
                        + "newest first on the assumption that it does not.");
                }
            }
        }

        // ---- the future is never shown ----------------------------------------------------------

        [Test]
        public void ADossierNeverShowsWhatHasNotHappenedYet()
        {
            Assert.IsTrue(LabDossiers.TryGet(CompetitorId.InflectionAi, out var inflection));

            // The company is hollowed out on 2024-03-19. On the day before, a player has no way of
            // knowing that, and a card that said so would turn the whole field into a spoiler.
            foreach (var chapter in inflection.ChaptersBy(On(2024, 3, 18)))
            {
                Assert.AreNotEqual(LabChapterKind.Exit, chapter.Kind,
                    "The card is showing the end of a company twelve days before it happens.");
            }

            var sawExit = false;
            foreach (var chapter in inflection.ChaptersBy(On(2024, 3, 20)))
            {
                sawExit |= chapter.Kind == LabChapterKind.Exit;
            }

            Assert.IsTrue(sawExit, "And the day after, it has to be there.");
        }

        [Test]
        public void AnythingPastTheKnownTimelineIsMarkedAsAProjection()
        {
            // The same honesty flag the hardware and competitor tables carry. The roster is real
            // companies and the difference between what was announced and where the game thinks it
            // goes has to survive contact with a player who was there for it.
            var lastKnown = On(2026, 2, 1);

            foreach (var dossier in LabDossiers.All)
            {
                foreach (var chapter in dossier.Chapters)
                {
                    if (chapter.On.IsOnOrAfter(lastKnown))
                    {
                        Assert.IsTrue(chapter.IsProjection,
                            $"{dossier.Name} states this as documented fact and it is dated past "
                            + $"what anybody knows: {chapter.Headline}");
                    }
                }
            }
        }

        // ---- the arcs actually happen -----------------------------------------------------------

        [Test]
        public void ThreeCompaniesVisiblyComeApart()
        {
            var falling = new List<string>();

            foreach (var dossier in LabDossiers.All)
            {
                foreach (var chapter in dossier.Chapters)
                {
                    if (chapter.Kind == LabChapterKind.Setback || chapter.Kind == LabChapterKind.Exit)
                    {
                        falling.Add(dossier.Name);
                        break;
                    }
                }
            }

            Assert.GreaterOrEqual(falling.Count, 3,
                "A field where every rival is a success story makes the player's own struggle look "
                + $"like a personal failing. Falling: {string.Join(", ", falling)}");
        }

        [Test]
        public void TheBrandCollapsesFasterThanTheCapabilityDoes()
        {
            // This is the shape of a company in trouble and it is the thing the player can watch
            // without being told: the models keep working and the name stops being worth anything.
            var early = Best(CompetitorId.StabilityAi, On(2023, 1, 1));
            var late = Best(CompetitorId.StabilityAi, On(2025, 1, 1));

            Assert.Greater(late.Capability, early.Capability,
                "The models they already trained do not get worse.");

            Assert.Less(late.BrandStrength, early.BrandStrength * 0.6,
                "And the name does. If both fall together this reads as a lab that went quiet "
                + "rather than one that lost its community.");
        }

        [Test]
        public void TheChallengerReachesTheFrontierAndThenStopsDeadTheSameMonth()
        {
            var peak = Best(CompetitorId.InflectionAi, On(2024, 3, 10));
            var after = Best(CompetitorId.InflectionAi, On(2024, 4, 1));

            Assert.Greater(peak.BrandStrength, 0.2, "It was a real challenger in March 2024.");
            Assert.AreEqual(peak.Capability, after.Capability, 1e-9,
                "Nothing new ever ships again, which is what being hollowed out looks like from "
                + "outside: the product still works.");

            Assert.Less(after.BrandStrength, peak.BrandStrength * 0.3,
                "Twelve days, and the name is worth almost nothing.");
        }

        [Test]
        public void TheSurvivorIsNeverTheBestAndIsAlwaysStillThere()
        {
            foreach (var year in new[] { 2023, 2024, 2025 })
            {
                var date = On(year, 12, 1);
                var cohere = Best(CompetitorId.Cohere, date);
                var frontier = CompetitorCatalog.FrontierCapabilityOn(date);

                Assert.Greater(cohere.Capability, 0.0, $"Still shipping in {year}.");
                Assert.Less(cohere.Capability, frontier,
                    $"The survivor is not supposed to win in {year}. The point is that it does not "
                    + "have to.");
            }
        }

        [Test]
        public void TheEuropeanBidIsFundedAndThenFallsBehindAnyway()
        {
            var funded = Best(CompetitorId.AlephAlpha, On(2024, 1, 1));
            var later = Best(CompetitorId.AlephAlpha, On(2026, 1, 1));

            Assert.Greater(later.Capability, funded.Capability, "It never actually stops working.");

            var gapThen = CompetitorCatalog.FrontierCapabilityOn(On(2024, 1, 1)) - funded.Capability;
            var gapLater = CompetitorCatalog.FrontierCapabilityOn(On(2026, 1, 1)) - later.Capability;

            Assert.Greater(gapLater, gapThen * 1.5,
                "Money bought them a seat and the frontier tripled past them anyway. If the gap "
                + "does not widen, the scale gap is a paragraph rather than a mechanic.");
        }

        // ---- it reaches the player ---------------------------------------------------------------

        [Test]
        public void TheFallsArriveInTheNewsWithoutPayingForAnything()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));

            Assert.IsFalse(simulation.State.IsMember(IntelTier.KnownWords),
                "This has to work for a player who never buys a membership.");

            // Two and a half years, which covers the image lab's lawsuits and the challenger's exit.
            for (var day = 0; day < 900 && !simulation.State.IsBankrupt; day++)
            {
                simulation.Advance(1);
            }

            var scandals = 0;
            var aboutRivals = 0;

            foreach (var item in simulation.State.News.All)
            {
                if (item.IsAboutPlayer)
                {
                    continue;
                }

                aboutRivals++;
                if (item.Section == NewsSection.Scandals)
                {
                    scandals++;
                }
            }

            Assert.Greater(aboutRivals, 0, "The rivals' history never reached the wire at all.");
            Assert.Greater(scandals, 0,
                "Nothing bad ever visibly happened to anybody else, so the player has no reason to "
                + "think safety and cost are things that end companies.");
        }

        [Test]
        public void AChapterFiledAsAProjectionSaysSoInTheStoryItself()
        {
            Assert.IsTrue(LabDossiers.TryGet(CompetitorId.AlephAlpha, out var lab));

            foreach (var chapter in lab.Chapters)
            {
                if (!chapter.IsProjection)
                {
                    continue;
                }

                var item = NewsDesk.FromLabChapter(lab, chapter);
                StringAssert.Contains("Projection", item.Body,
                    "A guess printed in the same type as a fact is the one thing the honesty flag "
                    + "exists to prevent.");

                return;
            }

            Assert.Fail("No projected chapter to check, so this test is checking nothing.");
        }

        private static CompetitorRelease Best(CompetitorId lab, GameDate date)
        {
            foreach (var entry in CompetitorCatalog.BestPerCompetitorOn(date))
            {
                if (entry.Competitor == lab)
                {
                    return entry;
                }
            }

            Assert.Fail($"{lab} has nothing live on {date}.");
            return default;
        }
    }
}
