using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What is coming, for the National Press desk.
    ///
    /// **This is what the cheapest membership actually buys**, and until now it bought nothing a
    /// player could point at: EVENT HUNTER is sold by National Press, gated behind a second
    /// membership as well, and every story that ever reached it was about accelerators. Events
    /// themselves stay free to read about. What twenty thousand a month buys is a month of head
    /// start and this list, which is the difference between a stand and an auction at three of the
    /// seven events in the year.
    /// </summary>
    public static class UpcomingEventsList
    {
        /// <summary>
        /// The list, or the sentence saying which membership would open it.
        ///
        /// <paramref name="outletName"/> is named rather than described, because "buy a
        /// subscription" over a locked panel is the shape that reads as a bug: the player is owed
        /// which one.
        /// </summary>
        /// <param name="open">
        /// What to do when a row is pressed. Null leaves the list a reading, which is what a
        /// render wants and what this list was for its whole life until 2026-10-25.
        ///
        /// **Reported as the rows not being clickable, and they were not.** The panel counted the
        /// days to registration and the stands still free, next to a price, and there was no way
        /// to act on any of it: `EventVenuePanel` is a finished screen that nothing in the game
        /// opened, and `TryRegisterForStand` had no caller in `UI/` at all. The mutator sweep this
        /// project documents finds exactly that and was not run before the build.
        /// </param>
        public static VisualElement Build(IReadOnlyList<UpcomingEventRow> rows, bool isMember,
            string outletName, Action<UpcomingEventRow> open = null)
        {
            var block = new VisualElement();
            block.AddToClassList("upnext");

            var head = new Label(Loc.T("upnext.heading"));
            head.AddToClassList("upnext__heading");
            block.Add(head);

            if (!isMember)
            {
                var locked = new Label(Loc.T("upnext.locked", outletName));
                locked.AddToClassList("upnext__locked");
                block.Add(locked);

                return block;
            }

            if (rows == null || rows.Count == 0)
            {
                var quiet = new Label(Loc.T("upnext.none"));
                quiet.AddToClassList("upnext__none");
                block.Add(quiet);

                return block;
            }

            foreach (var row in rows)
            {
                block.Add(Row(row, open));
            }

            return block;
        }

        /// <summary>
        /// One upcoming event.
        ///
        /// **A row that goes somewhere is a `Button`; one that does not is a `VisualElement`.**
        /// Same rule as the wire: a control that reacts to the cursor and then does nothing is
        /// worse than one that never looked clickable, and this project has shipped that twice.
        /// </summary>
        private static VisualElement Row(UpcomingEventRow row, Action<UpcomingEventRow> open)
        {
            var definition = CityEventCatalog.Get(row.Id);
            var venue = EventVenueCatalog.Get(definition.Venue);

            var captured = row;

            var line = open == null
                ? new VisualElement()
                : new Button(() => open(captured));

            line.AddToClassList("upnext__row");
            line.EnableInClassList("upnext__row--goes", open != null);

            var words = new VisualElement();
            words.AddToClassList("upnext__words");

            var name = new Label(definition.DisplayName);
            name.AddToClassList("upnext__name");
            words.Add(name);

            var where = new Label(venue.DisplayName);
            where.AddToClassList("upnext__where");
            words.Add(where);

            line.Add(words);

            // ---- how long is left -----------------------------------------------------------
            var clock = new VisualElement();
            clock.AddToClassList("upnext__cell");

            // **Always days, never months.** `UiFormat.Days` switches above sixty, which is
            // right almost everywhere and wrong on a list whose whole job is to be compared
            // down a column: three rows reading in days and a fourth reading 3.2 months is a
            // column the eye cannot rank.
            var days = new Label(Loc.Counted(row.DaysLeft, "noun.day"));
            days.AddToClassList("upnext__days");

            // Under a fortnight the window is the thing worth noticing on this row.
            days.EnableInClassList("upnext__days--soon", row.DaysLeft <= 14);
            clock.Add(days);

            var closes = new Label(Loc.T("upnext.closes"));
            closes.AddToClassList("upnext__cell-caption");
            clock.Add(closes);

            line.Add(clock);

            // ---- what is free ---------------------------------------------------------------
            var room = new VisualElement();
            room.AddToClassList("upnext__cell");

            var free = new Label(row.GoesToAuction
                ? Loc.T("upnext.auction")
                : row.FreeStands + " / " + row.SellableStands);

            free.AddToClassList("upnext__free");
            free.EnableInClassList("upnext__free--auction", row.GoesToAuction);
            room.Add(free);

            var stands = new Label(Loc.T("upnext.stands"));
            stands.AddToClassList("upnext__cell-caption");
            room.Add(stands);

            line.Add(room);

            // ---- prestige, as a bar ------------------------------------------------------------
            var regard = new VisualElement();
            regard.AddToClassList("upnext__cell");
            regard.AddToClassList("upnext__cell--bar");

            // The number as well as the bar. Four bars side by side can be ranked and cannot be
            // read: nothing on a length alone says whether this is 0.80 or 0.85.
            var reading = new Label(UiFormat.Number(venue.Prestige, 2));
            reading.AddToClassList("upnext__bar-value");
            regard.Add(reading);

            var track = new VisualElement();
            track.AddToClassList("upnext__bar");

            var fill = new VisualElement();
            fill.AddToClassList("upnext__bar-fill");
            fill.style.width = Length.Percent((float)(Math.Clamp(venue.Prestige, 0.0, 1.0) * 100.0));
            track.Add(fill);

            regard.Add(track);

            var prestige = new Label(Loc.T("upnext.prestige"));
            prestige.AddToClassList("upnext__cell-caption");
            regard.Add(prestige);

            line.Add(regard);

            return line;
        }
    }
}
