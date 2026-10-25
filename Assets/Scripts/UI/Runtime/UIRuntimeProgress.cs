using UnityEngine;

namespace WingSinger.UI {
    public sealed class UIRuntimeProgress : MonoBehaviour {
        public RectTransform fill, track;
        public bool dailyGoal;
        public void Present(BreathUIFlowController flow) {
            if (fill == null || track == null) return;
            var size = fill.sizeDelta;
            size.x = track.rect.width * Mathf.Clamp01(dailyGoal ? flow.DailyProgress : flow.SessionProgress);
            fill.sizeDelta = size;
        }
    }
}
