using TMPro;
using UnityEngine;

namespace WingSinger.UI {
    // Static five-glyph atlas; no dependency on installed fonts in the built game.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class HardwareStatusFont : MonoBehaviour {
        public TMP_FontAsset fallback;
        void Awake() {
            var text = GetComponent<TMP_Text>();
            if (fallback != null && text.font != null && !text.font.fallbackFontAssetTable.Contains(fallback))
                text.font.fallbackFontAssetTable.Add(fallback);
        }
    }
}
