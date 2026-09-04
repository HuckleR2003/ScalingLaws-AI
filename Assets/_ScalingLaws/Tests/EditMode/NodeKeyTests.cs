using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every research node has its own name, and the phrase book has words for it.
    ///
    /// **This is a ratchet for a fault that shipped.** `ResearchNode.KeyFor` is a switch with a
    /// `_ => "node.finetuning"` arm, and era five was written months after the switch and never
    /// added to it. So all five Statecraft nodes - the end game, the most expensive research in the
    /// game - drew on the tree as "Fine-tuning and prompting", with era one's description under
    /// them, in the completion event and in the news item that announces it.
    ///
    /// Nothing failed. A default arm is a valid answer to a switch, the key it returns is a real key
    /// with real words behind it, and `LocalisationTests` can only prove that the keys the interface
    /// asks for exist. It could not notice that nine nodes were asking for the same one.
    ///
    /// The general lesson, and it is the third time this project has hit it: **a fallback that draws
    /// the wrong thing confidently is worse than one that draws nothing.** A missing key renders the
    /// key, which anybody would spot in one screenshot.
    /// </summary>
    public sealed class NodeKeyTests
    {
        [Test]
        public void EveryNodeHasItsOwnNameRatherThanFallingThroughToADefault()
        {
            var seen = new Dictionary<string, ResearchNodeId>();

            foreach (var node in ResearchTree.All)
            {
                Assert.IsNotEmpty(node.DisplayName, $"{node.Id} has no name at all.");

                Assert.IsFalse(seen.TryGetValue(node.DisplayName, out var already),
                    $"{node.Id} draws as \"{node.DisplayName}\", which is already {already}'s "
                    + "name. Either two nodes share a phrase-book stem or one of them is falling "
                    + "through the default arm of KeyFor.");

                seen[node.DisplayName] = node.Id;
            }
        }

        /// <summary>
        /// The descriptions too, and they are the half that gives the fault away.
        ///
        /// Two nodes could conceivably be given the same short name by accident and read as a
        /// typo. Two nodes with the same four-line description are always the same key.
        /// </summary>
        [Test]
        public void EveryNodeHasItsOwnDescription()
        {
            var seen = new Dictionary<string, ResearchNodeId>();

            foreach (var node in ResearchTree.All)
            {
                Assert.IsNotEmpty(node.Description, $"{node.Id} has no description.");

                // A key with no words behind it renders as the key. "node." is the stem every
                // one of them starts with, so a description that opens with it is a raw key on
                // the largest screen in the game.
                Assert.IsFalse(node.Description.StartsWith("node."),
                    $"{node.Id} is rendering its own key, so the phrase book has no words for it.");

                Assert.IsFalse(seen.TryGetValue(node.Description, out var already),
                    $"{node.Id} and {already} share a description word for word, which means they "
                    + "share a phrase-book stem.");

                seen[node.Description] = node.Id;
            }
        }

        /// <summary>
        /// Every member of the enum is a node the tree can actually hand out.
        ///
        /// A member with no entry is a value that can be written into a save, named as a
        /// prerequisite and looked up, and `ResearchTree.Get` throws on it. The enum and the
        /// catalog are two lists that have to agree and nothing else checks that they do.
        /// </summary>
        [Test]
        public void EveryMemberOfTheEnumIsANodeInTheTree()
        {
            foreach (ResearchNodeId id in Enum.GetValues(typeof(ResearchNodeId)))
            {
                if (id == ResearchNodeId.None)
                {
                    continue;
                }

                Assert.IsTrue(ResearchTree.TryGet(id, out _),
                    $"{id} is a research node id with no node behind it.");
            }
        }

        /// <summary>Both languages have words for every node, in both fields.</summary>
        [Test]
        public void EveryNodeSpeaksBothLanguages()
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    foreach (var node in ResearchTree.All)
                    {
                        Assert.IsNotEmpty(node.DisplayName,
                            $"{node.Id} has no name in {language}.");

                        Assert.IsNotEmpty(node.Description,
                            $"{node.Id} has no description in {language}.");
                    }
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }
    }
}
