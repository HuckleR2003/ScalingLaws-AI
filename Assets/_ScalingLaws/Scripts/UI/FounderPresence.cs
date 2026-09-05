using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Puts the founder in the room and keeps them doing the right thing.
    ///
    /// **The room is rendered to a texture and shown as the background of the office screen**, so
    /// anything that happens in it is happening behind the whole interface. That is exactly why the
    /// founder is spawned at runtime rather than placed in the scene: `Game.unity` holds 107
    /// hand-placed prefab instances and a guard refuses to regenerate it, so nothing new goes in
    /// there by hand if it can be put in by code.
    ///
    /// It is deliberately not a `MonoBehaviour`. The shell already has an `Update`, the room has one
    /// founder, and a second component would mean a second place that decides what the founder is
    /// doing. This is owned and driven by the shell.
    /// </summary>
    public sealed class FounderPresence
    {
        /// <summary>Where the prefab lives. Resources, because it is spawned rather than referenced.</summary>
        public const string PrefabPath = "Character/Founder";

        /// <summary>The group the room builder leaves empty for exactly this.</summary>
        public const string StaffGroup = "Staff";

        private readonly Func<CompanyState> state;

        private OfficeActor actor;
        private bool searched;

        public FounderPresence(Func<CompanyState> state)
        {
            this.state = state;
        }

        /// <summary>
        /// The spawned figure, or null before Spawn has run.
        ///
        /// Exposed so the opening sequence can hide them until they get out of the car. Nothing
        /// else should be moving this: the routine owns where they walk.
        /// </summary>
        public Transform Model { get; private set; }

        /// <summary>What the founder is doing right now. Idle before anything has spawned.</summary>
        public FounderTask Task { get; private set; } = FounderTask.Working;

        /// <summary>True while the map has been opened and the founder is walking out to the car.</summary>
        public bool IsLeaving { get; private set; }

        /// <summary>True once they have reached the car and the room is empty.</summary>
        public bool IsAway { get; private set; }

        /// <summary>
        /// How much faster the clock should run this frame.
        ///
        /// Presentation only. It multiplies the real delta handed to `SimClock`, so the day sweeps
        /// faster while the founder is asleep and the simulation itself is untouched: the same days
        /// happen, they just take less of the player's evening.
        /// </summary>
        public float TimeScale => FounderRoutine.TimeScaleFor(Task);

        /// <summary>
        /// Finds the room and spawns the founder into it.
        ///
        /// Runs once and remembers that it ran, even when it fails. The office scene is not always
        /// loaded, and a lookup that retries every frame against a scene that will never contain a
        /// Staff group is a search of the whole hierarchy sixty times a second.
        /// </summary>
        public void Spawn()
        {
            if (searched)
            {
                return;
            }

            searched = true;

            var group = GameObject.Find(StaffGroup);
            if (group == null)
            {
                return;
            }

            // The face the player picked in the creator, and the generic founder only when they
            // picked nothing. The portrait promised this person; the room has to deliver them.
            var company = state?.Invoke();
            var prefab = Look(company?.FounderLook) ?? Resources.Load<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"[Scaling Laws] No founder at Resources/{PrefabPath}. Run "
                    + "Scaling Laws > Characters > Build founder rig and clips.");
                return;
            }

            var spawned = UnityEngine.Object.Instantiate(prefab, group.transform);
            spawned.name = "Founder";
            Model = spawned.transform;

            actor = spawned.GetComponent<OfficeActor>();
            if (actor == null)
            {
                // The look prefabs carry an Animator and no actor, because they are built for the
                // portrait first. The room needs the walking half as well.
                actor = spawned.AddComponent<OfficeActor>();
            }

            WearGlasses(spawned, company?.FounderGlasses ?? 0);

            // **The name over their head.** The room has had a person walking around it for weeks
            // and nothing saying who that is, which is the same gap the team page had: the game
            // knew and never said. Added here rather than in the prefab because the founder is
            // named at the creator and the prefab is shared with the portrait studio, where a
            // floating name over a headshot would be absurd.
            // The founder's own colour, deliberately none of the five staff colours: they are not
            // on the payroll and the room should say so without a word.
            spawned.AddComponent<NamePlate>().Set(
                UiFormat.PersonName(company?.FounderName),
                company == null ? null : Loc.T("plate.ceo_of", company.CompanyName),
                NamePlate.FounderColour);
        }

        /// <summary>
        /// The player opened the map. The founder walks out rather than the screen cutting.
        ///
        /// Returns false when there is nobody to walk, so the caller can go straight to the map
        /// instead of waiting for a journey that will never happen.
        /// </summary>
        public bool BeginLeaving()
        {
            if (actor == null)
            {
                return false;
            }

            IsLeaving = true;
            IsAway = false;
            return true;
        }

        /// <summary>Back from wherever they went. The room fills again.</summary>
        public void ComeBack()
        {
            IsLeaving = false;
            IsAway = false;
        }

        /// <summary>
        /// Called every frame by the shell. Decides the task and hands it to the actor.
        ///
        /// The decision is `FounderRoutine`'s, which knows nothing about Unity and is tested on its
        /// own. This is the wire between it and a model.
        /// </summary>
        public void Refresh(int dayIndex)
        {
            Task = FounderRoutine.TaskFor(dayIndex, IsLeaving, IsAway);

            if (actor == null)
            {
                return;
            }

            actor.Send(Task);

            // Reaching the car is what "away" means, and only the actor knows when that happened.
            if (IsLeaving && actor.HasArrived)
            {
                IsAway = true;
            }
        }

        /// <summary>The chosen look, or null when there is no such prefab.</summary>
        private static GameObject Look(string name) =>
            string.IsNullOrEmpty(name)
                ? null
                : Resources.Load<GameObject>($"{PortraitStudio.LookFolder}/{name}");

        /// <summary>
        /// Puts the chosen glasses on the head bone.
        ///
        /// Same offset the portrait uses, because the player chose them by looking at the portrait
        /// and the two must not disagree about where a face is.
        /// </summary>
        private static void WearGlasses(GameObject person, int choice)
        {
            if (choice <= 0)
            {
                return;
            }

            var pairs = new List<GameObject>();
            foreach (var loaded in Resources.LoadAll<GameObject>(PortraitStudio.LookFolder))
            {
                if (loaded.name.StartsWith("glasses_"))
                {
                    pairs.Add(loaded);
                }
            }

            pairs.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            if (choice > pairs.Count)
            {
                return;
            }

            var animator = person.GetComponent<Animator>();
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
            {
                return;
            }

            // Spawned at the character's own origin, where the prefab already lines up with the
            // face, then reparented to the head keeping the world position. Parenting straight to
            // the head bone adds head height a second time and lands them on the chest.
            // Same placement the portrait uses, because the player chose these by looking at the
            // portrait and the two must not disagree about where a face is.
            var worn = UnityEngine.Object.Instantiate(pairs[choice - 1]);

            var headHeight = head.position.y - person.transform.position.y;
            var scale = headHeight > 0.1f ? headHeight / PortraitStudio.FallbackHeadHeight : 1f;

            worn.transform.localScale *= scale;
            worn.transform.rotation = person.transform.rotation;
            worn.transform.position = head.position
                                      + person.transform.up * (PortraitStudio.EyeRise * scale)
                                      + person.transform.forward * (PortraitStudio.EyeReach * scale);

            worn.transform.SetParent(head, worldPositionStays: true);
        }

        /// <summary>True when a departure has finished and the map can open.</summary>
        public bool HasReachedTheCar => IsLeaving && actor != null && actor.HasArrived;
    }
}
