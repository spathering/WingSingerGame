using UnityEngine;
using TMPro;

namespace WingSinger.UI {
    public sealed class UIPageView : MonoBehaviour {
        public UIScreenId screenId;
        [Tooltip("Reference artwork contains illustrative values, not live hardware or saved progress.")]
        public bool usesReferencePreviewData = true;
        public TMP_Text[] selectedLevelLabels;
        void OnEnable() {
            if (!usesReferencePreviewData) return;
            var router=GetComponentInParent<UIScreenRouter>();
            if(router==null || selectedLevelLabels==null) return;
            string title=router.SelectedLevel==4 ? "Level 4 - Gentle Breeze" : "Level "+router.SelectedLevel;
            if(screenId==UIScreenId.LevelSelect) title=title.ToUpperInvariant();
            foreach(var label in selectedLevelLabels) if(label!=null) label.text=title;
        }
    }
}
