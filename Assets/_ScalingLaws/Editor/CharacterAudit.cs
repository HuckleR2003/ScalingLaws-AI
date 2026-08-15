using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// Says out loud whether every character and clip is actually usable.
    ///
    /// **Every fault in this area is silent.** An invalid avatar plays every clip as a T-pose, a
    /// clip bound to the wrong rig produces no animation at all, and a state with no motion looks
    /// exactly like a state whose clip is still. None of it errors. This prints the facts.
    /// </summary>
    public static class CharacterAudit
    {
        [MenuItem("Scaling Laws/Characters/Audit")]
        public static void Audit()
        {
            var lines = new List<string>();
            var bad = 0;

            foreach (var path in CharacterRigSetup.CharacterFiles())
            {
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                var ok = avatar != null && avatar.isValid && avatar.isHuman;
                bad += ok ? 0 : 1;
                lines.Add($"  {(ok ? "OK  " : "BAD ")} {Path.GetFileNameWithoutExtension(path)}");
            }

            foreach (var (_, clipName, loops) in FounderClipImporter.Wanted)
            {
                var clip = FounderClipImporter.Find(clipName);
                var ok = clip != null && clip.length > 0.1f;
                bad += ok ? 0 : 1;
                lines.Add(ok
                    ? $"  OK   clip {clipName}  {clip.length:0.00}s  loop={loops}"
                    : $"  BAD  clip {clipName} missing");
            }

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/_ScalingLaws/Art/Character/Founder.controller");

            if (controller == null)
            {
                lines.Add("  BAD  no controller");
                bad++;
            }
            else
            {
                foreach (var state in controller.layers[0].stateMachine.states)
                {
                    var motion = state.state.motion;
                    var ok = motion != null;
                    bad += ok ? 0 : 1;
                    lines.Add(ok
                        ? $"  OK   state {state.state.name} -> {motion.name}"
                        : $"  BAD  state {state.state.name} has no motion");
                }
            }

            Debug.Log($"AUDIT {(bad == 0 ? "CLEAN" : bad + " PROBLEMS")}\n" + string.Join("\n", lines));
        }
    }
}
