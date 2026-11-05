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
    public static class AnimatedForegroundSetup {
        const string Folder = "Assets/Art/Background/AnimatedForeground/";
        const string Key = "WingSinger.AnimatedForegroundValidation";
        static IEnumerator routine;
        static readonly List<string> checks = new List<string>();
        [Serializable] class Frame { public string file; }
        [Serializable] class Manifest { public float sourceWidth, sourceHeight, framesPerSecond, height, contentHeight; public Frame[] frames; }
        static AnimatedForegroundSetup() { EditorApplication.update += Tick; }

        [MenuItem("WingSinger/Apply Animated Foreground")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            ApplyToOpenDemo(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }
        public static void ApplyToOpenDemo() {
            if (!File.Exists(Folder + "Manifest.json")) return;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "Manifest.json"));
            var frames = manifest.frames.Select(frame => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + frame.file)).ToArray();
            if (frames.Length != 150 || frames.Any(frame => frame == null)) throw new InvalidOperationException("The compressed foreground sequence is incomplete.");
            var layer = Object.FindObjectsOfType<LoopingPanorama>().Single(item => item.name == "Looping Foreground");
            layer.animationFrames = frames; layer.animationFramesPerSecond = manifest.framesPerSecond;
            layer.sizeMultiplier = 2;
            layer.verticalOffsetFraction = -.15f;
            layer.animationVerticalScale = manifest.contentHeight / manifest.height;
            layer.animationSessionSource = Object.FindObjectOfType<BreathLevelManager>();
            layer.sourceAspectRatio = manifest.sourceWidth / manifest.sourceHeight;
            layer.backgroundRenderer.sharedMaterial.mainTexture = frames[0];
            EditorUtility.SetDirty(layer.backgroundRenderer.sharedMaterial); EditorUtility.SetDirty(layer); layer.Sync();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        public static void LowerMidground() {
            UIIntegrationSetup.RequireSavedScenes();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != BreathDemoBuilder.ScenePath) throw new InvalidOperationException("Open BreathDemo first.");
            var layer = Object.FindObjectsOfType<LoopingPanorama>().Single(item => item.name == "Looping Foreground");
            layer.verticalOffsetFraction = -.15f; layer.Sync(); EditorUtility.SetDirty(layer);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
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
        public static void ValidateSmoothBatch() {
            SessionState.SetBool(Key + "ThenUI", true);
            ApplyAndValidateBatch();
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetInt(Key + "Stage", 0) == 1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                if (SessionState.GetBool(Key + "ThenUI", false) && SessionState.GetInt(Key + "Exit", 1) == 0) {
                    SessionState.SetBool(Key + "ThenUI", false); NovemberArtSetup.ApplyAndValidateBatch(); return;
                }
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1)); return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "Stage", 0) != 0) return;
            try {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 60) throw new Exception("Foreground validation timed out");
                if (routine == null) { checks.Clear(); routine = Validate(); }
                if (!routine.MoveNext()) Finish(true, null);
            } catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
        static IEnumerator Validate() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); var manager = flow.levelManager;
            var layer = Object.FindObjectsOfType<LoopingPanorama>().Single(item => item.name == "Looping Foreground");
            Check(layer.animationFrames.Length == 150 && layer.animationFramesPerSecond == 20, "All 150 frames play at 20 FPS");
            Check(layer.animationFrames.All(frame => frame.width == 4096 && frame.height == 216 && frame.format == TextureFormat.BC7 && !frame.isReadable && frame.mipmapCount == 1),
                "All frames use 4096x216 BC7 without CPU copies or mipmaps (first=" + layer.animationFrames[0].width + "x" + layer.animationFrames[0].height + ", " + layer.animationFrames[0].format + ")");
            Check(Mathf.Abs(layer.animationVerticalScale - 213f/216) < .0001f, "UVs crop only the three compression-padding rows");
            Check(Mathf.Abs(layer.sourceAspectRatio - 30000f / 1562) < .001f && layer.parallaxSpeed == 1.15f, "Original aspect and 1.15x parallax preserved");
            Check(layer.AnimationFrame == 0 && layer.animationSessionSource == manager, "Menu holds the first frame on the shared foreground");
            manager.input = new GameObject("Foreground test input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
            double until = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(layer.AnimationFrame > 0, "Foreground animates during gameplay");
            var block = new MaterialPropertyBlock(); layer.backgroundRenderer.GetPropertyBlock(block);
            Check(block.GetTexture(Shader.PropertyToID("_MainTex")) == layer.animationFrames[layer.AnimationFrame], "Selected animation texture reaches the renderer property block");
            Check(layer.backgroundRenderer.sharedMaterial.shader.isSupported && block.GetTexture(Shader.PropertyToID("_NextTex"))
                == layer.animationFrames[(layer.AnimationFrame + 1) % 150], "Supported shader receives the next frame for continuous interpolation");
            Check(layer.FrameBlend > 0 && layer.FrameBlend < 1 && Mathf.Approximately(block.GetFloat("_FrameBlend"), layer.FrameBlend),
                "Foreground blends between source frames rather than stepping at 20 FPS");
            bool wrapped = false; int previous = layer.AnimationFrame;
            until = EditorApplication.timeSinceStartup + 10;
            while (!wrapped && EditorApplication.timeSinceStartup < until) {
                yield return null;
                wrapped = previous > 130 && layer.AnimationFrame < 20;
                previous = layer.AnimationFrame;
            }
            Check(wrapped, "Animation wraps from the last frames to the first without a texture gap");
            flow.router.Route(UIRouteAction.Pause); int paused = layer.AnimationFrame;
            until = EditorApplication.timeSinceStartup + .15;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(layer.AnimationFrame == paused, "Pause freezes the trees");
            flow.router.Route(UIRouteAction.Resume);
            manager.Restart(); yield return null; yield return null;
            Check(layer.AnimationFrame < 4, "Restart resets foreground animation");
            int id = layer.GetInstanceID(); manager.SwitchLevel(1); yield return null;
            Check(layer.GetInstanceID() == id && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "Switching levels reuses the shared foreground and activates one level");
            var camera = Camera.main; var position = camera.transform.position; layer.Sync(); float phase = layer.HorizontalPhase;
            camera.transform.position += Vector3.right * (layer.WorldPeriod / layer.parallaxSpeed); layer.Sync();
            Check(Mathf.Abs(Mathf.DeltaAngle(phase * 360, layer.HorizontalPhase * 360)) < .02f, "Animated foreground still repeats spatially");
            camera.transform.position = position;
            manager.Session.Advance(manager.Session.Pattern.Lead + 3, BreathLane.Middle);
            manager.player.Present(manager.Session, manager.CurrentLevel.settings); manager.CurrentLevel.Present(manager.Session);
            camera.transform.position = manager.player.cameraTarget.position + new Vector3(4, 0, -10);
            foreach (var panorama in Object.FindObjectsOfType<LoopingPanorama>()) panorama.Sync();
            NovemberArtSetup.Capture(flow, "Docs/Images/AnimatedForeground.png", 1280, 720);
            flow.ExitSession(); yield return null;
            Check(layer.AnimationFrame == 0 && layer.backgroundRenderer.sharedMaterial.mainTexture == layer.animationFrames[0], "Exit clears animation time without modifying the shared material each frame");
            long bytes = layer.animationFrames.Sum(frame => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(frame));
            checks.Add("Resident texture memory reported by Unity: " + bytes + " bytes");
        }
        static void Finish(bool success, string error) {
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation"); File.WriteAllText("Docs/Validation/AnimatedForeground.txt", string.Join("\n", checks.Select(check => "PASS: " + check)) + "\n" + (success ? "SUCCESS" : error));
            if (!success) Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Stage", 1); SessionState.SetInt(Key + "Exit", success ? 0 : 1); EditorApplication.isPlaying = false;
        }
    }
}
