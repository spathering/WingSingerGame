using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    // Keep the authored reference layout intact while fitting it inside the actual viewport.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class UIPageViewport : MonoBehaviour {
        public Vector2 designSize = new Vector2(2420, 1668);
        public RectTransform background;
        public bool respectSafeArea = true;
        RectTransform rect;
        DrivenRectTransformTracker tracker;
        RectTransform trackedParent;
        bool fitting, tracked;
        void OnEnable() { Canvas.willRenderCanvases += Fit; Fit(); }
        void LateUpdate() { Fit(); }
        void OnRectTransformDimensionsChange() { Fit(); }
        void OnValidate() { Fit(); }
        public void Fit() {
            if (fitting || !isActiveAndEnabled) return;
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
            if (rect == null) rect = GetComponent<RectTransform>();
            var parent = rect.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (parent == null || canvas == null || parent.rect.width <= 0 || parent.rect.height <= 0
                || designSize.x <= 0 || designSize.y <= 0) return;
            fitting = true;
            try {
                if (!tracked || trackedParent != parent) {
                    tracker.Clear(); tracker.Add(this, rect, DrivenTransformProperties.All);
                    if (background != null) tracker.Add(this, background, DrivenTransformProperties.All);
                    trackedParent = parent; tracked = true;
                }
                var viewport = parent.rect;
                var available = viewport;
                var camera = canvas.worldCamera;
                // RenderTexture captures have their own pixel rectangle; Screen.safeArea belongs to the display.
                if (respectSafeArea && (camera == null || camera.targetTexture == null)) {
                    Rect pixels = canvas.rootCanvas.pixelRect;
                    Rect safe = Screen.safeArea;
                    if (pixels.width > 0 && pixels.height > 0 && safe.width > 0 && safe.height > 0) {
                        float left = Mathf.Clamp01((safe.xMin - pixels.xMin) / pixels.width);
                        float bottom = Mathf.Clamp01((safe.yMin - pixels.yMin) / pixels.height);
                        float right = Mathf.Clamp01((safe.xMax - pixels.xMin) / pixels.width);
                        float top = Mathf.Clamp01((safe.yMax - pixels.yMin) / pixels.height);
                        if (right > left && top > bottom)
                            available = Rect.MinMaxRect(viewport.xMin + left * viewport.width, viewport.yMin + bottom * viewport.height,
                                viewport.xMin + right * viewport.width, viewport.yMin + top * viewport.height);
                    }
                }
                float scale = Mathf.Min(available.width / designSize.x, available.height / designSize.y);
                if (scale <= 0) return;
                Center(rect); SetSize(rect, designSize);
                SetPosition(rect, available.center - viewport.center);
                var targetScale = new Vector3(scale, scale, 1);
                if (rect.localScale != targetScale) rect.localScale = targetScale;
                if (background != null) {
                    Center(background);
                    Vector2 size = viewport.size / scale;
                    var image = background.GetComponent<Image>();
                    if (image != null && image.sprite != null) {
                        float aspect = image.sprite.rect.width / image.sprite.rect.height;
                        if (size.x / size.y > aspect) size.y = size.x / aspect; else size.x = size.y * aspect;
                    }
                    SetSize(background, size);
                    SetPosition(background, -rect.anchoredPosition / scale);
                    if (background.localScale != Vector3.one) background.localScale = Vector3.one;
                }
            } finally { fitting = false; }
        }
        static void Center(RectTransform target) {
            var center = new Vector2(.5f, .5f);
            if (target.anchorMin != center) target.anchorMin = center;
            if (target.anchorMax != center) target.anchorMax = center;
            if (target.pivot != center) target.pivot = center;
        }
        static void SetSize(RectTransform target, Vector2 size) { if (target.sizeDelta != size) target.sizeDelta = size; }
        static void SetPosition(RectTransform target, Vector2 position) { if (target.anchoredPosition != position) target.anchoredPosition = position; }
        void OnDisable() { Canvas.willRenderCanvases -= Fit; tracker.Clear(); tracked = false; }
    }
}
