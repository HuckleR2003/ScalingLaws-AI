using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The basement: the room in 3D, the cabinets standing in it, and the mode where the player
    /// buys, carries, places and rearranges them.
    ///
    /// **This was a form of sixteen buttons reading "0 / 8".** It worked, and it told the player
    /// nothing about the room they had just bought: floor space is the constraint the whole
    /// mechanic turns on, and a list of counts cannot show a floor. The grid is now the room, drawn
    /// by <see cref="BasementStage"/> and clicked directly.
    ///
    /// **The screen owns no rules.** `ServerHall` decides what may stand where, `CompanySimulation`
    /// decides what may be bought, and everything here is a click travelling to one of them. What
    /// the screen does own is the one piece of state neither of them should have: which cabinet the
    /// player is currently carrying.
    ///
    /// ### The thing on the cursor
    ///
    /// It is deliberately **not** a third place a rack can be. A carried cabinet is either a line in
    /// the store room the player has singled out, or a cabinet still standing exactly where it was
    /// while they decide where to put it. Nothing is in limbo, so nothing has to be saved, and
    /// closing the game mid-carry loses nobody anything.
    ///
    /// | Carrying | Left click on a free square | Right click |
    /// |---|---|---|
    /// | from the store room | stands it there | puts it down, back to the shop |
    /// | lifted off the floor | moves it, fans and all | drops it into the store room |
    ///
    /// Lifting is only marked, never performed, which is what lets a move keep the cabinet's fans:
    /// storing and re-standing would return them loose and make the player refit them.
    /// </summary>
    public sealed class ServerRoomScreen
    {
        private readonly System.Func<CompanySimulation> company;
        private readonly System.Action changed;

        /// <summary>Why the last attempt was refused. Empty when nothing has been refused.</summary>
        private string problem = string.Empty;

        private readonly ServerRoomBanner banner = new();
        private readonly BasementStage stage = new();
        private RackEditorPanel editor;

        /// <summary>The silicon shop, built once and kept, because it holds the sort and the batch.</summary>
        private PartsShop shop;

        /// <summary>Whether the shop is open over the room.</summary>
        private bool shopOpen;

        public ServerRoomScreen(System.Func<CompanySimulation> company, System.Action changed,
            System.Action leave = null, System.Action<string, string> announce = null)
        {
            this.company = company;
            this.changed = changed;
            this.leave = leave;
            this.announce = announce;
        }

        /// <summary>
        /// Says something happened, across the top of the screen.
        ///
        /// **Reported plainly: the player buys and barely knows they bought.** Sixty four
        /// accelerators leave the account on one click and the only thing that changes is a
        /// figure in a rail. Same notice the upgrade screen raises when a programme starts, for
        /// the same reason: money leaving with no acknowledgement is money the player is not sure
        /// they spent.
        /// </summary>
        private readonly System.Action<string, string> announce;

        /// <summary>
        /// The way out, back to the office.
        ///
        /// Optional, so the proof renders that build this screen on its own do not have to invent
        /// a shell to leave to. The button simply does nothing there, which is the right answer for
        /// a screenshot.
        /// </summary>
        private readonly System.Action leave;

        /// <summary>The square the player has open, or null when the floor is showing.</summary>
        private (int Column, int Row)? open;

        /// <summary>True while the shop and the store room are on screen.</summary>
        private bool building;

        /// <summary>What is on the cursor, if anything.</summary>
        private ServerRack carrying = ServerRack.None;

        /// <summary>
        /// Where the carried cabinet is still standing, when it came off the floor.
        ///
        /// Null means it came out of the store room. The two are different journeys and the
        /// difference is the whole reason a move can keep its fans.
        /// </summary>
        private (int Column, int Row)? liftedFrom;

        /// <summary>Which square the cursor is over. Presentation only; never read by a rule.</summary>
        private (int Column, int Row)? hovering;

        /// <summary>
        /// Opens the cabinet chooser, without a click.
        ///
        /// **For the proof render, and it earns its place.** A test has no panel, so an event sent
        /// to a button is never dispatched, and the only frame worth taking of this screen is one
        /// with something happening on it. `GameShell` already carries `OpenScreenByName` for the
        /// same reason.
        /// </summary>
        public void PickFor(int column, int row)
        {
            building = true;
            hovering = (column, row);
        }

        /// <summary>Switches the room's camera off when the player leaves. It is a camera, not a picture.</summary>
        public void Hide()
        {
            stage.SetVisible(false);
            stage.HideGhost();
        }

        public VisualElement Build()
        {
            var simulation = company();
            var state = simulation.State;

            var page = new VisualElement();
            page.AddToClassList("content");
            page.AddToClassList("room");

            // The name and the way out, on one line. The basement is a room rather than a page,
            // and every other screen is left through the bottom bar it is standing on: a room needs
            // a door somebody can see.
            var head = new VisualElement();
            head.AddToClassList("room__head");

            var back = new Button(() => leave?.Invoke()) { text = Loc.T("room.back") };
            back.AddToClassList("room__back");
            head.Add(back);

            var title = new Label(Loc.T("room.title"));
            title.AddToClassList("page-title");
            title.AddToClassList("room__title");
            head.Add(title);

            page.Add(head);

            if (!state.HasServerRoom)
            {
                stage.SetVisible(false);
                page.Add(BuildLocked(simulation));
                return page;
            }

            var strap = new Label(Loc.T("room.strap"));
            strap.AddToClassList("page-subtitle");
            page.Add(strap);

            page.Add(BuildHeatLegend());

            var floor = new VisualElement();
            floor.AddToClassList("roomfloor");
            floor.Add(BuildStage(simulation));
            floor.Add(BuildRail(simulation));
            page.Add(floor);

            // **After the floor, not before it.** The banner is absolutely positioned, and an
            // absolute element in UI Toolkit still paints in document order: added first, it went
            // underneath the room and the rail and only its edge was visible. It used to work
            // because the old grid did not reach that corner.
            banner.Refresh(simulation);
            page.Add(banner.Root);

            // **Over the room, like the cabinet editor.** The parts go in the cabinets on this
            // floor, so this is where a player looks for them; the two cards in the rail beside
            // it were a shelf rather than a shop and showed three of twenty two generations.
            if (shopOpen)
            {
                shop ??= new PartsShop(simulation, () => changed?.Invoke(), announce);

                var sheet = new VisualElement();
                sheet.AddToClassList("shopsheet");

                var close = new Button(() =>
                {
                    shopOpen = false;
                    changed?.Invoke();
                })
                {
                    text = Loc.T("common.close")
                };

                close.AddToClassList("shopsheet__close");

                shop.Refresh();
                sheet.Add(shop.Root);
                sheet.Add(close);

                page.Add(sheet);
            }

            if (open.HasValue)
            {
                editor ??= new RackEditorPanel(company, () => changed?.Invoke())
                {
                    announce = announce
                };

                editor.Close = () =>
                {
                    open = null;
                    changed?.Invoke();
                };

                page.Add(editor.Build(open.Value.Column, open.Value.Row));
            }

            // **The tour bar owns the bottom of the screen and so does this panel.** The bar is on
            // the panel root, so it paints after anything in the page and no z-order here can beat
            // it. It moves to the top while a cabinet is open, which is where the room has nothing.
            GuideOverlay.KeepClear?.Invoke(open.HasValue || shopOpen);

            return page;
        }

        /// <summary>
        /// What the colours on the cabinets mean, as five squares on one line.
        ///
        /// **A room read at a glance needs a key or it is decoration.** The floor turns a
        /// cabinet white, blue, green, yellow or red and nothing on the screen said which of
        /// those was a problem, so the only way to find out was to open every cabinet in turn,
        /// which is the opposite of what colouring the room is for.
        ///
        /// It walks the enum rather than listing five, so a sixth state cannot be added without
        /// appearing here.
        /// </summary>
        private static VisualElement BuildHeatLegend()
        {
            var key = new VisualElement();
            key.AddToClassList("hkey");

            var title = new Label(Loc.T("room.legend"));
            title.AddToClassList("hkey__title");
            key.Add(title);

            foreach (ServerRackCatalog.RackHeat state
                in System.Enum.GetValues(typeof(ServerRackCatalog.RackHeat)))
            {
                var item = new VisualElement();
                item.AddToClassList("hkey__item");

                var chip = new VisualElement();
                chip.AddToClassList("hkey__chip");
                chip.AddToClassList(ServerRackCatalog.ClassFor(state));
                item.Add(chip);

                var word = new Label(Loc.T(ServerRackCatalog.KeyFor(state)));
                word.AddToClassList("hkey__word");
                item.Add(word);

                // Both arguments are keys, never sentences: a card built from resolved copy
                // freezes in the language it was built in, which is nine faults in this project.
                InsightTip.AttachKeyed(item, ServerRackCatalog.KeyFor(state),
                    ServerRackCatalog.KeyFor(state) + ".note");

                key.Add(item);
            }

            return key;
        }

        // ---- the room ---------------------------------------------------------------------------

        /// <summary>
        /// The rendered basement, and every pointer event that lands on it.
        ///
        /// **Hovering never rebuilds the page.** The highlight and the carried cabinet are objects
        /// in the room, moved by the stage, so a pointer crossing the floor costs a transform
        /// rather than a rebuilt document. Only something that actually changes the company asks
        /// for a rebuild.
        /// </summary>
        private VisualElement BuildStage(CompanySimulation simulation)
        {
            var view = new VisualElement();
            view.AddToClassList("roomstage");

            stage.Ensure();
            stage.SetVisible(true);

            if (!stage.IsLive)
            {
                var pending = new Label(Loc.T("room.floor"));
                pending.AddToClassList("site-stage__title");
                view.Add(pending);

                var note = new Label(Loc.T("room.scene_missing"));
                note.AddToClassList("site-stage__note");
                view.Add(note);

                return view;
            }

            var known = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part);
            // **The ring belongs to one step of the walkthrough and nothing else.** Named by id
            // rather than by index, because a step inserted above it would silently move the ring
            // onto a sentence about the shop.
            stage.RingTheCabinets = GuideOverlay.WalkingOn(WalkthroughCatalog.RoomCabinetsStepId);

            stage.Dress(simulation.State.Hall, known ? part.PowerKilowatts : 0.0,
                simulation.Room);

            view.style.backgroundImage = Background.FromRenderTexture(stage.Texture);
            view.AddToClassList("roomstage--live");

            view.RegisterCallback<PointerMoveEvent>(evt => OnHover(simulation, view, evt.localPosition));
            view.RegisterCallback<PointerLeaveEvent>(_ => ClearHover());

            view.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!TrySquareAt(view, evt.localPosition, out var column, out var row))
                {
                    // Right-clicking off the floor is how a player puts down something they picked
                    // up by accident, so it has to work in the empty part of the room too.
                    if (evt.button == 1)
                    {
                        Release(simulation);
                    }

                    return;
                }

                if (evt.button == 1)
                {
                    OnRightClick(simulation, column, row);
                }
                else if (evt.button == 0)
                {
                    OnLeftClick(simulation, column, row);
                }

                evt.StopPropagation();
            });

            var hint = new Label(HintFor(simulation));
            hint.AddToClassList("roomstage__hint");
            view.Add(hint);

            return view;
        }

        /// <summary>
        /// Turns a point on the drawn element into a point in the camera's view.
        ///
        /// **The element and the texture are not the same shape.** The background is drawn
        /// scale-and-crop, so it fills the element and is cut off on one axis, and a mapping that
        /// ignored that would drift further from the truth the further the two aspects diverge.
        /// The player would see the highlight lag behind the cursor near the edges of the room,
        /// which is exactly where the corner squares are.
        /// </summary>
        private bool TrySquareAt(VisualElement view, Vector2 local, out int column, out int row)
        {
            column = -1;
            row = -1;

            var rect = view.contentRect;

            if (!stage.IsLive || rect.width <= 1f || rect.height <= 1f)
            {
                return false;
            }

            // **Moved to `StagePicking` when the office needed the same arithmetic.** One copy: the
            // note above says what two would buy, which is a click landing one square from the
            // cursor with the picture right and every test green.
            if (!StagePicking.TryViewport(view, local, stage.Texture, out var viewport))
            {
                return false;
            }

            return stage.SquareUnder(viewport, out column, out row);
        }

        private void OnHover(CompanySimulation simulation, VisualElement view, Vector2 local)
        {
            if (!TrySquareAt(view, local, out var column, out var row))
            {
                ClearHover();
                return;
            }

            if (hovering.HasValue && hovering.Value == (column, row))
            {
                return;
            }

            hovering = (column, row);

            var free = simulation.State.Hall.IsEmpty(column, row);

            // The outline is drawn whether or not something is being carried: an empty hand still
            // wants to know which cabinet a click would open.
            var allowed = carrying == ServerRack.None
                ? !free || building
                : free;

            stage.ShowGhost(carrying, column, row, allowed);
        }

        private void ClearHover()
        {
            hovering = null;
            stage.HideGhost();
        }

        // ---- what a click means ------------------------------------------------------------------

        private void OnLeftClick(CompanySimulation simulation, int column, int row)
        {
            if (carrying != ServerRack.None)
            {
                Put(simulation, column, row);
                return;
            }

            if (simulation.State.Hall.IsEmpty(column, row))
            {
                // An empty square with an empty hand is an invitation to fill it, so the shop
                // opens rather than nothing happening.
                building = true;
                changed?.Invoke();
                return;
            }

            open = (column, row);

            GuideOverlay.Reached?.Invoke("walk_room_open_rack");

            changed?.Invoke();
        }

        /// <summary>
        /// Right click: pick up, or put down.
        ///
        /// Picking up **marks** the cabinet rather than moving it, so a player who changes their
        /// mind has changed nothing, and a move that lands somewhere else keeps the fans that were
        /// bought for it.
        /// </summary>
        private void OnRightClick(CompanySimulation simulation, int column, int row)
        {
            if (carrying != ServerRack.None)
            {
                Release(simulation);
                return;
            }

            var square = simulation.State.Hall.At(column, row);

            if (square.IsEmpty)
            {
                return;
            }

            carrying = square.Rack;
            liftedFrom = (column, row);
            building = true;

            changed?.Invoke();
        }

        /// <summary>Stands the carried cabinet on a square, by whichever route it arrived.</summary>
        private void Put(CompanySimulation simulation, int column, int row)
        {
            var placed = liftedFrom.HasValue
                ? simulation.TryMoveRack(liftedFrom.Value.Column, liftedFrom.Value.Row,
                    column, row, out _)
                : simulation.TryStandRack(column, row, carrying, out _);

            if (!placed)
            {
                return;
            }

            carrying = ServerRack.None;
            liftedFrom = null;

            GuideOverlay.Reached?.Invoke("walk_room_stand");

            stage.HideGhost();
            changed?.Invoke();
        }

        /// <summary>
        /// Puts down whatever is being carried without placing it.
        ///
        /// A cabinet out of the store room goes nowhere, because it never left. One lifted off the
        /// floor goes into the store room, which is the only sense in which right-clicking twice
        /// "puts it away": the first click picked it up, the second says it is not going back down.
        /// </summary>
        private void Release(CompanySimulation simulation)
        {
            if (carrying == ServerRack.None)
            {
                return;
            }

            if (liftedFrom.HasValue)
            {
                simulation.TryStoreRack(liftedFrom.Value.Column, liftedFrom.Value.Row, out _);
            }

            carrying = ServerRack.None;
            liftedFrom = null;

            stage.HideGhost();
            changed?.Invoke();
        }

        private string HintFor(CompanySimulation simulation)
        {
            if (carrying != ServerRack.None)
            {
                return Loc.T("room.hint.carrying",
                    ServerRackCatalog.Get(carrying).DisplayName);
            }

            if (building)
            {
                return simulation.State.Warehouse.IsEmpty
                    ? Loc.T("room.hint.buy")
                    : Loc.T("room.hint.take");
            }

            return Loc.T("room.hint.idle");
        }

        // ---- the rail: the shop, then what the company already owns -------------------------------

        private VisualElement BuildRail(CompanySimulation simulation)
        {
            var rail = new VisualElement();
            rail.AddToClassList("roombuild");

            var head = new VisualElement();
            head.AddToClassList("roombuild__head");

            var title = new Label(Loc.T(building ? "room.build.on" : "room.build.off"));
            title.AddToClassList("roombuild__title");
            head.Add(title);

            var toggle = new Button(() =>
            {
                building = !building;

                if (!building)
                {
                    Release(simulation);
                }

                changed?.Invoke();
            })
            { text = Loc.T(building ? "room.build.close" : "room.build.open") };

            toggle.AddToClassList("chip");
            head.Add(toggle);

            rail.Add(head);

            var free = new Label(Loc.T("room.squares_free",
                simulation.State.Hall.FreeSquares, simulation.State.Hall.SquareCount));

            free.AddToClassList("roombuild__hint");
            rail.Add(free);

            if (!building)
            {
                return rail;
            }

            // **A scroller, because the rail can grow and a growing column in UI Toolkit deforms
            // rather than overflowing.** Four cabinets plus however many kinds are in the store
            // room already exceed the window on a short screen, and without this the store room
            // is silently squeezed onto the shop, which is what the first render of this rail
            // showed. Same floor the creator pages have had since they were built.
            var scroller = ScrollMemory.Keep(new ScrollView(), "room.build");
            scroller.AddToClassList("roombuild__scroll");

            scroller.Add(SectionHeading(Loc.T("room.build.silicon")));
            scroller.Add(BuildSilicon(simulation));

            scroller.Add(SectionHeading(Loc.T("room.build.shop")));

            foreach (var definition in ServerRackCatalog.All)
            {
                scroller.Add(ShopRow(simulation, definition));
            }

            scroller.Add(SectionHeading(Loc.T("room.build.store")));
            scroller.Add(BuildStoreRoom(simulation));

            rail.Add(scroller);
            return rail;
        }

        private static Label SectionHeading(string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("roombuild__section");
            return heading;
        }

        /// <summary>
        /// One cabinet for sale.
        ///
        /// **Buying takes it straight onto the cursor**, which is the flow the author asked for and
        /// also the honest one: nobody buys a rack in order to own a rack. Under the hood it is
        /// still two calls, so a purchase that cannot be placed is money spent on something sitting
        /// in the store room rather than money that vanished.
        /// </summary>
        /// <summary>How many accelerators a click buys, matching the batch the fleet screen sells.</summary>
        private const int SiliconBatch = 64;

        /// <summary>
        /// What is in the cabinets, and where the next card comes from.
        ///
        /// **The room could not say why it was empty.** Standing a cabinet on the floor does nothing
        /// visible until the company owns silicon to put in it, and silicon was bought two screens
        /// away with no mention of it here. A room whose central mechanic depends on a purchase it
        /// never names is a room that reads as broken.
        ///
        /// The tier gate is printed in the tier's own words rather than being re-derived, so the
        /// sentence here and the refusal from <see cref="CompanySimulation.TryBuyHardware"/> cannot
        /// drift apart.
        /// </summary>
        private VisualElement BuildSilicon(CompanySimulation simulation)
        {
            var state = simulation.State;
            var hall = state.Hall;
            var panel = new VisualElement();
            panel.AddToClassList("roombuild__silicon");

            var owned = 0;
            foreach (var asset in state.Pool.Assets)
            {
                if (asset.IsOnline(state.Date))
                {
                    owned += asset.Units;
                }
            }

            panel.Add(UiParts.StatLine(Loc.T("room.silicon.owned"), owned.ToString()));
            panel.Add(UiParts.StatLine(Loc.T("room.silicon.housed"), hall.HousedAccelerators.ToString()));

            panel.Add(UiParts.StatLine(Loc.T("room.silicon.free"),
                Math.Max(0, hall.TotalSlots - hall.HousedAccelerators).ToString()));

            // A floor with no cabinets on it has no slots, and telling that player to buy cards
            // would be sending them to spend money on something with nowhere to go.
            if (hall.TotalSlots == 0)
            {
                var noRacks = new Label(Loc.T("room.silicon.no_racks"));
                noRacks.AddToClassList("roombuild__hint");
                panel.Add(noRacks);

                return panel;
            }

            const ComputeTier tier = ComputeTier.ColocatedServers;

            if (!ComputeTierCatalog.TryGet(tier, out var definition))
            {
                return panel;
            }

            var status = definition.Evaluate(
                state.Date, state.CashUsd, state.ReleasedModelCount, state.LifetimeRevenueUsd);

            if (!status.IsUnlocked)
            {
                var locked = new Label(status.LockReason);
                locked.AddToClassList("roombuild__hint");
                panel.Add(locked);

                return panel;
            }

            // Accelerators only.  also carries host CPUs, node memory and
            // fabric, and none of those goes in a rack slot; offering them here would be offering
            // the player parts the room cannot hold.
            var newest = new List<HardwareGeneration>();

            foreach (var generation in HardwareCatalog.All)
            {
                if (generation.Class == HardwareClass.Accelerator && generation.IsAvailableOn(state.Date))
                {
                    newest.Add(generation);
                }
            }

            // Newest first. In 2027 the top of a catalog ordered by release date is a 2022 card, and
            // a shop that opens on five year old silicon is a shop nobody reads twice.
            newest.Sort((left, right) => right.ReleaseDate.DayIndex.CompareTo(left.ReleaseDate.DayIndex));

            // Two of them, as a shortcut to the newest silicon, and a door to the rest. The rail
            // is 306 pixels wide and there are twenty two generations: reading them here was
            // never going to work, and until the shop existed there was nowhere else to read them
            // from the room at all.
            for (var index = 0; index < newest.Count && index < 2; index++)
            {
                panel.Add(SiliconRow(simulation, newest[index], tier));
            }

            var browse = new Button(() =>
            {
                shopOpen = true;
                changed?.Invoke();
            })
            {
                text = Loc.T("shop.open")
            };

            browse.AddToClassList("roombuild__open");
            panel.Add(browse);

            return panel;
        }

        /// <summary>
        /// One generation, buyable from the room.
        ///
        /// The purchase is <see cref="CompanySimulation.TryBuyHardware"/>, unchanged and uncopied.
        /// This screen adds a door, not a rule: the price, the tier gate, the scarcity and the
        /// founder's discount are all decided where they already were, and a refusal here refuses
        /// for the same reason it would on the fleet screen.
        /// </summary>
        private VisualElement SiliconRow(
            CompanySimulation simulation, HardwareGeneration generation, ComputeTier tier)
        {
            // **The whole card used to be the button.** Sixty four accelerators left the account
            // on one click anywhere on it, including the part a player clicks to read the figures,
            // and a tester bought about a thousand of them without meaning to. The card is a card;
            // the purchase is a button on it with the price written on the button.
            var card = new VisualElement();
            card.AddToClassList("roombuild__card");

            var name = new Label(generation.DisplayName.ToUpperInvariant());
            name.AddToClassList("roombuild__name");
            card.Add(name);

            card.Add(UiParts.StatLine(Loc.T("room.silicon.batch"), SiliconBatch.ToString()));

            // Heat for the whole batch, because that is what decides whether it fits the cabinets
            // already on the floor. Same unit the rack shop prints its cooling in, one rail apart.
            card.Add(UiParts.StatLine(Loc.T("rack.cooling"),
                UiFormat.Kilowatts(generation.PowerKilowatts * SiliconBatch)));

            if (generation.IsProjection)
            {
                var projected = new Label(Loc.T("room.silicon.projected"));
                projected.AddToClassList("roombuild__hint");
                card.Add(projected);
            }

            var tierDefinition = ComputeTierCatalog.Get(tier);

            var price = MarketModel.PurchasePricePerUnitUsd(
                    generation, tierDefinition, MarketModel.ScarcityOn(simulation.State.Date))
                * simulation.State.Founder.HardwarePriceMultiplier
                * simulation.State.Home.HardwarePriceMultiplier;

            var total = (long)System.Math.Round(price * SiliconBatch);

            var buy = new Button(() =>
            {
                if (!simulation.TryBuyHardware(generation.Id, SiliconBatch, tier, out var why))
                {
                    AudioDirector.Deny();
                    problem = why;
                    changed?.Invoke();
                    return;
                }

                AudioDirector.Confirm();

                announce?.Invoke(Loc.T("room.bought.title"),
                    Loc.T("room.bought.note", SiliconBatch, generation.DisplayName,
                        UiFormat.Money(total), tierDefinition.LeadTimeDays));

                changed?.Invoke();
            })
            {
                text = UiFormat.Money(total)
            };

            buy.AddToClassList("roombuild__buy");
            buy.SetEnabled(simulation.State.CashUsd >= total);
            card.Add(buy);

            card.tooltip = generation.DisplayName + ". " + Loc.T("room.silicon.buy_note");

            return card;
        }

        private VisualElement ShopRow(CompanySimulation simulation, ServerRackDefinition definition)
        {
            var affordable = simulation.State.CashUsd >= definition.PriceUsd;

            var card = new Button(() =>
            {
                if (!simulation.TryBuyRack(definition.Id, out _))
                {
                    return;
                }

                carrying = definition.Id;
                liftedFrom = null;

                // A walkthrough step may be waiting on exactly this. Unconditional, and
                // ignored when nothing is running, so this screen never has to ask
                // whether a tutorial is on.
                GuideOverlay.Reached?.Invoke("walk_room_buy");

                // Buying puts it straight on the cursor, so the step about carrying it is over in
                // the same instant. Reported separately rather than folded into the step above,
                // because a player who picks a cabinet back up off the floor is also carrying one.
                GuideOverlay.Reached?.Invoke("walk_room_carry");

                changed?.Invoke();
            });

            card.AddToClassList("roombuild__card");
            card.SetEnabled(affordable);

            var face = new RackFace();
            face.AddToClassList("roombuild__face");
            face.Show(definition.Id, null);
            card.Add(face);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("roombuild__name");
            card.Add(name);

            // Three figures, not four. Slots and cooling are the trade the four cabinets exist
            // for and price is what stops it being free; upkeep is a real cost and it is the one
            // a player weighs after choosing rather than while choosing, so it moves to the
            // tooltip. A rail 306px wide has room for the decision or for the whole datasheet.
            card.Add(UiParts.StatLine(Loc.T("rack.slots"), definition.Slots.ToString()));

            card.Add(UiParts.StatLine(Loc.T("rack.cooling"),
                UiFormat.Kilowatts(definition.CoolingCapacityKilowatts)));

            card.Add(UiParts.StatLine(Loc.T("rack.price"), UiFormat.Money(definition.PriceUsd)));

            card.tooltip = Loc.T("rack.upkeep") + ": "
                + UiFormat.Money(definition.MonthlyUpkeepUsd) + "\n" + definition.Note;

            return card;
        }

        /// <summary>
        /// What the company owns and has not stood up.
        ///
        /// Empty most of the time, and it says so rather than being absent: a store room that only
        /// appears once there is something in it is a store room nobody knows exists, and the
        /// player needs to know where a cabinet goes when they put it down.
        /// </summary>
        private VisualElement BuildStoreRoom(CompanySimulation simulation)
        {
            var store = simulation.State.Warehouse;
            var panel = new VisualElement();

            if (store.IsEmpty)
            {
                var empty = new Label(Loc.T("room.store.empty"));
                empty.AddToClassList("roombuild__hint");
                panel.Add(empty);

                return panel;
            }

            foreach (var kind in store.Kinds())
            {
                var definition = ServerRackCatalog.Get(kind);
                var held = store.CountOf(kind);

                var row = new Button(() =>
                {
                    carrying = kind;
                    liftedFrom = null;

                    changed?.Invoke();
                });

                row.AddToClassList("roombuild__card");
                row.AddToClassList("roombuild__card--held");

                var name = new Label(definition.DisplayName.ToUpperInvariant());
                name.AddToClassList("roombuild__name");
                row.Add(name);

                var count = new Label($"x{held}");
                count.AddToClassList("roombuild__count");
                row.Add(count);

                panel.Add(row);

                // **The sell button is its own control, outside the row.** A row that both takes a
                // cabinet onto the cursor and sells it would be one misclick from turning a
                // $240,000 tank into $132,000, and this project has already shipped seven
                // mechanisms nobody could reach by being cautious in the wrong place.
                var sell = new Button(() =>
                {
                    if (simulation.TrySellRack(kind, out _))
                    {
                        changed?.Invoke();
                    }
                })
                {
                    text = Loc.T("room.store.sell",
                        UiFormat.Money(CompanySimulation.RackResaleUsd(kind)))
                };

                sell.AddToClassList("chip");
                sell.AddToClassList("roombuild__sell");
                panel.Add(sell);
            }

            if (store.Fans > 0)
            {
                var fans = new Label(Loc.T("room.store.fans", store.Fans));
                fans.AddToClassList("roombuild__hint");
                panel.Add(fans);

                // **`TrySellFan` had no caller when this screen was first written**, which made it
                // the eighth mechanism in this project complete in the simulation, green under
                // test, and impossible for a player to reach. The sweep that found it is eight
                // lines of regex over public mutators in `Simulation/` with no name matched in
                // `UI/`, and it has now paid for itself eight times.
                var sellFan = new Button(() =>
                {
                    if (simulation.TrySellFan())
                    {
                        changed?.Invoke();
                    }
                })
                {
                    text = Loc.T("room.store.sell",
                        UiFormat.Money((long)(ServerRackCatalog.FanPriceUsd
                                              * CompanySimulation.RackResaleFraction)))
                };

                sellFan.AddToClassList("chip");
                sellFan.AddToClassList("roombuild__sell");
                panel.Add(sellFan);
            }

            var worth = new Label(Loc.T("room.store.worth", UiFormat.Money(store.ValueUsd)));
            worth.AddToClassList("roombuild__hint");
            panel.Add(worth);

            return panel;
        }

        // ---- before there is a room ---------------------------------------------------------------

        /// <summary>
        /// What the screen is before there is a room, which is most of a first campaign.
        ///
        /// It has to sell the idea rather than report its absence: renting is a slider and this is
        /// the first thing in the game the company physically owns.
        /// </summary>
        private VisualElement BuildLocked(CompanySimulation simulation)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("emptystate");

            var heading = new Label(Loc.T("room.locked.title"));
            heading.AddToClassList("emptystate__title");
            panel.Add(heading);

            var body = new Label(Loc.T("room.locked.body"));
            body.AddToClassList("emptystate__body");
            panel.Add(body);

            // **Asked, rather than guessed at.** This used to enable the button on cash alone while
            // the operation also required the colocation tier, so a player with the seventy
            // thousand pressed a live button and nothing happened, with the reason thrown away into
            // a discarded `out`. One question, one answer, and the answer is on screen.
            var open = simulation.CanOpenServerRoom(out var why);

            var buy = new Button(() =>
            {
                if (simulation.TryOpenServerRoom(false, out var refused))
                {
                    changed?.Invoke();
                    return;
                }

                // A guard rather than a state a player can produce, since the button is disabled
                // whenever this would fail. It exists because the last version of this line
                // discarded the reason and that is precisely how the fault stayed invisible.
                problem = refused;
                changed?.Invoke();
            })
            {
                text = Loc.T("room.locked.buy",
                    UiFormat.Money(CompanySimulation.BasementPriceUsd))
            };

            buy.AddToClassList("button");
            buy.AddToClassList("button--primary");
            buy.AddToClassList("emptystate__go");
            buy.SetEnabled(open);
            panel.Add(buy);

            if (!open)
            {
                var blocked = new Label(why);
                blocked.AddToClassList("field__hint");
                blocked.AddToClassList("emptystate__blocked");
                panel.Add(blocked);
            }

            if (!string.IsNullOrEmpty(problem))
            {
                var refusedLabel = new Label(problem);
                refusedLabel.AddToClassList("field__hint");
                refusedLabel.AddToClassList("emptystate__blocked");
                panel.Add(refusedLabel);
            }

            var gift = new Label(Loc.T("room.locked.gift"));
            gift.AddToClassList("field__hint");
            panel.Add(gift);

            return panel;
        }
    }
}
