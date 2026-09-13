using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// One cabinet, opened: what is in its slots and what that is costing.
    ///
    /// **The slots are drawn, not counted.** A line saying "9 of 12 used" is a fact; twelve little
    /// bars where nine are lit and three are empty is the same fact in a form that answers the next
    /// question without being asked, which is whether the fan will fit.
    ///
    /// Nothing here computes. Heat, cooling and throughput all come out of the same `ServerHall`
    /// call the fleet reads, so a cabinet cannot look healthy on this panel and throttle in the
    /// books.
    /// </summary>
    public sealed class RackEditorPanel
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        public RackEditorPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>Set by the screen, so the cross closes the right thing.</summary>
        public Action Close { get; set; }

        public VisualElement Build(int column, int row)
        {
            var simulation = company();
            var hall = simulation.State.Hall;
            var square = hall.At(column, row);

            var veil = new VisualElement();
            veil.AddToClassList("rackmodal");

            var card = new VisualElement();
            card.AddToClassList("rackmodal__card");
            veil.Add(card);

            if (square.IsEmpty)
            {
                Close?.Invoke();
                return veil;
            }

            var definition = ServerRackCatalog.Get(square.Rack);

            card.Add(BuildHead(definition));
            card.Add(BuildStateBanner(simulation, column, row));
            card.Add(BuildStock(simulation));
            card.Add(BuildSlots(simulation, square, definition));
            card.Add(BuildStats(simulation, square, definition));
            card.Add(BuildActions(simulation, column, row, square));

            return veil;
        }

        /// <summary>
        /// What this cabinet is doing, in a band the colour of the strip on its own front.
        ///
        /// **The panel used to answer this only in numbers.** Heat, cooling and throughput were
        /// all on it, in kilowatts, and working out from those three whether the cabinet was
        /// fine is arithmetic the player has to do while the room is getting hot. The colour is
        /// the answer and the sentence is what to do about it; the kilowatts stay underneath
        /// for whoever wants to check.
        ///
        /// The state comes from <see cref="ServerHall.HeatAt"/>, which is the same reading the
        /// floor paints, so this band and the cabinet behind it cannot disagree.
        /// </summary>
        private VisualElement BuildStateBanner(CompanySimulation simulation, int column, int row)
        {
            var known = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part);

            var state = simulation.State.Hall.HeatAt(
                column, row, known ? part.PowerKilowatts : 0.0, simulation.Room);

            return HeatBand(state);
        }

        /// <summary>
        /// One state as a coloured band. Public and static because the room draws the same band
        /// in its legend, and two of these would be two places to get a colour wrong.
        /// </summary>
        public static VisualElement HeatBand(ServerRackCatalog.RackHeat state)
        {
            var band = new VisualElement();
            band.AddToClassList("rheat");
            band.AddToClassList(ServerRackCatalog.ClassFor(state));

            var word = new Label(Loc.T(ServerRackCatalog.KeyFor(state)));
            word.AddToClassList("rheat__word");
            band.Add(word);

            var note = new Label(Loc.T(ServerRackCatalog.KeyFor(state) + ".note"));
            note.AddToClassList("rheat__note");
            band.Add(note);

            return band;
        }

        /// <summary>
        /// What the company owns, how much of it is on this floor, and what is still in transit.
        ///
        /// **Reported plainly: "I bought about a thousand and I cannot see where they are."**
        /// Nothing in the room ever listed the silicon the company had paid for. The rail says how
        /// many are housed and the cabinet says how many are in it, and between those two numbers
        /// a player who has just spent forty million has no idea whether the order exists.
        ///
        /// Newest first, which is the order they were bought in and the order that matters: the
        /// thing a player is looking for after a purchase is the purchase.
        ///
        /// **It does not offer to put a card in this cabinet, and that is honest rather than
        /// missing.** The hall spreads every owned accelerator across every cabinet in proportion
        /// to what each one can hold, on every tick; there is no per-cabinet placement in the
        /// simulation to expose. A button that appeared to put one here and then watched the next
        /// day move it would be worse than no button. Naming what is owned is the half that is
        /// true today.
        /// </summary>
        private static VisualElement BuildStock(CompanySimulation simulation)
        {
            var state = simulation.State;

            var block = new VisualElement();
            block.AddToClassList("rackmodal__stock");

            var heading = new Label(Loc.T("rack.stock"));
            heading.AddToClassList("panel__heading");
            block.Add(heading);

            var online = 0;
            var waiting = 0;

            var rows = new List<(string Name, int Units, bool Waiting, GameDate Arrives)>();

            foreach (var asset in state.Pool.Assets)
            {
                if (asset.Units <= 0
                    || !HardwareCatalog.TryGet(asset.GenerationId, out var generation)
                    || generation.Class != HardwareClass.Accelerator)
                {
                    continue;
                }

                var here = asset.IsOnline(state.Date);

                if (here)
                {
                    online += asset.Units;
                }
                else
                {
                    waiting += asset.Units;
                }

                rows.Add((generation.DisplayName, asset.Units, !here, asset.CommissionDate));
            }

            if (rows.Count == 0)
            {
                var none = new Label(Loc.T("rack.stock.none"));
                none.AddToClassList("field__hint");
                block.Add(none);

                return block;
            }

            // The three numbers a player is actually asking for: what is here, what is in the
            // cabinets on this floor, and what is sitting in a datacenter somewhere else.
            var housed = state.Hall.HousedAccelerators;

            block.Add(UiParts.StatLine(Loc.T("rack.stock.owned"), online.ToString()));
            block.Add(UiParts.StatLine(Loc.T("rack.stock.here"), housed.ToString()));

            block.Add(UiParts.StatLine(Loc.T("rack.stock.elsewhere"),
                Math.Max(0, online - housed).ToString()));

            if (waiting > 0)
            {
                block.Add(UiParts.StatLine(Loc.T("rack.stock.waiting"), waiting.ToString()));
            }

            // Newest order first, which is what somebody who just bought is looking for.
            rows.Reverse();

            var list = new VisualElement();
            list.AddToClassList("rackstock");

            for (var index = 0; index < rows.Count && index < 6; index++)
            {
                var (name, units, later, arrives) = rows[index];

                var line = new Label(later
                    ? Loc.T("rack.stock.row_waiting", units, name, arrives.ToString())
                    : Loc.T("rack.stock.row", units, name));

                line.AddToClassList("rackstock__row");
                line.EnableInClassList("rackstock__row--waiting", later);
                list.Add(line);
            }

            block.Add(list);
            return block;
        }

        private VisualElement BuildHead(ServerRackDefinition definition)
        {
            var head = new VisualElement();
            head.AddToClassList("rackmodal__head");

            var name = new Label(definition.DisplayName);
            name.AddToClassList("rackmodal__name");
            head.Add(name);

            var close = new Button(() => Close?.Invoke()) { text = "✕" };
            close.AddToClassList("rackmodal__close");
            head.Add(close);

            return head;
        }

        /// <summary>
        /// The cabinet as a column of slots, filled bottom up.
        ///
        /// Accelerators first, then fans, which is how a rack is actually loaded and also what makes
        /// the trade visible: the fans sit at the top where the next card would have gone.
        /// </summary>
        /// <summary>
        /// The cabinet, drawn, with what is in it.
        ///
        /// **This was twelve coloured bars.** The bars answered "will the fan fit" and nothing
        /// else; the drawing answers it just as directly and also says what is in there, which era
        /// the silicon is from, and whether the heat has caught up with it.
        ///
        /// The lights are drawn by the game over dark art, never baked into it. Occupancy and
        /// throttling are simulation state, and a lit indicator painted into a texture would still
        /// be lit on a cabinet that had cooked.
        /// </summary>
        private static VisualElement BuildSlots(CompanySimulation simulation, HallSquare square,
            ServerRackDefinition definition)
        {
            var block = new VisualElement();
            block.AddToClassList("rackmodal__slots");

            var heading = new Label(Loc.T("rack.slots"));
            heading.AddToClassList("panel__heading");
            block.Add(heading);

            var used = square.Accelerators + square.Fans * ServerRackCatalog.FanSlots;

            var count = new Label(Loc.T("rack.slots_used", used, definition.Slots));
            count.AddToClassList("field__hint");
            block.Add(count);

            block.Add(BuildUplink(simulation, square, definition));

            var known = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part);
            var room = simulation.Room;
            var heat = known ? square.Accelerators * part.PowerKilowatts : 0.0;
            var cooling = room.CoolingFor(definition, square.Fans);

            var hot = ServerRackCatalog.ThrottleFactor(
                heat, cooling, room.PenaltyFor(square.Rack)) < 1.0;
            var era = simulation.State.Date.Year;

            var fills = new List<SlotFill>(definition.Slots);

            for (var index = 0; index < definition.Slots; index++)
            {
                if (index < square.Accelerators)
                {
                    fills.Add(new SlotFill(RackArt.Sled(era), RackArt.SledLights, 1.0, hot));
                }
                else if (index < used)
                {
                    fills.Add(new SlotFill(RackArt.Fan(), RackArt.FanRings, 1.0, hot));
                }
                else
                {
                    fills.Add(new SlotFill(RackArt.Blank(), default, 0.0, false));
                }
            }

            var face = new RackFace();
            face.Show(square.Rack, fills);

            block.Add(face);
            return block;
        }

        /// <summary>
        /// The cabinet's own switch, lit by how much of the cabinet is actually plugged into it.
        ///
        /// **Not a slot and not for sale.** Every rack has a switch at the top of it; this one is
        /// part of the cabinet the player already bought, which is why it sits above the slot count
        /// rather than inside the stack. Drawing it as something purchasable would be a fourteenth
        /// thing to buy that the simulation has no idea about.
        ///
        /// Thirty two ports, and the lit share is the cards in this cabinet against its slots. A
        /// half-full cabinet has a half-lit switch, which is a fact about the company rather than
        /// a decoration.
        /// </summary>
        private static VisualElement BuildUplink(CompanySimulation simulation, HallSquare square,
            ServerRackDefinition definition)
        {
            var strip = new RackSlot();
            strip.AddToClassList("rackmodal__uplink");

            var share = definition.Slots > 0
                ? square.Accelerators / (double)definition.Slots
                : 0.0;

            strip.Show(new SlotFill(
                RackArt.Support(HardwareClass.Network), RackArt.FabricPorts, share, false));

            return strip;
        }

        private static VisualElement BuildStats(CompanySimulation simulation, HallSquare square,
            ServerRackDefinition definition)
        {
            var block = new VisualElement();
            block.AddToClassList("rackmodal__stats");

            var known = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part);
            var room = simulation.Room;
            var heat = known ? square.Accelerators * part.PowerKilowatts : 0.0;
            var cooling = room.CoolingFor(definition, square.Fans);

            var penalty = room.PenaltyFor(square.Rack);
            var factor = ServerRackCatalog.ThrottleFactor(heat, cooling, penalty);
            var draw = heat + square.Fans * ServerRackCatalog.FanDrawKilowatts;

            block.Add(UiParts.StatLine(Loc.T("rack.accelerators"),
                $"{square.Accelerators} / {definition.Slots}"));
            block.Add(UiParts.StatLine(Loc.T("rack.fans"), square.Fans.ToString()));
            block.Add(UiParts.StatLine(Loc.T("rack.heat"), UiFormat.Kilowatts(heat)));
            block.Add(UiParts.StatLine(Loc.T("rack.cooling"), UiFormat.Kilowatts(cooling)));
            block.Add(UiParts.StatLine(Loc.T("rack.draw"), UiFormat.Kilowatts(draw)));

            block.Add(UiParts.StatLine(Loc.T("rack.throughput"),
                known
                    ? UiFormat.Petaflops(square.Accelerators * part.PetaflopsPerUnit * factor)
                    : "0"));

            // **The one node in this game that buys information rather than a number.** Fitting a
            // card is the decision this panel exists for and its cost was only visible afterwards:
            // the player added silicon, the cabinet went orange, and the throughput they had just
            // paid for was smaller than the throughput they had before. Rack telemetry answers it
            // in advance, and a company that has not researched it still has to find out the way
            // everybody did until now.
            if (room.ShowsTelemetry && known)
            {
                block.Add(UiParts.StatLine(Loc.T("rack.next_card"),
                    NextCardReading(square, definition, part, room)));
            }

            // The verdict, in a sentence. A throttle figure alone does not say that the power bill
            // is being paid in full for throughput that is not arriving.
            var verdict = new Label(factor >= 1.0
                ? Loc.T("rack.healthy")
                : Loc.T("rack.throttled", UiFormat.Percent(1.0 - factor)));

            verdict.AddToClassList("rackmodal__verdict");
            verdict.EnableInClassList("rackmodal__verdict--bad", factor < 1.0);
            block.Add(verdict);

            return block;
        }

        /// <summary>
        /// What one more accelerator would do to this cabinet, before it is fitted.
        ///
        /// The same two calls the fleet makes, on one more card. Nothing here is a second formula:
        /// a panel that predicted heat differently from the way the books charge for it would be
        /// worse than saying nothing, because the player would trust it.
        /// </summary>
        private static string NextCardReading(HallSquare square, ServerRackDefinition definition,
            HardwareGeneration part, RoomUpgrades room)
        {
            var used = square.Accelerators + square.Fans * ServerRackCatalog.FanSlots;

            if (used >= definition.Slots)
            {
                return Loc.T("rack.next_full");
            }

            var heat = (square.Accelerators + 1) * part.PowerKilowatts;
            var cooling = room.CoolingFor(definition, square.Fans);
            var factor = ServerRackCatalog.ThrottleFactor(heat, cooling, room.PenaltyFor(square.Rack));

            return Loc.T("rack.next_reading",
                UiFormat.Kilowatts(heat), UiFormat.Percent(factor, 0));
        }

        private VisualElement BuildActions(CompanySimulation simulation, int column, int row,
            HallSquare square)
        {
            var row_ = new VisualElement();
            row_.AddToClassList("rackmodal__actions");

            var canAfford = simulation.State.CashUsd >= ServerRackCatalog.FanPriceUsd;
            var hasRoom = simulation.State.Hall.FreeSlots(column, row) >= ServerRackCatalog.FanSlots;

            var add = new Button(() =>
            {
                if (simulation.TryFitFan(column, row, out _))
                {
                    GuideOverlay.Reached?.Invoke("walk_room_fit");

                    changed?.Invoke();
                }
            })
            {
                text = Loc.T("rack.add_fan", UiFormat.Money(ServerRackCatalog.FanPriceUsd))
            };

            add.AddToClassList("button");
            add.AddToClassList("button--primary");
            add.SetEnabled(canAfford && hasRoom);
            add.tooltip = hasRoom ? string.Empty : Loc.T("rack.full");
            row_.Add(add);

            if (square.Fans > 0)
            {
                var pull = new Button(() =>
                {
                    // Through the simulation rather than straight at the hall, so the fan lands in
                    // the store room instead of being destroyed on the way out of the cabinet.
                    if (simulation.TryStoreFan(column, row))
                    {
                        changed?.Invoke();
                    }
                })
                { text = Loc.T("rack.pull_fan") };

                pull.AddToClassList("button");
                row_.Add(pull);
            }

            return row_;
        }
    }
}
