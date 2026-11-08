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
    // Decorations are saved in the existing level Prefab, so its normal SetActive lifecycle owns them.
    [InitializeOnLoad]
    public static class LevelForegroundSetup {
        const string Folder = "Assets/Art/UI/Frontgrounds/";
        const string RootName = "Foreground Decorations";
        const string Key = "WingSinger.LevelForegroundValidation";
        const string Report = "Docs/Validation/LevelForeground.txt";
        static IEnumerator routine;
        static double started;
        static readonly List<string> checks = new List<string>();
        static LevelForegroundSetup() { EditorApplication.update += Tick; }

        [MenuItem("WingSinger/Apply Level Foreground Decorations")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != BreathDemoBuilder.ScenePath) throw new InvalidOperationException("Open the saved BreathDemo scene first.");
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            int index = manager.initialLevel;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(manager.levels[index].gameObject);
            if (string.IsNullOrEmpty(prefabPath)) throw new InvalidOperationException("The level must be an existing Prefab instance.");
            var files = Directory.GetFiles(Folder, "*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (files.Length != 18) throw new InvalidOperationException("Expected all 18 foreground assets.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/BreathSprites.mat");
            if (material == null || !material.shader.isSupported) throw new InvalidOperationException("URP sprite material missing.");
            var sprites = files.Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            if (sprites.Any(sprite => sprite == null)) throw new InvalidOperationException("Import the foreground PNGs as sprites first.");
            var bounds = files.Select(VisibleBounds).ToArray();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                var previous = root.transform.Find(RootName);
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var decorations = new GameObject(RootName).transform;
                decorations.SetParent(root.transform, false);
                var level = root.GetComponent<BreathLevel>();
                var parallax = decorations.gameObject.AddComponent<LevelForegroundParallax>();
                parallax.parallaxSpeed = 1.15f; level.foreground = parallax;
                float end = (float)(level.settings.CreatePattern().Lead + level.settings.durationSeconds) * level.settings.moveSpeed * parallax.parallaxSpeed + 30;
                var random = new System.Random(1105);
                float cursor = -16, previousWidth = 0;
                for (int i = 0; cursor < end || i < files.Length; i++) {
                    // Coprime stride alternates the six compositions with standalone plants/rocks.
                    int asset = i * 7 % files.Length;
                    string name = Path.GetFileNameWithoutExtension(files[asset]);
                    float visibleHeight = name.Contains("pillar") ? 2.3f : name.Contains("mushroom_red") ? .7f
                        : name.Contains("pebble") || name.Contains("stepping") ? .8f : 1.3f;
                    visibleHeight *= 1 + (i % 3 - 1) * .1f;
                    visibleHeight *= (2 + (float)random.NextDouble()) * .5f;
                    float scale = visibleHeight * sprites[asset].pixelsPerUnit / bounds[asset].height;
                    float visibleWidth = bounds[asset].width / sprites[asset].pixelsPerUnit * scale;
                    if (i > 0) cursor += (previousWidth + visibleWidth) / 2 + 2.5f + (float)random.NextDouble() * 6;
                    previousWidth = visibleWidth;
                    var go = new GameObject(name + "_" + i.ToString("D2"));
                    go.transform.SetParent(decorations, false);
                    // Use the alpha bounds rather than the padded 2048px canvas to place every base on the ground.
                    Vector2 pivot = sprites[asset].pivot;
                    float offsetX = (bounds[asset].center.x - pivot.x) / sprites[asset].pixelsPerUnit * scale;
                    float offsetY = (bounds[asset].yMin - pivot.y) / sprites[asset].pixelsPerUnit * scale;
                    float ground = -6.1f + (float)random.NextDouble() * .3f;
                    go.transform.localPosition = new Vector3(level.spawnPoint.localPosition.x + cursor - offsetX, ground - offsetY, 4);
                    go.transform.localScale = Vector3.one * scale;
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[asset]; renderer.sharedMaterial = material;
                    float shade = .54f + (float)random.NextDouble() * .1f;
                    renderer.color = new Color(shade * .96f, shade, shade * .94f, 1);
                    renderer.sortingOrder = -5;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            if (!Mathf.Approximately(trees.sizeMultiplier, 2)) {
                trees.sizeMultiplier = 2; trees.Sync(); EditorUtility.SetDirty(trees);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            AnimatedForegroundSetup.AlignToBackgroundBottom();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("WingSinger: deployed 18 foreground variants to " + prefabPath + "; tree image scale = 2.");
        }

        static Rect VisibleBounds(string path) {
            var image = new Texture2D(2, 2);
            try {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot decode " + path);
                var pixels = image.GetPixels32(); int left = image.width, right = -1, bottom = image.height, top = -1;
                for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++) {
                    if (pixels[y * image.width + x].a < 16) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
                if (right < left) throw new InvalidOperationException("Empty foreground " + path);
                return Rect.MinMaxRect(left, bottom, right + 1, top + 1);
            } finally { Object.DestroyImmediate(image); }
        }

        [MenuItem("WingSinger/Validate Level Foreground Decorations")]
        public static void Validate() {
            UIIntegrationSetup.RequireSavedScenes();
            var flow = Object.FindObjectOfType<BreathUIFlowController>();
            flow.profileKey = ""; flow.sensorInput.enabled = false;
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Restore", false);
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            started = EditorApplication.timeSinceStartup; checks.Clear(); EditorApplication.isPlaying = true;
        }
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks.Add("PASS: " + text); }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Restore", false)) {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath); return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                if (routine == null) { started = EditorApplication.timeSinceStartup; routine = Run(); }
                if (EditorApplication.timeSinceStartup - started > 60) throw new Exception("Foreground validation timed out.");
                if (!routine.MoveNext()) Finish(null);
            } catch (Exception error) { Finish(error.ToString()); }
        }
        static IEnumerator Run() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); var manager = flow.levelManager;
            manager.input = new GameObject("Foreground validation input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            int initial = manager.initialLevel;
            var decorations = manager.levels[initial].transform.Find(RootName);
            Check(decorations != null && !decorations.gameObject.activeInHierarchy, "Menu keeps level decorations disabled");
            int[] roots = manager.levels.Select(level => level.GetInstanceID()).ToArray();
            var renderers = decorations.GetComponentsInChildren<SpriteRenderer>(true);
            Check(renderers.Select(item => item.sprite).Distinct().Count() == 18, "All 18 new assets are deployed in the level Prefab");
            Check(renderers.All(item => item.sharedMaterial.shader.isSupported && item.sortingOrder == -5 && item.GetComponent<Collider2D>() == null), "URP decorations render above trees, below gameplay, without collisions");
            var parallax = manager.levels[initial].foreground;
            Check(parallax != null && parallax.transform == decorations, "Level explicitly owns the foreground lifecycle");
            var alphaBounds = renderers.Select(item => item.sprite).Distinct().ToDictionary(sprite => sprite,
                sprite => VisibleBounds(AssetDatabase.GetAssetPath(sprite)));
            var edges = new List<Vector2>();
            for (int i = 0; i < renderers.Length; i++) {
                var item = renderers[i]; string name = item.sprite.name;
                int ordinal = int.Parse(item.name.Substring(item.name.LastIndexOf('_') + 1));
                float oldHeight = name.Contains("pillar") ? 2.3f : name.Contains("mushroom_red") ? .7f
                    : name.Contains("pebble") || name.Contains("stepping") ? .8f : 1.3f;
                oldHeight *= 1 + (ordinal % 3 - 1) * .1f;
                var rect = alphaBounds[item.sprite]; float units = item.transform.localScale.x / item.sprite.pixelsPerUnit;
                float ratio = rect.height * units / oldHeight;
                Check(ratio >= .999f && ratio <= 1.501f, item.name + " size is 50 percent of the previous enlarged preset (1 to 1.5 times the initial size)");
                float x = item.transform.localPosition.x;
                edges.Add(new Vector2(x + (rect.xMin - item.sprite.pivot.x) * units, x + (rect.xMax - item.sprite.pivot.x) * units));
            }
            edges.Sort((a, b) => a.x.CompareTo(b.x));
            var gaps = Enumerable.Range(1, edges.Count - 1).Select(i => edges[i].x - edges[i - 1].y).ToArray();
            Check(gaps.All(gap => gap >= 2.49f && gap <= 8.51f) && gaps.Max() - gaps.Min() > 3, "Visible edge gaps vary randomly from 2.5 to 8.5 world units");
            Check(renderers.All(item => item.color.maxColorComponent < .65f && item.color.a == 1), "Foreground is darkened with opaque tints below 65 percent brightness");
            float requiredEnd = (float)(manager.levels[initial].settings.CreatePattern().Lead + manager.levels[initial].settings.durationSeconds)
                * manager.levels[initial].settings.moveSpeed * parallax.parallaxSpeed + 20;
            Check(edges.Last().y > requiredEnd, "Layout covers the full route at the matched foreground speed");
            var transforms = renderers.Select(item => item.transform.localPosition).ToArray();
            flow.router.Route(UIRouteAction.SelectLevel, level: initial + 1); flow.router.Route(UIRouteAction.StartFlight);
            yield return null; yield return null;
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            trees.Sync();
            float originalHeight = ((trees.worldSpaceVertical ? trees.referenceOrthographicSize : Camera.main.orthographicSize) * 2 + .02f) * .55f;
            Check(Mathf.Abs(trees.transform.localScale.y - originalHeight * 2) < .001f
                && Mathf.Abs(trees.WorldPeriod - originalHeight * 2 * trees.sourceAspectRatio) < .001f, "Tree height and width are exactly twice the previous dimensions");
            var background = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Background");
            float shiftedBottom = trees.worldSpaceVertical ? background.transform.position.y - background.transform.localScale.y / 2
                : Camera.main.transform.position.y - originalHeight / .55f / 2 - trees.transform.localScale.y * .15f;
            Check(Mathf.Approximately(trees.verticalOffsetFraction, -.15f)
                && Mathf.Abs(trees.transform.position.y - trees.transform.localScale.y / 2 - shiftedBottom) < .002f,
                "Midground preserves its authored size and aligns to the background bottom in world-space framing");
            var camera = Camera.main; var cameraPosition = camera.transform.position;
            parallax.Sync(); float before = decorations.position.x - cameraPosition.x;
            camera.transform.position += Vector3.right * 3; parallax.Sync();
            float screenShift = before - (decorations.position.x - camera.transform.position.x);
            Check(Mathf.Approximately(parallax.parallaxSpeed, trees.parallaxSpeed) && Mathf.Abs(screenShift - 3 * parallax.parallaxSpeed) < .001f,
                "Foreground and trees both move at 1.15x camera displacement");
            camera.transform.position = cameraPosition; parallax.Sync();
            for (int step = 0; step < 3; step++) {
                manager.Restart();
                manager.Session.Advance(manager.Session.Pattern.Lead + step * 16, BreathLane.Middle);
                manager.player.Present(manager.Session, manager.CurrentLevel.settings); manager.CurrentLevel.Present(manager.Session);
                Camera.main.transform.position = manager.player.cameraTarget.position + new Vector3(manager.cameraHorizontalOffset, 0, -10);
                parallax.Sync();
                NovemberArtSetup.Capture(flow, "Docs/Images/LevelForeground_" + step + ".png", 1280, 720);
            }
            flow.router.Route(UIRouteAction.Pause); yield return null; int frame = trees.AnimationFrame;
            var pausedPosition = decorations.localPosition;
            double until = EditorApplication.timeSinceStartup + .2;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(trees.AnimationFrame == frame, "Pause freezes enlarged tree animation");
            Check(decorations.localPosition == pausedPosition, "Pause freezes foreground parallax");
            flow.router.Route(UIRouteAction.Resume);
            manager.Restart();
            Check(decorations.localPosition == Vector3.zero, "Restart explicitly resets accumulated foreground displacement");
            yield return null;
            Check(decorations.gameObject.activeInHierarchy && transforms.SequenceEqual(renderers.Select(item => item.transform.localPosition)), "Restart reuses decorations at their original positions");
            manager.SwitchLevel((initial + 1) % manager.levels.Length); yield return null;
            Check(!decorations.gameObject.activeInHierarchy && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "Switching levels disables every decoration from the previous level");
            Check(decorations.localPosition == Vector3.zero, "Level exit resets parallax and clears camera bindings");
            manager.SwitchLevel(initial); yield return null;
            Check(decorations.gameObject.activeInHierarchy && roots.SequenceEqual(manager.levels.Select(level => level.GetInstanceID())), "Returning to the level reuses its Prefab instance and decorations");
            flow.ExitSession(); yield return null;
            Check(!decorations.gameObject.activeInHierarchy && trees.AnimationFrame == 0 && manager.Session == null, "Exit disables decorations and resets tree animation without callbacks");
        }
        static void Finish(string error) {
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation"); File.WriteAllText(Report, string.Join("\n", checks) + "\n" + (error ?? "SUCCESS"));
            if (error != null) Debug.LogError(error);
            routine = null; SessionState.SetBool(Key + "Restore", true); EditorApplication.isPlaying = false;
        }
    }
}
