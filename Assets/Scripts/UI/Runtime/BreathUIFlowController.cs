using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    // Runs after the level manager, including its final-frame collectible sweep.
    [DefaultExecutionOrder(100)]
    public sealed class BreathUIFlowController : MonoBehaviour, IUIRouteHandler {
        public UIScreenRouter router;
        public BreathLevelManager levelManager;
        public ArduinoBreathInput sensorInput;
        public string profileKey = "WingSinger.Progress.v1";
        public UIFlowBindings bindings;
        public FlightProgressStore Progress { get; private set; }
        public FlightRecord CurrentResult { get; private set; }
        public bool ViewingHistory { get; private set; }
        public bool IsPracticing { get; private set; }
        public bool IsSessionActive => levelManager != null && levelManager.Session != null;
        public bool IsPaused => IsSessionActive && levelManager.IsPaused;
        public bool SensorFresh => sensorInput != null && sensorInput.ReceiverSnapshot.IsFresh(sensorInput.sampleTimeoutSeconds);
        public float DailyProgress => Progress == null ? 0 : Mathf.Clamp01(Progress.TodaySessions / 6f);
        public float SessionProgress => !IsSessionActive ? 0 : (float)(levelManager.Session.PlayTime / levelManager.Session.Pattern.Duration);
        public int AvailableLevels => levelManager == null ? 0 : levelManager.LevelCount;
        public BreathLevelDefinition SelectedDefinition => levelManager.DefinitionAt(router.SelectedLevel - 1);
        public int ResultLevelNumber => CurrentResult == null ? router.SelectedLevel : ResolveRecordLevel(CurrentResult);
        public Action ReconnectOverride { get; set; }
        UIScreenId runningScreen;
        string sessionId;
        int practiceStage, followStartCount;
        float heldSeconds, previousTimeScale = 1, refreshAt;
        bool ownsPause;

        void OnEnable() {
            Attach();
            if (Progress != null && bindings != null) { bindings.Initialize(this); ShowHome(); }
        }
        void Attach() {
            if (router == null) return;
            router.RouteHandler = this;
            router.ScreenChanged -= OnScreenChanged;
            router.ScreenChanged += OnScreenChanged;
        }
        void Start() {
            if (router == null || levelManager == null || bindings == null) {
                Debug.LogError("WingSinger UI: missing navigation or gameplay bindings.", this); enabled = false; return;
            }
            Progress = new FlightProgressStore(profileKey);
            levelManager.startAutomatically = false;
            levelManager.allowKeyboardShortcuts = false;
            levelManager.StopSession();
            router.referencePreviewMode = false; router.enablePreviewShortcuts = false;
            router.SetSelectedLevel(1);
            bindings.Initialize(this);
            Attach(); ShowHome();
        }
        void Update() {
            if (Progress == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) {
                if (IsSessionActive && !IsPaused) Pause();
                else if (router.CurrentScreen == UIScreenId.FlightPaused) Resume();
                else if (router.CurrentScreen == UIScreenId.Settings || router.CurrentScreen == UIScreenId.ControllerConnection) router.Back();
            }
            if (Time.unscaledTime >= refreshAt) { Refresh(); refreshAt = Time.unscaledTime + .1f; }
        }
        void LateUpdate() {
            if (!IsSessionActive || IsPaused) return;
            if (IsPracticing) TickPractice(Time.deltaTime);
            if (IsSessionActive && levelManager.Session.Phase == BreathPhase.Finished) Finish();
        }
        public bool HandleRoute(UIRouteAction action, UIScreenId destination, int value) {
            if (Progress == null) return true;
            switch (action) {
                case UIRouteAction.SelectLevel:
                    if (CanSelect(value)) { router.SetSelectedLevel(value); router.Show(UIScreenId.ReadyForTakeoff); }
                    break;
                case UIRouteAction.StartFlight: StartSession(false); break;
                case UIRouteAction.TryRhythm: StartSession(true); break;
                case UIRouteAction.Pause: Pause(); break;
                case UIRouteAction.Resume: Resume(); break;
                case UIRouteAction.Restart:
                    if (CurrentResult != null) { if (ResultLevelNumber == 0) break; router.SetSelectedLevel(ResultLevelNumber); }
                    StartSession(IsPracticing && IsSessionActive); break;
                case UIRouteAction.NextLevel:
                    int next = ResultLevelNumber == 0 ? 0 : ResultLevelNumber + 1;
                    if (CanSelect(next)) { ExitSession(); router.SetSelectedLevel(next); router.Show(UIScreenId.ReadyForTakeoff); }
                    break;
                case UIRouteAction.Navigate: Navigate(destination); break;
                case UIRouteAction.Back: router.Back(); break;
                case UIRouteAction.CheckConnection:
                    if (ReconnectOverride != null) ReconnectOverride(); else if (sensorInput != null) sensorInput.Connect();
                    router.controllerCheckRequested.Invoke(); break;
                case UIRouteAction.ViewRecord:
                    var recent = Progress.RecentFlights;
                    if (value >= 0 && value < recent.Length) {
                        ExitSession(); CurrentResult = recent[value]; ViewingHistory = true;
                        router.SetSelectedLevel(Mathf.Max(1, ResolveRecordLevel(CurrentResult))); router.Show(UIScreenId.FlightComplete);
                    }
                    break;
                // Tutorial advances from actual input and collections, never from artwork clicks.
                case UIRouteAction.AdvancePractice: break;
            }
            Refresh(); return true;
        }
        void Navigate(UIScreenId target) {
            if (target == UIScreenId.HomeDashboard || target == UIScreenId.HomeClean) { ExitSession(); ShowHome(); return; }
            if (target == UIScreenId.Settings || target == UIScreenId.ControllerConnection) {
                if (IsSessionActive && !IsPaused) Pause();
                router.Show(target); return;
            }
            if (target == UIScreenId.LevelSelect || target == UIScreenId.FlightLog || target == UIScreenId.ReadyForTakeoff) {
                ExitSession(); router.Show(target);
            }
        }
        void ShowHome() {
            router.ClearHistory();
            router.Show(Progress.Data.records.Count > 0 ? UIScreenId.HomeDashboard : UIScreenId.HomeClean, false);
        }
        public bool CanSelect(int level) {
            if (level < 1 || level > AvailableLevels || Progress == null || levelManager.levels == null
                || level > levelManager.levels.Length || levelManager.levels[level - 1] == null || !levelManager.isActiveAndEnabled) return false;
            var definition = levelManager.DefinitionAt(level - 1);
            return (definition != null && definition.availableFromStart) || level <= Progress.Data.unlockedLevels;
        }
        int ResolveRecordLevel(FlightRecord record) {
            if (!string.IsNullOrEmpty(record.levelId) && levelManager.catalog != null) return levelManager.catalog.IndexOf(record.levelId) + 1;
            return record.levelNumber >= 1 && record.levelNumber <= AvailableLevels ? record.levelNumber : 0;
        }
        public bool InputReady => levelManager != null && (levelManager.input != sensorInput || SensorFresh && BreathSensorProtocol.AreThresholdsValid(sensorInput.inhaleMaximum, sensorInput.exhaleMinimum)
            || (sensorInput != null && !sensorInput.ReceiverSnapshot.Connected && sensorInput.keyboardWhenDisconnected && sensorInput.keyboardFallback != null));
        void StartSession(bool practice) {
            if (!CanSelect(router.SelectedLevel) || !InputReady) return;
            ExitSession();
            IsPracticing = practice; practiceStage = 0; heldSeconds = 0; followStartCount = 0;
            CurrentResult = null; ViewingHistory = false; sessionId = Guid.NewGuid().ToString("N");
            ApplyMotion();
            if (!levelManager.SwitchLevel(router.SelectedLevel - 1)) { IsPracticing = false; return; }
            runningScreen = practice ? UIScreenId.PracticeInhale : UIScreenId.PracticeFollow;
            router.ClearHistory(); router.Show(runningScreen, false);
            router.gameplayRequested.Invoke(router.SelectedLevel);
        }
        void Pause() {
            if (!IsSessionActive || IsPaused) return;
            runningScreen = router.CurrentScreen;
            previousTimeScale = Time.timeScale; ownsPause = true;
            levelManager.SetPaused(true); Time.timeScale = 0;
            router.Show(UIScreenId.FlightPaused);
        }
        void ReleasePause() {
            if (ownsPause) { Time.timeScale = previousTimeScale; ownsPause = false; }
            if (levelManager != null) levelManager.SetPaused(false);
        }
        void Resume() {
            if (!IsSessionActive) return;
            ReleasePause(); router.ClearHistory(); router.Show(runningScreen, false);
        }
        public void ExitSession() {
            ReleasePause();
            if (levelManager != null) levelManager.StopSession();
            IsPracticing = false; heldSeconds = 0;
        }
        void TickPractice(float dt) {
            var session = levelManager.Session;
            if (session.Phase != BreathPhase.Running || session.Pattern.IsPreparing(session.PlayTime)) return;
            if (practiceStage < 2) {
                var required = practiceStage == 0 ? BreathLane.Upper : BreathLane.Lower;
                heldSeconds = levelManager.input.ReadLane() == required ? heldSeconds + dt : 0;
                double target = practiceStage == 0 ? session.Pattern.Inhale : session.Pattern.Exhale;
                if (heldSeconds < target) return;
                practiceStage++; heldSeconds = 0;
                if (practiceStage == 2) followStartCount = session.CollectedCount;
                runningScreen = practiceStage == 1 ? UIScreenId.PracticeExhale : UIScreenId.PracticeFollow;
                router.Show(runningScreen, false); Refresh();
            } else if (session.CollectedCount - followStartCount >= 3) Finish();
        }
        void Finish() {
            if (!IsSessionActive) return;
            var s = levelManager.Session;
            double possible = s.Pattern.MatchingSeconds(0, s.PlayTime, BreathLane.Upper) + s.Pattern.MatchingSeconds(0, s.PlayTime, BreathLane.Lower);
            bool practice = IsPracticing;
            CurrentResult = new FlightRecord {
                id = sessionId, completedAtUtc = DateTime.UtcNow.ToString("O"), levelNumber = router.SelectedLevel,
                levelTitle = SelectedDefinition != null ? SelectedDefinition.title : SelectedSettings.displayName,
                levelId = SelectedDefinition != null ? SelectedDefinition.levelId : "", collected = s.CollectedCount, seconds = (float)s.PlayTime,
                completedCycles = Cycles(s), rhythmPercent = possible <= 0 ? 0 : Mathf.Clamp((float)(100 * s.CorrectSeconds / possible), 0, 100), practice = practice
            };
            Progress.Add(CurrentResult, AvailableLevels); ViewingHistory = false;
            ExitSession(); router.ClearHistory();
            router.Show(practice ? UIScreenId.PracticeComplete : UIScreenId.FlightComplete, false); Refresh();
        }
        static int Cycles(BreathSession s) => (int)Math.Floor(Math.Max(0, s.PlayTime - s.Pattern.Preparation - s.Pattern.GuideTransition) / s.Pattern.Cycle);
        BreathLevelSettings SelectedSettings => router.SelectedLevel <= AvailableLevels && router.SelectedLevel >= 1
            ? levelManager.levels[router.SelectedLevel - 1].settings : null;
        public void ApplyMotion() {
            if (Progress == null) return;
            levelManager.player.SetReduceMotion(Progress.Data.reduceMotion);
            foreach (var level in levelManager.levels) if (level != null) level.reduceMotion = Progress.Data.reduceMotion;
            bindings.SetReduceMotion(Progress.Data.reduceMotion);
        }
        void OnScreenChanged(UIScreenId id) {
            if (Progress == null) return;
            foreach (var page in router.pages) {
                bool live = IsSessionActive && (page.screenId == UIScreenId.PracticeInhale || page.screenId == UIScreenId.PracticeExhale || page.screenId == UIScreenId.PracticeFollow);
                foreach (string group in new[] { "Background", "Artwork", "Content" }) {
                    var child = page.transform.Find(group); if (child != null) child.gameObject.SetActive(!live);
                }
                var footer = page.transform.Find("Footer");
                if (footer != null) footer.gameObject.SetActive(!live || IsPracticing);
            }
            Refresh();
        }
        public void Refresh() { if (Progress != null && bindings != null) bindings.Present(this); }
        public string Text(UIValueKind kind, int index = 0) {
            if (Progress == null) return "";
            var settings = SelectedSettings;
            var s = IsSessionActive ? levelManager.Session : null;
            var recent = Progress.RecentFlights;
            string transport = sensorInput != null ? sensorInput.ConnectionLabel : "BLE";
            string connection = SensorFresh ? transport + " connected" : sensorInput != null && sensorInput.ReceiverSnapshot.Connected ? "Waiting for data" : "Controller disconnected";
            switch (kind) {
                case UIValueKind.LevelTitle: return ViewingHistory && CurrentResult != null ? "Level " + CurrentResult.levelNumber + " - " + CurrentResult.levelTitle
                    : "Level " + router.SelectedLevel + " - " + (SelectedDefinition != null ? SelectedDefinition.title : settings != null ? settings.displayName : "Unavailable");
                case UIValueKind.LevelDescription: return SelectedDefinition != null ? SelectedDefinition.description : "";
                case UIValueKind.WorldLevels: return AvailableLevels + " levels";
                case UIValueKind.InhaleSeconds: return settings == null ? "--" : settings.inhaleSeconds.ToString("0.#") + " s";
                case UIValueKind.ExhaleSeconds: return settings == null ? "--" : settings.exhaleSeconds.ToString("0.#") + " s";
                case UIValueKind.TodayPractice: return Progress.TodaySessions + " / 6";
                case UIValueKind.Streak: return Progress.Streak + "-day streak";
                case UIValueKind.StreakDays: return Progress.Streak + "-day";
                case UIValueKind.Connection: return connection;
                case UIValueKind.InputReadiness: return SensorFresh ? InputReady ? connection : "Adjust sensor thresholds" : InputReady ? "Keyboard: W / S available" : "Connect " + transport + " controller to fly";
                case UIValueKind.Battery: return "--";
                case UIValueKind.Signal: return transport;
                case UIValueKind.HudStep: return IsPracticing ? "Step " + (practiceStage + 1) + " / 3" : CurrentResult != null && CurrentResult.practice ? "Complete" : "Flight " + (s == null ? "0:00" : Duration((float)s.PlayTime));
                case UIValueKind.Collected: return (s != null ? s.CollectedCount : CurrentResult != null ? CurrentResult.collected : 0).ToString();
                case UIValueKind.ResultSeconds: return CurrentResult == null ? "0:00" : Duration(CurrentResult.seconds);
                case UIValueKind.ResultCycles: return (s != null ? Cycles(s) : CurrentResult != null ? CurrentResult.completedCycles : 0) + " cycles";
                case UIValueKind.ResultRhythm: return CurrentResult == null ? "0%" : CurrentResult.rhythmPercent.ToString("0") + "%";
                case UIValueKind.TodaySessions: return Progress.TodaySessions + " / 6 sessions";
                case UIValueKind.WeeklyFlights: return Progress.WeeklyFlights + " flights";
                case UIValueKind.WeeklyMinutes: return Progress.WeeklyMinutes.ToString("0.#") + " min";
                case UIValueKind.RecentTitle: return index < recent.Length ? "Level " + recent[index].levelNumber + " - " + recent[index].levelTitle : index == 0 ? "No completed flights yet" : "";
                case UIValueKind.RecentDetail: return index < recent.Length ? "Completed - " + Duration(recent[index].seconds) + " - " + recent[index].LocalDate.ToString("MMM d", CultureInfo.InvariantCulture) : "";
                case UIValueKind.InhaleHint: return "Breathe in to fly UP (W)";
                case UIValueKind.ExhaleHint: return "Breathe out to fly DOWN (S)";
                case UIValueKind.InstructionTitle: return practiceStage == 0 ? "Breathe in to fly UP" : practiceStage == 1 ? "Breathe out to fly DOWN" : "Follow the guide and collect 3 stars";
                case UIValueKind.InstructionDetail: return practiceStage < 2 ? "Hold " + (practiceStage == 0 ? "inhale / W" : "exhale / S") + " for " + (settings == null ? "--" : (practiceStage == 0 ? settings.inhaleSeconds : settings.exhaleSeconds).ToString("0.#")) + " s  (" + heldSeconds.ToString("0.0") + ")" : "Collected " + Math.Max(0, (s?.CollectedCount ?? 0) - followStartCount) + " / 3";
                default: return "";
            }
        }
        static string Duration(float seconds) { int t = Mathf.Max(0, Mathf.FloorToInt(seconds)); return (t / 60) + ":" + (t % 60).ToString("00"); }
        void OnDisable() {
            ExitSession();
            if (router != null) { router.ScreenChanged -= OnScreenChanged; if (router.RouteHandler == this) router.RouteHandler = null; }
            if (bindings != null) bindings.Unbind();
            Progress?.Save();
        }
    }
}
