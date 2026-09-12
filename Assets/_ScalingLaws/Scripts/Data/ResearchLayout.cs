using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>Where one node sits on the board: which column, which row.</summary>
    public readonly struct ResearchSlot
    {
        public ResearchSlot(ResearchNodeId node, int column, int row)
        {
            Node = node;
            Column = Math.Max(0, column);
            Row = Math.Max(0, row);
        }

        public ResearchNodeId Node { get; }

        /// <summary>How deep into the era's chain of prerequisites this node is. Zero is the start.</summary>
        public int Column { get; }

        /// <summary>Which lane it takes in that column, top to bottom.</summary>
        public int Row { get; }

        public override string ToString() => $"{Node} at {Column},{Row}";
    }

    /// <summary>
    /// The shape of the research board, derived from what needs what.
    ///
    /// **Derived, and that is the whole design decision.** The obvious way to build a technology
    /// screen that reads like Civilization's is to lay the nodes out by hand, one picture per era,
    /// which is exactly what that game does and can afford to do because its tree ships once. This
    /// one has 59 nodes and grows: four were added to it the week this was written. A hand-placed
    /// board means every new node is a drawing job, and the first time somebody adds one without
    /// redrawing the board the screen shows a node wired to nothing, which is a fault no test can
    /// see and no player can explain.
    ///
    /// So the column is how deep a node sits in its era's chain of prerequisites and the row is a
    /// lane chosen to keep a node beside the thing it follows. A node added tomorrow places itself,
    /// and the lines between nodes cannot disagree with the rules, because they are drawn from the
    /// same prerequisites the simulation gates on.
    ///
    /// **Pure, and in Data on purpose.** No UnityEngine, no pixels: a column and a row are a design
    /// decision about reading order, and the screen turns them into positions. That is what lets
    /// `ResearchLayoutTests` assert that no two nodes share a cell and that nothing is ever drawn to
    /// the left of something it needs, which are the two ways a board like this goes quietly wrong.
    /// </summary>
    public static class ResearchLayout
    {
        /// <summary>
        /// How deep a board is allowed to get before it grows sideways instead.
        ///
        /// Three, because the frame this is drawn in is a band across the page rather than a full
        /// screen, and because the request was a board a player scrolls left and right along.
        /// </summary>
        public const int MaxRows = 3;

        /// <summary>
        /// Lays out one era's nodes, in one track.
        ///
        /// Tracks are laid out separately because they are read separately: the screen already draws
        /// capability, deepening, safety and operations as their own bands, and mixing them into one
        /// graph would put a safety node in the middle of the capability chain because it happens to
        /// need one.
        /// </summary>
        public static IReadOnlyList<ResearchSlot> Place(ResearchEra era, ResearchTrack track)
        {
            var nodes = ResearchTree.All
                .Where(node => node.Era == era && node.Track == track)
                .ToList();

            return Place(nodes);
        }

        /// <summary>Lays out whatever it is handed, which is what makes this testable.</summary>
        public static IReadOnlyList<ResearchSlot> Place(IReadOnlyList<ResearchNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return Array.Empty<ResearchSlot>();
            }

            var present = new HashSet<ResearchNodeId>(nodes.Select(node => node.Id));

            // **A band where nothing needs anything is a row, not a column.** The deepening, safety
            // and operations bands are usually a handful of independent nodes, and depth would put
            // every one of them in the first column: two nodes reading top to bottom in a band that
            // is six times wider than it is tall. There is no order to show, so the only honest
            // shape is the one that reads like a shelf.
            var anyDependsOnAnother = nodes.Any(node =>
                ResearchTree.Get(node.Id).Prerequisites.Any(present.Contains));

            if (!anyDependsOnAnother)
            {
                return nodes
                    .Select((node, index) => new ResearchSlot(node.Id, index, 0))
                    .ToList();
            }

            var column = new Dictionary<ResearchNodeId, int>();

            // **Depth first, memoised, and a node outside this set counts as depth minus one.** A
            // node whose prerequisite belongs to an earlier era has nothing to sit behind here, so
            // it belongs at the left edge with the other openings rather than being pushed right by
            // a dependency the player cannot see on this board.
            int Depth(ResearchNodeId id, HashSet<ResearchNodeId> walking)
            {
                if (column.TryGetValue(id, out var known))
                {
                    return known;
                }

                // A cycle would be a fault in the catalogue rather than in the drawing, and
                // `ConsistencyTests` holds that separately. Here it must simply not hang.
                if (!walking.Add(id))
                {
                    return 0;
                }

                var deepest = -1;

                foreach (var required in ResearchTree.Get(id).Prerequisites)
                {
                    if (present.Contains(required))
                    {
                        deepest = Math.Max(deepest, Depth(required, walking));
                    }
                }

                walking.Remove(id);

                var answer = deepest + 1;
                column[id] = answer;
                return answer;
            }

            foreach (var node in nodes)
            {
                Depth(node.Id, new HashSet<ResearchNodeId>());
            }

            // **Then spread, because depth alone makes a column and not a board.** Most nodes in an
            // era need nothing else in that era, so depth puts nearly all of them in the first
            // column and the result is a tower: era one came out four wide and five deep, which is
            // the shape of a list rather than of a tree, and the whole point of this screen is that
            // a player reads it left to right.
            //
            // A node may always be moved further right: the only rule it owes anybody is to be
            // right of what it needs. So a column that is full pushes its next node along, and
            // anything that needed that node has already been given a deeper column because this
            // walks them in depth order.
            var slots = new List<ResearchSlot>(nodes.Count);
            var taken = new HashSet<(int Column, int Row)>();
            var rowOf = new Dictionary<ResearchNodeId, int>();
            var placedAt = new Dictionary<ResearchNodeId, int>();
            var filled = new Dictionary<int, int>();

            // Depth first, then the order the catalogue lists them, so the board is the same every
            // time it is read. The index is captured rather than searched for, because a node is a
            // struct and looking one up by value is both slow and a question about equality that
            // this does not need to answer.
            var order = nodes.Select((node, index) => (node, index))
                .OrderBy(pair => column[pair.node.Id])
                .ThenBy(pair => pair.index)
                .Select(pair => pair.node)
                .ToList();

            foreach (var node in order)
            {
                var at = column[node.Id];

                // Never left of anything it needs, whatever the spreading does.
                foreach (var required in ResearchTree.Get(node.Id).Prerequisites)
                {
                    if (placedAt.TryGetValue(required, out var behind))
                    {
                        at = Math.Max(at, behind + 1);
                    }
                }

                while (filled.TryGetValue(at, out var used) && used >= MaxRows)
                {
                    at++;
                }

                // The lane of whatever it follows when that lane is free, which keeps a chain
                // reading as a straight line rather than as a staircase.
                var wanted = 0;

                foreach (var required in ResearchTree.Get(node.Id).Prerequisites)
                {
                    if (rowOf.TryGetValue(required, out var followed))
                    {
                        wanted = followed;
                        break;
                    }
                }

                while (!taken.Add((at, wanted)))
                {
                    wanted = (wanted + 1) % MaxRows;

                    if (wanted == 0 && taken.Contains((at, 0)))
                    {
                        // This column is genuinely full. Start the next one.
                        at++;
                    }
                }

                filled[at] = filled.TryGetValue(at, out var count) ? count + 1 : 1;
                placedAt[node.Id] = at;
                rowOf[node.Id] = wanted;
                slots.Add(new ResearchSlot(node.Id, at, wanted));
            }

            return slots;
        }

        /// <summary>How wide the board is for this era and track, in columns.</summary>
        public static int Columns(IReadOnlyList<ResearchSlot> slots) =>
            slots == null || slots.Count == 0 ? 0 : slots.Max(slot => slot.Column) + 1;

        /// <summary>How deep it is, in lanes.</summary>
        public static int Rows(IReadOnlyList<ResearchSlot> slots) =>
            slots == null || slots.Count == 0 ? 0 : slots.Max(slot => slot.Row) + 1;
    }
}
