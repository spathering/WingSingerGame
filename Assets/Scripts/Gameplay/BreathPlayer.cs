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

        public void ResetPlayer(Vector3 spawn) {
            StopAnimation();
            origin = spawn;
            transform.position = spawn;
            body.localPosition = Vector3.zero;
            visualLane = BreathLane.Middle;
            destinationY = 0;
            animationMovingAllowed = false;
            if (characterAnimation != null) characterAnimation.ResetAnimation();
            cameraTarget.position = new Vector3(spawn.x, spawn.y, 0);
            highlight.color = new Color(0.4f, 0.85f, 1, 0.18f);
        }

        public void Present(BreathSession session, BreathLevelSettings settings) {
            animationMovingAllowed = session.Phase == BreathPhase.Running;
            transform.position = origin + Vector3.right * ((float)session.PlayTime * settings.moveSpeed);
            cameraTarget.position = new Vector3(transform.position.x, origin.y, 0);
            if (visualLane != session.PlayerLane) {
                StopAnimation();
                visualLane = session.PlayerLane;
                float y = (int)visualLane * settings.laneSpacing;
                destinationY = y;
                if (settings.laneTransitionSeconds <= 0) body.localPosition = new Vector3(0, y, 0);
                else laneTween = Tween.LocalPositionY(body, y, settings.laneTransitionSeconds, Ease.InOutSine);
            }
            highlight.color = session.IsMatching ? new Color(0.35f, 1f, 0.7f, 0.45f)
                : new Color(0.4f, 0.85f, 1, 0.18f);
        }

        // PrimeTween updates the body first; arrival must show cruise in the same rendered frame.
        void LateUpdate() {
            if (characterAnimation != null)
                characterAnimation.Present(Time.deltaTime,
                    animationMovingAllowed && laneTween.isAlive ? destinationY - body.localPosition.y : 0);
        }

        public void StopAnimation() { if (laneTween.isAlive) laneTween.Stop(); }
        void OnDisable() => StopAnimation();
    }
}
