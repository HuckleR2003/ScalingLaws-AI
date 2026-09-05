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

        private static readonly Walkthrough[] Entries = { ServerRoom };

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
