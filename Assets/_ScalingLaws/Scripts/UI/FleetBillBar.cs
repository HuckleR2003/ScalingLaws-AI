using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// One horizontal bar showing what a day of running the fleet is made of.
    ///
    /// Proportion rather than magnitude is the point. A player looking at a single daily figure
    /// cannot tell whether it is rent they could cancel tomorrow or electricity they are committed
    /// to for as long as they own the hardware, and those are completely different problems.
    ///
    /// Painter2D because USS cannot lay four proportional segments side by side without either
    /// fixed percentages in the stylesheet or a wrapper element per segment.
    /// </summary>
    public sealed class FleetBillBar : VisualElement
    {
        public static readonly Color RentColour = new(0.36f, 0.62f, 0.88f);
        public static readonly Color PowerColour = new(0.92f, 0.70f, 0.30f);
        public static readonly Color HousingColour = new(0.52f, 0.46f, 0.74f);
        public static readonly Color UpkeepColour = new(0.74f, 0.42f, 0.38f);

        private static readonly Color Empty = new(0.16f, 0.19f, 0.24f);

        private FleetBill bill;

        public FleetBillBar()
        {
            AddToClassList("fleet-bar");
            generateVisualContent += Draw;
        }

        public void Set(FleetBill value)
        {
            bill = value;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 2f || rect.height <= 2f)
            {
                return;
            }

            var painter = context.painter2D;
            var total = bill.TotalUsd;

            if (total <= 0.0)
            {
                Segment(painter, 0f, rect.width, rect.height, Empty);
                return;
            }

            var at = 0f;
            at = Slice(painter, at, rect, bill.CloudRentUsd / total, RentColour);
            at = Slice(painter, at, rect, bill.ElectricityUsd / total, PowerColour);
            at = Slice(painter, at, rect, bill.HousingUsd / total, HousingColour);
            Slice(painter, at, rect, bill.MaintenanceUsd / total, UpkeepColour);
        }

        private static float Slice(Painter2D painter, float from, Rect rect, double fraction,
            Color colour)
        {
            var width = (float)(fraction * rect.width);
            if (width <= 0.4f)
            {
                return from;
            }

            Segment(painter, from, from + width, rect.height, colour);
            return from + width;
        }

        private static void Segment(Painter2D painter, float left, float right, float height,
            Color colour)
        {
            painter.fillColor = colour;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, 0f));
            painter.LineTo(new Vector2(right, 0f));
            painter.LineTo(new Vector2(right, height));
            painter.LineTo(new Vector2(left, height));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
