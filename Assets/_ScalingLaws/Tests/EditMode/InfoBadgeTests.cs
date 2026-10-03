using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The "(i)" cards, checked for the two ways they stop being there.
    ///
    /// **The model creator shipped with none of them.** It is the screen a new player spends the
    /// most time on and the one with the most decisions that punish a guess, and every control on it
    /// was a slider with a caption. The badges were built for the architecture screen and nobody
    /// went back. Counting them is the only thing that would have said so.
    ///
    /// The second failure is quieter: a card whose four sections are empty renders as a title over
    /// nothing, which reads as a broken tooltip rather than as missing copy.
    /// </summary>
    public sealed class InfoBadgeTests
    {
        private static CompanySimulation Company()
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 80_000_000;
            simulation.SetRentedPetaflops(600.0);
            return simulation;
        }

        private static int BadgesIn(VisualElement root) =>
            root.Query<Button>(className: "infodot").ToList().Count;

        [Test]
        public void TheModelCreatorExplainsItsControls()
        {
            var panel = new UI.ModelCreatorPanel(Company());
            panel.Refresh();

            // Only the stage being looked at is in the tree, and the badges are spread over four
            // of the seven, so counting the opening page finds none of them.
            var found = 0;

            for (var stage = 0; stage < UI.ModelCreatorPanel.StageCount; stage++)
            {
                panel.Stage = stage;
                found += BadgesIn(panel.Root);
            }

            Assert.That(found, Is.GreaterThanOrEqualTo(4),
                "The creator is the screen with the most decisions on it and it shipped with no "
                + "explanations at all. Parameters, tokens, the data mix and the safety effort each "
                + "carry one.");
        }

        /// <summary>
        /// Every slider on the architecture screen says what it is for.
        ///
        /// **It asserted a count and the count was the wrong fact.** `Is.EqualTo(7)` caught the
        /// eighth control being added rather than a control being left unexplained, which is the
        /// opposite of the thing worth holding; adding the head-start slider failed it while the
        /// screen was more explained than before, not less. Counting the sliders and requiring one
        /// badge each says what this fixture has always meant.
        /// </summary>
        [Test]
        public void EverySliderOnTheArchitectureScreenSaysWhatItIsFor()
        {
            var panel = new UI.ArchitectureCreatorPanel(Company());
            panel.Refresh();

            var sliders = panel.Root.Query<UnityEngine.UIElements.Slider>().ToList().Count;

            Assert.That(sliders, Is.GreaterThanOrEqualTo(7),
                "Five directions, the budget and the calendar are the floor for this screen.");

            Assert.That(BadgesIn(panel.Root), Is.EqualTo(sliders),
                $"{sliders} controls on the screen and a different number of explanations. A "
                + "slider with nothing saying what it moves is a chore with a handle on it.");
        }

        /// <summary>
        /// Every note says something in both languages.
        ///
        /// A blank section is not a missing translation the player can work around: the card draws
        /// a heading with nothing under it, which looks like a rendering fault.
        /// </summary>
        [Test]
        public void EveryNoteIsWrittenInBothLanguages()
        {
            var notes = new (string Name, System.Func<TechNotes.Note> Get)[]
            {
                ("Sparsity", () => TechNotes.Sparsity),
                ("Throughput", () => TechNotes.Throughput),
                ("Quality", () => TechNotes.Quality),
                ("Serving", () => TechNotes.Serving),
                ("Reasoning", () => TechNotes.Reasoning),
                ("ResearchBudget", () => TechNotes.ResearchBudget),
                ("ProgrammeLength", () => TechNotes.ProgrammeLength),
                ("Parameters", () => TechNotes.Parameters),
                ("TokensPerParameter", () => TechNotes.TokensPerParameter),
                ("SafetyEffort", () => TechNotes.SafetyEffort),
                ("WebCrawl", () => TechNotes.WebCrawl),
                ("CuratedWeb", () => TechNotes.CuratedWeb),
                ("LicensedArchives", () => TechNotes.LicensedArchives),
            };

            var was = Loc.Current;
            var thin = new System.Collections.Generic.List<string>();

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var (name, get) in notes)
                    {
                        var note = get();

                        foreach (var (part, text) in new[]
                                 {
                                     ("title", note.Title), ("what", note.What),
                                     ("affects", note.Affects), ("high", note.High),
                                     ("low", note.Low)
                                 })
                        {
                            // A key that resolved to itself is a missing entry, and it reads on
                            // screen as the key rather than as a sentence.
                            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("tech."))
                            {
                                thin.Add($"{language} {name}.{part}: \"{text}\"");
                            }
                        }
                    }
                }
            }
            finally
            {
                Loc.Current = was;
            }

            Assert.IsEmpty(thin, "Sections with nothing in them:\n  " + string.Join("\n  ", thin));
        }

        /// <summary>
        /// Both ends of every control say something different.
        ///
        /// **This is a design check wearing a test.** A control where the low end has nothing to say
        /// is not a decision, it is a chore with a slider on it, and writing the two halves is what
        /// forces that to be noticed.
        /// </summary>
        [Test]
        public void TheTwoEndsOfEveryControlDisagree()
        {
            var was = Loc.Current;

            try
            {
                Loc.Current = Language.English;

                foreach (var note in new[]
                         {
                             TechNotes.Sparsity, TechNotes.Throughput, TechNotes.Quality,
                             TechNotes.Serving, TechNotes.Reasoning, TechNotes.Parameters,
                             TechNotes.TokensPerParameter, TechNotes.SafetyEffort
                         })
                {
                    Assert.That(note.Low, Is.Not.EqualTo(note.High),
                        $"{note.Title}: both ends read the same, so one of them is not a real option.");
                }
            }
            finally
            {
                Loc.Current = was;
            }
        }
    }
}
