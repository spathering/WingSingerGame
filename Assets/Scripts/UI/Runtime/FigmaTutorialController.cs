using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    public sealed class FigmaTutorialController : MonoBehaviour {
        public GameObject[] slides;
        public Button next, chooseLevel;
        public int CurrentSlide { get; private set; }
        public int SlideCount => slides == null ? 0 : slides.Length;
        UIPageViewport viewport;
        void OnEnable() {
            CurrentSlide = 0; Present();
            next.onClick.AddListener(Advance); chooseLevel.onClick.AddListener(Choose);
        }
        void OnDisable() { next.onClick.RemoveListener(Advance); chooseLevel.onClick.RemoveListener(Choose); }
        void LateUpdate() {
            if(viewport==null)viewport=GetComponent<UIPageViewport>();
            if(viewport==null||viewport.background==null)return;
            // Include the extra background visible beside the authored layout on wide screens.
            // The top navigation strip stays available for returning to Settings.
            var r=next.GetComponent<RectTransform>();float width=viewport.background.rect.width;
            r.sizeDelta=new Vector2(width,Mathf.Max(1668,viewport.background.rect.height)-180);
            r.anchoredPosition=new Vector2(1210+viewport.background.anchoredPosition.x-width*.5f,-180);
        }
        public void Advance() { if (CurrentSlide + 1 < SlideCount) { CurrentSlide++; Present(); } }
        void Present() {
            for (int i = 0; i < SlideCount; i++) slides[i].SetActive(i == CurrentSlide);
            next.gameObject.SetActive(CurrentSlide + 1 < SlideCount);
        }
        void Choose() { GetComponentInParent<UIScreenRouter>().Route(UIRouteAction.Navigate, UIScreenId.LevelSelect); }
    }
}
