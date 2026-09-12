using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Getting out of the first office, which is the thing players ask for most.
    ///
    /// **Reported by the players, not by a tester: the starting office makes hiring impossible.**
    /// That is literally true and it is the design: the house has no desks, so the only people a
    /// company can take on before it moves are remote. What has to be true is that moving is
    /// reachable on day one, that it actually hands over the desks, and that somebody can then be
    /// hired into one.
    ///
    /// Written before changing anything, because a mechanism that already works and cannot be found
    /// is a different problem from one that does not work, and the two have opposite fixes.
    /// </summary>
    public sealed class OfficeMoveTests
    {
        private static CompanySimulation Fresh() =>
            new(new CompanyState("Prometheus AI"));

        [Test]
        public void TheStartingOfficeReallyHasNowhereToSit()
        {
            var simulation = Fresh();

            Assert.That(simulation.State.Staff.Office, Is.EqualTo(OfficeTier.Garage),
                "A campaign no longer opens in the house, so the rest of this fixture is about a "
                + "situation that does not happen.");

            Assert.That(simulation.State.Staff.Desks, Is.Zero,
                "The house has desks, so the complaint this fixture exists for is gone.");

            Assert.That(simulation.State.Staff.HasFreeSeat, Is.False);
        }

        /// <summary>
        /// **The one that matters.** A company on day one, with the money it is handed, can move.
        /// </summary>
        [Test]
        public void AFreshCompanyCanAffordToMoveOutOnDayOne()
        {
            var simulation = Fresh();
            var loft = OfficeCatalog.Get(OfficeTier.Loft);

            Assert.That(simulation.State.CashUsd, Is.GreaterThanOrEqualTo(loft.RequiredCashUsd),
                "The first upgrade asks for more cash than a campaign starts with, so the office "
                + "that blocks hiring cannot be left until the company earns, and it cannot earn "
                + "at full strength until it leaves.");

            Assert.That(simulation.TryMoveOffice(OfficeTier.Loft, out var why), Is.True, why);

            Assert.That(simulation.State.Staff.Office, Is.EqualTo(OfficeTier.Loft),
                "The move reported success and the company is still in the house.");

            Assert.That(simulation.State.Staff.Desks, Is.EqualTo(loft.Desks),
                "The company moved and got no desks, which is the whole reason to move.");
        }

        /// <summary>
        /// And somebody can actually sit at one. **A move that charges for desks and still refuses
        /// every hire is the failure class this project has hit twelve times.**
        /// </summary>
        [Test]
        public void AfterMovingSomebodyCanBeHiredIntoADesk()
        {
            var simulation = Fresh();
            Assert.That(simulation.TryMoveOffice(OfficeTier.Loft, out var why), Is.True, why);

            var seated = simulation.State.Staff.Add(new Hire(
                StaffRole.ResearchScientist, 3, simulation.State.Date, "Iwona Krajewska",
                PlayerSkill.Concept, HireSource.Agency, 180.0));

            Assert.That(seated, Is.True, "Nobody can be hired into an office full of empty desks.");

            Assert.That(simulation.State.Staff.SeatedHeadcount, Is.EqualTo(1));
            Assert.That(simulation.State.Staff.HasFreeSeat, Is.True, "Ten desks and one person.");
        }

        /// <summary>
        /// **Remote is the way out before there is a way out**, and it has to work with no desks at
        /// all or a company in the house cannot hire anybody on any terms.
        /// </summary>
        [Test]
        public void RemoteWorkNeedsNoDesk()
        {
            var simulation = Fresh();

            Assume.That(simulation.State.Staff.Desks, Is.Zero);

            var hired = simulation.State.Staff.Add(new Hire(
                StaffRole.GoToMarket, 2, simulation.State.Date, "Ada Wrona",
                PlayerSkill.Support, HireSource.Remote, 88.0));

            Assert.That(hired, Is.True,
                "A company in the house cannot hire anybody at all, on any terms, which makes the "
                + "first hour of the game a screen with nothing on it.");

            Assert.That(simulation.State.Staff.SeatedHeadcount, Is.Zero,
                "A remote hire took a desk that does not exist.");
        }

        /// <summary>
        /// **Every office a player can move into is a room they can look at.**
        ///
        /// The tower was added to the ladder on 2026-08-28 and never got an entry in `RoomCatalog`,
        /// so `RoomFor` fell through to the garage, whose view loads no prefab at all. A company
        /// that reached the largest building it can lease, at three hundred and eighty million
        /// dollars, would have walked into an empty frame. Nothing fails when that happens: the
        /// stage loads null and draws the floor it already had.
        /// </summary>
        [Test]
        public void EveryOfficeOnTheLadderHasARoomToStandIn()
        {
            foreach (var entry in OfficeCatalog.All)
            {
                var room = RoomCatalog.For(entry.Tier);

                if (entry.Tier == OfficeTier.Garage)
                {
                    // The house is the scene the game already loads, so it names no prefab on
                    // purpose. Everything else has to bring its own.
                    continue;
                }

                Assert.That(room.IsLoaded, Is.True,
                    entry.DisplayName + " has no room, so moving into it leaves the office view "
                    + "showing whatever was there before.");
            }
        }

        /// <summary>
        /// Buying one outright, which a tester reported as not working.
        ///
        /// **It works, and this is the evidence.** Two of the six places carry a purchase price and
        /// the chooser draws a buy button only for those, so the likeliest reading of the report is
        /// a button that was never there rather than one that refused. Written so the next person to
        /// hear it can answer with a number.
        /// </summary>
        [Test]
        public void AnOfficeWithAPriceOnItCanActuallyBeBought()
        {
            var simulation = Fresh();
            var loft = OfficeCatalog.Get(OfficeTier.Loft);

            Assume.That(loft.CanBeBought, Is.True, "the first office is not for sale at all");

            simulation.State.CashUsd = loft.PurchasePriceUsd + loft.FitOutCostUsd + 1_000_000L;

            Assert.That(simulation.TryBuyOffice(OfficeTier.Loft, null, out var why), Is.True, why);

            Assert.That(simulation.State.Staff.Owns(OfficeTier.Loft), Is.True,
                "The purchase reported success and the company does not own the place.");

            Assert.That(simulation.State.Staff.Office, Is.EqualTo(OfficeTier.Loft),
                "It bought the office and stayed in the house.");

            Assert.That(simulation.State.Staff.DailyRentUsd, Is.Zero,
                "It owns the building and is still paying rent on it, which is the whole return "
                + "on the purchase.");
        }

        /// <summary>
        /// **Four of the six cannot be bought at all**, and the screen has to keep saying so by
        /// drawing no button rather than by refusing a click. A row that reads as an option and
        /// turns down every press reads as a bug, which is how this project has shipped the
        /// complaint twice before.
        /// </summary>
        [Test]
        public void AnOfficeWithNoPriceIsNotOfferedForSale()
        {
            var simulation = Fresh();
            simulation.State.CashUsd = 900_000_000L;

            foreach (var place in OfficeCatalog.All)
            {
                if (place.CanBeBought)
                {
                    continue;
                }

                Assert.That(simulation.TryBuyOffice(place.Tier, null, out var why), Is.False,
                    place.DisplayName + " has no purchase price and sold itself anyway.");

                Assert.That(why, Is.Not.Empty,
                    place.DisplayName + " refused the sale and said nothing about why.");
            }
        }

        /// <summary>The ladder goes up, and each rung really is bigger than the one below it.</summary>
        [Test]
        public void EveryRungHoldsMorePeopleThanTheOneBelow()
        {
            var previous = -1;

            foreach (var entry in OfficeCatalog.All)
            {
                Assert.That(entry.Desks, Is.GreaterThan(previous),
                    entry.DisplayName + " holds no more people than the office under it, so "
                    + "moving into it is a rent increase and nothing else.");

                previous = entry.Desks;
            }
        }
    }
}
