using System;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The world, drawn from real outlines.
    ///
    /// **This replaced six hand-typed blobs.** The old version was about seventy coordinates that
    /// approximated the continents, and it was honest about being a control for picking one of three
    /// places rather than a map. It also looked like six hand-typed blobs, which the author put
    /// plainly: the simple shapes were the loudest wrong thing on the screen a new player meets
    /// second.
    ///
    /// Now it is Natural Earth at 1:110m, Robinson projection, baked to a flat binary by
    /// `Tools/bake_world_map.py` and read by <see cref="WorldShapes"/>. Public domain, 43 kB, 177
    /// countries.
    ///
    /// **Dark countries, thick white borders, and nothing else.** No graticule, no labels painted
    /// into the map, no ocean texture. The map sits underneath a decision and everything drawn on it
    /// competes with the four numbers that decision is actually about. The colour is carried entirely
    /// by which countries are lit.
    ///
    /// Hit-testing is point-in-polygon against the same rings that are drawn, rather than boxes laid
    /// over the top. Boxes were right for six blobs and would be absurd for 177 countries, and the
    /// cursor has to agree with the picture or clicking Portugal selects Spain.
    /// </summary>
    public sealed class WorldMapElement : VisualElement
    {
        /// <summary>Border width in pixels. Thick on purpose: the borders are the drawing.</summary>
        public const float BorderWidth = 1.6f;

        /// <summary>And thicker still around whatever is chosen, so the eye finds it at a glance.</summary>
        public const float ChosenBorderWidth = 3.2f;

        /// <summary>How large a country marker is, as a share of the map's width.</summary>
        public const float PinRadius = 0.005f;

        /// <summary>
        /// Below this drawn area, a country gets a marker.
        ///
        /// **A rule rather than a list of small countries.** The first pass marked all sixteen and
        /// the six in Europe covered western Europe with white discs, hiding the outlines they were
        /// pointing at. A marker is for the places a cursor genuinely cannot find - Switzerland is
        /// 24 points, Singapore has no outline in the source at all - and France is not one of them.
        ///
        /// Measured off the same rings that are drawn, in map space where the whole world is 1 wide,
        /// so a finer re-bake moves the countries and not this number.
        ///
        /// **The value is where the data has a gap, not where it felt right.** Sorted by drawn area
        /// the sixteen run Taiwan 0.000023, Switzerland 0.000043, Ireland 0.000056, Singapore
        /// 0.000065, South Korea 0.000086, then the United Kingdom at 0.000239 - a jump of nearly
        /// three. Those five are a dozen pixels across at the size this draws and the rest are not.
        /// The first attempt at this constant was 0.00035, which caught eight of the sixteen
        /// including Germany and Japan, and put back most of the clutter the rule exists to remove.
        /// </summary>
        public const float NeedsAMarkerBelow = 0.00015f;

        private static readonly Color Water = new(0.043f, 0.055f, 0.078f);

        /// <summary>
        /// Land outside the chosen region. Nearly the ocean; present, not offered.
        ///
        /// **The four fills have to be four steps, not four shades.** The first pass put them inside
        /// about eight per cent of one another, which is a range that reads as one grey with noise
        /// in it: the render came back with no visible difference between "your region" and
        /// "everywhere else", which is the only thing this map's colour is for.
        /// </summary>
        private static readonly Color Quiet = new(0.085f, 0.098f, 0.122f);

        /// <summary>Land inside the chosen region.</summary>
        private static readonly Color Neighbourhood = new(0.215f, 0.245f, 0.300f);

        /// <summary>One of the sixteen, inside the chosen region.</summary>
        private static readonly Color Offered = new(0.345f, 0.390f, 0.470f);

        private static readonly Color Hovered = new(0.500f, 0.560f, 0.650f);
        private static readonly Color Chosen = new(0.470f, 0.815f, 0.610f);

        private static readonly Color Border = new(1f, 1f, 1f, 0.88f);
        private static readonly Color ChosenBorder = new(0.80f, 1f, 0.90f, 1f);

        private readonly Action<WorldRegion> onRegion;
        private readonly Action<Country> onCountry;

        private WorldRegion region;
        private Country country;
        private Country hovered;

        private readonly Label caption;

        public WorldMapElement(WorldRegion currentRegion, Country currentCountry,
            Action<WorldRegion> onRegion, Action<Country> onCountry)
        {
            this.onRegion = onRegion;
            this.onCountry = onCountry;

            region = currentRegion;
            country = currentCountry;

            AddToClassList("world-map");
            generateVisualContent += Draw;

            // The name of whatever the cursor is over, in the corner. Not painted into the map: a
            // label drawn at a country's centroid is unreadable at this size for anywhere smaller
            // than Brazil, and 177 of them is a wall of text over a picture.
            caption = new Label(string.Empty);
            caption.AddToClassList("world-map__caption");
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);

            RegisterCallback<MouseMoveEvent>(OnMove);
            RegisterCallback<MouseOutEvent>(_ => SetHover(Country.None, string.Empty));
            RegisterCallback<MouseDownEvent>(OnDown);
        }

        /// <summary>Points the map at a region, without telling anybody: the caller already knows.</summary>
        public void Select(WorldRegion pickedRegion, Country pickedCountry)
        {
            region = pickedRegion;
            country = pickedCountry;

            MarkDirtyRepaint();
        }

        // ---- what the cursor is over ---------------------------------------------------------------

        /// <summary>
        /// Pointer position to map space.
        ///
        /// The map is letterboxed inside whatever box the layout gives it, so this is the inverse of
        /// the same fit <see cref="Draw"/> uses. **One method, called by both**, because two copies
        /// of this arithmetic is how a click lands one country away from the one under the cursor,
        /// and nothing would report it: the picture right, the selection wrong, every test green.
        /// </summary>
        private bool ToMap(Vector2 local, out Vector2 point)
        {
            point = default;

            var rect = contentRect;

            if (rect.width <= 1f || rect.height <= 1f || float.IsNaN(rect.width))
            {
                return false;
            }

            var scale = Fit(rect, out var offset);

            point = new Vector2((local.x - offset.x) / scale, (local.y - offset.y) / scale);

            // Against the whole map rather than the view: leaning in crops the picture and a click
            // in the letterboxed margin is still a click on the world behind it.
            return point.x >= 0f && point.x <= 1f
                && point.y >= 0f && point.y <= WorldShapes.Aspect;
        }

        /// <summary>
        /// How much room is left around a region when the map leans in on it.
        ///
        /// A tenth of the region's own size on each side. Tight enough that the region fills the
        /// frame and loose enough that a country on its edge is not against the glass.
        /// </summary>
        public const float ZoomPadding = 0.55f;

        /// <summary>
        /// What part of the map is on screen, in map space.
        ///
        /// The whole world until a region is chosen, then that region's own bounds. **A rectangle
        /// rather than a scale and an offset**, because every other thing this element does is in
        /// map space - the outlines, the markers and the hit test all read the same coordinates -
        /// and a second way of saying where we are looking is how a click lands one country from
        /// the cursor with the picture right and every test green.
        /// </summary>
        public Rect View
        {
            get
            {
                if (region == WorldRegion.None)
                {
                    return new Rect(0f, 0f, 1f, WorldShapes.Aspect);
                }

                var lowX = float.MaxValue;
                var lowY = float.MaxValue;
                var highX = float.MinValue;
                var highY = float.MinValue;

                foreach (var shape in WorldShapes.All)
                {
                    // **The countries that can be picked, not every country in the region.** Natural
                    // Earth files Russia under Europe, so a box around everything the source calls
                    // European runs from Ireland to the Bering Strait and leaning in on it barely
                    // changes the picture. The point of leaning in is to make the sixteen big enough
                    // to click, and the padding above is generous so their neighbours still frame
                    // them.
                    if (shape.Region != region || shape.Member == Country.None)
                    {
                        continue;
                    }

                    foreach (var ring in shape.Rings)
                    {
                        foreach (var point in ring)
                        {
                            lowX = Mathf.Min(lowX, point.x);
                            lowY = Mathf.Min(lowY, point.y);
                            highX = Mathf.Max(highX, point.x);
                            highY = Mathf.Max(highY, point.y);
                        }
                    }
                }

                if (highX <= lowX || highY <= lowY)
                {
                    return new Rect(0f, 0f, 1f, WorldShapes.Aspect);
                }

                var padX = (highX - lowX) * ZoomPadding;
                var padY = (highY - lowY) * ZoomPadding;

                return new Rect(lowX - padX, lowY - padY,
                    highX - lowX + padX * 2f, highY - lowY + padY * 2f);
            }
        }

        /// <summary>
        /// How many pixels one unit of map space is, and where the view's origin sits.
        ///
        /// Fits the view rectangle rather than the whole map, so leaning in on a region is one
        /// change in one place and the drawing, the markers and the cursor all follow it.
        /// </summary>
        private float Fit(Rect rect, out Vector2 offset)
        {
            var view = View;

            var scale = Mathf.Min(rect.width / view.width, rect.height / view.height);

            offset = new Vector2(
                (rect.width - view.width * scale) * 0.5f - view.x * scale,
                (rect.height - view.height * scale) * 0.5f - view.y * scale);

            return scale;
        }

        private void OnMove(MouseMoveEvent move)
        {
            if (!ToMap(move.localMousePosition, out var point))
            {
                SetHover(Country.None, string.Empty);
                return;
            }

            foreach (var shape in WorldShapes.All)
            {
                if (shape.Region == WorldRegion.None || !shape.Contains(point))
                {
                    continue;
                }

                SetHover(shape.Member, shape.Name);
                return;
            }

            SetHover(Country.None, string.Empty);
        }

        private void SetHover(Country over, string name)
        {
            if (hovered == over && caption.text == name)
            {
                return;
            }

            hovered = over;
            caption.text = name;
            caption.EnableInClassList("world-map__caption--on", !string.IsNullOrEmpty(name));

            MarkDirtyRepaint();
        }

        /// <summary>
        /// A click picks a country when one is under it, and otherwise picks a region.
        ///
        /// **Both, from one click, and the order matters.** The author asked for region first and
        /// then a country, which is the right shape for the decision: three places to compare, then
        /// one to commit to. But making the map region-only would mean the country is pickable from
        /// the list and not from the map, and a map you cannot click the answer on is a picture.
        ///
        /// So clicking Spain selects Europe, and clicking Poland selects Europe *and* Poland. Nobody
        /// has to know which countries are which before they click.
        /// </summary>
        private void OnDown(MouseDownEvent down)
        {
            // **Right button steps back out**, which is the only way back to the three regions once
            // the map has leaned in on one. Handled before the hit test, because backing out of a
            // region is not a click on anything.
            if (down.button == 1)
            {
                if (region != WorldRegion.None)
                {
                    region = WorldRegion.None;
                    country = Country.None;

                    onRegion?.Invoke(WorldRegion.None);
                    MarkDirtyRepaint();
                }

                down.StopPropagation();
                return;
            }

            if (down.button != 0 || !ToMap(down.localMousePosition, out var point))
            {
                return;
            }

            foreach (var shape in WorldShapes.All)
            {
                if (shape.Region == WorldRegion.None || !shape.Contains(point))
                {
                    continue;
                }

                // **Exactly one callback per click.** Both handlers rebuild the whole creator page,
                // so raising the region and then the country would rebuild once with the country
                // cleared and once with it set, and the first rebuild has already thrown away this
                // element. Picking a country carries its region with it, which is what
                // `PickCountry` does, so there is nothing to say twice.
                //
                // **And which of the two a click means depends on how far in the map is.** From the
                // whole world it means the region, and the map leans in on it; once it has leaned
                // in, it means the country. Choosing a country off a world map means hunting for
                // Switzerland at four pixels across, which is what this replaced.
                if (region == WorldRegion.None)
                {
                    region = shape.Region;
                    country = Country.None;
                    onRegion?.Invoke(shape.Region);
                }
                else if (shape.Region != region)
                {
                    // A click outside the region we are looking at is a change of mind about the
                    // region, not a country in another one.
                    region = shape.Region;
                    country = Country.None;
                    onRegion?.Invoke(shape.Region);
                }
                else if (shape.Member != Country.None)
                {
                    country = shape.Member;
                    onCountry?.Invoke(shape.Member);
                }

                MarkDirtyRepaint();
                down.StopPropagation();
                return;
            }
        }

        // ---- drawing -------------------------------------------------------------------------------

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;

            if (rect.width <= 1f || rect.height <= 1f || float.IsNaN(rect.width))
            {
                return;
            }

            var painter = context.painter2D;

            painter.fillColor = Water;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2(rect.width, 0f));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0f, rect.height));
            painter.ClosePath();
            painter.Fill();

            var scale = Fit(rect, out var offset);

            // **Two passes, and the second is the whole reason the map reads.** Drawing each country
            // filled and stroked in one go lets a later neighbour's fill paint over the border that
            // was just drawn, so borders come out broken along whichever edge happened to be drawn
            // first. Every fill first, then every border, and the white is continuous.
            foreach (var shape in WorldShapes.All)
            {
                painter.fillColor = FillFor(shape);
                Trace(painter, shape, scale, offset);
                painter.Fill();
            }

            painter.strokeColor = Border;
            painter.lineWidth = BorderWidth;

            foreach (var shape in WorldShapes.All)
            {
                if (shape.Member != Country.None && shape.Member == country)
                {
                    continue;
                }

                Trace(painter, shape, scale, offset);
                painter.Stroke();
            }

            // The chosen country's outline last and heavier, so it is never cut into by a neighbour.
            if (country != Country.None && WorldShapes.For(country) is { } picked)
            {
                painter.strokeColor = ChosenBorder;
                painter.lineWidth = ChosenBorderWidth;
                Trace(painter, picked, scale, offset);
                painter.Stroke();
            }

            DrawPins(painter, scale, offset);
        }

        private Color FillFor(WorldShapes.Shape shape)
        {
            if (shape.Member != Country.None && shape.Member == country)
            {
                return Chosen;
            }

            if (shape.Member != Country.None && shape.Member == hovered)
            {
                return Hovered;
            }

            if (shape.Region == WorldRegion.None)
            {
                return Quiet;
            }

            // Before a region is chosen every region is offered, so everything is lit rather than
            // everything being dim. A map that starts dark says the choice has already been made.
            if (region == WorldRegion.None)
            {
                return shape.Member != Country.None ? Offered : Neighbourhood;
            }

            if (shape.Region != region)
            {
                return Quiet;
            }

            return shape.Member != Country.None ? Offered : Neighbourhood;
        }

        private static void Trace(Painter2D painter, WorldShapes.Shape shape, float scale,
            Vector2 offset)
        {
            painter.BeginPath();

            foreach (var ring in shape.Rings)
            {
                if (ring.Length < 3)
                {
                    continue;
                }

                painter.MoveTo(ring[0] * scale + offset);

                for (var index = 1; index < ring.Length; index++)
                {
                    painter.LineTo(ring[index] * scale + offset);
                }

                painter.ClosePath();
            }
        }

        /// <summary>
        /// A marker on every country that can be picked, in the region that is chosen.
        ///
        /// **Only in the chosen region, and that is a decluttering decision rather than a rule.**
        /// Six of the sixteen are in Europe and at this size their markers overlap into one blob.
        /// Showing the four or six that are currently relevant says "these are your options here"
        /// with no legend and no text.
        ///
        /// Switzerland and Singapore are the reason markers exist at all: one is 24 points and the
        /// other has no outline in the source at this resolution, so neither is a target a cursor can
        /// find. The marker is not the hit area, though - the country's own shape is - so the marker
        /// can stay small.
        /// </summary>
        private void DrawPins(Painter2D painter, float scale, Vector2 offset)
        {
            if (region == WorldRegion.None)
            {
                return;
            }

            foreach (var shape in WorldShapes.All)
            {
                if (shape.Member == Country.None || shape.Region != region)
                {
                    continue;
                }

                // The chosen one always gets its marker, however large it is: that is the map
                // saying where you are, which is a different job from saying where you can click.
                if (shape.Member != country && shape.DrawnArea >= NeedsAMarkerBelow)
                {
                    continue;
                }

                var at = shape.Pin * scale + offset;
                var radius = PinRadius * scale;

                painter.fillColor = shape.Member == country ? ChosenBorder : Border;
                painter.strokeColor = Water;
                painter.lineWidth = 1.5f;

                painter.BeginPath();
                painter.Arc(at, radius, 0f, 360f);
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }
    }
}
