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

        public void ResetPaper(int id, double routeTime, Vector3 localPosition) {
            StopFeedback();
            Id = id; RouteTime = routeTime; Collected = Missed = false;
            transform.localPosition = localPosition;
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

        public void Collect() {
            Collected = true;
            visual.color = new Color(1.5f, 1.4f, 1.1f);
            shrink = Tween.Scale(visual.transform, 0, 0.18f, Ease.InQuad);
        }

        public void MarkMissed() { Missed = true; gameObject.SetActive(false); }
        public void StopFeedback() { if (shrink.isAlive) shrink.Stop(); }
        void OnDisable() => StopFeedback();
    }
}
