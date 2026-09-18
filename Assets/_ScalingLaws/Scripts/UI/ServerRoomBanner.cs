using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the room is doing, in the corner, wherever you are standing in it.
    ///
    /// **Four figures and a bar, and every one of them answers a question the floor cannot.** A grid
    /// of cabinets shows what you own; it does not show that two of them are hot, that the bill went
    /// up when you fitted the fans, or that the queue is forming. Those are the numbers a player acts
    /// on and none of them are visible from the room itself.
    ///
    /// The temperature reads with its ceiling beside it, because a number with no scale is not a
    /// warning — the same reason every band on the architecture screen carries a baseline.
    /// </summary>
    public sealed class ServerRoomBanner
    {
        private readonly Label capacity = new();
        private readonly Label capacityNote = new();
        private readonly Label temperature = new();
        private readonly Label temperatureNote = new();
        private readonly Label power = new();
        private readonly Label powerNote = new();
        private readonly Label latency = new();
        private readonly VisualElement loadFill = new();
        private readonly Label health = new();
        private readonly Label climate = new();
        private readonly Label climateNote = new();
        private readonly Label users = new();
        private readonly Label usersNote = new();

        public ServerRoomBanner()
        {
            Root = new VisualElement();
            Root.AddToClassList("rbanner");

            Root.Add(Figure(Loc.T("room.banner.capacity"), capacity, capacityNote));
            Root.Add(Figure(Loc.T("room.banner.temperature"), temperature, temperatureNote));
            Root.Add(Figure(Loc.T("room.banner.power"), power, powerNote));

            // The reading is a word, and OVERHEATING at the figure size broke mid-word on the
            // first render. Smaller type, same colour.
            climate.AddToClassList("rbanner__value--word");
            var room = Figure(Loc.T("room.climate.title"), climate, climateNote);
            room.tooltip = Loc.T("room.climate.note");
            Root.Add(room);

            var served = Figure(Loc.T("room.users"), users, usersNote);
            served.tooltip = Loc.T("room.users.note");
            Root.Add(served);

            Root.Add(BuildLoad());

            health.AddToClassList("rbanner__health");
            Root.Add(health);
        }

        public VisualElement Root { get; }

        private static VisualElement Figure(string caption, Label value, Label note)
        {
            var block = new VisualElement();
            block.AddToClassList("rbanner__block");

            var label = new Label(caption);
            label.AddToClassList("rbanner__caption");
            block.Add(label);

            value.AddToClassList("rbanner__value");
            block.Add(value);

            note.AddToClassList("rbanner__note");
            block.Add(note);

            return block;
        }

        private VisualElement BuildLoad()
        {
            var block = new VisualElement();
            block.AddToClassList("rbanner__block");
            block.AddToClassList("rbanner__block--wide");

            var head = new VisualElement();
            head.AddToClassList("rbanner__loadhead");

            var label = new Label(Loc.T("room.banner.load"));
            label.AddToClassList("rbanner__caption");
            head.Add(label);

            latency.AddToClassList("rbanner__ms");
            head.Add(latency);

            block.Add(head);

            var track = new VisualElement();
            track.AddToClassList("rbanner__track");

            loadFill.AddToClassList("rbanner__fill");
            track.Add(loadFill);

            block.Add(track);
            return block;
        }

        /// <summary>
        /// Repoints every figure at the company as it stands today.
        ///
        /// Reads the same `ServiceQuality` the market reads, so the millisecond figure here and the
        /// one the customers are reacting to are the same number. Two sources for that would mean a
        /// bar that governs nothing.
        /// </summary>
        public void Refresh(CompanySimulation simulation)
        {
            if (simulation == null)
            {
                return;
            }

            var state = simulation.State;
            var hall = state.Hall;
            var quality = state.LastQuality;

            // ---- capacity -----------------------------------------------------------------------
            //
            // **From the cards the company owns, not from whatever the clouds are renting.** This
            // used to price the floor with `Market.RentableGeneration`, so a company running two
            // year old accelerators was shown this year's figures and the number on this bar was
            // not the number the market was served from. `BasementOutput` is the same arithmetic
            // the compute profile does.
            var housed = simulation.BasementOutput();

            // The heat figures read the owned cards' own draw, the same figure the floor, the
            // cabinet panel and the fleet use. It falls back to the rentable part with nothing owned.
            var perCardKw = simulation.HallPerAccelerator().Kilowatts;

            capacity.text = UiFormat.Petaflops(housed.Petaflops);

            // "32 / 32" under a heading reading CAPACITY invited exactly the wrong reading, which
            // is that the room supplies compute. It houses it. A company that owns no accelerators
            // gets nothing out of a full basement except the upkeep and the idle draw.
            capacityNote.text = Loc.T("room.banner.housed",
                hall.HousedAccelerators.ToString(), hall.TotalSlots.ToString());

            // ---- heat ----------------------------------------------------------------------------
            //
            // The hottest cabinet rather than an average. An average across a floor where one rack
            // is cooking and three are cold reads as comfortable, which is the one thing it is not.
            var hottest = HottestRatio(hall, perCardKw, simulation.Room);

            var worst = HottestState(hall, perCardKw, simulation.Room);

            temperature.text = UiFormat.Percent(hottest, 0);

            // The word rather than "of 100%", because a reading of 0% under a heading saying HEAT
            // is a room nobody can tell apart from a room that is merely cold. An empty floor says
            // so in the same five words the cabinets and the legend use.
            temperatureNote.text = Loc.T(ServerRackCatalog.KeyFor(worst));
            temperature.style.color = RackHeatPalette.Of(worst);

            // ---- power ---------------------------------------------------------------------------
            // The tariff comes from the pool, which is where the bill is actually raised. It was a
            // second copy of 0.19 sitting in a display string, which is how a rate ends up being
            // changed in one place and quoted from the other.
            power.text = UiFormat.Kilowatts(housed.DrawKilowatts);
            powerNote.text = UiFormat.Money(
                    (long)(housed.DrawKilowatts * 24.0 * simulation.Room.TariffUsd))
                + " " + Loc.T("common.a_day");

            // ---- load ----------------------------------------------------------------------------
            var load = Mathf.Clamp01((float)quality.Utilisation);

            loadFill.style.width = Length.Percent(load * 100f);
            loadFill.style.backgroundColor = LoadTone(load);
            latency.text = UiFormat.Milliseconds(quality.ResponseMilliseconds);

            // ---- the room itself ----------------------------------------------------------------
            //
            // **The room's reading, not the hottest cabinet's.** The two are different problems
            // with different fixes: a cabinet over its rating wants a fan, a room over its budget
            // wants a cooler, and a player shown only one of them buys the wrong thing.
            var room = simulation.RoomClimateToday();
            var roomTone = ToneOf(room.State);

            climate.text = Loc.T(ServerRackCatalog.KeyFor(room.State));
            climate.style.color = RackHeatPalette.Of(roomTone);
            climateNote.text = Loc.T("room.climate.reading",
                UiFormat.Number(room.HeatKilowatts, 0), UiFormat.Number(room.CoolingKilowatts, 0));

            // The figure large and the sentence small: "about 311.5k people" at display size ran to
            // two lines in a half-width block on the first render.
            var people = housed.Petaflops * simulation.UsersPerPetaflop();
            users.text = UiFormat.Count(people);
            usersNote.text = Loc.T("room.users.value", UiFormat.Count(people));

            // The advice is the room's when the room is the problem, because every cabinet in it
            // is losing work at once and no amount of fans fixes that.
            var advice = room.State switch
            {
                ServerRackCatalog.RoomClimateState.Overheating => Loc.T("room.climate.advice_hot"),
                ServerRackCatalog.RoomClimateState.Warm => Loc.T("room.climate.advice_warm"),
                _ => null
            };

            health.text = advice ?? (housed.ThrottledRacks > 0
                ? Loc.T("room.banner.throttling", housed.ThrottledRacks)
                : Loc.T("room.banner.all_clear"));

            var bad = housed.ThrottledRacks > 0
                      || room.State == ServerRackCatalog.RoomClimateState.Overheating;

            health.EnableInClassList("rbanner__health--bad", bad);
            health.EnableInClassList("rbanner__health--warn",
                !bad && room.State == ServerRackCatalog.RoomClimateState.Warm);
        }

        /// <summary>
        /// The room's four readings drawn in the cabinets' palette, so "near overheating" is the
        /// same yellow on the room as on a cabinet. One palette; the room borrows it.
        /// </summary>
        public static ServerRackCatalog.RackHeat ToneOf(ServerRackCatalog.RoomClimateState state) =>
            state switch
            {
                ServerRackCatalog.RoomClimateState.Cool => ServerRackCatalog.RackHeat.Cool,
                ServerRackCatalog.RoomClimateState.Comfortable => ServerRackCatalog.RackHeat.Comfortable,
                ServerRackCatalog.RoomClimateState.Warm => ServerRackCatalog.RackHeat.Warm,
                _ => ServerRackCatalog.RackHeat.Cooking
            };

        /// <summary>
        /// How close the worst cabinet is to the point where it stops delivering, 0 to 1.
        ///
        /// Expressed against its own cooling rather than in degrees, because the catalog rates racks
        /// in kilowatts of heat rather than in temperature, and inventing a degree figure to display
        /// would be a number the simulation does not use.
        /// </summary>
        private static double HottestRatio(ServerHall hall, double kilowattsPerAccelerator,
            RoomUpgrades upgrades)
        {
            var worst = 0.0;

            // The ratio comes from the hall, which is the one place it is worked out. This was a
            // third copy of the same three lines, alongside the floor tile and the cabinet panel.
            foreach (var square in hall.Occupied())
            {
                worst = Math.Max(worst,
                    hall.HeatRatio(square.Column, square.Row, kilowattsPerAccelerator, upgrades));
            }

            // Reported against the point where throttling begins, so 100% is exactly the edge and
            // anything past it is the cabinet already losing throughput.
            return Math.Clamp(worst / ServerRackCatalog.ThrottleFreeHeadroom, 0.0, 1.6);
        }

        /// <summary>
        /// The worst cabinet's state, from the raw ratio rather than the reported one.
        ///
        /// **This was a fourth copy of the colour mapping and it was reading the wrong number.**
        /// <see cref="HottestRatio"/> divides by `ThrottleFreeHeadroom` so the bar can show 100% at
        /// the edge; the thresholds beside it were written for the undivided ratio, so the banner
        /// turned amber at a load the floor still drew green. One reading, one palette.
        /// </summary>
        private static ServerRackCatalog.RackHeat HottestState(ServerHall hall,
            double kilowattsPerAccelerator, RoomUpgrades upgrades)
        {
            var worst = 0.0;

            foreach (var square in hall.Occupied())
            {
                worst = Math.Max(worst,
                    hall.HeatRatio(square.Column, square.Row, kilowattsPerAccelerator, upgrades));
            }

            return ServerRackCatalog.HeatOf(worst);
        }

        private static Color LoadTone(float load) =>
            load > 0.95f ? new Color(0.85f, 0.31f, 0.29f)
            : load > 0.80f ? new Color(0.91f, 0.55f, 0.24f)
            : new Color(0.49f, 0.78f, 0.60f);
    }
}
