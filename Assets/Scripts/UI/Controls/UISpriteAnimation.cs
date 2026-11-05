using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    [RequireComponent(typeof(Image))]
    public sealed class UISpriteAnimation : MonoBehaviour {
        public CharacterSpriteClip clip;
        Image image;
        double elapsed;
        Material blendMaterial, originalMaterial;
        public float FrameBlend { get; private set; }
        void OnEnable() {
            image = GetComponent<Image>(); elapsed = 0;
            originalMaterial = image.material;
            var shader = Resources.Load<Shader>("WingSingerUIInterpolation");
            if (shader != null) { blendMaterial = new Material(shader); image.material = blendMaterial; }
            Present();
        }
        void Update() {
            elapsed += Time.unscaledDeltaTime;
            Present();
        }
        void Present() {
            if (image == null || clip == null) return;
            clip.SamplePair(elapsed, out var first, out var next, out float blend);
            image.sprite = first; FrameBlend = blend;
            if (blendMaterial != null && first != null && next != null) {
                blendMaterial.SetVector("_FrameRect", Rect(first)); blendMaterial.SetVector("_NextRect", Rect(next));
                blendMaterial.SetTexture("_NextTex", next.texture); blendMaterial.SetFloat("_FrameBlend", blend);
            }
        }
        static Vector4 Rect(Sprite sprite) {
            var rect = sprite.rect; var texture = sprite.texture;
            return new Vector4(rect.width / texture.width, rect.height / texture.height, rect.x / texture.width, rect.y / texture.height);
        }
        void OnDisable() {
            elapsed = 0; Present();
            if (image != null) image.material = originalMaterial;
            if (blendMaterial != null) Destroy(blendMaterial);
            blendMaterial = null;
        }
    }
}
