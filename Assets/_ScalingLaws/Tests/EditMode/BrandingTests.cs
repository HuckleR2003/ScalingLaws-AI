using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The company's mark is the same mark everywhere, and it follows the company.
    ///
    /// **A brand that is derived rather than stored is only worth anything if it is stable.** The
    /// mark, its colour and its letter all come out of the company name, so nothing is saved and
    /// nothing can drift; the risk is the opposite one, that it drifts anyway because the derivation
    /// is not actually deterministic. `string.GetHashCode` is randomised per process on modern
    /// runtimes, so a naive hash would give the same company a different colour on every launch,
    /// which is exactly the class of fault nobody reports and everybody notices.
    /// </summary>
    public sealed class BrandingTests
    {
        [Test]
        public void TheSameNameAlwaysGivesTheSameColour()
        {
            var first = BrandMark.Ink("Prometheus AI");
            var second = BrandMark.Ink("Prometheus AI");

            Assert.That(second, Is.EqualTo(first),
                "The mark has to be the same colour on the creator, the model hub and the product "
                + "page, and the same colour tomorrow.");
        }

        [Test]
        public void DifferentCompaniesLookDifferent()
        {
            var names = new[]
            {
                "Prometheus AI", "Newco", "Aurora Labs", "HCK", "x", "Zenith Systems"
            };

            var inks = names.Select(BrandMark.Ink).ToList();

            for (var left = 0; left < inks.Count; left++)
            {
                for (var right = left + 1; right < inks.Count; right++)
                {
                    var apart = System.Math.Abs(inks[left].r - inks[right].r)
                        + System.Math.Abs(inks[left].g - inks[right].g)
                        + System.Math.Abs(inks[left].b - inks[right].b);

                    Assert.That(apart, Is.GreaterThan(0.05f),
                        $"\"{names[left]}\" and \"{names[right]}\" come out the same colour, so two "
                        + "companies would be indistinguishable in a ranking table.");
                }
            }
        }

        /// <summary>
        /// A name made of anything at all still produces a letter rather than an empty ring.
        /// </summary>
        [Test]
        public void EveryNameGetsALetter()
        {
            foreach (var name in new[] { "Prometheus", "  spaced", "4chan", "!!!", "", null })
            {
                var initial = BrandMark.InitialOf(name);

                Assert.That(initial, Is.Not.Empty,
                    $"\"{name}\" produced nothing, which draws as an empty ring and reads as a "
                    + "failed load.");
                Assert.That(initial.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void TheMarkFollowsTheCompanyItIsGiven()
        {
            var mark = new BrandMark { Company = "Prometheus AI" };
            var letter = mark.Q<Label>(className: "mark__letter");

            Assert.That(letter, Is.Not.Null);
            Assert.That(letter.text, Is.EqualTo("P"));

            mark.Company = "Zenith Systems";
            Assert.That(letter.text, Is.EqualTo("Z"),
                "Renaming the company has to re-letter the mark, or a renamed company keeps its old "
                + "initial on every screen until the game is restarted.");
        }

        /// <summary>
        /// The browser mock says the company, the product and the founder, and builds an address.
        /// </summary>
        [Test]
        public void TheBrowserShowsTheCompanyTheProductAndTheFounder()
        {
            var browser = new BrowserPreview();
            browser.Show("Prometheus AI", "Muse 1", "Marcin");

            var text = string.Join(" | ",
                browser.Root.Query<Label>().ToList().Select(label => label.text));

            Assert.That(text, Does.Contain("PROMETHEUS AI"), "The company is missing from the rail.");
            Assert.That(text, Does.Contain("Muse 1"), "The product is missing from the page.");
            Assert.That(text, Does.Contain("Marcin"),
                "The founder's own name is the one part of this that is about the player.");
            Assert.That(text, Does.Contain("prometheusai.ai"),
                "The address is built from the company name with everything a domain cannot carry "
                + "taken out.");
        }

        [Test]
        public void AnEmptyCompanyStillProducesAPage()
        {
            var browser = new BrowserPreview();
            browser.Show(string.Empty, null, null);

            var text = string.Join(" | ",
                browser.Root.Query<Label>().ToList().Select(label => label.text));

            Assert.That(text, Does.Contain("NEWCO"),
                "A company with no name has to fall back to something rather than render blank.");
        }

        /// <summary>
        /// The two arrows exist and neither of them does anything.
        ///
        /// **That is the feature, not a shortcut.** Choosing a mark is a decision the game does not
        /// offer yet, and showing where it will live is the difference between a player who knows it
        /// is coming and one who never finds out.
        /// </summary>
        [Test]
        public void TheMarkArrowsAreThereAndLocked()
        {
            var browser = new BrowserPreview();
            browser.Show("Prometheus AI", "Muse 1", "Marcin");

            var arrows = browser.Root.Query<Button>(className: "wb__arrow").ToList();

            Assert.That(arrows.Count, Is.EqualTo(2), "One arrow each side of the mark.");

            foreach (var arrow in arrows)
            {
                Assert.IsFalse(arrow.enabledSelf,
                    "A live arrow that changes nothing is worse than no arrow at all.");
                Assert.That(arrow.tooltip, Is.Not.Empty,
                    "A dead control with no explanation reads as a bug.");
            }
        }

        /// <summary>
        /// The chip carries the mark, so the thing designed in the creator is the thing in the list.
        /// </summary>
        [Test]
        public void TheChipCarriesTheCompanyMark()
        {
            var chip = ChipPreview.Build("Prometheus AI", "Muse 1");

            var mark = chip.Q<BrandMark>();
            Assert.That(mark, Is.Not.Null, "The die has no mark on it.");
            Assert.That(mark.Company, Is.EqualTo("Prometheus AI"));

            Assert.That(chip.Query(className: "die__pin").ToList().Count, Is.EqualTo(18),
                "Nine contacts a side is what makes a rounded rectangle read as a processor.");
        }

        /// <summary>
        /// Every phrase the mock uses exists in both languages.
        /// </summary>
        [Test]
        public void TheMockReadsInBothLanguages()
        {
            var was = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var browser = new BrowserPreview();
                    browser.Show("Prometheus AI", null, null);

                    foreach (var label in browser.Root.Query<Label>().ToList())
                    {
                        Assert.That(label.text, Does.Not.StartWith("wb."),
                            $"{language}: the mock is printing its own key.");
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }

        /// <summary>
        /// The branding stage is the first thing the creator shows.
        /// </summary>
        [Test]
        public void BrandingIsTheFirstStageOfTheCreator()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 80_000_000;
            simulation.SetRentedPetaflops(600.0);

            var panel = new ModelCreatorPanel(simulation);
            panel.Refresh();
            panel.Stage = 0;

            Assert.That(panel.Root.Q(className: "wb"), Is.Not.Null,
                "The first page of the creator is where the player decides who they are, and the "
                + "mock is the whole point of it.");
            Assert.That(panel.Root.Q(className: "die"), Is.Not.Null,
                "The silicon preview is missing from the branding page.");
        }
    }
}
