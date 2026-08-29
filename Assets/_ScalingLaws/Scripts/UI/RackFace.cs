using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>What is in one slot of a cabinet, and how hard it is working.</summary>
    public readonly struct SlotFill
    {
        public SlotFill(Texture2D picture, LightGrid lights, double lit, bool hot)
        {
            Picture = picture;
            Lights = lights;
            Lit = Math.Clamp(lit, 0.0, 1.0);
            Hot = hot;
        }

        public Texture2D Picture { get; }
        public LightGrid Lights { get; }

        /// <summary>What share of this part's indicators are lit, 0 to 1.</summary>
        public double Lit { get; }

        /// <summary>True when the cabinet this sits in is throttling.</summary>
        public bool Hot { get; }
    }

    /// <summary>
    /// One part in one slot: the drawing, with its own indicators lit over it.
    ///
    /// **A child rather than something the cabinet paints.** In UI Toolkit a child draws on top of
    /// whatever its parent generated, so lights painted by the cabinet would sit underneath every
    /// part slotted into it, which is a mistake that looks like the lights simply not working.
    /// </summary>
    public sealed class RackSlot : VisualElement
    {
        /// <summary>Cool white for a part that is simply working.</summary>
        private static readonly Color Working = new(0.62f, 0.88f, 1f, 1f);

        /// <summary>Amber for a cabinet the heat has caught up with.</summary>
        private static readonly Color Throttling = new(1f, 0.72f, 0.30f, 1f);

        private SlotFill fill;

        public RackSlot()
        {
            AddToClassList("rackslot");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(SlotFill contents)
        {
            fill = contents;

            style.backgroundImage = contents.Picture == null
                ? StyleKeyword.None
                : new StyleBackground(contents.Picture);

            MarkDirtyRepaint();
        }

        /// <summary>
        /// The indicators, lit from the left.
        ///
        /// **Filled in order, never at random positions.** A grid that lights scattered cells reads
        /// as noise; one that fills from one end reads as a gauge, and a player learns to see a
        /// half-lit switch as a half-used one without anybody printing a number next to it.
        /// </summary>
        private void Draw(MeshGenerationContext context)
        {
            var grid = fill.Lights;

            if (grid.IsEmpty || fill.Picture == null)
            {
                return;
            }

            var width = contentRect.width;
            var height = contentRect.height;

            if (width <= 2f || height <= 2f)
            {
                return;
            }

            var count = grid.Columns * grid.Rows;
            var on = (int)Math.Round(fill.Lit * count);

            if (on <= 0)
            {
                return;
            }

            var painter = context.painter2D;
            var colour = fill.Hot ? Throttling : Working;

            painter.fillColor = colour;

            var x0 = width * grid.Left;
            var x1 = width * grid.Right;
            var y0 = height * grid.Top;
            var y1 = height * grid.Bottom;

            var cellWidth = (x1 - x0) / grid.Columns;
            var cellHeight = (y1 - y0) / grid.Rows;

            if (cellWidth <= 0.4f || cellHeight <= 0.4f)
            {
                return;
            }

            var drawn = 0;

            for (var row = 0; row < grid.Rows && drawn < on; row++)
            {
                for (var column = 0; column < grid.Columns && drawn < on; column++)
                {
                    var cell = new Rect(
                        x0 + column * cellWidth,
                        y0 + row * cellHeight,
                        cellWidth,
                        cellHeight);

                    if (grid.Radius > 0f)
                    {
                        var radius = Mathf.Min(cell.width, cell.height) * grid.Radius;

                        // Below a pixel a disc renders as nothing, which is worse than not trying.
                        if (radius >= 0.9f)
                        {
                            painter.BeginPath();
                            painter.Arc(cell.center, radius, 0f, 360f);

                            if (grid.Hollow)
                            {
                                painter.strokeColor = colour;
                                painter.lineWidth = Mathf.Max(1.4f, radius * 0.22f);
                                painter.Stroke();
                            }
                            else
                            {
                                painter.Fill();
                            }
                        }
                    }
                    else
                    {
                        var inset = Mathf.Min(cell.width, cell.height) * 0.18f;

                        painter.BeginPath();
                        painter.MoveTo(new Vector2(cell.xMin + inset, cell.yMin + inset));
                        painter.LineTo(new Vector2(cell.xMax - inset, cell.yMin + inset));
                        painter.LineTo(new Vector2(cell.xMax - inset, cell.yMax - inset));
                        painter.LineTo(new Vector2(cell.xMin + inset, cell.yMax - inset));
                        painter.ClosePath();
                        painter.Fill();
                    }

                    drawn++;
                }
            }
        }
    }

    /// <summary>
    /// One cabinet, drawn: the picture, the parts slotted into it, and the lights on.
    ///
    /// **The picture is the dark base and the game draws every lit thing over it.** That is the
    /// rule the art was commissioned to, and it is what lets a cabinet that is throttling look
    /// different from an identical one that is not, without a second set of drawings.
    /// </summary>
    public sealed class RackFace : VisualElement
    {
        private readonly List<RackSlot> children = new();

        public RackFace()
        {
            AddToClassList("rackface");
        }

        /// <summary>How many slots this face is currently drawing. Used by tests.</summary>
        public int SlotCount => children.Count;

        /// <summary>
        /// Sets what this cabinet is and what is in it, top slot first.
        ///
        /// The slot elements are reused rather than rebuilt, so a cabinet redrawn once a day does
        /// not throw away and remake sixteen elements every second and a half.
        /// </summary>
        public void Show(ServerRack rack, IReadOnlyList<SlotFill> fills)
        {
            var picture = RackArt.Cabinet(rack);

            // A missing drawing leaves a plain plate rather than a hole, which is why this room
            // worked before any of the art existed and why one part can be added at a time.
            style.backgroundImage = picture == null
                ? StyleKeyword.None
                : new StyleBackground(picture);

            var wanted = fills?.Count ?? 0;

            while (children.Count < wanted)
            {
                var slot = new RackSlot();
                children.Add(slot);
                Add(slot);
            }

            while (children.Count > wanted)
            {
                var last = children[^1];
                children.RemoveAt(children.Count - 1);
                last.RemoveFromHierarchy();
            }

            if (wanted == 0)
            {
                return;
            }

            var interior = RackArt.InteriorOf(rack);
            var each = interior.height / wanted;

            for (var index = 0; index < wanted; index++)
            {
                var slot = children[index];

                slot.style.position = Position.Absolute;
                slot.style.left = Length.Percent(interior.x * 100f);
                slot.style.width = Length.Percent(interior.width * 100f);
                slot.style.top = Length.Percent((interior.y + index * each) * 100f);
                slot.style.height = Length.Percent(each * 100f);

                slot.Show(fills[index]);
            }
        }
    }
}
