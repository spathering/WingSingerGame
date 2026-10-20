using UnityEngine;

namespace WingSinger {
    public sealed class BreathCharacterAnimator : MonoBehaviour {
        public SpriteRenderer target;
        public CharacterSpriteClip cruise, rise, descend;
        public CharacterSpriteClip CurrentClip => current;
        double clock;
        CharacterSpriteClip current;

        public void ResetAnimation() {
            clock = 0; current = cruise;
            if (target != null && cruise != null) target.sprite = cruise.Sample(0, true);
        }

        public void Present(float deltaTime, float verticalMovement) {
            // Direction comes from physical movement, including returning to the middle lane.
            var clip = verticalMovement > 0.001f ? rise : verticalMovement < -0.001f ? descend : cruise;
            if (clip == null || target == null) return;
            if (current != clip) { current = clip; clock = 0; }
            else clock += Mathf.Max(0, deltaTime);
            target.sprite = clip.Sample(clock, true);
        }

        void OnDisable() => ResetAnimation();
    }
}
