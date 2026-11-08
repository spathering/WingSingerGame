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
        float entranceStartX, entranceDuration;
        public bool IsEntering { get; private set; }
        public bool ReduceMotion { get; private set; }
        public void SetReduceMotion(bool reduce) {
            ReduceMotion = reduce;
            if (!reduce) return;
            StopAnimation(); IsEntering = false;
            var position = body.localPosition; position.x = 0; position.y = destinationY; body.localPosition = position;
        }

        public void ResetPlayer(Vector3 spawn, float middleHeight = 0) {
            StopAnimation();
            origin = spawn;
            transform.position = spawn;
            body.localPosition = Vector3.up * middleHeight;
            visualLane = BreathLane.Middle;
            destinationY = middleHeight;
            animationMovingAllowed = false;
            entranceStartX = entranceDuration = 0; IsEntering = false;
            if (characterAnimation != null) characterAnimation.ResetAnimation();
            cameraTarget.position = new Vector3(spawn.x, spawn.y, 0);
            if (highlight != null) highlight.enabled = false;
        }

        public void BeginEntrance(float viewportLeftX, float duration) {
            entranceDuration = duration;
            IsEntering = duration > 0 && !ReduceMotion;
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
                float y = settings.LaneHeight((int)visualLane);
                destinationY = y;
                if (settings.laneTransitionSeconds <= 0 || ReduceMotion) body.localPosition = new Vector3(body.localPosition.x, y, 0);
                else laneTween = Tween.LocalPositionY(body, y, settings.laneTransitionSeconds, Ease.InOutSine);
            }
        }

        // PrimeTween updates the body first; arrival begins the tilt-to-cruise transition.
        void LateUpdate() {
            if (characterAnimation != null)
                characterAnimation.Present(Time.deltaTime,
                    !ReduceMotion && animationMovingAllowed && laneTween.isAlive ? destinationY - body.localPosition.y : 0, ReduceMotion);
        }

        public void StopAnimation() { if (laneTween.isAlive) laneTween.Stop(); }
        void OnDisable() => StopAnimation();
    }
}
