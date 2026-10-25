using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    public static class GameplayNavigationSetup {
        public const string ScenePath = "Assets/Scenes/BreathDemo.unity";
        [MenuItem("WingSinger/UI/Connect Navigation to Gameplay")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play and save open scenes before updating navigation.");
            Directory.CreateDirectory("Temp/NavigationBackup");
            string backup = "Temp/NavigationBackup/BreathDemo.before-navigation.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            if (manager == null) throw new InvalidOperationException("The gameplay scene has no level manager.");
            var existing = Object.FindObjectOfType<BreathUIFlowController>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UIFlowRoot.prefab"), scene);
            root.name = "GameUI";
            var router = root.GetComponent<UIScreenRouter>();
            var flow = root.AddComponent<BreathUIFlowController>();
            var bindings = root.AddComponent<UIFlowBindings>();
            flow.router = router; flow.levelManager = manager; flow.sensorInput = Object.FindObjectOfType<ArduinoBreathInput>(); flow.bindings = bindings;
            manager.startAutomatically = false; manager.allowKeyboardShortcuts = false;
            router.referencePreviewMode = false; router.enablePreviewShortcuts = false; router.initialScreen = UIScreenId.HomeClean;
            if (manager.hud != null) { manager.hud.gameObject.SetActive(false); manager.hud = null; }
            foreach (var page in router.pages) {
                page.usesReferencePreviewData = false;
                foreach (var label in page.selectedLevelLabels) Bind(label, UIValueKind.LevelTitle);
                BindOptional(page, "Content/LevelCount/Label", UIValueKind.WorldLevels);
                BindOptional(page, "Footer/TodayPanel/Streak/Label", UIValueKind.Streak);
                BindOptional(page, "Footer/TodayPanel/Controller/Label", UIValueKind.Connection);
                Stats(page.transform.Find("Footer/TodayPanel"), UIValueKind.InhaleSeconds, UIValueKind.ExhaleSeconds, UIValueKind.TodayPractice);
                Stats(page.transform.Find("Content/ReadyPanel"), UIValueKind.InhaleSeconds, UIValueKind.ExhaleSeconds, UIValueKind.TodayPractice);
                BindOptional(page, "Content/ReadyPanel/Checklist0/Text", UIValueKind.InputReadiness);
                BindOptional(page, "Content/ReadyPanel/LevelTitle/Label", UIValueKind.LevelTitle);
                BindOptional(page, "Content/ReadyPanel/InhaleHint", UIValueKind.InhaleHint);
                BindOptional(page, "Content/ReadyPanel/ExhaleHint", UIValueKind.ExhaleHint);
                BindOptional(page, "Content/PausePanel/ProgressPanel/Cycles", UIValueKind.ResultCycles);
                Progress(page.transform.Find("Content/PausePanel/ProgressPanel"), false);
                Stats(page.transform.Find("Content/CompletePanel"), UIValueKind.ResultSeconds, UIValueKind.ResultCycles, UIValueKind.ResultRhythm);
                BindOptional(page, "Content/CompletePanel/TodayProgress/Value", UIValueKind.TodaySessions);
                Progress(page.transform.Find("Content/CompletePanel/TodayProgress"), true);
                var hud = page.transform.Find("Header/PracticeHUD");
                if (hud != null) {
                    Bind(hud.Find("Step/Label").GetComponent<TMP_Text>(), UIValueKind.HudStep);
                    Bind(hud.Find("Stars/Label").GetComponent<TMP_Text>(), UIValueKind.Collected);
                    var badge = hud.Find("ConnectionBadge/Label") ?? hud.Find("Connection/Label");
                    Bind(badge.GetComponent<TMP_Text>(), UIValueKind.Connection);
                }
                BindOptional(page, "Footer/Instruction/Title", UIValueKind.InstructionTitle);
                BindOptional(page, "Footer/Instruction/Subtitle", UIValueKind.InstructionDetail);
                BindOptional(page, "Content/PracticeCompletePanel/Count", UIValueKind.Collected);
            }
            var settings = router.pages.Single(p => p.screenId == UIScreenId.Settings);
            bindings.music = Find<Slider>(settings, "Content/SettingsPanel/AudioSection/Music");
            bindings.sound = Find<Slider>(settings, "Content/SettingsPanel/AudioSection/SoundEffects");
            bindings.reduceMotion = Find<Toggle>(settings, "Content/SettingsPanel/DisplaySection/ReduceMotion");
            bindings.highContrast = Find<Toggle>(settings, "Content/SettingsPanel/DisplaySection/HighContrast");
            // Existing scene has no music/SFX sources; future sources are explicitly assigned here.
            BindOptional(settings, "Content/SettingsPanel/SupportSection/Controller/Value", UIValueKind.Connection);
            BindOptional(router.pages.Single(p => p.screenId == UIScreenId.LevelSelect), "Content/LevelsPanel/SelectedLevel/Description", UIValueKind.LevelDescription);
            var controller = router.pages.Single(p => p.screenId == UIScreenId.ControllerConnection);
            BindOptional(controller, "Content/ControllerPanel/Status/Label", UIValueKind.Connection);
            BindOptional(controller, "Content/ControllerPanel/BatteryValue", UIValueKind.Battery);
            BindOptional(controller, "Content/ControllerPanel/SignalValue", UIValueKind.Signal);
            Find<TMP_Text>(controller, "Content/ControllerPanel/DeviceName").text = "WingSinger USB Controller";
            Find<TMP_Text>(controller, "Content/ControllerPanel/SignalLabel").text = "Connection";
            var levels = router.pages.Single(p => p.screenId == UIScreenId.LevelSelect);
            bindings.levelCards = levels.GetComponentsInChildren<UIRouteButton>(true).Where(b => b.action == UIRouteAction.SelectLevel).OrderBy(b => b.levelNumber).Select(b => b.GetComponent<Button>()).ToArray();
            bindings.levelStatus = bindings.levelCards.Select(b => b.transform.Find("Status").GetComponent<Image>()).ToArray();
            foreach (var card in bindings.levelCards) {
                var outline = card.GetComponent<Outline>(); if (outline != null) Object.DestroyImmediate(outline);
                card.transform.Find("Number").GetComponent<TMP_Text>().color = new Color(.1f, .3f, .2f);
                card.transform.Find("Thumbnail").GetComponent<Image>().color = Color.white;
                card.transform.Find("StatusBackground").gameObject.SetActive(false);
            }
            foreach (string name in new[] { "WorldLeft", "WorldRight" }) {
                var button = levels.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name); if (button != null) button.interactable = false;
            }
            var log = router.pages.Single(p => p.screenId == UIScreenId.FlightLog);
            var summaries = log.transform.Find("Content/LogPanel").Cast<Transform>().Where(t => t.name.StartsWith("Summary", StringComparison.Ordinal)).ToArray();
            if (summaries.Length != 3) throw new InvalidOperationException("Missing flight log summaries.");
            var kinds = new[] { UIValueKind.StreakDays, UIValueKind.WeeklyFlights, UIValueKind.WeeklyMinutes };
            for (int i = 0; i < 3; i++) Bind(summaries[i].Find("Value").GetComponent<TMP_Text>(), kinds[i]);
            bindings.recentRows = new Button[3];
            for (int i = 0; i < 3; i++) {
                var row = Find<Button>(log, "Content/LogPanel/Recent/RecentFlight" + i); bindings.recentRows[i] = row;
                var route = row.GetComponent<UIRouteButton>(); route.action = UIRouteAction.ViewRecord; route.levelNumber = i;
                Bind(row.transform.Find("Title").GetComponent<TMP_Text>(), UIValueKind.RecentTitle, i);
                Bind(row.transform.Find("Detail").GetComponent<TMP_Text>(), UIValueKind.RecentDetail, i);
            }
            bindings.weekTiles = new Image[7]; bindings.weekChecks = new GameObject[7];
            for (int i = 0; i < 7; i++) {
                var tile = Find<Image>(log, "Content/LogPanel/Week/Complete" + i); bindings.weekTiles[i] = tile;
                var check = tile.transform.Find("Check");
                if (check == null) {
                    var go = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(tile.transform, false);
                    var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = new Vector2(47, 41);
                    var image = go.GetComponent<Image>(); image.sprite = Sprite("IconCheckWhite"); image.raycastTarget = false; check = go.transform;
                }
                bindings.weekChecks[i] = check.gameObject;
            }
            bindings.completedIcon = Sprite("IconCheck"); bindings.unlockedIcon = Sprite("IconPlay"); bindings.lockedIcon = Sprite("IconLock");
            var catalog = AssetDatabase.LoadAssetAtPath<BreathLevelCatalog>(UIIntegrationSetup.CatalogPath);
            if (catalog != null) UIIntegrationSetup.BindCatalog(flow, catalog);
            if (Object.FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            router.SetSelectedLevel(1); router.Initialize();
            foreach (var component in new Component[] { manager, router, flow, bindings }) {
                EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("WingSinger: connected 13 UI pages to the existing single-scene gameplay manager.");
        }
        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/" + name + ".png");
        static T Find<T>(UIPageView page, string path) where T : Component {
            var target = page.transform.Find(path);
            if (target == null || target.GetComponent<T>() == null) throw new InvalidOperationException(page.name + ": missing " + path);
            return target.GetComponent<T>();
        }
        static void Bind(TMP_Text text, UIValueKind kind, int index = 0) {
            var label = text.GetComponent<UIRuntimeLabel>() ?? text.gameObject.AddComponent<UIRuntimeLabel>(); label.value = kind; label.index = index;
            text.enableAutoSizing = true; text.fontSizeMax = text.fontSize; text.fontSizeMin = Mathf.Min(18, text.fontSize);
        }
        static void BindOptional(UIPageView page, string path, UIValueKind kind) {
            var target = page.transform.Find(path); if (target != null) Bind(target.GetComponent<TMP_Text>(), kind);
        }
        static void Stats(Transform parent, params UIValueKind[] kinds) {
            if (parent == null) return;
            var stats = parent.Cast<Transform>().Where(t => t.name == "StatCard").ToArray();
            if (stats.Length != kinds.Length) throw new InvalidOperationException("Missing stat cards: " + parent.name);
            for (int i = 0; i < stats.Length; i++) Bind(stats[i].Find("Value").GetComponent<TMP_Text>(), kinds[i]);
        }
        static void Progress(Transform parent, bool daily) {
            if (parent == null) return;
            var progress = parent.gameObject.AddComponent<UIRuntimeProgress>(); progress.dailyGoal = daily;
            progress.fill = parent.Find("Fill").GetComponent<RectTransform>(); progress.track = parent.Find("Track").GetComponent<RectTransform>();
        }
    }
}
