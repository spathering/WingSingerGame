using UnityEngine;

namespace WingSinger {
    public sealed class BreathCharacterAnimator : MonoBehaviour {
        public SpriteRenderer target;
        public CharacterSpriteClip cruise, rise, descend;
        public CharacterSpriteClip cruiseToRise, cruiseToDescend;
        [Min(0.01f)] public float returnToCruiseSeconds = 0.28f;
        [Min(0.01f)] public float turnSmoothSeconds = .15f;
        [Min(0)] public float floatAmplitude = .08f;
        [Min(0)] public float floatFrequency = .55f;
        [Min(0)] public float floatRollDegrees = 1.2f;
        public CharacterSpriteClip CurrentClip => current;
        public bool IsReturning => movementDirection == 0 && Mathf.Abs(pose) > .005f;
        public float Pose => pose;
        double clock;
        CharacterSpriteClip current;
        float pose, poseVelocity;
        int movementDirection;
        Vector3 restPosition;
        Quaternion restRotation;
        bool capturedRestPose;

        void CaptureRestPose() {
            if (capturedRestPose || target == null) return;
            restPosition = target.transform.localPosition; restRotation = target.transform.localRotation;
            capturedRestPose = true;
        }

        public void ResetAnimation() {
            CaptureRestPose(); clock = 0; current = cruise;
            pose = poseVelocity = 0; movementDirection = 0;
            if (target == null) return;
            target.transform.localPosition = restPosition; target.transform.localRotation = restRotation;
            if (cruise != null) target.sprite = cruise.Sample(0, true);
        }

        public void Present(float deltaTime, float verticalMovement, bool reduceMotion = false) {
            if (target == null) return;
            CaptureRestPose();
            float dt = Mathf.Max(0, deltaTime);
            if (dt <= 0) return;
            clock += dt;
            movementDirection = !reduceMotion && verticalMovement > .001f ? 1 : !reduceMotion && verticalMovement < -.001f ? -1 : 0;
            if (reduceMotion) { pose = poseVelocity = 0; }
            else {
                pose = Mathf.SmoothDamp(pose, movementDirection, ref poseVelocity,
                    movementDirection == 0 ? returnToCruiseSeconds * .5f : turnSmoothSeconds, Mathf.Infinity, dt);
                if (Mathf.Abs(pose - movementDirection) < .005f && Mathf.Abs(poseVelocity) < .05f) { pose = movementDirection; poseVelocity = 0; }
            }
            var transition = pose >= 0 ? cruiseToRise : cruiseToDescend;
            if (transition != null && Mathf.Abs(pose) > .005f && Mathf.Abs(pose) < .995f) {
                current = transition;
                target.sprite = transition.Sample(transition.Duration * Mathf.Abs(pose), false);
            } else {
                current = pose >= .995f ? rise : pose <= -.995f ? descend : cruise;
                if (current != null) target.sprite = current.Sample(clock, true);
            }
            float phase = (float)(clock * floatFrequency * Mathf.PI * 2);
            target.transform.localPosition = restPosition + Vector3.up * (reduceMotion ? 0 : Mathf.Sin(phase) * floatAmplitude);
            target.transform.localRotation = restRotation * Quaternion.Euler(0, 0, reduceMotion ? 0 : Mathf.Sin(phase * .7f) * floatRollDegrees);
        }

        void OnDisable() => ResetAnimation();
    }
}
