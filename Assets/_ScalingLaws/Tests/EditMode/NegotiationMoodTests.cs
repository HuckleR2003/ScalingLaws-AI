using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The face on an offer, and the candidate who wants more for waiting.
    ///
    /// **The face is the only part of the hidden floor the player is allowed to see**, so the one
    /// thing it must never do is disagree with the answer. A face that says "they would sign" over
    /// an offer the send button then refuses is worse than no face at all: it teaches the player to
    /// stop trusting the screen.
    /// </summary>
    public sealed class NegotiationMoodTests
    {
        private static Candidate Somebody(uint seed = 7) =>
            Candidate.Roll(1, PlayerSkill.Software, HireSource.Agency, 50,
                new DeterministicRandom(seed));

        [Test]
        public void TheFaceNeverPromisesWhatTheVerdictRefuses()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var candidate = Somebody(seed);

                // Walk the whole range around their floor in small steps.
                for (var step = 0.5; step <= 1.4; step += 0.02)
                {
                    var offer = candidate.ReservationHourlyUsd * step;
                    var mood = Negotiation.MoodFor(candidate, offer, 0L);
                    var verdict = Negotiation.Judge(candidate, offer, 0L, 0);

                    if (mood is OfferMood.Delighted or OfferMood.Pleased)
                    {
                        Assert.That(verdict, Is.EqualTo(OfferVerdict.Accepted),
                            $"A happy face over an offer of {offer:N2} that was not accepted.");
                    }

                    if (mood == OfferMood.Insulted)
                    {
                        Assert.That(verdict, Is.EqualTo(OfferVerdict.WalkedAway),
                            $"An insulted face over an offer of {offer:N2} they did not leave over.");
                    }
                }
            }
        }

        [Test]
        public void TheFaceWarmsAsTheOfferRises()
        {
            var candidate = Somebody();

            var low = Negotiation.MoodFor(candidate, candidate.ReservationHourlyUsd * 0.6, 0L);
            var mid = Negotiation.MoodFor(candidate, candidate.ReservationHourlyUsd * 0.97, 0L);
            var high = Negotiation.MoodFor(candidate, candidate.ReservationHourlyUsd * 1.2, 0L);

            // The enum runs Delighted to Insulted, so warmer is a smaller number.
            Assert.That((int)high, Is.LessThan((int)mid));
            Assert.That((int)mid, Is.LessThan((int)low));
        }

        [Test]
        public void ABonusWarmsTheFaceToo()
        {
            var candidate = Somebody(21);
            var wage = candidate.ReservationHourlyUsd * 0.95;

            var bare = Negotiation.MoodFor(candidate, wage, 0L);
            var sweetened = Negotiation.MoodFor(candidate, wage, 60_000L);

            Assert.That((int)sweetened, Is.LessThan((int)bare),
                "The bonus counts towards the offer, so it has to count towards the face.");
        }

        [Test]
        public void EveryMoodHasAFaceAndAThingItSays()
        {
            foreach (OfferMood mood in System.Enum.GetValues(typeof(OfferMood)))
            {
                var (face, says) = Negotiation.Portrait(mood);

                Assert.That(face, Is.Not.Empty, $"{mood} has no face.");
                Assert.That(says, Is.Not.Empty, $"{mood} says nothing.");
            }
        }

        [Test]
        public void RaisingTheAskMovesTheFloorWithIt()
        {
            var candidate = Somebody(33);

            var askBefore = candidate.AskingHourlyUsd;
            var floorBefore = candidate.ReservationHourlyUsd;

            candidate.RaiseTheAsk(Negotiation.ImpatienceRaise);

            Assert.That(candidate.AskingHourlyUsd, Is.GreaterThan(askBefore));
            Assert.That(candidate.ReservationHourlyUsd, Is.GreaterThan(floorBefore),
                "Somebody who talks themselves up does not then sign for the old number.");
        }

        [Test]
        public void HoldingFirmSometimesCostsTheCompanyMoney()
        {
            // Across many candidates, at least one refused round has to produce a raise, and at
            // least one has to not: a mechanic that fires every time is a tax, and one that never
            // fires is dead code.
            var raised = 0;
            var held = 0;

            for (uint seed = 1; seed <= 40; seed++)
            {
                var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
                simulation.State.CashUsd = 40_000_000;
                simulation.State.Staff.SetOffice(OfficeTier.Floor);
                simulation.State.Hiring.Random.State = seed;

                var candidate = simulation.Shortlist(PlayerSkill.Software, HireSource.Agency, 50, 1)
                    .First();

                simulation.TryApproach(candidate);

                MailItem letter = null;
                for (var day = 0; day < 12 && letter == null; day++)
                {
                    simulation.AdvanceDay();
                    letter = simulation.State.Mail.All
                        .FirstOrDefault(item => item.Candidate != null && !item.IsClosed);
                }

                Assert.That(letter, Is.Not.Null);

                var before = letter.Candidate.AskingHourlyUsd;
                var verdict = simulation.Negotiate(letter, before * 0.90, 0L, out _);

                if (verdict != OfferVerdict.HeldFirm)
                {
                    continue;
                }

                if (letter.Candidate.AskingHourlyUsd > before)
                {
                    raised++;
                }
                else
                {
                    held++;
                }
            }

            Assert.That(raised, Is.GreaterThan(0),
                "Nobody ever asked for more, so waiting costs the company nothing.");

            Assert.That(held, Is.GreaterThan(0),
                "Everybody asked for more, which makes pressing back a flat tax rather than a risk.");
        }
    }
}
