using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class YellowFlightSetup {
        const string Key = "WingSinger.YellowFlightValidation";
        const string StarPath = "Assets/Art/Paper/star_yellow.asset";
        const string CranePath = "Assets/Art/Paper/crane_yellow.asset";
        static IEnumerator routine;
        static readonly List<string> checks = new List<string>();
        static double started;
        static YellowFlightSetup() { EditorApplication.update += Tick; }

        [MenuItem("WingSinger/Apply Yellow Flight Art")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != BreathDemoBuilder.ScenePath)
                throw new InvalidOperationException("Open BreathDemo first.");
            var star = CreateClip("starFrames_yellow", StarPath);
            var crane = CreateClip("crane_yellow", CranePath);
            const string paperPath = "Assets/Prefabs/Gameplay/PaperCollectible.prefab";
            var paperRoot = PrefabUtility.LoadPrefabContents(paperPath);
            try {
                var paper = paperRoot.GetComponent<PaperCollectible>();
                paper.clip = star; paper.visualScale = .36f * 1.5f;
                paper.visual.sprite = star.frames[0]; paper.visual.transform.localScale = Vector3.one * paper.visualScale;
                PrefabUtility.SaveAsPrefabAsset(paperRoot, paperPath);
            } finally { PrefabUtility.UnloadPrefabContents(paperRoot); }
            foreach (string path in new[] { "Assets/Prefabs/Levels/Level_001_FirstFlight.prefab", "Assets/Prefabs/Levels/Level_002_SlowBreath.prefab" }) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var level = root.GetComponent<BreathLevel>();
                    level.guideClip = crane; level.guideVisual.sprite = crane.frames[0];
                    level.guideVisual.transform.localScale = Vector3.one * (.5f * 1.5f);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("WingSinger: both levels use yellow crane/star animations, normalized to the old 2-unit canvas and scaled 1.5x.");
        }

        public static void ApplyFlightLayout() {
            UIIntegrationSetup.RequireSavedScenes();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != BreathDemoBuilder.ScenePath) throw new InvalidOperationException("Open BreathDemo first.");
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            foreach (var level in manager.levels) {
                level.settings.laneCenterOffset = -.84f; level.settings.upperLaneSpacing = 3.3f; level.settings.lowerLaneSpacing = 3.7f;
                level.settings.attractionRadius = 1.2f; level.settings.Validate(); EditorUtility.SetDirty(level.settings);
            }
            var visual = manager.player.characterAnimation.target.transform;
            visual.localScale = Vector3.one * 2; EditorUtility.SetDirty(visual);
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            trees.verticalOffsetFraction = -.15f; trees.Sync(); EditorUtility.SetDirty(trees);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void ApplyFraming() {
            UIIntegrationSetup.RequireSavedScenes();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != BreathDemoBuilder.ScenePath) throw new InvalidOperationException("Open BreathDemo first.");
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            float playerWidth = VisibleWidth(manager.player.characterAnimation.cruise.frames[0],
                manager.player.characterAnimation.target.transform.lossyScale.x, true);
            float halfPlayerHeight = VisibleHeight(manager.player.characterAnimation.cruise.frames[0],
                manager.player.characterAnimation.target.transform.lossyScale.y, true) * .5f;
            manager.cameraHorizontalOffset = 4 + playerWidth * .5f;
            var transposer = manager.virtualCamera.GetCinemachineComponent<Cinemachine.CinemachineTransposer>();
            var offset = transposer.m_FollowOffset; offset.x = manager.cameraHorizontalOffset; transposer.m_FollowOffset = offset;
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(transposer);
            foreach (var level in manager.levels) {
                level.settings.lowerLaneSpacing = 3.7f - halfPlayerHeight;
                level.settings.Validate(); EditorUtility.SetDirty(level.settings);
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(level.gameObject);
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var source = root.GetComponent<BreathLevel>();
                    source.guideHorizontalOffset = playerWidth * .5f + 3 * VisibleWidth(source.guideClip.frames[0], source.guideVisual.transform.localScale.x, false);
                    source.guide.localPosition = new Vector3(source.spawnPoint.localPosition.x + source.guideHorizontalOffset,
                        source.spawnPoint.localPosition.y + source.settings.LaneHeight(0), -.2f);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            trees.verticalOffsetFraction = -.15f; trees.Sync(); EditorUtility.SetDirty(trees);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Framing: player moved right half a width; lower lane raised by " + halfPlayerHeight + " world units; crane moved right two more widths.");
        }

        static float VisibleWidth(Sprite sprite, float scale, bool chromaKey) => VisibleExtent(sprite, scale, chromaKey, false);
        static float VisibleHeight(Sprite sprite, float scale, bool chromaKey) => VisibleExtent(sprite, scale, chromaKey, true);
        static float VisibleExtent(Sprite sprite, float scale, bool chromaKey, bool height) {
            var image = new Texture2D(2, 2);
            try {
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
                var pixels = image.GetPixels32(); var rect = sprite.rect;
                int left = image.width, right = -1, bottom = image.height, top = -1;
                for (int y = (int)rect.yMin; y < (int)rect.yMax; y++) for (int x = (int)rect.xMin; x < (int)rect.xMax; x++) {
                    var color = pixels[y * image.width + x];
                    if (color.a < 16 || (chromaKey && Math.Min(color.r, color.b) - color.g > 180)) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
                if (right < left) throw new InvalidOperationException("Empty sprite: " + sprite.name);
                return (height ? top - bottom + 1 : right - left + 1) / sprite.pixelsPerUnit * scale;
            } finally { Object.DestroyImmediate(image); }
        }

        static CharacterSpriteClip CreateClip(string folder, string assetPath) {
            string source = "Assets/Art/UI/Sprites/" + folder;
            var files = Directory.GetFiles(source, "*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (files.Length != 120) throw new InvalidOperationException("Expected 120 frames in " + source);
            string fingerprint;
            using (var hash = SHA256.Create()) {
                foreach (string file in files) { byte[] bytes = File.ReadAllBytes(file); hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0); }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); fingerprint = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
            var clip = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>(assetPath);
            if (clip != null && clip.sourceSha256 == fingerprint && clip.frames != null && clip.frames.Length == 120 && clip.frames.All(frame => frame != null)) return clip;
            if (clip == null) { clip = ScriptableObject.CreateInstance<CharacterSpriteClip>(); AssetDatabase.CreateAsset(clip, assetPath); }
            else foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>()) Object.DestroyImmediate(sprite, true);
            clip.frames = new Sprite[files.Length]; clip.frameSeconds = Enumerable.Repeat(.02f, files.Length).ToArray();
            clip.sourceFile = source; clip.sourceSha256 = fingerprint;
            for (int i = 0; i < files.Length; i++) {
                string path = files[i].Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) { AssetDatabase.ImportAsset(path); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
                string before = EditorJsonUtility.ToJson(importer);
                importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false; importer.isReadable = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var platform = importer.GetPlatformTextureSettings("Standalone");
                platform.name = "Standalone"; platform.overridden = true; platform.maxTextureSize = 512; platform.format = TextureImporterFormat.BC7;
                importer.SetPlatformTextureSettings(platform);
                if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var frame = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f,.5f), texture.height / 2f, 0, SpriteMeshType.FullRect);
                frame.name = folder + "_" + i.ToString("D3"); AssetDatabase.AddObjectToAsset(frame, clip); clip.frames[i] = frame;
            }
            clip.RebuildTiming(); EditorUtility.SetDirty(clip); return clip;
        }

        [MenuItem("WingSinger/Validate Yellow Flight Art")]
        public static void Validate() {
            UIIntegrationSetup.RequireSavedScenes();
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); flow.profileKey = ""; flow.sensorInput.enabled = false;
            checks.Clear(); SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Restore", false);
            SessionState.SetBool(Key + "Background", Application.runInBackground); EditorApplication.isPlaying = true;
        }
        static void Check(bool value, string text) { if (!value) throw new Exception(text); checks.Add("PASS: " + text); }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Restore", false)) {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath); return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                if (routine == null) { checks.Clear(); started = EditorApplication.timeSinceStartup; routine = Run(); }
                if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Yellow flight validation timed out.");
                if (!routine.MoveNext()) Finish(null);
            } catch (Exception error) { Finish(error.ToString()); }
        }
        static IEnumerator Run() {
            Application.runInBackground = true; for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); var manager = flow.levelManager;
            manager.input = new GameObject("Yellow flight validation input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            var star = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>(StarPath); var crane = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>(CranePath);
            Check(star.frames.Length == 120 && crane.frames.Length == 120 && star.frames.Concat(crane.frames).All(frame => frame != null), "Both yellow animations contain all 120 frames");
            Check(star.Sample(.04, true) != star.Sample(0, true) && crane.Sample(.04, true) != crane.Sample(0, true), "Both animations advance and loop using the existing clip sampler");
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight); yield return null;
            foreach (var level in manager.levels) {
                Check(level.guideClip == crane && Mathf.Abs(level.guideVisual.transform.localScale.x - .75f) < .0001f, level.name + " uses the yellow crane at 1.5x");
                Check(level.collectiblePrefab.clip == star && Mathf.Abs(level.collectiblePrefab.visualScale - .54f) < .0001f, level.name + " uses yellow stars at 1.5x, including runtime reset scale");
            }
            var animator = manager.player.characterAnimation;
            Check(animator.target.transform.localScale == Vector3.one * 2, "Character visual is exactly twice its previous size");
            float halfCharacterHeight = VisibleHeight(animator.cruise.frames[0], animator.target.transform.lossyScale.y, true) * .5f;
            foreach (var level in manager.levels) {
                Check(Mathf.Abs(level.settings.LaneHeight(1) - 2.46f) < .001f && Mathf.Abs(level.settings.LaneHeight(0) + .84f) < .001f
                    && Mathf.Abs(level.settings.LaneHeight(-1) + 4.54f - halfCharacterHeight) < .001f, level.name + " preserves upper/middle lanes and raises the lower lane by half a character height");
                Check(level.Papers.All(paper => Mathf.Abs(paper.transform.localPosition.y - level.settings.LaneHeight((int)level.PaperLayout[paper.Id].Lane)) < .001f),
                    level.name + " paper routes use the same lane mapping as the player");
            }
            animator.ResetAnimation(); var rest = animator.target.transform.localPosition;
            animator.Present(.25f, 0);
            Check(animator.target.transform.localPosition.y > rest.y + .03f, "Idle character floats while the collision body stays at its lane anchor");
            var body = manager.player.body.localPosition;
            animator.Present(.016f, 1); float first = animator.Pose;
            Check(first > 0 && first < .15f && animator.CurrentClip == animator.cruiseToRise, "Rising begins with a gradual forward pose transition");
            for (int i = 0; i < 20; i++) animator.Present(.016f, 1);
            float rising = animator.Pose; animator.Present(.016f, -1);
            Check(animator.Pose > 0 && Mathf.Abs(animator.Pose - rising) < .15f, "Reversing movement changes heading smoothly without flipping instantly");
            for (int i = 0; i < 70; i++) animator.Present(.016f, -1);
            Check(animator.Pose < -.95f, "Descending settles toward its target orientation");
            for (int i = 0; i < 100; i++) animator.Present(.016f, 0);
            Check(Mathf.Abs(animator.Pose) < .005f && animator.CurrentClip == animator.cruise, "Lane dwell eases back to the cruise pose");
            Check(manager.player.body.localPosition == body, "Floating and turning only affect the visual child, preserving collision and camera anchors");
            animator.Present(.016f, 1, true);
            Check(animator.Pose == 0 && animator.target.transform.localPosition == rest, "Reduce Motion disables floating and turning");
            manager.Restart(); manager.Session.Advance(manager.Session.Pattern.Lead + 2, BreathLane.Middle);
            manager.player.Present(manager.Session, manager.CurrentLevel.settings); manager.CurrentLevel.Present(manager.Session);
            Camera.main.transform.position = manager.player.cameraTarget.position + new Vector3(manager.cameraHorizontalOffset,0,-10);
            manager.CurrentLevel.foreground?.Sync();
            animator.Present(.25f, 0);
            NovemberArtSetup.Capture(flow, "Docs/Images/YellowFlight.png", 1280, 720);
            var paper = manager.CurrentLevel.Papers.First(item => item.gameObject.activeSelf);
            Check(paper.clip == star && paper.visual.transform.localScale.x > .53f, "Revealed runtime collectibles use the enlarged yellow animation");
            paper.Collect(); manager.Restart();
            Check(manager.CurrentLevel.Papers.All(item => !item.Collected && Mathf.Abs(item.visual.transform.localScale.x - .54f) < .0001f), "Restart cancels collection feedback and restores enlarged stars");
            flow.router.Route(UIRouteAction.Pause); yield return null;
            var sprite = animator.target.sprite; var position = animator.target.transform.localPosition; var rotation = animator.target.transform.localRotation;
            double until = EditorApplication.timeSinceStartup + .2;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(animator.target.sprite == sprite && animator.target.transform.localPosition == position && animator.target.transform.localRotation == rotation, "Pause freezes sprite playback, float and heading");
            flow.router.Route(UIRouteAction.Resume);
            manager.Restart();
            manager.Session.Advance(manager.Session.Pattern.Lead + .1, BreathLane.Middle);
            manager.CurrentLevel.Present(manager.Session);
            var testPaper = manager.CurrentLevel.Papers.First(item => item.gameObject.activeSelf);
            var testTarget = new GameObject("Absorption test target").transform;
            testTarget.position = testPaper.transform.position + Vector3.right;
            double scoreBefore = manager.Session.Score;
            Check(manager.CurrentLevel.CollectBetween(manager.Session, testTarget.position, testTarget.position, testTarget) > 0,
                "Stars within the 1.2-unit attraction range begin absorption before direct contact");
            Check(manager.CurrentLevel.CollectBetween(manager.Session, testTarget.position, testTarget.position, testTarget) == 0
                && manager.Session.Score > scoreBefore, "Absorption awards score once without duplicates");
            float initialDistance = Vector3.Distance(testPaper.visual.transform.position, testTarget.position);
            until = EditorApplication.timeSinceStartup + .08;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(testPaper.Collected && testPaper.visual.transform.localScale.x < .54f
                && Vector3.Distance(testPaper.visual.transform.position, testTarget.position) < initialDistance,
                "Absorbed stars move toward the character and shrink over time");
            flow.router.Route(UIRouteAction.Pause);
            var attractionPosition = testPaper.visual.transform.position;
            var attractionScale = testPaper.visual.transform.localScale;
            until = EditorApplication.timeSinceStartup + .15;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(testPaper.visual.transform.position == attractionPosition && testPaper.visual.transform.localScale == attractionScale,
                "Pause freezes star attraction and shrink feedback");
            flow.router.Route(UIRouteAction.Resume);
            manager.Restart();
            Check(testPaper.visual.transform.localPosition == Vector3.zero && !testPaper.Collected,
                "Restart cancels attraction and restores the pooled star visual position");
            Object.Destroy(testTarget.gameObject);
            var missed = manager.CurrentLevel.Papers[0];
            missed.Present(100, 100);
            var far = missed.transform.position + Vector3.up * 10 + Vector3.right * 2;
            manager.Session.Advance(manager.Session.Pattern.Lead + .1, BreathLane.Middle);
            manager.CurrentLevel.CollectBetween(manager.Session, far, far);
            Check(missed.gameObject.activeSelf && !missed.Missed, "Passing an uncollected star does not hide it while still in view");
            missed.RecycleIfBehindCamera(missed.visual.bounds.min.x - 1);
            Check(!missed.Missed, "A visible uncollected star remains in the pool and on screen");
            missed.RecycleIfBehindCamera(missed.visual.bounds.max.x + .2f);
            Check(missed.Missed && !missed.gameObject.activeSelf, "Uncollected stars recycle only after their entire visual leaves the camera on the left");
            manager.Restart(); manager.Session.Advance(manager.Session.Pattern.Lead + .1, BreathLane.Middle);
            foreach (var lane in new[] { BreathLane.Upper, BreathLane.Middle, BreathLane.Lower }) {
                ((ExternalBreathInput)manager.input).Submit(lane);
                float peakPose = 0; until = EditorApplication.timeSinceStartup + 1.3;
                while (EditorApplication.timeSinceStartup < until) { peakPose = Mathf.Max(peakPose, animator.Pose * (int)lane); yield return null; }
                Check((lane == BreathLane.Middle || peakPose > .1f) && Mathf.Abs(manager.player.body.localPosition.y - manager.CurrentLevel.settings.LaneHeight((int)lane)) < .001f
                    && Mathf.Abs(animator.Pose) < .01f, "Live " + lane + " lane movement tilts smoothly and returns to cruise without moving the collision anchor");
                var camera = Camera.main;
                float screenY = camera.WorldToViewportPoint(manager.player.body.position).y;
                float expected = lane == BreathLane.Upper ? .716f : lane == BreathLane.Middle ? .4263f : .10175f + halfCharacterHeight / (camera.orthographicSize * 2);
                Check(Mathf.Abs(screenY - expected) < .003f, lane + " lane screen height matches the updated layout");
                manager.CurrentLevel.foreground?.Sync();
                NovemberArtSetup.Capture(flow, "Docs/Images/FlightLayout_" + lane + ".png", 1280, 720);
            }
            var treeLayer = Object.FindObjectsOfType<LoopingPanorama>().Single(layer => layer.name == "Looping Foreground");
            treeLayer.Sync();
            Check(Mathf.Approximately(treeLayer.verticalOffsetFraction, -.15f), "Trees are raised 5 percent from the previous position, totaling a 15 percent downward offset");
            float playerWidth = VisibleWidth(animator.cruise.frames[0], animator.target.transform.lossyScale.x, true);
            Check(Mathf.Abs(manager.cameraHorizontalOffset - 4 - playerWidth * .5f) < .001f
                && Mathf.Abs((manager.player.body.position.x - Camera.main.transform.position.x) - (-4 - playerWidth * .5f)) < .01f,
                "Player is shifted right by half a character width from the previous layout");
            foreach (var level in manager.levels) {
                float craneWidth = VisibleWidth(level.guideClip.frames[0], level.guideVisual.transform.localScale.x, false);
                Check(Mathf.Abs(level.guideHorizontalOffset - playerWidth * .5f - craneWidth * 3) < .001f,
                    level.name + " crane appears two more visible crane widths right after compensating camera framing");
            }
            manager.Next();
            Check(animator.Pose == 0 && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "Switching levels resets heading and preserves one active level");
            flow.ExitSession();
            Check(animator.Pose == 0 && animator.target.transform.localPosition == rest && manager.Session == null, "Exit resets visual motion and ends the gameplay session");
        }
        static void Finish(string error) {
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation"); File.WriteAllText("Docs/Validation/YellowFlight.txt", string.Join("\n", checks) + "\n" + (error ?? "SUCCESS"));
            if (error != null) Debug.LogError(error);
            routine = null; SessionState.SetBool(Key + "Restore", true); EditorApplication.isPlaying = false;
        }
    }
}
