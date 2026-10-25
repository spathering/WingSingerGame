using UnityEngine;

namespace WingSinger {
    public sealed class BreathCharacterAnimator : MonoBehaviour {
        public SpriteRenderer target;
        public CharacterSpriteClip cruise, rise, descend;
        public CharacterSpriteClip cruiseToRise, cruiseToDescend;
        [Min(0.01f)] public float returnToCruiseSeconds = 0.28f;
        public CharacterSpriteClip CurrentClip => current;
        public bool IsReturning => returnClip != null;
        double clock;
        CharacterSpriteClip current;
        CharacterSpriteClip returnClip;
        float returnClock;
        int movementDirection;

        public void ResetAnimation() {
            clock = 0; current = cruise;
            returnClip = null; returnClock = 0; movementDirection = 0;
            if (target != null && cruise != null) target.sprite = cruise.Sample(0, true);
        }

        public void Present(float deltaTime, float verticalMovement) {
            float dt = Mathf.Max(0, deltaTime);
            int direction = verticalMovement > 0.001f ? 1 : verticalMovement < -0.001f ? -1 : 0;
            if (direction != 0) {
                // New movement can interrupt the return; never wait for an old animation to finish.
                returnClip = null;
            } else if (movementDirection != 0) {
                returnClip = movementDirection > 0 ? cruiseToRise : cruiseToDescend;
                returnClock = 0;
            }
            movementDirection = direction;
            if (returnClip != null && target != null) {
                float duration = Mathf.Max(0.01f, returnToCruiseSeconds);
                returnClock += dt;
                if (returnClock < duration) {
                    current = returnClip;
                    // Existing clips go from cruise to tilt. Reverse them to straighten the bird.
                    target.sprite = returnClip.Sample(returnClip.Duration * (1 - returnClock / duration), false);
                    return;
                }
                returnClip = null;
            }
            var clip = direction > 0 ? rise : direction < 0 ? descend : cruise;
            if (clip == null || target == null) return;
            if (current != clip) { current = clip; clock = 0; }
            else clock += dt;
            target.sprite = clip.Sample(clock, true);
        }

        void OnDisable() => ResetAnimation();
    }
}
