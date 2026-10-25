using UnityEngine;

namespace WingSinger {
    // Runs after the Cinemachine brain; UVs derive from world X rather than accumulated motion.
    [DefaultExecutionOrder(10000)]
    public sealed class LoopingPanorama : MonoBehaviour {
        public Camera targetCamera;
        public MeshRenderer backgroundRenderer;
        public float originWorldX;
        public float depth = 10;
        public float WorldPeriod { get; private set; }
        public float HorizontalPhase { get; private set; }
        MaterialPropertyBlock properties;
        static readonly int TextureST = Shader.PropertyToID("_MainTex_ST");

        public void Sync() {
            if (targetCamera == null || backgroundRenderer == null || !targetCamera.orthographic) return;
            var material = backgroundRenderer.sharedMaterial;
            var texture = material == null ? null : material.mainTexture;
            if (texture == null) return;
            float height = targetCamera.orthographicSize * 2 + 0.02f;
            float width = height * targetCamera.aspect;
            WorldPeriod = height * texture.width / texture.height;
            float left = targetCamera.transform.position.x - width / 2;
            HorizontalPhase = Mathf.Repeat((left - originWorldX) / WorldPeriod, 1);
            transform.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, depth);
            transform.localScale = new Vector3(width, height, 1);
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetVector(TextureST, new Vector4(width / WorldPeriod, 1, HorizontalPhase, 0));
            backgroundRenderer.SetPropertyBlock(properties);
        }

        void LateUpdate() => Sync();
    }
}
