using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Simulation;
using UnityEngine;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The cash flow bars along the top of the report, one per month.
    ///
    /// Drawn with Painter2D for the same reason everything else in this project is: USS has no way to
    /// draw a column of arbitrary height from data. Green above the line, red below, and the line
    /// itself is where a month broke even.
    /// </summary>
    public sealed class FinanceChart : VisualElement
    {
        private static readonly Color Good = new(0.36f, 0.72f, 0.48f);
        private static readonly Color Bad = new(0.80f, 0.34f, 0.32f);
        private static readonly Color Axis = new(0.40f, 0.44f, 0.52f);
        private static readonly Color Picked = new(0.94f, 0.94f, 0.96f);

        private long[] values = Array.Empty<long>();
        private int selected = -1;

        public FinanceChart()
        {
            AddToClassList("finance-chart");
            generateVisualContent += Draw;
        }

        public void Set(IReadOnlyList<long> series, int selectedIndex)
        {
            values = new long[series?.Count ?? 0];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = series[index];
            }

            selected = selectedIndex;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (float.IsNaN(rect.width) || rect.width <= 4f || rect.height <= 4f || values.Length == 0)
            {
                return;
            }

            var painter = context.painter2D;

            // Scaled to the largest swing in either direction, so a good month and a bad one of the
            // same size are the same height. Scaling each side separately would make a small loss look
            // like a catastrophe next to a large profit.
            var extent = 1L;
            foreach (var value in values)
            {
                extent = Math.Max(extent, Math.Abs(value));
            }

            var middle = rect.height / 2f;
            var slot = rect.width / values.Length;
            var barWidth = Mathf.Max(1f, slot * 0.62f);

            painter.strokeColor = Axis;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, middle));
            painter.LineTo(new Vector2(rect.width, middle));
            painter.Stroke();

            for (var index = 0; index < values.Length; index++)
            {
                var height = (float)(Math.Abs(values[index]) / (double)extent) * (middle - 2f);
                if (height < 1f)
                {
                    height = 1f;
                }

                var left = index * slot + (slot - barWidth) / 2f;
                var top = values[index] >= 0 ? middle - height : middle;

                painter.fillColor = index == selected
                    ? Picked
                    : values[index] >= 0 ? Good : Bad;

                painter.BeginPath();
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(left + barWidth, top));
                painter.LineTo(new Vector2(left + barWidth, top + height));
                painter.LineTo(new Vector2(left, top + height));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }

    /// <summary>
    /// The financial report.
    ///
    /// It reads <see cref="Ledger"/> and prints it. It adds nothing up that the ledger has not already
    /// recorded, which is what lets the bottom line here be the same number as the bank balance.
    /// Green is money in, red is money out, and the eye should be able to find the worst line in the
    /// month without reading a single figure.
    /// </summary>
    public sealed class FinanceReport
    {
        private readonly Func<Ledger> books;
        private readonly Func<GameDate> today;
        private readonly FinanceChart chart = new();
        private readonly VisualElement rows = new();
        private readonly Label headline = new();
        private readonly Label caption = new();
        private readonly Button monthly;
        private readonly Button daily;

        private bool showDays;
        private int monthKey = -1;

        public FinanceReport(Func<Ledger> books, Func<GameDate> today, Action close)
        {
            this.books = books;
            this.today = today;

            Root = new VisualElement();
            Root.AddToClassList("finance");

            var head = new VisualElement();
            head.AddToClassList("finance__head");

            var title = new Label(Loc.T("books.where_money_went"));
            title.AddToClassList("finance__title");
            head.Add(title);

            monthly = Toggle("BY MONTH", () => { showDays = false; Render(); });
            daily = Toggle("BY DAY", () => { showDays = true; Render(); });
            head.Add(monthly);
            head.Add(daily);

            var dismiss = new Button(close) { text = Loc.T("common.close") };
            dismiss.AddToClassList("button");
            dismiss.style.marginLeft = 10;
            dismiss.style.marginTop = 0;
            dismiss.style.marginRight = 0;
            dismiss.style.marginBottom = 0;
            head.Add(dismiss);

            Root.Add(head);

            headline.AddToClassList("finance__headline");
            Root.Add(headline);

            caption.AddToClassList("finance__caption");
            Root.Add(caption);

            Root.Add(chart);

            rows.AddToClassList("finance__rows");
            Root.Add(rows);
        }

        public VisualElement Root { get; }

        private static Button Toggle(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("finance__toggle");
            return button;
        }

        /// <summary>Opens on the month currently being played.</summary>
        public void Open()
        {
            monthKey = Ledger.MonthKeyOf(today());
            Render();
        }

        private void Render()
        {
            monthly.EnableInClassList("finance__toggle--on", !showDays);
            daily.EnableInClassList("finance__toggle--on", showDays);

            var recorded = books().RecordedMonths();
            rows.Clear();

            if (recorded.Count == 0)
            {
                headline.text = Loc.T("finance.nothing_recorded");
                caption.text = Loc.T("finance.books_start");
                chart.Set(Array.Empty<long>(), -1);
                return;
            }

            if (monthKey < 0 || !recorded.Contains(monthKey))
            {
                monthKey = recorded[^1];
            }

            var series = new List<long>(recorded.Count);
            foreach (var key in recorded)
            {
                series.Add(books().MonthCashFlow(key));
            }

            chart.Set(series, recorded.IndexOf(monthKey));

            var flow = books().MonthCashFlow(monthKey);
            headline.text = (flow >= 0 ? "+" : "-") + UiFormat.Money(Math.Abs(flow));
            headline.EnableInClassList("finance__headline--up", flow >= 0);
            headline.EnableInClassList("finance__headline--down", flow < 0);

            caption.text = showDays
                ? $"{MonthName(monthKey)}, day by day. "
                    + $"In {UiFormat.Money(books().MonthIncome(monthKey))}, "
                    + $"out {UiFormat.Money(books().MonthCost(monthKey))}."
                : $"{MonthName(monthKey)}. In {UiFormat.Money(books().MonthIncome(monthKey))}, "
                    + $"out {UiFormat.Money(books().MonthCost(monthKey))}.";

            RenderGroup("Model");
            RenderGroup("Company");
            RenderGroup("Capital");
        }

        /// <summary>
        /// One heading and the lines under it. A group with nothing in it is left out rather than
        /// printed as a row of zeroes, because a report full of zeroes is harder to read than a short
        /// one.
        /// </summary>
        private void RenderGroup(string group)
        {
            var any = false;
            var block = new VisualElement();

            foreach (var info in Ledger.Lines)
            {
                if (info.Group != group)
                {
                    continue;
                }

                var amount = Amount(info.Line);
                if (amount == 0L)
                {
                    continue;
                }

                if (!any)
                {
                    var heading = new Label(group.ToUpperInvariant());
                    heading.AddToClassList("finance__group");
                    block.Add(heading);
                    any = true;
                }

                block.Add(Row(info, amount));
            }

            if (any)
            {
                rows.Add(block);
            }
        }

        private long Amount(LedgerLine line)
        {
            if (!showDays)
            {
                return books().MonthTotal(monthKey, line);
            }

            // Day view only has the month being played, because that is the only one kept day by day.
            // Older months are monthly totals and pretending otherwise would invent a shape for them.
            var total = 0L;
            for (var day = 1; day <= 31; day++)
            {
                total += books().DayTotal(day, line);
            }

            return total;
        }

        private VisualElement Row(LedgerLineInfo info, long amount)
        {
            var row = new VisualElement();
            row.AddToClassList("finance-row");

            var name = new Label(info.DisplayName);
            name.AddToClassList("finance-row__name");
            row.Add(name);

            if (!info.IsCash)
            {
                var tag = new Label(Loc.T("books.not_cash"));
                tag.AddToClassList("finance-row__tag");
                row.Add(tag);
            }

            var value = new Label((info.IsIncome ? "+" : "-") + UiFormat.Money(amount));
            value.AddToClassList("finance-row__value");
            value.EnableInClassList("finance-row__value--in", info.IsIncome);
            value.EnableInClassList("finance-row__value--out", !info.IsIncome);
            row.Add(value);

            return row;
        }

        private static string MonthName(int key)
        {
            var year = key / 12;
            var month = key % 12 + 1;
            return $"{year}-{month:00}";
        }
    }
}
