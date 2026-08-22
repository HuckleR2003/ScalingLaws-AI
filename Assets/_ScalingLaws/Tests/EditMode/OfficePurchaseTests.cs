using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Renting a place against owning it.
    ///
    /// **Two different decisions, and the whole point is that neither is obviously right.** Rent is
    /// a monthly bill a struggling company can walk away from. A purchase is capital that never
    /// comes back and ends the rent forever, priced at about ten years of it, so a company that will
    /// still be here in a decade should buy and one that is not sure should not tie up the money.
    /// </summary>
    public sealed class OfficePurchaseTests
    {
        private static CompanySimulation Rich(long cash = 400_000_000)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = cash;
            return simulation;
        }

        [Test]
        public void BuyingCostsAboutTenYearsOfRent()
        {
            foreach (var place in OfficeCatalog.Places())
            {
                if (!place.CanBeBought)
                {
                    continue;
                }

                var years = place.PurchasePriceUsd / (double)(place.MonthlyRentUsd * 12L);

                Assert.Greater(years, 5.0,
                    $"{place.DisplayName} pays for itself in {years:0.0} years, so renting is never "
                    + "the right answer and the choice disappears.");

                Assert.Less(years, 16.0,
                    $"{place.DisplayName} takes {years:0.0} years to pay for itself, which is longer "
                    + "than the campaign. Nobody would ever buy it.");
            }
        }

        [Test]
        public void TheHouseIsNotForSale()
        {
            Assert.IsFalse(OfficeCatalog.Get(OfficeTier.Garage).CanBeBought,
                "Nobody sells you the room you already live in.");
        }

        [Test]
        public void TheSmallHubHoldsTenPeople()
        {
            Assert.AreEqual(10, OfficeCatalog.Get(OfficeTier.Loft).Desks);
        }

        // ---- what buying does ---------------------------------------------------------------------

        [Test]
        public void BuyingTakesTheMoneyAndEndsTheRent()
        {
            var simulation = Rich();
            var place = OfficeCatalog.Get(OfficeTier.Loft);

            Assert.IsTrue(simulation.TryBuyOffice(OfficeTier.Loft, out var why), why);

            Assert.IsTrue(simulation.State.Staff.Owns(OfficeTier.Loft));
            Assert.AreEqual(OfficeTier.Loft, simulation.State.Staff.Office,
                "Buying somewhere the company is not should move it in.");

            Assert.AreEqual(0L, simulation.State.Staff.DailyRentUsd,
                "The whole return on a purchase is that the rent stops.");

            Assert.LessOrEqual(simulation.State.CashUsd,
                400_000_000 - place.PurchasePriceUsd,
                "The money never came out.");
        }

        [Test]
        public void RentingTheSamePlaceStillCosts()
        {
            var simulation = Rich();
            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Loft, out var why), why);

            Assert.IsFalse(simulation.State.Staff.Owns(OfficeTier.Loft));
            Assert.Greater(simulation.State.Staff.DailyRentUsd, 0L);
        }

        [Test]
        public void ACompanyThatCannotAffordItIsRefusedWithAReason()
        {
            var simulation = Rich(cash: 1_000_000);

            Assert.IsFalse(simulation.TryBuyOffice(OfficeTier.Loft, out var why));
            Assert.IsNotEmpty(why);
            Assert.IsFalse(simulation.State.Staff.Owns(OfficeTier.Loft));
        }

        [Test]
        public void BuyingTwiceIsRefused()
        {
            var simulation = Rich();
            Assert.IsTrue(simulation.TryBuyOffice(OfficeTier.Loft, out _));
            Assert.IsFalse(simulation.TryBuyOffice(OfficeTier.Loft, out var why));
            StringAssert.Contains("already owns", why);
        }

        [Test]
        public void OwningSomewhereSurvivesMovingOutAndBack()
        {
            // Owning is per place rather than a flag on the current office. A company that buys the
            // small hub and later moves up still owns it, and moving back has to be free.
            var simulation = Rich(cash: 900_000_000);

            Assert.IsTrue(simulation.TryBuyOffice(OfficeTier.Loft, out _));
            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Floor, out var why), why);

            Assert.Greater(simulation.State.Staff.DailyRentUsd, 0L,
                "The company is renting the big hub now and should be paying for it.");

            Assert.IsTrue(simulation.State.Staff.Owns(OfficeTier.Loft),
                "It sold the building by walking out of it.");

            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Loft, out _));
            Assert.AreEqual(0L, simulation.State.Staff.DailyRentUsd,
                "Moving back into a place it owns should cost nothing to keep.");
        }

        [Test]
        public void BuyingThePlaceTheCompanyIsAlreadyInSkipsTheFitOut()
        {
            var moved = Rich();
            Assert.IsTrue(moved.TryMoveOffice(OfficeTier.Loft, out _));
            var afterMove = moved.State.CashUsd;

            Assert.IsTrue(moved.TryBuyOffice(OfficeTier.Loft, out _));

            var place = OfficeCatalog.Get(OfficeTier.Loft);
            Assert.AreEqual(afterMove - place.PurchasePriceUsd, moved.State.CashUsd,
                "It was charged to fit out a place it was already sitting in.");
        }

        // ---- the save ---------------------------------------------------------------------------

        [Test]
        public void OwnershipSurvivesASave()
        {
            var simulation = Rich();
            Assert.IsTrue(simulation.TryBuyOffice(OfficeTier.Loft, out _));

            var restored = SaveStore.Restore(SaveStore.Capture(simulation.State));

            Assert.IsTrue(restored.Staff.Owns(OfficeTier.Loft),
                "Reloading handed the building back and started the rent again.");

            Assert.AreEqual(0L, restored.Staff.DailyRentUsd);
        }

        /// <summary>
        /// A furnished move delivers furniture.
        ///
        /// **This is the check that this project has failed six times.** A tick that charged
        /// $39,000 and handed over nothing would look completely correct: the money leaves, the
        /// office changes, the event fires, and the only thing missing is the entire point. The
        /// morale is asserted because that is what the pieces are for.
        /// </summary>
        [Test]
        public void AFurnishedMoveActuallyPutsFurnitureOnTheFloor()
        {
            var simulation = Rich();
            var room = RoomCatalog.For(OfficeTier.Loft);

            Assert.IsTrue(room.AllowsFurniture, "This test needs a place with floor in it.");

            var zone = new DecorZone(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

            Assert.IsTrue(simulation.TryMoveOffice(OfficeTier.Loft, zone, out var why), why);

            Assert.That(simulation.State.Decor, Is.Not.Null);

            Assert.That(simulation.State.Decor.Items.Count,
                Is.EqualTo(OfficeCatalog.FurnishedPack.Count),
                "Every piece in the pack has to arrive, standing or stored.");

            Assert.That(simulation.State.Staff.ComfortBonus, Is.GreaterThan(0.0),
                "Furniture that raises nobody's morale is furniture that is not there.");
        }

        [Test]
        public void AnUnfurnishedMoveCostsLessAndDeliversNothing()
        {
            var bare = Rich();
            Assert.IsTrue(bare.TryMoveOffice(OfficeTier.Loft, null, out _));

            var room = RoomCatalog.For(OfficeTier.Loft);
            var zone = new DecorZone(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

            var furnished = Rich();
            Assert.IsTrue(furnished.TryMoveOffice(OfficeTier.Loft, zone, out _));

            Assert.That(bare.State.CashUsd - furnished.State.CashUsd,
                Is.EqualTo(OfficeCatalog.FurnishedPackUsd),
                "The difference between the two moves is exactly the pack, and nothing else.");

            Assert.That(bare.State.Decor?.Items.Count ?? 0, Is.EqualTo(0));
        }

        /// <summary>
        /// The pack is a saving, not a surcharge.
        ///
        /// If it ever costs more than the pieces are worth, ticking it is a tax on anybody who does
        /// not check, and the option stops being a choice. The same rule the safety effort slider
        /// is held to.
        /// </summary>
        [Test]
        public void TheFurnishedPackIsCheaperThanBuyingThePiecesOneAtATime()
        {
            Assert.That(OfficeCatalog.FurnishedPackListUsd,
                Is.GreaterThan(OfficeCatalog.FurnishedPackUsd),
                "Nobody would take a standard fit-out that costs more than choosing it themselves.");

            Assert.That(OfficeCatalog.FurnishedPack,
                Has.None.EqualTo(FurnitureKind.Desk).And.None.EqualTo(FurnitureKind.StandingDesk),
                "Desks are what caps hiring. A pack containing them is an economy change wearing a "
                + "convenience label.");
        }

        [Test]
        public void AnOlderCampaignOwnsNothing()
        {
            var old = new SaveData { version = 28 };
            var moved = SaveMigration.UpgradeV28ToV29(old);

            Assert.AreEqual(29, moved.version);
            Assert.IsEmpty(moved.ownedOffices,
                "Crediting an old company with a building would wipe a bill it has been paying all "
                + "along.");
        }
    }
}
