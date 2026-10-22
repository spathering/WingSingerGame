using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger {
    public sealed class BreathHud : MonoBehaviour {
        public TMP_Text levelLabel, scoreLabel, timerLabel, cueLabel, feedbackLabel, resultLabel;
        public RectTransform progressFill;
        public GameObject resultPanel;
        public Button restartButton, nextButton, resultRestartButton, resultNextButton;
        bool resultsShown;

        public void ResetHud(string title) {
            levelLabel.text = title;
            resultsShown = false;
            resultPanel.SetActive(false);
        }

        public void Present(BreathSession session) {
            scoreLabel.text = $"{System.Math.Floor(session.Score):0000}";
            timerLabel.text = $"{System.Math.Ceiling(session.Remaining):00}s";
            progressFill.anchorMax = new Vector2((float)(session.PlayTime / session.Pattern.Duration), 1);
            if (session.Phase == BreathPhase.Preview) {
                cueLabel.text = $"FOLLOW THE LIGHT  /  {System.Math.Ceiling(session.Pattern.Lead - session.Elapsed)}";
                feedbackLabel.text = "The guide goes first. Get ready to breathe.";
            } else if (session.Phase == BreathPhase.Running) {
                if (session.Pattern.IsPreparing(session.PlayTime)) {
                    cueLabel.text = $"GET READY  /  {System.Math.Ceiling(session.Pattern.Preparation - session.PlayTime)}";
                    feedbackLabel.text = "Stay on the middle lane. Release W and S.";
                    return;
                }
                cueLabel.text = session.RequiredLane == BreathLane.Upper ? "W  /  INHALE" : "S  /  EXHALE";
                feedbackLabel.text = $"Collect the paper objects.  Collected: {session.CollectedCount}";
            } else {
                cueLabel.text = "FLIGHT COMPLETE";
                feedbackLabel.text = "R  /  RESTART     N  /  NEXT FLIGHT";
                if (!resultsShown) {
                    resultsShown = true;
                    resultLabel.text = $"FLIGHT COMPLETE\n\n<size=64>{System.Math.Floor(session.Score):0}</size>\nPOINTS\n\n"
                        + $"Paper collected: {session.CollectedCount}";
                    resultPanel.SetActive(true);
                }
            }
        }
    }
}
