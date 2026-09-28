using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One short guided tour of a screen, offered after the opening tutorial rather than during it.
    ///
    /// **The same `GuideStep` the opening tour is made of.** A walkthrough is not a second kind of
    /// tutorial with its own strip, its own highlight and its own lock; it is a different list of
    /// steps fed to the machinery that already exists. Two tutorial systems would be two places to
    /// fix the click-eating bug that took four reports to find the first time.
    ///
    /// What is different is the shape of the thing rather than the parts. The opening tour is one
    /// long linear conversation the player takes once. These are named, short, chosen from a list,
    /// and finished rather than skipped: once started, the interface is locked to what the step is
    /// asking for, because a three minute walkthrough somebody wanders out of halfway is worse than
    /// no walkthrough at all.
    /// </summary>
    public sealed class Walkthrough
    {
        public Walkthrough(string id, string titleKey, string blurbKey, GuideTarget opensOn,
            IReadOnlyList<GuideStep> steps)
        {
            Id = id;
            this.titleKey = titleKey;
            this.blurbKey = blurbKey;
            OpensOn = opensOn;
            Steps = steps;
        }

        private readonly string titleKey;
        private readonly string blurbKey;

        /// <summary>Written into saves once it is finished, so it must never be renamed.</summary>
        public string Id { get; }

        /// <summary>Resolved per read, so a language change mid-campaign reaches it.</summary>
        public string Title => Loc.T(titleKey);

        public string Blurb => Loc.T(blurbKey);

        /// <summary>Which screen it happens on. The runner opens it before the first step.</summary>
        public GuideTarget OpensOn { get; }

        public IReadOnlyList<GuideStep> Steps { get; }
    }

    /// <summary>
    /// The walkthroughs the player can ask for.
    ///
    /// Deliberately a catalog rather than a growing method: adding one is a row here, a handful of
    /// phrases, and nothing else. The runner, the chip that offers it, the lock and the highlight
    /// are all already written and none of them knows how many there are.
    /// </summary>
    public static class WalkthroughCatalog
    {
        /// <summary>The one Emil offers as soon as the tour is over.</summary>
        public const string ServerRoomId = "walk_serverroom";

        /// <summary>
        /// The step that is about the cabinets already on the floor.
        ///
        /// Named here because the room reads it: the floor is one render texture, so that step's
        /// highlight is drawn in the scene rather than put on an element, and the room has to know
        /// which step asked for it. An id rather than an index, or a step inserted above would move
        /// the ring onto a sentence about the shop.
        /// </summary>
        public const string RoomCabinetsStepId = "walk_room_shop";

        private static readonly Walkthrough ServerRoom = new(
            ServerRoomId,
            "walk.room.title",
            "walk.room.blurb",
            GuideTarget.Room,
            new List<GuideStep>
            {
                // Each step names what the player has to do and rings the thing they do it with.
                // The last one waits for a click rather than a NEXT, because a walkthrough that
                // can be finished without touching anything has taught nobody anything.
                // The highlight classes are the ones the room actually adds, checked by
                // `WalkthroughTests.EveryStepRingsSomethingTheRoomDraws` rather than by reading:
                // a class that does not exist rings nothing and reports nothing, which is the
                // failure that shipped a whole screen of unstyled bars once already.
                new("walk_room_open", "walk.room.open", GuideTarget.Room,
                    highlight: "roombuild"),

                // **The cabinets, not the shop.** This step is the sentence "these are the
                // cabinets, the cheap one holds four", and it rang the price list on the right.
                // The floor cannot carry a class - it is one render texture - so the room raises an
                // outline round the occupied squares while this step is showing.
                new("walk_room_shop", "walk.room.shop", GuideTarget.Room,
                    highlight: "roomstage"),

                new("walk_room_buy", "walk.room.buy", GuideTarget.Room,
                    highlight: "roombuild__card", waitForClick: true),

                // Satisfied by the piece reaching the cursor, which is what the sentence is
                // about. It used to wait for a NEXT, so the player put the cabinet down and the
                // tour was still telling them they were carrying it.
                new("walk_room_carry", "walk.room.carry", GuideTarget.Room,
                    highlight: "roomfloor", waitForClick: true),

                new("walk_room_stand", "walk.room.stand", GuideTarget.Room,
                    highlight: "roomfloor", waitForClick: true),

                new("walk_room_open_rack", "walk.room.open_rack", GuideTarget.Room,
                    highlight: "roomfloor", waitForClick: true),

                new("walk_room_fit", "walk.room.fit", GuideTarget.Room,
                    highlight: "rackmodal__actions", waitForClick: true),

                new("walk_room_done", "walk.room.done", GuideTarget.Room)
            });

        /// <summary>The one Emil rings about the first time the desk takes a ticket.</summary>
        public const string SupportId = "walk_support";

        /// <summary>
        /// The desk, walked through on the day it starts mattering.
        ///
        /// **It opens on SITE rather than on the desk**, which is the opposite of the room's and is
        /// deliberate. The room has an icon of its own and the player has already found it; the
        /// management page is reached from a button on the product banner and nothing else in the
        /// game points at it, so the first thing worth teaching is where the door is. Walking
        /// somebody straight through a door they cannot find again has taught them nothing.
        ///
        /// Everything after that rings a part of the panel in the order a person reads it: what the
        /// number means, what is queueing, who is on it, and only then the three things that can be
        /// bought. The ladders are last because they are the expensive answer and hiring is the
        /// cheap one, and a tour that opens on the shop teaches the wrong lesson about a queue.
        /// </summary>
        private static readonly Walkthrough Support = new(
            SupportId,
            "walk.support.title",
            "walk.support.blurb",
            GuideTarget.Site,
            new List<GuideStep>
            {
                // The way in. Satisfied by the page opening, never by a button on the strip: the
                // whole point of this step is that the player presses the thing they will have to
                // press again tomorrow.
                // The id is written out here and again where the shell reports it, the same as every
                // other waiting step: `WalkthroughTests` can only read literals, and a step whose id
                // it cannot find is a player locked inside a screen with the bottom bar shut.
                new("walk_support_open", "walk.support.open", GuideTarget.Support,
                    highlight: "mb__manage", waitForClick: true),

                // **The tab, pressed by the player rather than opened for them.**
                //
                // A tester walked this and reported that after the management page opened there
                // was nothing on it about support at all. That was true: the desk was a strip
                // three panels down a tab they were not on, and the tour went straight to ringing
                // a class that was not in the tree. The desk has a tab of its own now, so the step
                // that was missing is the one where they find it, and the same rule applies as to
                // the button above: a player who is shown a door without pressing it has not
                // learned where it is.
                new("walk_support_tab", "walk.support.tab", GuideTarget.Support,
                    highlight: "mg-tab--support", waitForClick: true),

                new("walk_support_verdict", "walk.support.verdict", GuideTarget.Support,
                    highlight: "sup__satis"),

                new("walk_support_queue", "walk.support.queue", GuideTarget.Support,
                    highlight: "sup__queue"),

                new("walk_support_staff", "walk.support.staff", GuideTarget.Support,
                    highlight: "sup__acts"),

                new("walk_support_ladders", "walk.support.ladders", GuideTarget.Support,
                    highlight: "support__ladders"),

                new("walk_support_done", "walk.support.done", GuideTarget.Support)
            });

        private static readonly Walkthrough[] Entries = { ServerRoom, Support };

        public static IReadOnlyList<Walkthrough> All => Entries;

        public static bool TryGet(string id, out Walkthrough walkthrough)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    walkthrough = entry;
                    return true;
                }
            }

            walkthrough = null;
            return false;
        }
    }
}
