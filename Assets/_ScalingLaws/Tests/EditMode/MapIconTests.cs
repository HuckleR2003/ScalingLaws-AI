using System;
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
    /// The city map's icons: that there is one for every category and every kind of site, and that
    /// the panels actually show them.
    ///
    /// A missing icon falls back to a plain colour on screen, which is right for a player and
    /// silent for everyone else — so a file renamed, or a ninth category added without a picture,
    /// fails here by name instead.
    /// </summary>
    public sealed class MapIconTests
    {
        [Test]
        public void EveryCategoryHasItsIcon()
        {
            AllPresent(Enum.GetValues(typeof(MapCategory)).Cast<MapCategory>()
                .Select(category => (category.ToString(), MapIcons.FileFor(category), MapIcons.For(category))));
        }

        [Test]
        public void EveryKindOfSiteHasItsIcon()
        {
            AllPresent(Enum.GetValues(typeof(MapSiteKind)).Cast<MapSiteKind>()
                .Select(kind => (kind.ToString(), MapIcons.FileFor(kind), MapIcons.For(kind))));
        }

        [Test]
        public void TheSiteCardShowsTheIconForTheKindOfPlace()
        {
            var card = new MapSiteCard(() => { });

            foreach (var site in MapSiteCatalog.All)
            {
                card.Show(site);

                var icon = card.Q(className: "site-card__icon");
                Assert.That(icon.style.backgroundImage.value.texture, Is.SameAs(MapIcons.For(site.Kind)),
                    $"{site.DisplayName} ({site.Kind}) opened a card without its icon.");
            }
        }

        [Test]
        public void EveryLegendRowShowsItsCategoryIcon()
        {
            var legend = new MapLegendPanel(new MapFilterState());
            var swatches = legend.Query(className: "map-legend__swatch").ToList();

            Assert.That(swatches, Has.Count.EqualTo(MapCategoryPalette.All.Length));

            for (var index = 0; index < swatches.Count; index++)
            {
                Assert.That(swatches[index].style.backgroundImage.value.texture,
                    Is.SameAs(MapIcons.For(MapCategoryPalette.All[index])),
                    $"The {MapCategoryPalette.All[index]} row in the legend shows no icon.");
            }
        }

        private static void AllPresent(IEnumerable<(string Name, string File, Texture2D Icon)> icons)
        {
            var problems = new List<string>();

            foreach (var (name, file, icon) in icons)
            {
                if (file == null)
                {
                    problems.Add($"{name}: no file name in MapIcons");
                }
                else if (icon == null)
                {
                    problems.Add($"{name}: Resources/{MapIcons.Folder}{file}.png is missing");
                }
                else if (icon.width != 256 || icon.height != 256)
                {
                    problems.Add($"{name}: {file}.png is {icon.width}x{icon.height}, the set is 256x256");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
