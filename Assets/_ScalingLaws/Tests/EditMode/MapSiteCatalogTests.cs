using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The draft points of interest in <see cref="MapSiteCatalog"/>, checked against the districts
    /// <see cref="CityLayout"/> already surveyed.
    ///
    /// **What this cannot catch.** A position inside its own district's circle can still overlap
    /// another site, sit on a road CityLayout draws through the same district, or land on sloped
    /// ground CityTerrainBuilder has not flattened. None of that is visible to an assertion; it needs
    /// a render, which is what CitySnapshot and CityFlight are for. This fixture only catches the
    /// mistake a coordinate typo makes: a site the district math puts in the wrong place, or one
    /// pointed at a district that does not exist.
    /// </summary>
    public sealed class MapSiteCatalogTests
    {
        [Test]
        public void TheCatalogIsActuallyThere()
        {
            Assert.Greater(MapSiteCatalog.All.Count, 0, "MapSiteCatalog.Entries is empty.");
        }

        [Test]
        public void EveryIdIsUnique()
        {
            var seen = new HashSet<string>();
            var duplicates = new List<string>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (!seen.Add(site.Id))
                {
                    duplicates.Add(site.Id);
                }
            }

            Assert.IsEmpty(duplicates, "Duplicate MapSiteCatalog ids: " + string.Join(", ", duplicates));
        }

        [Test]
        public void EveryDistrictReferenceExists()
        {
            var districtIds = new HashSet<string>();
            foreach (var district in CityLayout.Districts)
            {
                districtIds.Add(district.Id);
            }

            var missing = new List<string>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (!districtIds.Contains(site.DistrictId))
                {
                    missing.Add($"{site.Id} -> \"{site.DistrictId}\"");
                }
            }

            Assert.IsEmpty(missing,
                "MapSiteCatalog entries pointing at a district CityLayout does not have: "
                + string.Join(", ", missing));
        }

        /// <summary>
        /// Every site sits inside its own district's circle, except the two power plants.
        ///
        /// Not a hard geometric requirement of the game — a site a little outside its named district
        /// would still render — but a site that lands nowhere near the district it claims to belong
        /// to is almost always a typo'd coordinate, and this is the assertion that would have caught
        /// one before anybody had to run Unity to notice.
        ///
        /// <see cref="MapSiteKind.PowerPlantStake"/> is exempt by design, not by omission: both sites
        /// sit on open land near the river and the coast, outside every drawn district, which is
        /// where real power infrastructure actually goes. See the note on <see cref="MapSiteCatalog"/>
        /// itself about the "Energy Belt" district that did not survive into <c>CityLayout.cs</c>.
        /// </summary>
        [Test]
        public void EverySitesPositionIsInsideItsOwnDistrict()
        {
            var byId = new Dictionary<string, DistrictDefinition>();
            foreach (var district in CityLayout.Districts)
            {
                byId[district.Id] = district;
            }

            var outside = new List<string>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Kind == MapSiteKind.PowerPlantStake)
                {
                    continue;
                }

                if (!byId.TryGetValue(site.DistrictId, out var district))
                {
                    // Reported by EveryDistrictReferenceExists; do not double-report here.
                    continue;
                }

                var dx = site.Position.X - district.CentreX;
                var dz = site.Position.Z - district.CentreZ;
                var distance = System.Math.Sqrt(dx * dx + dz * dz);

                if (distance > district.Radius)
                {
                    outside.Add($"{site.Id} is {distance:0}m from the centre of \"{district.Id}\", "
                        + $"which has a {district.Radius:0}m radius");
                }
            }

            Assert.IsEmpty(outside, "MapSiteCatalog entries outside their own district: "
                + string.Join("; ", outside));
        }

        [Test]
        public void NoRadiusIsNegativeOrAbsurd()
        {
            var bad = new List<string>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Radius < 0f || site.Radius > 200f || float.IsNaN(site.Radius))
                {
                    bad.Add($"{site.Id}: {site.Radius}");
                }
            }

            Assert.IsEmpty(bad, "MapSiteCatalog entries with an implausible radius: "
                + string.Join(", ", bad));
        }
    }
}
