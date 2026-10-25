using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    [RequireComponent(typeof(Button))]
    public sealed class UIRouteButton : MonoBehaviour {
        public UIRouteAction action;
        public UIScreenId destination;
        public int levelNumber;
        Button button;
        void OnEnable() { Bind(); }
        public void Bind() {
            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveListener(Trigger);
            button.onClick.AddListener(Trigger);
        }
        void OnDisable() { if (button != null) button.onClick.RemoveListener(Trigger); }
        public void Trigger() {
            var router = GetComponentInParent<UIScreenRouter>();
            if (router != null) router.Route(action, destination, levelNumber);
        }
    }
}
