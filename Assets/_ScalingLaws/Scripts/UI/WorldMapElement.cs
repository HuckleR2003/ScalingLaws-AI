using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The world, drawn rather than imported.
    ///
    /// A texture would have to be sourced, licensed, made to match the palette and then remade every
    /// time the palette moves. Painter2D draws the same shapes from a dozen lines of coordinates,
    /// scales to any size without blurring, and recolours itself when a region is selected. The
    /// outlines are deliberately coarse: this is a control for picking one of three places, not an
    /// atlas, and a blocky silhouette reads better at 300 pixels tall than an accurate coastline.
    ///
    /// Clicks are handled by transparent buttons laid over each landmass rather than by hit-testing
    /// the polygons, so hover, focus and keyboard navigation all work the way they do everywhere else.
    /// </summary>
    public sealed class WorldMapElement : VisualElement
    {
        private static readonly Vector2[] NorthAmerica =
        {
            new(0.075f, 0.20f), new(0.200f, 0.13f), new(0.290f, 0.15f), new(0.270f, 0.24f),
            new(0.300f, 0.28f), new(0.260f, 0.34f), new(0.220f, 0.40f), new(0.200f, 0.47f),
            new(0.170f, 0.42f), new(0.140f, 0.33f), new(0.090f, 0.28f)
        };

        private static readonly Vector2[] SouthAmerica =
        {
            new(0.215f, 0.52f), new(0.270f, 0.51f), new(0.300f, 0.57f), new(0.290f, 0.66f),
            new(0.260f, 0.76f), new(0.240f, 0.88f), new(0.215f, 0.83f), new(0.205f, 0.70f),
            new(0.190f, 0.60f)
        };

        private static readonly Vector2[] Europe =
        {
            new(0.445f, 0.155f), new(0.520f, 0.140f), new(0.555f, 0.190f), new(0.545f, 0.250f),
            new(0.505f, 0.290f), new(0.475f, 0.330f), new(0.455f, 0.280f), new(0.440f, 0.220f)
        };

        private static readonly Vector2[] Asia =
        {
            new(0.555f, 0.140f), new(0.660f, 0.110f), new(0.780f, 0.130f), new(0.870f, 0.190f),
            new(0.855f, 0.280f), new(0.800f, 0.330f), new(0.750f, 0.300f), new(0.710f, 0.360f),
            new(0.660f, 0.440f), new(0.620f, 0.500f), new(0.600f, 0.420f), new(0.575f, 0.330f),
            new(0.545f, 0.250f)
        };

        private static readonly Vector2[] Africa =
        {
            new(0.455f, 0.400f), new(0.530f, 0.380f), new(0.575f, 0.430f), new(0.565f, 0.550f),
            new(0.535f, 0.660f), new(0.505f, 0.780f), new(0.475f, 0.700f), new(0.465f, 0.570f),
            new(0.445f, 0.470f)
        };

        private static readonly Vector2[] Oceania =
        {
            new(0.790f, 0.630f), new(0.870f, 0.620f), new(0.905f, 0.680f), new(0.885f, 0.750f),
            new(0.820f, 0.760f), new(0.785f, 0.700f)
        };

        private static readonly Color Water = new(0.055f, 0.075f, 0.105f);
        private static readonly Color Land = new(0.16f, 0.20f, 0.26f);
        private static readonly Color LandEdge = new(0.26f, 0.33f, 0.42f);
        private static readonly Color Selectable = new(0.20f, 0.28f, 0.40f);
        private static readonly Color Selected = new(0.25f, 0.48f, 0.78f);
        private static readonly Color SelectedEdge = new(0.55f, 0.78f, 1.00f);

        private readonly Action<WorldRegion> onPick;
        private WorldRegion selected;

        public WorldMapElement(WorldRegion current, Action<WorldRegion> onPick)
        {
            this.onPick = onPick;
            selected = current;

            AddToClassList("world-map");
            generateVisualContent += Draw;

            // Boxes cover the drawn silhouette closely enough that the cursor never has to hunt.
            AddHotspot(WorldRegion.America, "AMERICA", 5f, 10f, 30f, 82f);
            AddHotspot(WorldRegion.Europe, "EUROPE", 42f, 11f, 16f, 25f);
            AddHotspot(WorldRegion.Asia, "ASIA", 59f, 8f, 33f, 45f);
        }

        public void Select(WorldRegion region)
        {
            selected = region;
            MarkDirtyRepaint();

            foreach (var child in Children())
            {
                if (child is Button button && button.userData is WorldRegion tagged)
                {
                    button.EnableInClassList("world-map__pick--on", tagged == region);
                }
            }
        }

        private void AddHotspot(WorldRegion region, string label, float left, float top, float width, float height)
        {
            var button = new Button(() =>
            {
                Select(region);
                onPick?.Invoke(region);
            })
            {
                text = label,
                userData = region
            };

            button.AddToClassList("world-map__pick");
            button.EnableInClassList("world-map__pick--on", region == selected);
            button.style.position = Position.Absolute;
            button.style.left = Length.Percent(left);
            button.style.top = Length.Percent(top);
            button.style.width = Length.Percent(width);
            button.style.height = Length.Percent(height);
            Add(button);
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (rect.width <= 1f || rect.height <= 1f || float.IsNaN(rect.width))
            {
                return;
            }

            var painter = context.painter2D;

            painter.fillColor = Water;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2(rect.width, 0f));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0f, rect.height));
            painter.ClosePath();
            painter.Fill();

            DrawLandmass(painter, rect, Africa, WorldRegion.None);
            DrawLandmass(painter, rect, Oceania, WorldRegion.None);
            DrawLandmass(painter, rect, NorthAmerica, WorldRegion.America);
            DrawLandmass(painter, rect, SouthAmerica, WorldRegion.America);
            DrawLandmass(painter, rect, Europe, WorldRegion.Europe);
            DrawLandmass(painter, rect, Asia, WorldRegion.Asia);
        }

        private void DrawLandmass(Painter2D painter, Rect rect, IReadOnlyList<Vector2> shape, WorldRegion region)
        {
            var isSelected = region != WorldRegion.None && region == selected;

            painter.fillColor = region == WorldRegion.None
                ? Land
                : isSelected ? Selected : Selectable;
            painter.strokeColor = isSelected ? SelectedEdge : LandEdge;
            painter.lineWidth = isSelected ? 2f : 1f;

            painter.BeginPath();
            painter.MoveTo(new Vector2(shape[0].x * rect.width, shape[0].y * rect.height));
            for (var index = 1; index < shape.Count; index++)
            {
                painter.LineTo(new Vector2(shape[index].x * rect.width, shape[index].y * rect.height));
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
    }
}
