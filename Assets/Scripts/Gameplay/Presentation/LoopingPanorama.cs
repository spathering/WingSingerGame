using UnityEngine;

namespace WingSinger {
    // Runs after the Cinemachine brain; UVs derive from world X rather than accumulated motion.
    [DefaultExecutionOrder(10000)]
    public sealed class LoopingPanorama : MonoBehaviour {
        public Camera targetCamera;
        public MeshRenderer backgroundRenderer;
        public float originWorldX;
        public float depth = 10;
        [Min(0)] public float parallaxSpeed = 1;
        public float parallaxAnchorX;
        [Range(0.05f, 1)] public float heightFraction = 1;
        [Range(-1, 1)] public float verticalAnchor = -1;
        [Tooltip("Original image width / height; zero uses the imported texture dimensions.")]
        public float sourceAspectRatio;
        [Tooltip("Start each gameplay session at the left of the image using the actual viewport, rather than a saved reference aspect.")]
        public bool anchorToSessionViewport;
        BreathSession panoramaSession;
        float sessionOriginWorldX;
        [Header("Optional foreground animation")]
        public Texture2D[] animationFrames;
        [Min(1)] public float animationFramesPerSecond = 20;
        [Range(.01f, 1)] public float animationVerticalScale = 1;
        public BreathLevelManager animationSessionSource;
        double animationSeconds;
        BreathSession animationSession;
        public int AnimationFrame { get; private set; }
        public float FrameBlend { get; private set; }
        public float WorldPeriod { get; private set; }
        public float HorizontalPhase { get; private set; }
        MaterialPropertyBlock properties;
        static readonly int TextureST = Shader.PropertyToID("_MainTex_ST");
        static readonly int MainTexture = Shader.PropertyToID("_MainTex");

        public void Sync() {
            if (targetCamera == null || backgroundRenderer == null || !targetCamera.orthographic) return;
            var material = backgroundRenderer.sharedMaterial;
            var texture = material == null ? null : material.mainTexture;
            if (texture == null) return;
            float viewHeight = targetCamera.orthographicSize * 2 + 0.02f;
            float height = viewHeight * Mathf.Clamp(heightFraction, 0.05f, 1);
            float width = viewHeight * targetCamera.aspect;
            WorldPeriod = height * (sourceAspectRatio > 0 ? sourceAspectRatio : (float)texture.width / texture.height);
            float left = (targetCamera.transform.position.x - parallaxAnchorX) * Mathf.Max(0, parallaxSpeed)
                + parallaxAnchorX - width / 2;
            float origin = originWorldX;
            var currentSession = animationSessionSource != null ? animationSessionSource.Session : null;
            if (anchorToSessionViewport && currentSession != null) {
                if (!ReferenceEquals(panoramaSession, currentSession)) {
                    // Executed after Cinemachine: the saved scene aspect may differ from the Game view.
                    // Skip four texture texels so the source image's dark outer border stays off screen.
                    sessionOriginWorldX = left - WorldPeriod * 4f / texture.width;
                    panoramaSession = currentSession;
                }
                origin = sessionOriginWorldX;
            } else panoramaSession = null;
            HorizontalPhase = Mathf.Repeat((left - origin) / WorldPeriod, 1);
            transform.position = new Vector3(targetCamera.transform.position.x,
                targetCamera.transform.position.y + (viewHeight - height) * verticalAnchor * 0.5f, depth);
            transform.localScale = new Vector3(width, height, 1);
            if (properties == null) properties = new MaterialPropertyBlock();
            float vScale = animationFrames != null && animationFrames.Length > 0 ? animationVerticalScale : 1;
            properties.SetVector(TextureST, new Vector4(width / WorldPeriod, vScale, HorizontalPhase, 1 - vScale));
            if (animationFrames != null && animationFrames.Length > 0) {
                AnimationFrame = (int)(animationSeconds * Mathf.Max(1, animationFramesPerSecond) % animationFrames.Length);
                FrameBlend = (float)(animationSeconds * Mathf.Max(1, animationFramesPerSecond) % 1);
                if (animationFrames[AnimationFrame] != null) properties.SetTexture(MainTexture, animationFrames[AnimationFrame]);
                properties.SetTexture("_NextTex", animationFrames[(AnimationFrame + 1) % animationFrames.Length]);
                properties.SetFloat("_FrameBlend", FrameBlend);
            }
            backgroundRenderer.SetPropertyBlock(properties);
        }

        void LateUpdate() {
            if (animationSessionSource != null) {
                var current = animationSessionSource.Session;
                if (!ReferenceEquals(animationSession, current)) { animationSeconds = 0; animationSession = current; }
                else if (current != null && !animationSessionSource.IsPaused) animationSeconds += Time.deltaTime;
            } else animationSeconds += Time.deltaTime;
            Sync();
        }
        void OnDisable() { animationSeconds = 0; animationSession = null; panoramaSession = null; AnimationFrame = 0; }
    }
}
