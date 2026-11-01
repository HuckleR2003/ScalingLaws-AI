using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// A number the rules compute, print, and never use.
    ///
    /// **This is the third shape of unreachable and nothing was watching for it.** The two that
    /// were already guarded are a mutator in `Simulation/` with no caller in `UI/`, which the
    /// regex sweep in `UiWiringTests` and the audit note in CLAUDE.md cover, and a control in
    /// `UI/` that never reaches `Simulation/`, which is found by playing. This one is worse than
    /// either, because everything about it looks right: the figure is real, it is correct, it is
    /// drawn on a screen with a label beside it, and it enters no rule. A player reads it, plans
    /// around it, and the game does not agree.
    ///
    /// Two instances in two days, both found by a tester rather than by a test:
    ///
    /// - `AlliedPetaflops`, 2026-10-31. Allied capacity was summed, shown, and never added to
    ///   anything the market served from. `AllianceTests` asserted it was above zero and passed
    ///   for months, which is the exact failure a test can have: it measured that the arithmetic
    ///   worked and never asked what the arithmetic was for.
    /// - `TrainingRun.ActualTokensBillions`, 2026-11-01. Clamped in the constructor, written into
    ///   the save, and read by nothing in the rules.
    ///
    /// ### What this sweep is, and what it deliberately is not
    ///
    /// It reads the source of `Scripts/Simulation/` and lists every public get-only member that
    /// nothing else inside `Scripts/Simulation/` reads. An assignment is not a read, which is the
    /// whole trick: the first version of this counted the constructor line that clamps
    /// `ActualTokensBillions` as a use and therefore missed the one instance known to be dead.
    ///
    /// **It cannot tell a fault from a report, and it does not try.** Most of the baseline below
    /// is correct by design: `CompanySnapshot`, `DayReport`, `ModelRow` and `ProductStanding`
    /// exist to be read by the interface and by nothing else, and a member of one of those being
    /// unread inside the rules is what they are for. The difference between those and
    /// `AlliedPetaflops` is whether the number claims to do something, and that is a judgement no
    /// regex makes.
    ///
    /// So this is **a ratchet, not a cleanup**. The baseline is what was measured on 2026-11-01
    /// and the test fails on the hundred and thirty first, which is the one somebody is about to
    /// write. Adding a line is a deliberate act and the reviewer gets to ask what the number is
    /// for. Emptying the list is not the goal and never was.
    ///
    /// ### What it does not look at, said out loud
    ///
    /// **Get-only members only, never methods.** Both instances found by hand were properties, so
    /// that is where the evidence is, and widening this to every value-returning method would
    /// multiply the baseline by something nobody would read. The limit is written down because the
    /// alternative is a fixture quoted as covering a layer it half covers. If a method-shaped
    /// instance ever turns up, that is the day to widen it.
    ///
    /// **The first thing this guard failed on was a line added to the baseline by hand**, for a
    /// method, which the sweep does not scan and therefore can never report as unread. That is the
    /// second half of the ratchet earning its place: the list may not describe code the sweep is
    /// not looking at either, or it becomes a document that claims cover it does not have.
    /// </summary>
    public sealed class NumbersTheRulesNeverReadTests
    {
        private static string SimulationFolder =>
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "Simulation");

        /// <summary>A public member with a getter and no public setter, which is a computation.</summary>
        private static readonly Regex Declaration = new(
            @"public\s+(?:static\s+)?(?:readonly\s+)?([\w<>\[\],\.\?]+)\s+(\w+)\s*(?:=>|\{\s*get;?\s*(?:private\s+set;\s*)?\})",
            RegexOptions.Compiled);

        /// <summary>What sits in front of a name when the name is being declared rather than read.</summary>
        private static readonly Regex DeclarationTail = new(
            @"public\s+(?:static\s+)?(?:readonly\s+)?[\w<>\[\],\.\?]+\s+$", RegexOptions.Compiled);

        private static readonly Regex Word = new(@"\w+", RegexOptions.Compiled);

        /// <summary>
        /// Measured 2026-11-01 by this fixture, never typed from memory.
        ///
        /// **Most of these are right.** See the note on the class: a report type's whole job is to
        /// be read somewhere else. The ones below that are faults rather than reports are marked,
        /// and marking them here is the only record that anybody looked.
        /// </summary>
        private static readonly string[] KnownUnread =
        {
            // **The one known fault this list carried is gone**, deleted on 2026-11-01 rather
            // than shown, because it was measured to be a copy of a figure the same record
            // already held rather than information about anything. The ratchet only tightens.


            // ---- everything else measured on the day the sweep was written ----
            "AcquisitionOffer.MadeOn",
            "AcquisitionOffer.ValuationMultiple",
            "ArchitectureDesigner.ComputeSavingVersusBaseline",
            "ArchitectureProject.CalendarProgress",
            "ArchitectureProject.IsWaitingForCompute",
            "Candidate.PortraitSeed",
            "CapTable.HasBoardControl",
            "CapTable.Holders",
            "CapTable.IsUnderInvestorMandate",
            "CapTable.Rounds",
            "CapTable.WasDownRound",
            "CompanySimulation.Alliances.RenewalIsOnTheTable",
            "CompanySnapshot.CapabilityGap",
            "CompanySnapshot.IsTraining",
            "CompanySnapshot.MarketDemandBillionTokensPerDay",
            "CompanySnapshot.MarketPricePerMillionTokensUsd",
            "CompanySnapshot.NetWorthUsd",
            "CompanySnapshot.TrainingModelName",
            "CompanySnapshot.TrainingProgress",
            "CompanySnapshot.TrainingProjectedCapability",
            "CompanyState.HasQueuedEvents",
            "CompanyState.HousingTier",
            "CompanyState.Messages",
            "CompanyState.PendingEventCount",
            "CompanyState.Signals",
            "CompetitorAgent.PendingCapabilityAdjustment",
            "CompetitorAgent.PlannedReleasesRemaining",
            "ComputePool.RentedAndPackagedPetaflops",
            "ComputeProfile.AcceleratorsSupported",
            "ComputeProfile.IsOverPowerBudget",
            "ComputeProfile.RealizedEfficiency",
            "ComputeProfile.RoomPetaflops",
            "ComputeProfile.StarvingClass",
            "ComputeProfile.TotalCostPerPetaflopDayUsd",
            "DayReport.CapabilityGap",
            "DayReport.CashFlowUsd",
            "DayReport.EconomicProfitUsd",
            "DayReport.TrainingProgress",
            "DayReport.UnservedBillionTokens",
            "DecorPlan.Items",
            "DecorPlan.Stored",
            "DeployedModel.RetiredOn",
            "EventAuction.DroppedOut",
            "EventAuction.Number",
            "EventAuction.StillIn",
            "EventSeason.GoesToAuction",
            "EventStand.ViralShare",
            "FundingMarket.DollarsPerEquityPoint",
            "FundingMarket.Refusal",
            "GuideProgress.IsStillRunning",
            "HiringDesk.Approaches",
            "HiringDesk.Soonest",
            "HiringDesk.StartedDayIndex",
            "IntelligenceService.IsCorrect",
            "LabAlliances.EverSigned",
            "LabAlliances.Outcome",
            "Lawsuit.AwardedUsd",
            "Lawsuit.CostsUsd",
            "Lawsuit.Grounds",
            "Ledger.Group",
            "Ledger.HasAnything",
            "Ledger.HasRecordedDays",
            "Ledger.LastRecordedDayIndex",
            "Ledger.Lines",
            "Mailbox.Arrived",
            "Mailbox.NeedsAnswer",
            "Mailbox.Sender",
            "MarketConditions.DailyMarketRevenueUsd",
            "MarketConditions.RentPricePerPetaflopDayUsd",
            "Messenger.Lines",
            "Messenger.Mine",
            "ModelBlueprint.HasDataProtection",
            "ModelBlueprint.TrainingTokens",
            "ModelRecord.CanRetire",
            "ModelRecord.Index",
            "ModelRecord.StateWord",
            "ModelRow.Subscribers",
            "ModelTraitSet.IsBehindMarket",
            "ModelTraitSet.IsMaxed",
            "ModelUpgradeProject.DaysRemaining",
            "ModelUpgradeProject.IsBatch",
            "MonetizationPolicy.PaidShareOfTokens",
            "NewsFeed.Outlet",
            "ProductStanding.Freshness",
            "ProductStanding.Happiness",
            "ProductStanding.IsProfitable",
            "ProductStanding.MonthEarningsUsd",
            "ProductStanding.OwnLifetimeUsd",
            "ProductStanding.OwnRecentUsd",
            "ProductStanding.Subscribers",
            "RegulatoryAction.Subtitle",
            "ReleaseLine.FreeTokensPerDay",
            "ReleaseLine.ReleasedOn",
            "ReleaseLine.Versions",
            "ResearchProject.CalendarProgress",
            "ResearchProject.IsInProgress",
            "ResearchProject.IsWaitingForCompute",
            "RivalRelations.Delta",
            "RivalRelations.History",
            "RivalRelations.Known",
            "RivalRelations.ReasonKey",
            "SegmentMarket.LastEntrantCount",
            "SegmentMarket.LeaderIndex",
            "SegmentMarket.LeaderName",
            "SegmentMarket.OwnerCount",
            "SegmentMarket.OwnerShares",
            "SegmentMarket.PlayerTokensPerDay",
            "SegmentMarket.RivalCount",
            "SegmentMarket.UnservedShare",
            "ServerHall.FreeSquares",
            "ServerHall.IsHealthy",
            "SkillSet.IsMaxed",
            "StateProgramme.LastDelivery",
            "StateProgramme.Signatory",
            "StateProgramme.SignedOn",
            "TrainingProfile.BandPosition",
            "TrainingProfile.BudgetEfficiency",
            "TrainingProfile.Notes",
            "TrainingProfile.ProfileName",
            "TrainingProjection.ComputeEconomicCostUsd",
            "TrainingProjection.IsOvertrained",
            "TrainingProjection.IsUndertrained",
            "TrainingProjection.ProjectedLoss",
            "UpgradeSubject.Index",
            "UpgradeSubject.VersionName",
            "UserSentiment.BestRivalUsers",
            "UserSentiment.Mood",
            "WorkInFlight.Busy",
            "WorkInFlight.Caption",
        };

        private readonly struct CodeLine
        {
            public CodeLine(string file, int number, string code)
            {
                File = file;
                Number = number;
                Code = code;
            }

            public string File { get; }
            public int Number { get; }
            public string Code { get; }
        }

        /// <summary>
        /// Every line of the rules with its comment stripped.
        ///
        /// Stripped by line rather than by pattern, because the first version tested the
        /// twenty four characters in front of a match for comment marks and threw away every read
        /// written as a multiplication: `a * b` carries a star and a space.
        /// </summary>
        private static List<CodeLine> Rules()
        {
            var lines = new List<CodeLine>();

            foreach (var file in Directory.GetFiles(SimulationFolder, "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var text = File.ReadAllLines(file);

                for (var i = 0; i < text.Length; i++)
                {
                    var cut = text[i].IndexOf("//", StringComparison.Ordinal);
                    lines.Add(new CodeLine(name, i + 1, cut >= 0 ? text[i].Substring(0, cut) : text[i]));
                }
            }

            return lines;
        }

        /// <summary>
        /// The sweep. Returns `Type.Member` for every computation the rules never read back.
        ///
        /// Indexed by word once rather than searched per name: eight hundred and seventy five
        /// declarations against twenty thousand lines is twenty million regex passes done the
        /// obvious way, and a guard nobody will wait for is a guard somebody switches off.
        /// </summary>
        private static List<string> Unread(out int declarations)
        {
            var lines = Rules();

            Assert.That(lines, Is.Not.Empty,
                "No sources found under Scripts/Simulation, so this sweep is measuring nothing.");

            var occurrences = new Dictionary<string, List<(int Line, int At)>>();

            for (var i = 0; i < lines.Count; i++)
            {
                foreach (Match word in Word.Matches(lines[i].Code))
                {
                    if (!occurrences.TryGetValue(word.Value, out var list))
                    {
                        list = new List<(int, int)>();
                        occurrences[word.Value] = list;
                    }

                    list.Add((i, word.Index));
                }
            }

            var declared = new List<(string Owner, string Name)>();

            foreach (var line in lines)
            {
                var match = Declaration.Match(line.Code);

                if (match.Success && match.Groups[1].Value != "void")
                {
                    declared.Add((line.File, match.Groups[2].Value));
                }
            }

            declarations = declared.Count;
            var unread = new List<string>();

            foreach (var (owner, name) in declared)
            {
                var reads = 0;

                if (occurrences.TryGetValue(name, out var found))
                {
                    foreach (var (index, at) in found)
                    {
                        var code = lines[index].Code;
                        var before = code.Substring(0, at);
                        var after = code.Substring(at + name.Length);

                        // The declaration itself is not a read of itself.
                        if (DeclarationTail.IsMatch(before))
                        {
                            continue;
                        }

                        // **A write is not a read**, and this is the line that makes the sweep
                        // work. Everything in the baseline that is genuinely dead is clamped or
                        // assigned somewhere, usually in the constructor of the type that holds it.
                        if (Regex.IsMatch(after, @"^\s*=[^=]"))
                        {
                            continue;
                        }

                        reads++;
                        break;
                    }
                }

                if (reads == 0)
                {
                    var entry = owner + "." + name;

                    if (!unread.Contains(entry))
                    {
                        unread.Add(entry);
                    }
                }
            }

            unread.Sort(StringComparer.Ordinal);
            return unread;
        }

        /// <summary>
        /// Nothing new may be added to the pile.
        ///
        /// The message names what to do, because a failure that only says a count went up gets
        /// fixed by raising the count.
        /// </summary>
        [Test]
        public void NoNewComputationIsAddedThatTheRulesDoNotRead()
        {
            var unread = Unread(out var declarations);
            var known = new HashSet<string>(KnownUnread, StringComparer.Ordinal);
            var fresh = unread.Where(entry => !known.Contains(entry)).ToList();

            Debug.Log($"[sweep] {declarations} public computations in the rules, {unread.Count} "
                + $"of them read nowhere inside the rules, {fresh.Count} of those new.");

            Assert.That(fresh, Is.Empty,
                "These compute a number that nothing in Simulation/ reads back:\n  "
                + string.Join("\n  ", fresh)
                + "\n\nTwo questions, and only the second one is answered by adding a line to "
                + "KnownUnread. Does a rule need this figure, in which case wire it, because a "
                + "number on a screen that enters no rule is the fault that shipped as "
                + "AlliedPetaflops and as TrainingRun.ActualTokensBillions. Or is it a reading for "
                + "the interface and nothing else, the way DayReport and CompanySnapshot are, in "
                + "which case say so on the line you add.");
        }

        /// <summary>
        /// And the pile may not rot either.
        ///
        /// An entry that is now read is a fixed fault or a changed design, and leaving it listed
        /// means the next person reads the list as current when it is not. This project has found
        /// a stale claim in its own documents five times.
        /// </summary>
        [Test]
        public void TheBaselineStillDescribesTheCodeItWasMeasuredAgainst()
        {
            var unread = new HashSet<string>(Unread(out _), StringComparer.Ordinal);
            var gone = KnownUnread.Where(entry => !unread.Contains(entry)).ToList();

            Assert.That(gone, Is.Empty,
                "These are listed as read by nothing and the rules read them now, so the list is "
                + "describing yesterday:\n  " + string.Join("\n  ", gone)
                + "\n\nDelete those lines from KnownUnread. The ratchet only tightens.");
        }

        /// <summary>
        /// The sweep itself, measured against the instance it was written for.
        ///
        /// **Without this the fixture could pass by finding nothing at all**, which is how the
        /// first version of it passed: it counted the constructor assignment as a use and reported
        /// the one member known to be dead as healthy. A sweep with a silent hole in it is worse
        /// than no sweep, because it is quoted as cover.
        /// </summary>
        /// <summary>
        /// The sweep still finds things, and the thing it found first is gone because it found it.
        ///
        /// **This used to assert that `TrainingRun.ActualTokensBillions` was still in the list**,
        /// which was right for one day: it was the member this sweep was written to catch and the
        /// proof the sweep had no hole in it. It was then measured to be a copy rather than a
        /// fact and deleted, so the assertion had to change with it, and the fixture's own doc
        /// said so in advance.
        ///
        /// What is left is the guard against the sweep passing by finding nothing at all, which is
        /// how its first version passed: it counted a constructor clamping its own field as a use.
        /// Two numbers with wide bands, because the point is to catch a pattern that has stopped
        /// matching rather than to pin a count that moves every time somebody adds a property.
        /// </summary>
        [Test]
        public void TheSweepIsStillLookingAtSomething()
        {
            var unread = Unread(out var declarations);

            Assert.That(declarations, Is.GreaterThan(500),
                $"Only {declarations} public computations were found in the rules, which is too "
                + "few for this layer. The declaration pattern has stopped matching.");

            Assert.That(unread.Count, Is.GreaterThan(50),
                $"The sweep reports only {unread.Count} unread computations against a baseline of "
                + "about 130. A sweep that suddenly finds almost nothing has broken rather than "
                + "been satisfied, and a broken one gets quoted as cover.");
        }
    }
}
