using System;
using System.Collections.Generic;
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

            person.AddComponent<NamePlate>().Set(hire.Name);

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
