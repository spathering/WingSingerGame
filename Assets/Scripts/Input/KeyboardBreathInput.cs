using UnityEngine;

namespace WingSinger {
    // A future sensor adapter can implement BreathInput without changing scoring or level management.
    public sealed class KeyboardBreathInput : BreathInput {
        public KeyCode inhaleKey = KeyCode.W;
        public KeyCode exhaleKey = KeyCode.S;
        public override BreathLane ReadLane() => Resolve(Input.GetKey(inhaleKey), Input.GetKey(exhaleKey));
        public static BreathLane Resolve(bool inhale, bool exhale) => inhale == exhale ? BreathLane.Middle
            : inhale ? BreathLane.Upper : BreathLane.Lower;
    }
}
