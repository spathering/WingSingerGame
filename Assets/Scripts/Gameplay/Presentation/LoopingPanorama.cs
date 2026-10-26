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
        public float WorldPeriod { get; private set; }
        public float HorizontalPhase { get; private set; }
        MaterialPropertyBlock properties;
        static readonly int TextureST = Shader.PropertyToID("_MainTex_ST");

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
            HorizontalPhase = Mathf.Repeat((left - originWorldX) / WorldPeriod, 1);
            transform.position = new Vector3(targetCamera.transform.position.x,
                targetCamera.transform.position.y + (viewHeight - height) * verticalAnchor * 0.5f, depth);
            transform.localScale = new Vector3(width, height, 1);
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetVector(TextureST, new Vector4(width / WorldPeriod, 1, HorizontalPhase, 0));
            backgroundRenderer.SetPropertyBlock(properties);
        }

        void LateUpdate() => Sync();
    }
}
