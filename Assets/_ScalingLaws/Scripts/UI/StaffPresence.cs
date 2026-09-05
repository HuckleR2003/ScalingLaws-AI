using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The people the company has hired, standing in the office.
    ///
    /// **The room has had a `Staff` group since the day it was generated and only ever held the
    /// founder.** A company could hire twelve people, pay them every day, watch them move research
    /// and reliability and brand, and the room they were supposedly in was empty except for the
    /// player. That is the same gap the team page had before names were put on it: the game knew and
    /// never showed it.
    ///
    /// Spawned rather than placed, for the reason the founder is: `Game.unity` holds 107 hand-placed
    /// prefab instances and a guard refuses to regenerate it, so nothing new goes in there by hand
    /// if code can put it in.
    ///
    /// **Rebuilt only when the roster changes.** The office is re-dressed on every tab change and
    /// destroying and re-instantiating a dozen skinned meshes each time is the kind of cost that
    /// does not show up until somebody has a full floor.
    /// </summary>
    public sealed class StaffPresence
    {
        /// <summary>The group the room builder writes, shared with the founder.</summary>
        public const string StaffGroup = FounderPresence.StaffGroup;

        /// <summary>What a spawned employee is called, so the founder is never mistaken for one.</summary>
        public const string NamePrefix = "Employee ";

        /// <summary>
        /// How far apart people stand, in metres.
        ///
        /// They are placed on a grid rather than at desks. The rooms are generated at four different
        /// sizes and only some of them carry desk markers, so anchoring to desks would put a full
        /// floor's worth of people on top of each other in the two rooms that have none. A grid is
        /// honest about being a placeholder and never overlaps.
        /// </summary>
        public const float Spacing = 1.4f;

        /// <summary>Rows before the grid wraps. Wide rather than deep: the camera looks along z.</summary>
        public const int PerRow = 5;

        private readonly Func<CompanyState> state;
        private readonly List<GameObject> spawned = new();

        private int shownCount = -1;
        private string shownSignature = string.Empty;

        public StaffPresence(Func<CompanyState> state)
        {
            this.state = state;
        }

        /// <summary>How many people are actually standing in the room. Read by the guard.</summary>
        public int Standing => spawned.Count;

        /// <summary>
        /// Puts the right people in the room.
        ///
        /// Cheap to call on every repaint: it builds a short signature of the roster and returns
        /// without touching the scene when nothing has changed.
        /// </summary>
        public void Refresh()
        {
            var company = state?.Invoke();

            if (company == null)
            {
                return;
            }

            var group = GameObject.Find(StaffGroup);

            if (group == null)
            {
                // No office scene loaded. Not a failure: the shell runs in tests and in the menu.
                return;
            }

            var hires = company.Staff.Hires;
            var signature = Signature(hires);

            if (signature == shownSignature && shownCount == hires.Count)
            {
                return;
            }

            shownSignature = signature;
            shownCount = hires.Count;

            Clear();

            for (var index = 0; index < hires.Count; index++)
            {
                Spawn(group.transform, hires[index], index);
            }

            // A fresh set of people has nobody hidden yet, so the next `SetHour` has to do the work
            // even if the hour has not moved.
            shownHour = -1;
        }

        /// <summary>
        /// What the roster looks like, cheaply.
        ///
        /// Names and roles rather than a hash of everything: a bonus paid or an hour changed does
        /// not move anybody in the room, and rebuilding twelve skinned meshes because somebody
        /// clicked a schedule would be work nobody asked for.
        /// </summary>
        private static string Signature(IReadOnlyList<Hire> hires)
        {
            var text = new System.Text.StringBuilder(hires.Count * 12);

            foreach (var hire in hires)
            {
                text.Append(hire.Name).Append('/').Append((int)hire.Role).Append(';');
            }

            return text.ToString();
        }

        private void Spawn(Transform group, Hire hire, int index)
        {
            var prefab = Resources.Load<GameObject>(FounderPresence.PrefabPath);

            if (prefab == null)
            {
                return;
            }

            var person = UnityEngine.Object.Instantiate(prefab, group);
            person.name = NamePrefix + index;

            // A grid behind where the founder works, stepped back a row so nobody stands inside
            // them on an empty roster.
            person.transform.localPosition = new Vector3(
                (index % PerRow - (PerRow - 1) * 0.5f) * Spacing,
                0f,
                -2.0f - index / PerRow * Spacing);

            person.AddComponent<NamePlate>().Set(
                hire.Name,
                NamePlate.TitleFor(hire.Role),
                NamePlate.ColourFor(hire.Role));

            // **What makes them clickable.** The character packs ship with no collider at all, so a
            // ray through the office camera passed straight through every person in the room. One
            // capsule, sized to a person, on the root the picker walks up to.
            var body = person.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.32f;
            body.center = new Vector3(0f, 0.9f, 0f);

            // Which employee this is, for the panel the click opens. On the root rather than in a
            // dictionary keyed by transform, because the scene is the thing that survives a repaint
            // and a dictionary would have to be kept in step with it.
            person.AddComponent<OfficePerson>().Index = index;

            spawned.Add(person);
        }

        /// <summary>
        /// Which employee is nearest a point in the camera's view, or -1 when nobody is near it.
        ///
        /// **The click path that does not go through physics.** `StagePicking` raycasts, which is
        /// exact when it works and has four ways to come back empty that look identical from the
        /// outside: a capsule on a scaled prefab root, a rig that swallows the ray, the wrong
        /// camera, or a scene with physics not running. A playtest reported the result as "clicking
        /// a person does nothing", which is all any of them look like.
        ///
        /// Projecting is the same arithmetic the build-mode slot markers already use, it needs no
        /// component on the model, and it cannot be defeated by a collider.
        ///
        /// The aim is the body rather than the feet: <see cref="AimHeight"/> up the model, which is
        /// roughly where a person's chest is and where a player points.
        /// </summary>
        public int NearestTo(Camera camera, Vector2 viewport, float radius = 0.055f)
        {
            if (camera == null || spawned.Count == 0)
            {
                return -1;
            }

            var best = -1;
            var bestDistance = radius;

            for (var index = 0; index < spawned.Count; index++)
            {
                var person = spawned[index];

                // Somebody off shift is not in the room to be clicked on. Their renderers are off,
                // and a card opened by clicking an empty patch of floor is worse than no card.
                if (person == null || !IsOnShift(index))
                {
                    continue;
                }

                var world = person.transform.position + Vector3.up * AimHeight;
                var view = camera.WorldToViewportPoint(world);

                if (view.z <= 0f)
                {
                    continue;
                }

                var distance = Vector2.Distance(new Vector2(view.x, view.y), viewport);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            return best;
        }

        /// <summary>Where on a person a click is aimed, in metres up from the floor.</summary>
        private const float AimHeight = 1.0f;

        /// <summary>
        /// Whether this employee is currently drawn.
        ///
        /// Read off the renderers rather than recomputed from the clock, so the answer cannot
        /// disagree with what is on screen: they are the same fact.
        /// </summary>
        private bool IsOnShift(int index)
        {
            if (index < 0 || index >= spawned.Count || spawned[index] == null)
            {
                return false;
            }

            foreach (var renderer in spawned[index].GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Shows the people whose shift it is and hides the rest.
        ///
        /// **The first thing in the game that makes the schedule control visible.** Hours were
        /// already saved and already read by loyalty, and the only place they appeared was a panel
        /// nobody opened twice. A control whose effect cannot be seen is a control people set once
        /// and forget about.
        ///
        /// Presentation only, and deliberately: the day still pays a full salary and still does a
        /// full day's work whatever the clock says. Hours are a loyalty and expectation mechanic,
        /// and turning them into a productivity dial is a different design that would have to be
        /// balanced rather than a side effect of drawing somebody.
        ///
        /// Cheap enough for every frame: it compares the hour it last drew and returns.
        /// </summary>
        public void SetHour(double hour)
        {
            var company = state?.Invoke();

            if (company == null || spawned.Count == 0)
            {
                return;
            }

            // Whole hours. The clock sweeps continuously and a person cannot half arrive, so
            // anything finer is work done for a difference nobody can see.
            var now = (int)System.Math.Floor(System.Math.Clamp(hour, 0.0, 23.999));

            if (now == shownHour)
            {
                return;
            }

            shownHour = now;

            var hires = company.Staff.Hires;

            for (var index = 0; index < spawned.Count && index < hires.Count; index++)
            {
                var person = spawned[index];

                if (person == null)
                {
                    continue;
                }

                var hire = hires[index];
                var onDuty = now >= hire.StartHour && now < hire.EndHour;

                // The renderers rather than the object, so the name plate stays up: the plate is
                // what is left of somebody who has gone home and it is the point of the whole
                // arrangement.
                foreach (var renderer in person.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.transform.name is "Line" or "Rule")
                    {
                        continue;
                    }

                    renderer.enabled = onDuty;
                }

                var plate = person.GetComponent<NamePlate>();

                if (plate != null)
                {
                    plate.SetOnDuty(onDuty, Loc.T("plate.off_duty",
                        NamePlate.TitleFor(hire.Role),
                        hire.StartHour.ToString("00"),
                        hire.EndHour.ToString("00")));
                }
            }
        }

        /// <summary>The hour last drawn, so a frame that changes nothing touches nothing.</summary>
        private int shownHour = -1;

        /// <summary>Takes everybody out, for a rebuild or a move.</summary>
        public void Clear()
        {
            foreach (var person in spawned)
            {
                if (person == null)
                {
                    continue;
                }

                // Destroy is a no-op outside play mode and DestroyImmediate throws inside it. Both
                // matter: the office is dressed from editor tooling as well as from the game.
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(person);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(person);
                }
            }

            spawned.Clear();
        }
    }

    /// <summary>
    /// Which employee a model in the room is.
    ///
    /// A component rather than a lookup keyed by transform, because the scene outlives a repaint and
    /// a dictionary would have to be kept in step with it. The founder does not carry one: index -1
    /// would be a second meaning for the same field, and the picker asks a different question of
    /// them anyway.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficePerson : MonoBehaviour
    {
        /// <summary>Position in <see cref="StaffRoster.Hires"/>, which is what the panel takes.</summary>
        public int Index { get; set; }
    }
}
