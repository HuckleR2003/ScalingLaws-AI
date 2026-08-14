using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>How loudly a story lands. The banner only interrupts for the loud ones.</summary>
    public enum NewsWeight
    {
        Routine = 0,
        Notable = 1,

        /// <summary>Something the player would want to stop and read. Used sparingly.</summary>
        Loud = 2
    }

    /// <summary>
    /// One story.
    ///
    /// A story is always **about something that happened**, never a mood. Every item in the feed is
    /// built from an event the simulation raised, a rival's actual state, or a bought signal. Nothing
    /// here invents a fact, which is the same rule the management page runs on and for the same
    /// reason: two places that both compute the truth eventually disagree about it.
    /// </summary>
    public readonly struct NewsItem
    {
        public NewsItem(GameDate date, NewsSection section, string headline, string body,
            string outlet, bool isAboutPlayer, NewsWeight weight)
        {
            Date = date;
            Section = section;
            Headline = headline ?? string.Empty;
            Body = body ?? string.Empty;
            Outlet = outlet ?? string.Empty;
            IsAboutPlayer = isAboutPlayer;
            Weight = weight;
        }

        public GameDate Date { get; }
        public NewsSection Section { get; }
        public string Headline { get; }
        public string Body { get; }

        /// <summary>Who printed it. The paid desks put their own name on their work.</summary>
        public string Outlet { get; }

        /// <summary>
        /// True when the company is the subject rather than the reader.
        ///
        /// Worth its own field because the player's own scandals are the ones that matter most and
        /// the ones a feed of rival news would bury.
        /// </summary>
        public bool IsAboutPlayer { get; }

        public NewsWeight Weight { get; }

        public override string ToString() => $"{Date} [{Section}] {Headline}";
    }

    /// <summary>
    /// Everything the company has read, newest last.
    ///
    /// Capped, because a fifteen year campaign would otherwise carry several thousand strings through
    /// every save. The cap is on the whole feed rather than per section: a quiet decade of premieres
    /// should not push out the one scandal that explains why the numbers went wrong.
    /// </summary>
    public sealed class NewsFeed
    {
        /// <summary>Stories kept. Roughly two years of an active company.</summary>
        public const int Capacity = 90;

        private readonly List<NewsItem> items = new();

        /// <summary>Stories filed since the player last opened the news screen.</summary>
        public int Unread { get; private set; }

        public IReadOnlyList<NewsItem> All => items;

        public int Count => items.Count;

        public void Add(in NewsItem item)
        {
            items.Add(item);
            Unread++;

            while (items.Count > Capacity)
            {
                items.RemoveAt(0);
            }
        }

        public void MarkRead() => Unread = 0;

        /// <summary>Restores one unread mark. Only the save uses this.</summary>
        public void NoteUnread() => Unread = Math.Min(Unread + 1, items.Count);

        public void Clear()
        {
            items.Clear();
            Unread = 0;
        }

        /// <summary>The newest stories in one section, or in all of them for the wire.</summary>
        public List<NewsItem> In(NewsSection section, int most)
        {
            var found = new List<NewsItem>(Math.Max(0, most));

            for (var index = items.Count - 1; index >= 0 && found.Count < most; index--)
            {
                if (section == NewsSection.Wire || items[index].Section == section)
                {
                    found.Add(items[index]);
                }
            }

            return found;
        }

        /// <summary>
        /// The story the corner banner shows.
        ///
        /// Loudest first and newest among equals, so a scandal filed this morning outranks a routine
        /// premiere filed this afternoon. Without the weight the banner would show whatever happened
        /// last, which on a busy day is a staff hire sitting on top of a regulatory fine.
        /// </summary>
        public bool TryGetHeadline(out NewsItem headline)
        {
            headline = default;
            var found = false;

            for (var index = items.Count - 1; index >= 0 && index > items.Count - 12; index--)
            {
                if (!found || items[index].Weight > headline.Weight)
                {
                    headline = items[index];
                    found = true;
                }
            }

            return found;
        }
    }
}
