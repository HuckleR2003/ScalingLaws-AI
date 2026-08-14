using System;
using System.Collections.Generic;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The little bar chart inside the model banner: one column a day for the month so far.
    ///
    /// Green above the line, red below, exactly like the finance report, because they are the same
    /// number at two scales and reading them differently would be a small lie.
    /// </summary>
    public sealed class ProductSparkline : VisualElement
    {
        private static readonly Color Good = new(0.30f, 0.68f, 0.38f);
        private static readonly Color Bad = new(0.82f, 0.28f, 0.26f);
        private static readonly Color Rule = new(0.62f, 0.60f, 0.62f);

        private long[] values = Array.Empty<long>();

        public ProductSparkline()
        {
            AddToClassList("mb-chart");
            generateVisualContent += Draw;
        }

        public void Set(IReadOnlyList<long> series)
        {
            values = new long[series?.Count ?? 0];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = series[index];
            }

            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 4f || rect.height <= 4f)
            {
                return;
            }

            var painter = context.painter2D;

            // Two thirds down, not at the very bottom. A losing month is drawn below the line and at
            // six pixels of clearance those bars ran out of the chart and over the money underneath.
            var baseline = rect.height * 0.66f;

            painter.strokeColor = Rule;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, baseline));
            painter.LineTo(new Vector2(rect.width, baseline));
            painter.Stroke();

            if (values.Length == 0)
            {
                return;
            }

            var extent = 1L;
            foreach (var value in values)
            {
                extent = Math.Max(extent, Math.Abs(value));
            }

            var slot = rect.width / values.Length;
            var width = Mathf.Max(2f, slot * 0.66f);

            for (var index = 0; index < values.Length; index++)
            {
                // Scaled to whichever side has less room, so neither direction can escape the box.
                var room = Math.Min(baseline - 3f, rect.height - baseline - 3f);
                var height = (float)(Math.Abs(values[index]) / (double)extent) * room;
                if (height < 2f)
                {
                    height = 2f;
                }

                var left = index * slot + (slot - width) / 2f;
                var up = values[index] >= 0;
                var top = up ? baseline - height : baseline;

                painter.fillColor = up ? Good : Bad;
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(left + width, top));
                painter.LineTo(new Vector2(left + width, top + height));
                painter.LineTo(new Vector2(left, top + height));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>
    /// The product banner, top right.
    ///
    /// It answers one question a player asks constantly and previously had to go looking for: is the
    /// thing I shipped doing well? Four facts, in the order they get asked. Are people happy with it.
    /// Is it still current. How many are paying. Is it making money.
    ///
    /// While a run is in flight it becomes the run instead, because during those weeks that is the
    /// only thing happening and a banner about a product that has not shipped yet would be showing
    /// numbers about nothing.
    /// </summary>
    public sealed class ModelBanner
    {
        private readonly Func<ProductStanding> product;
        private readonly Func<WorkInFlight> inFlight;
        private readonly Func<IReadOnlyList<long>> dailySeries;

        private readonly Label title = new();
        private readonly Button manage;
        private readonly VisualElement body = new();
        private readonly VisualElement training = new();

        private readonly VisualElement happinessFill = new();
        private readonly VisualElement topicalityFill = new();
        private readonly Label topicalityWord = new();
        private readonly Label subscribers = new();
        private readonly ProductSparkline chart = new();
        private readonly Label net = new();
        private readonly Label earnings = new();

        private readonly VisualElement trainingFill = new();
        private readonly Label trainingDays = new();
        private readonly Label trainingCaption = new("TRAINING MODEL");
        private readonly Label chevron = new();

        /// <summary>Folded away by the player, or folded by default because there is nothing to show.</summary>
        private bool collapsed;

        private bool hidden;

        /// <summary>The player asked to see the banner even though nothing has shipped.</summary>
        private bool openedEmpty;

        public ModelBanner(Func<ProductStanding> product, Func<WorkInFlight> inFlight,
            Func<IReadOnlyList<long>> dailySeries, Action openManagement)
        {
            this.product = product;
            this.inFlight = inFlight;
            this.dailySeries = dailySeries;

            Root = new VisualElement();
            Root.AddToClassList("mb");

            var head = new VisualElement();
            head.AddToClassList("mb__head");

            // The title is the collapse control. With no product the banner is one maroon strip and
            // nothing else, and a player running several lines can fold any of them away rather than
            // losing the corner to a stack of panels.
            var fold = new Button(() =>
            {
                if (!product().Exists)
                {
                    openedEmpty = !openedEmpty;
                }
                else
                {
                    collapsed = !collapsed;
                }

                Refresh();
            });
            fold.AddToClassList("mb__fold");
            title.AddToClassList("mb__title");
            fold.Add(title);

            chevron.AddToClassList("mb__chevron");
            fold.Add(chevron);
            head.Add(fold);

            var divider = new Label("|");
            divider.AddToClassList("mb__divider");
            head.Add(divider);

            manage = new Button(openManagement) { text = "Official Page / Management" };
            manage.AddToClassList("mb__manage");
            head.Add(manage);

            Root.Add(head);
            Root.Add(BuildBody());
            Root.Add(BuildTraining());
        }

        public VisualElement Root { get; }

        private VisualElement BuildBody()
        {
            body.AddToClassList("mb__body");

            var meters = new VisualElement();
            meters.AddToClassList("mb__meters");
            meters.Add(Meter("HAPPINESS", happinessFill, "mb-meter__fill--good"));

            var right = Meter("TOPICALITY", topicalityFill, "mb-meter__fill--warm");
            topicalityWord.AddToClassList("mb-meter__word");
            right.Add(topicalityWord);
            meters.Add(right);

            body.Add(meters);

            subscribers.AddToClassList("mb__subs");
            body.Add(subscribers);

            body.Add(chart);

            var footer = new VisualElement();
            footer.AddToClassList("mb__footer");

            footer.Add(FooterCell("NET INCOME", net));
            footer.Add(FooterCell("SUBS. EARNINGS", earnings));
            body.Add(footer);

            return body;
        }

        private static VisualElement Meter(string caption, VisualElement fill, string fillClass)
        {
            var block = new VisualElement();
            block.AddToClassList("mb-meter");

            var label = new Label(caption);
            label.AddToClassList("mb-meter__label");
            block.Add(label);

            var track = new VisualElement();
            track.AddToClassList("mb-meter__track");

            fill.AddToClassList("mb-meter__fill");
            fill.AddToClassList(fillClass);
            track.Add(fill);

            block.Add(track);
            return block;
        }

        private static VisualElement FooterCell(string caption, Label value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("mb-cell");

            var label = new Label(caption);
            label.AddToClassList("mb-cell__label");
            cell.Add(label);

            value.AddToClassList("mb-cell__value");
            cell.Add(value);

            return cell;
        }

        private VisualElement BuildTraining()
        {
            training.AddToClassList("mb__training");
            training.style.display = DisplayStyle.None;

            // The fill sits behind the words rather than beside them, so the whole strip is the
            // progress bar. A run is weeks long and this is the only thing happening; it should read
            // as one object filling up, not as a label with a bar under it.
            trainingFill.AddToClassList("mb__training-fill");
            training.Add(trainingFill);

            trainingCaption.AddToClassList("mb__training-caption");
            training.Add(trainingCaption);

            trainingDays.AddToClassList("mb__training-days");
            training.Add(trainingDays);

            return training;
        }

        /// <summary>Hides the banner entirely, for screens that need the whole window.</summary>
        public void SetHidden(bool value)
        {
            hidden = value;
            Root.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Pushed every frame with the clock, so a run visibly advances.</summary>
        public void Refresh()
        {
            if (hidden)
            {
                return;
            }

            var work = inFlight();

            if (work.Busy)
            {
                ShowWork(work);
                return;
            }

            training.style.display = DisplayStyle.None;

            var standing = product();
            title.text = standing.Name.ToUpperInvariant();

            // Nothing on sale means nothing to report, so the banner is just its own header until
            // there is. The player can still open it to see the month's money.
            var shut = collapsed || !standing.Exists && !openedEmpty;

            body.style.display = shut ? DisplayStyle.None : DisplayStyle.Flex;
            manage.style.display = shut ? DisplayStyle.None : DisplayStyle.Flex;
            chevron.text = shut ? "+" : "-";

            if (shut)
            {
                return;
            }

            happinessFill.style.width = Length.Percent((float)(standing.Happiness * 100.0));
            topicalityFill.style.width = Length.Percent((float)(standing.Topicality * 100.0));
            topicalityWord.text = standing.Exists ? standing.Freshness : string.Empty;

            topicalityFill.EnableInClassList("mb-meter__fill--bad", standing.Topicality < 0.3);

            subscribers.text = standing.Exists
                ? $"Current Subs.  {UiFormat.Count(standing.Subscribers)}"
                : "Nothing on sale yet.";

            chart.Set(dailySeries());

            net.text = (standing.MonthNetUsd >= 0 ? "+" : "-")
                + UiFormat.Money(Math.Abs(standing.MonthNetUsd));

            net.EnableInClassList("mb-cell__value--good", standing.IsProfitable);
            net.EnableInClassList("mb-cell__value--bad", !standing.IsProfitable);

            earnings.text = UiFormat.Money(standing.MonthEarningsUsd);
            earnings.EnableInClassList("mb-cell__value--good", standing.MonthEarningsUsd > 0L);

            manage.tooltip = standing.Exists
                ? $"{standing.Name} is {standing.DaysOld} days old, capability "
                    + $"{UiFormat.Number(standing.Capability)} against a frontier of "
                    + $"{UiFormat.Number(standing.Frontier)}."
                : "Nothing has shipped yet.";
        }

        private void ShowWork(WorkInFlight work)
        {
            body.style.display = DisplayStyle.None;
            manage.style.display = DisplayStyle.None;
            training.style.display = DisplayStyle.Flex;

            title.text = work.Subject.ToUpperInvariant();
            trainingCaption.text = work.Caption;
            trainingFill.style.width = Length.Percent((float)(work.Progress * 100.0));

            // Days left, which is the number a player actually plans around. The percentage is there
            // to make the bar readable, not to be the answer.
            trainingDays.text = work.DaysLeft == 1
                ? $"1 day left   ({work.Progress:P0})"
                : $"{work.DaysLeft:N0} days left   ({work.Progress:P0})";
        }
    }
}
