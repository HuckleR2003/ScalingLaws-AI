using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The room the office camera is pointed at, and what is standing in it.
    ///
    /// **Why this is a runtime swap rather than three scenes.** The game scene is generated, its
    /// contents are fixed, and the office camera in it is aimed at a room that is already there. A
    /// second and third room baked into the same file would both be loaded whichever one is rented,
    /// and regenerating that scene per tier would change a prefab count the project checks before
    /// every commit. So the garage stays where it is, the two hubs live under Resources, and moving
    /// office means hiding one room and loading another under the same camera.
    ///
    /// The camera itself is a child of the baked room, which is convenient rather than accidental:
    /// it is already at the right height and angle, and the hub prefabs were laid out for that
    /// angle. Only its distance and orthographic size change.
    /// </summary>
    public sealed class OfficeStage
    {
        /// <summary>What the builder called the group a placed piece goes into.</summary>
        public const string FurnitureGroup = "Furniture";

        /// <summary>How far the camera pulls back. Far enough that nothing clips the near plane.</summary>
        public const float CameraPullback = 30f;

        private readonly Transform anchor;
        private readonly Camera camera;
        private readonly Transform bakedRoom;

        /// <summary>The name of the group every room builder writes its walking points into.</summary>
        public const string WaypointGroup = "Waypoints";

        /// <summary>
        /// The group the founder and the staff are spawned into.
        ///
        /// Named here as well as on <see cref="FounderPresence"/> because this class has to know
        /// what is not furniture, and it is reached from the room rather than by a scene-wide
        /// search.
        /// </summary>
        public const string PeopleGroup = FounderPresence.StaffGroup;

        /// <summary>
        /// The walking points of the room that is actually on screen.
        ///
        /// **The founder was standing in the air**, and this is why. `OfficeActor` found its points
        /// with `GameObject.Find("Waypoints")`, which searches the whole scene and answers with
        /// whichever it reaches first. The house is never destroyed when the company moves, only
        /// its geometry is hidden, so its group is still there: a company renting a floor was
        /// walking the house's points, and the house keeps `Bed` and `UpstairsDesk` on a mezzanine
        /// three metres up. A rented floor has no mezzanine, so the founder sat down in mid air at
        /// the height the bedroom used to be.
        ///
        /// Null until a room is shown, which is what the actor treats as "stay where you are".
        /// </summary>
        public Transform WaypointRoot
        {
            get
            {
                var room = loadedRoom != null ? loadedRoom.transform : bakedRoom;
                return room == null ? null : room.Find(WaypointGroup);
            }
        }

        private GameObject loadedRoom;
        private OfficeTier? shownTier;

        /// <summary>The room on screen, kept so the dressing knows what the room already has.</summary>
        private RoomView shownView;

        /// <summary>
        /// Everything standing in the office that is not a child of the room transform.
        ///
        /// **The author furnished the game scene by hand and not all of it landed under one
        /// parent.** Hiding the baked room walks the renderers under that transform, which is
        /// most of the house and not the sofa, the nightstand, the vases or the gamepad. Those
        /// stayed lit after a move and were drawn through the new floor: two offices in one
        /// frame, and the first thing a player sees on the day they move out.
        ///
        /// Collected by where they stand rather than by name or by parent, because the thing
        /// they have in common is that they are in the room. Collected once, before anything is
        /// spawned, so the founder, the staff, the racks and the furniture the player buys are
        /// never in this list: those belong to whichever room is current and are handled by it.
        /// </summary>
        private List<MeshRenderer> bakedExtras;

        /// <summary>
        /// Binds to whatever the game scene already has.
        ///
        /// Everything is optional. A shell running in a test, or in a scene that was never
        /// generated, gets a stage that does nothing rather than a null reference on the first
        /// frame the office is shown.
        /// </summary>
        public OfficeStage(GameObject officeRoom)
        {
            if (officeRoom == null)
            {
                return;
            }

            bakedRoom = officeRoom.transform;
            anchor = bakedRoom.parent;
            camera = officeRoom.GetComponentInChildren<Camera>(true);
        }

        public bool IsLive => bakedRoom != null;

        /// <summary>
        /// The camera the room is drawn through, so a click can be turned into a ray.
        ///
        /// Exposed rather than kept private because the picking lives in the screen: the stage owns
        /// the geometry and the screen owns what a click means, which is the same split the basement
        /// already uses.
        /// </summary>
        public Camera View => camera;

        /// <summary>What the camera renders into, needed to undo the crop a click travelled through.</summary>
        public Texture Texture => camera != null ? camera.targetTexture : null;

        /// <summary>
        /// Where a floor slot lands in the camera's view, 0 to 1 from the bottom left.
        ///
        /// **Computed from the room's transform, not from the camera.** A slot is a position in
        /// metres inside the room, and the room is a real object in a real scene: going through its
        /// transform means a room moved or rotated in the editor takes its floor with it, and the
        /// build mode does not have to know it happened.
        /// </summary>
        public bool ViewportOfSlot(float x, float z, out Vector2 point)
        {
            point = default;

            var room = CurrentRoom;

            if (room == null || camera == null)
            {
                return false;
            }

            var world = room.TransformPoint(new Vector3(x, 0f, z));
            var view = camera.WorldToViewportPoint(world);

            // Behind the camera reads as a valid point with a negative depth, which would put a
            // marker on screen for a slot nobody can see.
            if (view.z <= 0f)
            {
                return false;
            }

            point = new Vector2(view.x, view.y);
            return true;
        }

        /// <summary>
        /// Which point on the floor a click landed on, in room-local metres.
        ///
        /// A ray into the room's own floor plane. The caller turns that into a slot, because the
        /// slot grid belongs to the plan and this class has no business knowing how the floor is
        /// divided.
        /// </summary>
        public bool FloorPointAt(Vector2 viewport, out float x, out float z)
        {
            x = 0f;
            z = 0f;

            var room = CurrentRoom;

            if (room == null || camera == null)
            {
                return false;
            }

            var ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var floor = new Plane(room.up, room.position);

            if (!floor.Raycast(ray, out var distance))
            {
                return false;
            }

            var local = room.InverseTransformPoint(ray.GetPoint(distance));

            x = local.x;
            z = local.z;

            return true;
        }

        /// <summary>Which room is on screen. Null until the first call to <see cref="Show"/>.</summary>
        public OfficeTier? ShownTier => shownTier;

        /// <summary>The room currently being looked at, baked or loaded.</summary>
        public Transform CurrentRoom =>
            loadedRoom != null ? loadedRoom.transform : bakedRoom;

        /// <summary>
        /// Puts the right room under the camera and stands the furniture up in it.
        ///
        /// Cheap to call every time the office screen opens: it returns immediately when the tier
        /// has not changed, and the furniture is rebuilt on its own because a piece can be bought
        /// without the lease changing.
        /// </summary>
        public void Show(OfficeTier tier, DecorPlan decor, string companyName = null)
        {
            if (!IsLive)
            {
                return;
            }

            if (shownTier != tier)
            {
                SwapRoom(tier);
                shownTier = tier;
                shownName = null;
            }

            // The name over the entrance. Written when it changes rather than every call, because
            // fitting it measures glyphs and the office is shown on every day's repaint.
            if (loadedRoom != null && companyName != shownName)
            {
                CompanySignAnchor.ApplyAll(loadedRoom, companyName);
                shownName = companyName;
            }

            // The tier's own desks, read from the catalog the hiring cap reads. One source, so the
            // room can never show a different number of desks than the company is allowed to fill.
            deskCount = OfficeCatalog.Get(tier).Desks;

            Dress(decor);
        }

        /// <summary>The name last written on the sign, so an unchanged one is not written again.</summary>
        private string shownName;

        private void SwapRoom(OfficeTier tier)
        {
            var view = RoomCatalog.For(tier);
            shownView = view;

            // **Before anything is instantiated, and this line is the whole fix.**
            //
            // `Extras()` sweeps every renderer in the scene that is not part of the house and
            // stands inside the house's own box, and it remembers the answer forever. A loaded
            // room is instantiated at exactly the house's position, so every piece of it is inside
            // that box and none of it is a child of the house: if the sweep runs for the first
            // time with a room already loaded, it collects that room as somebody else's furniture
            // and hides it.
            //
            // A new campaign never hit it. It opens in the garage, which is not a loaded room, so
            // the sweep ran with nothing to eat and the answer was cached before the company ever
            // moved. **Loading a save into an office hit it every time**, because the first room
            // ever shown was a loaded one. Reported as the office simply not being there.
            Extras();

            if (loadedRoom != null)
            {
                Object.Destroy(loadedRoom);
                loadedRoom = null;
            }

            if (view.IsLoaded)
            {
                var prefab = Resources.Load<GameObject>(view.ResourcePath);
                if (prefab != null)
                {
                    loadedRoom = Object.Instantiate(prefab, anchor);
                    loadedRoom.name = view.ResourcePath;

                    // Same spot the garage sits in: far below the interface camera, so neither
                    // camera can ever see the other's geometry.
                    loadedRoom.transform.position = bakedRoom.position;
                    loadedRoom.transform.rotation = bakedRoom.rotation;
                }
            }

            // The baked room's own geometry is hidden rather than destroyed, because the camera and
            // the key light are its children and destroying it would take the office view with it.
            var showBaked = loadedRoom == null;

            SetGeometryVisible(bakedRoom, showBaked);

            foreach (var stray in Extras())
            {
                if (stray != null)
                {
                    stray.enabled = showBaked;
                }
            }

            Frame(view);
        }

        /// <summary>
        /// The hand-placed things standing in the office, found once and remembered.
        ///
        /// The box is the baked room's own extent, grown by two metres so a piece pushed
        /// slightly through a wall still counts. Anything outside it is somebody else's scene:
        /// the city, the basement, the menu.
        /// </summary>
        private List<MeshRenderer> Extras()
        {
            if (bakedExtras != null)
            {
                return bakedExtras;
            }

            bakedExtras = new List<MeshRenderer>();

            if (bakedRoom == null)
            {
                return bakedExtras;
            }

            var own = bakedRoom.GetComponentsInChildren<MeshRenderer>(true);
            var room = new Bounds(bakedRoom.position, Vector3.one * 12f);

            foreach (var renderer in own)
            {
                room.Encapsulate(renderer.bounds);
            }

            room.Expand(2f);

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || renderer.transform.IsChildOf(bakedRoom))
                {
                    continue;
                }

                // **Never the room the company is standing in.** The call above is ordered so this
                // cannot happen, and this is the second belt: a loaded room sits at the house's
                // own position, so it is inside the box and it is not part of the house, which is
                // exactly the shape of a stray. One reordering of `SwapRoom` would otherwise hide
                // the whole office again.
                if (loadedRoom != null && renderer.transform.IsChildOf(loadedRoom.transform))
                {
                    continue;
                }

                // **And never a person.** Same reasoning as `SetGeometryVisible`: a name plate is
                // a `MeshRenderer` standing exactly where somebody is standing, which is inside
                // the room by definition. A founder spawned before the first room swap would
                // otherwise be collected here and switched off with the furniture.
                if (renderer.GetComponentInParent<OfficeActor>() != null
                    || renderer.GetComponentInParent<NamePlate>() != null)
                {
                    continue;
                }

                if (room.Intersects(renderer.bounds))
                {
                    bakedExtras.Add(renderer);
                }
            }

            return bakedExtras;
        }

        /// <summary>
        /// Hides a room's meshes while leaving its cameras, its lights and its people alone.
        ///
        /// Renderers rather than the GameObject, for exactly that reason: switching the garage off
        /// wholesale would switch off the camera that is rendering the office.
        ///
        /// **And never the people.** Reported as the founder losing their name plate the moment
        /// the company moved into a rented floor. `Staff` is a child of the house, the plate is
        /// two `TextMesh` objects and a quad, and a `TextMesh` carries a `MeshRenderer`, so hiding
        /// the house's geometry switched the name off. The founder's own body survived because a
        /// character is a `SkinnedMeshRenderer`, which this sweep does not touch, so what the
        /// player saw was a person with no name rather than no person. The staff kept theirs only
        /// by accident of timing: they are respawned whenever the roster changes, which is after
        /// the move.
        /// </summary>
        private static void SetGeometryVisible(Transform room, bool visible)
        {
            if (room == null)
            {
                return;
            }

            var people = room.Find(PeopleGroup);

            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (people != null && renderer.transform.IsChildOf(people))
                {
                    continue;
                }

                renderer.enabled = visible;
            }
        }

        private void Frame(RoomView view)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographicSize = view.CameraSize;

            var floor = CurrentRoom != null ? CurrentRoom.position : Vector3.zero;
            var focus = floor + new Vector3(view.FocusX, 3f, view.FocusZ);

            // The angle is left exactly as the scene builder set it. Every placement in every room
            // was laid out for it and a nudge here moves all of them.
            camera.transform.position = focus - camera.transform.forward * CameraPullback;
        }

        /// <summary>
        /// Rebuilds the placed furniture from scratch.
        ///
        /// Cleared and refilled rather than diffed. The list is a dozen boxes on a screen that is
        /// already rendering a room, and a diff would be code that can disagree with the plan.
        /// </summary>
        /// <summary>
        /// How far apart the room's own desks stand, in metres.
        ///
        /// Wider than the furniture grid, because a desk is a place somebody sits rather than a
        /// thing standing against a wall, and two people at arm's length read as a call centre.
        /// </summary>
        public const float DeskSpacing = 2.4f;

        /// <summary>Desks per row before the block steps back. The camera looks along z.</summary>
        public const int DesksPerRow = 5;

        /// <summary>What a desk in the room is called, so the clear can match on it.</summary>
        public const string DeskName = "TierDesk";

        /// <summary>
        /// Stands the desks the office tier itself pays for.
        ///
        /// **Not furniture, and that distinction is the whole reason this is here rather than in the
        /// plan.** Every tier carries a desk count, it is what caps hiring, the rent pays for it, and
        /// until now nothing drew it: LVL 1 said ten desks over an empty floor, which reads as an
        /// office nobody has furnished. These cannot be bought, moved or sold, they add nothing to
        /// `ExtraDesks`, and no number in the game moves because they exist. They are what the office
        /// is, the same way its walls are.
        /// </summary>
        /// <param name="desks">What the lease pays for, before the room is looked at.</param>
        private void StandTierDesks(int desks)
        {
            var room = CurrentRoom;

            // **Only the ones the room has not already built, which today is none of them.**
            // `RoomView.FixedDesks` records how many desks the builder actually put on that
            // floor, and the lease pays for exactly that many, so this stood a second full set
            // of ten on the small floor and twenty on the big one. They were not even in the
            // room: the block is laid out from the origin backwards, which is off the floor
            // entirely, so a company that moved out of the house got a row of white boxes
            // standing in the dark beside its new office. Two sources for one fact, again.
            var extra = Mathf.Max(0, desks - shownView.FixedDesks);

            if (room == null || extra <= 0)
            {
                return;
            }

            desks = extra;

            var group = room.Find(FurnitureGroup);

            if (group == null)
            {
                return;
            }

            for (var index = 0; index < desks; index++)
            {
                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = DeskName + index;
                desk.transform.SetParent(group, false);

                // Inside the room rather than behind its origin. A tier that promises more desks
                // than its own room builds is the only way to get here, and standing them off the
                // floor is what made that state look like a rendering fault rather than a
                // mismatched number.
                desk.transform.localPosition = new Vector3(
                    2.0f + index % DesksPerRow * DeskSpacing,
                    0.36f,
                    1.6f + index / DesksPerRow * DeskSpacing);

                desk.transform.localScale = new Vector3(1.5f, 0.72f, 0.75f);

                // A collider here would eat the click meant for whoever is sitting at it, and the
                // room has exactly one interaction.
                var collider = desk.GetComponent<BoxCollider>();

                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                var renderer = desk.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterial = MaterialFor("desk");
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private void Dress(DecorPlan decor)
        {
            var room = CurrentRoom;
            if (room == null)
            {
                return;
            }

            var group = room.Find(FurnitureGroup);
            if (group == null)
            {
                var made = new GameObject(FurnitureGroup);
                made.transform.SetParent(room, false);
                group = made.transform;
            }

            for (var index = group.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(group.GetChild(index).gameObject);
            }

            if (decor == null)
            {
                return;
            }

            foreach (var item in decor.Placed)
            {
                Stand(group, item);
            }

            StandTierDesks(deskCount);
        }

        /// <summary>
        /// How many desks the tier pays for, remembered between dresses.
        ///
        /// A field rather than a parameter on `Dress`, because `Dress` is called from `Show` with the
        /// decor and the tier's desks are a property of the tier `Show` was already given.
        /// </summary>
        private int deskCount;

        private static void Stand(Transform group, DecorItem item)
        {
            var piece = item.Definition;

            // **The model, when there is one.** Every piece used to be drawn as a box in its
            // colour, which the author reported as strange blocks at the front of the office once
            // the rebuilt rooms put the shop's floor where the camera looks. `DecorModelBuilder`
            // writes one prefab per piece under Resources; the box below is what a piece without
            // one still gets, so nothing bought ever goes missing.
            var model = Resources.Load<GameObject>(DecorFolder + piece.Kind);

            if (model != null)
            {
                var placed = Object.Instantiate(model, group);
                placed.name = piece.DisplayName;
                placed.transform.localPosition = new Vector3(item.X, 0f, item.Z);
                placed.transform.localRotation = Quaternion.identity;
                return;
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = piece.DisplayName;
            box.transform.SetParent(group, false);
            box.transform.localPosition = new Vector3(item.X, piece.SizeY / 2f, item.Z);
            box.transform.localScale = new Vector3(piece.SizeX, piece.SizeY, piece.SizeZ);

            // Nothing walks into it, and a collider on a decoration is a collider the founder's
            // route has to be told about.
            Object.Destroy(box.GetComponent<BoxCollider>());

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(piece.Tint);
        }

        /// <summary>Where the furniture shop's models live under Resources.</summary>
        public const string DecorFolder = "Decor/";

        private static readonly Dictionary<string, Material> Paints = new();

        /// <summary>
        /// One material per colour, shared.
        ///
        /// The shader is looked up rather than named: a URP shader under the built-in pipeline draws
        /// magenta rather than failing, which is a bug that only shows up on screen.
        /// </summary>
        private static Material MaterialFor(string tint)
        {
            if (Paints.TryGetValue(tint, out var cached) && cached != null)
            {
                return cached;
            }

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            shader = shader != null ? shader : Shader.Find("Standard");

            var material = new Material(shader);

            if (ColorUtility.TryParseHtmlString(tint, out var colour))
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", colour);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }
            }

            Paints[tint] = material;
            return material;
        }
    }
}
