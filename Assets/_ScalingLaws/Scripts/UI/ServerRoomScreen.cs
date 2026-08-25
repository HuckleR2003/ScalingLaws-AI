using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The floor, the cabinets standing on it, and the corner that says how they are doing.
    ///
    /// **The grid is the room.** `ServerHall` has modelled a floor of squares since long before
    /// anything drew one, so this screen adds no rules of its own: it draws what the hall says is
    /// there and sends clicks back to it. Every number on the page comes out of the same call the
    /// fleet reads, which is what stops a cabinet looking healthy here and throttling in the books.
    ///
    /// Locked until the company has somewhere to put a rack, and the locked state is a real page
    /// rather than a message, because it is where the player is told the room exists at all.
    /// </summary>
    public sealed class ServerRoomScreen
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        private readonly ServerRoomBanner banner = new();
        private RackEditorPanel editor;

        public ServerRoomScreen(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        /// <summary>The square the player has open, or null when the floor is showing.</summary>
        private (int Column, int Row)? open;

        public VisualElement Build()
        {
            var simulation = company();
            var state = simulation.State;

            var page = new VisualElement();
            page.AddToClassList("content");
            page.AddToClassList("room");

            var title = new Label(Loc.T("room.title"));
            title.AddToClassList("page-title");
            page.Add(title);

            if (!state.HasServerRoom)
            {
                page.Add(BuildLocked(simulation));
                return page;
            }

            var strap = new Label(Loc.T("room.strap"));
            strap.AddToClassList("page-subtitle");
            page.Add(strap);

            // The corner, over everything. Absolute, so opening a cabinet does not move it.
            banner.Refresh(simulation);
            page.Add(banner.Root);

            page.Add(BuildFloor(simulation));

            if (open.HasValue)
            {
                editor ??= new RackEditorPanel(company, () =>
                {
                    changed?.Invoke();
                });

                editor.Close = () =>
                {
                    open = null;
                    changed?.Invoke();
                };

                page.Add(editor.Build(open.Value.Column, open.Value.Row));
            }

            return page;
        }

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

            var buy = new Button(() =>
            {
                if (simulation.TryOpenServerRoom(false, out _))
                {
                    changed?.Invoke();
                }
            })
            {
                text = Loc.T("room.locked.buy",
                    UiFormat.Money(CompanySimulation.BasementPriceUsd))
            };

            buy.AddToClassList("button");
            buy.AddToClassList("button--primary");
            buy.AddToClassList("emptystate__go");
            buy.SetEnabled(simulation.State.CashUsd >= CompanySimulation.BasementPriceUsd);
            panel.Add(buy);

            var gift = new Label(Loc.T("room.locked.gift"));
            gift.AddToClassList("field__hint");
            panel.Add(gift);

            return panel;
        }

        private VisualElement BuildFloor(CompanySimulation simulation)
        {
            var hall = simulation.State.Hall;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("room.floor"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var note = new Label(Loc.T("room.squares_free", hall.FreeSquares, hall.SquareCount));
            note.AddToClassList("field__hint");
            panel.Add(note);

            var grid = new VisualElement();
            grid.AddToClassList("room__grid");

            var known = HardwareCatalog.TryGet(simulation.Market.RentableGeneration, out var part);

            for (var row = 0; row < hall.Rows; row++)
            {
                var line = new VisualElement();
                line.AddToClassList("room__row");

                for (var column = 0; column < hall.Columns; column++)
                {
                    line.Add(BuildSquare(simulation, hall.At(column, row), part, known));
                }

                grid.Add(line);
            }

            panel.Add(grid);
            return panel;
        }

        /// <summary>
        /// One square. Empty floor, or a cabinet with a heat edge down its side.
        ///
        /// **The heat is the colour, not a number.** A grid of sixteen figures is a spreadsheet; a
        /// grid where two cabinets are orange is a room you can read in a glance and then click the
        /// orange one.
        /// </summary>
        private VisualElement BuildSquare(CompanySimulation simulation, HallSquare square,
            HardwareGeneration part, bool known)
        {
            var at = (square.Column, square.Row);

            var tile = new Button(() =>
            {
                if (square.IsEmpty)
                {
                    // An empty square offers the cheapest cabinet, because a chooser here would be
                    // a second shop and the rack cards already live on the compute screen.
                    if (simulation.State.CashUsd >= ServerRackCatalog.Get(ServerRack.Enclosed).PriceUsd
                        && simulation.TryPlaceRack(square.Column, square.Row, ServerRack.Enclosed,
                            out _))
                    {
                        changed?.Invoke();
                    }

                    return;
                }

                open = at;
                changed?.Invoke();
            });

            tile.AddToClassList("room__square");
            tile.EnableInClassList("room__square--empty", square.IsEmpty);

            if (square.IsEmpty)
            {
                var plus = new Label("+");
                plus.AddToClassList("room__plus");
                tile.Add(plus);

                return tile;
            }

            var definition = ServerRackCatalog.Get(square.Rack);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("room__name");
            tile.Add(name);

            var fill = new Label($"{square.Accelerators} / {definition.Slots}");
            fill.AddToClassList("room__fill");
            tile.Add(fill);

            if (square.Fans > 0)
            {
                var fans = new Label($"{square.Fans} × {Loc.T("part.fan")}");
                fans.AddToClassList("room__fans");
                tile.Add(fans);
            }

            // The edge carries the heat, in the same four tones the corner banner uses.
            var heat = !known || square.Accelerators <= 0
                ? 0.0
                : square.Accelerators * part.PowerKilowatts
                  / Math.Max(0.1, definition.CoolingCapacityKilowatts
                                  + square.Fans * ServerRackCatalog.FanCoolingKilowatts);

            tile.EnableInClassList("room__square--warm", heat > 0.85 && heat <= 1.0);
            tile.EnableInClassList("room__square--hot", heat > 1.0 && heat <= 1.15);
            tile.EnableInClassList("room__square--cooking", heat > 1.15);

            return tile;
        }
    }
}
