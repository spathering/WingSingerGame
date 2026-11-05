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
    public static class OpeningBackgroundValidation {
        const string Key = "WingSinger.OpeningBackgroundCheck";
        static IEnumerator routine;
        static readonly List<string> checks = new List<string>();
        static OpeningBackgroundValidation() { EditorApplication.update += Tick; }
        public static void Begin() {
            UIIntegrationSetup.RequireSavedScenes();
            EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            var manager = Object.FindObjectOfType<BreathLevelManager>();
            foreach (var layer in Object.FindObjectsOfType<LoopingPanorama>()) {
                layer.anchorToSessionViewport = true; layer.animationSessionSource = manager; EditorUtility.SetDirty(layer);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); flow.profileKey = ""; flow.sensorInput.enabled = false;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Stage", 0);
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            EditorApplication.isPlaying = true;
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetInt(Key + "Stage", 0) == 1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath); return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "Stage", 0) != 0) return;
            try {
                if (routine == null) { checks.Clear(); routine = Run(); }
                if (!routine.MoveNext()) Finish(null);
            } catch (Exception error) { Finish(error.ToString()); }
        }
        static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks.Add("PASS: " + text); }
        static IEnumerator Run() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); var manager = flow.levelManager;
            manager.input = new GameObject("Opening background test input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
            var camera = Camera.main; float oldAspect = camera.aspect;
            var layers = Object.FindObjectsOfType<LoopingPanorama>();
            int[] roots = manager.levels.Select(level => level.GetInstanceID()).ToArray();
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
            foreach (var size in new[] { new Vector2Int(2560,1080), new Vector2Int(1920,1080), new Vector2Int(865,721) }) {
                camera.aspect = (float)size.x / size.y;
                manager.Restart();
                camera.transform.position = manager.player.cameraTarget.position + new Vector3(4, 0, -10);
                if (size.x == 2560) {
                    foreach (var layer in layers) { layer.anchorToSessionViewport = false; layer.Sync(); }
                    checks.Add("INFO: Old wide-view background phase=" + layers.Single(layer => layer.name == "Looping Background").HorizontalPhase);
                    NovemberArtSetup.Capture(flow, "Docs/Images/OpeningBackgroundBefore.png", size.x, size.y);
                    foreach (var layer in layers) layer.anchorToSessionViewport = true;
                }
                foreach (var layer in layers) {
                    layer.Sync();
                    Check(layer.HorizontalPhase > 0 && layer.HorizontalPhase < .002f, size + " / " + layer.name + " starts after the image border, without wrapping to the tail");
                    Vector3 position = camera.transform.position; float phase = layer.HorizontalPhase;
                    camera.transform.position += Vector3.right * 3; layer.Sync();
                    Check(Mathf.Abs((layer.HorizontalPhase - phase) * layer.WorldPeriod - 3 * layer.parallaxSpeed) < .002f, layer.name + " preserves parallax after opening");
                    camera.transform.position = position; layer.Sync();
                }
                NovemberArtSetup.Capture(flow, "Docs/Images/OpeningBackgroundFixed_" + size.x + ".png", size.x, size.y);
                manager.SwitchLevel(1);
                camera.transform.position = manager.player.cameraTarget.position + new Vector3(4, 0, -10);
                foreach (var layer in layers) { layer.Sync(); Check(layer.HorizontalPhase < .002f, layer.name + " reanchors on level switch"); }
            }
            camera.aspect = oldAspect;
            Check(roots.SequenceEqual(manager.levels.Select(level => level.GetInstanceID())) && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "Opening/restart/switch reuse the same level instances");
            flow.ExitSession(); yield return null;
            Check(manager.Session == null && flow.sensorInput.Receiver == null, "Verification leaves no session, sensor connection or persisted progress");
        }
        static void Finish(string error) {
            var flow = Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
            Time.timeScale = 1; Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation"); File.WriteAllText("Docs/Validation/OpeningBackground.txt", string.Join("\n", checks) + "\n" + (error ?? "SUCCESS"));
            if (error != null) Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Stage", 1); EditorApplication.isPlaying = false;
        }
    }
}
