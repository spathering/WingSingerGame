using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    [RequireComponent(typeof(Slider))]
    public sealed class UISliderValue : MonoBehaviour {
        public TMP_Text valueLabel;
        Slider slider;
        void OnEnable() {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(Present);
            Present(slider.value);
        }
        void OnDisable() { if (slider != null) slider.onValueChanged.RemoveListener(Present); }
        public void Present(float value) { if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value * 100) + "%"; }
    }
}
