using System;
using System.Text;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Long games, many seeds, checked every year against things that must never be true.
    ///
    /// This is not a balance fixture. <see cref="PlayabilityTests"/> asks whether the game is fair;
    /// this asks whether it is still a game after a decade. The difference matters because every
    /// number here is fed by compounding curves, and a term that is merely slightly wrong looks fine
    /// in year one and is infinite in year twelve.
    ///
    /// It runs several different players rather than one, because a bug that only appears when
    /// somebody buys hardware is invisible to a player who never does.
    /// </summary>
    public sealed class StabilityScanTests
    {
        private const int Years = 14;
        private static readonly uint[] Seeds = { 3, 77, 512, 4242, 90210 };

        private enum Archetype
        {
            /// <summary>Never acts. The floor: the world alone must stay sane.</summary>
            Passive,

            /// <summary>Rents, trains, ships, repeats. What most players will do.</summary>
            Shipper,

            /// <summary>Buys hardware and keeps it, which is the path that ages and depreciates.</summary>
            Owner
        }

        private static void Play(Archetype who, uint seed, Action<CompanySimulation, int> everyYear)
        {
            var simulation = new CompanySimulation(new CompanyState($"Scan{seed}", seed));
            var generation = 1;

            for (var day = 0; day < Years * 365; day++)
            {
                if (who != Archetype.Passive && day == 0)
                {
                    simulation.SetRentedPetaflops(150.0);
                }

                if (who == Archetype.Owner && day == 400)
                {
                    // One purchase, then live with it. Owning is the branch that ages, depreciates
                    // and draws power, and none of that is exercised by renting.
                    simulation.TryBuyHardware(
                        MarketModel.RentableGenerationOn(simulation.State.Date), 8,
                        ComputeTier.RentedCloud, out _);
                }

                if (who != Archetype.Passive && simulation.State.ActiveRun == null
                    && simulation.State.Shelf.Count == 0 && day % 220 == 10)
                {
                    var blueprint = new ModelBlueprint($"Scan {generation}",
                        ArchitectureId.DenseTransformer, 4.0 * generation, 80.0 * generation,
                        DatasetSource.WebCrawl, ModelType.General, "Scan");

                    if (simulation.TryStartTraining(blueprint, out _))
                    {
                        generation++;
                    }
                }

                simulation.AdvanceDay();

                if (simulation.State.Shelf.Count > 0)
                {
                    simulation.TryReleaseModel(0, 1.0, out _);
                }

                if (day % 365 == 364)
                {
                    everyYear(simulation, day / 365 + 1);
                }
            }
        }

        /// <summary>
        /// Nothing in the market may become impossible, at any point, for any player, in any year.
        /// </summary>
        [Test]
        public void TheMarketStaysFiniteForFourteenYears()
        {
            foreach (var who in Enum.GetValues(typeof(Archetype)))
            {
                foreach (var seed in Seeds)
                {
                    Play((Archetype)who, seed, (simulation, year) =>
                    {
                        var where = $"{who} seed {seed} year {year}";
                        var breakdown = simulation.MarketByType();

                        Assert.IsFalse(double.IsNaN(breakdown.AddressableUsers), where);
                        Assert.Greater(breakdown.AddressableUsers, 0.0,
                            $"{where}: the market emptied of people entirely.");

                        Assert.GreaterOrEqual(breakdown.TotalUsersOverall, 0.0, where);
                        Assert.LessOrEqual(breakdown.TotalUsersOverall,
                            breakdown.AddressableUsers * 1.000001,
                            $"{where}: more people are being served than exist.");

                        foreach (var standing in breakdown.Types)
                        {
                            Assert.IsFalse(double.IsNaN(standing.TotalUsers), $"{where} {standing.Type}");
                            Assert.GreaterOrEqual(standing.TotalUsers, 0.0, $"{where} {standing.Type}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// The books have to reconcile with the bank in every year of every run, not only in the one
        /// campaign the ledger fixture happens to drive.
        /// </summary>
        [Test]
        public void TheBooksReconcileInEveryRun()
        {
            foreach (var who in Enum.GetValues(typeof(Archetype)))
            {
                foreach (var seed in Seeds)
                {
                    Play((Archetype)who, seed, (simulation, year) =>
                    {
                        var state = simulation.State;
                        var reconstructed =
                            CompanyState.StartingCashUsd + state.Ledger.TotalCashFlowUsd;

                        Assert.AreEqual(state.CashUsd, reconstructed,
                            $"{who} seed {seed} year {year}: the bank says {state.CashUsd:N0} and the "
                            + $"books say {reconstructed:N0}.");
                    });
                }
            }
        }

        [Test]
        public void NoCompanyEverReachesAnImpossibleBalanceOrReputation()
        {
            foreach (var who in Enum.GetValues(typeof(Archetype)))
            {
                foreach (var seed in Seeds)
                {
                    Play((Archetype)who, seed, (simulation, year) =>
                    {
                        var state = simulation.State;
                        var where = $"{who} seed {seed} year {year}";

                        Assert.IsTrue(state.CashUsd > long.MinValue / 4
                            && state.CashUsd < long.MaxValue / 4,
                            $"{where}: cash ran away to {state.CashUsd}.");

                        Assert.IsFalse(double.IsNaN(state.Reputation), where);
                        Assert.GreaterOrEqual(state.Reputation, 0.0, where);
                        Assert.LessOrEqual(state.Reputation, 1.0, where);

                        var sentiment = simulation.Sentiment();
                        Assert.IsFalse(double.IsNaN(sentiment.Users), where);
                        Assert.GreaterOrEqual(sentiment.Satisfaction, 0.0, where);
                        Assert.LessOrEqual(sentiment.Satisfaction, 1.0, where);
                    });
                }
            }
        }

        /// <summary>
        /// The fleet bill has to keep adding up to the number the company is actually charged. These
        /// are two separate computations of the same day and nothing forces them to agree.
        /// </summary>
        [Test]
        public void TheFleetBillAlwaysAddsUpToTheFleetCost()
        {
            foreach (var seed in Seeds)
            {
                Play(Archetype.Owner, seed, (simulation, year) =>
                {
                    var profile = simulation.Profile;
                    var bill = profile.Bill;

                    Assert.AreEqual(profile.DailyOperatingCostUsd, bill.TotalUsd,
                        Math.Max(1.0, profile.DailyOperatingCostUsd * 1e-9),
                        $"seed {seed} year {year}: the four bills do not make the total.");

                    Assert.GreaterOrEqual(bill.ElectricityUsd, 0.0);
                    Assert.GreaterOrEqual(bill.CloudRentUsd, 0.0);
                });
            }
        }

        /// <summary>
        /// A player who keeps shipping has to stay in the game. Not winning, in it: the spine says a
        /// company that coasts goes broke, and it must not also say that a company that works goes
        /// broke, or there is nothing to play.
        /// </summary>
        [Test]
        public void APlayerWhoKeepsShippingIsStillTradingAfterFourteenYears()
        {
            var report = new StringBuilder();
            var survived = 0;

            foreach (var seed in Seeds)
            {
                long finalCash = 0;
                var finalUsers = 0.0;

                Play(Archetype.Shipper, seed, (simulation, year) =>
                {
                    finalCash = simulation.State.CashUsd;
                    finalUsers = simulation.Sentiment().Users;

                    if (year == Years)
                    {
                        report.AppendLine($"seed {seed}: cash {finalCash:N0}, users {finalUsers:N0}, "
                            + $"models {simulation.State.DeployedModels.Count}");
                    }
                });

                if (finalCash > 0)
                {
                    survived++;
                }
            }

            TestContext.WriteLine(report.ToString());

            Assert.Greater(survived, 0,
                "Every seed bankrupted a player who rented compute and shipped a model every eight "
                + "months for fourteen years. That is not difficulty, it is an unwinnable game.");
        }
    }
}
