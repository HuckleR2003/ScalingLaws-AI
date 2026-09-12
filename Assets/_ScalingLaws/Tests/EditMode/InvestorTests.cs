using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Who owns the company, and who is offering to.
    ///
    /// **Reported: there was one offer, from nobody, and a percentage with no names against it.** An
    /// offer that cannot be compared with anything is a yes or a no rather than a decision, and a
    /// founder's share with nothing beside it makes an investor an abstraction the player dilutes
    /// themselves against rather than somebody on their board.
    /// </summary>
    public sealed class InvestorTests
    {
        private static CompanySimulation Fresh() =>
            new(new CompanyState("Prometheus AI"));

        /// <summary>
        /// **The line worth noticing on day one.** Somebody believed in this before there was
        /// anything to believe in, and the bar says so before the player has raised anything.
        /// </summary>
        [Test]
        public void AFreshCompanyIsNotWhollyOwnedByItsFounders()
        {
            var table = Fresh().State.CapTable;

            Assert.That(table.Holders, Has.Count.EqualTo(1),
                "Nobody is on the register, so the ownership bar is one colour and says nothing.");

            Assert.That(table.Holders[0].Investor, Is.EqualTo(InvestorId.ESolutions));

            Assert.That(table.Holders[0].Fraction,
                Is.EqualTo(InvestorCatalog.FriendsAndFamilyStake).Within(1e-9));

            Assert.That(table.FounderEquity + table.InvestorEquity, Is.EqualTo(1.0).Within(1e-9),
                "The founders and the named holders between them do not own the company.");
        }

        /// <summary>
        /// **New shares dilute everybody, which is what makes the bar add up.** The first version of
        /// this took the slice off the founders twice, once against the register and once against
        /// the founder figure, and the bar came out over a hundred per cent.
        /// </summary>
        [Test]
        public void IssuingSharesDilutesEveryoneAlreadyOnTheRegister()
        {
            var table = Fresh().State.CapTable;
            var emilBefore = table.Holders[0].Fraction;

            table.Issue(InvestorId.SequelPartners, 0.20, 30_000_000L, GameDate.Start);

            Assert.That(table.Holders, Has.Count.EqualTo(2), "The new name is not on the register.");

            var emil = table.Holders.First(h => h.Investor == InvestorId.ESolutions);

            Assert.That(emil.Fraction, Is.EqualTo(emilBefore * 0.80).Within(1e-9),
                "The existing holder was not diluted, so the company now adds up to more than "
                + "itself.");

            Assert.That(table.FounderEquity + table.InvestorEquity, Is.EqualTo(1.0).Within(1e-9),
                "The register does not add up to one company.");
        }

        /// <summary>A second cheque from the same name tops up their row rather than adding one.</summary>
        [Test]
        public void OneNameIsOneRowHoweverManyTimesTheyInvest()
        {
            var table = Fresh().State.CapTable;

            table.Issue(InvestorId.SequelPartners, 0.15, 20_000_000L, GameDate.Start);
            table.Issue(InvestorId.SequelPartners, 0.10, 40_000_000L, GameDate.Start);

            var rows = table.Holders.Count(h => h.Investor == InvestorId.SequelPartners);

            Assert.That(rows, Is.EqualTo(1),
                "The same firm is on the register twice, so the screen is the only thing that "
                + "knows they are one investor.");

            var sequel = table.Holders.First(h => h.Investor == InvestorId.SequelPartners);

            Assert.That(sequel.PaidUsd, Is.EqualTo(60_000_000L),
                "What they have put in was not added up across their rounds.");
        }

        /// <summary>
        /// **None of the twelve is simply better.** The moment one is, twelve names collapse into
        /// one and the book stops being a choice. Same guard the marketing channels carry.
        /// </summary>
        [Test]
        public void NoInvestorIsSimplyBetterThanAnother()
        {
            var all = InvestorCatalog.All;

            for (var left = 0; left < all.Count; left++)
            {
                for (var right = 0; right < all.Count; right++)
                {
                    if (left == right)
                    {
                        continue;
                    }

                    var a = all[left];
                    var b = all[right];

                    // Paying more, writing more and waiting longer, all at once, with nothing asked
                    // in return. Ties are fine; strictly better on every axis is not.
                    var dominates = a.Appetite >= b.Appetite
                        && a.TicketShare >= b.TicketShare
                        && a.PatienceDays >= b.PatienceDays
                        && (a.Appetite > b.Appetite
                            || a.TicketShare > b.TicketShare
                            || a.PatienceDays > b.PatienceDays);

                    Assert.That(dominates, Is.False,
                        $"{a.Id} is better than {b.Id} on price, size and patience at once, so "
                        + "nobody would ever take the second one.");
                }
            }
        }

        [Test]
        public void EveryInvestorHasItsOwnNameAndItsOwnPitch()
        {
            var names = InvestorCatalog.All.Select(entry => entry.DisplayName).ToList();
            var pitches = InvestorCatalog.All.Select(entry => entry.Pitch).ToList();

            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                "Two investors share a name: " + string.Join(", ", names));

            Assert.That(pitches.Distinct().Count(), Is.EqualTo(pitches.Count),
                "Two investors share a pitch, so one is drawing another's words.");
        }

        /// <summary>
        /// **Two to twenty days, measured rather than asserted.** A cadence nothing checks is a
        /// constant with a comment on it.
        /// </summary>
        [Test]
        public void CallersArriveBetweenTwoAndTwentyDaysApart()
        {
            const uint Seed = 0x5CA1AB1E;

            var gaps = new List<int>();
            var last = 0;

            for (var day = 1; day < 4_000; day++)
            {
                if (!InvestorDesk.SomebodyCallsOn(Seed, new GameDate(day), last))
                {
                    continue;
                }

                gaps.Add(day - last);
                last = day;
            }

            Assert.That(gaps, Is.Not.Empty, "Nobody ever calls, so the book is always empty.");

            Assert.That(gaps.Min(), Is.GreaterThanOrEqualTo(InvestorDesk.SoonestGapDays),
                "Somebody turned up after " + gaps.Min() + " days, under the floor.");

            Assert.That(gaps.Max(), Is.LessThanOrEqualTo(InvestorDesk.LongestGapDays),
                "A quiet stretch ran " + gaps.Max() + " days, over the ceiling.");

            // And the gap is drawn rather than fixed, or the cadence is a metronome.
            Assert.That(gaps.Distinct().Count(), Is.GreaterThan(3),
                "Every gap is the same length, so nothing is being drawn.");
        }

        /// <summary>The same seed and the same days give the same callers, or a reload re-rolls.</summary>
        [Test]
        public void TheScheduleReplaysIdentically()
        {
            static List<int> Days(uint seed)
            {
                var found = new List<int>();
                var last = 0;

                for (var day = 1; day < 900; day++)
                {
                    if (!InvestorDesk.SomebodyCallsOn(seed, new GameDate(day), last))
                    {
                        continue;
                    }

                    found.Add(day);
                    last = day;
                }

                return found;
            }

            Assert.That(Days(4242u), Is.EqualTo(Days(4242u)).AsCollection,
                "The same campaign produced two different sets of callers, so a reload re-rolls "
                + "who turns up.");
        }

        [Test]
        public void TheBookNeverHoldsMoreThanTenAndNeverTheSameNameTwice()
        {
            var desk = new InvestorDesk();
            var today = GameDate.Start;

            foreach (var definition in InvestorCatalog.All)
            {
                desk.Add(new FundingOffer(FundingStage.SeriesA, today, today.AddDays(30),
                    10_000_000L, 100_000_000L, 0.09, 1.0, false, definition.Id));

                // Offered twice by the same firm, which is what a long campaign will try.
                desk.Add(new FundingOffer(FundingStage.SeriesA, today, today.AddDays(30),
                    10_000_000L, 100_000_000L, 0.09, 1.0, false, definition.Id));
            }

            Assert.That(desk.Count, Is.LessThanOrEqualTo(InvestorDesk.MostOpenOffers),
                "The book holds " + desk.Count + " term sheets, which is a list rather than a "
                + "choice.");

            var names = desk.Open.Select(offer => offer.Investor).ToList();

            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                "The same firm is offering twice at once.");
        }

        /// <summary>Nobody waits forever, and what lapsed is gone rather than still clickable.</summary>
        [Test]
        public void OffersAreWithdrawnWhenTheirTimeIsUp()
        {
            var desk = new InvestorDesk();
            var today = GameDate.Start;

            desk.Add(new FundingOffer(FundingStage.SeriesA, today, today.AddDays(10),
                10_000_000L, 100_000_000L, 0.09, 1.0, false, InvestorId.TigrisGlobal));

            Assert.That(desk.Withdraw(today.AddDays(9)), Is.Zero, "An offer lapsed early.");
            Assert.That(desk.Count, Is.EqualTo(1));

            Assert.That(desk.Withdraw(today.AddDays(11)), Is.EqualTo(1),
                "An offer nobody took is still on the table after its date.");

            Assert.That(desk.Count, Is.Zero);
        }

        /// <summary>
        /// **A v52 campaign that never raised gets the favour stated; one that raised does not.**
        /// The old file records one number and nothing about who holds the rest, so naming the other
        /// holders after the fact would be inventing a figure nobody can check.
        /// </summary>
        [Test]
        public void AnOlderSaveGetsTheFavourOnlyWhereItCanBeStated()
        {
            var untouched = SaveMigration.UpgradeV52ToV53(new SaveData
            {
                version = 52,
                founderEquity = 1.0
            });

            Assert.That(untouched.version, Is.EqualTo(53));
            Assert.That(untouched.holderInvestors, Has.Count.EqualTo(1),
                "A company that never raised did not get Emil's slice back.");

            Assert.That(untouched.founderEquity,
                Is.EqualTo(1.0 - InvestorCatalog.FriendsAndFamilyStake).Within(1e-9));

            var raised = SaveMigration.UpgradeV52ToV53(new SaveData
            {
                version = 52,
                founderEquity = 0.64,
                fundingRounds = { new FundingRoundData { stage = 1, raisedUsd = 25_000_000 } }
            });

            Assert.That(raised.holderInvestors, Is.Empty,
                "A campaign that has already raised had names invented for its register.");

            Assert.That(raised.founderEquity, Is.EqualTo(0.64).Within(1e-9),
                "The founders' share was rewritten under a player who had already diluted.");
        }
    }
}
