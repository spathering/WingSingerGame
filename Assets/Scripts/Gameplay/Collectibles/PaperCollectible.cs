using PrimeTween;
using UnityEngine;

namespace WingSinger {
    public sealed class PaperCollectible : MonoBehaviour {
        public SpriteRenderer visual;
        public CharacterSpriteClip clip;
        public float visualScale = 0.36f;
        public bool Collected { get; private set; }
        public bool Missed { get; private set; }
        public int Id { get; private set; }
        public double RouteTime { get; private set; }
        Tween shrink;
        Transform absorptionTarget;

        public void ResetPaper(int id, double routeTime, Vector3 localPosition) {
            StopFeedback();
            Id = id; RouteTime = routeTime; Collected = Missed = false;
            transform.localPosition = localPosition;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * visualScale;
            visual.color = Color.white;
            visual.sprite = clip.Sample(0, true);
            gameObject.SetActive(false);
        }

        public void Present(double guideTime, double elapsed) {
            if (Missed || (Collected && !shrink.isAlive)) { gameObject.SetActive(false); return; }
            bool revealed = guideTime + 0.000001 >= RouteTime;
            gameObject.SetActive(revealed);
            if (revealed) visual.sprite = clip.Sample(elapsed - RouteTime, true);
        }

        public bool IntersectsSweep(Vector3 from, Vector3 to, float radius) {
            if (Collected || Missed || !gameObject.activeInHierarchy) return false;
            Vector2 a = from, b = to, point = transform.position;
            Vector2 segment = b - a;
            float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude) : 0;
            return (point - (a + segment * t)).sqrMagnitude <= radius * radius;
        }

        public void Collect(Transform target = null) {
            if (Collected) return;
            Collected = true;
            absorptionTarget = target;
            visual.color = Color.white;
            shrink = Tween.Scale(visual.transform, 0, 0.22f, Ease.InQuad);
        }

        void LateUpdate() {
            if (Collected && shrink.isAlive && absorptionTarget != null)
                visual.transform.position = Vector3.Lerp(visual.transform.position, absorptionTarget.position, 1 - Mathf.Exp(-18 * Time.deltaTime));
        }

        public void RecycleIfBehindCamera(float cameraLeft) {
            if (!Collected && gameObject.activeInHierarchy && visual.bounds.max.x < cameraLeft - .1f) MarkMissed();
        }

        public void MarkMissed() { Missed = true; gameObject.SetActive(false); }
        public void StopFeedback() { if (shrink.isAlive) shrink.Stop(); absorptionTarget = null; }
        void OnDisable() => StopFeedback();
    }
}
