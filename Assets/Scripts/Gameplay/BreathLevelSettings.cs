using UnityEngine;
using UnityEngine.Serialization;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Breathing Level Settings")]
    public sealed class BreathLevelSettings : ScriptableObject {
        public string displayName = "First Flight";
        [Header("Breathing pattern (seconds)")]
        [Min(0.1f)] public float inhaleSeconds = 3;
        [Min(0.1f)] public float exhaleSeconds = 4;
        [Header("Session")]
        [Min(1)] public float durationSeconds = 45;
        [Header("Paper collection")]
        [FormerlySerializedAs("pointsPerSecond"), Min(0)] public float pointsPerCollectible = 10;
        [Tooltip("Maximum time gap on straight sections. Each section is divided evenly with both endpoints included.")]
        [Min(0.05f)] public float collectibleIntervalSeconds = 0.5f;
        [Min(0.05f)] public float collectionRadius = 0.55f;
        [Tooltip("Optional route visualization. Paper objects show the route by default.")]
        public bool showGuideLine;
        [Min(0)] public float guideLeadSeconds = 2;
        [Tooltip("Route time spent on the middle lane before the first inhale. Included in the game duration.")]
        [Min(0)] public float preparationSeconds = 3;
        [Tooltip("Time taken by the guide to travel between lanes. Added between inhale/exhale dwell periods.")]
        [Min(0.01f)] public float guideTransitionSeconds = 0.65f;
        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 4;
        [Min(0.1f)] public float laneSpacing = 2.2f;
        [Min(0)] public float laneTransitionSeconds = 0.16f;
        [Tooltip("Fly in from the left while already moving toward the route. Continues at normal speed after entry. Zero disables the entrance. Preview is extended if needed.")]
        [Min(0)] public float playerEntranceSeconds = 1.25f;

        public BreathPattern CreatePattern() => new BreathPattern(inhaleSeconds, exhaleSeconds,
            durationSeconds, pointsPerCollectible, Mathf.Max(guideLeadSeconds, playerEntranceSeconds), laneTransitionSeconds,
            preparationSeconds, guideTransitionSeconds);

        public void Validate() {
            if (float.IsNaN(playerEntranceSeconds) || float.IsInfinity(playerEntranceSeconds) || playerEntranceSeconds < 0
                || float.IsNaN(guideLeadSeconds) || float.IsInfinity(guideLeadSeconds) || guideLeadSeconds < 0)
                throw new System.ArgumentException("Preview and entrance durations must be nonnegative and finite.");
            CreatePattern();
            if (!IsPositiveFinite(moveSpeed) || !IsPositiveFinite(laneSpacing) || !IsPositiveFinite(guideTransitionSeconds))
                throw new System.ArgumentException("Movement speed, lane spacing and guide transition time must be positive and finite.");
            if (!IsPositiveFinite(collectibleIntervalSeconds) || collectibleIntervalSeconds < 0.05f || !IsPositiveFinite(collectionRadius))
                throw new System.ArgumentException("Paper interval must be at least 0.05s; collection radius must be positive and finite.");
        }

        static bool IsPositiveFinite(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
