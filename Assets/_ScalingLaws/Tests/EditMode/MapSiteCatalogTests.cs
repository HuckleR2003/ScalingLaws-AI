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

        /// <summary>
        /// Silicon Valley's headquarters are ranked addresses: one of each rank from the best down, no
        /// gaps and no ties, because handing the best address to the leading lab means there has to be
        /// exactly one best address.
        /// </summary>
        [Test]
        public void HeadquartersAreRankedOneToNWithoutGapsOrTies()
        {
            var ranks = new List<int>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Kind == MapSiteKind.RivalHeadquarters)
                {
                    ranks.Add(site.Tier);
                }
            }

            ranks.Sort();
            Assert.IsNotEmpty(ranks, "No rival headquarters in the catalog.");

            for (var index = 0; index < ranks.Count; index++)
            {
                Assert.AreEqual(index + 1, ranks[index],
                    "Headquarters ranks must run 1, 2, 3 ... with no gaps or repeats: " + string.Join(", ", ranks));
            }
        }

        /// <summary>A headquarters is named after a lab on the roster, by its in-game name, never a real company's.</summary>
        [Test]
        public void EveryHeadquartersBelongsToALabOnTheRoster()
        {
            var names = new HashSet<string>();
            foreach (var dossier in LabDossiers.All)
            {
                names.Add(dossier.Name);
            }

            var strangers = new List<string>();
            var seen = new HashSet<string>();

            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Kind != MapSiteKind.RivalHeadquarters)
                {
                    continue;
                }

                if (!names.Contains(site.DisplayName))
                {
                    strangers.Add($"{site.Id} -> \"{site.DisplayName}\"");
                }

                if (!seen.Add(site.DisplayName))
                {
                    strangers.Add($"{site.DisplayName} has two headquarters");
                }
            }

            Assert.IsEmpty(strangers, "Headquarters not matching the roster: " + string.Join("; ", strangers));
        }

        /// <summary>Every office the catalog announces as coming soon has a building waiting for it on the map.</summary>
        [Test]
        public void EveryAnnouncedOfficeHasASiteOnTheMap()
        {
            var ids = new HashSet<string>();
            foreach (var site in MapSiteCatalog.All)
            {
                if (site.Kind == MapSiteKind.OfficeLease)
                {
                    ids.Add(site.Id);
                }
            }

            foreach (var announced in OfficeCatalog.ComingSoon)
            {
                // "office.soon.tower.name" is announced; "office.soon.tower" is where it stands.
                var id = announced.NameKey.Substring(0, announced.NameKey.Length - ".name".Length);
                Assert.That(ids, Does.Contain(id), $"No map site for the announced office {announced.NameKey}.");
            }
        }

        /// <summary>A district laid out on terraces has some; every terrace belongs to a district that exists.</summary>
        [Test]
        public void TerracesAndTheDistrictsThatUseThemAgree()
        {
            var districts = new Dictionary<string, DistrictDefinition>();
            foreach (var district in CityLayout.Districts)
            {
                districts[district.Id] = district;
            }

            var used = new HashSet<string>();
            foreach (var terrace in CityBlocks.Terraces)
            {
                Assert.That(districts.ContainsKey(terrace.DistrictId), $"Terrace {terrace.Id} names no district.");
                used.Add(terrace.DistrictId);
            }

            foreach (var district in CityLayout.Districts)
            {
                if (!district.LevelsGround)
                {
                    Assert.That(used, Does.Contain(district.Id),
                        $"{district.Id} levels no pad of its own and has no terrace either: it would stand on raw hillside.");
                }
            }
        }
    }
}
