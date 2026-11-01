using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What the card that opens on a clicked place actually prints.
    ///
    /// **The point of these is the line between a real number and a dash.** Offices and the two power
    /// plants have catalogs behind them, so their rows carry the figures the simulation would charge;
    /// everything else has to keep showing a dash, because a number in the interface that no catalog
    /// computes is an economy invented in the UI. Both halves are checked here.
    /// </summary>
    public sealed class MapSiteCardTests
    {
        private static MapSiteDefinition Site(string id) =>
            MapSiteCatalog.All.First(site => site.Id == id);

        private static List<string> ValuesOn(MapSiteDefinition site)
        {
            var card = new MapSiteCard(() => { });
            card.Show(site);

            return card.Query<Label>(className: "site-card__figure-value")
                .ToList()
                .Select(label => label.text)
                .ToList();
        }

        private static List<string> LabelsOn(MapSiteDefinition site)
        {
            var card = new MapSiteCard(() => { });
            card.Show(site);

            return card.Query<Label>(className: "site-card__figure-label")
                .ToList()
                .Select(label => label.text)
                .ToList();
        }

        [Test]
        public void AnOfficeShowsTheRentAndDesksItsCatalogHolds()
        {
            var office = OfficeCatalog.Get(OfficeTier.Loft);
            var values = ValuesOn(Site("office.loft"));

            Assert.That(values.Any(value => value.Contains(UiFormat.Money(office.MonthlyRentUsd))), Is.True,
                $"The Loft card should print its monthly rent. Printed: {string.Join(" | ", values)}");

            Assert.That(values, Contains.Item(office.Desks.ToString()),
                $"The Loft card should print its desk count. Printed: {string.Join(" | ", values)}");

            Assert.That(values, Has.None.EqualTo("—"),
                "An office whose catalog holds every figure should have no dashes left.");
        }

        [Test]
        public void APowerPlantShowsItsCapexAndBuildTime()
        {
            var plant = PowerPlantCatalog.Get(PowerPlantSite.Coastal);
            var values = ValuesOn(Site("plant.coastal"));

            Assert.That(values.Any(value => value.Contains(UiFormat.Money(plant.CapexUsd))), Is.True,
                $"The coastal plant should print its capex. Printed: {string.Join(" | ", values)}");

            Assert.That(values, Contains.Item(UiFormat.Days(plant.BuildDays)),
                $"The coastal plant should print its build time. Printed: {string.Join(" | ", values)}");
        }

        /// <summary>
        /// A venue says what a stand costs and how many people will see it.
        ///
        /// **Both numbers were dashes until 2026-11-01**, while `EventVenueCatalog` held the
        /// visitors and the prestige and derived the hire from them. Found in three seconds of
        /// video a tester sent with the words "it doesnt work", which were about the button
        /// underneath rather than these two rows. The rows were the second fault on the card.
        ///
        /// Asserted against the catalog rather than against 39,648, so changing the venue changes
        /// the expected figure instead of breaking a test that has memorised one.
        /// </summary>
        [Test]
        public void AVenueShowsWhatAStandCostsAndHowManyWillSeeIt()
        {
            var venue = EventVenueCatalog.Get(EventVenueId.MediaPlaza);
            var values = ValuesOn(Site("event.mediaplaza"));

            Assert.That(values, Contains.Item(UiFormat.Money(venue.SmallStandCostUsd)),
                $"Media Plaza should print the hire its own catalog derives. Printed: {string.Join(" | ", values)}");

            Assert.That(values, Contains.Item(UiFormat.Count(venue.AverageVisitors)),
                $"Media Plaza should print its audience. Printed: {string.Join(" | ", values)}");

            Assert.That(values.Any(value => value == "—"), Is.False,
                $"Nothing on a wired venue should still be a dash. Printed: {string.Join(" | ", values)}");
        }

        /// <summary>
        /// The five halls to rent in River Works have no prices in any catalog yet — that is the open
        /// question in Fix 24's notes, and until it is answered the card must say nothing instead.
        /// </summary>
        [Test]
        public void AServerHallStillShowsDashesBecauseNoCatalogPricesIt()
        {
            var values = ValuesOn(Site("server.riverworks.small"));

            Assert.That(values, Is.Not.Empty, "The rows themselves should still be there.");
            Assert.That(values.All(value => value == "—"), Is.True,
                $"No figure here is computed by a catalog. Printed: {string.Join(" | ", values)}");
        }

        [Test]
        public void EveryFigureLabelIsInThePhraseBook()
        {
            foreach (var site in MapSiteCatalog.All)
            {
                foreach (var label in LabelsOn(site))
                {
                    Assert.That(label, Is.Not.Empty, $"{site.Id} has a figure with no label.");
                    Assert.That(label, Does.Not.StartWith("map.card."),
                        $"{site.Id} shows a raw phrase-book key: {label}");
                }
            }
        }

        /// <summary>
        /// The little window into the place is drawn only where there is a still to put in it.
        ///
        /// **This used to assert the opposite and it was right at the time.** The empty frame was
        /// built early on purpose, so interior shots could be dropped in as they were rendered.
        /// A tester read the empty box with the word PREVIEW in it as something left over from an
        /// earlier build, and the author agreed and asked for it gone on 2026-10-31.
        ///
        /// The rule rather than today's emptiness: the frame appears where the kind of place has
        /// an inside **and** a still exists for it. That is true now, with no stills at all, and
        /// stays true on the day the first one lands in Resources/Map/Interiors/.
        /// </summary>
        [Test]
        public void APreviewIsDrawnOnlyWhereThereIsAStillToShow()
        {
            var drawn = 0;

            foreach (var site in MapSiteCatalog.All)
            {
                var hasInside = site.Kind is MapSiteKind.OfficeLease or MapSiteKind.ServerFacility;
                var still = Resources.Load<Texture2D>($"Map/Interiors/{site.Id}") != null;
                var expected = hasInside && still;

                Assert.That(PreviewShows(site), Is.EqualTo(expected),
                    site.Id + " draws its preview frame when it should not, or the other way "
                    + "round. Kind has an inside: " + hasInside + ". A still exists: " + still + ".");

                if (expected)
                {
                    drawn++;
                }
            }

            Debug.Log("[preview] " + drawn + " of " + MapSiteCatalog.All.Count
                      + " places have an interior still to show.");
        }

        private static bool PreviewShows(MapSiteDefinition site)
        {
            var card = new MapSiteCard(() => { });
            card.Show(site);

            var preview = card.Q<VisualElement>(className: "site-card__preview");

            Assert.That(preview, Is.Not.Null, "The card should always build the preview frame.");
            return preview.style.display.value == DisplayStyle.Flex;
        }
    }
}
