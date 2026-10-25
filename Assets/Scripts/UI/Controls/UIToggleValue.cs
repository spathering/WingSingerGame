using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    [RequireComponent(typeof(Toggle))]
    public sealed class UIToggleValue : MonoBehaviour {
        public RectTransform handle;
        public TMP_Text valueLabel;
        public Image track;
        Toggle toggle;
        void OnEnable() {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(Present);
            Present(toggle.isOn);
        }
        void OnDisable() { if (toggle != null) toggle.onValueChanged.RemoveListener(Present); }
        public void Present(bool value) {
            if (valueLabel != null) valueLabel.text = value ? "On" : "Off";
            if (track != null) track.color = value ? new Color(.26f,.55f,.24f) : new Color(.72f,.72f,.73f);
            if (handle != null) { var p=handle.anchoredPosition; p.x=value ? 35 : -35; handle.anchoredPosition=p; }
        }
    }
}
