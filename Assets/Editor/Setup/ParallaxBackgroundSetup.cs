using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WingSinger.UI;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class ParallaxBackgroundSetup {
        const string Folder = "Assets/Art/Background/";
        const string Key = "WingSingerParallaxValidation";
        const string Report = "Docs/Validation/ParallaxBackgroundMergeValidation.txt";
        static double started;
        static ParallaxBackgroundSetup() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) {
                    SessionState.SetBool(Key + "Background", Application.runInBackground);
                    Application.runInBackground = true; started = EditorApplication.timeSinceStartup;
                }
            };
            EditorApplication.update += ValidateTick;
        }

        [MenuItem("WingSinger/Apply Two Layer Background")]
        public static void Apply() {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != BreathDemoBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved BreathDemo scene outside Play mode first.");
            ApplyToOpenDemo(); EditorSceneManager.SaveScene(scene);
        }

        public static void ApplyToOpenDemo() {
            var camera = Camera.main;
            var manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
            if (camera == null || manager == null) throw new InvalidOperationException("Shared camera/manager missing.");
            var settings = manager.levels[manager.initialLevel].settings;
            float approach = settings.playerEntranceSeconds > 0 ? (float)settings.CreatePattern().Lead * settings.moveSpeed : 0;
            float anchor = manager.levels[manager.initialLevel].spawnPoint.position.x + 4 - approach;
            float left = anchor - (camera.orthographicSize * 2 + 0.02f) * camera.aspect / 2;
            ConfigureLayer("Looping Background", "SkyPanorama", 30910f / 1568, 0.65f, 1, 10, -20, anchor, left);
            ConfigureLayer("Looping Foreground", "ForestForeground", 30910f / 1541, 1.15f, 0.55f, 5, -10, anchor, left);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        static void ConfigureLayer(string name, string asset, float aspect, float speed, float height, float depth, int order, float anchor, float left) {
            string path = Folder + asset + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing panorama " + path);
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 16384; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.name = "Standalone"; platform.overridden = true; platform.maxTextureSize = 16384;
            platform.format = TextureImporterFormat.BC7; importer.SetPlatformTextureSettings(platform);
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + asset + ".mat");
            var shader = Shader.Find("WingSinger/URP Parallax Panorama");
            if (shader == null) throw new InvalidOperationException("Parallax shader not imported.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + asset + ".mat"); }
            material.shader = shader; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorUtility.SetDirty(material);
            var go = GameObject.Find(name);
            if (go == null) { go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); }
            var layer = go.GetComponent<LoopingPanorama>();
            if (layer == null) layer = go.AddComponent<LoopingPanorama>();
            layer.targetCamera = Camera.main; layer.backgroundRenderer = go.GetComponent<MeshRenderer>();
            layer.backgroundRenderer.sharedMaterial = material; layer.backgroundRenderer.sortingOrder = order;
            layer.backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off; layer.backgroundRenderer.receiveShadows = false;
            layer.parallaxSpeed = speed; layer.heightFraction = height; layer.verticalAnchor = -1;
            layer.depth = depth; layer.parallaxAnchorX = anchor; layer.originWorldX = left; layer.sourceAspectRatio = aspect;
            layer.Sync(); EditorUtility.SetDirty(layer);
        }

        public static void ApplyAndValidateBatch() {
            try {
                EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                Apply(); ValidateExisting();
            } catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        [MenuItem("WingSinger/Validate Two Layer Background")]
        public static void ValidateExisting() {
            UIIntegrationSetup.RequireSavedScenes();
            EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            var flow = UnityEngine.Object.FindObjectOfType<BreathUIFlowController>();
            if (flow == null) throw new InvalidOperationException("Production navigation is missing.");
            // Test overrides remain in memory: no physical USB connection and no writes to the user's progress.
            flow.profileKey = ""; flow.sensorInput.enabled = false;
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Two-layer background integration validation\n" + DateTimeOffset.Now.ToString("O") + "\n");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }

        static void Check(bool value, string message) {
            if (!value) throw new Exception(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }

        static void ValidateTick() {
            if (SessionState.GetBool(Key + "Restore", false) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key + "Restore", false); EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || started <= 0
                || EditorApplication.timeSinceStartup - started < 1) return;
            bool success = false;
            try {
                var manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                var flow = UnityEngine.Object.FindObjectOfType<BreathUIFlowController>();
                Check(flow != null && flow.Progress != null && flow.profileKey == "" && manager.Session == null,
                    "Production starts at the menu with isolated progress and no auto-started session");
                manager.input = new GameObject("Parallax validation input", typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();
                flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
                Check(manager.Session != null && manager.CurrentIndex == 0 && flow.router.CurrentScreen == UIScreenId.PracticeFollow,
                    "The real navigation starts the first registered gameplay level");
                var layers = UnityEngine.Object.FindObjectsOfType<LoopingPanorama>();
                Check(layers.Length == 2, "Exactly two shared panorama layers");
                Check(layers.All(l => l.transform.parent == null && l.backgroundRenderer.sharedMaterial.shader.isSupported), "Shared layers use a supported URP shader");
                var camera = Camera.main; var position = camera.transform.position; float aspect = camera.aspect;
                foreach (var layer in layers) {
                    layer.Sync(); float phase = layer.HorizontalPhase, period = layer.WorldPeriod;
                    camera.transform.position = position + Vector3.right * 3; layer.Sync();
                    float change = Mathf.Repeat(layer.HorizontalPhase - phase, 1) * period;
                    Check(Mathf.Abs(change - 3 * layer.parallaxSpeed) < 0.002f, layer.name + " tracks its configured speed");
                    camera.transform.position = position + Vector3.right * (period / layer.parallaxSpeed); layer.Sync();
                    Check(Mathf.Abs(Mathf.DeltaAngle(phase * 360, layer.HorizontalPhase * 360)) < 0.01f, layer.name + " loops after a full period");
                    camera.transform.position = position; camera.aspect = 4f / 3; layer.Sync();
                    Check(Mathf.Abs(layer.transform.localScale.x / ((camera.orthographicSize * 2 + 0.02f) * camera.aspect) - 1) < 0.001f, layer.name + " covers a resized viewport");
                    camera.aspect = aspect; layer.Sync();
                }
                var foreground = layers.Single(l => l.name == "Looping Foreground");
                var background = layers.Single(l => l.name == "Looping Background");
                Check(background.parallaxSpeed < foreground.parallaxSpeed, "Foreground scrolls faster than background");
                Check(foreground.backgroundRenderer.sortingOrder > background.backgroundRenderer.sortingOrder && foreground.heightFraction < 1, "Transparent foreground is layered above the sky and anchored below gameplay");
                int[] ids = layers.Select(l => l.GetInstanceID()).OrderBy(i => i).ToArray();
                var firstSession = manager.Session;
                flow.router.Route(UIRouteAction.Pause); flow.router.Route(UIRouteAction.Restart);
                Check(manager.Session != firstSession && manager.CurrentIndex == 0 && Time.timeScale > 0, "Navigation restart resets the session and releases pause");
                flow.router.Route(UIRouteAction.NextLevel); flow.router.Route(UIRouteAction.StartFlight);
                Check(manager.CurrentIndex == 1 && manager.Session.Pattern.Duration == 60, "Next level uses the existing second SO configuration");
                Check(manager.levels.Count(l => l.gameObject.activeSelf) == 1 && layers.All(l => l.gameObject.activeInHierarchy), "Restart/switch keep one level and both shared layers");
                Check(ids.SequenceEqual(UnityEngine.Object.FindObjectsOfType<LoopingPanorama>().Select(l => l.GetInstanceID()).OrderBy(i => i)), "Switch reuses both background objects");
                flow.router.Route(UIRouteAction.Navigate, UIScreenId.LevelSelect);
                flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
                manager.Session.Advance(manager.Session.Pattern.Lead + 4, BreathLane.Middle);
                manager.player.Present(manager.Session, manager.CurrentLevel.settings); manager.CurrentLevel.Present(manager.Session);
                camera.transform.position = manager.player.cameraTarget.position + new Vector3(4, 0, -10);
                foreach (var layer in layers) layer.Sync();
                flow.Refresh();
                BreathArtSetup.Capture("Docs/Images/ParallaxBackground.png");
                flow.router.Route(UIRouteAction.Pause); flow.router.Route(UIRouteAction.Navigate, UIScreenId.LevelSelect);
                Check(manager.Session == null && manager.levels.All(l => !l.gameObject.activeSelf) && layers.All(l => l.gameObject.activeInHierarchy),
                    "Leaving a paused level clears gameplay while retaining the shared background layers");
                Check(Time.timeScale > 0 && flow.Progress.Data.records.Count == 0 && flow.sensorInput.Receiver == null,
                    "Background validation leaves no pause, physical receiver or fabricated completion records");
                File.AppendAllText(Report, "SUCCESS\n"); success = true;
            } catch (Exception error) { File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); }
            finally {
                SessionState.SetBool(Key, false); Application.runInBackground = SessionState.GetBool(Key + "Background", false);
                var flow = UnityEngine.Object.FindObjectOfType<BreathUIFlowController>(); if (flow != null) flow.ExitSession();
                SessionState.SetBool(Key + "Restore", true);
                EditorApplication.isPlaying = false;
                if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
            }
        }
    }
}
