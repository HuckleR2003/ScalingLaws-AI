using System;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A fleet made of reserved packages is still a fleet.
    ///
    /// **This fixture exists because it was not.** `BuildProfile` added the packages' petaflops,
    /// their utilisation ceiling and their bill, and never their accelerator memory. So a company
    /// that bought packages and left the rent slider at zero saw capacity it had paid for on the
    /// compute screen, and `TrainingPlanner` refused every run it ever asked for with "needs N GB
    /// of accelerator memory, fleet offers 0 GB". Nothing failed and nothing said why.
    ///
    /// It is the shape this project keeps meeting: the comment above the block asserted that the
    /// packages join the fleet so every downstream reader sees one fleet, and one line was missing.
    /// </summary>
    public sealed class PackagedFleetTests
    {
        private static readonly GameDate Day = GameDate.FromCalendar(2023, 6, 1);

        [Test]
        public void AFleetOfNothingButPackagesCanStillTrain()
        {
            var market = MarketModel.Evaluate(Day);

            var pool = new ComputePool();
            pool.Packages[HostingPackage.Standard] = 3;

            var profile = pool.BuildProfile(Day, market);

            Assert.That(profile.RawPetaflops, Is.GreaterThan(0.0),
                "the packages are supposed to be capacity");
            Assert.That(profile.TotalAcceleratorMemoryGigabytes, Is.GreaterThan(0.0),
                "capacity with no memory refuses every training run the player asks for");
        }

        /// <summary>
        /// The same petaflops bought either way carry the same memory, because a package is rented
        /// capacity with a better contract behind it rather than a different kind of silicon. Two
        /// answers here would be two fleets, and the screen would report whichever it asked first.
        /// </summary>
        [Test]
        public void PackagedCapacityCarriesTheSameMemoryAsTheSliderWouldForThosePetaflops()
        {
            var market = MarketModel.Evaluate(Day);

            var packagedPool = new ComputePool();
            packagedPool.Packages[HostingPackage.Standard] = 2;
            var packagedPetaflops = packagedPool.PackagedPetaflops;

            var rentedPool = new ComputePool();
            rentedPool.SetRentedPetaflops(packagedPetaflops);

            var packaged = packagedPool.BuildProfile(Day, market);
            var rented = rentedPool.BuildProfile(Day, market);

            Assert.That(packaged.RawPetaflops, Is.EqualTo(rented.RawPetaflops).Within(0.001));
            Assert.That(packaged.TotalAcceleratorMemoryGigabytes,
                Is.EqualTo(rented.TotalAcceleratorMemoryGigabytes).Within(0.001));
        }

        /// <summary>
        /// **The scope line of the repair, stated as a test.** Only the memory joined the fleet.
        /// The accelerator count did not, because it feeds `ScalingEfficiency`, and moving it would
        /// retune the fleet of every company that has ever bought a package. That is a balance
        /// change and this was a missing line.
        /// </summary>
        [Test]
        public void BuyingAPackageDoesNotQuietlyRetuneTheFleetsScalingEfficiency()
        {
            var market = MarketModel.Evaluate(Day);

            var pool = new ComputePool();
            pool.SetRentedPetaflops(400.0);
            var beforeScaling = pool.BuildProfile(Day, market).ScalingEfficiency;

            pool.Packages[HostingPackage.Standard] = 1;
            var afterScaling = pool.BuildProfile(Day, market).ScalingEfficiency;

            Assert.That(afterScaling, Is.EqualTo(beforeScaling).Within(0.0001));
        }
    }
}
