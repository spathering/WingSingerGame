using UnityEngine;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Breathing Level Settings")]
    public sealed class BreathLevelSettings : ScriptableObject {
        public string displayName = "First Flight";
        [Header("Breathing pattern (seconds)")]
        [Min(0.1f)] public float inhaleSeconds = 3;
        [Min(0.1f)] public float exhaleSeconds = 4;
        [Header("Session")]
        [Min(1)] public float durationSeconds = 45;
        [Min(0)] public float pointsPerSecond = 10;
        [Min(0)] public float guideLeadSeconds = 2;
        [Tooltip("Route time spent on the middle lane before the first inhale. Included in the game duration.")]
        [Min(0)] public float preparationSeconds = 3;
        [Tooltip("Time taken by the guide to travel between lanes. Added between inhale/exhale dwell periods.")]
        [Min(0.01f)] public float guideTransitionSeconds = 0.65f;
        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 4;
        [Min(0.1f)] public float laneSpacing = 2.2f;
        [Min(0)] public float laneTransitionSeconds = 0.16f;

        public BreathPattern CreatePattern() => new BreathPattern(inhaleSeconds, exhaleSeconds,
            durationSeconds, pointsPerSecond, guideLeadSeconds, laneTransitionSeconds,
            preparationSeconds, guideTransitionSeconds);

        public void Validate() {
            CreatePattern();
            if (!IsPositiveFinite(moveSpeed) || !IsPositiveFinite(laneSpacing) || !IsPositiveFinite(guideTransitionSeconds))
                throw new System.ArgumentException("Movement speed, lane spacing and guide transition time must be positive and finite.");
        }

        static bool IsPositiveFinite(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
