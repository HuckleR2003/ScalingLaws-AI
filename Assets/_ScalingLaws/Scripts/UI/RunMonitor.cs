using System;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The screen beside the creator's controls: what the run being described would actually do.
    ///
    /// **It replaces a photograph, and that is the point.** A stock picture of a server hall took a
    /// third of the width of the busiest screen in the game and answered nothing. This project has
    /// already reached this answer twice: `BrowserPreview` is a live mock rather than a screenshot
    /// because a screenshot becomes a lie the moment the company is renamed, and `PortraitStudio`
    /// renders the prefab the game will spawn rather than a picture of somebody else.
    ///
    /// **Nothing here is on the strip above it.** The four figures across the top of the creator are
    /// the capability, the ceiling, the calendar and the bill. Two statements of one fact on one
    /// screen is the disagreement with a date on it, so this shows the four things that strip cannot:
    /// how much of the fleet the run takes, what the bill is against the money in the bank, where the
    /// model would land against the frontier, and what a token costs to serve.
    ///
    /// **The curve is a shape, not a reading.** Its ends are the projection's own, and it carries no
    /// axis numbers, because the honest thing to say about a run that has not happened is roughly
    /// this steep rather than a figure to two decimal places.
    /// </summary>
    public sealed class RunMonitor : VisualElement
    {
        /// <summary>One frame of the screen. Everything on it is read, nothing is invented.</summary>
        public readonly struct Reading
        {
            public Reading(double clusterShare, double billAgainstCash, double capability,
                double frontier, double tokensPerText, bool feasible)
            {
                ClusterShare = Clamp01(clusterShare);
                BillAgainstCash = Clamp01(billAgainstCash);
                Capability = Math.Max(0.0, capability);
                Frontier = Math.Max(1.0, frontier);
                TokensPerText = Math.Clamp(tokensPerText, 0.5, 1.0);
                Feasible = feasible;
            }

            /// <summary>Share of the fleet this run would take while it is in flight.</summary>
            public double ClusterShare { get; }

            /// <summary>The bill as a share of the money in the bank. One is the whole bank.</summary>
            public double BillAgainstCash { get; }

            public double Capability { get; }

            /// <summary>The best capability on the market today, which is what it lands against.</summary>
            public double Frontier { get; }

            public double TokensPerText { get; }

            /// <summary>False while the plan cannot be built at all. The screen says so plainly.</summary>
            public bool Feasible { get; }

            private static double Clamp01(double value) =>
                double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);
        }

        private static readonly Color Ink = new(0.31f, 0.69f, 0.91f);
        private static readonly Color Rule = new(0.11f, 0.153f, 0.204f);

        private readonly VisualElement curve;
        private readonly Label status;
        private readonly Meter cluster;
        private readonly Meter bill;
        private readonly Meter against;
        private readonly Meter perText;

        private Reading reading = new(0.0, 0.0, 0.0, 1.0, 1.0, true);

        public RunMonitor()
        {
            AddToClassList("runmon");

            var head = new VisualElement();
            head.AddToClassList("runmon__head");

            var title = new Label(Loc.T("monitor.title"));
            title.AddToClassList("runmon__title");
            head.Add(title);

            var lamp = new VisualElement();
            lamp.AddToClassList("runmon__lamp");
            head.Add(lamp);

            Add(head);

            curve = new VisualElement();
            curve.AddToClassList("runmon__curve");
            curve.generateVisualContent += DrawCurve;
            Add(curve);

            status = new Label(Loc.T("monitor.shape"));
            status.AddToClassList("runmon__status");
            Add(status);

            cluster = new Meter("monitor.cluster", new Color(0.31f, 0.69f, 0.91f));
            bill = new Meter("monitor.bill", new Color(0.878f, 0.639f, 0.333f));
            against = new Meter("monitor.against", new Color(0.349f, 0.788f, 0.541f));
            perText = new Meter("monitor.per_text", new Color(0.690f, 0.424f, 0.878f));

            Add(cluster);
            Add(bill);
            Add(against);
            Add(perText);
        }

        /// <summary>Hands the screen a new frame. Called from the creator's own repricing.</summary>
        public void Show(Reading next)
        {
            reading = next;

            cluster.Set(reading.ClusterShare, UiFormat.Percent(reading.ClusterShare));
            bill.Set(reading.BillAgainstCash, UiFormat.Percent(reading.BillAgainstCash));

            // Against the frontier rather than out of a hundred: what matters about a model is where
            // it lands beside what is already on the market, and the same 60 is a triumph in 2023
            // and an also-ran in 2028.
            var share = reading.Capability / reading.Frontier;
            against.Set(Math.Clamp(share, 0.0, 1.0), UiFormat.Number(reading.Capability, 1));

            // The whole bar is the off-the-shelf vocabulary, so a shorter bar is a cheaper token.
            perText.Set(reading.TokensPerText, UiFormat.Percent(reading.TokensPerText));

            status.text = reading.Feasible ? Loc.T("monitor.shape") : Loc.T("monitor.blocked");
            status.EnableInClassList("runmon__status--blocked", !reading.Feasible);

            curve.MarkDirtyRepaint();
        }

        /// <summary>
        /// The run, drawn: steep at the start and flattening, which is what a training curve does and
        /// what the scaling law this game is named after says it must do.
        ///
        /// How far it climbs is the projection against the frontier. A plan that would land level
        /// with the best model on the market reaches the top of the box; one that would land at half
        /// of it reaches halfway. Nothing is written on the axes.
        /// </summary>
        private void DrawCurve(MeshGenerationContext context)
        {
            var width = curve.contentRect.width;
            var height = curve.contentRect.height;

            if (width <= 1f || height <= 1f)
            {
                return;
            }

            var painter = context.painter2D;

            painter.strokeColor = Rule;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(2f, height - 6f));
            painter.LineTo(new Vector2(width - 2f, height - 6f));
            painter.Stroke();

            var reach = (float)Math.Clamp(reading.Capability / reading.Frontier, 0.04, 1.0);
            var top = height - 6f - (height - 14f) * reach;

            painter.strokeColor = reading.Feasible ? Ink : new Color(0.88f, 0.35f, 0.42f);
            painter.lineWidth = 1.8f;
            painter.BeginPath();

            for (var step = 0; step <= 48; step++)
            {
                var along = step / 48f;

                // Diminishing returns, drawn: most of the climb happens early and the tail is the
                // part a longer run is paying for.
                var climb = 1f - Mathf.Exp(-3.1f * along);
                var x = 3f + (width - 6f) * along;
                var y = height - 6f - (height - 6f - top) * climb;

                if (step == 0)
                {
                    painter.MoveTo(new Vector2(x, y));
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            painter.Stroke();
        }

        /// <summary>One labelled bar. Four of them, and they are the whole screen under the curve.</summary>
        private sealed class Meter : VisualElement
        {
            private readonly Label figure;
            private readonly VisualElement fill;

            public Meter(string captionKey, Color colour)
            {
                AddToClassList("runmon__meter");

                var row = new VisualElement();
                row.AddToClassList("runmon__mrow");

                var caption = new Label(Loc.T(captionKey));
                caption.AddToClassList("runmon__mcap");
                row.Add(caption);

                figure = new Label("0");
                figure.AddToClassList("runmon__mfig");
                row.Add(figure);

                Add(row);

                var track = new VisualElement();
                track.AddToClassList("runmon__track");

                fill = new VisualElement();
                fill.AddToClassList("runmon__fill");
                fill.style.backgroundColor = colour;
                track.Add(fill);

                Add(track);
            }

            public void Set(double share, string reads)
            {
                fill.style.width = Length.Percent((float)(Math.Clamp(share, 0.0, 1.0) * 100.0));
                figure.text = reads;
            }
        }
    }
}
