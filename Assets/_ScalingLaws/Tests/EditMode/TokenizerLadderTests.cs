using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The tokenizer ladder on the DATA stage: four rungs, six steps of adaptation, and one number
    /// out of it that the market and the fleet are both charged by.
    ///
    /// What is worth holding here is not the arithmetic, it is the three rules the mechanic stands
    /// on: the untouched option is exactly 1.0, money never overtakes the technology, and the choice
    /// travels all the way to the thing on sale.
    /// </summary>
    public sealed class TokenizerLadderTests
    {
        [Test]
        public void TheUntouchedOptionIsExactlyNeutral()
        {
            Assert.AreEqual(1.0,
                TokenizerCatalog.TokensPerText(TokenizerKind.OffTheShelf, 0), 1e-9,
                "a company that never opens this control must be charged exactly what it was "
                + "charged before the control existed, or adding it retunes the whole economy");

            Assert.AreEqual(0.0, TokenizerCatalog.AdaptationShare(0), 1e-9,
                "and the first step of the bar has to be free");
        }

        [Test]
        public void EveryRungIsCheaperThanTheOneBelowIt()
        {
            var rungs = TokenizerCatalog.All;

            for (var index = 1; index < rungs.Count; index++)
            {
                Assert.Less(rungs[index].TokensPerText, rungs[index - 1].TokensPerText,
                    $"{rungs[index].Kind} has to beat {rungs[index - 1].Kind}");
            }
        }

        [Test]
        public void MoneyNeverOvertakesTheTechnology()
        {
            var rungs = TokenizerCatalog.All;

            for (var index = 1; index < rungs.Count; index++)
            {
                Assert.Greater(rungs[index - 1].AdaptedTokensPerText, rungs[index].TokensPerText,
                    "a fully adapted corpus on the lower rung must still be dearer than the next "
                    + "rung untouched. The author's rule: research pushes hardest, money only helps.");
            }
        }

        [Test]
        public void TheWholeLadderIsWorthExactlyTwentyPerCent()
        {
            var best = TokenizerCatalog.TokensPerText(
                TokenizerKind.LearnedVocabulary, TokenizerCatalog.AdaptationLevels - 1);

            Assert.AreEqual(0.80, best, 1e-9,
                "everything researched and fully adapted is the twenty per cent that was agreed, "
                + "and nothing in the ladder may quietly reach past it");
        }

        [Test]
        public void EachStepOfTheBarIsWorthMoreMoneyThanTheOneBefore()
        {
            for (var level = 1; level < TokenizerCatalog.AdaptationLevels; level++)
            {
                Assert.Greater(TokenizerCatalog.AdaptationShare(level),
                    TokenizerCatalog.AdaptationShare(level - 1));
            }

            Assert.AreEqual(0L, TokenizerCatalog.AdaptationCostUsd(0, 40_000_000));
            Assert.AreEqual(10_000_000L,
                TokenizerCatalog.AdaptationCostUsd(TokenizerCatalog.AdaptationLevels - 1, 40_000_000),
                "the top step is a quarter of the run it is adapting for");
        }

        [Test]
        public void ARungNobodyResearchedCannotBeTrained()
        {
            var simulation = Ready();

            var blueprint = Blueprint().WithTokenizer(TokenizerKind.LearnedVocabulary, 0);

            Assert.IsFalse(simulation.TryStartTraining(blueprint, out var why),
                "the rule lives in the simulation, not on the screen: a cap that only the interface "
                + "knows about is a suggestion the moment a second way in exists");
            Assert.IsNotEmpty(why);

            simulation.State.UnlockedResearch.Add(ResearchNodeId.SubwordTokenizer);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.ByteLevelTokenizer);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.LearnedVocabulary);

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var second), second);
        }

        [Test]
        public void TheRungTravelsAllTheWayToTheModelOnSale()
        {
            var simulation = Ready();
            simulation.State.UnlockedResearch.Add(ResearchNodeId.SubwordTokenizer);

            var blueprint = Blueprint().WithTokenizer(TokenizerKind.Subword, 3);

            Assert.IsTrue(simulation.TryStartTraining(blueprint, out var why), why);

            for (var day = 0; day < 900 && simulation.State.ActiveRun != null; day++)
            {
                simulation.AdvanceDay();
            }

            Assert.AreEqual(1, simulation.State.Shelf.Count, "the run has to finish");

            var shelved = simulation.State.Shelf[0];
            Assert.AreEqual(TokenizerKind.Subword, shelved.Tokenizer);
            Assert.AreEqual(3, shelved.TokenizerAdaptation);

            var live = shelved.Release(simulation.State.Date, 1.0);
            Assert.AreEqual(TokenizerKind.Subword, live.Tokenizer);
            Assert.AreEqual(3, live.TokenizerAdaptation);
            Assert.Less(live.TokensPerText, 1.0,
                "and the thing on sale is actually charged the cheaper token, which is the half "
                + "that has failed in this project before: chosen everywhere, delivered nowhere");
        }

        [Test]
        public void AdaptingTheCorpusIsPaidForWhenTheRunStarts()
        {
            var free = Ready();
            Assert.IsTrue(free.TryStartTraining(Blueprint().WithTokenizer(TokenizerKind.OffTheShelf, 0),
                out var whyFree), whyFree);

            var paid = Ready();
            Assert.IsTrue(paid.TryStartTraining(Blueprint().WithTokenizer(TokenizerKind.OffTheShelf, 5),
                out var whyPaid), whyPaid);

            Assert.Less(paid.State.CashUsd, free.State.CashUsd,
                "the top of the bar has to cost real money on the day the run starts");
        }

        /// <summary>
        /// The whole chain, measured rather than asserted: a cheaper token has to end up as more
        /// tokens served off the same cluster and a better standing, not merely as a smaller number
        /// on a card. Every fixture around this one tests a link.
        /// </summary>
        [Test]
        public void ACheaperTokenActuallyReachesTheMarketAndTheFleet()
        {
            var plain = Selling(TokenizerKind.OffTheShelf, 0);
            var best = Selling(TokenizerKind.LearnedVocabulary, TokenizerCatalog.AdaptationLevels - 1);

            for (var day = 0; day < 240; day++)
            {
                plain.AdvanceDay();
                best.AdvanceDay();
            }

            var plainCapacity = plain.State.LastQuality.Capacity;
            var bestCapacity = best.State.LastQuality.Capacity;

            Assert.Greater(bestCapacity, plainCapacity * 1.15,
                $"the same cluster served {bestCapacity:N0}B tokens on the top rung against "
                + $"{plainCapacity:N0}B off the shelf. A saving nobody can measure through the fleet "
                + "is a saving that does not exist.");
        }

        /// <summary>A company already selling one model, built on a chosen rung.</summary>
        private static CompanySimulation Selling(TokenizerKind kind, int adaptation)
        {
            var simulation = Ready();

            simulation.State.UnlockedResearch.Add(ResearchNodeId.SubwordTokenizer);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.ByteLevelTokenizer);
            simulation.State.UnlockedResearch.Add(ResearchNodeId.LearnedVocabulary);

            var model = new DeployedModel(
                "Probe", ArchitectureId.DenseTransformer, 42.0, simulation.State.Date,
                20e9, 1.0, ModelType.General, "Probe", 0, 0, -1, 1, kind, adaptation);

            simulation.State.AddDeployedModel(model);

            return simulation;
        }

        /// <summary>
        /// **The guard that was missing.** Until 2026-09-18 the eleven `With` helpers carried eleven
        /// of the fifteen fields and silently dropped the four safety tiers, so renaming a blueprint
        /// threw away the protection it had been given. Nothing had called one on a hardened
        /// blueprint yet, which is the only reason it had never cost anybody a fine.
        /// </summary>
        [Test]
        public void EveryWithHelperKeepsEveryOtherField()
        {
            var full = new ModelBlueprint(
                "Full", ArchitectureId.SparseMixture, 30.0, 600.0,
                DatasetSource.WebCrawl | DatasetSource.CodeCorpus,
                ModelType.Coding, "Line", TrainingPrecision.BFloat16, ModelShape.Deep,
                DeduplicationPass.Aggressive, 12, 2, 1, 0, 3,
                TokenizerKind.ByteLevelBpe, 4);

            var helpers = typeof(ModelBlueprint)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name.StartsWith("With", StringComparison.Ordinal))
                .ToList();

            Assert.GreaterOrEqual(helpers.Count, 12, "every helper is walked, not a list kept by hand");

            foreach (var helper in helpers)
            {
                var copy = (ModelBlueprint)helper.Invoke(full, Arguments(helper, full));

                foreach (var property in typeof(ModelBlueprint).GetProperties())
                {
                    if (!property.CanRead || Touched(helper.Name, property.Name))
                    {
                        continue;
                    }

                    Assert.AreEqual(property.GetValue(full), property.GetValue(copy),
                        $"{helper.Name} dropped {property.Name}");
                }
            }
        }

        /// <summary>A value for each parameter that is not the one the original already holds.</summary>
        private static object[] Arguments(MethodInfo helper, ModelBlueprint full) =>
            helper.GetParameters().Select(parameter => parameter.Name switch
            {
                "name" => (object)"Renamed",
                "family" => "Another line",
                "parameterCountBillions" => 45.0,
                "trainingTokensBillions" => 900.0,
                "architecture" => ArchitectureId.DenseTransformer,
                "type" => ModelType.General,
                "dataSources" => DatasetSource.WebCrawl,
                "precision" => TrainingPrecision.Float32,
                "shape" => ModelShape.Wide,
                "pass" => DeduplicationPass.None,
                "monthsBack" => 6,
                "tokenizer" => TokenizerKind.Subword,
                "adaptationLevel" => 1,
                _ => throw new InvalidOperationException(
                    $"{helper.Name} takes {parameter.Name}, which this fixture has no value for. "
                    + "Add one rather than skipping the helper.")
            }).ToArray();

        /// <summary>The field a helper is allowed to change, by the helper's own name.</summary>
        private static bool Touched(string helper, string property) => helper switch
        {
            "WithName" => property == "Name",
            "WithFamily" => property is "Family" or "HasFamily",
            "WithParameters" => property is "ParameterCountBillions" or "TokensPerParameter"
                or "ParameterCount",
            "WithTokens" => property is "TrainingTokensBillions" or "TokensPerParameter" or "TrainingTokens",
            "WithArchitecture" => property == "Architecture",
            "WithType" => property == "Type",
            "WithDataSources" => property == "DataSources",
            "WithPrecision" => property == "Precision",
            "WithShape" => property == "Shape",
            "WithDeduplication" => property == "Deduplication",
            "WithCutoff" => property == "CutoffMonthsBack",
            "WithTokenizer" => property is "Tokenizer" or "TokenizerAdaptation" or "TokensPerText",
            _ => false
        };

        private static ModelBlueprint Blueprint() =>
            new("Tokenizer probe", ArchitectureId.DenseTransformer, 8.0, 160.0, DatasetSource.WebCrawl);

        /// <summary>A company with money and a cluster, so a run is refused for one reason only.</summary>
        private static CompanySimulation Ready()
        {
            var state = CompanyState.FromOpeningChoice("Tokenizer Labs", CompanyArchetype.Custom,
                FounderTrait.None, FounderTrait.None);

            state.CashUsd = 400_000_000L;

            var simulation = new CompanySimulation(state);
            simulation.SetRentedPetaflops(900.0);

            return simulation;
        }
    }
}
