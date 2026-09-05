using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The basement in 3D: the room, the cabinets standing on its squares, and the camera that
    /// draws the lot into a texture the interface can put behind its own controls.
    ///
    /// **The same shape as <see cref="OfficeStage"/>, and deliberately so.** The office already
    /// solved this: a room loaded from Resources, a camera pointed at it, a group the runtime
    /// clears and refills. Inventing a second way to show a room would mean two answers to every
    /// question about lighting, framing and when to rebuild.
    ///
    /// It differs in one thing that matters. The office is a picture of a place; this is a picture
    /// the player clicks on, so the stage also has to answer **where on screen a given square is**.
    /// That is <see cref="ViewportOf"/>, and it is why the room is generated against
    /// <see cref="BasementFloor"/> rather than laid out by hand: the projection is only right if
    /// the marker really is where the data says.
    ///
    /// Everything is optional. A shell running in a test, or before the prefab has been built, gets
    /// a stage that quietly does nothing rather than a null reference on the first frame.
    /// </summary>
    public sealed class BasementStage
    {
        /// <summary>Where the builder writes the room.</summary>
        public const string RoomResource = "Rooms/Basement";

        /// <summary>
        /// Where the builder writes one marker per floor square.
        ///
        /// Cabinets are parented to these markers rather than to a group of their own, which is
        /// what makes a placement one local position. The builder also writes a `Racks` group that
        /// nothing here uses; it is left alone because it is the builder's file, and reading it was
        /// the bug that made the floor never empty.
        /// </summary>
        public const string SquareGroup = "Squares";

        /// <summary>
        /// Far from everything else, and below it.
        ///
        /// The game scene already holds a house and swaps two office floors under one camera at
        /// ground level. A basement dropped in beside them would be visible to their cameras, and
        /// this camera would see their walls. Two hundred metres down is cheaper than layer masks
        /// and impossible to get subtly wrong.
        /// </summary>
        public static readonly Vector3 Anchor = new(0f, -200f, 0f);

        private GameObject room;
        private GameObject ghost;
        private MeshRenderer ghostOutline;
        private MeshRenderer ghostBody;
        private Camera camera;
        private RenderTexture target;

        private readonly Dictionary<(int Column, int Row), Transform> squares = new();

        /// <summary>What the interface draws as the background of the room screen.</summary>
        public RenderTexture Texture => target;

        /// <summary>False when the prefab is missing, which is every test and a fresh clone.</summary>
        public bool IsLive => room != null && camera != null;

        /// <summary>
        /// Builds the room once, then keeps it.
        ///
        /// Called every time the screen opens. It returns immediately when the room is already
        /// standing, because loading a prefab and creating a render texture on every rebuild of a
        /// page that rebuilds itself once a simulated day is a leak with a frame rate attached.
        /// </summary>
        public void Ensure()
        {
            if (room != null)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(RoomResource);

            if (prefab == null)
            {
                return;
            }

            room = Object.Instantiate(prefab);
            room.name = "Basement";
            room.transform.position = Anchor;

            IndexSquares();

            target = new RenderTexture(1280, 720, 24) { name = "BasementView" };

            var cameraObject = new GameObject("BasementCamera");
            cameraObject.transform.SetParent(room.transform, false);

            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = BasementFloor.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f, 1f);
            camera.targetTexture = target;

            // The angle every room in this game is drawn at. Changing it here would make the
            // basement read as a different game, and the grid was laid out for it.
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);

            var focus = Anchor + new Vector3(BasementFloor.FocusX, 1.1f, BasementFloor.FocusZ);
            cameraObject.transform.position = focus - cameraObject.transform.forward * 26f;

            camera.enabled = false;
        }

        /// <summary>Switched on only while the room screen is open. It is a camera, not a screenshot.</summary>
        public void SetVisible(bool visible)
        {
            if (camera != null)
            {
                camera.enabled = visible;
            }
        }

        /// <summary>
        /// Stands the cabinets the hall says are there, coloured by how hard each is working.
        ///
        /// Cleared and refilled rather than diffed, same reasoning the office furniture records: it
        /// is sixteen boxes on a screen that is already rendering a room, and a diff would be code
        /// that can disagree with the floor.
        /// </summary>
        /// <summary>
        /// Whether the occupied squares carry the tutorial's ring.
        ///
        /// Set by the room while the walkthrough is on the step that says "these are the cabinets".
        /// A field rather than an argument because `Dress` is called from several places and only
        /// one of them knows anything about a tutorial.
        /// </summary>
        public bool RingTheCabinets { get; set; }

        public void Dress(ServerHall hall, double kilowattsPerAccelerator,
            RoomUpgrades? upgrades = null)
        {
            ClearRacks();

            if (hall == null)
            {
                return;
            }

            foreach (var square in hall.Occupied())
            {
                if (!squares.TryGetValue((square.Column, square.Row), out var marker))
                {
                    continue;
                }

                Stand(marker, square,
                    hall.HeatAt(square.Column, square.Row, kilowattsPerAccelerator, upgrades),
                    RingTheCabinets);
            }
        }

        /// <summary>
        /// Where a square lands in the camera's view, 0 to 1 from the bottom left.
        ///
        /// **This is how a click on a picture becomes a click on a square.** The interface lays its
        /// own hit targets over the rendered texture at these points rather than raycasting into
        /// it, which keeps the picking in UI Toolkit where the rest of the screen already lives and
        /// works whatever size the element is drawn at.
        ///
        /// Read from the marker's real transform, not recomputed from
        /// <see cref="BasementFloor"/>: if the room in the scene ever disagreed with the data, this
        /// has to follow the room, because the room is what the player is looking at.
        /// </summary>
        public bool ViewportOf(int column, int row, out Vector2 point)
        {
            point = default;

            if (camera == null || !squares.TryGetValue((column, row), out var marker))
            {
                return false;
            }

            var projected = camera.WorldToViewportPoint(marker.position);

            if (projected.z < 0f)
            {
                return false;
            }

            point = new Vector2(projected.x, projected.y);
            return true;
        }

        /// <summary>
        /// Which square the cursor is over, from a point in the camera's view.
        ///
        /// **The inverse of drawing the room, and it has to be exact.** A cursor that highlights
        /// one square while the click lands on its neighbour is the worst kind of fault this
        /// feature can have: nothing errors, the simulation is right, and the player learns not to
        /// trust the room. The camera is orthographic and the floor is a plane, so the answer is a
        /// ray against y = 0 rather than an approximation, and the column comes from the same
        /// <see cref="BasementFloor"/> arithmetic that put the marker there.
        ///
        /// The gap between squares counts as no square. Snapping from the walkway to whichever
        /// tile is nearest would let a player drop a cabinet somewhere they were not pointing.
        /// </summary>
        public bool SquareUnder(Vector2 viewport, out int column, out int row)
        {
            column = -1;
            row = -1;

            if (camera == null)
            {
                return false;
            }

            var ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var floor = new Plane(Vector3.up, new Vector3(0f, Anchor.y, 0f));

            if (!floor.Raycast(ray, out var distance))
            {
                return false;
            }

            var hit = ray.GetPoint(distance) - Anchor;

            // Undo the grid's origin, then see how far into a tile the point falls. A remainder
            // past the tile's own width is a point in the walkway between two of them.
            var alongX = hit.x - BasementFloor.Margin;
            var alongZ = hit.z - BasementFloor.Margin;

            if (alongX < 0f || alongZ < 0f)
            {
                return false;
            }

            var candidateColumn = Mathf.FloorToInt(alongX / BasementFloor.Pitch);
            var candidateRow = Mathf.FloorToInt(alongZ / BasementFloor.Pitch);

            if (!BasementFloor.Contains(candidateColumn, candidateRow))
            {
                return false;
            }

            if (alongX - candidateColumn * BasementFloor.Pitch > BasementFloor.SquareSize
                || alongZ - candidateRow * BasementFloor.Pitch > BasementFloor.SquareSize)
            {
                return false;
            }

            column = candidateColumn;
            row = candidateRow;
            return true;
        }

        /// <summary>
        /// Puts the cabinet being carried down on a square, without committing anything.
        ///
        /// **The thing on the cursor is drawn in the room rather than beside the pointer.** A
        /// sprite following the mouse tells the player where their hand is, which they already
        /// know. Standing the cabinet on the square it would occupy answers the question they
        /// actually have, which is whether it fits there and what the room looks like with it.
        ///
        /// <paramref name="allowed"/> false paints the outline red instead of refusing to draw:
        /// showing the player the square they cannot use, and why it is lit differently, beats a
        /// ghost that silently disappears over occupied ground.
        /// </summary>
        public void ShowGhost(ServerRack rack, int column, int row, bool allowed)
        {
            if (!squares.TryGetValue((column, row), out var marker))
            {
                HideGhost();
                return;
            }

            // **Built once and then moved, never rebuilt.** This runs on every pointer move, and
            // `Object.Destroy` only takes effect at the end of the frame: tearing the ghost down
            // and standing a new one up each time would draw both for a frame, sixty times a
            // second, which reads as flicker rather than as a cursor.
            EnsureGhost();

            ghost.transform.SetParent(marker, false);
            ghost.transform.localPosition = Vector3.zero;
            ghost.SetActive(true);

            ghostOutline.sharedMaterial = Paint(
                allowed ? "ghost-ok" : "ghost-no",
                allowed ? new Color(0.42f, 0.78f, 0.58f) : new Color(0.78f, 0.30f, 0.28f));

            var showBody = rack != ServerRack.None && allowed;
            ghostBody.gameObject.SetActive(showBody);

            if (!showBody)
            {
                return;
            }

            ghostBody.transform.localPosition = new Vector3(0f, RackHeight(rack) / 2f, 0f);
            ghostBody.transform.localScale = RackScale(rack);
        }

        private void EnsureGhost()
        {
            if (ghost != null)
            {
                return;
            }

            ghost = new GameObject("Ghost");

            var outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outline.name = "Outline";
            outline.transform.SetParent(ghost.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0.03f, 0f);

            outline.transform.localScale =
                new Vector3(BasementFloor.SquareSize, 0.04f, BasementFloor.SquareSize);

            Object.Destroy(outline.GetComponent<BoxCollider>());
            ghostOutline = outline.GetComponent<MeshRenderer>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "GhostRack";
            body.transform.SetParent(ghost.transform, false);

            Object.Destroy(body.GetComponent<BoxCollider>());
            ghostBody = body.GetComponent<MeshRenderer>();

            ghostBody.sharedMaterial = Paint("ghost-body", new Color(0.46f, 0.62f, 0.72f));
        }

        public void HideGhost()
        {
            if (ghost != null)
            {
                ghost.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
                target = null;
            }

            if (ghost != null)
            {
                Object.Destroy(ghost);
                ghost = null;
                ghostOutline = null;
                ghostBody = null;
            }

            if (room != null)
            {
                Object.Destroy(room);
                room = null;
            }

            squares.Clear();
            camera = null;
        }

        // ---- the room's own furniture ------------------------------------------------------------

        private void IndexSquares()
        {
            squares.Clear();

            var group = room.transform.Find(SquareGroup);

            if (group == null)
            {
                return;
            }

            for (var row = 0; row < BasementFloor.Rows; row++)
            {
                for (var column = 0; column < BasementFloor.Columns; column++)
                {
                    var marker = group.Find(BasementFloor.MarkerName(column, row));

                    if (marker != null)
                    {
                        squares[(column, row)] = marker;
                    }
                }
            }
        }

        /// <summary>
        /// One cabinet: a box on the square, and a lit strip down the front carrying the heat.
        ///
        /// **The colour is on a separate sliver rather than on the cabinet itself.** A whole rack
        /// tinted red reads as a red rack, and the player has four kinds of cabinet to tell apart
        /// by sight. The body keeps the material that says which kind it is; the strip says how it
        /// is doing, which is the same division the floor tiles use.
        /// </summary>
        /// <summary>
        /// How tall a cabinet stands, from how much it holds.
        ///
        /// Public and static so the editor's snapshot tool draws the same box the game does. A
        /// preview with its own idea of the shape is a preview that stops being evidence.
        /// </summary>
        public static float RackHeight(ServerRack rack) =>
            Mathf.Lerp(1.1f, 1.95f, Mathf.InverseLerp(4f, 28f, ServerRackCatalog.Get(rack).Slots));

        /// <inheritdoc cref="RackHeight"/>
        public static Vector3 RackScale(ServerRack rack) =>
            new(BasementFloor.SquareSize * 0.56f, RackHeight(rack),
                BasementFloor.SquareSize * 0.78f);

        /// <summary>
        /// Takes every cabinet off the floor.
        ///
        /// **Walks the square markers, because that is where cabinets live.** `Stand` parents each
        /// one to its marker, which is what makes its placement a single local position. The clear
        /// this replaced destroyed the children of a `Racks` group that nothing is ever parented
        /// to, so it destroyed nothing: every cabinet ever stood stayed on the floor for the life
        /// of the scene, and since the room re-dresses on repaint they also stacked on their own
        /// squares dozens deep.
        ///
        /// The visible symptom was a cabinet left behind after a move, on a square the hall
        /// correctly reported as empty, so it refused every click. The picture and the simulation
        /// disagreed and only the picture was wrong.
        ///
        /// **Detached before destroyed.** `Object.Destroy` is deferred to the end of the frame, so
        /// a second dress in the same frame would find them still parented and stand duplicates
        /// over the top.
        /// </summary>
        private void ClearRacks()
        {
            foreach (var marker in squares.Values)
            {
                if (marker == null)
                {
                    continue;
                }

                for (var index = marker.childCount - 1; index >= 0; index--)
                {
                    var child = marker.GetChild(index);

                    if (child == null || !child.name.StartsWith(RackPrefix, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    child.SetParent(null, false);
                    Object.Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// What a cabinet in the scene is called.
        ///
        /// The clear matches on it, so it is a constant rather than a literal in two places: a
        /// rename in one of them is a floor that never empties, which is the bug this replaced.
        /// </summary>
        public const string RackPrefix = "Rack_";

        private static void Stand(Transform marker, HallSquare square,
            ServerRackCatalog.RackHeat heat, bool ring = false)
        {
            // **The tutorial's ring, drawn in the room because the floor has no element to class.**
            // A plate under the cabinet rather than an outline around it: the camera looks down at
            // thirty degrees, so the sides of a box are mostly hidden by the box and the floor
            // beneath it is the part that is always in view.
            if (ring)
            {
                var halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                halo.name = "WalkRing";
                halo.transform.SetParent(marker, false);
                halo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                halo.transform.localScale = new Vector3(1.45f, 0.04f, 1.45f);

                Object.Destroy(halo.GetComponent<BoxCollider>());
                halo.GetComponent<MeshRenderer>().sharedMaterial =
                    Paint("walk-ring", new Color(0.86f, 0.44f, 0.36f));
            }

            // Taller cabinets for more slots, within what the ceiling allows. The immersion tank is
            // the exception in the catalog and it reads as one on the floor.
            var height = RackHeight(square.Rack);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = $"{RackPrefix}{square.Column}_{square.Row}";
            body.transform.SetParent(marker, false);
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            body.transform.localScale = RackScale(square.Rack);

            Object.Destroy(body.GetComponent<BoxCollider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = BodyPaint(square.Rack);

            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Heat";
            strip.transform.SetParent(body.transform, false);

            // On the two faces the camera can see, at 30/-45. A strip on the back is a strip
            // nobody ever looks at.
            strip.transform.localPosition = new Vector3(0.52f, 0.04f, 0f);
            strip.transform.localScale = new Vector3(0.06f, 0.72f, 0.5f);

            Object.Destroy(strip.GetComponent<BoxCollider>());
            strip.GetComponent<MeshRenderer>().sharedMaterial = HeatPaint(heat);
        }

        private static readonly Dictionary<string, Material> Paints = new();

        private static Material BodyPaint(ServerRack rack) =>
            Paint("rack-" + rack, rack switch
            {
                ServerRack.OpenFrame => new Color(0.32f, 0.33f, 0.36f),
                ServerRack.Enclosed => new Color(0.20f, 0.21f, 0.24f),
                ServerRack.HighDensity => new Color(0.17f, 0.20f, 0.26f),
                ServerRack.Immersion => new Color(0.14f, 0.22f, 0.24f),
                _ => new Color(0.22f, 0.22f, 0.24f)
            });

        /// <summary>
        /// Green, amber, red, and the two red states share a colour on the floor.
        ///
        /// The player reads the room at a glance and opens whichever cabinet is red. Which kind of
        /// red it is belongs in the panel that opens, where there is room to say what to do about
        /// it.
        /// </summary>
        private static Material HeatPaint(ServerRackCatalog.RackHeat heat) =>
            Paint("heat-" + heat, heat switch
            {
                ServerRackCatalog.RackHeat.Warm => new Color(0.89f, 0.75f, 0.27f),
                ServerRackCatalog.RackHeat.Throttling => new Color(0.91f, 0.55f, 0.24f),
                ServerRackCatalog.RackHeat.Cooking => new Color(0.85f, 0.31f, 0.29f),
                _ => new Color(0.49f, 0.78f, 0.60f)
            });

        /// <summary>
        /// One material per colour, shared and cached.
        ///
        /// The shader is looked up rather than named, for the reason recorded three times in this
        /// project already: a URP shader under the built-in pipeline draws magenta rather than
        /// failing, and that only ever shows up on screen.
        /// </summary>
        private static Material Paint(string key, Color colour)
        {
            if (Paints.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            shader = shader != null ? shader : Shader.Find("Standard");

            var material = new Material(shader) { name = key };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", colour);
            }

            Paints[key] = material;
            return material;
        }
    }
}
