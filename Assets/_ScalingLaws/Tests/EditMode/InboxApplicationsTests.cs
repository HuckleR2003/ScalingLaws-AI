using NUnit.Framework;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The inbox can turn unasked applications away. From the author's list of 2026-09-18, and the
    /// v60 save that carries the choice.
    /// </summary>
    public sealed class InboxApplicationsTests
    {
        private static int Applications(CompanyState state)
        {
            var count = 0;

            foreach (var letter in state.Mail.All)
            {
                if (letter.Kind == MailKind.JobOffer)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void TurnedOffNoLetterArrivesAndTurnedOnTheyDo()
        {
            var refusing = new CompanySimulation(new CompanyState("Prometheus AI", 4242u));
            refusing.State.AcceptsApplications = false;

            var accepting = new CompanySimulation(new CompanyState("Prometheus AI", 4242u));

            for (var day = 0; day < 600; day++)
            {
                refusing.AdvanceDay();
                accepting.AdvanceDay();

                while (refusing.State.TryDequeueEvent(out _))
                {
                }

                while (accepting.State.TryDequeueEvent(out _))
                {
                }
            }

            Assert.That(Applications(accepting.State), Is.GreaterThan(0),
                "Six hundred days with the door open and nobody wrote, so this measures nothing.");
            Assert.That(Applications(refusing.State), Is.EqualTo(0),
                "The door was shut and applications still came in.");
        }

        [Test]
        public void TheChoiceSurvivesASave()
        {
            var state = new CompanyState("Prometheus AI", 4242u) { AcceptsApplications = false };

            var back = SaveStore.Restore(SaveStore.Parse(
                UnityEngine.JsonUtility.ToJson(SaveStore.Capture(state))));

            Assert.That(back.AcceptsApplications, Is.False);
        }

        [Test]
        public void AnOlderSaveKeepsReceivingThem()
        {
            var data = SaveStore.Capture(new CompanyState("Prometheus AI", 4242u));
            data.version = 59;
            data.refusesApplications = true; // whatever an older file holds in the new field

            var upgraded = SaveMigration.UpgradeV59ToV60(data);

            Assert.That(upgraded.version, Is.EqualTo(60));
            Assert.That(upgraded.refusesApplications, Is.False,
                "A v59 campaign had no way to refuse letters, so it received them.");
        }
    }
}
