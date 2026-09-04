using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The baked world, and whether it agrees with the catalog that uses it.
    ///
    /// **The asset and the enum are two files that have to say the same thing.** The bake maps ISO
    /// codes to `Country` values by hand, because Natural Earth has no idea this game exists. A
    /// value changed on one side and not the other produces a country that is drawn and cannot be
    /// picked, or one that is offered in a list and does not exist on the map, and neither throws.
    /// </summary>
    public sealed class WorldMapTests
    {
        /// <summary>
        /// The asset is there and readable.
        ///
        /// Its own test, because everything below would pass vacuously on an empty list: a loader
        /// that returns nothing makes "every country is in the right region" true.
        /// </summary>
        [Test]
        public void TheMapIsActuallyThere()
        {
            Assert.Greater(WorldShapes.All.Count, 100,
                "The baked map is missing or unreadable. Run Tools/bake_world_map.py.");

            Assert.Greater(WorldShapes.Aspect, 0.2f);
            Assert.Less(WorldShapes.Aspect, 1.0f,
                "A world map taller than it is wide means the projection or the normalisation is "
                + "wrong.");
        }

        /// <summary>
        /// Every country the game offers is on the map, and each one only once.
        ///
        /// The failure this catches is silent in both directions: a country in the catalog with no
        /// shape cannot be clicked, and a duplicate means two outlines answer to one name so the
        /// highlight lands on whichever the loop reaches first.
        /// </summary>
        [Test]
        public void EveryCountryInTheCatalogIsOnTheMapExactlyOnce()
        {
            var counts = new Dictionary<Country, int>();

            foreach (var shape in WorldShapes.All)
            {
                if (shape.Member == Country.None)
                {
                    continue;
                }

                counts.TryGetValue(shape.Member, out var seen);
                counts[shape.Member] = seen + 1;
            }

            var missing = new List<string>();
            var doubled = new List<string>();

            foreach (var definition in WorldRegionCatalog.AllCountries)
            {
                if (!counts.TryGetValue(definition.Country, out var seen))
                {
                    missing.Add(definition.DisplayName);
                }
                else if (seen > 1)
                {
                    doubled.Add($"{definition.DisplayName} x{seen}");
                }
            }

            CollectionAssert.IsEmpty(missing,
                "Offered in the creator, absent from the map: " + string.Join(", ", missing));

            CollectionAssert.IsEmpty(doubled,
                "Drawn more than once: " + string.Join(", ", doubled));

            Assert.AreEqual(WorldRegionCatalog.AllCountries.Count, counts.Count,
                "The map carries a country the catalog has never heard of.");
        }

        /// <summary>
        /// A country's shape sits in the region the catalog puts it in.
        ///
        /// Both files assign a region by hand and they are assigned in different places for
        /// different reasons, so this is the one assertion that can catch a copy-paste in the bake.
        /// </summary>
        [Test]
        public void TheMapAndTheCatalogAgreeAboutWhereCountriesAre()
        {
            foreach (var definition in WorldRegionCatalog.AllCountries)
            {
                var shape = WorldShapes.For(definition.Country);

                Assert.IsNotNull(shape, definition.DisplayName);
                Assert.AreEqual(definition.Region, shape.Region,
                    $"{definition.DisplayName} is drawn in {shape.Region} and offered under "
                    + $"{definition.Region}.");
            }
        }

        /// <summary>
        /// Every shape is inside the box it says it is, and its pin is inside the shape.
        ///
        /// **The pin matters more than it looks.** It is where the marker is drawn, and a centroid
        /// that lands outside its own country puts Norway's dot in the sea. Ordinary for a crescent,
        /// which is why it is measured rather than assumed.
        /// </summary>
        [Test]
        public void EveryOutlineIsInsideTheMapAndEveryPinIsInsideItsCountry()
        {
            var strays = new List<string>();

            foreach (var shape in WorldShapes.All)
            {
                foreach (var ring in shape.Rings)
                {
                    foreach (var point in ring)
                    {
                        Assert.GreaterOrEqual(point.x, -0.001f, shape.Name);
                        Assert.LessOrEqual(point.x, 1.001f, shape.Name);
                        Assert.GreaterOrEqual(point.y, -0.001f, shape.Name);
                        Assert.LessOrEqual(point.y, WorldShapes.Aspect + 0.001f, shape.Name);
                    }
                }

                if (shape.Member != Country.None && !shape.Contains(shape.Pin))
                {
                    strays.Add(shape.Name);
                }
            }

            CollectionAssert.IsEmpty(strays,
                "The marker for these lands outside the country it belongs to: "
                + string.Join(", ", strays));
        }

        /// <summary>
        /// The cursor finds what the eye sees.
        ///
        /// Hit-testing is point-in-polygon against the rings that are drawn, so this asserts the two
        /// cannot disagree: a point on Poland answers Poland, and a point in the middle of the
        /// Atlantic answers nothing at all.
        /// </summary>
        [Test]
        public void ClickingACountryFindsThatCountry()
        {
            foreach (var definition in WorldRegionCatalog.AllCountries)
            {
                var shape = WorldShapes.For(definition.Country);

                Assert.AreEqual(definition.Country, WorldShapes.PlayableAt(shape.Pin),
                    $"A click on {definition.DisplayName}'s own marker does not select it.");

                Assert.AreEqual(definition.Region, WorldShapes.RegionAt(shape.Pin),
                    $"A click on {definition.DisplayName} does not select its region.");
            }

            // The middle of the South Atlantic. Nothing there, and nothing should be reported.
            var ocean = new Vector2(0.42f, WorldShapes.Aspect * 0.80f);

            Assert.AreEqual(Country.None, WorldShapes.PlayableAt(ocean));
            Assert.AreEqual(WorldRegion.None, WorldShapes.RegionAt(ocean));
        }

        /// <summary>
        /// Exactly the countries too small to click get a marker.
        ///
        /// **A threshold is a number somebody chose, so it needs a witness.** The rule is "mark what
        /// a cursor cannot find", and the first value caught eight of the sixteen including Germany
        /// and Japan, which is not that rule. This pins the set rather than the constant: re-baking
        /// at a finer resolution moves every area, and the question worth asking afterwards is
        /// whether the same five countries still need help.
        /// </summary>
        [Test]
        public void OnlyTheCountriesTooSmallToClickGetAMarker()
        {
            var marked = new List<string>();

            foreach (var definition in WorldRegionCatalog.AllCountries)
            {
                var shape = WorldShapes.For(definition.Country);

                if (shape.DrawnArea < WorldMapElement.NeedsAMarkerBelow)
                {
                    marked.Add(definition.DisplayName);
                }
            }

            marked.Sort();

            CollectionAssert.AreEqual(
                new[] { "Ireland", "Singapore", "South Korea", "Switzerland", "Taiwan" },
                marked,
                "The set of countries a cursor cannot find has changed: " + string.Join(", ", marked));
        }

        /// <summary>
        /// The map is coarse enough to draw and fine enough to recognise.
        ///
        /// A bound in both directions on purpose. Too many points and Painter2D is retessellating
        /// five figures of geometry on every hover; too few and the simplification has eaten the
        /// coastlines, which is the kind of change nobody notices in a diff.
        /// </summary>
        [Test]
        public void TheOutlinesAreTheRightWeight()
        {
            var points = 0;

            foreach (var shape in WorldShapes.All)
            {
                foreach (var ring in shape.Rings)
                {
                    points += ring.Length;
                }
            }

            Assert.Greater(points, 3000,
                $"Only {points} points left: the simplification has eaten the coastlines.");

            Assert.Less(points, 12000,
                $"{points} points is more geometry than a picker needs, and every hover retessellates it.");
        }
    }
}
