using ScalingLaws.Core;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Drives the overview camera in the city map scene: WASD and the arrows pan the ground, "="
    /// and "-" dolly the camera along its own forward vector to zoom, and ESC hands the scene back
    /// to <see cref="SceneFlow.ReturnFromCityMap"/>.
    ///
    /// **Panning is relative to the camera's own flattened basis, not to world X/Z.** The camera
    /// sits at a fixed 36-degree pitch — <see cref="Editor.CityDressingBuilder.BuildCamera"/> never
    /// rotates it, there is no orbit — so "forward" only ever means one thing: away from the
    /// camera, projected onto the ground. Reading world axes directly would still work today, by
    /// coincidence of what that one fixed angle happens to point along, and break the moment
    /// anybody changes it.
    ///
    /// **The key-reading and the world-math are split on purpose**, the same way
    /// <see cref="KeyboardShortcuts"/> splits `Resolve`/`ResolveScroll` from the `Input` calls that
    /// feed them: the panel of eight keys collapsing to one direction, and the clamping, are the
    /// part worth getting right in a test that does not need a live scene. Reading the keyboard is
    /// not.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityMapController : MonoBehaviour
    {
        /// <summary>Ground units a second at <see cref="ReferenceHeight"/>. Faster once zoomed out, see <see cref="HeightFactor"/>.</summary>
        public const float PanUnitsPerSecond = 500f;

        /// <summary>World units a second the camera dollies along its own forward vector while zooming.</summary>
        public const float ZoomUnitsPerSecond = 900f;

        /// <summary>The height <see cref="PanUnitsPerSecond"/> is measured at. Panning scales from here.</summary>
        public const float ReferenceHeight = 900f;

        /// <summary>Never below this fraction of the base pan speed, even pressed right up against the terrain.</summary>
        public const float MinHeightFactor = 0.35f;

        /// <summary>Never above this multiple of the base pan speed, however far out the player zooms.</summary>
        public const float MaxHeightFactor = 3.5f;

        /// <summary>Closest the camera may dolly in. Below this a wide shot starts clipping through real geometry.</summary>
        public const float MinHeight = 100f;

        /// <summary>
        /// Furthest out. A little past the builder's own opening height (2050) so a player who
        /// zooms out to get their bearings is not stopped just short of the view that greeted them.
        /// </summary>
        public const float MaxHeight = 3200f;

        /// <summary>
        /// How far past the coastline panning still goes. Bayview is not a rectangle — a hard stop
        /// at exactly <see cref="CityLayout.Size"/> would clip the view at an angle wherever the
        /// shoreline curves inside that square, mid-district.
        /// </summary>
        public const float PanMargin = 400f;

        /// <summary>How quickly a district jump closes the distance each second. Higher eases out faster.</summary>
        public const float FlyToRate = 2.4f;

        /// <summary>Close enough that snapping the rest of the way is not a visible jump.</summary>
        public const float FlyToArrivalDistance = 2f;

        /// <summary>
        /// How long the opening pull-back takes, in seconds. The author asked for three to four.
        /// </summary>
        public const float OpeningSeconds = 3.4f;

        /// <summary>
        /// How far above its own street the camera starts, before it pulls back. Close enough that
        /// the founder's house fills the shot and the roofs either side of it are separate houses.
        /// </summary>
        public const float OpeningHeightAboveGround = 165f;

        private Camera cam;
        private Vector3? flyTarget;
        private Vector3 openingFrom;
        private Vector3 openingTo;
        private float openingSeconds;
        private bool opening;

        /// <summary>What the office had its shadows set to, put back when the map closes.</summary>
        private float shadowDistanceBefore;
        private int shadowCascadesBefore;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>
        /// The map opens on the founder's own house and pulls back off it to the overview.
        ///
        /// **It starts where the player just was.** Leaving the office is otherwise a cut to a city
        /// from two kilometres up, which says nothing about where the company is standing in it. The
        /// house is a real building on this map, so beginning the shot there and travelling out is
        /// the one move that answers "where am I" without a label saying so.
        ///
        /// The scene's authored camera position is the destination, read here rather than restated,
        /// so moving the opening view in the builder moves the end of this flight with it.
        /// </summary>
        private void Start()
        {
            openingTo = transform.position;
            openingFrom = OpeningFrom(CityLayout.FounderHome,
                CityLayout.GroundHeightAt(CityLayout.FounderHome), transform.forward);

            transform.position = openingFrom;
            openingSeconds = 0f;
            opening = true;

            // **Shadows have to reach as far as the camera can see.** The quality settings stop
            // drawing them at 150 m, which is a setting written for a room: from map height the city
            // lay flat and shadows only appeared once the player had zoomed right in, which is what
            // the author reported. The distance follows the camera instead, and the office's own
            // setting is put back on the way out, because 150 m is right for a room.
            shadowDistanceBefore = QualitySettings.shadowDistance;
            shadowCascadesBefore = QualitySettings.shadowCascades;
            QualitySettings.shadowCascades = 4;
        }

        private void OnDisable()
        {
            if (shadowDistanceBefore <= 0f)
            {
                return;
            }

            QualitySettings.shadowDistance = shadowDistanceBefore;
            QualitySettings.shadowCascades = shadowCascadesBefore;
        }

        /// <summary>
        /// How far shadows are drawn from a camera at this height: far enough to cover what is in
        /// frame, and no further, because every extra metre is spread over the same shadow map and
        /// makes every shadow in it softer.
        /// </summary>
        public static float ShadowDistanceFor(float cameraHeight) =>
            Mathf.Clamp(cameraHeight * 2.2f, 220f, 2600f);

        /// <summary>
        /// Where the opening shot begins: low over `target`, on the camera's own fixed look angle,
        /// so the house is in the middle of the frame rather than under the camera.
        /// </summary>
        public static Vector3 OpeningFrom(MapPoint target, float targetGroundHeight, Vector3 forward)
        {
            var height = Mathf.Max(MinHeight, targetGroundHeight + OpeningHeightAboveGround);
            var ground = CameraGroundFor(new Vector2(target.X, target.Z), targetGroundHeight,
                height, forward);

            return new Vector3(ground.x, height, ground.y);
        }

        /// <summary>
        /// How far along the pull-back the camera is at a given moment, nought to one.
        ///
        /// **Fast first, settling at the end.** The author's words were that the map drops away
        /// suddenly and then arrives; an even travel reads as a lift rather than as leaving, and an
        /// ease-in reads as the camera being dragged.
        /// </summary>
        public static float OpeningEase(float seconds)
        {
            var t = Mathf.Clamp01(seconds / OpeningSeconds);
            var left = 1f - t;

            return 1f - left * left * left;
        }

        /// <summary>
        /// Starts an eased flight to a spot on the ground, keeping the current height and the
        /// fixed look angle — the same "district" jump the map's quick-travel panel offers.
        ///
        /// Takes the target's own ground height rather than assuming one, because
        /// <see cref="Data.CityLayout.Districts"/> are levelled to different heights and aiming
        /// from a fixed guess would centre every district except the one whose height happened to
        /// match it.
        /// </summary>
        public void FlyTo(Vector2 groundTarget, float targetGroundHeight)
        {
            FlyTo(groundTarget, targetGroundHeight, transform.position.y);
        }

        /// <summary>
        /// The same flight, ending at a chosen height rather than keeping the one the camera is at.
        ///
        /// What the legend's SHOW button needs: a player who has zoomed out to the whole city and
        /// then asks to be shown a place would otherwise be flown to it and still be looking at the
        /// whole city, which is a flight that answers nothing.
        /// </summary>
        public void FlyTo(Vector2 groundTarget, float targetGroundHeight, float cameraHeight)
        {
            var height = Mathf.Clamp(cameraHeight, MinHeight, MaxHeight);
            var ground = CameraGroundFor(groundTarget, targetGroundHeight, height, transform.forward);

            // Asking to be taken somewhere ends the opening shot. Without this the pull-back would
            // go on running underneath the flight and the two would fight for the same camera.
            opening = false;
            flyTarget = new Vector3(ground.x, height, ground.y);
        }

        /// <summary>
        /// Where the camera's own ground point has to be so that, looking along `forward` from
        /// `cameraHeight`, the fixed angle lands exactly on `target` — the maths behind
        /// <see cref="FlyTo"/>, pulled out because it is worth checking without a live camera.
        ///
        /// A straight teleport to the target's own XZ would put the district under the camera
        /// rather than under where the camera is looking, which at this pitch is well short of
        /// directly below — the district would land in the corner of the frame, not the centre.
        /// </summary>
        public static Vector2 CameraGroundFor(Vector2 target, float targetHeight,
            float cameraHeight, Vector3 forward)
        {
            var drop = -forward.y;
            var reach = drop > 0.001f ? (cameraHeight - targetHeight) / drop : 0f;

            return new Vector2(target.x - forward.x * reach, target.y - forward.z * reach);
        }

        private void Update()
        {
            // Shadows reach as far as this height needs; see the note in `Start`.
            QualitySettings.shadowDistance = ShadowDistanceFor(transform.position.y);

            // M both opens the map and closes it, so the key the office answers is the key the map
            // answers. ESC stays: it is what every other panel in the game closes with.
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
            {
                SceneFlow.ReturnFromCityMap();
                return;
            }

            var deltaSeconds = Time.unscaledDeltaTime;

            var panAxis = ResolvePan(
                Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));

            var zoom = ResolveZoom(
                Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus),
                Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus));

            // The wheel is how a map is zoomed everywhere else, and a player reaches for it before
            // they read any hint. It is a distance a notch rather than a speed a second: the keys
            // are held and the wheel is not.
            var wheel = ScrollDolly(Input.mouseScrollDelta.y);

            // Any of the three is the player taking the camera back, mid-flight or not. That
            // includes the opening shot: a player reaching for the keys has finished watching it,
            // and a camera that goes on travelling under their hands is a camera that is broken.
            if (panAxis != Vector2.zero || zoom != 0f || wheel != 0f)
            {
                flyTarget = null;
                opening = false;
            }

            if (opening)
            {
                openingSeconds += deltaSeconds;

                transform.position = Vector3.Lerp(openingFrom, openingTo, OpeningEase(openingSeconds));

                if (openingSeconds >= OpeningSeconds)
                {
                    transform.position = openingTo;
                    opening = false;
                }

                return;
            }

            if (flyTarget.HasValue)
            {
                var target = flyTarget.Value;

                if (Vector3.Distance(transform.position, target) <= FlyToArrivalDistance)
                {
                    transform.position = target;
                    flyTarget = null;
                }
                else
                {
                    var t = 1f - Mathf.Exp(-FlyToRate * deltaSeconds);
                    transform.position = Vector3.Lerp(transform.position, target, t);
                }

                return;
            }

            if (panAxis != Vector2.zero)
            {
                var forward = FlattenToGround(transform.forward);
                var right = FlattenToGround(transform.right);
                var world = forward * panAxis.y + right * panAxis.x;

                var factor = HeightFactor(transform.position.y);
                var moved = transform.position + world * (PanUnitsPerSecond * factor * deltaSeconds);

                var bounded = ClampToMap(new Vector2(moved.x, moved.z));
                transform.position = new Vector3(bounded.x, moved.y, bounded.y);
            }

            if (zoom != 0f)
            {
                var candidate = transform.position + transform.forward * (zoom * ZoomUnitsPerSecond * deltaSeconds);

                if (candidate.y >= MinHeight && candidate.y <= MaxHeight)
                {
                    transform.position = candidate;
                }
            }

            if (wheel != 0f)
            {
                // Clamped rather than refused, unlike the keys above: a wheel notch is a whole jump
                // and dropping it at the limits would make the last notch before the floor do
                // nothing at all. It stops exactly at the limit instead.
                var distance = ClampDolly(transform.position.y, transform.forward.y, wheel);
                transform.position += transform.forward * distance;
            }
        }

        /// <summary>World units the camera dollies for one notch of the wheel.</summary>
        public const float ZoomUnitsPerNotch = 280f;

        /// <summary>
        /// Notches, however the platform reports them, turned into a distance along the camera's
        /// forward vector. Positive zooms in.
        ///
        /// **Capped at three notches a frame.** A trackpad's flick arrives as one enormous delta on
        /// a single frame, and without the cap that one frame takes the camera from the whole city
        /// down to the pavement.
        /// </summary>
        public static float ScrollDolly(float notches) =>
            Mathf.Clamp(notches, -3f, 3f) * ZoomUnitsPerNotch;

        /// <summary>
        /// The same distance, shortened so the camera lands on <see cref="MinHeight"/> or
        /// <see cref="MaxHeight"/> instead of going through it. Zero once it is against a limit and
        /// pushing further that way.
        /// </summary>
        public static float ClampDolly(float cameraHeight, float forwardY, float distance)
        {
            if (Mathf.Abs(forwardY) < 0.0001f || distance == 0f)
            {
                return 0f;
            }

            var height = cameraHeight + forwardY * distance;

            if (height < MinHeight)
            {
                return (MinHeight - cameraHeight) / forwardY;
            }

            return height > MaxHeight ? (MaxHeight - cameraHeight) / forwardY : distance;
        }

        /// <summary>
        /// Eight keys collapsed to one ground-plane direction, in the camera's own forward/right —
        /// x is right, y is forward. Zero when nothing is pressed or opposite keys cancel out.
        /// </summary>
        public static Vector2 ResolvePan(bool forward, bool back, bool left, bool right)
        {
            var axis = new Vector2(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                (forward ? 1f : 0f) - (back ? 1f : 0f));

            return axis == Vector2.zero ? Vector2.zero : axis.normalized;
        }

        /// <summary>+1 zooming in, -1 zooming out, 0 for neither or both held at once.</summary>
        public static float ResolveZoom(bool zoomIn, bool zoomOut)
        {
            if (zoomIn == zoomOut)
            {
                return 0f;
            }

            return zoomIn ? 1f : -1f;
        }

        /// <summary>
        /// How much faster than <see cref="PanUnitsPerSecond"/> to pan at a given camera height.
        /// Crossing the whole map should take roughly the same few seconds whether the player is
        /// zoomed in on one district or looking at all five at once, which a fixed speed cannot do.
        /// </summary>
        public static float HeightFactor(float cameraHeight) =>
            Mathf.Clamp(cameraHeight / ReferenceHeight, MinHeightFactor, MaxHeightFactor);

        /// <summary>
        /// Keeps the camera's ground point within <see cref="PanMargin"/> of the map: the city's square,
        /// and south of it as far as the southernmost district reaches.
        /// </summary>
        public static Vector2 ClampToMap(Vector2 groundPosition) => new(
            Mathf.Clamp(groundPosition.x, -PanMargin, CityLayout.Size + PanMargin),
            Mathf.Clamp(groundPosition.y, SouthmostGround - PanMargin, CityLayout.Size + PanMargin));

        /// <summary>
        /// How far south the city is built: zero for the original square, below it once a district
        /// stands on the tile added south of the port. The land goes on further; there is nothing on it.
        /// </summary>
        public static float SouthmostGround
        {
            get
            {
                var southmost = 0f;

                foreach (var district in CityLayout.Districts)
                {
                    southmost = Mathf.Min(southmost, district.CentreZ - district.Radius);
                }

                return southmost;
            }
        }

        private static Vector3 FlattenToGround(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }
}
