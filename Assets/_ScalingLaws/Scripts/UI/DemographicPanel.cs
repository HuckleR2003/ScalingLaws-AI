using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The pie. One slice per lab, plus a hollow slice for the market nobody holds yet.
    ///
    /// Drawn with Painter2D because USS has no arcs, the same way the world map and the clock dial
    /// are drawn. The player is always the first slice and always the brightest, so the question
    /// "how much of this is mine" is answered before any label is read.
    /// </summary>
    public sealed class MarketPie : VisualElement
    {
        private static readonly Color PlayerColour = new(0.36f, 0.70f, 0.98f);
        private static readonly Color Unserved = new(0.16f, 0.19f, 0.24f);
        private static readonly Color Edge = new(0.06f, 0.07f, 0.09f);

        /// <summary>
        /// Rival colours. Fixed and repeating rather than generated, so a lab keeps the same colour
        /// between two readings of the same chart, which is the only way a pie is readable at all.
        /// </summary>
        private static readonly Color[] RivalColours =
        {
            new(0.85f, 0.36f, 0.32f), new(0.94f, 0.72f, 0.28f), new(0.44f, 0.76f, 0.46f),
            new(0.70f, 0.52f, 0.88f), new(0.36f, 0.62f, 0.72f), new(0.88f, 0.52f, 0.36f),
            new(0.56f, 0.60f, 0.72f), new(0.78f, 0.42f, 0.60f)
        };

        private double[] slices = Array.Empty<double>();
        private double unservedShare;

        public MarketPie()
        {
            AddToClassList("market-pie");
            generateVisualContent += Draw;
        }

        public static Color ColourFor(int ownerIndex) =>
            ownerIndex <= 0 ? PlayerColour : RivalColours[(ownerIndex - 1) % RivalColours.Length];

        /// <summary>Shares in owner order, index zero the player. They need not sum to one.</summary>
        public void Set(IReadOnlyList<double> ownerShares, double unserved)
        {
            slices = new double[ownerShares?.Count ?? 0];
            for (var index = 0; index < slices.Length; index++)
            {
                slices[index] = Math.Max(0.0, ownerShares[index]);
            }

            unservedShare = Math.Clamp(unserved, 0.0, 1.0);
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
            var centre = new Vector2(rect.width / 2f, rect.height / 2f);
            var radius = Math.Min(rect.width, rect.height) / 2f - 2f;

            var total = unservedShare;
            for (var index = 0; index < slices.Length; index++)
            {
                total += slices[index];
            }

            if (total <= 0.0)
            {
                painter.fillColor = Unserved;
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Fill();
                return;
            }

            // Twelve o'clock, clockwise. A pie that starts anywhere else is harder to compare at a
            // glance against the last time the player looked at it.
            var at = -90f;

            for (var index = 0; index < slices.Length; index++)
            {
                at = Slice(painter, centre, radius, at, slices[index] / total, ColourFor(index));
            }

            Slice(painter, centre, radius, at, unservedShare / total, Unserved);
        }

        private static float Slice(Painter2D painter, Vector2 centre, float radius, float from,
            double fraction, Color colour)
        {
            var sweep = (float)(fraction * 360.0);
            if (sweep <= 0.05f)
            {
                return from;
            }

            painter.fillColor = colour;
            painter.strokeColor = Edge;
            painter.lineWidth = 1f;

            painter.BeginPath();
            painter.MoveTo(centre);
            painter.Arc(centre, radius, from, from + sweep);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            return from + sweep;
        }
    }

    /// <summary>
    /// The demographic band on the Foundation stage: who the market is made of, and who is winning
    /// each kind of product.
    ///
    /// It reads one <see cref="MarketBreakdown"/> and draws it. It computes nothing itself, because
    /// a panel that does its own arithmetic is a second copy of the rules waiting to disagree with
    /// the first.
    /// </summary>
    public sealed class DemographicPanel
    {
        private readonly MarketPie pie = new();
        private readonly VisualElement rows = new();
        private readonly Label caption = new();
        private readonly Label headline = new();

        private MarketBreakdown breakdown;
        private ModelType selected = ModelType.None;

        public DemographicPanel()
        {
            Root = new VisualElement();
            Root.AddToClassList("demographics");

            var chart = new VisualElement();
            chart.AddToClassList("demographics__chart");
            chart.Add(pie);

            headline.AddToClassList("demographics__headline");
            chart.Add(headline);

            caption.AddToClassList("demographics__caption");
            chart.Add(caption);

            Root.Add(chart);

            rows.AddToClassList("demographics__rows");
            Root.Add(BuildTable());
        }

        public VisualElement Root { get; }

        /// <summary>The category the player is inspecting, or None for the whole market.</summary>
        public ModelType Selected => selected;

        private VisualElement BuildTable()
        {
            var table = new VisualElement();
            table.AddToClassList("demographics__table");

            var header = new VisualElement();
            header.AddToClassList("demo-row");
            header.AddToClassList("demo-row--header");

            header.Add(HeaderCell("MODEL TYPE", "demo-row__name"));
            header.Add(HeaderCell("USERS", "demo-row__users"));
            header.Add(HeaderCell("SHARE", "demo-row__share"));
            header.Add(HeaderCell("LEADER", "demo-row__leader"));

            table.Add(header);
            table.Add(rows);
            return table;
        }

        private static Label HeaderCell(string text, string cellClass)
        {
            var label = new Label(text);
            label.AddToClassList(cellClass);
            label.AddToClassList("demo-row__head");
            return label;
        }

        public void Show(MarketBreakdown latest)
        {
            breakdown = latest;
            Render();
        }

        private void Render()
        {
            rows.Clear();

            if (breakdown == null || breakdown.Types.Count == 0)
            {
                headline.text = Loc.T("demo.no_market");
                caption.text = Loc.T("demo.nobody_served");
                pie.Set(Array.Empty<double>(), 1.0);
                return;
            }

            if (selected == ModelType.None)
            {
                pie.Set(breakdown.OwnerUsersOverall, breakdown.UnservedShare);

                // The headline is the whole market, not the part of it somebody has already won.
                //
                // It used to be the served total, so in January 2022, when eight hundred thousand
                // people were in the market and nobody had shipped anything yet, every figure on this
                // panel read zero. It was true and it looked like a broken screen: as though there
                // were no people in the world rather than nobody serving them.
                headline.text = UiFormat.Count(breakdown.AddressableUsers);

                caption.text = breakdown.TotalUsersOverall <= 0.0
                    ? "people who would use one of these, and nobody is serving them yet."
                    : $"people in the market. {UiFormat.Percent(1.0 - breakdown.UnservedShare)} are "
                        + $"using something, and you hold "
                        + $"{UiFormat.Percent(breakdown.OverallShareOf(0))} of those.";
            }
            else if (breakdown.TryGetType(selected, out var standing))
            {
                pie.Set(standing.OwnerUsers, 0.0);
                headline.text = UiFormat.Count(standing.TotalUsers);
                caption.text = Loc.T("demo.users_led_by", ModelTypeCatalog.Get(selected).DisplayName.ToLowerInvariant(),
                    standing.LeaderName, UiFormat.Percent(standing.ShareOf(standing.LeaderIndex)));
            }

            foreach (var standing in breakdown.Types)
            {
                rows.Add(BuildRow(standing));
            }
        }

        private VisualElement BuildRow(TypeStanding standing)
        {
            var definition = ModelTypeCatalog.Get(standing.Type);
            var picked = standing.Type == selected;

            var row = new Button(() =>
            {
                // Clicking the selected row again returns to the whole market, so the panel never
                // traps the player in a category they only wanted to glance at.
                selected = picked ? ModelType.None : standing.Type;
                Render();
            });

            row.AddToClassList("demo-row");
            row.EnableInClassList("demo-row--on", picked);

            var swatch = new VisualElement();
            swatch.AddToClassList("demo-row__swatch");
            swatch.style.backgroundColor = MarketPie.ColourFor(standing.LeaderIndex);
            row.Add(swatch);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("demo-row__name");
            row.Add(name);

            // A dash rather than a zero. Ten rows of "0" read as a fault; a dash reads as a category
            // nobody is in yet, which is what it is.
            var users = new Label(standing.TotalUsers > 0.0
                ? UiFormat.Count(standing.TotalUsers)
                : "-");
            users.AddToClassList("demo-row__users");
            row.Add(users);

            var share = new Label(standing.TotalUsers > 0.0
                ? UiFormat.Percent(standing.PlayerShare)
                : "-");
            share.AddToClassList("demo-row__share");
            share.EnableInClassList("demo-row__share--held", standing.PlayerUsers > 0.0);
            row.Add(share);

            var leader = new Label(standing.TotalUsers > 0.0
                ? standing.LeaderName
                : Loc.T("demo.leader_nobody"));
            leader.AddToClassList("demo-row__leader");
            leader.EnableInClassList("demo-row__leader--you", standing.LeaderIndex <= 0);
            row.Add(leader);

            row.tooltip = Loc.T("demo.row_note", definition.Description,
                UiFormat.Count(standing.PlayerUsers), UiFormat.Count(standing.TotalUsers));

            return row;
        }
    }
}
