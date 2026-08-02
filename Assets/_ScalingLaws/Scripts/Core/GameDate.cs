using System;

namespace ScalingLaws.Core
{
    /// <summary>
    /// A calendar day in the campaign, stored as a whole-day offset from 1 January 2022.
    /// Integers only: every save, replay and test stays bit-identical, with no floating point
    /// drift and no time zones. <see cref="DateTime"/> is used for calendar arithmetic only.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        /// <summary>Day zero. The month before anybody outside a lab had used a chat model.</summary>
        public static readonly DateTime Epoch = new(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>A campaign runs thirty years. Anything past that is a corrupt save, not a long game.</summary>
        public const int MaximumDayIndex = 30 * 366;

        /// <summary>
        /// Ten years of negative room. The catalog needs it: a V100 shipped in 2017 and an A100 in
        /// 2020, and if both clamped to day zero the game could not tell which one is the successor.
        /// </summary>
        public const int MinimumDayIndex = -10 * 366;

        public GameDate(int dayIndex)
        {
            DayIndex = Math.Clamp(dayIndex, MinimumDayIndex, MaximumDayIndex);
        }

        /// <summary>Whole days since <see cref="Epoch"/>. This is the only serialized field.</summary>
        public int DayIndex { get; }

        public static GameDate Start => new(0);

        public static GameDate FromCalendar(int year, int month, int day)
        {
            var safeYear = Math.Clamp(year, Epoch.Year - 10, Epoch.Year + 30);
            var safeMonth = Math.Clamp(month, 1, 12);
            var safeDay = Math.Clamp(day, 1, DateTime.DaysInMonth(safeYear, safeMonth));
            var moment = new DateTime(safeYear, safeMonth, safeDay, 0, 0, 0, DateTimeKind.Utc);
            return new GameDate((int)(moment - Epoch).TotalDays);
        }

        public DateTime ToDateTime() => Epoch.AddDays(DayIndex);

        public int Year => ToDateTime().Year;
        public int Month => ToDateTime().Month;
        public int Day => ToDateTime().Day;

        /// <summary>Quarter of the calendar year, 1 to 4. Investors think in these.</summary>
        public int Quarter => (Month - 1) / 3 + 1;

        public GameDate AddDays(int days) => new(DayIndex + days);

        public GameDate AddMonths(int months) => FromCalendarClamped(ToDateTime().AddMonths(months));

        public int DaysUntil(GameDate other) => other.DayIndex - DayIndex;

        /// <summary>Fractional years between two dates, using the mean Gregorian year.</summary>
        public double YearsUntil(GameDate other) => (other.DayIndex - DayIndex) / 365.2425;

        public bool IsOnOrAfter(GameDate other) => DayIndex >= other.DayIndex;

        public bool IsBefore(GameDate other) => DayIndex < other.DayIndex;

        private static GameDate FromCalendarClamped(DateTime moment)
        {
            var days = (int)(moment.Date - Epoch).TotalDays;
            return new GameDate(days);
        }

        public int CompareTo(GameDate other) => DayIndex.CompareTo(other.DayIndex);

        public bool Equals(GameDate other) => DayIndex == other.DayIndex;

        public override bool Equals(object obj) => obj is GameDate other && Equals(other);

        public override int GetHashCode() => DayIndex;

        public override string ToString() => ToDateTime().ToString("yyyy-MM-dd");

        public static bool operator ==(GameDate left, GameDate right) => left.Equals(right);
        public static bool operator !=(GameDate left, GameDate right) => !left.Equals(right);
        public static bool operator <(GameDate left, GameDate right) => left.DayIndex < right.DayIndex;
        public static bool operator >(GameDate left, GameDate right) => left.DayIndex > right.DayIndex;
        public static bool operator <=(GameDate left, GameDate right) => left.DayIndex <= right.DayIndex;
        public static bool operator >=(GameDate left, GameDate right) => left.DayIndex >= right.DayIndex;
        public static GameDate operator +(GameDate date, int days) => date.AddDays(days);
        public static int operator -(GameDate left, GameDate right) => left.DayIndex - right.DayIndex;
    }
}
