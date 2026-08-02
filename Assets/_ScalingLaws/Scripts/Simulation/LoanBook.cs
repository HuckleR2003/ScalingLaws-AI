using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>One loan being serviced.</summary>
    public sealed class Loan
    {
        public Loan(LoanProduct product, GameDate takenOn, long principalUsd, long totalRepaymentUsd, int termDays, int graceDays)
        {
            Product = product;
            TakenOn = takenOn;
            PrincipalUsd = Math.Max(0L, principalUsd);
            TotalRepaymentUsd = Math.Max(PrincipalUsd, totalRepaymentUsd);
            TermDays = Math.Clamp(termDays, 1, 8000);
            GraceDays = Math.Clamp(graceDays, 0, TermDays - 1);
        }

        public LoanProduct Product { get; }
        public GameDate TakenOn { get; }
        public long PrincipalUsd { get; }
        public long TotalRepaymentUsd { get; }
        public int TermDays { get; }
        public int GraceDays { get; }

        public long RepaidUsd { get; private set; }

        /// <summary>Days the schedule has been missed for. Lenders tolerate a little, not a lot.</summary>
        public int DaysInArrears { get; private set; }

        public long OutstandingUsd => Math.Max(0L, TotalRepaymentUsd - RepaidUsd);

        public bool IsSettled => RepaidUsd >= TotalRepaymentUsd;

        public long DailyInstalmentUsd =>
            SimUnits.ToDollars(TotalRepaymentUsd / (double)Math.Max(1, TermDays - GraceDays));

        public bool IsInGracePeriod(GameDate date) => date.DayIndex - TakenOn.DayIndex < GraceDays;

        public GameDate MaturesOn => TakenOn.AddDays(TermDays);

        /// <summary>What is due today. Zero during grace and zero once settled.</summary>
        public long DueToday(GameDate date)
        {
            if (IsSettled || IsInGracePeriod(date) || date > MaturesOn)
            {
                return IsSettled ? 0L : Math.Min(OutstandingUsd, DailyInstalmentUsd);
            }

            return Math.Min(OutstandingUsd, DailyInstalmentUsd);
        }

        public void Pay(long amountUsd)
        {
            RepaidUsd = Math.Min(TotalRepaymentUsd, RepaidUsd + Math.Max(0L, amountUsd));
            DaysInArrears = 0;
        }

        public void Miss() => DaysInArrears++;

        public void Restore(long repaidUsd, int daysInArrears)
        {
            RepaidUsd = Math.Clamp(repaidUsd, 0L, TotalRepaymentUsd);
            DaysInArrears = Math.Clamp(daysInArrears, 0, 100_000);
        }

        public override string ToString() =>
            $"{Product}: ${OutstandingUsd:N0} outstanding, {DaysInArrears} days in arrears";
    }

    /// <summary>Whether a product is on offer, and if not, why not.</summary>
    public readonly struct LoanAvailability
    {
        public LoanAvailability(LoanProduct product, bool isAvailable, string reason)
        {
            Product = product;
            IsAvailable = isAvailable;
            Reason = isAvailable ? string.Empty : reason ?? string.Empty;
        }

        public LoanProduct Product { get; }
        public bool IsAvailable { get; }
        public string Reason { get; }

        public override string ToString() => IsAvailable ? $"{Product}: open" : $"{Product}: {Reason}";
    }

    /// <summary>
    /// Every loan the company is servicing.
    ///
    /// Debt is the counterweight to the funding rounds. A round is expensive once and permanent; a
    /// loan is cheap once and relentless. The instalment leaves the account every day whether the
    /// model shipped, whether the market moved, and whether anyone bought anything.
    ///
    /// Arrears are tracked rather than instantly fatal, because a lender will carry a good company
    /// through a bad quarter. Past <see cref="ArrearsBeforeDefault"/> they stop.
    /// </summary>
    public sealed class LoanBook
    {
        /// <summary>Consecutive missed days a lender tolerates before calling a default.</summary>
        public const int ArrearsBeforeDefault = 60;

        private readonly List<Loan> loans = new();

        public IReadOnlyList<Loan> Loans => loans;

        public int OpenCount
        {
            get
            {
                var open = 0;
                foreach (var loan in loans)
                {
                    if (!loan.IsSettled)
                    {
                        open++;
                    }
                }

                return open;
            }
        }

        public long TotalOutstandingUsd
        {
            get
            {
                var total = 0L;
                foreach (var loan in loans)
                {
                    total += loan.OutstandingUsd;
                }

                return total;
            }
        }

        /// <summary>What every open loan takes today, before checking whether it can be paid.</summary>
        public long DailyServiceUsd(GameDate date)
        {
            var total = 0L;
            foreach (var loan in loans)
            {
                if (!loan.IsSettled && !loan.IsInGracePeriod(date))
                {
                    total += loan.DueToday(date);
                }
            }

            return total;
        }

        public bool Has(LoanProduct product)
        {
            foreach (var loan in loans)
            {
                if (loan.Product == product && !loan.IsSettled)
                {
                    return true;
                }
            }

            return false;
        }

        public void Add(Loan loan)
        {
            if (loan != null)
            {
                loans.Add(loan);
            }
        }

        public void Clear() => loans.Clear();

        /// <summary>Any loan whose arrears have run past what a lender will carry.</summary>
        public Loan FirstDefaulted()
        {
            foreach (var loan in loans)
            {
                if (!loan.IsSettled && loan.DaysInArrears >= ArrearsBeforeDefault)
                {
                    return loan;
                }
            }

            return null;
        }

        /// <summary>
        /// Services every open loan out of the cash available, oldest first. Returns what was paid
        /// and marks anything that could not be met.
        /// </summary>
        public long Service(GameDate date, long availableCashUsd)
        {
            var paid = 0L;
            var remaining = availableCashUsd;

            foreach (var loan in loans)
            {
                if (loan.IsSettled || loan.IsInGracePeriod(date))
                {
                    continue;
                }

                var due = loan.DueToday(date);
                if (due <= 0L)
                {
                    continue;
                }

                if (remaining >= due)
                {
                    loan.Pay(due);
                    remaining -= due;
                    paid += due;
                }
                else
                {
                    loan.Miss();
                }
            }

            return paid;
        }
    }
}
