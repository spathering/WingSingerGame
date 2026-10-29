using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class GameplayNavigationValidation {
        const string Key = "WingSinger.GameplayNavigation.Validation";
        const string Report = "Docs/Validation/GameplayNavigationValidation.json";
        [Serializable] class Result { public string date; public bool success; public string[] checks; public string error; public string input = "Simulated USB transport through the production serial receiver; no physical ESP32 claim."; }
        static readonly List<string> checks = new List<string>();
        static IEnumerator routine;
        static BreathUIFlowController flow;
        static SamplePort port;
        static GameplayNavigationValidation() { EditorApplication.update += Tick; }
        [MenuItem("WingSinger/UI/Validate Gameplay Navigation")]
        public static void Begin() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Stop Play and save open scenes first.");
            EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var sceneFlow = Object.FindObjectOfType<BreathUIFlowController>();
            if (sceneFlow == null) throw new InvalidOperationException("Connect navigation first.");
            // Unsaved test overrides: never open physical ports or write the user's save data.
            sceneFlow.profileKey = ""; sceneFlow.sensorInput.enabled = false;
            sceneFlow.sensorInput.loadSavedConfiguration = false;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Stage", 0);
            SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetInt(Key + "Stage", 0) == 1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key, false);
                // Discard test overrides without saving them.
                EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 0)); return;
            }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 120) { Finish(false, "Navigation validation timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "Stage", 0) != 0) return;
            try {
                if (routine == null) { checks.Clear(); routine = Run(); }
                if (!routine.MoveNext()) Finish(true, null);
            } catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Check(bool condition, string description) { if (!condition) throw new Exception(description); checks.Add(description); }
        static void Click(string name) {
            var page = flow.router.Page(flow.router.CurrentScreen);
            var button = page.GetComponentsInChildren<Button>().First(b => b.name == name);
            Check(button.interactable, "Button enabled: " + name); button.onClick.Invoke();
            Check(flow.router.pages.Count(p => p.gameObject.activeSelf) == 1, "Exactly one UI page after " + name);
        }
        static IEnumerator Run() {
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            flow = Object.FindObjectOfType<BreathUIFlowController>();
            Check(flow != null && flow.Progress != null && flow.profileKey == "", "Production flow starts with an isolated in-memory profile");
            var router = flow.router; var manager = flow.levelManager; var input = flow.sensorInput;
            int[] levelIds = manager.levels.Select(l => l.GetInstanceID()).ToArray();
            Check(router.CurrentScreen == UIScreenId.HomeClean && !flow.IsSessionActive && manager.levels.All(l => !l.gameObject.activeSelf), "New profile opens clean home with no active game level");
            Check(!router.referencePreviewMode && !router.enablePreviewShortcuts && !manager.allowKeyboardShortcuts, "Production disables gallery shortcuts and level hotkeys");
            manager.ValidateCatalog();
            Check(manager.catalog != null && manager.catalog.Count == 2 && manager.catalog.levels.All(d => d.prefab != null && d.settings == d.prefab.settings && d.sourceScenePath == GameplayNavigationSetup.ScenePath), "Two level definition SOs reference the original gameplay Prefabs and breathing settings");
            var buttons = router.GetComponentsInChildren<Button>(true);
            Check(buttons.Length > 50 && buttons.All(b => b.GetComponents<UIInteractionTween>().Length == 1), "Every existing UI button has exactly one PrimeTween interaction component");
            var hoverButton = router.Page(router.CurrentScreen).GetComponentsInChildren<Button>().Single(b => b.name == "ViewLevels");
            var effect = hoverButton.GetComponent<UIInteractionTween>();
            var rect = hoverButton.GetComponent<RectTransform>();
            Vector3 restScale = rect.localScale; Vector2 restPosition = rect.anchoredPosition;
            Vector3 restCenter = rect.TransformPoint(rect.rect.center);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            double effectUntil = EditorApplication.timeSinceStartup + .22;
            Time.timeScale = 0; effect.OnPointerEnter(pointer);
            while (EditorApplication.timeSinceStartup < effectUntil) yield return null;
            Check(effect.CurrentScale > 1.03f && Time.timeScale == 0, "PrimeTween hover feedback advances on unscaled time while gameplay is paused");
            Check(Vector3.Distance(restCenter, rect.TransformPoint(rect.rect.center)) < .02f, "Button scales around its visual center without changing the authored pivot");
            effect.OnPointerDown(pointer); effectUntil = EditorApplication.timeSinceStartup + .14;
            while (EditorApplication.timeSinceStartup < effectUntil) yield return null;
            Check(effect.CurrentScale < .97f, "Left press gives a subtle shrink feedback");
            effect.OnPointerUp(pointer); effectUntil = EditorApplication.timeSinceStartup + .28;
            while (EditorApplication.timeSinceStartup < effectUntil) yield return null;
            Check(Mathf.Abs(effect.CurrentScale - effect.hoverScale) < .001f, "Pointer release returns to the hover scale");
            effect.OnPointerExit(pointer); effectUntil = EditorApplication.timeSinceStartup + .22;
            while (EditorApplication.timeSinceStartup < effectUntil) yield return null;
            Check(rect.localScale == restScale && rect.anchoredPosition == restPosition, "Pointer exit restores the exact authored transform");
            effect.OnPointerEnter(pointer); hoverButton.interactable = false; yield return null; yield return null;
            Check(effect.CurrentScale == 1 && !effect.IsAnimating, "A newly disabled button cancels its tween and resets its visual state");
            effect.OnPointerDown(pointer); Check(!effect.IsAnimating, "Disabled buttons cannot start press tweens");
            hoverButton.interactable = true; effect.OnPointerEnter(pointer); hoverButton.gameObject.SetActive(false);
            Check(rect.localScale == restScale && rect.anchoredPosition == restPosition && !effect.IsAnimating, "Page or button disable stops feedback without leaving scale or position behind");
            hoverButton.gameObject.SetActive(true); Time.timeScale = 1;
            flow.bindings.reduceMotion.isOn = true; effect.OnPointerEnter(pointer);
            Check(effect.CurrentScale == 1 && !effect.IsAnimating && effect.ReduceMotion, "Reduce Motion immediately suppresses all optional button scaling");
            port = new SamplePort();
            var client = new BreathSensorClient(new BreathSensorConnectionOptions("COM99", 1, .05, 5), null, (p, b) => port);
            input.bluetoothProtocol = BreathBluetoothProtocol.HandshakeUart; // This fixture exercises legacy line framing.
            input.Connect(client);
            double until = EditorApplication.timeSinceStartup + 3;
            while (!flow.SensorFresh && EditorApplication.timeSinceStartup < until) yield return null;
            Check(flow.SensorFresh && port.HelloCount == 1, "Production receiver identifies simulated USB device and receives fresh samples");
            Click("Settings");
            var settingsTitle = router.Page(UIScreenId.Settings).transform.Find("Content/SettingsPanel/Title").GetComponent<TMPro.TMP_Text>();
            Color originalTitleColor = settingsTitle.color;
            var musicGO = new GameObject("Validation Music", typeof(AudioSource)); var soundGO = new GameObject("Validation SFX", typeof(AudioSource));
            flow.bindings.musicSources = new[] { musicGO.GetComponent<AudioSource>() }; flow.bindings.soundSources = new[] { soundGO.GetComponent<AudioSource>() };
            flow.bindings.music.value = .35f; flow.bindings.sound.value = .45f;
            Check(Mathf.Approximately(flow.Progress.Data.musicVolume, .35f) && Mathf.Approximately(musicGO.GetComponent<AudioSource>().volume, .35f) && Mathf.Approximately(soundGO.GetComponent<AudioSource>().volume, .45f), "Audio sliders save independent gains and apply them to assigned audio sources");
            flow.bindings.reduceMotion.isOn = true; flow.bindings.highContrast.isOn = true;
            Check(manager.player.ReduceMotion && manager.levels.All(l => l.reduceMotion) && flow.Progress.Data.highContrast, "Display switches apply to gameplay and persist in the profile");
            Click("Controller");
            Check(flow.Text(UIValueKind.Battery) == "--" && flow.Text(UIValueKind.Signal) == "USB", "Controller page does not invent battery or wireless telemetry");
            int reconnects = 0; flow.ReconnectOverride = () => reconnects++;
            Click("CheckConnection"); Check(reconnects == 1, "Connection check invokes one reconnect operation");
            Click("Done"); Click("Back"); Check(router.CurrentScreen == UIScreenId.HomeClean, "Controller and settings return to their original caller");
            Click("ViewLevels");
            Check(flow.AvailableLevels == 2 && flow.bindings.levelCards.Take(2).All(b => b.interactable) && flow.bindings.levelCards.Skip(2).All(b => !b.interactable), "Both existing catalog levels are selectable immediately; unused reference slots stay disabled");
            Capture("LevelSelect");
            flow.bindings.levelCards[1].onClick.Invoke();
            Check(router.CurrentScreen == UIScreenId.ReadyForTakeoff && flow.SelectedDefinition.levelId == "slow-breath" && flow.Text(UIValueKind.InhaleSeconds) == "4 s" && flow.Text(UIValueKind.ExhaleSeconds) == "6 s", "Second level card reads its title and rhythm from the selected SO");
            Check(router.Page(UIScreenId.ReadyForTakeoff).transform.Find("Content/ReadyPanel/LevelTitle/Label").GetComponent<TMPro.TMP_Text>().text == "Level 2 - Slow Breath", "Ready page visibly displays the selected catalog level rather than reference text");
            Capture("ReadySlowBreath"); Click("StartFlight");
            Check(manager.CurrentLevel == manager.levels[1] && manager.Session.Pattern.Duration == 60 && manager.Session.Pattern.Inhale == 4 && manager.Session.Pattern.Exhale == 6, "Selecting Slow Breath starts the original 60-second gameplay level with its own settings");
            for (int i = 0; i < 4; i++) yield return null;
            Capture("LiveSlowBreath"); Click("Pause"); Click("Levels");
            Check(flow.Progress.Data.records.Count == 0 && manager.levels.All(l => !l.gameObject.activeSelf), "Leaving the second level before completion preserves the profile and cleans its state");
            router.Route(UIRouteAction.SelectLevel, level: 12); Check(router.CurrentScreen == UIScreenId.LevelSelect, "Invalid or locked selection cannot start a level");
            flow.bindings.levelCards[0].onClick.Invoke(); Check(router.CurrentScreen == UIScreenId.ReadyForTakeoff && router.SelectedLevel == 1, "Actual level card opens ready page for level one");
            Check(router.Page(UIScreenId.ReadyForTakeoff).transform.Find("Content/ReadyPanel/LevelTitle/Label").GetComponent<TMPro.TMP_Text>().text == "Level 1 - First Flight", "Ready title switches back correctly when selecting the first level");
            input.exhaleMinimum = input.inhaleMaximum; flow.Refresh();
            Check(!flow.InputReady && !router.Page(UIScreenId.ReadyForTakeoff).GetComponentsInChildren<UIRouteButton>().Single(b => b.action == UIRouteAction.StartFlight).GetComponent<Button>().interactable, "Invalid sensor thresholds block takeoff");
            input.exhaleMinimum = 1000; flow.Refresh(); Click("StartFlight");
            Check(flow.IsSessionActive && manager.CurrentIndex == 0 && manager.levels.Count(l => l.gameObject.activeSelf) == 1, "Takeoff activates exactly one existing level and starts its real session");
            Check(!router.Page(UIScreenId.PracticeFollow).transform.Find("Background").gameObject.activeSelf && !router.Page(UIScreenId.PracticeFollow).transform.Find("Content").gameObject.activeSelf, "Live flight HUD reveals real gameplay instead of reference artwork");
            Check(!manager.player.IsEntering, "Reduce Motion suppresses the optional fly-in animation");
            for (int i = 0; i < 4; i++) yield return null;
            Capture("LiveFlight");
            var session = manager.Session; double pausedAt = session.Elapsed; Vector3 pausedPosition = manager.player.body.position;
            Time.timeScale = .75f; Click("Pause");
            Check(flow.IsPaused && Time.timeScale == 0, "Pause freezes gameplay with explicit session state");
            long sequence = input.ReceiverSnapshot.SampleSequence;
            until = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(session.Elapsed == pausedAt && manager.player.body.position == pausedPosition && input.ReceiverSnapshot.SampleSequence > sequence, "Paused game clock and player stay frozen while USB reception continues");
            Capture("PausedFlight");
            Click("Settings"); Click("Controller"); Click("Done"); Click("Back");
            Check(router.CurrentScreen == UIScreenId.FlightPaused && manager.Session == session && Time.timeScale == 0, "Nested settings preserve the paused session and return to Pause");
            Click("Resume"); Check(manager.Session == session && !flow.IsPaused && Time.timeScale == .75f && input.Receiver == client, "Resume restores original time scale and reuses session and serial receiver");
            Time.timeScale = 1; yield return null;
            Check(session.Elapsed > pausedAt, "Session time resumes after Resume");
            Click("Pause"); Click("Restart");
            Check(manager.Session != session && manager.Session.Elapsed == 0 && Time.timeScale == 1 && input.Receiver == client, "Restart resets the current session without reconnecting USB");
            Check(manager.levels.Select(l => l.GetInstanceID()).SequenceEqual(levelIds), "Restart reuses the existing level root objects");
            Click("Pause"); Click("Levels");
            Check(!flow.IsSessionActive && manager.levels.All(l => !l.gameObject.activeSelf) && manager.levels.SelectMany(l => l.Papers).All(p => !p.gameObject.activeSelf) && Time.timeScale == 1, "Leaving a paused game clears session and level collection feedback");
            flow.bindings.levelCards[0].onClick.Invoke(); Click("StartFlight");
            session = manager.Session;
            session.Advance(session.Pattern.Lead + 2, BreathLane.Upper); session.TryCollect(999999);
            int collected = session.CollectedCount;
            session.Advance(session.Pattern.Duration, BreathLane.Upper);
            for (int i = 0; i < 3; i++) yield return null;
            Check(router.CurrentScreen == UIScreenId.FlightComplete && !flow.IsSessionActive, "Actual session completion opens result page and exits the level");
            Check(flow.Progress.Data.records.Count == 1 && flow.CurrentResult.collected == collected && Mathf.Approximately(flow.CurrentResult.seconds, (float)session.Pattern.Duration), "Completion saves one record with real session duration and collections");
            Check(flow.Progress.Data.unlockedLevels == 2 && flow.bindings.levelCards[1].interactable && flow.CurrentResult.levelId == "first-flight", "Completion stores a stable catalog ID and preserves existing level availability");
            Capture("FlightResult");
            for (int i = 0; i < 3; i++) yield return null;
            Check(flow.Progress.Data.records.Count == 1, "Additional finished frames cannot duplicate a record");
            Click("NextLevel"); Check(router.SelectedLevel == 2 && router.CurrentScreen == UIScreenId.ReadyForTakeoff, "Next Level opens the real next level's preparation page");
            Click("StartFlight"); Check(manager.CurrentIndex == 1 && manager.levels.Count(l => l.gameObject.activeSelf) == 1, "Second level starts through the same manager");
            session = manager.Session; session.Advance(session.Pattern.Lead + session.Pattern.Duration, BreathLane.Lower);
            for (int i = 0; i < 3; i++) yield return null;
            Check(flow.Progress.Data.records.Count == 2 && flow.Progress.Data.unlockedLevels == 2, "Last level completion does not unlock nonexistent levels");
            Check(!router.Page(UIScreenId.FlightComplete).GetComponentsInChildren<UIRouteButton>().Single(b => b.action == UIRouteAction.NextLevel).GetComponent<Button>().interactable, "Next Level is disabled after the final available level");
            Click("Levels"); Click("Back"); Check(router.CurrentScreen == UIScreenId.HomeDashboard, "Completed activity selects the dashboard home variant");
            Click("FlightLog");
            Check(flow.Progress.WeeklyFlights == 2 && flow.Progress.TodaySessions == 2 && flow.Progress.Streak == 1, "Flight log derives week, day and streak statistics from saved records");
            Capture("FlightLog");
            flow.bindings.recentRows[1].onClick.Invoke();
            Check(flow.ViewingHistory && flow.CurrentResult.levelNumber == 1 && flow.Progress.Data.records.Count == 2 && !flow.IsSessionActive, "Historical result is read-only and does not finish or replay a session");
            var originalCatalog = manager.catalog; var originalLevels = manager.levels;
            var reorderedCatalog = Object.Instantiate(originalCatalog); reorderedCatalog.levels = originalCatalog.levels.Reverse().ToArray();
            manager.catalog = reorderedCatalog; manager.levels = originalLevels.Reverse().ToArray(); manager.ValidateCatalog();
            Check(flow.ResultLevelNumber == 2 && manager.DefinitionAt(flow.ResultLevelNumber - 1).levelId == "first-flight", "Historical replay resolves stable level IDs even after catalog reordering");
            manager.catalog = originalCatalog; manager.levels = originalLevels; Object.Destroy(reorderedCatalog);
            Click("PlayAgain"); Check(manager.CurrentIndex == 0 && !flow.ViewingHistory && flow.CurrentResult == null, "Historical Play Again starts the recorded level with a fresh session");
            Click("Pause"); Click("Levels"); flow.bindings.levelCards[0].onClick.Invoke(); Click("TryRhythm");
            Check(flow.IsPracticing && router.CurrentScreen == UIScreenId.PracticeInhale && flow.IsSessionActive, "Try Rhythm runs actual input practice on an existing level");
            router.Route(UIRouteAction.AdvancePractice); Check(router.CurrentScreen == UIScreenId.PracticeInhale, "Reference instruction click cannot skip production practice");
            session = manager.Session; session.Advance(session.Pattern.Lead + session.Pattern.Preparation + .1, BreathLane.Middle);
            var tick = typeof(BreathUIFlowController).GetMethod("TickPractice", BindingFlags.Instance | BindingFlags.NonPublic);
            port.Value = 300; until = EditorApplication.timeSinceStartup + 2;
            while (input.ReadLane() != BreathLane.Upper && EditorApplication.timeSinceStartup < until) yield return null;
            Check(input.ReadLane() == BreathLane.Upper, "USB sample 300 maps to inhale / upper lane");
            tick.Invoke(flow, new object[] { (float)session.Pattern.Inhale + .1f });
            Check(router.CurrentScreen == UIScreenId.PracticeExhale, "Sustained actual inhale advances to exhale practice");
            Click("Pause"); Click("Settings"); Click("Back"); Click("Resume");
            Check(router.CurrentScreen == UIScreenId.PracticeExhale && manager.Session == session, "Tutorial pause and resume preserve the exact stage");
            port.Value = 1300; until = EditorApplication.timeSinceStartup + 2;
            while (input.ReadLane() != BreathLane.Lower && EditorApplication.timeSinceStartup < until) yield return null;
            Check(input.ReadLane() == BreathLane.Lower, "USB sample 1300 maps to exhale / lower lane");
            tick.Invoke(flow, new object[] { (float)session.Pattern.Exhale + .1f });
            Check(router.CurrentScreen == UIScreenId.PracticeFollow, "Sustained actual exhale advances to live follow practice");
            session.Advance(.001, BreathLane.Lower);
            session.TryCollect(999991); session.TryCollect(999992); session.TryCollect(999993);
            tick.Invoke(flow, new object[] { .01f });
            Check(router.CurrentScreen == UIScreenId.PracticeComplete && flow.Progress.Data.records.Count == 3 && flow.CurrentResult.practice, "Three new collections complete practice and record it separately");
            Check(flow.Progress.WeeklyFlights == 2 && flow.Progress.TodaySessions == 3, "Practice counts toward daily activity without inflating completed flights");
            Click("ChooseLevel");
            var saved = new FlightProgressStore(""); saved.Read(flow.Progress.Export());
            Check(saved.Data.records.Count == 3 && saved.Data.unlockedLevels == 2 && saved.Data.reduceMotion && Mathf.Approximately(saved.Data.musicVolume, .35f), "Progress and settings survive a JSON save/load round trip");
            saved.Read("{broken json}"); Check(saved.Data.records.Count == 0 && saved.Data.unlockedLevels == 1, "Malformed local save safely falls back to a new profile");
            flow.bindings.levelCards[0].onClick.Invoke(); Click("StartFlight"); Click("Pause");
            flow.enabled = false;
            Check(Time.timeScale == 1 && manager.Session == null && manager.levels.All(l => !l.gameObject.activeSelf) && input.Receiver == client, "Disabling navigation releases its pause and gameplay state without taking ownership of serial input");
            flow.enabled = true; yield return null;
            Check(router.CurrentScreen == UIScreenId.HomeDashboard && router.RouteHandler == flow, "Re-enabling navigation restores routes and home");
            flow.bindings.highContrast.isOn = false;
            Check(settingsTitle.color == originalTitleColor, "High Contrast can restore original colors after navigation is re-enabled");
            Check(manager.levels.Select(l => l.GetInstanceID()).SequenceEqual(levelIds) && SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == GameplayNavigationSetup.ScenePath, "All routes preserve a single Unity scene and the original level instances");
            input.Disconnect(); Check(!client.IsRunning && port.Disposed, "Test receiver shuts down without leaving a serial worker");
            Object.Destroy(musicGO); Object.Destroy(soundGO);
        }
        static void Capture(string name) {
            Directory.CreateDirectory("Docs/Design/UI/RuntimePreviews");
            var canvas = flow.GetComponent<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; var cameraTarget = camera.targetTexture;
            var mode = canvas.renderMode; var canvasCamera = canvas.worldCamera; float distance = canvas.planeDistance, aspect = camera.aspect;
            var panoramas = Object.FindObjectsOfType<LoopingPanorama>();
            int sortingOrder = canvas.sortingOrder;
            Texture2D image = null;
            try {
                target.Create(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
                canvas.sortingOrder = 32000;
                camera.targetTexture = target; camera.aspect = 1280f / 720;
                foreach (var panorama in panoramas) panorama.Sync();
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes("Docs/Design/UI/RuntimePreviews/" + name + ".png", image.EncodeToPNG());
            } finally {
                canvas.renderMode = mode; canvas.worldCamera = canvasCamera; canvas.planeDistance = distance;
                canvas.sortingOrder = sortingOrder;
                camera.targetTexture = cameraTarget; camera.aspect = aspect; RenderTexture.active = previous;
                foreach (var panorama in panoramas) panorama.Sync();
                if (image != null) Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            }
        }
        static void Finish(bool success, string error) {
            if (flow != null) { flow.ExitSession(); flow.sensorInput.Disconnect(); }
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, JsonUtility.ToJson(new Result { date = DateTimeOffset.Now.ToString("O"), success = success, checks = checks.ToArray(), error = error }, true));
            if (success) Debug.Log("WingSinger gameplay navigation passed: " + checks.Count + " checks."); else Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Stage", 1); SessionState.SetInt(Key + "Exit", success ? 0 : 1); EditorApplication.isPlaying = false;
        }
        sealed class SamplePort : IBreathSerialTransport {
            public volatile int Value = 700;
            public volatile bool Disposed;
            public int HelloCount;
            bool identified;
            public int Read(byte[] buffer) {
                Thread.Sleep(10); if (Disposed) return 0;
                string line = identified ? Value + "\n" : BreathSensorProtocol.Identity + "\n"; identified = true;
                var bytes = Encoding.ASCII.GetBytes(line); Array.Copy(bytes, buffer, bytes.Length); return bytes.Length;
            }
            public void WriteLine(string line) { if (line == BreathSensorProtocol.Hello) Interlocked.Increment(ref HelloCount); }
            public void CancelRead() { Disposed = true; }
            public void Dispose() { Disposed = true; }
        }
    }
}
