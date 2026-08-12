using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What it is like to use the product, and what that does to the market.
    ///
    /// This closes the gap recorded in CLAUDE.md: share used to be awarded on how attractive a model
    /// was while the capacity to serve it was computed separately and then ignored, so a company with
    /// no compute at all held five million users. Demand that cannot be met now costs the thing it
    /// should cost, which is the experience, and the experience feeds back into who stays.
    /// </summary>
    public sealed class ServiceQualityTests
    {
        [Test]
        public void AnEmptyClusterIsFastAndAFullOneIsNot()
        {
            var quiet = new ServiceQuality(10.0, 100.0, 0.0);
            // Past the critical threshold, not exactly on it. Ninety five percent is the edge of the
            // band rather than inside it, and a test sitting on a boundary asks an ambiguous question.
            var busy = new ServiceQuality(99.0, 100.0, 0.0);

            Assert.Less(quiet.ResponseMilliseconds, busy.ResponseMilliseconds);
            Assert.AreEqual(ServiceStatus.Stable, quiet.Status);
            Assert.AreEqual(ServiceStatus.Critical, busy.Status);
        }

        /// <summary>
        /// The shape that makes overprovisioning a real decision. Response time does not rise in a
        /// straight line: the last ten percent of a cluster costs more than the first eighty.
        /// </summary>
        [Test]
        public void TheLastTenPercentOfTheClusterHurtsFarMoreThanTheFirstEighty()
        {
            var floor = new ServiceQuality(0.0, 100.0, 0.0).ResponseMilliseconds;
            var eighty = new ServiceQuality(80.0, 100.0, 0.0).ResponseMilliseconds;
            var ninetyFive = new ServiceQuality(95.0, 100.0, 0.0).ResponseMilliseconds;

            var firstEighty = eighty - floor;
            var lastFifteen = ninetyFive - eighty;

            Assert.Greater(lastFifteen, firstEighty,
                $"The first eighty percent added {firstEighty:N0}ms and the last fifteen added "
                + $"{lastFifteen:N0}ms. If those are the same, filling a cluster is linear and there "
                + "is no reason to ever keep headroom.");
        }

        [Test]
        public void AComfortableServiceIsNotPunishedAtAll()
        {
            Assert.AreEqual(1.0, new ServiceQuality(50.0, 100.0, 0.0).Reliability, 1e-12,
                "Half a cluster is a healthy day and must cost nothing. A penalty that always "
                + "applies is a tax, not a mechanic.");
        }

        [Test]
        public void AnOverloadedServiceLosesItsAppealButNeverAllOfIt()
        {
            var drowning = new ServiceQuality(200.0, 100.0, 0.0);

            Assert.Less(drowning.Reliability, 1.0);
            Assert.GreaterOrEqual(drowning.Reliability, ServiceQuality.WorstReliability,
                "Even a bad service keeps somebody. Zero would delete a company overnight.");
        }

        [Test]
        public void ReservedCapacityHoldsUpBetterUnderTheSameLoad()
        {
            var shared = new ServiceQuality(90.0, 100.0, 0.0);
            var reserved = new ServiceQuality(90.0, 100.0, 1.0);

            Assert.Less(reserved.ResponseMilliseconds, shared.ResponseMilliseconds,
                "Reserved capacity is the whole reason a package costs more per petaflop than the "
                + "slider. If it behaves identically under load, nobody should ever buy one.");
        }

        [Test]
        public void NoLoadHoweverAbsurdProducesANonsenseReading()
        {
            foreach (var (demand, capacity) in new[]
            {
                (0.0, 0.0), (-5.0, 100.0), (double.NaN, 100.0), (100.0, double.NaN),
                (1e18, 1e-9)
            })
            {
                var quality = new ServiceQuality(demand, capacity, 0.0);

                Assert.IsFalse(double.IsNaN(quality.Utilisation), $"{demand}/{capacity}");
                Assert.IsFalse(double.IsNaN(quality.ResponseMilliseconds), $"{demand}/{capacity}");
                Assert.GreaterOrEqual(quality.Reliability, 0.0);
                Assert.LessOrEqual(quality.Reliability, 1.0);
            }
        }

        // ---- the packages -------------------------------------------------------------------

        [Test]
        public void ThePackagesAreNotALadderWhereOneIsSimplyBest()
        {
            var standard = HostingCatalog.Get(HostingPackage.Standard);
            var edge = HostingCatalog.Get(HostingPackage.LowLatency);
            var bulk = HostingCatalog.Get(HostingPackage.Bulk);

            // Bulk is the biggest and the worst behaved. Edge is the smallest and the best behaved.
            // If either of those stops being true, one package dominates and the choice disappears.
            Assert.Greater(bulk.Petaflops, standard.Petaflops);
            Assert.Greater(standard.Petaflops, edge.Petaflops);

            Assert.Greater(edge.ReservedQuality, standard.ReservedQuality);
            Assert.Greater(standard.ReservedQuality, bulk.ReservedQuality);

            var bulkPerFlop = bulk.MonthlyCostUsd / bulk.Petaflops;
            var edgePerFlop = edge.MonthlyCostUsd / edge.Petaflops;

            Assert.Less(bulkPerFlop, edgePerFlop,
                "Bulk has to be cheaper per petaflop or it is worse on every axis at once.");
        }

        [Test]
        public void PackagesStackAndAddTheirCapacityToTheFleet()
        {
            var state = new CompanyState("Packco", 3);
            var before = state.Pool.PackagedPetaflops;

            state.Pool.SetPackageCount(HostingPackage.Standard, 3);

            Assert.AreEqual(before + HostingCatalog.Get(HostingPackage.Standard).Petaflops * 3.0,
                state.Pool.PackagedPetaflops, 1e-9);

            Assert.AreEqual(3, state.Pool.PackageCount(HostingPackage.Standard));
        }

        [Test]
        public void APackageCountCannotBeNegativeOrUnbounded()
        {
            var state = new CompanyState("Capco", 3);
            var definition = HostingCatalog.Get(HostingPackage.Bulk);

            state.Pool.SetPackageCount(HostingPackage.Bulk, -4);
            Assert.AreEqual(0, state.Pool.PackageCount(HostingPackage.Bulk));

            state.Pool.SetPackageCount(HostingPackage.Bulk, 99_999);
            Assert.AreEqual(definition.UnitCap, state.Pool.PackageCount(HostingPackage.Bulk),
                "Nothing scales for ever, and an uncapped package is an infinite money sink or an "
                + "infinite capacity cheat depending on the balance.");
        }

        [Test]
        public void BulkDragsDownTheQualityOfAMixedFleet()
        {
            var state = new CompanyState("Mixco", 3);

            state.Pool.SetPackageCount(HostingPackage.LowLatency, 1);
            var pure = state.Pool.PackagedQuality;

            state.Pool.SetPackageCount(HostingPackage.Bulk, 2);
            var mixed = state.Pool.PackagedQuality;

            Assert.Less(mixed, pure,
                "Filling out a careful fleet with cheap shared capacity has to show up somewhere, "
                + "or bulk is free volume.");
        }

        // ---- through the real market ---------------------------------------------------------

        /// <summary>
        /// The reason the whole mechanism exists, measured through the market rather than asserted.
        /// Two identical companies, same model, same price, one with room to serve and one without.
        /// </summary>
        [Test]
        public void ACompanyThatCannotServeItsUsersLosesThemToOneThatCan()
        {
            static double Play(double petaflops)
            {
                var simulation = new CompanySimulation(new CompanyState("Loadco", 606));
                simulation.SetRentedPetaflops(petaflops);

                simulation.State.AddDeployedModel(new DeployedModel(
                    "Subject", ArchitectureId.DenseTransformer, 52.0,
                    simulation.State.Date, 2e10, 1.0, ModelType.General));

                for (var day = 0; day < 700; day++)
                {
                    simulation.AdvanceDay();
                }

                return simulation.Sentiment().Users;
            }

            var starved = Play(2.0);
            var comfortable = Play(400.0);

            Assert.Greater(comfortable, starved,
                $"A fleet with room held {comfortable:N0} users and a starved one held {starved:N0}. "
                + "If those are equal, capacity still does not reach the market and share is awarded "
                + "to companies that cannot deliver anything.");
        }

        [Test]
        public void TheServiceLoadSurvivesASaveBecauseTomorrowsMarketReadsIt()
        {
            var simulation = new CompanySimulation(new CompanyState("Loadco", 77));
            simulation.SetRentedPetaflops(30.0);
            simulation.State.Pool.SetPackageCount(HostingPackage.Standard, 2);

            simulation.State.AddDeployedModel(new DeployedModel(
                "Subject", ArchitectureId.DenseTransformer, 50.0,
                simulation.State.Date, 2e10, 1.0, ModelType.General));

            for (var day = 0; day < 300; day++)
            {
                simulation.AdvanceDay();
            }

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(simulation.State.LastQuality.Utilisation,
                restored.LastQuality.Utilisation, 1e-9,
                "It looks derived and it is not: tomorrow's market reads it, so dropping it replays "
                + "one day differently.");

            Assert.AreEqual(2, restored.Pool.PackageCount(HostingPackage.Standard));
        }

        [Test]
        public void AnOlderSaveLoadsWithNoPackagesAndNoBacklog()
        {
            var data = new SaveData { version = 18 };
            var upgraded = SaveMigration.UpgradeV18ToV19(data);

            Assert.AreEqual(19, upgraded.version);
            Assert.IsEmpty(upgraded.hostingPackages);
            Assert.AreEqual(0.0, upgraded.qualityDemanded, 1e-9,
                "Inventing a bad day would punish a player for loading a game.");
        }
    }
}
