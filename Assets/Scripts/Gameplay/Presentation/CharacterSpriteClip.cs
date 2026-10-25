using UnityEngine;

namespace WingSinger {
    // Sprites are sub-assets of this clip, so references survive builds without an editor importer.
    public sealed class CharacterSpriteClip : ScriptableObject {
        public Sprite[] frames;
        public float[] frameSeconds;
        public string sourceFile;
        public string sourceSha256;
        float[] frameEnds;
        public float Duration { get; private set; }

        void OnEnable() => RebuildTiming();

        public void RebuildTiming() {
            Duration = 0;
            frameEnds = new float[frames == null ? 0 : frames.Length];
            for (int i = 0; i < frameEnds.Length; i++) {
                Duration += frameSeconds != null && i < frameSeconds.Length
                    ? Mathf.Max(0.01f, frameSeconds[i]) : 0.05f;
                frameEnds[i] = Duration;
            }
        }

        public Sprite Sample(double seconds, bool loop) {
            if (frames == null || frames.Length == 0) return null;
            if (frameEnds == null || frameEnds.Length != frames.Length) RebuildTiming();
            if (seconds <= 0) return frames[0];
            double time = loop ? seconds % Duration : System.Math.Min(seconds, Duration);
            int low = 0, high = frames.Length - 1;
            while (low < high) {
                int middle = (low + high) / 2;
                if (time < frameEnds[middle]) high = middle;
                else low = middle + 1;
            }
            return frames[low];
        }
    }
}
