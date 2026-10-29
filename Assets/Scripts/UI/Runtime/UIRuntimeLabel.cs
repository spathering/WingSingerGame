using TMPro;
using UnityEngine;

namespace WingSinger.UI {
    public enum UIValueKind {
        LevelTitle, WorldLevels, InhaleSeconds, ExhaleSeconds, TodayPractice, Streak,
        Connection, InputReadiness, Battery, Signal, HudStep, Collected, ResultSeconds,
        ResultCycles, ResultRhythm, TodaySessions, WeeklyFlights, WeeklyMinutes,
        RecentTitle, RecentDetail, InhaleHint, ExhaleHint, InstructionTitle, InstructionDetail, StreakDays, LevelDescription, SensorReading
    }
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UIRuntimeLabel : MonoBehaviour {
        public UIValueKind value;
        public int index;
        TMP_Text label;
        public void Present(BreathUIFlowController flow) {
            if (label == null) label = GetComponent<TMP_Text>();
            label.text = flow.Text(value, index);
            if (value == UIValueKind.Connection || value == UIValueKind.InputReadiness)
                label.color = flow.Progress.Data.highContrast ? new Color(.15f,.12f,.1f)
                    : flow.SensorFresh ? new Color(.26f,.55f,.24f) : new Color(.42f,.42f,.37f);
        }
    }
}
