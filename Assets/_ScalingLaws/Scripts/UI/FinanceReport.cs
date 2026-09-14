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
        private int hovered = -1;

        public FinanceChart()
        {
            AddToClassList("finance-chart");
            generateVisualContent += Draw;

            // **A bar the pointer rests on reports itself.** Asked for by the author: run along the
            // months and watch the figures underneath follow, click one to keep it there.
            RegisterCallback<PointerMoveEvent>(move => Hover(SlotAt(move.localPosition.x)));
            RegisterCallback<PointerLeaveEvent>(_ => Hover(-1));
            RegisterCallback<PointerDownEvent>(down =>
            {
                var slot = SlotAt(down.localPosition.x);

                if (slot >= 0)
                {
                    Clicked?.Invoke(slot);
                }
            });
        }

        /// <summary>The bar under the pointer changed. Minus one when the pointer left.</summary>
        public event Action<int> Hovered;

        /// <summary>A bar was clicked.</summary>
        public event Action<int> Clicked;

        /// <summary>How many bars are drawn. For tests, which cannot look.</summary>
        public int BarCount => values.Length;

        private int SlotAt(float x)
        {
            var width = contentRect.width;

            if (values.Length == 0 || float.IsNaN(width) || width <= 0f)
            {
                return -1;
            }

            return Mathf.Clamp((int)(x / (width / values.Length)), 0, values.Length - 1);
        }

        private void Hover(int slot)
        {
            if (slot == hovered)
            {
                return;
            }

            hovered = slot;
            MarkDirtyRepaint();
            Hovered?.Invoke(slot);
        }

        public void Set(IReadOnlyList<long> series, int selectedIndex)
        {
            values = new long[series?.Count ?? 0];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = series[index];
            }

            selected = selectedIndex;

            if (hovered >= values.Length)
            {
                hovered = -1;
            }

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

                var tone = index == selected
                    ? Picked
                    : values[index] >= 0 ? Good : Bad;

                painter.fillColor = index == hovered && index != selected
                    ? Color.Lerp(tone, Color.white, 0.35f)
                    : tone;

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

        /// <summary>How many days the day view reaches back.</summary>
        public const int DaysShown = 30;

        private readonly VisualElement labels = new();

        /// <summary>What each bar stands for: a month key in the month view, a day index in the day view.</summary>
        private readonly List<int> slots = new();

        private bool showDays;

        /// <summary>The bar that was clicked. Minus one follows the newest bar as days pass.</summary>
        private int pinned = -1;

        /// <summary>The bar the pointer is resting on, or minus one.</summary>
        private int preview = -1;

        // What the figures underneath are about right now, which is the preview when there is one.
        private bool reportingDay;
        private int reportedKey;

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

            monthly = Toggle(Loc.T("finance.by_month"), () => ShowDays(false));
            daily = Toggle(Loc.T("finance.by_day"), () => ShowDays(true));
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

            // Under the bars, one label to a bar, so a bar can be read as a date.
            labels.AddToClassList("finance-chart__labels");
            Root.Add(labels);

            chart.Hovered += Preview;
            chart.Clicked += Pin;

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

        /// <summary>
        /// Switches between the two views. The toggles call this and nothing else does the switch,
        /// so a test can drive it: an EditMode element has no panel, so a click sent to a button is
        /// never dispatched and the lambda behind it would go unmeasured.
        /// </summary>
        public void ShowDays(bool days)
        {
            showDays = days;
            pinned = -1;
            preview = -1;
            Render();
        }

        /// <summary>Opens on the newest bar.</summary>
        public void Open()
        {
            pinned = -1;
            preview = -1;
            Render();
        }

        /// <summary>
        /// Draws it again from the books as they are now.
        ///
        /// **The day view did not move when a day passed**, only when a toggle was pressed again, so
        /// the report a player had open was always behind the company. The shell calls this every
        /// time it refreshes the top bar, which includes every day that rolls over.
        /// </summary>
        public void Refresh() => Render();

        /// <summary>What resting the pointer on a bar does. Minus one goes back to the kept bar.</summary>
        public void Preview(int slot)
        {
            preview = slot >= 0 && slot < slots.Count ? slot : -1;
            RenderBody();
        }

        /// <summary>What clicking a bar does: keep it until another one is clicked.</summary>
        public void Pin(int slot)
        {
            pinned = slot >= 0 && slot < slots.Count ? slot : -1;
            preview = -1;
            Render();
        }

        private void Render()
        {
            monthly.EnableInClassList("finance__toggle--on", !showDays);
            daily.EnableInClassList("finance__toggle--on", showDays);

            var ledger = books();
            var recorded = ledger.RecordedMonths();

            slots.Clear();
            labels.Clear();

            if (recorded.Count == 0)
            {
                rows.Clear();
                headline.text = Loc.T("finance.nothing_recorded");
                headline.EnableInClassList("finance__headline--up", false);
                headline.EnableInClassList("finance__headline--down", false);
                caption.text = Loc.T("finance.books_start");
                chart.Set(Array.Empty<long>(), -1);
                return;
            }

            var series = new List<long>();

            // **The day view is the last thirty days, not the days of this month.** On the first of a
            // month it used to draw one bar. A day with nothing posted is a real zero and keeps its
            // place, so thirty bars are always thirty days.
            if (showDays && ledger.HasRecordedDays)
            {
                var newest = ledger.LastRecordedDayIndex;

                for (var dayIndex = newest - (DaysShown - 1); dayIndex <= newest; dayIndex++)
                {
                    slots.Add(dayIndex);
                    series.Add(ledger.DayIndexCashFlow(dayIndex));
                }
            }
            else
            {
                foreach (var key in recorded)
                {
                    slots.Add(key);
                    series.Add(ledger.MonthCashFlow(key));
                }
            }

            if (pinned >= slots.Count)
            {
                pinned = -1;
            }

            if (preview >= slots.Count)
            {
                preview = -1;
            }

            chart.Set(series, Kept());
            DrawLabels();
            RenderBody();
        }

        /// <summary>The bar being kept: the clicked one, or the newest.</summary>
        private int Kept() => pinned >= 0 ? pinned : slots.Count - 1;

        private bool ShowingDays => showDays && books().HasRecordedDays;

        /// <summary>A date under each bar, thinned so they never run into each other.</summary>
        private void DrawLabels()
        {
            var every = Math.Max(1, (int)Math.Ceiling(slots.Count / 10.0));

            for (var index = 0; index < slots.Count; index++)
            {
                var shown = index % every == 0 || index == slots.Count - 1;

                var label = new Label(shown ? SlotName(index) : string.Empty);
                label.AddToClassList("finance-chart__label");
                label.pickingMode = PickingMode.Ignore;
                labels.Add(label);
            }
        }

        private string SlotName(int index)
        {
            if (ShowingDays)
            {
                var date = new GameDate(slots[index]);
                return $"{date.Day:00}.{date.Month:00}";
            }

            return MonthName(slots[index]);
        }

        /// <summary>The headline, the sentence and the lines, for whichever bar is being read.</summary>
        private void RenderBody()
        {
            rows.Clear();

            if (slots.Count == 0)
            {
                return;
            }

            var slot = preview >= 0 ? preview : Kept();
            var ledger = books();

            reportingDay = ShowingDays;
            reportedKey = slots[slot];

            var flow = reportingDay
                ? ledger.DayIndexCashFlow(reportedKey)
                : ledger.MonthCashFlow(reportedKey);

            headline.text = (flow >= 0 ? "+" : "-") + UiFormat.Money(Math.Abs(flow));
            headline.EnableInClassList("finance__headline--up", flow >= 0);
            headline.EnableInClassList("finance__headline--down", flow < 0);

            // A day view asked for before any day was recorded falls back to the month, and says so,
            // rather than drawing a page of zeroes.
            caption.text = reportingDay
                ? Loc.T("finance.on_day", DayName(reportedKey),
                    UiFormat.Money(ledger.DayIndexIncome(reportedKey)),
                    UiFormat.Money(ledger.DayIndexCost(reportedKey)))
                : showDays
                    ? Loc.T("finance.no_day_yet", MonthName(reportedKey))
                    : Loc.T("finance.in_month", MonthName(reportedKey),
                        UiFormat.Money(ledger.MonthIncome(reportedKey)),
                        UiFormat.Money(ledger.MonthCost(reportedKey)));

            // **Fleet and Trading were never drawn.** The report walked three of the ledger's five
            // groups, so the cloud rent, the electricity, the housing and the power station were
            // in the bank balance and nowhere on the page that explains it.
            RenderGroup("Model");
            RenderGroup("Fleet");
            RenderGroup("Company");
            RenderGroup("Capital");
            RenderGroup("Trading");
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
            // It walked all thirty one days here and returned the month, which is why the two
            // views printed the same lines. The day view only has the month being played, because
            // that is the only one kept day by day, and with no day recorded yet it falls back to
            // the month rather than to a page of zeroes.
            return reportingDay
                ? books().DayIndexTotal(reportedKey, line)
                : books().MonthTotal(reportedKey, line);
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

        /// <summary>A month the way the author asked to read one under a bar: 04.2022.</summary>
        private static string MonthName(int key)
        {
            var year = key / 12;
            var month = key % 12 + 1;
            return $"{month:00}.{year}";
        }

        private static string DayName(int dayIndex)
        {
            var date = new GameDate(dayIndex);
            return $"{date.Day:00}.{date.Month:00}.{date.Year}";
        }
    }
}
