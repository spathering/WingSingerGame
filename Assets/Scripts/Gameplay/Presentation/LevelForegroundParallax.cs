using UnityEngine;

namespace WingSinger {
    // Absolute camera displacement avoids accumulated drift and runs after Cinemachine.
    [DefaultExecutionOrder(10001)]
    public sealed class LevelForegroundParallax : MonoBehaviour {
        [Min(1)] public float parallaxSpeed = 1.65f;
        [Tooltip("Fixed world offset aligning the decorative ground with the tree layer. Does not follow camera Y or zoom.")]
        public float worldVerticalOffset = -2.8f;
        Camera targetCamera;
        BreathLevelManager sessionSource;
        float originCameraX;

        public void BeginSession(Camera camera, BreathLevelManager manager, float cameraX) {
            ResetPresentation();
            targetCamera = camera; sessionSource = manager; originCameraX = cameraX;
        }

        public void ResetPresentation() {
            transform.localPosition = Vector3.zero;
            targetCamera = null; sessionSource = null;
        }

        public void Sync() {
            if (targetCamera == null || sessionSource == null || sessionSource.Session == null || sessionSource.IsPaused) return;
            float offset = (targetCamera.transform.position.x - originCameraX) * (Mathf.Max(1, parallaxSpeed) - 1);
            Vector3 displacement = Vector3.left * offset;
            displacement.y = worldVerticalOffset;
            transform.localPosition = transform.parent != null ? transform.parent.InverseTransformVector(displacement) : displacement;
        }

        void LateUpdate() { Sync(); }
        void OnDisable() { ResetPresentation(); }
    }
}
