using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class PaperCollectionValidation {
        const string Key = "WingSingerPaperValidation";
        const string Report = "Logs/PaperCollectionValidation.txt";
        static BreathLevelManager manager;
        static ExternalBreathInput manual;
        static BreathLevelSettings original, temporary;
        static int stage;
        static double started;
        static int[] poolIds;
        static string errors = "";
        static PaperCollectionValidation() {
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update += Tick;
        }

        public static void BeginBatch() {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, "Paper collection verification\n");
            BreathDemoValidation.Validate();
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void Check(bool ok, string message) {
            if (!ok) throw new Exception(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }

        static void OnPlayState(PlayModeStateChange state) {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) {
                manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                stage = 0; started = EditorApplication.timeSinceStartup;
                errors = ""; Application.logMessageReceived += LogError;
            } else if (state == PlayModeStateChange.EnteredEditMode) {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(File.ReadAllText(Report).Contains("SUCCESS") ? 0 : 1);
            }
        }

        static void Tick() {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || manager == null) return;
            try {
                if (EditorApplication.timeSinceStartup - started < 0.35) return;
                switch (stage) {
                    case 0:
                        Check(manager.Session != null && manager.CurrentIndex == 0, "Initial level starts");
                        Check(manager.Session.Score == 0, "Preview does not award points");
                        Check(UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length == 0, "Game remains free of UI");
                        var level = manager.CurrentLevel;
                        Check(level.PaperLayout.All(p => Math.Abs(level.settings.CreatePattern().HeightAt(p.Time) - (int)p.Lane) < 0.00001), "Every paper lies on a route/lane intersection");
                        Check(!level.routeCore.enabled && !level.routeGlow.enabled, "Continuous route is hidden");
                        Check(level.guideClip.sourceFile == "craneFlat (1).gif" && level.collectiblePrefab.clip.sourceFile == "star.gif", "Requested GIF roles are preserved");
                        Check(level.guideClip.frames.Length == 120 && Math.Abs(level.guideClip.Duration - 2.4) < 0.001, "Guide GIF timing preserved");
                        Check(level.collectiblePrefab.clip.frames.Length == 120 && Math.Abs(level.collectiblePrefab.clip.Duration - 2.4) < 0.001, "Collectible GIF timing preserved");
                        poolIds = level.Papers.Select(p => p.GetInstanceID()).ToArray();
                        manual = manager.gameObject.AddComponent<ExternalBreathInput>(); manager.input = manual;
                        manager.Restart(); manager.Session.Advance(2.1, BreathLane.Middle);
                        stage = 1; started = EditorApplication.timeSinceStartup; break;
                    case 1:
                        Check(manager.Session.CollectedCount > 0 && manager.Session.Score == manager.Session.CollectedCount * manager.CurrentLevel.settings.pointsPerCollectible, "Actual movement collects discrete objects and awards one value each");
                        var current = manager.CurrentLevel;
                        var upper = current.Papers.First(p => current.PaperLayout[p.Id].Lane == BreathLane.Upper);
                        manager.Session.Advance(Math.Max(0, upper.RouteTime - manager.Session.Elapsed), BreathLane.Middle);
                        current.Present(manager.Session);
                        Vector3 point = upper.transform.position;
                        Check(!upper.IntersectsSweep(point + Vector3.down * current.settings.laneSpacing, point + Vector3.down * current.settings.laneSpacing, current.settings.collectionRadius), "Wrong lane cannot collect");
                        Check(upper.IntersectsSweep(point + Vector3.left * 5, point + Vector3.right * 5, current.settings.collectionRadius), "Swept movement catches a paper across a long frame");
                        var middle = current.PaperLayout.First(p => p.Lane == BreathLane.Middle && p.Time > current.settings.preparationSeconds + 0.001);
                        manager.Session.Advance(middle.Time + manager.Session.Pattern.Lead - manager.Session.Elapsed, BreathLane.Middle);
                        current.Present(manager.Session);
                        Vector3 middlePosition = current.transform.TransformPoint(new Vector3((float)middle.Time * current.settings.moveSpeed, 0, -0.2f));
                        double before = manager.Session.Score;
                        Check(current.CollectBetween(manager.Session, middlePosition, middlePosition) == 1, "Transition middle-lane paper can be collected");
                        Check(current.CollectBetween(manager.Session, middlePosition, middlePosition) == 0 && manager.Session.Score == before + current.settings.pointsPerCollectible, "Duplicate contact cannot award again");
                        original = current.settings; temporary = UnityEngine.Object.Instantiate(original);
                        temporary.durationSeconds = 0.8f; temporary.preparationSeconds = 0.8f;
                        temporary.guideLeadSeconds = 0.1f; temporary.collectibleIntervalSeconds = 0.4f;
                        current.settings = temporary; manual.Submit(BreathLane.Middle); manager.Restart();
                        stage = 2; started = EditorApplication.timeSinceStartup; break;
                    case 2:
                        if (manager.Session.Phase != BreathPhase.Finished) {
                            if (EditorApplication.timeSinceStartup - started > 15) throw new Exception("Session timer stalled");
                            return;
                        }
                        Check(manager.Session.CollectedCount == 3, "Start, interior and final endpoint all collected in live play");
                        Check(manager.Session.Score == 3 * temporary.pointsPerCollectible, "No continuous score remains");
                        manager.Restart();
                        Check(manager.Session.Score == 0 && manager.CurrentLevel.Papers.Take(3).All(p => !p.Collected && p.gameObject.activeSelf == (p.RouteTime == 0)), "Restart resets score, visibility and collected state");
                        Check(poolIds.SequenceEqual(manager.CurrentLevel.Papers.Select(p => p.GetInstanceID())), "Restart reuses the same paper pool");
                        var old = manager.CurrentLevel;
                        manager.Next();
                        Check(manager.levels.Count(l => l.gameObject.activeSelf) == 1 && old.Papers.All(p => !p.gameObject.activeInHierarchy), "Switch disables all old papers and preserves one active level");
                        old.settings = original; manager.SwitchLevel(0); manager.Session.Advance(7.0, BreathLane.Upper); manual.Submit(BreathLane.Upper);
                        stage = 3; started = EditorApplication.timeSinceStartup; break;
                    case 3:
                        Check(manager.CurrentLevel.Papers.Any(p => p.gameObject.activeInHierarchy && !p.Collected), "Visible paper route is populated ahead of the player");
                        Check(string.IsNullOrEmpty(errors), "No runtime errors");
                        BreathArtSetup.Capture("Docs/Images/PaperCollection.png");
                        File.AppendAllText(Report, "SUCCESS: paper layout, GIF assets, collection, feedback, final frame, pooling and level lifecycle verified.\n");
                        Finish(); break;
                }
            } catch (Exception error) {
                File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); Finish();
            }
        }

        static void LogError(string message, string stack, LogType type) {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors += message + "\n";
        }

        static void Finish() {
            Application.logMessageReceived -= LogError;
            if (manager != null && original != null) manager.levels[0].settings = original;
            if (temporary != null) UnityEngine.Object.Destroy(temporary);
            EditorApplication.isPlaying = false;
        }
    }
}
