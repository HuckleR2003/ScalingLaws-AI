using System.Collections.Generic;
using ScalingLaws.UI;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Covers a grey placeholder strip with repeated copies of a real model, along the strip's own
    /// length, at the strip's own width.
    ///
    /// **One tool instead of one per generator.** The city is drawn by several builders that each
    /// work out their own geometry — arterial centrelines, district grids, subdivision surveys, park
    /// paths — and the first two road passes each re-derived one of those from scratch. That does not
    /// scale and it drifts: the copy and the original disagree the moment either changes. Every
    /// placeholder already records what it stands for, where it is, which way it faces and how big it
    /// is (<see cref="CityProp"/>), so a strip can be replaced from the strip itself, whoever drew it.
    ///
    /// **A placeholder's footprint is (width, thickness, length) in its own local axes**, and its
    /// rotation already points local +Z down the strip — see `CityDressingBuilder.StreetStrip`, which
    /// builds every one of them that way. That is the contract this relies on.
    ///
    /// Scaling is as close to uniform as the strip allows: the model is scaled to the strip's width,
    /// the step is the model's own length at that scale, and only the last fraction is absorbed by
    /// nudging the step so a whole number of pieces fits exactly. A strip covered by pieces of
    /// roughly their natural proportions keeps whatever the artist drew on them; one piece stretched
    /// the length of the strip does not.
    /// </summary>
    public static class CityPropTiler
    {
        /// <summary>A model, and the one thing about it that cannot be derived: which way it faces.</summary>
        public readonly struct Piece
        {
            public Piece(string path, bool lengthAlongX)
            {
                Path = path;
                LengthAlongX = lengthAlongX;
            }

            public string Path { get; }

            /// <summary>
            /// True when the model's long dimension is its local X — which is how Kenney draws road
            /// tiles, and the opposite of how it draws the suburban driveways and paths. Measured
            /// with <see cref="KenneyModelAudit"/>, never assumed.
            /// </summary>
            public bool LengthAlongX { get; }
        }

        /// <summary>
        /// Replaces every placeholder of one kind. Returns how many strips were covered.
        ///
        /// Strips narrower or shorter than this are skipped rather than covered with a sliver: a
        /// kerb stub the length of a doorstep is not worth a draw call, and scaling a model down
        /// that far stops reading as anything.
        /// </summary>
        /// <param name="minimumWidth">
        /// Lets one kind be covered by more than one model. `RoadSegment` is drawn for a twelve
        /// metre suburban lane and for a four metre park path alike; a road tile has lane markings
        /// painted on it and a footpath must not.
        /// </param>
        public static int Replace(CityPropKind kind, Piece piece, Transform parent,
            float minimumLength = 1.5f, float lift = 0.02f,
            float minimumWidth = 0f, float maximumWidth = float.MaxValue)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(piece.Path);
            if (model == null)
            {
                Debug.LogError($"[Tiler] Could not load {piece.Path}.");
                return 0;
            }

            if (!TryMeasure(model, out var modelSize))
            {
                Debug.LogError($"[Tiler] {piece.Path} has no renderer to measure.");
                return 0;
            }

            var modelWidth = piece.LengthAlongX ? modelSize.z : modelSize.x;
            var modelLength = piece.LengthAlongX ? modelSize.x : modelSize.z;

            if (modelWidth <= 0.0001f || modelLength <= 0.0001f)
            {
                Debug.LogError($"[Tiler] {piece.Path} measures zero in a direction it is needed in.");
                return 0;
            }

            var turn = piece.LengthAlongX ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;

            var doomed = new List<GameObject>();
            var covered = 0;
            var placed = 0;

            foreach (var prop in Object.FindObjectsByType<CityProp>(FindObjectsSortMode.None))
            {
                if (prop.Kind != kind)
                {
                    continue;
                }

                var footprint = prop.Footprint;
                var width = footprint.x;
                var length = footprint.z;

                if (length < minimumLength || width <= 0.01f
                    || width < minimumWidth || width > maximumWidth)
                {
                    continue;
                }

                var box = prop.transform;

                // The placeholder is a centred cube, so its own base is half its thickness below the
                // position it was given. That base is the ground the generator surveyed — including
                // whatever levelling its district had — which is a better answer than re-sampling the
                // terrain here would be.
                var groundY = box.position.y - footprint.y * 0.5f + lift;

                var widthFactor = width / modelWidth;
                var pieces = Mathf.Max(1, Mathf.RoundToInt(length / (modelLength * widthFactor)));
                var step = length / pieces;
                var lengthFactor = step / modelLength;

                var scale = piece.LengthAlongX
                    ? new Vector3(lengthFactor, widthFactor, widthFactor)
                    : new Vector3(widthFactor, widthFactor, lengthFactor);

                var group = new GameObject(prop.name).transform;
                group.SetParent(parent, false);

                for (var index = 0; index < pieces; index++)
                {
                    var along = -length * 0.5f + step * (index + 0.5f);
                    var at = box.position + box.rotation * new Vector3(0f, 0f, along);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                    instance.transform.position = new Vector3(at.x, groundY, at.z);
                    instance.transform.rotation = box.rotation * turn;
                    instance.transform.localScale = scale;

                    placed++;
                }

                doomed.Add(prop.gameObject);
                covered++;
            }

            foreach (var box in doomed)
            {
                Object.DestroyImmediate(box);
            }

            Debug.Log($"[Tiler] {kind}: {covered} strips covered by {placed} pieces of {model.name}.");
            return covered;
        }

        /// <summary>Local size of a model on disk, where it sits unrotated and unscaled.</summary>
        private static bool TryMeasure(GameObject model, out Vector3 size)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);

            if (renderers.Length == 0)
            {
                size = Vector3.zero;
                return false;
            }

            var bounds = renderers[0].localBounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].localBounds);
            }

            size = bounds.size;
            return true;
        }
    }
}
