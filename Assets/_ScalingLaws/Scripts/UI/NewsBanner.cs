using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The corner ticker: the loudest thing filed recently, and a way in.
    ///
    /// It exists because thirty four kinds of event were being raised and thrown away, so a rival
    /// could ship and a regulator could pull the company's flagship off the market in the same week
    /// without a word appearing anywhere. **A banner that shows one headline is worth more than a
    /// screen nobody opens**, which is why the entry point is here rather than only in the bar.
    ///
    /// It shows one story at a time on purpose. A corner that scrolls three headlines is a second
    /// thing to read while the office is on screen; one line with a count beside it is a glance.
    /// </summary>
    public sealed class NewsBanner
    {
        private readonly Func<NewsFeed> feed;

        /// <summary>
        /// What the unread count was last time, so a rise can be noticed.
        ///
        /// The banner sits at seventy percent transparent and is meant to be ignorable. That only
        /// works if it has a way to ask for attention when something actually arrives, otherwise
        /// quiet and important look identical.
        /// </summary>
        private int lastUnread = -1;
        private readonly Label kicker;
        private readonly Label headline;
        private readonly Label dateline;
        private readonly VisualElement rule;
        private readonly Label badge;

        public NewsBanner(Func<NewsFeed> feed, Action openNews)
        {
            this.feed = feed;

            Root = new VisualElement();
            Root.AddToClassList("nb");

            var head = new VisualElement();
            head.AddToClassList("nb__head");

            var masthead = new Label("THE WIRE");
            masthead.AddToClassList("nb__masthead");
            head.Add(masthead);

            badge = new Label();
            badge.AddToClassList("nb__badge");
            head.Add(badge);

            Root.Add(head);

            // The section's colour, carried as a bar down the left of the story. It is the only
            // thing on the banner that changes shape, so a scandal reads differently from a
            // premiere before a single word has been read.
            var body = new VisualElement();
            body.AddToClassList("nb__body");

            rule = new VisualElement();
            rule.AddToClassList("nb__rule");
            body.Add(rule);

            var text = new VisualElement();
            text.AddToClassList("nb__text");

            kicker = new Label();
            kicker.AddToClassList("nb__kicker");
            text.Add(kicker);

            headline = new Label();
            headline.AddToClassList("nb__headline");
            text.Add(headline);

            dateline = new Label();
            dateline.AddToClassList("nb__dateline");
            text.Add(dateline);

            body.Add(text);
            Root.Add(body);

            var open = new Button(openNews) { text = "SEE NEWS" };
            open.AddToClassList("nb__open");
            Root.Add(open);
        }

        public VisualElement Root { get; }

        public void SetHidden(bool hidden) =>
            Root.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;

        /// <summary>
        /// Comes up to full strength for a moment, then fades back.
        ///
        /// Two seconds rather than a permanent change, because a banner that stays bright until it
        /// is clicked is a banner that is always bright by the second year.
        /// </summary>
        private void Flash()
        {
            Root.AddToClassList("nb--new");
            Root.schedule.Execute(() => Root.RemoveFromClassList("nb--new")).ExecuteLater(2000);
        }

        public void Refresh()
        {
            var news = feed();
            if (news == null || !news.TryGetHeadline(out var story))
            {
                kicker.text = "QUIET";
                headline.text = "Nothing has happened yet.";
                dateline.text = string.Empty;
                badge.text = string.Empty;
                badge.style.display = DisplayStyle.None;
                rule.RemoveFromClassList("nb__rule--loud");
                return;
            }

            kicker.text = KickerFor(story);
            headline.text = story.Headline;
            dateline.text = story.Date.ToString()
                + (story.Outlet.Length > 0 && story.Outlet != "Wire" ? "  ·  " + story.Outlet : string.Empty);

            badge.text = news.Unread > 0 ? news.Unread.ToString() : string.Empty;
            badge.style.display = news.Unread > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (lastUnread >= 0 && news.Unread > lastUnread)
            {
                Flash();
            }

            lastUnread = news.Unread;

            rule.EnableInClassList("nb__rule--loud", story.Weight == NewsWeight.Loud);
            rule.EnableInClassList("nb__rule--scandal", story.Section == NewsSection.Scandals);
            rule.EnableInClassList("nb__rule--premiere", story.Section == NewsSection.Premieres);
        }

        /// <summary>
        /// The small line above the headline. It names the section, except for the company's own
        /// trouble, which gets called what it is.
        /// </summary>
        private static string KickerFor(in NewsItem story) => story.Section switch
        {
            NewsSection.Scandals => story.IsAboutPlayer ? "ABOUT YOU" : "SCANDAL",
            NewsSection.Premieres => story.IsAboutPlayer ? "YOU SHIPPED" : "PREMIERE",
            NewsSection.TotalTrueNews => "TOTAL TRUE NEWS",
            NewsSection.ItSpy => "IT SPY",
            NewsSection.EventHunter => "EVENT HUNTER",
            _ => "WIRE"
        };
    }
}
