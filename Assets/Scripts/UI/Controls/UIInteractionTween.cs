using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WingSinger.UI {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIInteractionTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler {
        [Range(1, 1.1f)] public float hoverScale = 1.035f;
        [Range(.85f, 1)] public float pressedScale = .96f;
        [Min(.01f)] public float hoverSeconds = .12f, pressSeconds = .07f, releaseSeconds = .16f;
        public bool ReduceMotion { get; private set; }
        public bool IsAnimating => animation.isAlive;
        public float CurrentScale => factor;
        RectTransform rect;
        Button button;
        Vector3 restScale;
        Vector2 restPosition;
        Tween animation;
        float factor = 1;
        bool hovered, selected, pressed, wasInteractable;

        void Awake() {
            rect = GetComponent<RectTransform>(); button = GetComponent<Button>();
            restScale = rect.localScale; restPosition = rect.anchoredPosition;
        }
        void OnEnable() { hovered = selected = pressed = false; wasInteractable = button.IsInteractable(); ResetVisual(); }
        void Update() {
            bool interactable = button.IsInteractable();
            if (interactable == wasInteractable) return;
            wasInteractable = interactable;
            if (!interactable) { hovered = selected = pressed = false; ResetVisual(); }
        }
        bool CanAnimate => isActiveAndEnabled && button.IsActive() && button.IsInteractable() && !ReduceMotion;
        float RestTarget => hovered || selected ? hoverScale : 1;
        public void SetReduceMotion(bool value) {
            if (ReduceMotion == value) return;
            ReduceMotion = value;
            if (value) ResetVisual();
        }
        void Animate(float target, float seconds, Ease ease = Ease.OutQuad) {
            if (animation.isAlive) animation.Stop();
            if (!CanAnimate) { Apply(1); return; }
            animation = Tween.Custom(this, factor, target, seconds,
                (owner, value) => owner.Apply(value), ease: ease, useUnscaledTime: true);
        }
        void Apply(float value) {
            factor = value;
            rect.localScale = Vector3.Scale(restScale, new Vector3(value, value, 1));
            // Keep the visual center fixed without changing the authored pivot/layout.
            var centerOffset = Vector2.Scale(rect.rect.size, new Vector2(.5f, .5f) - rect.pivot);
            rect.anchoredPosition = restPosition + Vector2.Scale(centerOffset, new Vector2(restScale.x, restScale.y)) * (1 - value);
        }
        void ResetVisual() { if (animation.isAlive) animation.Stop(); if (rect != null) Apply(1); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; if (!pressed) Animate(RestTarget, hoverSeconds); }
        public void OnPointerExit(PointerEventData e) { hovered = pressed = false; Animate(RestTarget, hoverSeconds); }
        public void OnPointerDown(PointerEventData e) {
            if (e.button != PointerEventData.InputButton.Left || !CanAnimate) return;
            pressed = true; Animate(pressedScale, pressSeconds);
        }
        public void OnPointerUp(PointerEventData e) {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressed = false; Animate(RestTarget, releaseSeconds, Ease.OutBack);
        }
        public void OnSelect(BaseEventData e) { selected = true; if (!pressed) Animate(RestTarget, hoverSeconds); }
        public void OnDeselect(BaseEventData e) { selected = pressed = false; Animate(RestTarget, hoverSeconds); }
        public void OnSubmit(BaseEventData e) {
            if (!CanAnimate) return;
            Animate(pressedScale, pressSeconds);
            animation.OnComplete(this, owner => owner.Animate(owner.RestTarget, owner.releaseSeconds, Ease.OutBack));
        }
        void OnDisable() { hovered = selected = pressed = false; ResetVisual(); }
        void OnDestroy() { if (animation.isAlive) animation.Stop(); }
    }
}
