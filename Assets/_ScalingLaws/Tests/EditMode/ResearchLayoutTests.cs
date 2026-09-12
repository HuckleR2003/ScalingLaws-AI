using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The research board, as arithmetic.
    ///
    /// **This exists so the board can never need redrawing.** The alternative was a hand-placed map
    /// per era, which is what Civilization does and what the author asked about; the reason this
    /// project cannot afford it is that the tree grows, and a hand-placed board silently stops
    /// matching the rules the day somebody adds a node. Every fault that would produce is caught
    /// here instead: two nodes on one square, a node drawn to the left of something it needs, a node
    /// that appears twice or not at all.
    /// </summary>
    public sealed class ResearchLayoutTests
    {
        private static (ResearchEra Era, ResearchTrack Track)[] Boards() =>
            (from ResearchEra era in Enum.GetValues(typeof(ResearchEra))
                from ResearchTrack track in Enum.GetValues(typeof(ResearchTrack))
                select (era, track)).ToArray();

        [Test]
        public void EveryNodeIsPlacedExactlyOnce()
        {
            foreach (var (era, track) in Boards())
            {
                var expected = ResearchTree.All
                    .Where(node => node.Era == era && node.Track == track)
                    .Select(node => node.Id)
                    .ToList();

                var placed = ResearchLayout.Place(era, track).Select(slot => slot.Node).ToList();

                Assert.That(placed.OrderBy(id => id), Is.EqualTo(expected.OrderBy(id => id)),
                    $"The {era}/{track} board does not hold the same nodes the catalogue does.");

                Assert.That(placed.Distinct().Count(), Is.EqualTo(placed.Count),
                    $"A node is placed twice on the {era}/{track} board.");
            }
        }

        /// <summary>**Two nodes on one square is one node the player cannot click.**</summary>
        [Test]
        public void NoTwoNodesShareASquare()
        {
            foreach (var (era, track) in Boards())
            {
                var cells = ResearchLayout.Place(era, track)
                    .Select(slot => (slot.Column, slot.Row))
                    .ToList();

                Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Count),
                    $"Two nodes are drawn on the same square of the {era}/{track} board.");
            }
        }

        /// <summary>
        /// **Nothing is ever drawn to the left of what it needs.**
        ///
        /// This is the one a hand-placed board loses first, and it is the one that makes a tree
        /// readable: a player reads left to right and takes that to mean "this comes after that".
        /// </summary>
        [Test]
        public void APrerequisiteIsAlwaysFurtherLeft()
        {
            foreach (var (era, track) in Boards())
            {
                var slots = ResearchLayout.Place(era, track);
                var at = slots.ToDictionary(slot => slot.Node, slot => slot.Column);

                foreach (var slot in slots)
                {
                    foreach (var required in ResearchTree.Get(slot.Node).Prerequisites)
                    {
                        if (!at.TryGetValue(required, out var behind))
                        {
                            // An earlier era's node. It is not on this board and the rule is about
                            // this board.
                            continue;
                        }

                        Assert.That(behind, Is.LessThan(slot.Column),
                            $"{ResearchTree.Get(slot.Node).DisplayName} is drawn at or before "
                            + $"{ResearchTree.Get(required).DisplayName}, which it needs first.");
                    }
                }
            }
        }

        /// <summary>
        /// The opening of an era is at the left edge, so there is somewhere for the eye to start.
        /// </summary>
        [Test]
        public void EveryBoardWithNodesOnItStartsAtTheLeft()
        {
            foreach (var (era, track) in Boards())
            {
                var slots = ResearchLayout.Place(era, track);

                if (slots.Count == 0)
                {
                    continue;
                }

                Assert.That(slots.Min(slot => slot.Column), Is.Zero,
                    $"The {era}/{track} board has no node in its first column.");

                Assert.That(slots.Min(slot => slot.Row), Is.Zero,
                    $"The {era}/{track} board has no node in its first row.");
            }
        }

        /// <summary>
        /// **The same board every time.** It is derived, so two runs must agree, or the screen
        /// reshuffles itself between visits and the player's memory of where a node was is wrong.
        /// </summary>
        [Test]
        public void TheSameBoardIsProducedEveryTime()
        {
            foreach (var (era, track) in Boards())
            {
                var first = ResearchLayout.Place(era, track);
                var second = ResearchLayout.Place(era, track);

                Assert.That(second.Select(slot => slot.ToString()),
                    Is.EqualTo(first.Select(slot => slot.ToString())),
                    $"The {era}/{track} board came out differently on the second reading.");
            }
        }

        /// <summary>
        /// **No board is deeper than the band it is drawn in.**
        ///
        /// Depth alone put nearly every node of an era in the first column, because most of them
        /// need nothing else in that era: era one came out four wide and five deep, which is a list
        /// standing on its end. A full column pushes its next node right, which is always allowed
        /// because the only rule a node owes anybody is to be right of what it needs.
        /// </summary>
        [Test]
        public void NoBoardIsDeeperThanTheBandItIsDrawnIn()
        {
            foreach (var (era, track) in Boards())
            {
                var slots = ResearchLayout.Place(era, track);

                if (slots.Count == 0)
                {
                    continue;
                }

                Assert.That(ResearchLayout.Rows(slots),
                    Is.LessThanOrEqualTo(ResearchLayout.MaxRows),
                    $"The {era}/{track} board is {ResearchLayout.Rows(slots)} rows deep, and the "
                    + $"band it is drawn in holds {ResearchLayout.MaxRows}.");
            }
        }

        /// <summary>
        /// It grows sideways rather than downwards, which is the whole request.
        ///
        /// Stated as the fewest columns the cap allows rather than as "wider than tall", because
        /// four nodes in three lanes is two columns by three rows and there is nothing wrong with
        /// that: it is the only shape four nodes can take. What matters is that nothing stacks
        /// beyond the cap and the columns are filled rather than the rows.
        /// </summary>
        [Test]
        public void EveryBoardUsesAsManyColumnsAsTheCapRequires()
        {
            foreach (var (era, track) in Boards())
            {
                var slots = ResearchLayout.Place(era, track);

                if (slots.Count == 0)
                {
                    continue;
                }

                var fewest = (slots.Count + ResearchLayout.MaxRows - 1) / ResearchLayout.MaxRows;

                Assert.That(ResearchLayout.Columns(slots), Is.GreaterThanOrEqualTo(fewest),
                    $"The {era}/{track} board holds {slots.Count} nodes in "
                    + $"{ResearchLayout.Columns(slots)} columns, and {ResearchLayout.MaxRows} "
                    + $"lanes each means it needs at least {fewest}.");
            }
        }

        /// <summary>A board no wider than it needs to be, so a long era is still one screen of eye.</summary>
        [Test]
        public void NoBoardIsAbsurdlyWideOrDeep()
        {
            foreach (var (era, track) in Boards())
            {
                var slots = ResearchLayout.Place(era, track);

                if (slots.Count == 0)
                {
                    continue;
                }

                var columns = ResearchLayout.Columns(slots);
                var rows = ResearchLayout.Rows(slots);

                Assert.That(columns, Is.LessThanOrEqualTo(slots.Count),
                    $"The {era}/{track} board is wider than it has nodes.");

                Assert.That(rows, Is.LessThanOrEqualTo(slots.Count),
                    $"The {era}/{track} board is deeper than it has nodes.");

                Assert.That(columns * rows, Is.LessThanOrEqualTo(slots.Count * 4),
                    $"The {era}/{track} board is mostly empty squares: {columns} by {rows} for "
                    + $"{slots.Count} nodes.");
            }
        }
    }
}
