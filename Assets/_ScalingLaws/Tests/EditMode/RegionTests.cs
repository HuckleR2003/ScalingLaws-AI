using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests
{
    /// <summary>
    /// The home country has to be a trade rather than a right answer, and it has to actually reach
    /// the numbers. A region that only prints four figures on the creator screen is decoration.
    /// </summary>
    public sealed class RegionTests
    {
        [Test]
        public void EveryRegionHasCountriesAndEveryCountryHasARegion()
        {
            foreach (var region in WorldRegionCatalog.All)
            {
                Assert.IsNotEmpty(WorldRegionCatalog.CountriesIn(region.Region),
                    $"{region.DisplayName} has nothing to pick.");
            }

            foreach (var country in WorldRegionCatalog.AllCountries)
            {
                Assert.AreNotEqual(WorldRegion.None, country.Region, country.DisplayName);
                Assert.AreSame(country, WorldRegionCatalog.Get(country.Country));
            }
        }

        [Test]
        public void NoCountryIsBestAtEverything()
        {
            foreach (var country in WorldRegionCatalog.AllCountries)
            {
                var cheapHardware = country.HardwarePriceMultiplier < 1.0;
                var lowTax = country.TaxRate < 0.20;
                var strongTalent = country.InnovationMultiplier > 1.0;
                var quietMarket = country.LocalCompetitionMultiplier < 0.8;

                var wins = new[] { cheapHardware, lowTax, strongTalent, quietMarket }.Count(w => w);
                Assert.Less(wins, 4, $"{country.DisplayName} wins on every axis, so there is no choice to make.");
            }
        }

        [Test]
        public void TheCheapHardwarePlacesAreNotAlsoTheEmptyOnes()
        {
            // The whole point of the map: silicon is cheapest where the rivals already are.
            var cheapest = WorldRegionCatalog.AllCountries.OrderBy(c => c.HardwarePriceMultiplier).First();
            var quietest = WorldRegionCatalog.AllCountries.OrderBy(c => c.LocalCompetitionMultiplier).First();

            Assert.AreNotEqual(cheapest.Country, quietest.Country);
        }

        [Test]
        public void TaxIsChargedOnProfitAndNotOnTurnover()
        {
            var state = new CompanyState("Testco", 31) { HomeCountry = Country.Ireland };
            var before = state.CashUsd;

            var simulation = new CompanySimulation(state);
            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            // Nothing has shipped, so there is no profit and there must be no tax.
            Assert.AreEqual(0L, state.LifetimeTaxPaidUsd);
            Assert.Less(state.CashUsd, before, "A month of costs with no product should still lose money.");
        }

        [Test]
        public void ADearerCountryReallyChargesMoreForTheSameHardware()
        {
            var date = GameDate.FromCalendar(2023, 6, 1);

            var cheap = Buyer("Cheap", date, Country.Taiwan);
            var dear = Buyer("Dear", date, Country.Brazil);

            Assert.IsTrue(new CompanySimulation(cheap).TryBuyHardware(
                HardwareGenerationId.AcceleratorH100, 64, ComputeTier.ColocatedServers, out var why), why);
            Assert.IsTrue(new CompanySimulation(dear).TryBuyHardware(
                HardwareGenerationId.AcceleratorH100, 64, ComputeTier.ColocatedServers, out _));

            Assert.Less(cheap.LifetimeCapitalSpentUsd, dear.LifetimeCapitalSpentUsd,
                "Taiwan should not pay Brazilian prices for the same accelerators.");
        }

        /// <summary>A company that has shipped once, which is what the colocated tier is gated on.</summary>
        private static CompanyState Buyer(string name, GameDate date, Country country)
        {
            var state = new CompanyState(name, 5)
            {
                Date = date,
                CashUsd = 5_000_000_000L,
                HomeCountry = country
            };

            state.AddDeployedModel(new DeployedModel(
                "Flagship", ArchitectureId.DenseTransformer, 40.0, date, 2e10, 1.0));
            return state;
        }

        [Test]
        public void ASaveKeepsTheHomeCountry()
        {
            var data = new SaveData { homeCountry = (int)Country.Poland, worldRegion = (int)WorldRegion.Europe };
            Assert.AreEqual(WorldRegion.Europe, WorldRegionCatalog.Get((Country)data.homeCountry).Region);
        }

        [Test]
        public void AV10SaveIsRegisteredSomewhereRatherThanNowhere()
        {
            var data = new SaveData { version = 10, worldRegion = 0, homeCountry = 0 };
            var upgraded = SaveMigration.UpgradeV10ToV11(data);

            Assert.AreEqual(11, upgraded.version);
            Assert.AreEqual((int)WorldRegion.America, upgraded.worldRegion);
            Assert.AreEqual((int)Country.UnitedStates, upgraded.homeCountry);
            Assert.IsNotEmpty(SaveMigration.LastMigrationNotes);
        }

        [Test]
        public void EveryMigrationStepStampsItsOwnVersionAndNotTheNewest()
        {
            // A step that stamps CurrentVersion works only for as long as it happens to be the last
            // one. This is the guard against the next version bump silently skipping a step.
            var data = new SaveData { version = 9 };
            Assert.AreEqual(10, SaveMigration.UpgradeV9ToV10(data).version);
        }

        [Test]
        public void TheRegionAverageSitsInsideItsOwnCountries()
        {
            foreach (var region in WorldRegionCatalog.All)
            {
                var countries = WorldRegionCatalog.CountriesIn(region.Region);
                var average = WorldRegionCatalog.Average(region.Region);

                Assert.GreaterOrEqual(average.TaxRate, countries.Min(c => c.TaxRate) - 1e-9);
                Assert.LessOrEqual(average.TaxRate, countries.Max(c => c.TaxRate) + 1e-9);
            }
        }
    }
}
