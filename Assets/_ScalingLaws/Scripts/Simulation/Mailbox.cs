using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>What arrived, and therefore what the letter is allowed to ask for.</summary>
    public enum MailKind
    {
        /// <summary>Read it and get on with your day.</summary>
        Notice = 0,

        /// <summary>The year's corporation tax, accrued daily and billed once. Has to be paid.</summary>
        TaxDemand = 1,

        /// <summary>A regulator wants money after an incident. Has to be paid.</summary>
        Fine = 2,

        /// <summary>Somebody wants a job at a price. Can be accepted, haggled, or ignored.</summary>
        JobOffer = 3,

        /// <summary>A lender saying something about a facility already drawn.</summary>
        LoanNotice = 4
    }

    /// <summary>What the reader can do about it.</summary>
    public enum MailAction
    {
        None = 0,
        Pay = 1,
        Accept = 2,
        Decline = 3,

        /// <summary>Offer less than they asked. They may take it, or they may walk.</summary>
        Haggle = 4,

        /// <summary>Ask the revenue to wait. Costs interest, costs no standing, has a ceiling.</summary>
        Defer = 5
    }

    /// <summary>
    /// One letter.
    ///
    /// **A letter is a claim on the player's attention, so most things must not become one.** The
    /// wire already carries everything that merely happened; mail is for the small set of events
    /// that are waiting on an answer. If a letter has no action and no deadline it should almost
    /// certainly have been a news story instead.
    ///
    /// The payload is deliberately dumb: an amount, a role, a level. The rules for what those mean
    /// live in <see cref="CompanySimulation"/>, so the interface can render a letter it has never
    /// heard of and the simulation stays the only thing that can spend money.
    /// </summary>
    public sealed class MailItem
    {
        public MailItem(int id, MailKind kind, GameDate arrived, string sender, string subject,
            string body)
        {
            Id = id;
            Kind = kind;
            Arrived = arrived;
            Sender = sender ?? string.Empty;
            Subject = subject ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public int Id { get; }
        public MailKind Kind { get; }
        public GameDate Arrived { get; }
        public string Sender { get; }
        public string Subject { get; }
        public string Body { get; }

        public bool IsRead { get; set; }

        /// <summary>Settled, accepted, declined or expired. A closed letter asks for nothing.</summary>
        public bool IsClosed { get; set; }

        /// <summary>How it closed, for the archive. Empty while it is still open.</summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Money owed, for a demand. Zero on everything else.</summary>
        public long AmountUsd { get; set; }

        /// <summary>The day after which ignoring it starts costing. Zero for no deadline.</summary>
        public int DueDayIndex { get; set; }

        // ---- job offers ------------------------------------------------------------------------

        public StaffRole Role { get; set; }
        public int Skill { get; set; }

        /// <summary>What they are asking a year. The company can offer less, once.</summary>
        public long AskingSalaryUsd { get; set; }

        /// <summary>True once the company has countered. Nobody haggles twice.</summary>
        public bool HasBeenHaggled { get; set; }

        public LoanProduct Loan { get; set; }

        /// <summary>
        /// Days this demand has already been pushed back.
        ///
        /// Kept on the letter rather than as a company-wide counter, because two demands can be
        /// outstanding at once when a year's bill is deferred into the next year's, and each has its
        /// own ceiling to run into.
        /// </summary>
        public int DeferredDays { get; set; }

        public bool IsOverdue(GameDate today) =>
            !IsClosed && DueDayIndex > 0 && today.DayIndex > DueDayIndex;

        public int DaysLeft(GameDate today) =>
            DueDayIndex <= 0 ? int.MaxValue : Math.Max(0, DueDayIndex - today.DayIndex);

        /// <summary>Everything this letter offers, in the order a reader would try them.</summary>
        public IReadOnlyList<MailAction> Actions
        {
            get
            {
                if (IsClosed)
                {
                    return Array.Empty<MailAction>();
                }

                return Kind switch
                {
                    // Only the revenue waits. A regulator's penalty offers paying and nothing
                    // else, which is what keeps deferral from being a general answer to owing money.
                    // At the ceiling the option is gone rather than present and refusing: a button
                    // that exists to say no teaches the player to stop reading buttons.
                    MailKind.TaxDemand => DeferredDays >= CompanySimulation.LongestDeferralDays
                        ? new[] { MailAction.Pay }
                        : new[] { MailAction.Pay, MailAction.Defer },
                    MailKind.Fine => new[] { MailAction.Pay },
                    MailKind.JobOffer => HasBeenHaggled
                        ? new[] { MailAction.Accept, MailAction.Decline }
                        : new[] { MailAction.Accept, MailAction.Haggle, MailAction.Decline },
                    _ => Array.Empty<MailAction>()
                };
            }
        }
    }

    /// <summary>
    /// The company's inbox.
    ///
    /// Capped like the news feed, but with a rule the news feed does not need: **an open letter is
    /// never dropped to make room.** A tax demand aging out of the bottom of the list because a
    /// quiet decade filled it with notices would be the game forgiving a debt by accident.
    /// </summary>
    public sealed class Mailbox
    {
        public const int Capacity = 60;

        private readonly List<MailItem> items = new();
        private int nextId = 1;

        public IReadOnlyList<MailItem> All => items;

        public int Unread
        {
            get
            {
                var count = 0;
                foreach (var item in items)
                {
                    if (!item.IsRead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Letters still waiting on an answer. The number worth putting on a badge.</summary>
        public int Open
        {
            get
            {
                var count = 0;
                foreach (var item in items)
                {
                    if (!item.IsClosed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public long OwedUsd
        {
            get
            {
                var total = 0L;
                foreach (var item in items)
                {
                    if (!item.IsClosed)
                    {
                        total += item.AmountUsd;
                    }
                }

                return total;
            }
        }

        public MailItem Add(MailKind kind, GameDate arrived, string sender, string subject, string body)
        {
            var item = new MailItem(nextId++, kind, arrived, sender, subject, body);
            items.Add(item);
            Trim();
            return item;
        }

        /// <summary>Puts a restored letter back, keeping the id it already had.</summary>
        public void Restore(MailItem item)
        {
            if (item == null)
            {
                return;
            }

            items.Add(item);
            nextId = Math.Max(nextId, item.Id + 1);
        }

        public bool TryGet(int id, out MailItem item)
        {
            foreach (var candidate in items)
            {
                if (candidate.Id == id)
                {
                    item = candidate;
                    return true;
                }
            }

            item = null;
            return false;
        }

        public void Clear()
        {
            items.Clear();
            nextId = 1;
        }

        /// <summary>Oldest closed letters go first. An open one is never dropped.</summary>
        private void Trim()
        {
            while (items.Count > Capacity)
            {
                var removed = false;
                for (var index = 0; index < items.Count; index++)
                {
                    if (items[index].IsClosed)
                    {
                        items.RemoveAt(index);
                        removed = true;
                        break;
                    }
                }

                if (!removed)
                {
                    // Everything in the box is still open, which means the player is ignoring a great
                    // deal. Growing past the cap is the lesser evil: silently deleting a demand would
                    // be the game forgiving a debt by accident.
                    return;
                }
            }
        }
    }
}
