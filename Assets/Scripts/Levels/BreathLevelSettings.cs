using UnityEngine;
using UnityEngine.Serialization;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Breathing Level Settings")]
    public sealed class BreathLevelSettings : ScriptableObject {
        public string displayName = "First Flight";
        [Header("Breathing pattern (seconds)")]
        [Min(0.1f)] public float inhaleSeconds = 3;
        [Min(0.1f)] public float exhaleSeconds = 4;
        [Header("Segmented route (optional)")]
        [Min(1)] public int loopCount = 1;
        [Min(1)] public float loopSeconds = 50;
        [Min(0)] public float middleTailSeconds = 2;
        [Header("Session")]
        [Min(1)] public float durationSeconds = 45;
        [Header("Paper collection")]
        [FormerlySerializedAs("pointsPerSecond"), Min(0)] public float pointsPerCollectible = 10;
        [Tooltip("Maximum time gap on straight sections. Each section is divided evenly with both endpoints included.")]
        [Min(0.05f)] public float collectibleIntervalSeconds = 0.5f;
        [Min(0.05f)] public float collectionRadius = 0.55f;
        [Min(0)] public float attractionRadius = 1.2f;
        [Tooltip("Optional route visualization. Paper objects show the route by default.")]
        public bool showGuideLine;
        [Min(0)] public float guideLeadSeconds = 2;
        [Tooltip("Route time spent on the middle lane before the first inhale. Included in the game duration.")]
        [Min(0)] public float preparationSeconds = 3;
        [Tooltip("Time taken by the guide to travel between lanes. Added between inhale/exhale dwell periods.")]
        [Min(0.01f)] public float guideTransitionSeconds = 0.65f;
        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 4;
        [Min(0.1f)] public float laneSpacing = 3;
        public float laneCenterOffset;
        [Min(0)] public float upperLaneSpacing;
        [Min(0)] public float lowerLaneSpacing;
        public float LaneHeight(float lane) => laneCenterOffset + lane * (lane >= 0
            ? (upperLaneSpacing > 0 ? upperLaneSpacing : laneSpacing)
            : (lowerLaneSpacing > 0 ? lowerLaneSpacing : laneSpacing));
        [Min(0)] public float laneTransitionSeconds = 0.16f;
        [Tooltip("Fly in from the left while already moving toward the route. Continues at normal speed after entry. Zero disables the entrance. Preview is extended if needed.")]
        [Min(0)] public float playerEntranceSeconds = 1.25f;

        public BreathPattern CreatePattern() => new BreathPattern(inhaleSeconds, exhaleSeconds,
            durationSeconds, pointsPerCollectible, Mathf.Max(guideLeadSeconds, playerEntranceSeconds), laneTransitionSeconds,
            preparationSeconds, guideTransitionSeconds, loopCount, loopSeconds, middleTailSeconds);

        public void Validate() {
            if (float.IsNaN(playerEntranceSeconds) || float.IsInfinity(playerEntranceSeconds) || playerEntranceSeconds < 0
                || float.IsNaN(guideLeadSeconds) || float.IsInfinity(guideLeadSeconds) || guideLeadSeconds < 0)
                throw new System.ArgumentException("Preview and entrance durations must be nonnegative and finite.");
            CreatePattern();
            if (float.IsNaN(laneCenterOffset) || float.IsInfinity(laneCenterOffset)
                || !IsNonnegativeFinite(upperLaneSpacing) || !IsNonnegativeFinite(lowerLaneSpacing) || !IsNonnegativeFinite(attractionRadius))
                throw new System.ArgumentException("Lane offsets and attraction radius must be finite; spacing overrides and radius must be nonnegative.");
            if (!IsPositiveFinite(moveSpeed) || !IsPositiveFinite(laneSpacing) || !IsPositiveFinite(guideTransitionSeconds))
                throw new System.ArgumentException("Movement speed, lane spacing and guide transition time must be positive and finite.");
            if (!IsPositiveFinite(collectibleIntervalSeconds) || collectibleIntervalSeconds < 0.05f || !IsPositiveFinite(collectionRadius))
                throw new System.ArgumentException("Paper interval must be at least 0.05s; collection radius must be positive and finite.");
        }

        static bool IsPositiveFinite(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        static bool IsNonnegativeFinite(float value) => value >= 0 && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
