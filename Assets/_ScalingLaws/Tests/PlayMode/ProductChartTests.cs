using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// The chart in a product banner is about that product.
    ///
    /// **Reported by Francisco on 2026-10-17: the revenue graph on a model counts grants and
    /// loans.** It did. The lead banner was handed the company's daily net, which sums every cash
    /// line in the ledger, so a state grant, a loan drawdown, the payroll and a tax demand were all
    /// drawn as columns of one model's month, inside a card with that model's name at the top of it.
    ///
    /// This is the third correction of that one card and the reason it needed three is that each of
    /// the first two fixed the part somebody had looked at. The figures beside the chart were moved
    /// off the company's month in September; the follower banners were given their own model's
    /// revenue a fortnight before that, for this exact reason, which left the lead the only banner
    /// in the game still reporting the books. **Nothing measured which series a banner was given**,
    /// so both repairs could land without closing the fault.
    ///
    /// So the claim here is deliberately not about the lead banner. It is about every chart on the
    /// screen at once: each one has to be some marketed model's own revenue. A guard written about
    /// the lead alone would have gone quiet the next time the corner grew a third kind of banner.
    /// </summary>
    public sealed class ProductChartTests
    {
        /// <summary>Days played after the campaign is assembled, so the rings hold real takings.</summary>
        private const int DaysPlayed = 45;

        private static VisualElement Root =>
            Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        private static List<ProductSparkline> Charts()
        {
            var charts = new List<ProductSparkline>();
            foreach (var element in Root.Query(className: "mb-chart").ToList())
            {
                if (element is ProductSparkline chart)
                {
                    charts.Add(chart);
                }
            }

            return charts;
        }

        private static List<long> Copy(IReadOnlyList<long> series)
        {
            var copy = new List<long>(series.Count);
            foreach (var value in series)
            {
                copy.Add(value);
            }

            return copy;
        }

        private static bool Same(IReadOnlyList<long> left, IReadOnlyList<long> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerator OpenASellingCompany(GameShell shell)
        {
            TabProofCampaign.Furnish(shell.Simulation);

            // **Played rather than assembled, for this one fixture.** `Furnish` fills the screens
            // without ticking the clock, so every revenue ring is empty, and a comparison between
            // two empty sequences passes while measuring nothing.
            shell.Simulation.Advance(DaysPlayed);

            Assert.That(shell.OpenScreenByName("Site"), Is.True, "no screen called Site");

            yield return null;
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator OpenTheGame()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            // The phone mounts on the panel root and sits over the corner being measured.
            foreach (var over in Root.Query(className: "phone").ToList())
            {
                over.RemoveFromHierarchy();
            }
        }

        [UnityTest]
        public IEnumerator EveryChartInTheCornerIsOneModelsOwnRevenue()
        {
            var shell = Object.FindFirstObjectByType<GameShell>();
            yield return OpenASellingCompany(shell);

            var rings = new List<IReadOnlyList<long>>();
            var earned = false;

            foreach (var record in shell.Simulation.MarketedModels())
            {
                var ring = record.Model.RecentRevenueUsd;
                rings.Add(ring);

                foreach (var day in ring)
                {
                    earned |= day != 0L;
                }
            }

            Assert.That(rings, Is.Not.Empty,
                "Nothing is on sale in this campaign, so there is no product chart to check.");

            // Without this the comparison below is a row of zeros against a row of zeros.
            Assert.That(earned, Is.True,
                "No model took anything in the days played, so every ring is empty and this "
                + "fixture would pass against a chart drawn from the ledger.");

            var charts = Charts();

            Assert.That(charts, Is.Not.Empty, "The corner drew no product chart at all.");

            foreach (var chart in charts)
            {
                var matched = false;
                foreach (var ring in rings)
                {
                    matched |= Same(chart.Values, ring);
                }

                Assert.That(matched, Is.True,
                    "A banner chart holds a series that is not any model's own revenue. The lead "
                    + "banner used to be handed the company's daily net here, which is the books "
                    + "rather than the product: grants, loan drawdowns and the payroll included.");
            }
        }

        /// <summary>
        /// The sharp end of the report, and the half a shape comparison cannot catch.
        ///
        /// Money arriving from somewhere that is not a customer must not move a product's chart at
        /// all. Under the old reading a grant landed on today's ledger line and the next refresh
        /// drew it, so a company that took state money on a Tuesday showed its model having an
        /// extraordinary Tuesday.
        /// </summary>
        [UnityTest]
        public IEnumerator AGrantDoesNotShowUpInAModelsChart()
        {
            var shell = Object.FindFirstObjectByType<GameShell>();
            yield return OpenASellingCompany(shell);

            var before = new List<List<long>>();
            foreach (var chart in Charts())
            {
                before.Add(Copy(chart.Values));
            }

            Assert.That(before, Is.Not.Empty, "The corner drew no product chart at all.");

            shell.Simulation.State.PostCash(LedgerLine.GrantAward, 900_000_000L);

            // Opening the tab that is already open is the day rollover, which is the path that
            // redraws the banner. Same measurement `DayRolloverTests` is built on.
            shell.OpenScreenByName("Site");

            yield return null;
            yield return null;

            var after = Charts();

            Assert.That(after.Count, Is.EqualTo(before.Count),
                "The corner gained or lost a banner over a grant arriving.");

            for (var index = 0; index < after.Count; index++)
            {
                Assert.That(Same(after[index].Values, before[index]), Is.True,
                    "A product chart changed when $900M of grant money arrived. A grant is not "
                    + "something anybody bought, and this card is about one model.");
            }
        }
    }
}
