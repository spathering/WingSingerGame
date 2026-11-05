using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class NovemberArtSetup {
        const string Folder = "Assets/Art/UI/Animation/";
        const string Key = "WingSinger.NovemberArt";
        static readonly UIScreenId[] Screens = { UIScreenId.LevelSelect, UIScreenId.ReadyForTakeoff, UIScreenId.FlightPaused, UIScreenId.FlightComplete, UIScreenId.FlightLog, UIScreenId.HomeClean, UIScreenId.HomeDashboard };
        static readonly string[] Targets = { "Artwork/Airship", "Artwork/Airship", "Artwork/Mascot", "Artwork/Celebration", "Artwork/Mascot", "Artwork/Airship", "Artwork/Airship" };
        static IEnumerator routine;
        static readonly List<string> checks = new List<string>();
        static NovemberArtSetup() { EditorApplication.update += Tick; }

        [MenuItem("WingSinger/UI/Apply November Artwork")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            var manifest = JsonUtility.FromJson<BreathArtSetup.Manifest>(File.ReadAllText(Folder + "UIAnimationManifest.json"));
            var clips = manifest.clips.Select(info => BreathArtSetup.CreateSpriteClip(info, Folder, 8192)).ToArray();
            AssetDatabase.ImportAsset("Assets/Art/Background/SkyPanorama.png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Background/SkyPanorama.png");
            const string bgPath = Folder + "MenuBackground.asset";
            var background = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>(bgPath);
            if (background == null) {
                background = ScriptableObject.CreateInstance<CharacterSpriteClip>();
                AssetDatabase.CreateAsset(background, bgPath);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                sprite.name = "NovemberForestBackground"; AssetDatabase.AddObjectToAsset(sprite, background);
                background.frames = new[] { sprite }; background.frameSeconds = new[] { 1f }; EditorUtility.SetDirty(background);
            }
            foreach (string path in Directory.GetFiles("Assets/Prefabs/UI/Screens", "*.prefab")) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root.GetComponent<UIPageView>(), clips, background.frames[0]); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { "Assets/Scenes/UI/UIFlowPreview.unity", BreathDemoBuilder.ScenePath }) {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var page in Object.FindObjectsOfType<UIPageView>(true)) Configure(page, clips, background.frames[0]);
                if (path == BreathDemoBuilder.ScenePath) {
                    var layer = Object.FindObjectsOfType<LoopingPanorama>().Single(item => item.name == "Looping Background");
                    layer.sourceAspectRatio = 32768f / 1443; layer.Sync(); EditorUtility.SetDirty(layer);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        static void Configure(UIPageView page, CharacterSpriteClip[] clips, Sprite background) {
            var bg = page.transform.Find("Background").GetComponent<Image>(); bg.sprite = background;
            EditorUtility.SetDirty(bg); PrefabUtility.RecordPrefabInstancePropertyModifications(bg);
            int index = Array.IndexOf(Screens, page.screenId);
            if (index < 0) return;
            var target = page.transform.Find(Targets[index]);
            if (target == null) throw new InvalidOperationException("Missing UI animation target: " + page.screenId);
            var clip = clips[index >= 5 ? 1 : index];
            var image = target.GetComponent<Image>(); image.sprite = clip.frames[0]; image.preserveAspect = true; image.raycastTarget = false;
            var animation = target.GetComponent<UISpriteAnimation>() ?? target.gameObject.AddComponent<UISpriteAnimation>();
            animation.clip = clip;
            // The previous level-select art extended beyond the viewport. Fit the new complete balloon beside the cards.
            if (index == 0) {
                var rect = image.rectTransform;
                rect.anchoredPosition = new Vector2(25, -500); rect.sizeDelta = new Vector2(550, 900);
                EditorUtility.SetDirty(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
            foreach (var item in new Component[] { image, animation }) {
                EditorUtility.SetDirty(item); PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }
        public static void ApplyAndValidateBatch() {
            try {
                Apply();
                var flow = Object.FindObjectOfType<BreathUIFlowController>(); flow.profileKey = ""; flow.sensorInput.enabled = false;
                SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Stage", 0);
                SessionState.SetBool(Key + "Background", Application.runInBackground);
                SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
                EditorApplication.isPlaying = true;
            } catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetInt(Key + "Stage", 0) == 1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1)); return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "Stage", 0) != 0) return;
            try {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 120) throw new Exception("Art validation timeout");
                if (routine == null) { checks.Clear(); routine = Validate(); }
                if (!routine.MoveNext()) Finish(true, null);
            } catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks.Add(description); }
        static IEnumerator Validate() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>();
            foreach (var screen in Screens) {
                flow.router.Show(screen, false); yield return null;
                var page = flow.router.Page(screen); var animation = page.GetComponentInChildren<UISpriteAnimation>();
                Check(animation != null && animation.clip.frames.Length == 100 && Math.Abs(animation.clip.Duration - 5) < .01f
                    && animation.clip.frameSeconds.All(seconds => Math.Abs(seconds - .05f) < .0001f), screen + " has 100 frames at 20 FPS / five-second loop, matching player timing");
                animation.clip.SamplePair(.0125, out var firstFrame, out var nextFrame, out float weight);
                Check(firstFrame == animation.clip.frames[0] && nextFrame == animation.clip.frames[1] && Mathf.Abs(weight-.25f)<.001f,
                    screen + " interpolates at 12.5ms, between 20 FPS source frames");
                Check(animation.GetComponent<Image>().material.shader.name == "WingSinger/UI Frame Blend"
                    && animation.GetComponent<Image>().material.shader.isSupported, screen + " uses the supported premultiplied-alpha UI blend shader");
                if (screen == UIScreenId.HomeClean || screen == UIScreenId.HomeDashboard)
                    Check(animation.clip == flow.router.Page(UIScreenId.ReadyForTakeoff).GetComponentInChildren<UISpriteAnimation>(true).clip, screen + " shares the takeoff preparation animation");
                Time.timeScale = 0;
                var image = animation.GetComponent<Image>(); var first = image.sprite;
                double until = EditorApplication.timeSinceStartup + 2;
                while (image.sprite == first && EditorApplication.timeSinceStartup < until) yield return null;
                Check(image.sprite != first, screen + " animates at zero gameplay time scale");
                Capture(flow, "Docs/Images/NovemberArt/" + screen + ".png", 1280, 720);
                page.gameObject.SetActive(false);
                Check(image.sprite == animation.clip.frames[0], screen + " resets when hidden");
                page.gameObject.SetActive(true);
                foreach (var size in new[] { new Vector2Int(1024,768), new Vector2Int(2560,1080) })
                    Capture(flow, "Docs/Images/NovemberArt/" + screen + "_" + size.x + ".png", size.x, size.y);
                Time.timeScale = 1;
            }
            flow.router.Route(UIRouteAction.Navigate, UIScreenId.HomeClean);
            var manager = flow.levelManager;
            manager.input = new GameObject("Art validation input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
            var layers = Object.FindObjectsOfType<LoopingPanorama>();
            var background = layers.Single(layer => layer.name == "Looping Background");
            Check(Math.Abs(background.sourceAspectRatio - 32768f / 1443) < .001f, "Background uses the new panorama aspect ratio without stretching");
            foreach (var layer in layers) {
                var camera = Camera.main; var position = camera.transform.position; layer.Sync(); float phase = layer.HorizontalPhase;
                camera.transform.position += Vector3.right * (layer.WorldPeriod / layer.parallaxSpeed); layer.Sync();
                Check(Mathf.Abs(Mathf.DeltaAngle(phase * 360, layer.HorizontalPhase * 360)) < .02f, layer.name + " repeats after one scroll period");
                camera.transform.position = position; layer.Sync();
            }
            Capture(flow, "Docs/Images/NovemberArt/Gameplay.png", 1280, 720);
            int[] roots = manager.levels.Select(level => level.GetInstanceID()).ToArray();
            manager.Restart(); manager.SwitchLevel(1);
            Check(manager.levels.Count(level => level.gameObject.activeSelf) == 1 && roots.SequenceEqual(manager.levels.Select(level => level.GetInstanceID())), "Restart and switch preserve one active level and reuse instances");
            flow.ExitSession();
            Check(manager.Session == null && flow.sensorInput.Receiver == null, "Art validation exits without hardware input or saved progress");
        }
        public static void Capture(BreathUIFlowController flow, string path, int width, int height) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var camera = Camera.main; var canvas = flow.GetComponent<Canvas>();
            var oldTarget = camera.targetTexture; var oldAspect = camera.aspect; var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera; var oldDistance = canvas.planeDistance; int oldOrder = canvas.sortingOrder;
            var oldActive = RenderTexture.active; var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try {
                target.Create(); camera.targetTexture = target; camera.aspect = (float)width / height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f; canvas.sortingOrder = 32000;
                foreach (var layer in Object.FindObjectsOfType<LoopingPanorama>()) layer.Sync();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            } finally {
                camera.targetTexture = oldTarget; camera.aspect = oldAspect; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldDistance; canvas.sortingOrder = oldOrder; RenderTexture.active = oldActive;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        static void Finish(bool success, string error) {
            Time.timeScale = 1;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText("Docs/Validation/NovemberArt.txt", string.Join("\n", checks.Select(check => "PASS: " + check)) + "\n" + (success ? "SUCCESS" : error));
            if (!success) Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Stage", 1); SessionState.SetInt(Key + "Exit", success ? 0 : 1); EditorApplication.isPlaying = false;
        }
    }
}
