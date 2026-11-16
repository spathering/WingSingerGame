using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    [DefaultExecutionOrder(10003)]
    public sealed class FigmaGameplayHud : MonoBehaviour {
        public TMP_Text cue, title, detail, feedback, meterStatus;
        public FigmaSurface meter;
        public FigmaSurface[] progress;
        public Button sound;
        public CanvasGroup meterGroup;
        public RectTransform meterTrack, meterMarker;
        public TMP_Text meterValue, meterReference;
        public BreathLane CurrentPromptLane { get; private set; }
        public BreathMeterReading MeterReading { get; private set; }
        BreathUIFlowController flow;
        void OnEnable() { flow = GetComponentInParent<BreathUIFlowController>(); sound.onClick.AddListener(ToggleSound); }
        void OnDisable() { sound.onClick.RemoveListener(ToggleSound); }
        void ToggleSound() { if (flow != null && flow.Progress != null) flow.bindings.sound.value = flow.Progress.Data.soundVolume > 0 ? 0 : .8f; }
        void LateUpdate() {
            if (flow == null || flow.Progress == null) return;
            var s = flow.levelManager.Session;
            if (s == null) return;
            // Sample the exact clock already used to position the leading crane. The player's
            // route clock trails it and can already be in the opposite breathing phase.
            var level=flow.levelManager.CurrentLevel;
            bool blocked=flow.levelManager.IsPresentationBlocking;
            var lane=blocked||level==null?BreathLane.Middle:s.Pattern.LaneAt(level.SampledGuideTime);
            CurrentPromptLane=lane;
            cue.text = blocked?"GET READY":lane == BreathLane.Lower ? "BREATHE IN ↓" : lane == BreathLane.Upper ? "BREATHE OUT ↑" : "BREATHE NATURALLY";
            title.text = lane == BreathLane.Lower ? "Breathe in gently." : lane == BreathLane.Upper ? "Breathe out gently." : "Find your rhythm.";
            detail.text = lane == BreathLane.Lower ? "Follow the crane downward.  W" : lane == BreathLane.Upper ? "Follow the crane upward.  S" : "Relax and follow the crane.";
            var actual = flow.levelManager.input.ReadLane();
            bool match = actual == s.RequiredLane;
            feedback.text = match ? "Nice and steady" : "Follow the crane";
            var sensor=flow.sensorInput;
            MeterReading=sensor==null?default:BreathMeterReading.Evaluate(sensor.ReceiverSnapshot,sensor.Configuration,sensor.UsesCsv,
                flow.levelManager.input==sensor && sensor.isActiveAndEnabled);
            meterGroup.alpha=MeterReading.Available?1:.45f;
            meterStatus.text=!MeterReading.Available?"NO SENSOR":MeterReading.Lane==BreathLane.Lower?"INHALE":MeterReading.Lane==BreathLane.Upper?"EXHALE":"REST";
            meterValue.text=MeterReading.Available?MeterReading.ValueMv.ToString("0")+" mV":"-- mV";
            meterReference.text="Target: "+(MeterReading.TargetDeltaMv>0?MeterReading.TargetDeltaMv.ToString("0"):"--")+" mV";
            // The marker is 100% of target; the full bar represents 200% of target deviation.
            float height=meterTrack.rect.height;
            var size = meter.rectTransform.sizeDelta; size.y = height*Mathf.Clamp01((float)MeterReading.Strength*.5f); meter.rectTransform.sizeDelta = size;
            var marker=meterMarker.anchoredPosition;marker.y=meterTrack.anchoredPosition.y-height*.5f;meterMarker.anchoredPosition=marker;
            meter.color=MeterReading.TargetReached?new Color(.33f,.55f,.26f):new Color(.65f,.65f,.55f);
            for (int i = 0; i < progress.Length; i++) progress[i].color = flow.SessionProgress * progress.Length >= i + 1 ? new Color(.4f,.54f,.29f) : new Color(.78f,.74f,.65f,.65f);
        }
    }
}
