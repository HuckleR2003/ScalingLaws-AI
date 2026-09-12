using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// One era's nodes, on a board, with the lines between them.
    ///
    /// **The lines are the half that was missing.** Every node in this game has its prerequisites in
    /// the catalogue and the screen has never drawn one of them, so the order a player is meant to
    /// read the tree in existed only inside the rules. A tester put it plainly: *"can you simplify
    /// what each research node does? Sometimes you dont really know what something does"*. Part of
    /// that is what a node gives you, which the card answers; the rest is what comes after what,
    /// which is this.
    ///
    /// Positions come from <see cref="ResearchLayout"/> and nothing here decides where a node goes.
    /// That separation is what lets the placement be tested without a panel, and it is why adding a
    /// node to the catalogue cannot leave the board wrong: there is no second copy of the shape.
    /// </summary>
    public sealed class ResearchBoard : VisualElement
    {
        /// <summary>A card, and the gaps around it. The card is wide because it carries words.</summary>
        public const float CardWidth = 258f;

        public const float CardHeight = 100f;
        public const float ColumnGap = 48f;
        public const float RowGap = 18f;
        public const float Margin = 14f;

        private readonly List<(Vector2 From, Vector2 To)> wires = new();

        /// <summary>How tall the board came out, so the frame around it can be sized to fit.</summary>
        public float BoardHeight { get; private set; }

        public ResearchBoard()
        {
            AddToClassList("rboard");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawWires;
        }

        /// <summary>
        /// Lays the cards out and works out where the wires run.
        /// </summary>
        /// <param name="slots">Where each node sits, from the layout.</param>
        /// <param name="card">Builds the card for one node. The board does not know what is on it.</param>
        /// <param name="isOnThisBoard">
        /// Whether a prerequisite is one of these nodes. A requirement from an earlier era has no
        /// card here to draw a line to, and a line running off the edge of a board to a node the
        /// player cannot see says less than no line at all.
        /// </param>
        public void Fill(IReadOnlyList<ResearchSlot> slots,
            Func<ResearchNodeId, VisualElement> card,
            Func<ResearchNodeId, bool> isOnThisBoard)
        {
            Clear();
            wires.Clear();

            if (slots == null || slots.Count == 0 || card == null)
            {
                style.width = 0f;
                style.height = 0f;
                return;
            }

            var at = new Dictionary<ResearchNodeId, Vector2>();

            foreach (var slot in slots)
            {
                var x = Margin + slot.Column * (CardWidth + ColumnGap);
                var y = Margin + slot.Row * (CardHeight + RowGap);

                at[slot.Node] = new Vector2(x, y);

                var element = card(slot.Node);

                if (element == null)
                {
                    continue;
                }

                element.style.position = Position.Absolute;
                element.style.left = x;
                element.style.top = y;
                element.style.width = CardWidth;
                element.style.height = CardHeight;

                Add(element);
            }

            foreach (var slot in slots)
            {
                var to = at[slot.Node];

                foreach (var required in ResearchTree.Get(slot.Node).Prerequisites)
                {
                    if (isOnThisBoard != null && !isOnThisBoard(required))
                    {
                        continue;
                    }

                    if (!at.TryGetValue(required, out var from))
                    {
                        continue;
                    }

                    // Out of the right-hand edge of what comes first, into the left-hand edge of
                    // what follows, both at the middle of the card.
                    wires.Add((
                        new Vector2(from.x + CardWidth, from.y + CardHeight / 2f),
                        new Vector2(to.x, to.y + CardHeight / 2f)));
                }
            }

            style.width = Margin * 2f + ResearchLayout.Columns(slots) * (CardWidth + ColumnGap)
                - ColumnGap;

            BoardHeight = Margin * 2f + ResearchLayout.Rows(slots) * (CardHeight + RowGap) - RowGap;
            style.height = BoardHeight;

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Elbows, not diagonals.
        ///
        /// A straight line between two cards on different rows crosses whatever is between them and
        /// reads as a wire in a diagram. An elbow that leaves horizontally, turns once in the gap
        /// between the columns and arrives horizontally reads as a route, and the gap is empty by
        /// construction because the layout only ever puts cards in columns.
        /// </summary>
        private void DrawWires(MeshGenerationContext context)
        {
            if (wires.Count == 0)
            {
                return;
            }

            var painter = context.painter2D;

            painter.lineWidth = 2f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.22f);
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            foreach (var (from, to) in wires)
            {
                var turn = (from.x + to.x) / 2f;

                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(new Vector2(turn, from.y));
                painter.LineTo(new Vector2(turn, to.y));
                painter.LineTo(to);
                painter.Stroke();
            }
        }
    }
}
