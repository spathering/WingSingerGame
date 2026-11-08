using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class CameraFramingValidation {
        const string Key = "WingSinger.CameraFramingValidation";
        static IEnumerator routine;
        static readonly List<string> checks = new List<string>();
        static double started;
        static CameraFramingValidation() { EditorApplication.update += Tick; }

        [MenuItem("WingSinger/Apply Follow Camera And HD Trees")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            AnimatedForegroundSetup.ApplyToOpenDemo();
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            var framing = manager.virtualCamera.GetComponent<BreathCameraFraming>();
            if (framing == null) framing = manager.virtualCamera.gameObject.AddComponent<BreathCameraFraming>();
            framing.manager = manager; framing.minimumOrthographicSize = 5.6f;
            foreach (var level in manager.levels) {
                level.settings.laneCenterOffset = -1.4f; level.settings.laneSpacing = 7.2f;
                level.settings.laneTransitionSeconds = .38f;
                level.settings.upperLaneSpacing = level.settings.lowerLaneSpacing = 0;
                level.settings.Validate(); EditorUtility.SetDirty(level.settings);
            }
            if (manager.player.highlight != null) { manager.player.highlight.enabled = false; EditorUtility.SetDirty(manager.player.highlight); }
            var lens = manager.virtualCamera.m_Lens; lens.OrthographicSize = framing.minimumOrthographicSize;
            manager.virtualCamera.m_Lens = lens;
            Camera.main.orthographicSize = lens.OrthographicSize;
            EditorUtility.SetDirty(framing); EditorUtility.SetDirty(manager.virtualCamera); EditorUtility.SetDirty(Camera.main);
            foreach (var layer in Object.FindObjectsOfType<LoopingPanorama>()) {
                layer.worldSpaceVertical = true; layer.referenceOrthographicSize = 6.8f; layer.worldOriginY = 0;
                layer.verticalOverscan = layer.name == "Looping Background" ? 1.8f : 1;
                layer.Sync(); EditorUtility.SetDirty(layer); EditorUtility.SetDirty(layer.transform);
            }
            AnimatedForegroundSetup.AlignToBackgroundBottom();
            framing.worldBackground = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Background");
            EditorUtility.SetDirty(framing);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }

        public static void ApplyAndValidateBatch() {
            try {
                Apply();
                var flow = Object.FindObjectOfType<BreathUIFlowController>();
                flow.profileKey = ""; flow.sensorInput.enabled = false;
                SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Restore", false);
                SessionState.SetBool(Key + "Background", Application.runInBackground);
                EditorApplication.isPlaying = true;
            } catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Restore", false)) {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1));
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                if (routine == null) { checks.Clear(); started = EditorApplication.timeSinceStartup; routine = Run(); }
                if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Camera validation timed out");
                if (!routine.MoveNext()) Finish(null);
            } catch (Exception error) { Finish(error.ToString()); }
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add("PASS: " + message); }
        static void InFrame(Camera camera, Bounds bounds, bool checkVertical = true) {
            var a = camera.WorldToViewportPoint(bounds.min); var b = camera.WorldToViewportPoint(bounds.max);
            if (a.x < .079f || b.x > .921f || (checkVertical && (a.y < .079f || b.y > .921f)))
                throw new Exception("Target clipped: " + a + " -> " + b + "; camera=" + camera.transform.position + "; size=" + camera.orthographicSize);
        }
        static IEnumerator Run() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); var manager = flow.levelManager;
            var input = new GameObject("Camera validation input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            manager.input = input;
            var camera = Camera.main;
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            var background = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Background");
            Check(trees.animationFrames.Length == 150 && trees.animationFrames.All(frame => frame.width == 8192 && frame.height == 428
                && frame.format == TextureFormat.BC7 && !frame.isReadable), "150 HD tree frames are 8192x428 BC7, no CPU copies");
            Check(Mathf.Abs(trees.animationVerticalScale - 427f/428) < .0001f, "HD padding is excluded from UVs");
            checks.Add("INFO: Editor tree texture memory bytes=" + trees.animationFrames.Sum(frame => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(frame)));
            int[] roots = manager.levels.Select(level => level.GetInstanceID()).ToArray();
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
            foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1080), new Vector2Int(865,721) }) {
                camera.aspect = (float)resolution.x / resolution.y;
                for (int index = 0; index < manager.levels.Length; index++) {
                    manager.SwitchLevel(index);
                    var settings = manager.CurrentLevel.settings;
                    Check(Mathf.Abs(settings.LaneHeight(1) - settings.LaneHeight(0) - 7.2f) < .001f
                        && Mathf.Abs(settings.LaneHeight(0) - settings.LaneHeight(-1) - 7.2f) < .001f, "Both environment lane gaps equal 7.2 world units");
                    float treeBottom = trees.transform.position.y - trees.transform.localScale.y * .5f;
                    float treeTop = treeBottom + trees.transform.localScale.y;
                    Check(settings.LaneHeight(1) - manager.player.characterAnimation.target.bounds.extents.y > treeTop,
                        "Upper-lane player is fully above the highest part of the tree layer, in the sky");
                    Check(settings.LaneHeight(0) > treeBottom + trees.transform.localScale.y * .6f && settings.LaneHeight(0) < treeTop,
                        "Middle lane crosses the canopy region of the tree layer");
                    Check(settings.LaneHeight(-1) > treeBottom && settings.LaneHeight(-1) < treeBottom + trees.transform.localScale.y * .3f,
                        "Lower lane crosses the ground region at the tree bases");
                    Check(manager.CurrentLevel.PaperLayout.Length > 0 && manager.CurrentLevel.PaperLayout.All(point => point.Lane != BreathLane.Middle),
                        "No preparation or transition stars remain on the middle lane");
                    yield return null; yield return null;
                    Check(manager.player.IsEntering && camera.WorldToViewportPoint(manager.player.body.position).x < 0,
                        resolution + " / " + index + " preserves off-screen entrance");
                    manager.Session.Advance(manager.Session.Pattern.Lead + .1, BreathLane.Middle);
                    yield return null; yield return null;
                    float middleY = 0, upperY = 0;
                    var paperPositions = manager.CurrentLevel.Papers.Select(paper => paper.transform.position).ToArray();
                    float fixedBackgroundY = background.transform.position.y, fixedBackgroundHeight = background.transform.localScale.y;
                    float fixedTreesY = trees.transform.position.y, fixedTreesHeight = trees.transform.localScale.y;
                    float middleCrop = 0, upperCrop = 0;
                    foreach (var lane in new[] { BreathLane.Middle, BreathLane.Upper, BreathLane.Lower }) {
                        input.Submit(lane);
                        double until = EditorApplication.timeSinceStartup + .85;
                        int frames = 0;
                        while (EditorApplication.timeSinceStartup < until || frames < 5) {
                            yield return null; frames++;
                            InFrame(camera, manager.player.characterAnimation.target.bounds);
                            InFrame(camera, manager.CurrentLevel.guideVisual.bounds, false);
                            float bgHalfHeight = background.transform.localScale.y * .5f;
                            if (camera.transform.position.y - camera.orthographicSize < background.transform.position.y - bgHalfHeight - .001f
                                || camera.transform.position.y + camera.orthographicSize > background.transform.position.y + bgHalfHeight + .001f)
                                throw new Exception("Vertical camera movement exposed space outside the background");
                        }
                        Check(camera.orthographicSize >= 5.599f, resolution + " / " + index + " / " + lane + " player stays fully visible and crane stays horizontally within 8% margins, including transitions");
                        if (resolution.x == 1920 && lane == BreathLane.Middle)
                            Check(camera.orthographicSize < 6.1f, "Normal widescreen view is closer than the previous 6.2 framing");
                        Check(Mathf.Abs((trees.transform.position.y - trees.transform.localScale.y * .5f)
                            - (background.transform.position.y - background.transform.localScale.y * .5f)) < .001f,
                            "Tree layer bottom exactly matches the fixed background bottom during camera follow and zoom");
                        Check(Mathf.Abs(background.transform.position.y - fixedBackgroundY) < .001f
                            && Mathf.Abs(background.transform.localScale.y - fixedBackgroundHeight) < .001f
                            && Mathf.Abs(trees.transform.position.y - fixedTreesY) < .001f
                            && Mathf.Abs(trees.transform.localScale.y - fixedTreesHeight) < .001f,
                            "Background and trees keep fixed world Y and size during camera movement");
                        Check(paperPositions.SequenceEqual(manager.CurrentLevel.Papers.Select(paper => paper.transform.position)),
                            "Stars keep their world positions while the player changes lanes and the camera follows");
                        float crop = (camera.transform.position.y - fixedBackgroundY) / fixedBackgroundHeight + .5f;
                        float cropHalfHeight = camera.orthographicSize / fixedBackgroundHeight;
                        Check(crop - cropHalfHeight >= 0 && crop + cropHalfHeight <= 1 && cropHalfHeight < .5f,
                            "Camera shows only a cropped part of the background without exposing top/bottom edges");
                        if (lane == BreathLane.Middle) middleCrop = crop;
                        if (lane == BreathLane.Middle) middleY = camera.transform.position.y;
                        if (lane == BreathLane.Upper) {
                            upperCrop = crop;
                            upperY = camera.transform.position.y;
                            Check(upperY > middleY + .3f, "Camera rises with the player");
                            Check(upperCrop > middleCrop, "Upper lane reveals a higher part of the fixed background");
                        }
                        if (lane == BreathLane.Lower) {
                            Check(camera.transform.position.y < upperY - .5f, "Camera descends with the player");
                            Check(crop < upperCrop && crop < middleCrop, "Lower lane reveals a lower part of the fixed background");
                        }
                        var foreground = manager.CurrentLevel.foreground;
                        if (foreground != null) {
                            foreground.Sync();
                            float offsetY = foreground.transform.position.y - foreground.transform.parent.position.y;
                            Check(Mathf.Abs(offsetY - foreground.worldVerticalOffset) < .001f, "Near decorations stay aligned to the fixed world ground");
                        }
                        if (index == 0 && resolution.x == 1920)
                            NovemberArtSetup.Capture(flow, "Docs/Images/CameraFraming_" + lane + ".png", resolution.x, resolution.y);
                    }
                    manager.SetPaused(true); Time.timeScale = 0;
                    var paused = camera.transform.position; int frame = trees.AnimationFrame;
                    for (int i = 0; i < 5; i++) yield return null;
                    Check((camera.transform.position-paused).sqrMagnitude < .00001f && trees.AnimationFrame == frame, "Pause freezes follow and HD animation");
                    float pausedSize = camera.orthographicSize;
                    var guidePosition = manager.CurrentLevel.guide.position;
                    manager.CurrentLevel.guide.position += Vector3.up * 30;
                    yield return null; yield return null;
                    InFrame(camera, manager.CurrentLevel.guideVisual.bounds, false);
                    Check(Mathf.Abs(camera.transform.position.y - paused.y) < .001f && Mathf.Abs(camera.orthographicSize - pausedSize) < .001f,
                        "Crane outside the vertical viewport does not move or zoom the camera; horizontal framing remains protected");
                    manager.CurrentLevel.guide.position = guidePosition;
                    manager.SetPaused(false); Time.timeScale = 1;
                    manager.Restart(); yield return null; yield return null;
                    Check(Mathf.Abs(camera.transform.position.y - manager.CurrentLevel.spawnPoint.position.y) < .001f,
                        "Restart snaps to neutral height, clearing previous follow velocity");
                }
            }
            var spawningChecks = VerifyGuideSpawning(manager, input);
            while (spawningChecks.MoveNext()) yield return spawningChecks.Current;
            var collectionChecks = VerifyStarRules(flow, manager, input, camera);
            while (collectionChecks.MoveNext()) yield return collectionChecks.Current;
            Check(roots.SequenceEqual(manager.levels.Select(level => level.GetInstanceID())) && manager.levels.Count(level => level.gameObject.activeSelf) == 1,
                "Switch/restart reuse the same level instances; exactly one active level");
            flow.ExitSession(); yield return null; yield return null;
            Check(manager.Session == null && manager.levels.All(level => !level.gameObject.activeSelf) && trees.AnimationFrame == 0,
                "Exit disables levels and resets animation without retained session work");
        }
        static IEnumerator VerifyGuideSpawning(BreathLevelManager manager, ExternalBreathInput input) {
            input.Submit(BreathLane.Middle);
            for (int index = 0; index < manager.levels.Length; index++) {
                manager.SwitchLevel(index); manager.SetPaused(true);
                var level = manager.CurrentLevel;
                double lead = Math.Max(0, level.guideHorizontalOffset) / level.settings.moveSpeed;
                foreach (var lane in new[] { BreathLane.Upper, BreathLane.Lower }) {
                    var star = level.Papers.First(paper => level.PaperLayout[paper.Id].Lane == lane);
                    double beforeArrival = star.RouteTime - lead - .02;
                    manager.Session.Advance(Math.Max(0, beforeArrival - manager.Session.Elapsed), BreathLane.Middle);
                    level.Present(manager.Session);
                    Check(!star.gameObject.activeSelf && level.guide.position.x < star.transform.position.x,
                        index + " / " + lane + " first star is hidden immediately before the crane reaches it");
                    manager.Session.Advance(.02, BreathLane.Middle); level.Present(manager.Session);
                    Check(star.gameObject.activeSelf && Vector2.Distance(level.guide.position, star.transform.position) < .001f,
                        index + " / " + lane + " first star appears at the crane's actual XY, including after lane transition");
                    manager.Session.Advance(.12, BreathLane.Middle); level.Present(manager.Session);
                    Check(star.visual.sprite == star.clip.Sample(.12, true) && star.visual.sprite != star.clip.frames[0],
                        "Newly revealed star animates immediately using its actual reveal time");
                    int shown = level.Papers.Count(paper => paper.gameObject.activeSelf);
                    yield return null; yield return null;
                    Check(shown == level.Papers.Count(paper => paper.gameObject.activeSelf), "Pause does not generate extra stars");
                }
                manager.Session.Advance(2.2, BreathLane.Middle); level.Present(manager.Session);
                double guideTime = (level.guide.localPosition.x - level.spawnPoint.localPosition.x) / level.settings.moveSpeed;
                Check(level.Papers.All(paper => paper.gameObject.activeSelf == (paper.RouteTime <= guideTime + .00001)),
                    "A long frame reveals every crossed star and leaves every future star hidden");
                double endElapsed = manager.Session.Pattern.Duration - lead + .1;
                manager.Session.Advance(Math.Max(0, endElapsed - manager.Session.Elapsed), BreathLane.Middle); level.Present(manager.Session);
                Check(level.Papers.All(paper => paper.gameObject.activeSelf) && Mathf.Abs(level.guide.localPosition.x
                    - level.spawnPoint.localPosition.x - level.settings.durationSeconds * level.settings.moveSpeed) < .001f,
                    "Crane stops at the shared route endpoint with all final stars revealed");
                var lastStar = level.Papers[level.PaperLayout.Length - 1]; var frame = lastStar.visual.sprite;
                manager.Session.Advance(.12, BreathLane.Middle); level.Present(manager.Session);
                Check(lastStar.visual.sprite != frame, "Revealed stars keep animating while the crane waits at the endpoint");
                manager.Restart(); manager.SetPaused(true);
                Check(level.Papers.All(paper => !paper.gameObject.activeSelf && !paper.Collected && !paper.Missed),
                    "Restart clears revealed stars until the crane passes again");
            }
            manager.SetPaused(false);
        }

        static IEnumerator VerifyStarRules(BreathUIFlowController flow, BreathLevelManager manager, ExternalBreathInput input, Camera camera) {
            camera.aspect = 16f/9; input.Submit(BreathLane.Middle); manager.SwitchLevel(0);
            var level = manager.CurrentLevel;
            var star = level.Papers.First(paper => level.PaperLayout[paper.Id].Lane == BreathLane.Upper);
            manager.Session.Advance(manager.Session.Pattern.Lead + star.RouteTime - .1, BreathLane.Middle);
            yield return null; yield return null;
            double until = EditorApplication.timeSinceStartup + 8;
            while (manager.player.body.position.x < star.transform.position.x + 2) {
                if (EditorApplication.timeSinceStartup > until) throw new Exception("Waiting for player to pass the missed star timed out");
                yield return null;
            }
            Check(!star.Collected && !star.Missed && star.gameObject.activeInHierarchy,
                "Missed star remains visible after the player passes its world X by two units");
            NovemberArtSetup.Capture(flow, "Docs/Images/StarMissedBehindPlayer.png", 1280, 720);
            until = EditorApplication.timeSinceStartup + 8;
            while (!star.Missed) {
                float left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
                if (star.visual.bounds.max.x >= left - .1f && !star.gameObject.activeInHierarchy)
                    throw new Exception("An uncollected star vanished before its full visual passed the camera edge");
                if (EditorApplication.timeSinceStartup > until) throw new Exception("Waiting for off-screen recycling timed out");
                yield return null;
            }
            Check(!star.gameObject.activeInHierarchy && star.visual.bounds.max.x
                < camera.transform.position.x - camera.orthographicSize * camera.aspect - .1f,
                "Missed star is recycled only after its complete bounds leave the actual camera");

            manager.Restart(); level = manager.CurrentLevel;
            star = level.Papers.First(paper => level.PaperLayout[paper.Id].Lane == BreathLane.Upper);
            Check(!star.Collected && !star.Missed && manager.Session.CollectedCount == 0, "Restart restores missed stars and score");
            input.Submit(BreathLane.Upper);
            manager.Session.Advance(manager.Session.Pattern.Lead + star.RouteTime - .5, BreathLane.Upper);
            until = EditorApplication.timeSinceStartup + 8;
            while (!star.Collected) {
                if (EditorApplication.timeSinceStartup > until) throw new Exception("Waiting for a real star collision timed out");
                yield return null;
            }
            Check(star.visual.color == Color.white && !manager.player.highlight.enabled,
                "Actual collection has no HDR star flash or player highlight");
            double score = manager.Session.Score;
            Check(!manager.Session.TryCollect(star.Id) && manager.Session.Score == score,
                "Collection still awards once per star without continuous score");
            until = EditorApplication.timeSinceStartup + .35;
            while (EditorApplication.timeSinceStartup < until) {
                if (star.visual.color != Color.white || manager.player.highlight.enabled)
                    throw new Exception("Collection flash returned during absorption");
                yield return null;
            }
            Check(!star.gameObject.activeInHierarchy, "Collected star finishes its existing absorption/shrink without lighting effects");
        }
        static void Finish(string error) {
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText("Docs/Validation/CameraFraming.txt", string.Join("\n", checks) + "\n" + (error ?? "SUCCESS"));
            if (error != null) Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Exit", error == null ? 0 : 1);
            SessionState.SetBool(Key + "Restore", true); EditorApplication.isPlaying = false;
        }
    }
}
