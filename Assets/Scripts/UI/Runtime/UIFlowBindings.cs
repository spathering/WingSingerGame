using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    public sealed class UIFlowBindings : MonoBehaviour {
        public Slider music, sound;
        public Toggle reduceMotion, highContrast;
        public AudioSource[] musicSources = Array.Empty<AudioSource>(), soundSources = Array.Empty<AudioSource>();
        public Button[] levelCards, recentRows;
        public Image[] levelStatus, weekTiles;
        public GameObject[] weekChecks;
        public Sprite completedIcon, unlockedIcon, lockedIcon;
        public bool useFigmaArtwork;
        UIRuntimeLabel[] labels;
        UIRuntimeProgress[] fills;
        UIRouteButton[] routes;
        TMP_Text[] texts;
        Color[] originalColors;
        UIInteractionTween[] interactions;
        Image[] thumbnails;
        FigmaSurface[] selectionBorders;
        BreathUIFlowController flow;
        bool settingsDirty;
        float saveAfter;
        public void Initialize(BreathUIFlowController owner) {
            Unbind(); flow = owner;
            labels = GetComponentsInChildren<UIRuntimeLabel>(true); fills = GetComponentsInChildren<UIRuntimeProgress>(true);
            routes = GetComponentsInChildren<UIRouteButton>(true);
            interactions = GetComponentsInChildren<UIInteractionTween>(true);
            thumbnails = new Image[levelCards.Length];
            for (int i = 0; i < levelCards.Length; i++) thumbnails[i] = levelCards[i].GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Thumbnail");
            selectionBorders=levelCards.Select(card=>card.GetComponentsInChildren<FigmaSurface>(true).FirstOrDefault(s=>s.name=="Thumbnail border")).ToArray();
            if (texts == null) {
                texts = GetComponentsInChildren<TMP_Text>(true);
                originalColors = new Color[texts.Length]; for (int i = 0; i < texts.Length; i++) originalColors[i] = texts[i].color;
            }
            var d = flow.Progress.Data;
            music.SetValueWithoutNotify(d.musicVolume); sound.SetValueWithoutNotify(d.soundVolume);
            reduceMotion.SetIsOnWithoutNotify(d.reduceMotion); highContrast.SetIsOnWithoutNotify(d.highContrast);
            music.GetComponent<UISliderValue>()?.Present(d.musicVolume); sound.GetComponent<UISliderValue>()?.Present(d.soundVolume);
            reduceMotion.GetComponent<UIToggleValue>()?.Present(d.reduceMotion); highContrast.GetComponent<UIToggleValue>()?.Present(d.highContrast);
            music.onValueChanged.AddListener(Music); sound.onValueChanged.AddListener(Sound);
            reduceMotion.onValueChanged.AddListener(Motion); highContrast.onValueChanged.AddListener(Contrast);
            ApplyAudio(); ApplyContrast(); SetReduceMotion(d.reduceMotion);
        }
        public void Unbind() {
            if (music != null) music.onValueChanged.RemoveListener(Music);
            if (sound != null) sound.onValueChanged.RemoveListener(Sound);
            if (reduceMotion != null) reduceMotion.onValueChanged.RemoveListener(Motion);
            if (highContrast != null) highContrast.onValueChanged.RemoveListener(Contrast);
        }
        void SaveSoon() { settingsDirty = true; saveAfter = Time.unscaledTime + .3f; }
        void Update() { if (settingsDirty && Time.unscaledTime >= saveAfter) { settingsDirty = false; flow.Progress.Save(); } }
        void Music(float v) { flow.Progress.Data.musicVolume = v; ApplyAudio(); SaveSoon(); }
        void Sound(float v) { flow.Progress.Data.soundVolume = v; ApplyAudio(); SaveSoon(); }
        void Motion(bool v) { flow.Progress.Data.reduceMotion = v; flow.ApplyMotion(); SaveSoon(); }
        void Contrast(bool v) { flow.Progress.Data.highContrast = v; ApplyContrast(); SaveSoon(); }
        void ApplyAudio() {
            foreach (var source in musicSources) if (source != null) source.volume = flow.Progress.Data.musicVolume;
            foreach (var source in soundSources) if (source != null) source.volume = flow.Progress.Data.soundVolume;
        }
        void ApplyContrast() {
            for (int i = 0; i < texts.Length; i++) {
                var c = originalColors[i];
                texts[i].color = flow.Progress.Data.highContrast && Mathf.Max(c.r, c.g, c.b) < .85f ? new Color(.15f, .12f, .1f, c.a) : c;
            }
        }
        public void SetReduceMotion(bool value) {
            if (interactions == null) return;
            foreach (var interaction in interactions) interaction.SetReduceMotion(value);
        }
        public void Present(BreathUIFlowController owner) {
            if (labels == null) return;
            foreach (var label in labels) label.Present(owner);
            foreach (var fill in fills) fill.Present(owner);
            for (int i = 0; i < levelCards.Length; i++) {
                bool selectable = owner.CanSelect(i + 1);
                levelCards[i].interactable = selectable;
                var definition = owner.levelManager.DefinitionAt(i);
                bool complete = definition == null ? owner.Progress.Completed(i + 1) : owner.Progress.Completed(definition.levelId, i + 1);
                levelStatus[i].sprite = selectable && complete ? completedIcon : selectable ? unlockedIcon : lockedIcon;
                if(useFigmaArtwork && selectionBorders[i]!=null) {
                    bool selected=selectable && owner.router.SelectedLevel==i+1;
                    selectionBorders[i].borderColor=selected?new Color(.91f,.57f,.16f):new Color(.65f,.66f,.59f);
                    selectionBorders[i].borderWidth=selected?5:3.2f;selectionBorders[i].SetVerticesDirty();
                }
                if (!useFigmaArtwork && thumbnails[i] != null && definition != null && definition.thumbnail != null) thumbnails[i].sprite = definition.thumbnail;
            }
            var recent = owner.Progress.RecentFlights;
            for (int i = 0; i < recentRows.Length; i++) {
                recentRows[i].interactable = i < recent.Length;
                recentRows[i].gameObject.SetActive(i < recent.Length || i == 0);
            }
            for (int i = 0; i < weekTiles.Length; i++) {
                bool done = owner.Progress.HasActivity(FlightProgressStore.WeekStart.AddDays(i));
                weekTiles[i].color = done ? new Color(.68f, .84f, .45f) : new Color(.96f, .89f, .77f);
                weekChecks[i].SetActive(done);
            }
            foreach (var route in routes) {
                var button = route.GetComponent<Button>();
                switch (route.action) {
                    case UIRouteAction.StartFlight: button.interactable = owner.CanSelect(owner.router.SelectedLevel) && owner.InputReady; break;
                    case UIRouteAction.ContinueSelection: button.interactable = owner.CanSelect(owner.router.SelectedLevel); break;
                    case UIRouteAction.NextLevel: button.interactable = owner.ResultLevelNumber > 0 && owner.CanSelect(owner.ResultLevelNumber + 1); break;
                    case UIRouteAction.Restart: button.interactable = owner.ResultLevelNumber > 0 && owner.CanSelect(owner.ResultLevelNumber); break;
                    case UIRouteAction.Resume: button.interactable = owner.IsSessionActive; break;
                    case UIRouteAction.Pause: button.interactable = owner.IsSessionActive; break;
                    case UIRouteAction.AdvancePractice: button.interactable = false; break;
                }
            }
        }
    }
}
