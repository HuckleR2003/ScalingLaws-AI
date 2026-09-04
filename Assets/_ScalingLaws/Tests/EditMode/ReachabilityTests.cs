using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Content the simulation supports and the interface never offers.
    ///
    /// **This is the third distinct shape of the fault this project keeps shipping, and the one
    /// nothing was watching.** `UiWiringTests` asks whether a method has a caller. That catches a
    /// mechanism with no button. It cannot catch a mechanism whose button only ever passes one
    /// value, which is how three of the four server cabinets stayed complete, tested and impossible
    /// to buy for months: `TryPlaceRack` was called, correctly, with `ServerRack.Enclosed` and
    /// nothing else, ever.
    ///
    /// The rule here is deliberately loose in one direction and strict in the other. A catalog may
    /// be offered either by naming every member or by walking its `All`, because both are real
    /// designs; what it may not do is offer some members and quietly drop the rest.
    /// </summary>
    public sealed class ReachabilityTests
    {
        private static string UiText()
        {
            var folder = Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts", "UI");

            return string.Concat(Directory
                .GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        }

        private static string SimulationText()
        {
            var folder = Path.Combine(Application.dataPath, "_ScalingLaws", "Scripts");

            return string.Concat(Directory
                .GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        }

        /// <summary>
        /// Everything a player picks between, and the catalog that would offer it whole.
        ///
        /// Adding a row here is how a new choice gets covered. Leaving one out is how the next one
        /// ships unreachable.
        /// </summary>
        private static readonly (string Enum, string Catalog)[] Choices =
        {
            ("ServerRack", "ServerRackCatalog"),
            ("HostingPackage", "HostingCatalog"),
            ("LoanProduct", "LoanCatalog"),
            ("MarketingChannel", "MarketingCatalog"),
            ("StaffBenefit", "BenefitCatalog"),
            ("SmearTier", "SmearCatalog"),
        };

        /// <summary>
        /// Every option in a catalog can be chosen, by name or by the interface walking the whole
        /// list.
        ///
        /// The failure this exists for: `ServerRoomScreen` placed an enclosed rack and only ever an
        /// enclosed rack. Every test passed, the other three cabinets were correct, and the cooling
        /// trade the entire rack system was tuned around could not be made by anybody.
        /// </summary>
        [Test]
        public void EveryChoiceInACatalogCanActuallyBeChosen()
        {
            var ui = UiText();
            var everything = SimulationText();
            var unreachable = new List<string>();

            foreach (var (name, catalog) in Choices)
            {
                // Walking the whole catalog offers all of it, which is the common and better design.
                if (Regex.IsMatch(ui, Regex.Escape(catalog) + @"\.All\b"))
                {
                    continue;
                }

                var declaration = Regex.Match(
                    everything, @"enum\s+" + Regex.Escape(name) + @"\s*\{(.*?)\}", RegexOptions.Singleline);

                Assert.That(declaration.Success, Is.True, $"No enum named {name}.");

                foreach (Match value in Regex.Matches(
                    declaration.Groups[1].Value, @"^\s*([A-Z]\w*)", RegexOptions.Multiline))
                {
                    var member = value.Groups[1].Value;

                    if (member is "None" or "Unknown")
                    {
                        continue;
                    }

                    if (!Regex.IsMatch(ui, @"\b" + Regex.Escape(name) + @"\." + Regex.Escape(member) + @"\b"))
                    {
                        unreachable.Add($"{name}.{member}  (offered by {catalog})");
                    }
                }
            }

            Assert.IsEmpty(unreachable,
                "The simulation supports these and no control ever asks for them, so they are "
                + "content nobody can reach:\n  " + string.Join("\n  ", unreachable)
                + "\n\nOffer them by name, or walk the catalog.");
        }

        /// <summary>
        /// Every cabinet and fan operation is named somewhere in the interface.
        ///
        /// **This caught two things on the day it was written**, which is why it is here rather
        /// than in the fixture that tests the store room. `TrySellFan` was complete, tested and
        /// called from nowhere, and would have been the eighth mechanism in this project finished
        /// in the simulation and impossible for a player to reach. Then it caught `TryPlaceRack`,
        /// which was the opposite fault: not a missing button but a **second way to pay for a
        /// cabinet**, left behind when buying and placing were split, exercised only by tests.
        ///
        /// It proves a name appears, not that a click arrives. Worth stating plainly: a button
        /// wired to the wrong square would pass this. It is the cheap half of the check, and it is
        /// the half that keeps catching things.
        /// </summary>
        [Test]
        public void EveryCabinetOperationIsReachableFromTheInterface()
        {
            var ui = UiText();

            var operations = typeof(Simulation.CompanySimulation)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name)
                .Where(name => name.StartsWith("Try")
                               && (name.Contains("Rack") || name.Contains("Fan")))
                .Distinct()
                .ToList();

            Assert.IsNotEmpty(operations, "the sweep found nothing to sweep, which is not a pass");

            var unreachable = operations
                .Where(name => !Regex.IsMatch(ui, @"\." + Regex.Escape(name) + @"\s*\("))
                .ToList();

            Assert.IsEmpty(unreachable,
                "These are complete in the simulation and no screen calls them:\n  "
                + string.Join("\n  ", unreachable)
                + "\n\nEither wire a control to them, or delete them. A green suite does not prove "
                + "a player can reach the feature.");
        }

        /// <summary>
        /// Every tier on the compute ladder can actually be entered.
        ///
        /// **The third one could not, for months.** `TryOrderDatacenter` is complete: a gate on
        /// date, cash, models shipped and lifetime revenue, an eighty million dollar capex, a three
        /// hundred day lead time, its own power tariff and forty megawatts of capacity. Nothing in
        /// the interface ever called it. The ladder drew the row, said OPEN when the gate cleared,
        /// and offered nothing to press, so a whole layer of the infrastructure game was visible,
        /// unlocked, and unreachable.
        ///
        /// Written as a sweep over the entry points rather than as one assertion about the
        /// datacenter, because the next tier added will be added to the catalog and not to this
        /// list, and a test that names one tier would not notice.
        /// </summary>
        [Test]
        public void EveryComputeTierHasAWayIn()
        {
            var ui = UiText();
            var missing = new List<string>();

            foreach (var definition in Data.ComputeTierCatalog.All)
            {
                var tier = definition.Tier;

                // Rented capacity is contracted with a slider rather than entered, and a tier the
                // player is put into on day one needs no door at all.
                if (definition.IsRented || definition.FacilityCapexUsd <= 0L)
                {
                    continue;
                }

                if (!Regex.IsMatch(ui, @"\bComputeTier\." + Regex.Escape(tier.ToString()) + @"\b"))
                {
                    missing.Add($"{tier} is never named in Scripts/UI");
                }
            }

            Assert.IsEmpty(missing,
                "A tier with a capex is a thing the player signs for, so something has to offer "
                + "it:\n  " + string.Join("\n  ", missing));

            Assert.That(Regex.IsMatch(ui, @"TryOrderDatacenter\s*\("), Is.True,
                "Nothing in the interface commissions a datacenter, so the cheapest compute in the "
                + "game is unlocked and unreachable.");
        }

        /// <summary>
        /// The offer to buy the company can be answered.
        ///
        /// It arrives, it sits for forty five days and it expires. Both halves of the answer were
        /// written and neither was called from anywhere, so a player watched a ten figure offer
        /// come and go without ever being shown it.
        /// </summary>
        [Test]
        public void AnOfferToBuyTheCompanyCanBeAcceptedAndRefused()
        {
            var ui = UiText();

            Assert.That(Regex.IsMatch(ui, @"AcceptAcquisition\s*\("), Is.True,
                "Nothing in the interface accepts an acquisition offer.");

            Assert.That(Regex.IsMatch(ui, @"DeclineAcquisition\s*\("), Is.True,
                "Nothing in the interface refuses an acquisition offer.");

            Assert.That(Regex.IsMatch(ui, @"PendingAcquisition"), Is.True,
                "Nothing in the interface ever looks at whether an offer is open, so one can "
                + "arrive and expire without the player being told it existed.");
        }

        /// <summary>
        /// A corpus can be bought with money, which is a route the design had and never offered.
        ///
        /// `TryAcquireDataSource` was complete and tested from the day it was written and called
        /// from nowhere in `Scripts/UI/`. The DATA stage listed only corpora the company already
        /// owned, so an unowned one was not merely unbuyable: nothing anywhere in the game said it
        /// existed, what it cost, or what would open it.
        ///
        /// Both names are asserted. The buy alone would pass on a screen that offered every corpus
        /// and let the simulation refuse most of them, and the check alone would pass on a list
        /// nobody can act on.
        /// </summary>
        [Test]
        public void ACorpusCanBeBoughtWithMoneyAndTheScreenKnowsWhichOnes()
        {
            var ui = UiText();

            Assert.That(Regex.IsMatch(ui, @"TryAcquireDataSource\s*\("), Is.True,
                "Nothing in the interface buys a corpus, so the cash route to one is unreachable.");

            Assert.That(Regex.IsMatch(ui, @"CanAcquireDataSource\s*\("), Is.True,
                "The interface offers corpora without asking the simulation whether it would sell "
                + "one, which is a second copy of five conditions waiting to disagree.");
        }

        /// <summary>
        /// Every screen the shell can draw is opened by something.
        ///
        /// A `Screen` member with a case in the switch and no `Show` call anywhere is a page that
        /// exists, renders correctly and cannot be visited.
        /// </summary>
        [Test]
        public void EveryScreenHasSomethingThatOpensIt()
        {
            var ui = UiText();

            var declaration = Regex.Match(ui, @"enum Screen\s*\{(.*?)\n        \}",
                RegexOptions.Singleline);

            Assert.That(declaration.Success, Is.True, "No Screen enum found.");

            var orphans = new List<string>();

            foreach (Match value in Regex.Matches(
                declaration.Groups[1].Value, @"^\s*([A-Z]\w*)", RegexOptions.Multiline))
            {
                var screen = value.Groups[1].Value;

                if (!Regex.IsMatch(ui, @"Show\(\s*Screen\." + Regex.Escape(screen) + @"\s*\)")
                    && !Regex.IsMatch(ui, @"OpenScreenByName"))
                {
                    orphans.Add(screen);
                }
            }

            Assert.IsEmpty(orphans,
                "These screens are built and nothing opens them:\n  " + string.Join("\n  ", orphans));
        }
    }
}
