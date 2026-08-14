using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Foundations for the model history screen.
    ///
    /// The question the screen has to answer is "how good was the first one against the thirtieth,
    /// and what did each of them earn". **Nothing can answer that after the fact.** The company's
    /// books record a month's revenue, not which model brought it in, so the attribution has to be
    /// made on the day and kept.
    ///
    /// These tests hold the two properties that make such a record trustworthy: the parts add up to
    /// the whole, and they survive a save. A history page whose numbers do not reconcile with the
    /// finance report is worse than no history page.
    /// </summary>
    public sealed class ModelHistoryTests
    {
        private static CompanySimulation Fresh(uint seed = 700)
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", seed));
            simulation.SetRentedPetaflops(90.0);
            return simulation;
        }

        private static DeployedModel Ship(CompanySimulation simulation, string name, double capability,
            ModelType type = ModelType.General, string family = "")
        {
            var model = new DeployedModel(name, ArchitectureId.DenseTransformer, capability,
                simulation.State.Date, 2e10, 1.0, type, family);

            simulation.State.AddDeployedModel(model);
            return model;
        }

        [Test]
        public void AModelRecordsWhatItEarnedWhileItWasOnSale()
        {
            var simulation = Fresh();
            var model = Ship(simulation, "Atlas One", 48.0);

            for (var day = 0; day < 200; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.Greater(model.DaysOnSale, 0, "A model on sale has to count the days.");
            Assert.Greater(model.LifetimeRevenueUsd, 0L,
                "Two hundred days of trading and the model is credited with nothing, so the history "
                + "page would show a blank column for every model ever shipped.");

            Assert.Greater(model.PeakUsers, 0.0);
        }

        /// <summary>
        /// The property that makes the record trustworthy. The parts are shares of one revenue figure,
        /// never a second calculation, so the history page and the finance report cannot drift.
        /// </summary>
        [Test]
        public void WhatTheModelsEarnedAddsUpToWhatTheCompanyEarned()
        {
            var simulation = Fresh(701);

            Ship(simulation, "Atlas One", 46.0, ModelType.General, "Atlas");
            Ship(simulation, "Sonnet", 44.0, ModelType.Coding, "Sonnet");

            for (var day = 0; day < 400; day++)
            {
                simulation.AdvanceDay();
            }

            var perModel = 0L;
            foreach (var model in simulation.State.DeployedModels)
            {
                perModel += model.LifetimeRevenueUsd;
            }

            var booked = 0L;
            var month = Ledger.MonthKeyOf(simulation.State.Date);
            for (var back = 0; back <= 14; back++)
            {
                booked += simulation.State.Ledger.MonthTotal(month - back, LedgerLine.Subscriptions);
            }

            // Without this the reconciliation is satisfied by zero equals zero, which is the way a
            // test like this quietly stops testing anything.
            Assert.Greater(booked, 0L,
                "Nothing was earned in four hundred days, so the reconciliation below proves nothing.");

            Assert.AreEqual(booked, perModel,
                $"The books say {booked:N0} and the models say {perModel:N0}. A history page that "
                + "cannot reconcile with the finance report is worse than no history page.");
        }

        [Test]
        public void ASupersededModelStopsEarningBecauseItIsNoLongerOnSale()
        {
            var simulation = Fresh(702);

            var old = Ship(simulation, "Atlas One", 30.0, ModelType.General, "Atlas");

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            var earnedAlone = old.LifetimeRevenueUsd;

            // A stronger model in the same line. One line is one product, so the older one comes off
            // the market and the market stops crediting it.
            Ship(simulation, "Atlas Two", 55.0, ModelType.General, "Atlas");

            for (var day = 0; day < 120; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(earnedAlone, old.LifetimeRevenueUsd,
                "A superseded model is not on sale, so it cannot go on earning. If it does, the "
                + "history page rewards never withdrawing anything, which is the exact hole product "
                + "lines were introduced to close.");
        }

        [Test]
        public void RetiringRecordsWhenSoAHistoryPageCanDrawTheSpan()
        {
            var simulation = Fresh(703);
            var model = Ship(simulation, "Atlas One", 40.0);

            for (var day = 0; day < 30; day++)
            {
                simulation.AdvanceDay();
            }

            var when = simulation.State.Date;
            model.RetireOn(when);

            Assert.IsTrue(model.IsRetired);
            Assert.AreEqual(when.DayIndex, model.RetiredOn.DayIndex);

            model.RetireOn(when.AddDays(400));
            Assert.AreEqual(when.DayIndex, model.RetiredOn.DayIndex,
                "Retiring twice must not move the date. A model comes off sale once.");
        }

        [Test]
        public void TheRecordSurvivesASave()
        {
            var simulation = Fresh(704);
            var model = Ship(simulation, "Atlas One", 47.0);

            for (var day = 0; day < 180; day++)
            {
                simulation.AdvanceDay();
            }

            var restored = SaveStore.Restore(
                SaveStore.Parse(JsonUtility.ToJson(SaveStore.Capture(simulation.State))));

            Assert.AreEqual(1, restored.DeployedModels.Count);
            var back = restored.DeployedModels[0];

            Assert.AreEqual(model.LifetimeRevenueUsd, back.LifetimeRevenueUsd,
                "Earnings cannot be recomputed from a later company state, so losing them on load "
                + "loses them permanently.");

            Assert.AreEqual(model.DaysOnSale, back.DaysOnSale);
            Assert.AreEqual(model.PeakUsers, back.PeakUsers, 1e-6);
        }

        [Test]
        public void AnOlderSaveStartsEveryModelAtZeroRatherThanGuessing()
        {
            var data = new SaveData { version = 22 };
            data.models.Add(new DeployedModelData { name = "Atlas One", capability = 40.0 });

            var upgraded = SaveMigration.UpgradeV22ToV23(data);

            Assert.AreEqual(0L, upgraded.models[0].lifetimeRevenueUsd,
                "Splitting lifetime revenue among models by a rule nobody can check would put a "
                + "confident wrong number on the one page whose job is to be trustworthy. Zero is "
                + "wrong too, and it is wrong in a way the page can explain.");
        }
    }
}
