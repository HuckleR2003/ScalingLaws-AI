using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The wire.
    ///
    /// **This fixture is the ratchet for the largest gap ever found in this project.** Thirty four
    /// kinds of event were being raised every day and drained into a list nothing read. A regulator
    /// could fine the company, cut its reputation and pull its flagship off the market in one morning
    /// and the player was told nothing: the model simply vanished from sale and the income fell. That
    /// does not read as a hard game, it reads as a broken one.
    ///
    /// So the tests here are mostly about **reaching the reader**, not about the prose.
    /// </summary>
    public sealed class NewsTests
    {
        private static CompanySimulation Fresh(uint seed = 500) =>
            new(new CompanyState("Adco", seed));

        private static CompanySimulation Selling(uint seed = 501, int days = 60)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(80.0);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Atlas One", ArchitectureId.DenseTransformer, 48.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            for (var day = 0; day < days; day++)
            {
                simulation.AdvanceDay();
            }

            return simulation;
        }

        // ---- the gap this exists to close --------------------------------------------------------

        [Test]
        public void AnIncidentReachesTheReaderInsteadOfBeingDrained()
        {
            var simulation = Fresh();

            simulation.State.RaiseEvent(new CompanyEvent(
                CompanyEventType.SafetyIncident, simulation.State.Date,
                "A regulator has opened an inquiry. Penalty $4,000,000.", 4_000_000L));

            var scandals = simulation.State.News.In(NewsSection.Scandals, 5);

            Assert.AreEqual(1, scandals.Count,
                "An incident that never reaches the reader is a model vanishing from sale with no "
                + "explanation, which is the bug this whole file exists to prevent.");

            Assert.IsTrue(scandals[0].IsAboutPlayer);
            Assert.AreEqual(NewsWeight.Loud, scandals[0].Weight);
        }

        [Test]
        public void ARivalShippingIsPrintedAndSoIsYourOwnRelease()
        {
            var simulation = Fresh();

            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.RivalReleased,
                simulation.State.Date, "Vena released Orion at capability 51.2."));

            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.ModelReleased,
                simulation.State.Date, "Atlas One is on sale."));

            var premieres = simulation.State.News.In(NewsSection.Premieres, 5);
            Assert.AreEqual(2, premieres.Count, "Both launches belong on the same page, on purpose.");

            var mine = 0;
            foreach (var story in premieres)
            {
                if (story.IsAboutPlayer)
                {
                    mine++;
                }
            }

            Assert.AreEqual(1, mine, "Exactly one of those two was the player's.");
        }

        /// <summary>
        /// The filter is the job. A feed that prints everything is the drained list with extra steps.
        /// </summary>
        [Test]
        public void TheEventsThatAreOnlyTheirOwnClickComingBackAreNotPrinted()
        {
            var simulation = Fresh();

            foreach (var quiet in new[]
                     {
                         CompanyEventType.TrainingStarted,
                         CompanyEventType.HardwareOrdered,
                         CompanyEventType.StaffHired,
                         CompanyEventType.SkillLevelled,
                         CompanyEventType.IntelReceived
                     })
            {
                simulation.State.RaiseEvent(new CompanyEvent(quiet, simulation.State.Date, "x"));
            }

            Assert.AreEqual(0, simulation.State.News.Count,
                "Reporting the player to themselves buries the events they did not cause.");
        }

        [Test]
        public void TheBannerShowsTheLoudestRecentStoryRatherThanTheLastOne()
        {
            var simulation = Fresh();

            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.SafetyIncident,
                simulation.State.Date, "A regulator has opened an inquiry."));

            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.OfficeMoved,
                simulation.State.Date, "Moved to a larger floor."));

            Assert.IsTrue(simulation.State.News.TryGetHeadline(out var headline));
            Assert.AreEqual(NewsWeight.Loud, headline.Weight,
                "On a busy day the last event is a routine one. A banner that shows whatever happened "
                + "most recently puts an office move on top of a regulatory inquiry.");
        }

        [Test]
        public void TheFeedIsCappedSoAFifteenYearCampaignDoesNotCarryThousandsOfStrings()
        {
            var simulation = Fresh();

            for (var filed = 0; filed < NewsFeed.Capacity + 40; filed++)
            {
                simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.RivalReleased,
                    simulation.State.Date, $"Lab {filed} shipped."));
            }

            Assert.AreEqual(NewsFeed.Capacity, simulation.State.News.Count);
        }

        // ---- the paid desks ------------------------------------------------------------------------

        [Test]
        public void NothingIsPaidForAndTheRightHandColumnIsEmpty()
        {
            var simulation = Selling();

            foreach (var section in new[]
                     { NewsSection.TotalTrueNews, NewsSection.ItSpy, NewsSection.EventHunter })
            {
                Assert.IsEmpty(simulation.State.News.In(section, 5),
                    $"{section} filed something to a company paying nobody.");
            }
        }

        [Test]
        public void EveryMembershipIsBilledAndNotJustTheDearestOne()
        {
            var simulation = Fresh();
            simulation.SetIntelSubscription(IntelTier.NationalPress, true);
            simulation.SetIntelSubscription(IntelTier.TrendSearch, true);

            var expected = IntelligenceService.MonthlyRetainerUsd(IntelTier.NationalPress)
                + IntelligenceService.MonthlyRetainerUsd(IntelTier.TrendSearch);

            Assert.AreEqual(expected, simulation.MonthlyIntelRetainerUsd(),
                "Two retainers is two invoices. Billing only the best one would make the cheap desks "
                + "free once the dear one is held.");
        }

        [Test]
        public void MembershipsAreIndependentSoTheCheapOneCanBeHeldWithoutTheDearOne()
        {
            var simulation = Fresh();
            simulation.SetIntelSubscription(IntelTier.NationalPress, true);

            Assert.IsTrue(simulation.State.IsMember(IntelTier.NationalPress));
            Assert.IsFalse(simulation.State.IsMember(IntelTier.TrendSearch),
                "Holding the cheap desk must not imply the dear one, or Event Hunter's double gate "
                + "cannot exist.");

            simulation.SetIntelSubscription(IntelTier.NationalPress, false);
            Assert.IsFalse(simulation.State.IsMember(IntelTier.NationalPress),
                "A retainer the player can start and not stop is a subscription trap.");
        }

        [Test]
        public void PayingKnownWordsFillsItSpyWithDifferentLabsRatherThanTheSameOne()
        {
            var simulation = Selling(502, 5);
            simulation.SetIntelSubscription(IntelTier.KnownWords, true);

            for (var day = 0; day < NewsDesk.DossierIntervalDays * 4 + 4; day++)
            {
                simulation.AdvanceDay();
            }

            var dossiers = simulation.State.News.In(NewsSection.ItSpy, 10);
            Assert.Greater(dossiers.Count, 1, "Four intervals should file more than one dossier.");

            var subjects = new HashSet<string>();
            foreach (var story in dossiers)
            {
                subjects.Add(story.Headline);
            }

            Assert.Greater(subjects.Count, 1,
                "The desk works through the field in order. Reporting one lab repeatedly would leave "
                + "the lab quietly waiting out a hardware cycle unmentioned, which is the single most "
                + "useful thing this desk can find.");
        }

        /// <summary>
        /// The rule the whole news layer runs on: it reports, it never computes a second opinion.
        /// </summary>
        [Test]
        public void ADossierCannotContradictTheMarketItIsDescribing()
        {
            var simulation = Selling(503, 5);
            simulation.SetIntelSubscription(IntelTier.KnownWords, true);

            for (var day = 0; day < NewsDesk.DossierIntervalDays + 2; day++)
            {
                simulation.AdvanceDay();
            }

            var dossiers = simulation.State.News.In(NewsSection.ItSpy, 1);
            Assert.IsNotEmpty(dossiers);

            var breakdown = simulation.MarketByType();
            var named = false;

            for (var owner = 1; owner < breakdown.OwnerNames.Count; owner++)
            {
                if (dossiers[0].Headline.Contains(breakdown.OwnerNames[owner]))
                {
                    named = true;
                }
            }

            Assert.IsTrue(named,
                $"'{dossiers[0].Headline}' is about a lab the market has never heard of.");
        }

        // ---- the screen ------------------------------------------------------------------------------

        private static List<string> Words(VisualElement root)
        {
            var found = new List<string>();

            void Walk(VisualElement element)
            {
                switch (element)
                {
                    case Label label when !string.IsNullOrEmpty(label.text):
                        found.Add(label.text);
                        break;
                    case Button button when !string.IsNullOrEmpty(button.text):
                        found.Add(button.text);
                        break;
                }

                foreach (var child in element.Children())
                {
                    Walk(child);
                }
            }

            Walk(root);
            return found;
        }

        private static bool Says(VisualElement root, string fragment) =>
            Words(root).Exists(text => text.Contains(fragment));

        [Test]
        public void TheScreenLaysOutAllSixSections()
        {
            var simulation = Selling();
            var screen = new NewsScreen(simulation, (_, _) => { });
            screen.Refresh();

            foreach (var heading in new[]
                     { "LATEST", "SCANDALS", "PREMIERES", "TOTAL TRUE NEWS", "IT SPY", "EVENT HUNTER" })
            {
                Assert.IsTrue(Says(screen.Root, heading), $"{heading} is not on the page.");
            }
        }

        /// <summary>
        /// The author's own specification, and the awkward one: National Press sells Event Hunter and
        /// the section still will not open. A player who has paid and cannot read it is owed the
        /// reason in plain words rather than a generic padlock.
        /// </summary>
        [Test]
        public void EventHunterNamesTrendSearchEvenWhenNationalPressIsAlreadyPaid()
        {
            var simulation = Selling();
            simulation.SetIntelSubscription(IntelTier.NationalPress, true);

            var screen = new NewsScreen(simulation, (_, _) => { });
            screen.Refresh();

            Assert.IsTrue(Says(screen.Root, "Requires TrendSearch Team membership"),
                "Event Hunter has to say which membership is missing, and it is not the one already "
                + "being paid for.");

            Assert.IsTrue(Says(screen.Root, "National Press is paid and it is not enough on its own"),
                "And it has to acknowledge the money already spent, or it reads as a bug.");
        }

        [Test]
        public void PayingBothOpensEventHunter()
        {
            var simulation = Selling();
            simulation.SetIntelSubscription(IntelTier.NationalPress, true);
            simulation.SetIntelSubscription(IntelTier.TrendSearch, true);

            var screen = new NewsScreen(simulation, (_, _) => { });
            screen.Refresh();

            Assert.IsFalse(Says(screen.Root, "Requires TrendSearch Team membership"),
                "A member who has paid for both is still being told to pay.");
        }

        [Test]
        public void TheScreenOffersAWayToJoinAndAWayToLeave()
        {
            var simulation = Selling();
            var joined = new List<IntelTier>();

            var screen = new NewsScreen(simulation, (tier, on) =>
            {
                if (on)
                {
                    joined.Add(tier);
                }
            });

            screen.Refresh();
            Assert.IsTrue(Says(screen.Root, "JOIN KNOWNWORDS"), "No way in.");

            simulation.SetIntelSubscription(IntelTier.KnownWords, true);
            screen.Refresh();
            Assert.IsTrue(Says(screen.Root, "CANCEL KNOWNWORDS"), "No way out.");
        }

        [Test]
        public void OpeningTheScreenClearsTheUnreadCount()
        {
            var simulation = Fresh();
            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.RivalReleased,
                simulation.State.Date, "Vena shipped."));

            Assert.AreEqual(1, simulation.State.News.Unread);

            new NewsScreen(simulation, (_, _) => { }).Refresh();
            Assert.AreEqual(0, simulation.State.News.Unread);
        }

        [Test]
        public void TheBannerNamesTheStoryAndOffersTheWayIn()
        {
            var simulation = Fresh();
            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.SafetyIncident,
                simulation.State.Date, "A regulator has opened an inquiry."));

            var opened = false;
            var banner = new NewsBanner(() => simulation.State.News, () => opened = true);
            banner.Refresh();

            Assert.IsTrue(Says(banner.Root, "ABOUT YOU"),
                "The company's own trouble gets called what it is.");

            Assert.IsTrue(Says(banner.Root, "SEE NEWS"));
            Assert.IsFalse(opened, "Building the banner must not navigate on its own.");
        }

        [Test]
        public void TheBannerSaysSoWhenNothingHasHappened()
        {
            var simulation = Fresh();
            var banner = new NewsBanner(() => simulation.State.News, () => { });

            Assert.DoesNotThrow(() => banner.Refresh(), "Day one throws.");
            Assert.IsTrue(Says(banner.Root, "Nothing has happened yet."));
        }

        // ---- persistence -------------------------------------------------------------------------------

        [Test]
        public void TheNewsAndTheMembershipsSurviveASave()
        {
            var simulation = Selling(504, 10);
            simulation.SetIntelSubscription(IntelTier.KnownWords, true);
            simulation.State.RaiseEvent(new CompanyEvent(CompanyEventType.SafetyIncident,
                simulation.State.Date, "A regulator has opened an inquiry."));

            var before = simulation.State.News.Count;

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(before, restored.News.Count, "Losing the paper on load is losing history.");
            Assert.IsTrue(restored.IsMember(IntelTier.KnownWords));
            Assert.IsFalse(restored.IsMember(IntelTier.TrendSearch));

            Assert.IsTrue(restored.News.TryGetHeadline(out var headline));
            Assert.AreEqual(NewsWeight.Loud, headline.Weight);
        }

        [Test]
        public void AnOlderSaveKeepsTheOneDeskItWasPayingForAndNothingElse()
        {
            var data = new SaveData { version = 22, intelSubscription = (int)IntelTier.KnownWords };
            var upgraded = SaveMigration.UpgradeV22ToV23(data);

            Assert.AreEqual(23, upgraded.version);
            Assert.AreEqual(1, upgraded.memberships.Count,
                "A v22 company held exactly one subscription. Granting the cheaper desks too would "
                + "hand the player retainers they never bought and then invoice them for both.");

            Assert.AreEqual((int)IntelTier.KnownWords, upgraded.memberships[0]);
            Assert.IsEmpty(upgraded.news,
                "A v22 file has no record of what was announced, so a back catalogue would be stories "
                + "about events that may never have happened.");
        }

        [Test]
        public void AnOlderSaveWithNoDeskJoinsNothing()
        {
            var data = new SaveData { version = 22, intelSubscription = (int)IntelTier.PublicNews };
            Assert.IsEmpty(SaveMigration.UpgradeV22ToV23(data).memberships);
        }
    }
}
