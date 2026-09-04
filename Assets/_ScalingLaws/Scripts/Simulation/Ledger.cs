using System;
using System.Collections.Generic;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Every reason money moves. Ordered so a report reads top to bottom without sorting.
    /// </summary>
    public enum LedgerLine
    {
        // What comes in.
        Subscriptions = 0,
        Licensing = 1,
        AssetSales = 2,
        Funding = 3,

        /// <summary>
        /// Grant money coming in.
        ///
        /// Its own line rather than folded into Funding for the same reason Investment is: an
        /// advance that has to be handed back if a condition is missed is a different fact from a
        /// round that never does, and a books page that merges them cannot answer how much of the
        /// company's capital is actually at risk.
        /// </summary>
        GrantAward = 4,

        // What goes out.
        CloudRent = 10,
        ServingFree = 11,
        Electricity = 23,
        Housing = 24,
        Maintenance = 25,
        Salaries = 12,
        Marketing = 13,
        Intelligence = 14,
        Research = 15,
        DataAcquisition = 16,
        Hardware = 17,
        Facilities = 18,
        Interest = 19,
        Tax = 20,
        Depreciation = 21,
        Fines = 22,

        /// <summary>
        /// Buying shares in other companies, and buying whole ones.
        ///
        /// Its own line rather than folded into Funding, because money going out to acquire an
        /// asset and money coming in from a lender are opposite facts and a books page that
        /// nets them says nothing about either.
        /// </summary>
        Investment = 26,

        /// <summary>
        /// What a government pays for running its sectors.
        ///
        /// **Its own line rather than folded into subscriptions**, because a books page that merges
        /// them cannot answer the one question the endgame is about: how much of this company's
        /// income depends on one contract. A player reading a single revenue row would not see the
        /// concentration until the contract stopped.
        /// </summary>
        StateProgramme = 28,

        /// <summary>An advance given back because the term was missed.</summary>
        GrantRepaid = 27
    }

    /// <summary>What a line is called and which side of the report it sits on.</summary>
    public readonly struct LedgerLineInfo
    {
        public LedgerLineInfo(LedgerLine line, string displayName, string group, bool isIncome,
            bool isCash)
        {
            Line = line;
            DisplayName = displayName;
            Group = group;
            IsIncome = isIncome;
            IsCash = isCash;
        }

        public LedgerLine Line { get; }
        public string DisplayName { get; }

        /// <summary>The heading this line sits under. Lines sharing a group are shown together.</summary>
        public string Group { get; }

        public bool IsIncome { get; }

        /// <summary>
        /// False for depreciation, which is real and is not cash. Keeping it in the ledger but out of
        /// the cash total is the only way the report can both explain the year and still add up to the
        /// bank balance.
        /// </summary>
        public bool IsCash { get; }
    }

    /// <summary>
    /// The company's books.
    ///
    /// The rule that makes this trustworthy: **nothing here is recomputed**. Every figure is posted at
    /// the moment the money actually moves, by the code that moves it, so the report cannot disagree
    /// with the cash balance. A report that recalculates its own totals is a second copy of the
    /// arithmetic and it will eventually tell the player something the bank does not.
    ///
    /// Kept as monthly totals with the current month also held day by day. A full daily history for a
    /// fifteen year game is a hundred and fifty thousand numbers to save and nobody reads day 412.
    /// </summary>
    public sealed class Ledger
    {
        /// <summary>How many months are kept. Beyond this the oldest month is dropped.</summary>
        public const int MonthsKept = 60;

        private static readonly LedgerLineInfo[] Catalog =
        {
            new(LedgerLine.Subscriptions, "Subscriptions and API", "Model", true, true),
            new(LedgerLine.Licensing, "Licensing", "Model", true, true),
            new(LedgerLine.AssetSales, "Hardware sold", "Capital", true, true),
            new(LedgerLine.Funding, "Funding raised and loans", "Capital", true, true),

            new(LedgerLine.CloudRent, "Cloud capacity rented", "Fleet", false, true),
            new(LedgerLine.Electricity, "Electricity", "Fleet", false, true),
            new(LedgerLine.Housing, "Housing and cooling", "Fleet", false, true),
            new(LedgerLine.Maintenance, "Hardware upkeep", "Fleet", false, true),

            // A memo, not a payment. The free tier does not send an invoice of its own; it eats a
            // share of a fleet bill that is already counted above. Adding it to the cash total would
            // charge the company twice and stop the report reconciling with the bank.
            new(LedgerLine.ServingFree, "of which the free tier ate", "Model", false, false),
            new(LedgerLine.Salaries, "Salaries", "Company", false, true),
            new(LedgerLine.Marketing, "Marketing", "Company", false, true),
            new(LedgerLine.Intelligence, "Intelligence retainer", "Company", false, true),
            new(LedgerLine.Research, "Research and upgrades", "Company", false, true),
            new(LedgerLine.DataAcquisition, "Data licensing", "Company", false, true),
            new(LedgerLine.Hardware, "Hardware bought", "Capital", false, true),
            new(LedgerLine.Facilities, "Facilities", "Capital", false, true),
            new(LedgerLine.Interest, "Debt interest", "Capital", false, true),
            new(LedgerLine.Tax, "Corporate tax", "Company", false, true),
            new(LedgerLine.Depreciation, "Depreciation", "Capital", false, false),
            new(LedgerLine.Fines, "Fines and incidents", "Company", false, true),
            new(LedgerLine.Investment, "Shares and acquisitions", "Capital", false, false),
            new(LedgerLine.GrantAward, "Grants awarded", "Capital", true, true),
            new(LedgerLine.GrantRepaid, "Grant advances returned", "Capital", false, true),
            new(LedgerLine.StateProgramme, "State programme", "Trading", true, true)
        };

        /// <summary>month index (year * 12 + month - 1) to the totals for that month.</summary>
        private readonly Dictionary<int, long[]> months = new();

        /// <summary>Day of the current month, one based, to that day's totals.</summary>
        private readonly Dictionary<int, long[]> currentMonthDays = new();

        private int currentMonthKey = -1;

        /// <summary>
        /// Net cash from every month that has been dropped off the back of the history.
        ///
        /// Without this the books stop explaining the balance the moment the retention window fills.
        /// A stability scan caught it in year six of a fourteen year game: the bank and the ledger
        /// disagreed by exactly the twelve months that had aged out, and the report had quietly
        /// stopped being able to account for the company.
        /// </summary>
        public long CarriedForwardUsd { get; private set; }

        public static IReadOnlyList<LedgerLineInfo> Lines => Catalog;

        public static LedgerLineInfo Info(LedgerLine line)
        {
            foreach (var entry in Catalog)
            {
                if (entry.Line == line)
                {
                    return entry;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(line), line, "Unknown ledger line.");
        }

        public static int MonthKeyOf(GameDate date) => date.Year * 12 + date.Month - 1;

        /// <summary>
        /// Records an amount against a reason. Always positive: whether it adds or subtracts is a
        /// property of the line, not of the caller, so a sign slip at one call site cannot turn a cost
        /// into income.
        /// </summary>
        public void Post(GameDate date, LedgerLine line, long amountUsd)
        {
            if (amountUsd == 0L)
            {
                return;
            }

            var key = MonthKeyOf(date);
            if (key != currentMonthKey)
            {
                currentMonthKey = key;
                currentMonthDays.Clear();
            }

            Bucket(months, key)[IndexOf(line)] += Math.Abs(amountUsd);
            Bucket(currentMonthDays, date.Day)[IndexOf(line)] += Math.Abs(amountUsd);

            Trim();
        }

        /// <summary>Total for one line in one month, or zero if that month recorded nothing.</summary>
        public long MonthTotal(int monthKey, LedgerLine line) =>
            months.TryGetValue(monthKey, out var row) ? row[IndexOf(line)] : 0L;

        /// <summary>Total for one line on one day of the month currently being recorded.</summary>
        public long DayTotal(int day, LedgerLine line) =>
            currentMonthDays.TryGetValue(day, out var row) ? row[IndexOf(line)] : 0L;

        /// <summary>
        /// Everything the books account for, including months whose detail has been dropped. This plus
        /// the starting balance is the bank balance, and a test holds that to the cent.
        /// </summary>
        public long TotalCashFlowUsd
        {
            get
            {
                var total = CarriedForwardUsd;
                foreach (var key in months.Keys)
                {
                    total += MonthCashFlow(key);
                }

                return total;
            }
        }

        /// <summary>Income minus cash costs for a month. Depreciation is deliberately excluded.</summary>
        public long MonthCashFlow(int monthKey)
        {
            var total = 0L;
            foreach (var entry in Catalog)
            {
                if (!entry.IsCash)
                {
                    continue;
                }

                var amount = MonthTotal(monthKey, entry.Line);
                total += entry.IsIncome ? amount : -amount;
            }

            return total;
        }

        public long MonthIncome(int monthKey) => Side(monthKey, true);

        public long MonthCost(int monthKey) => Side(monthKey, false);

        /// <summary>Month keys that recorded anything, oldest first.</summary>
        public List<int> RecordedMonths()
        {
            var keys = new List<int>(months.Keys);
            keys.Sort();
            return keys;
        }

        public bool HasAnything => months.Count > 0;

        /// <summary>Flattens to two parallel lists for the save. Month key, then one total per line.</summary>
        public void Capture(List<int> monthKeys, List<long> amounts)
        {
            monthKeys.Clear();
            amounts.Clear();

            foreach (var key in RecordedMonths())
            {
                monthKeys.Add(key);
                foreach (var total in months[key])
                {
                    amounts.Add(total);
                }
            }
        }

        /// <summary>
        /// Rebuilds from a save. A file written with a different set of lines is dropped rather than
        /// stretched, because a report whose columns have shifted by one is worse than an empty one.
        /// </summary>
        public void Restore(IReadOnlyList<int> monthKeys, IReadOnlyList<long> amounts,
            long carriedForwardUsd = 0L)
        {
            months.Clear();
            currentMonthDays.Clear();
            currentMonthKey = -1;
            CarriedForwardUsd = carriedForwardUsd;

            if (monthKeys == null || amounts == null)
            {
                return;
            }

            var width = Catalog.Length;
            if (amounts.Count != monthKeys.Count * width)
            {
                return;
            }

            for (var index = 0; index < monthKeys.Count; index++)
            {
                var row = new long[width];
                for (var column = 0; column < width; column++)
                {
                    row[column] = Math.Max(0L, amounts[index * width + column]);
                }

                months[monthKeys[index]] = row;
            }

            Trim();
        }

        private long Side(int monthKey, bool income)
        {
            var total = 0L;
            foreach (var entry in Catalog)
            {
                if (entry.IsIncome == income && entry.IsCash)
                {
                    total += MonthTotal(monthKey, entry.Line);
                }
            }

            return total;
        }

        private static long[] Bucket(Dictionary<int, long[]> into, int key)
        {
            if (!into.TryGetValue(key, out var row))
            {
                row = new long[Catalog.Length];
                into[key] = row;
            }

            return row;
        }

        private void Trim()
        {
            while (months.Count > MonthsKept)
            {
                var oldest = int.MaxValue;
                foreach (var key in months.Keys)
                {
                    if (key < oldest)
                    {
                        oldest = key;
                    }
                }

                // Carried forward before it is forgotten, so the total is still explainable even when
                // the detail is not.
                CarriedForwardUsd += MonthCashFlow(oldest);
                months.Remove(oldest);
            }
        }

        private static int IndexOf(LedgerLine line)
        {
            for (var index = 0; index < Catalog.Length; index++)
            {
                if (Catalog[index].Line == line)
                {
                    return index;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(line), line, "Unknown ledger line.");
        }
    }
}
