using UnityEngine;

namespace WingSinger {
    public abstract class BreathInput : MonoBehaviour {
        public abstract BreathLane ReadLane();
    }

}
