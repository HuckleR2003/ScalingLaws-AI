using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The gate behind the creator's type tiles.
    ///
    /// This fixture exists because of a hole that survived two sessions and every one of 244 tests:
    /// the market was split by model type, the research tree grew a chain of four type nodes, the
    /// demographic panel drew the categories, and the creator never set the type on the blueprint at
    /// all. Every model the player released was general, forever. Nothing failed, because no test
    /// asked whether a player could reach the feature.
    /// </summary>
    public sealed class ModelTypeChoiceTests
    {
        private static CompanyState FreshState() => new("Pickerco", 31);

        [Test]
        public void AGeneralModelNeedsNoResearchAndIsAlwaysAvailable()
        {
            var state = FreshState();

            Assert.IsTrue(state.CanBuildType(ModelType.General),
                "A brand new company has to be able to build something on day one.");
        }

        [Test]
        public void EverySpecialisedTypeIsLockedOnDayOne()
        {
            var state = FreshState();

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == ResearchNodeId.None)
                {
                    continue;
                }

                Assert.IsFalse(state.CanBuildType(definition.Type),
                    $"{definition.DisplayName} is available before its research, so the chain that "
                    + "gates it buys nothing.");
            }
        }

        [Test]
        public void ResearchingTheNodeIsWhatOpensTheType()
        {
            var state = FreshState();
            var coding = ModelTypeCatalog.Get(ModelType.Coding);

            Assert.IsFalse(state.CanBuildType(ModelType.Coding));

            state.UnlockedResearch.Add(coding.Requires);

            Assert.IsTrue(state.CanBuildType(ModelType.Coding),
                "The gate has to be the node the catalog names, not a second list somewhere else.");
        }

        /// <summary>
        /// The gate and the catalog must not drift. If a type's Requires field changes, this follows it
        /// rather than restating a node id that would quietly become wrong.
        /// </summary>
        [Test]
        public void UnlockingEveryTypeNodeMakesEveryTypeBuildable()
        {
            var state = FreshState();

            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires != ResearchNodeId.None)
                {
                    state.UnlockedResearch.Add(definition.Requires);
                }
            }

            foreach (var definition in ModelTypeCatalog.All)
            {
                Assert.IsTrue(state.CanBuildType(definition.Type),
                    $"{definition.DisplayName} stayed locked with its own node researched.");
            }
        }

        /// <summary>
        /// The chain, from the player's side. A type is only reachable once everything before it is,
        /// so the order the tiles unlock in is the order of difficulty rather than an arbitrary list.
        /// </summary>
        [Test]
        public void TheTypesOpenInOneOrderAndNobodySkipsAhead()
        {
            var chain = new[]
            {
                ResearchNodeId.ConversationalModels,
                ResearchNodeId.CodingModels,
                ResearchNodeId.AutomationModels,
                ResearchNodeId.AgenticWorkstation
            };

            var state = FreshState();

            for (var step = 0; step < chain.Length; step++)
            {
                // Everything up to and including this step is researched, nothing beyond it.
                state = FreshState();
                for (var done = 0; done <= step; done++)
                {
                    state.UnlockedResearch.Add(chain[done]);
                }

                Assert.IsTrue(state.CanBuildType(TypeGatedBy(chain[step])),
                    $"{chain[step]} researched and its type still locked.");

                if (step + 1 < chain.Length)
                {
                    Assert.IsFalse(state.CanBuildType(TypeGatedBy(chain[step + 1])),
                        $"{chain[step + 1]}'s type opened without its own node, so the chain leaks.");
                }
            }
        }

        [Test]
        public void ABlueprintKeepsItsTypeThroughEveryEdit()
        {
            // The creator rebuilds the blueprint on every keystroke and slider move. A With helper that
            // dropped the type would silently reset a specialised model to general.
            var blueprint = new ModelBlueprint("Coder", ArchitectureId.DenseTransformer, 20.0, 400.0,
                DatasetSource.None, ModelType.Coding);

            Assert.AreEqual(ModelType.Coding, blueprint.WithName("Renamed").Type);
            Assert.AreEqual(ModelType.Coding, blueprint.WithParameters(40.0).Type);
            Assert.AreEqual(ModelType.Coding, blueprint.WithTokens(900.0).Type);
            Assert.AreEqual(ModelType.Coding,
                blueprint.WithArchitecture(ArchitectureId.SparseMixture).Type);
            Assert.AreEqual(ModelType.Coding, blueprint.WithDataSources(DatasetSource.None).Type);
        }

        private static ModelType TypeGatedBy(ResearchNodeId node)
        {
            foreach (var definition in ModelTypeCatalog.All)
            {
                if (definition.Requires == node)
                {
                    return definition.Type;
                }
            }

            Assert.Fail($"No model type is gated by {node}, so the chain in this test is stale.");
            return ModelType.General;
        }
    }
}
