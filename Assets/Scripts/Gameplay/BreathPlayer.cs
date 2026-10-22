using PrimeTween;
using UnityEngine;

namespace WingSinger {
    public sealed class BreathPlayer : MonoBehaviour {
        public Transform body;
        public Transform cameraTarget;
        public SpriteRenderer highlight;
        public BreathCharacterAnimator characterAnimation;
        Tween laneTween;
        BreathLane visualLane;
        Vector3 origin;
        float destinationY;
        bool animationMovingAllowed;
        float collectionFlash;
        float entranceStartX, entranceDuration;
        public bool IsEntering { get; private set; }
        public void PlayCollectionFeedback() { collectionFlash = 0.22f; }

        public void ResetPlayer(Vector3 spawn) {
            StopAnimation();
            origin = spawn;
            transform.position = spawn;
            body.localPosition = Vector3.zero;
            visualLane = BreathLane.Middle;
            destinationY = 0;
            animationMovingAllowed = false;
            collectionFlash = 0;
            entranceStartX = entranceDuration = 0; IsEntering = false;
            if (characterAnimation != null) characterAnimation.ResetAnimation();
            cameraTarget.position = new Vector3(spawn.x, spawn.y, 0);
            highlight.color = new Color(0.4f, 0.85f, 1, 0.18f);
        }

        public void BeginEntrance(float viewportLeftX, float duration) {
            entranceDuration = duration;
            IsEntering = duration > 0;
            if (!IsEntering) return;
            float halfWidth = characterAnimation != null && characterAnimation.target != null
                ? characterAnimation.target.bounds.extents.x : 1;
            Vector3 start = body.position;
            start.x = viewportLeftX - halfWidth - 0.3f;
            entranceStartX = body.parent.InverseTransformPoint(start).x;
            var local = body.localPosition; local.x = entranceStartX; body.localPosition = local;
        }

        public void Present(BreathSession session, BreathLevelSettings settings) {
            animationMovingAllowed = session.Phase == BreathPhase.Running;
            // Approach the route origin while previewing instead of waiting there after the fly-in.
            // The root and camera keep the same speed across both entrance completion and gameplay start.
            double travelTime = entranceDuration > 0 ? session.Elapsed - session.Pattern.Lead : session.PlayTime;
            transform.position = origin + Vector3.right * ((float)travelTime * settings.moveSpeed);
            cameraTarget.position = new Vector3(transform.position.x, origin.y, 0);
            if (IsEntering) {
                float t = Mathf.Clamp01((float)session.Elapsed / entranceDuration);
                var local = body.localPosition;
                local.x = Mathf.Lerp(entranceStartX, 0, Mathf.Sin(t * Mathf.PI * 0.5f));
                body.localPosition = local;
                IsEntering = t < 1;
            }
            if (visualLane != session.PlayerLane) {
                StopAnimation();
                visualLane = session.PlayerLane;
                float y = (int)visualLane * settings.laneSpacing;
                destinationY = y;
                if (settings.laneTransitionSeconds <= 0) body.localPosition = new Vector3(body.localPosition.x, y, 0);
                else laneTween = Tween.LocalPositionY(body, y, settings.laneTransitionSeconds, Ease.InOutSine);
            }
        }

        // PrimeTween updates the body first; arrival begins the tilt-to-cruise transition.
        void LateUpdate() {
            collectionFlash = Mathf.Max(0, collectionFlash - Time.deltaTime);
            highlight.color = collectionFlash > 0 ? new Color(0.35f, 1, 0.7f, 0.65f) : new Color(0.4f, 0.85f, 1, 0.18f);
            if (characterAnimation != null)
                characterAnimation.Present(Time.deltaTime,
                    animationMovingAllowed && laneTween.isAlive ? destinationY - body.localPosition.y : 0);
        }

        public void StopAnimation() { if (laneTween.isAlive) laneTween.Stop(); }
        void OnDisable() => StopAnimation();
    }
}
