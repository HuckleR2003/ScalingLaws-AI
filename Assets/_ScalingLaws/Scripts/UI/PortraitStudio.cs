using System.Collections.Generic;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A live head-and-shoulders render of the founder, for the creator's portrait plate.
    ///
    /// **A photograph would have been easier and it would have been the wrong thing.** The player is
    /// choosing somebody who then walks around the office for fifteen years of game time, and a
    /// still image cannot promise that the person in the frame is the person who turns up. This
    /// renders the actual prefab the game will spawn, playing the actual controller it will use.
    ///
    /// The technique is already in the project: the office is rendered to a texture and shown as the
    /// background of the site screen. This is the same trick at a smaller size.
    ///
    /// **Isolation is by distance, not by layer.** The rig is parked four kilometres under the
    /// world and the camera's far plane is six metres, so nothing in a scene can reach it and it
    /// cannot reach anything. A culling mask would be tidier and would also need a layer reserved in
    /// project settings, which is a file the scene builder does not own.
    ///
    /// The rig is built entirely in code and destroyed with the screen, because the creator is in
    /// the menu scene and must not leave anything behind when the campaign starts.
    /// </summary>
    public sealed class PortraitStudio
    {
        /// <summary>Where the looks live. Built by the editor tool of the same name.</summary>
        public const string LookFolder = "Character/Looks";

        /// <summary>Pixels. Square-ish and generous, because the plate is 250px tall and can grow.</summary>
        public const int TextureSize = 512;

        /// <summary>
        /// Where the camera sits, as a fraction of the distance from the floor to the head bone.
        ///
        /// **Framed from the model rather than from a constant, and that was found by measuring.**
        /// The first version put the camera at a fixed 1.56m because that is roughly eye height on a
        /// 1.8m human. It worked for five of the fourteen. The Dudes pack is built at a different
        /// scale entirely, head bone at 2.24m against CharCrafter's 1.53m, so the camera was pointed
        /// at their chest and nine portraits rendered as an almost empty frame. Nothing errored.
        ///
        /// Slightly below the head bone, because the brief is head *and shoulders*.
        /// </summary>
        public const float EyeFraction = 1.02f;

        /// <summary>How far in front of the face, again as a fraction of head height.</summary>
        public const float DistanceFraction = 0.62f;

        /// <summary>Used when a model has no head bone to measure. A 1.8m human.</summary>
        public const float FallbackHeadHeight = 1.6f;

        /// <summary>
        /// Where the whole rig is parked.
        ///
        /// A long way from anything. The menu scene has no geometry, but the office scene does, and
        /// building the studio inside a room means rendering that room's walls behind the face.
        /// </summary>
        public static readonly Vector3 Somewhere = new(0f, -4000f, 0f);

        /// <summary>How far above the head bone the eyes are, at a 1.6m head height.</summary>
        public const float EyeRise = 0.145f;

        /// <summary>And how far in front of it. Scaled with the model, because the packs differ.</summary>
        public const float EyeReach = 0.115f;

        private GameObject rig;
        private Camera camera;
        private GameObject body;
        private GameObject glasses;

        private List<GameObject> looks;
        private List<GameObject> spectacles;

        /// <summary>The texture the portrait plate draws. Null until <see cref="Open"/> succeeds.</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>Which face is showing, as an index into what was found on disk.</summary>
        public int LookIndex { get; private set; }

        /// <summary>Zero is bare-faced. One upward are the pairs that were found.</summary>
        public int GlassesIndex { get; private set; }

        /// <summary>How many faces there are to choose between.</summary>
        public int LookCount => looks?.Count ?? 0;

        /// <summary>Bare-faced plus every pair found.</summary>
        public int GlassesCount => (spectacles?.Count ?? 0) + 1;

        /// <summary>The saved identity of the current face. A name, never an index.</summary>
        public string LookName =>
            looks != null && LookIndex >= 0 && LookIndex < looks.Count ? looks[LookIndex].name : string.Empty;

        /// <summary>
        /// Builds the rig. Safe to call when there is nothing to load: the plate simply stays as it
        /// was and the arrows do nothing, which is the right answer for a project where the
        /// character packs are gitignored and a fresh clone does not have them.
        /// </summary>
        public bool Open(string startingLook = null, int startingGlasses = 0)
        {
            Close();

            looks = new List<GameObject>();
            spectacles = new List<GameObject>();

            foreach (var loaded in Resources.LoadAll<GameObject>(LookFolder))
            {
                if (loaded.name.StartsWith("look_"))
                {
                    looks.Add(loaded);
                }
                else if (loaded.name.StartsWith("glasses_"))
                {
                    spectacles.Add(loaded);
                }
            }

            // Sorted by name, so the order on screen is the order the builder wrote and does not
            // depend on whatever order the loader happened to return.
            looks.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            spectacles.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            if (looks.Count == 0)
            {
                return false;
            }

            LookIndex = Mathf.Max(0, looks.FindIndex(look => look.name == startingLook));
            GlassesIndex = Mathf.Clamp(startingGlasses, 0, GlassesCount - 1);

            rig = new GameObject("PortraitStudio") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = Somewhere;

            Texture = new RenderTexture(TextureSize, TextureSize, 24)
            {
                name = "Portrait",
                antiAliasing = 4
            };

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = Texture;
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.05f;

            // Generous, because the far plane is what isolates the rig from any scene around it and
            // the tallest model here is nearly three metres.
            camera.farClipPlane = 12f;

            // A flat backdrop rather than a skybox. The plate behind it is nearly black and the face
            // has to read against it without a horizon line cutting through the shoulders.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.086f, 1f);

            var keyObject = new GameObject("Key");
            keyObject.transform.SetParent(rig.transform, false);
            keyObject.transform.localRotation = Quaternion.Euler(18f, 205f, 0f);

            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.97f, 0.92f);

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(rig.transform, false);
            fillObject.transform.localRotation = Quaternion.Euler(10f, 140f, 0f);

            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.35f;
            fill.color = new Color(0.72f, 0.80f, 1f);

            Rebuild();
            return true;
        }

        /// <summary>Next or previous face. Wraps, because a portrait chooser that dead-ends is a list.</summary>
        public void StepLook(int by)
        {
            if (LookCount == 0)
            {
                return;
            }

            LookIndex = ((LookIndex + by) % LookCount + LookCount) % LookCount;
            Rebuild();
        }

        /// <summary>Next or previous pair, including none.</summary>
        public void StepGlasses(int by)
        {
            GlassesIndex = ((GlassesIndex + by) % GlassesCount + GlassesCount) % GlassesCount;
            RebuildGlasses();
        }

        /// <summary>Tears the whole rig down. The creator owns it and the campaign must not inherit it.</summary>
        public void Close()
        {
            if (rig != null)
            {
                Discard(rig);
                rig = null;
            }

            if (Texture != null)
            {
                Texture.Release();
                Discard(Texture);
                Texture = null;
            }

            camera = null;
            body = null;
            glasses = null;
        }

        /// <summary>
        /// Destroys, in play mode or out of it.
        ///
        /// `Object.Destroy` is deferred and does nothing outside play mode, which matters because
        /// the only way to check this thing actually renders is to drive it from an editor tool.
        /// Swapping a face there would otherwise stack models on top of each other.
        /// </summary>
        private static void Discard(Object thing)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(thing);
            }
            else
            {
                Object.DestroyImmediate(thing);
            }
        }

        /// <summary>Renders one frame on demand. Used by the verification tool, not by the game.</summary>
        public void RenderNow()
        {
            if (camera != null)
            {
                camera.Render();
            }
        }

        private void Rebuild()
        {
            if (body != null)
            {
                Discard(body);
            }

            body = Object.Instantiate(looks[LookIndex], rig.transform);
            body.transform.localPosition = Vector3.zero;

            // Facing the camera, which sits behind them in local space.
            body.transform.localRotation = Quaternion.identity;

            Frame();
            RebuildGlasses();
        }

        /// <summary>
        /// Points the camera at this particular head.
        ///
        /// The packs are built at different scales and there is no standard for how tall a character
        /// is. Measuring the head bone is the only thing that works for a model nobody has seen yet.
        /// </summary>
        private void Frame()
        {
            if (camera == null || body == null)
            {
                return;
            }

            var animator = body.GetComponent<Animator>();
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

            // Local to the rig, which is parked a long way under the world.
            var headHeight = head != null
                ? head.position.y - rig.transform.position.y
                : FallbackHeadHeight;

            if (headHeight <= 0.1f)
            {
                headHeight = FallbackHeadHeight;
            }

            camera.transform.localPosition = new Vector3(
                0f, headHeight * EyeFraction, headHeight * DistanceFraction);
        }

        private void RebuildGlasses()
        {
            if (glasses != null)
            {
                Discard(glasses);
                glasses = null;
            }

            if (body == null || GlassesIndex <= 0 || spectacles.Count == 0)
            {
                return;
            }

            var animator = body.GetComponent<Animator>();
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
            {
                return;
            }

            // Parented to the head bone so they move with it. The offset is one number per axis and
            // it is a guess that looks right rather than a measurement: the packs put their heads in
            // slightly different places and there is no rig standard for where a face is.
            // **Measured, not guessed, and the first two guesses were both wrong.** The prefab does
            // not sit at face height inside itself and it does not sit on its own pivot either: its
            // mesh is centred on the model's origin, at the floor. So neither parenting it to the
            // head nor dropping it at the body root puts it anywhere near a face.
            //
            // Placed in world space from the head bone instead, along the character's own up and
            // forward, so it does not depend on which way a particular rig happens to point its head
            // bone. Then parented keeping that position, which is what makes it follow the head.
            glasses = Object.Instantiate(spectacles[GlassesIndex - 1]);

            var headHeight = head.position.y - rig.transform.position.y;
            var scale = headHeight > 0.1f ? headHeight / FallbackHeadHeight : 1f;

            glasses.transform.localScale *= scale;
            glasses.transform.rotation = body.transform.rotation;
            glasses.transform.position = head.position
                                         + body.transform.up * (EyeRise * scale)
                                         + body.transform.forward * (EyeReach * scale);

            glasses.transform.SetParent(head, worldPositionStays: true);
        }
    }
}
