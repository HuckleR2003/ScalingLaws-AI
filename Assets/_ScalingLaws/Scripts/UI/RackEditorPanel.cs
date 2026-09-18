using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
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

        /// <summary>
        /// The right-hand column, which is what a row is dragged onto.
        ///
        /// Held rather than passed, because the rows are built before the cabinet is: the bay goes
        /// in on the left first. A row only asks for this when the pointer is released, by which
        /// time the whole card exists.
        /// </summary>
        private VisualElement cabinetColumn;

        /// <summary>The card under the cursor while a row is being dragged, or null.</summary>
        private VisualElement carried;

        /// <summary>
        /// Says what happened, across the top of the screen.
        ///
        /// Fitting a card moves one thing by one slot in a picture the player may not be looking
        /// at, and selling a batch moves money and nothing else. Both need a sentence.
        /// </summary>
        public Action<string, string> announce { get; set; }

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

            // **The whole card turns, not only the band.** Asked for by the author in those words:
            // red with OVERHEATING, yellow with NEAR OVERHEATING. A band a player has to read is a
            // band a player skips; a card that has changed colour is noticed before it is read.
            var (_, perCardKw) = simulation.HallPerAccelerator();
            var heatNow = hall.HeatAt(column, row, perCardKw, simulation.Room);
            var roomNow = simulation.RoomClimateToday();

            var hot = heatNow == ServerRackCatalog.RackHeat.Cooking
                      || roomNow.State == ServerRackCatalog.RoomClimateState.Overheating;
            var warm = !hot && (heatNow == ServerRackCatalog.RackHeat.Warm
                                || roomNow.State == ServerRackCatalog.RoomClimateState.Warm);

            card.EnableInClassList("rackmodal__card--hot", hot);
            card.EnableInClassList("rackmodal__card--warm", warm);

            if (hot || warm)
            {
                card.Add(BuildAlarm(hot, heatNow, roomNow));
            }

            // **Two columns: the parts on the left, the cabinet on the right.**
            //
            // Reported twice, the second time as still broken: you can buy silicon and there is no
            // way to put it anywhere. The bay is the answer, and it is on the left because that is
            // where the author asked for it and because the thing being filled should be the thing
            // in the middle of the screen.
            var body = new VisualElement();
            body.AddToClassList("rackmodal__body");

            body.Add(BuildPartsBay(simulation, column, row));

            var cabinet = new VisualElement();
            cabinet.AddToClassList("rackmodal__cabinet");
            cabinetColumn = cabinet;
            cabinet.Add(BuildSlots(simulation, square, definition));
            cabinet.Add(BuildStats(simulation, square, definition));
            cabinet.Add(BuildOverclock(simulation, column, row, roomNow));
            cabinet.Add(BuildActions(simulation, column, row, square));

            body.Add(cabinet);
            card.Add(body);

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
            // The owned cards' own draw, which is what the fleet is served from. The rentable part
            // was a year ahead of a company running older silicon and read hotter than the room.
            var (_, kilowatts) = simulation.HallPerAccelerator();

            var state = simulation.State.Hall.HeatAt(column, row, kilowatts, simulation.Room);

            return HeatBand(state);
        }

        /// <summary>
        /// OVERHEATING or NEAR OVERHEATING, with the one piece of advice that fixes it.
        ///
        /// **The advice names the room when the room is the cause.** A cabinet losing work because
        /// the whole cellar is over its budget is not fixed by a fan in this cabinet, and telling a
        /// player to fit one would spend their money on the wrong thing.
        /// </summary>
        private static VisualElement BuildAlarm(bool hot, ServerRackCatalog.RackHeat heat, RoomClimate room)
        {
            var alarm = new VisualElement();
            alarm.AddToClassList("rackalarm");
            alarm.AddToClassList(hot ? "rackalarm--hot" : "rackalarm--warm");

            var word = new Label(Loc.T(hot ? "rack.overheating" : "rack.near_overheating"));
            word.AddToClassList("rackalarm__word");
            alarm.Add(word);

            string advice;

            if (room.State == ServerRackCatalog.RoomClimateState.Overheating)
            {
                advice = Loc.T("room.climate.advice_hot");
            }
            else if (heat == ServerRackCatalog.RackHeat.Cooking)
            {
                advice = Loc.T("rack.overheating.note");
            }
            else if (room.State == ServerRackCatalog.RoomClimateState.Warm)
            {
                advice = Loc.T("room.climate.advice_warm");
            }
            else
            {
                advice = Loc.T("rack.near_overheating.note");
            }

            var note = new Label(advice);
            note.AddToClassList("rackalarm__note");
            alarm.Add(note);

            return alarm;
        }

        /// <summary>
        /// STOCK, LEVEL 1, LEVEL 2. Raising it needs a room with air to spare and the simulation
        /// says so when it refuses; lowering it is always allowed.
        /// </summary>
        private VisualElement BuildOverclock(CompanySimulation simulation, int column, int row,
            RoomClimate room)
        {
            var hall = simulation.State.Hall;
            var current = hall.OverclockAt(column, row);
            var coolEnough = room.Ratio < ServerRackCatalog.OverclockAllowedBelow;

            var block = new VisualElement();
            block.AddToClassList("rackoc");
            block.tooltip = Loc.T("rack.oc.note");

            var caption = new Label(Loc.T("rack.oc"));
            caption.AddToClassList("rackoc__caption");
            block.Add(caption);

            var choices = new VisualElement();
            choices.AddToClassList("rackoc__choices");

            for (var level = 0; level <= ServerRackCatalog.OverclockLevels; level++)
            {
                var wanted = level;

                var choice = new Button(() =>
                {
                    if (simulation.TrySetOverclock(column, row, wanted, out var why))
                    {
                        changed?.Invoke();
                    }
                    else
                    {
                        announce?.Invoke(Loc.T("rack.oc"), why);
                    }
                })
                {
                    text = level == 0 ? Loc.T("rack.oc.off") : Loc.T("rack.oc.level", level)
                };

                choice.AddToClassList("chip");
                choice.AddToClassList("rackoc__choice");
                choice.EnableInClassList("rackoc__choice--on", level == current);
                choice.SetEnabled(level <= current || coolEnough);
                choices.Add(choice);
            }

            block.Add(choices);

            string line;

            if (current > 0 && !room.OverclocksRunning)
            {
                line = Loc.T("rack.oc.suspended");
            }
            else if (!coolEnough && current < ServerRackCatalog.OverclockLevels)
            {
                line = Loc.T("rack.oc_room_warm");
            }
            else
            {
                line = Loc.T("rack.oc.note");
            }

            var note = new Label(line);
            note.AddToClassList("rackoc__note");
            note.EnableInClassList("rackoc__note--bad", current > 0 && !room.OverclocksRunning);
            block.Add(note);

            return block;
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

        // `BuildStock` stood here: a read-only list of what the company owned, added when a
        // tester could not find a thousand accelerators he had bought. It answered "where are
        // they" and nothing else, and the next report was that there is still no way to put one
        // anywhere. `BuildPartsBay` is the same list with the two things you can do to a row on
        // it, so this one is gone rather than left beside it saying the same thing twice.

        /// <summary>
        /// Everything the company owns, sorted, with a way to put one in this cabinet.
        ///
        /// **Strongest first**, by petaflops a card, because that is the order a player thinks in
        /// when deciding what goes in the cabinet they are looking at. What is still in transit is
        /// listed underneath with its date, greyed, because it is the answer to "where did my order
        /// go" and it is not something that can be fitted yet.
        ///
        /// A row does three things and the author asked for all three: **double click fits one**,
        /// the FIT button fits one, and SELL sells the whole batch at today's residual. Dragging a
        /// row onto the cabinet is not built.
        /// </summary>
        private VisualElement BuildPartsBay(CompanySimulation simulation, int column, int row)
        {
            var state = simulation.State;

            var bay = new VisualElement();
            bay.AddToClassList("rackbay");

            var heading = new Label(Loc.T("rack.stock"));
            heading.AddToClassList("panel__heading");
            bay.Add(heading);

            // One line per generation rather than per purchase order, because two orders of the
            // same card are the same card and a player counting their A100s does not care which
            // invoice they arrived on. The index of the first batch is kept for the sale.
            var online = new List<(HardwareGeneration Part, int Units, int Asset)>();
            var waiting = new List<(HardwareGeneration Part, int Units, GameDate Arrives)>();

            for (var index = 0; index < state.Pool.Assets.Count; index++)
            {
                var asset = state.Pool.Assets[index];

                if (asset.Units <= 0
                    || !HardwareCatalog.TryGet(asset.GenerationId, out var part)
                    || part.Class != HardwareClass.Accelerator)
                {
                    continue;
                }

                if (!asset.IsOnline(state.Date))
                {
                    waiting.Add((part, asset.Units, asset.CommissionDate));
                    continue;
                }

                var at = online.FindIndex(line => line.Part.Id == part.Id);

                if (at >= 0)
                {
                    online[at] = (part, online[at].Units + asset.Units, online[at].Asset);
                }
                else
                {
                    online.Add((part, asset.Units, index));
                }
            }

            online.Sort((left, right) =>
                right.Part.PetaflopsPerUnit.CompareTo(left.Part.PetaflopsPerUnit));

            if (online.Count == 0 && waiting.Count == 0)
            {
                var none = new Label(Loc.T("rack.stock.none"));
                none.AddToClassList("field__hint");
                bay.Add(none);

                return bay;
            }

            var housed = state.Hall.HousedAccelerators;
            var owned = simulation.OnlineAccelerators();

            bay.Add(UiParts.StatLine(Loc.T("rack.stock.here"),
                state.Hall.At(column, row).Accelerators.ToString()));

            bay.Add(UiParts.StatLine(Loc.T("rack.stock.elsewhere"),
                Math.Max(0, housed - state.Hall.At(column, row).Accelerators).ToString()));

            bay.Add(UiParts.StatLine(Loc.T("rack.stock.loose"),
                Math.Max(0, owned - housed).ToString()));

            var list = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };

            list.AddToClassList("rackbay__list");

            foreach (var line in online)
            {
                list.Add(PartRow(simulation, column, row, line.Part, line.Units, line.Asset));
            }

            foreach (var line in waiting)
            {
                var coming = new Label(Loc.T("rack.stock.row_waiting",
                    line.Units, line.Part.DisplayName, line.Arrives.ToString()));

                coming.AddToClassList("rackstock__row");
                coming.AddToClassList("rackstock__row--waiting");
                list.Add(coming);
            }

            bay.Add(list);

            var note = new Label(Loc.T("rack.fit_note"));
            note.AddToClassList("field__hint");
            bay.Add(note);

            return bay;
        }

        /// <summary>One generation the company owns, with the two things that can be done to it.</summary>
        private VisualElement PartRow(CompanySimulation simulation, int column, int row,
            HardwareGeneration part, int units, int asset)
        {
            var line = new VisualElement();
            line.AddToClassList("rackbay__row");

            var words = new VisualElement();
            words.AddToClassList("rackbay__words");

            var name = new Label(units + "x  " + part.DisplayName);
            name.AddToClassList("rackbay__name");
            words.Add(name);

            var spec = new Label(UiFormat.Petaflops(part.PetaflopsPerUnit)
                + "  ·  " + UiFormat.Kilowatts(part.PowerKilowatts));

            spec.AddToClassList("rackbay__spec");
            words.Add(spec);

            line.Add(words);

            var fit = new Button(() => Fit(simulation, column, row, part))
            {
                text = Loc.T("rack.fit")
            };

            fit.AddToClassList("rackbay__fit");
            line.Add(fit);

            var sell = new Button(() =>
            {
                if (simulation.TrySellHardware(asset, units, out var proceeds, out var why))
                {
                    AudioDirector.Confirm();
                    announce?.Invoke(Loc.T("rack.sold"),
                        Loc.T("rack.sold_note", units, part.DisplayName,
                            UiFormat.Money(proceeds)));
                }
                else
                {
                    AudioDirector.Deny();
                    announce?.Invoke(Loc.T("rack.sell_cards"), why);
                }

                changed?.Invoke();
            })
            {
                text = Loc.T("rack.sell_cards")
            };

            sell.AddToClassList("rackbay__sell");
            line.Add(sell);

            // **Double click fits one**, which is what the author asked for by name and what a
            // player tries before they find a button.
            line.RegisterCallback<ClickEvent>(click =>
            {
                if (click.clickCount >= 2)
                {
                    Fit(simulation, column, row, part);
                }
            });

            Draggable(line, simulation, column, row, part);

            return line;
        }

        /// <summary>
        /// Lets a row be dragged out of the list and dropped on the cabinet.
        ///
        /// **Asked for by name**, alongside the button and the double click, and it is the one of
        /// the three a player tries without being told. Three things make it work and each of them
        /// is a way it goes wrong without:
        ///
        /// - **The pointer is captured on the first move, never on the press.** A row is also a
        ///   double click target and a card with two buttons on it; capturing on the press would
        ///   swallow both.
        /// - **What follows the cursor is a copy**, mounted on the panel root and ignoring pointer
        ///   events. Moving the row itself takes it out of the list under the cursor, and a ghost
        ///   that can be hit by the pointer is a ghost that lands on itself.
        /// - **The drop is tested against the cabinet column's world rectangle**, not against
        ///   whatever the pointer is over. UI Toolkit reports the topmost element under the
        ///   pointer, which during a drag is frequently the ghost.
        /// </summary>
        private void Draggable(VisualElement line, CompanySimulation simulation, int column,
            int row, HardwareGeneration part)
        {
            var from = Vector2.zero;
            var dragging = false;

            line.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.button != 0)
                {
                    return;
                }

                from = down.position;
                dragging = false;
            });

            line.RegisterCallback<PointerMoveEvent>(move =>
            {
                if (move.pressedButtons != 1)
                {
                    return;
                }

                if (!dragging)
                {
                    // Far enough that it is a drag rather than a hand shaking on a click. Six
                    // pixels is about what a double click survives.
                    if ((move.position - (Vector3)from).sqrMagnitude < 36f)
                    {
                        return;
                    }

                    dragging = true;
                    line.CapturePointer(move.pointerId);
                    carried = Ghost(part);
                }

                if (carried == null)
                {
                    return;
                }

                carried.style.left = move.position.x + 14f;
                carried.style.top = move.position.y - 12f;
            });

            line.RegisterCallback<PointerUpEvent>(up =>
            {
                if (!dragging)
                {
                    return;
                }

                dragging = false;
                line.ReleasePointer(up.pointerId);

                var onCabinet = cabinetColumn != null
                    && cabinetColumn.worldBound.Contains(up.position);

                carried?.RemoveFromHierarchy();
                carried = null;

                if (onCabinet)
                {
                    Fit(simulation, column, row, part);
                }
            });
        }

        /// <summary>The copy that follows the cursor. Null when there is nowhere to mount it.</summary>
        private static VisualElement Ghost(HardwareGeneration part)
        {
            var host = InsightTip.Host;

            if (host == null)
            {
                return null;
            }

            var ghost = new Label(part.DisplayName);
            ghost.AddToClassList("rackbay__ghost");
            ghost.pickingMode = PickingMode.Ignore;

            host.Add(ghost);
            return ghost;
        }

        /// <summary>Puts one card in the cabinet on screen and says so.</summary>
        private void Fit(CompanySimulation simulation, int column, int row, HardwareGeneration part)
        {
            if (simulation.TryFitCard(column, row, out var why))
            {
                AudioDirector.Confirm();

                announce?.Invoke(Loc.T("rack.fit"),
                    Loc.T("rack.fitted", part.DisplayName,
                        ServerRackCatalog.Get(simulation.State.Hall.At(column, row).Rack)
                            .DisplayName));
            }
            else
            {
                AudioDirector.Deny();
                announce?.Invoke(Loc.T("rack.fit"), why);
            }

            changed?.Invoke();
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
            var hall = simulation.State.Hall;
            var (perCardPf, perCardKw) = simulation.HallPerAccelerator();
            var climate = hall.Climate(perCardKw, room);

            // **Everything below is what the hall itself computes.** Heat and cooling here used to
            // be worked out inline, which was right until the room and the overclock arrived and
            // then quoted a cabinet that ignored both.
            var level = climate.OverclocksRunning ? hall.OverclockAt(square.Column, square.Row) : 0;
            var heat = square.Accelerators * perCardKw
                       * (1.0 + ServerRackCatalog.OverclockHeatPerLevel * level);
            var cooling = room.CoolingFor(definition, square.Fans) * climate.CabinetFactor;
            var delivered = hall.CabinetPetaflops(square.Column, square.Row, perCardPf, perCardKw, room);
            var ideal = square.Accelerators * perCardPf
                        * (1.0 + ServerRackCatalog.OverclockThroughputPerLevel * level);
            var factor = ideal > 0.0 ? Math.Min(1.0, delivered / ideal) : 1.0;
            var draw = heat + square.Fans * ServerRackCatalog.FanDrawKilowatts;

            block.Add(UiParts.StatLine(Loc.T("rack.accelerators"),
                $"{square.Accelerators} / {definition.Slots}"));
            block.Add(UiParts.StatLine(Loc.T("rack.fans"), square.Fans.ToString()));
            block.Add(UiParts.StatLine(Loc.T("rack.heat"), UiFormat.Kilowatts(heat)));
            block.Add(UiParts.StatLine(Loc.T("rack.cooling"), UiFormat.Kilowatts(cooling)));
            block.Add(UiParts.StatLine(Loc.T("rack.draw"), UiFormat.Kilowatts(draw)));

            block.Add(UiParts.StatLine(Loc.T("rack.throughput"), UiFormat.Petaflops(delivered)));

            var people = new Label(Loc.T("rack.users",
                UiFormat.Count(delivered * simulation.UsersPerPetaflop())));
            people.AddToClassList("rackmodal__users");
            people.tooltip = Loc.T("room.users.note");
            block.Add(people);

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
