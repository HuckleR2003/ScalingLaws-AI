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

        public ServerRoomBanner()
        {
            Root = new VisualElement();
            Root.AddToClassList("rbanner");

            Root.Add(Figure(Loc.T("room.banner.capacity"), capacity, capacityNote));
            Root.Add(Figure(Loc.T("room.banner.temperature"), temperature, temperatureNote));
            Root.Add(Figure(Loc.T("room.banner.power"), power, powerNote));

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
            var housed = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part)
                ? hall.Output(part.PetaflopsPerUnit, part.PowerKilowatts)
                : new HallOutput(0.0, 0.0, 0);

            capacity.text = UiFormat.Petaflops(housed.Petaflops);
            capacityNote.text = $"{hall.HousedAccelerators} / {hall.TotalSlots}";

            // ---- heat ----------------------------------------------------------------------------
            //
            // The hottest cabinet rather than an average. An average across a floor where one rack
            // is cooking and three are cold reads as comfortable, which is the one thing it is not.
            var hottest = HottestRatio(hall, part);

            temperature.text = UiFormat.Percent(hottest, 0);
            temperatureNote.text = Loc.T("room.banner.of_max", "100%");
            temperature.style.color = HeatTone(hottest);

            // ---- power ---------------------------------------------------------------------------
            power.text = UiFormat.Kilowatts(housed.DrawKilowatts);
            powerNote.text = UiFormat.Money(
                (long)(housed.DrawKilowatts * 24.0 * 0.19)) + " " + Loc.T("common.a_day");

            // ---- load ----------------------------------------------------------------------------
            var load = Mathf.Clamp01((float)quality.Utilisation);

            loadFill.style.width = Length.Percent(load * 100f);
            loadFill.style.backgroundColor = LoadTone(load);
            latency.text = UiFormat.Milliseconds(quality.ResponseMilliseconds);

            health.text = housed.ThrottledRacks > 0
                ? Loc.T("room.banner.throttling", housed.ThrottledRacks)
                : Loc.T("room.banner.all_clear");

            health.EnableInClassList("rbanner__health--bad", housed.ThrottledRacks > 0);
        }

        /// <summary>
        /// How close the worst cabinet is to the point where it stops delivering, 0 to 1.
        ///
        /// Expressed against its own cooling rather than in degrees, because the catalog rates racks
        /// in kilowatts of heat rather than in temperature, and inventing a degree figure to display
        /// would be a number the simulation does not use.
        /// </summary>
        private static double HottestRatio(ServerHall hall, HardwareGeneration part)
        {
            var worst = 0.0;

            // The ratio comes from the hall, which is the one place it is worked out. This was a
            // third copy of the same three lines, alongside the floor tile and the cabinet panel.
            foreach (var square in hall.Occupied())
            {
                worst = Math.Max(worst,
                    hall.HeatRatio(square.Column, square.Row, part.PowerKilowatts));
            }

            // Reported against the point where throttling begins, so 100% is exactly the edge and
            // anything past it is the cabinet already losing throughput.
            return Math.Clamp(worst / ServerRackCatalog.ThrottleFreeHeadroom, 0.0, 1.6);
        }

        private static Color HeatTone(double ratio) =>
            ratio > 1.15 ? new Color(0.85f, 0.31f, 0.29f)
            : ratio > 1.0 ? new Color(0.91f, 0.55f, 0.24f)
            : ratio > 0.85 ? new Color(0.89f, 0.75f, 0.27f)
            : new Color(0.49f, 0.78f, 0.60f);

        private static Color LoadTone(float load) =>
            load > 0.95f ? new Color(0.85f, 0.31f, 0.29f)
            : load > 0.80f ? new Color(0.91f, 0.55f, 0.24f)
            : new Color(0.49f, 0.78f, 0.60f);
    }
}
