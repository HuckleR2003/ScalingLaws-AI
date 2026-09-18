using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
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
        /// The little window into the place is for places that have an inside worth showing. A tax
        /// office is a pin with an errand attached, not a property.
        /// </summary>
        [Test]
        public void OnlyPlacesWithAnInsideCarryThePreview()
        {
            Assert.That(PreviewShows(Site("office.loft")), Is.True, "An office should offer its inside.");
            Assert.That(PreviewShows(Site("server.riverworks.small")), Is.True, "A hall should offer its inside.");
            Assert.That(PreviewShows(Site("civic.taxoffice")), Is.False, "The tax office has no inside to show.");
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
