using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class BreathDemoValidation {
        const string Request = "Temp/WingSingerRouteValidate.request";
        const string Report = "Logs/WingSingerValidation.txt";
        static double started;
        static int stage;
        static BreathLevelManager manager;
        static ExternalBreathInput manual;
        static BreathLevelSettings original, temporary;
        static float initialCameraX;
        static string errors = "";

        static BreathDemoValidation() {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        public static void BuildAndValidateBatch() {
            try {
                if (!File.Exists(BreathDemoBuilder.ScenePath)) BreathDemoBuilder.Build();
                File.WriteAllText(Request, "Validate generated scene");
            } catch (Exception error) {
                File.WriteAllText(Report, "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 0.00001, $"{message}: {actual} != {expected}");

        [MenuItem("WingSinger/Validate Breathing Demo")]
        public static void Validate() {
            var p = new BreathPattern(3, 4, 10, 10, 2, 0.2);
            Check(p.LaneAt(0) == BreathLane.Upper && p.LaneAt(3) == BreathLane.Lower && p.LaneAt(7) == BreathLane.Upper, "Route boundaries");
            Near(p.MatchingSeconds(0, 14, BreathLane.Upper), 6, "Multiple cycles");
            Near(p.MatchingSeconds(2, 9, BreathLane.Lower), 4, "Partial interval");
            Near(p.MatchingSeconds(0, 14, BreathLane.Middle), 0, "Neutral never scores");
            var session = new BreathSession(p);
            session.Advance(2, BreathLane.Upper); Near(session.Score, 0, "No preview points");
            session.Advance(3, BreathLane.Upper); Near(session.Score, 0, "Upper dwell alone does not score");
            Check(session.TryCollect(0) && !session.TryCollect(0), "One award per collectible");
            session.Advance(4, BreathLane.Lower); Near(session.Score, 10, "Only paper awards points");
            session.Advance(100, BreathLane.Upper); Near(session.Score, 10, "Final frame clamps duration");
            Near(session.PlayTime, 10, "Final timer");
            session.Advance(100, BreathLane.Lower); Near(session.Score, 10, "Finished game frozen");
            Check(!session.TryCollect(1), "Finished collection window closes");
            Check(session.PlayerLane == BreathLane.Upper && session.Phase == BreathPhase.Finished, "Finished input frozen");
            var empty = new BreathSession(p); empty.Advance(100, BreathLane.Middle); Near(empty.Score, 0, "No score without input");
            var one = new BreathSession(new BreathPattern(3, 4, 15, 10, 0, 0)); one.Advance(15, BreathLane.Upper);
            var many = new BreathSession(one.Pattern); for (int i = 0; i < 150; i++) many.Advance(0.1, BreathLane.Upper);
            Near(one.Score, many.Score, "Frame rate independent scoring");
            Check(KeyboardBreathInput.Resolve(true, false) == BreathLane.Upper && KeyboardBreathInput.Resolve(false, true) == BreathLane.Lower
                && KeyboardBreathInput.Resolve(true, true) == BreathLane.Middle && KeyboardBreathInput.Resolve(false, false) == BreathLane.Middle, "Keyboard mapping");
            bool invalid = false; try { new BreathPattern(0, 1, 1, 1, 0, 0); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "Reject invalid durations");
            invalid = false; try { session.Advance(double.NaN, BreathLane.Upper); } catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid, "Reject invalid elapsed time");

            var smooth = new BreathPattern(3, 4, 18, 10, 2, 0.2, 3, 1);
            Check(smooth.LaneAt(0) == BreathLane.Middle && smooth.LaneAt(2.99) == BreathLane.Middle, "Middle preparation route");
            Near(smooth.HeightAt(0), 0, "Middle start"); Near(smooth.HeightAt(3), 0, "First curve starts at middle");
            Near(smooth.HeightAt(3.5), 0.5, "First curve midpoint"); Near(smooth.HeightAt(4), 1, "First curve arrives at upper");
            Near(smooth.HeightAt(7.5), 0, "Down curve midpoint"); Near(smooth.HeightAt(8), -1, "Down curve arrives");
            Near(smooth.HeightAt(12.5), 0, "Up curve midpoint"); Near(smooth.HeightAt(13), 1, "Up curve arrives");
            Check(smooth.IsGuideTransition(3.5) && smooth.IsGuideTransition(7.5) && !smooth.IsGuideTransition(4.5), "Transition classification");
            foreach (double boundary in new[] { 3d, 4d, 7d, 8d, 12d, 13d })
                Check(Math.Abs(smooth.HeightAt(boundary - 0.000001) - smooth.HeightAt(boundary + 0.000001)) < 0.00001, "Continuous curve boundary");
            Near(smooth.MatchingSeconds(0, 3, BreathLane.Upper), 0, "Preparation does not score");
            Near(smooth.MatchingSeconds(3, 4, BreathLane.Upper), 0, "Initial curve does not score");
            Near(smooth.MatchingSeconds(7, 8, BreathLane.Lower), 0, "Down curve does not score");
            Near(smooth.MatchingSeconds(0, 22, BreathLane.Upper), 6, "Upper dwell integrates across smooth cycles");
            Near(smooth.MatchingSeconds(0, 22, BreathLane.Lower), 8, "Lower dwell integrates across smooth cycles");
            var prepared = new BreathSession(smooth);
            prepared.Advance(5, BreathLane.Upper); Near(prepared.Score, 0, "No early preparation score");
            Check(!prepared.IsMatching, "Initial curve cannot show scoring feedback");
            prepared.Advance(1, BreathLane.Upper); Near(prepared.Score, 0, "Initial travel excludes score");
            prepared.Advance(3, BreathLane.Upper); Near(prepared.Score, 0, "Inhale dwell does not auto-score");
            prepared.Advance(1, BreathLane.Lower); Near(prepared.Score, 0, "Travel does not auto-score");
            prepared.Advance(4, BreathLane.Lower); Near(prepared.Score, 0, "Exhale dwell does not auto-score");
            var single = new BreathSession(smooth); single.Advance(100, BreathLane.Upper);
            var split = new BreathSession(smooth); for (int i = 0; i < 1000; i++) split.Advance(0.1, BreathLane.Upper);
            Near(single.Score, split.Score, "Smooth route scoring remains frame-rate independent");
            var noPrep = new BreathPattern(3, 4, 18, 10, 2, 0.2, 0, 1);
            Check(noPrep.LaneAt(0) == BreathLane.Upper && noPrep.IsGuideTransition(0), "Zero preparation begins first curve immediately");
            invalid = false; try { new BreathPattern(3, 4, 18, 10, 2, 0.2, -1, 1); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "Reject invalid preparation");
            File.WriteAllText(Report, "PASS: deterministic route, input, preview, scoring, transition, long frame, timer, finish, frame-rate and invalid settings checks.\n"
                + "PASS: configurable middle-lane preparation, continuous initial/down/up curves, exact dwell scoring and no points on preparation or curves.\n");
        }

        static void Poll() {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(Request);
                try {
                    Validate();
                    Check(File.Exists(BreathDemoBuilder.ScenePath), "Demo scene missing");
                    Check(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), "Save the current scene before validation");
                    EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                    var savedManager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                    Check(savedManager != null, "Manager missing in saved scene");
                    Check(savedManager.levels.All(l => PrefabUtility.IsPartOfPrefabInstance(l.gameObject)), "Saved scene must contain real Prefab instances");
                    ValidateRenderedRoute(savedManager.levels[0]);
                    SessionState.SetBool("WingSingerValidation", true);
                    EditorApplication.isPlaying = true;
                } catch (Exception error) {
                    File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error);
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            if (!SessionState.GetBool("WingSingerValidation", false) || !EditorApplication.isPlaying || manager == null) return;
            try { TickSmokeTest(); }
            catch (Exception error) { File.AppendAllText(Report, "FAIL: " + error + "\n"); Finish(); }
        }

        static void OnPlayState(PlayModeStateChange state) {
            if (!SessionState.GetBool("WingSingerValidation", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) {
                manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                if (manager == null) { File.AppendAllText(Report, "FAIL: runtime manager missing\n"); Finish(); return; }
                manual = manager.gameObject.AddComponent<ExternalBreathInput>(); manager.input = manual;
                errors = ""; Application.logMessageReceived += LogError;
                stage = 0; started = EditorApplication.timeSinceStartup;
            } else if (state == PlayModeStateChange.EnteredEditMode) {
                SessionState.SetBool("WingSingerValidation", false);
                manager = null; manual = null;
                if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(Report).Contains("SUCCESS:") ? 0 : 1);
            }
        }

        static void TickSmokeTest() {
            if (EditorApplication.timeSinceStartup - started < 0.3) return;
            switch (stage) {
                case 0:
                    Check(manager.Session != null && manager.CurrentIndex == 0, "Initial level not running");
                    Check(manager.levels.Count(l => l.gameObject.activeInHierarchy) == 1, "Initial active level invariant");
                    Check(!manager.SwitchLevel(999) && manager.CurrentIndex == 0, "Invalid request changed level");
                    manager.Next(); Check(manager.CurrentIndex == 1, "Next level failed");
                    Check(manager.levels.Count(l => l.gameObject.activeInHierarchy) == 1, "Switch left multiple active levels");
                    manager.SwitchLevel(0);
                    original = manager.CurrentLevel.settings; temporary = UnityEngine.Object.Instantiate(original);
                    temporary.durationSeconds = 0.8f; temporary.inhaleSeconds = 0.35f; temporary.exhaleSeconds = 0.3f;
                    temporary.guideLeadSeconds = 0.1f; temporary.laneTransitionSeconds = 0.02f;
                    temporary.preparationSeconds = 0.1f; temporary.guideTransitionSeconds = 0.06f;
                    manager.CurrentLevel.settings = temporary; manager.Restart();
                    manual.Submit(BreathLane.Upper); initialCameraX = Camera.main.WorldToViewportPoint(manager.player.transform.position).x;
                    stage = 1; started = EditorApplication.timeSinceStartup; break;
                case 1:
                    manual.Submit(manager.Session.RequiredLane);
                    var expectedHeight = (float)manager.Session.Pattern.HeightAt(Math.Min(manager.Session.Elapsed, manager.Session.Pattern.Duration)) * temporary.laneSpacing;
                    Check(Mathf.Abs(manager.CurrentLevel.guide.localPosition.y - expectedHeight) < 0.001f, "Live guide diverges from smooth route");
                    if (manager.Session.Phase != BreathPhase.Finished) {
                        Check(Mathf.Abs(Camera.main.WorldToViewportPoint(manager.player.transform.position).x - initialCameraX) < 0.03f, "Camera does not keep player horizontal position");
                        Check(EditorApplication.timeSinceStartup - started < 15, "Runtime timer stalled");
                        return;
                    }
                    Check(manager.Session.Score > 0, "Runtime follow produced no score");
                    if (manager.hud != null) Check(manager.hud.resultPanel.activeSelf, "Results not shown");
                    Near(manager.Session.PlayTime, 0.8f, "Runtime end duration");
                    File.AppendAllText(Report, "PASS: play-mode Prefab switching, invalid index, camera follow, live movement/scoring, timer and results.\n");
                    manager.Restart();
                    Near(manager.Session.Score, 0, "Restart score reset"); Near(manager.Session.PlayTime, 0, "Restart timer reset");
                    if (manager.hud != null) Check(!manager.hud.resultPanel.activeSelf, "Restart results still visible");
                    Check(manager.CurrentLevel.routeCore.positionCount <= 1, "Old route survived restart");
                    manager.CurrentLevel.settings = original; manager.Restart(); manual.Submit(BreathLane.Upper);
                    stage = 2; started = EditorApplication.timeSinceStartup; break;
                case 2:
                    manual.Submit(manager.Session.RequiredLane);
                    double captureTime = original.preparationSeconds + original.guideTransitionSeconds
                        + original.inhaleSeconds + original.guideTransitionSeconds * 0.5;
                    if (manager.Session.Elapsed < captureTime) return;
                    Capture("Docs/Images/BreathDemo.png");
                    File.AppendAllText(Report, "PASS: restart resets score/time/path/results; no previous level activity.\n");
                    Check(string.IsNullOrEmpty(errors), "Runtime error log: " + errors);
                    File.AppendAllText(Report, "SUCCESS: saved scene compiled and play-mode checks passed.\n");
                    Finish(); break;
            }
        }

        static void ValidateRenderedRoute(BreathLevel level) {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(level.gameObject);
            var preview = PrefabUtility.LoadPrefabContents(prefabPath);
            try { ValidateRouteContents(preview.GetComponent<BreathLevel>()); }
            finally { PrefabUtility.UnloadPrefabContents(preview); }
        }

        static void ValidateRouteContents(BreathLevel level) {
            level.Prepare();
            var session = new BreathSession(level.settings.CreatePattern());
            level.Present(session);
            Near(level.guide.localPosition.y, 0, "Guide begins on middle lane");
            session.Advance(level.settings.preparationSeconds + level.settings.guideTransitionSeconds * 0.5, BreathLane.Middle);
            level.Present(session);
            Near(level.guide.localPosition.y, level.settings.laneSpacing * 0.5, "Rendered first curve midpoint");
            Check(level.routeCore.positionCount > 3, "Curve not sampled into path");
            var last = level.routeCore.GetPosition(level.routeCore.positionCount - 1);
            Check(Vector3.Distance(last, level.guide.localPosition) < 0.001f, "Path does not end at moving guide");
            for (int i = 1; i < level.routeCore.positionCount; i++)
                Check(level.routeCore.GetPosition(i).x > level.routeCore.GetPosition(i - 1).x, "Path contains instantaneous vertical jumps");
            level.Exit();
            File.AppendAllText(Report, "PASS: saved Prefab guide starts in middle, travels through intermediate positions and draws a continuous path.\n");
        }

        static void LogError(string condition, string stack, LogType type) {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += condition + "\n";
        }

        static void Finish() {
            Application.logMessageReceived -= LogError;
            if (manager != null && temporary != null && original != null) manager.levels[0].settings = original;
            if (temporary != null) UnityEngine.Object.Destroy(temporary);
            EditorApplication.isPlaying = false;
        }

        static void Capture(string path) {
            BreathArtSetup.Capture(path);
        }
    }
}
