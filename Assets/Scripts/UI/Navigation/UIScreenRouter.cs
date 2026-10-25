using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace WingSinger.UI {
    public interface IUIRouteHandler {
        bool HandleRoute(UIRouteAction action, UIScreenId destination, int value);
    }
    // UI-only navigation. Gameplay integrations call the existing level manager;
    // this router never loads scenes or creates another gameplay session model.
    public sealed class UIScreenRouter : MonoBehaviour {
        public UIPageView[] pages;
        public UIScreenId initialScreen = UIScreenId.HomeDashboard;
        public bool enablePreviewShortcuts = true;
        [Tooltip("Explicit gallery mode; Start Flight previews HUD art instead of claiming to run a level.")]
        public bool referencePreviewMode = true;
        public UnityEvent<int> gameplayRequested = new UnityEvent<int>();
        public UnityEvent controllerCheckRequested = new UnityEvent();
        public UIScreenId CurrentScreen { get; private set; }
        public int SelectedLevel { get; private set; } = 4;
        public IUIRouteHandler RouteHandler { get; set; }
        public event Action<UIScreenId> ScreenChanged;
        readonly Dictionary<UIScreenId, UIPageView> lookup = new Dictionary<UIScreenId, UIPageView>();
        readonly List<UIScreenId> history = new List<UIScreenId>();
        bool practicing;

        void Awake() { Initialize(); }
        public void Initialize() {
            lookup.Clear(); history.Clear();
            if (pages == null || pages.Length == 0) pages = GetComponentsInChildren<UIPageView>(true);
            foreach (var page in pages) {
                if (page == null || lookup.ContainsKey(page.screenId))
                    throw new InvalidOperationException("UI page is missing or duplicated.");
                lookup.Add(page.screenId, page);
            }
            Show(initialScreen, false);
        }
        public void Show(UIScreenId id, bool remember = true) {
            if (!lookup.ContainsKey(id)) throw new ArgumentException("UI screen is not registered: " + id);
            if (remember && lookup.ContainsKey(CurrentScreen) && id != CurrentScreen) {
                if (history.Count == 32) history.RemoveAt(0);
                history.Add(CurrentScreen);
            }
            CurrentScreen = id;
            foreach (var pair in lookup) pair.Value.gameObject.SetActive(pair.Key == id);
            ScreenChanged?.Invoke(id);
        }
        public void SetSelectedLevel(int level) { SelectedLevel = Mathf.Max(1, level); }
        public void ClearHistory() { history.Clear(); }
        public UIPageView Page(UIScreenId id) => lookup.TryGetValue(id, out var page) ? page : null;
        public void Back() {
            if (history.Count == 0) { Show(initialScreen, false); return; }
            int last = history.Count - 1;
            var id = history[last]; history.RemoveAt(last); Show(id, false);
        }
        public void Route(UIRouteAction action, UIScreenId destination = UIScreenId.HomeDashboard, int level = 0) {
            if (RouteHandler != null && RouteHandler.HandleRoute(action, destination, level)) return;
            switch (action) {
                case UIRouteAction.Navigate: Show(destination); break;
                case UIRouteAction.Back:
                case UIRouteAction.Resume: Back(); break;
                case UIRouteAction.SelectLevel: SelectedLevel = Mathf.Clamp(level, 1, 12); Show(UIScreenId.ReadyForTakeoff); break;
                case UIRouteAction.TryRhythm: practicing = true; Show(UIScreenId.PracticeInhale); break;
                case UIRouteAction.StartFlight:
                    practicing = false; gameplayRequested.Invoke(SelectedLevel);
                    if (referencePreviewMode) Show(UIScreenId.PracticeFollow);
                    break;
                case UIRouteAction.Pause: Show(UIScreenId.FlightPaused); break;
                case UIRouteAction.Restart: Show(practicing ? UIScreenId.PracticeInhale : UIScreenId.PracticeFollow); break;
                case UIRouteAction.NextLevel: SelectedLevel = Mathf.Min(12, SelectedLevel + 1); Show(UIScreenId.ReadyForTakeoff); break;
                case UIRouteAction.CheckConnection: controllerCheckRequested.Invoke(); break;
                case UIRouteAction.AdvancePractice:
                    if (CurrentScreen == UIScreenId.PracticeInhale) Show(UIScreenId.PracticeExhale);
                    else if (CurrentScreen == UIScreenId.PracticeExhale) Show(UIScreenId.PracticeFollow);
                    else if (CurrentScreen == UIScreenId.PracticeFollow) Show(practicing ? UIScreenId.PracticeComplete : UIScreenId.FlightComplete);
                    break;
            }
        }
        void Update() {
            if (!enablePreviewShortcuts) return;
            if (Input.GetKeyDown(KeyCode.Space)) Route(UIRouteAction.AdvancePractice);
            if (Input.GetKeyDown(KeyCode.Escape)) {
                if (CurrentScreen == UIScreenId.PracticeInhale || CurrentScreen == UIScreenId.PracticeExhale || CurrentScreen == UIScreenId.PracticeFollow)
                    Route(UIRouteAction.Pause);
                else Back();
            }
        }
    }
}
