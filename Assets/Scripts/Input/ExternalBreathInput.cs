using UnityEngine;

namespace WingSinger {
    // Integration point only; no hardware protocol is assumed by this prototype.
    public sealed class ExternalBreathInput : BreathInput {
        [SerializeField] BreathLane lane = BreathLane.Middle;
        public void Submit(BreathLane value) { lane = value; }
        public override BreathLane ReadLane() => lane;
        void OnDisable() { lane = BreathLane.Middle; }
    }
}
