using System;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WingSinger.Editor {
    // A request file permits an explicit, one-shot build in an already open editor.
    [InitializeOnLoad]
    public static class BreathDemoBuilder {
        public const string ScenePath = "Assets/Scenes/BreathDemo.unity";
        const string Request = "Temp/WingSingerBuild.request";
        const string Report = "Logs/WingSingerBuild.txt";
        static Sprite disc, glow;
        static Material spriteMaterial, lineMaterial;
        static TMP_FontAsset font;
        static readonly Color Cyan = new Color(0.3f, 0.95f, 1);
        static readonly Color Purple = new Color(0.76f, 0.53f, 1);
        static readonly Color Ink = new Color(0.025f, 0.045f, 0.1f);

        static BreathDemoBuilder() { EditorApplication.update += HandleRequest; }

        static void HandleRequest() {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception error) { Directory.CreateDirectory("Logs"); File.WriteAllText(Report, error.ToString()); Debug.LogException(error); }
        }

        [MenuItem("WingSinger/Build Breathing Demo")]
        public static void Build() {
            // Never silently discard changes in the user's currently open scene.
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save the open scene before building the demo.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("The demo already exists. Open BreathDemo.unity; no existing assets were overwritten.");
            EnsureFolder("Assets/Art"); EnsureFolder("Assets/Settings/Levels");
            EnsureFolder("Assets/Prefabs/Levels");
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
                AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("TMP Essential Resources font could not be imported.");
            disc = CreateSprite("Disc", false); glow = CreateSprite("Glow", true);
            var shader = Shader.Find("WingSinger/URP Glow Sprite");
            if (shader == null) throw new InvalidOperationException("URP glow sprite shader is missing.");
            spriteMaterial = new Material(shader) { name = "BreathSprites" };
            lineMaterial = new Material(shader) { name = "BreathLines" };
            AssetDatabase.CreateAsset(spriteMaterial, "Assets/Art/BreathSprites.mat");
            AssetDatabase.CreateAsset(lineMaterial, "Assets/Art/BreathLines.mat");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.ambientLight = Color.white;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5.7f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            camera.transform.position = new Vector3(4, 0, -10); camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.6f); bloom.threshold.Override(0.85f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.2f);
            AssetDatabase.CreateAsset(profile, "Assets/Settings/BreathDemoVolume.asset");
            var volume = new GameObject("Global Volume").AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;

            var player = CreatePlayer();
            var virtualCamera = new GameObject("Flight Camera").AddComponent<CinemachineVirtualCamera>();
            virtualCamera.Follow = player.cameraTarget;
            virtualCamera.m_Lens.OrthographicSize = 5.7f;
            var transposer = virtualCamera.AddCinemachineComponent<CinemachineTransposer>();
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            transposer.m_FollowOffset = new Vector3(4, 0, -10);
            transposer.m_XDamping = transposer.m_YDamping = transposer.m_ZDamping = 0;
            camera.GetComponent<CinemachineBrain>().m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0);

            var levelRoot = new GameObject("LevelRoot").transform;
            var levels = new[] {
                CreateLevel("Level_001_FirstFlight", "01  /  FIRST FLIGHT", 3, 4, 45, Cyan, levelRoot),
                CreateLevel("Level_002_SlowBreath", "02  /  SLOW BREATH", 4, 6, 60, Purple, levelRoot)
            };
            var game = new GameObject("GameRoot");
            var input = game.AddComponent<KeyboardBreathInput>();
            var manager = game.AddComponent<BreathLevelManager>();
            manager.levels = levels; manager.player = player; manager.input = input; manager.virtualCamera = virtualCamera;
            player.ResetPlayer(levels[0].spawnPoint.position);
            // Visible in the scene editor; Start re-establishes this invariant at runtime.
            levels[0].gameObject.SetActive(true); levels[1].gameObject.SetActive(false);
            Selection.activeGameObject = game;
            if (File.Exists("Assets/Art/Character/CharacterManifest.json")) BreathArtSetup.ApplyToOpenDemo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var originalBuildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(originalBuildScenes).ToArray();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs"); File.WriteAllText(Report, "SUCCESS\n" + ScenePath + "\nTwo Prefab instances, shared player/camera/UI, URP effects.\n");
            Debug.Log("WingSinger: demo built. Press Play in BreathDemo.unity.");
        }

        static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Sprite CreateSprite(string name, bool soft) {
            string path = $"Assets/Art/{name}.png";
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) {
                float distance = new Vector2((x + 0.5f - 64) / 64, (y + 0.5f - 64) / 64).magnitude;
                float alpha = soft ? Mathf.Pow(Mathf.Clamp01(1 - distance), 2) : Mathf.Clamp01((1 - distance) * 64);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 128;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static SpriteRenderer Shape(string name, Transform parent, Vector3 position, Vector2 size, Color color, bool soft = false, int order = 0, float angle = 0) {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1); go.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = soft ? glow : disc;
            renderer.sharedMaterial = spriteMaterial; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        static BreathPlayer CreatePlayer() {
            var player = new GameObject("Player").AddComponent<BreathPlayer>();
            player.body = new GameObject("Bird").transform; player.body.SetParent(player.transform, false);
            player.cameraTarget = new GameObject("CameraTarget").transform;
            player.highlight = Shape("Aura", player.body, Vector3.zero, Vector2.one * 2.1f, new Color(0.3f, 0.9f, 1, 0.3f), true, 8);
            Shape("Body", player.body, Vector3.zero, new Vector2(0.85f, 0.55f), new Color(1.4f, 0.9f, 0.35f), false, 12);
            Shape("Wing", player.body, new Vector3(-0.12f, 0.2f, -0.01f), new Vector2(0.7f, 0.3f), new Color(1, 0.7f, 0.28f), false, 13, -28);
            Shape("Tail", player.body, new Vector3(-0.5f, 0.05f, 0), new Vector2(0.5f, 0.16f), new Color(1, 0.7f, 0.28f), false, 11, -15);
            Shape("Beak", player.body, new Vector3(0.43f, 0.02f, 0), new Vector2(0.27f, 0.12f), new Color(1, 0.48f, 0.25f), false, 11);
            Shape("Eye", player.body, new Vector3(0.22f, 0.12f, -0.02f), Vector2.one * 0.09f, Ink, false, 14);
            return player;
        }

        static BreathLevel CreateLevel(string id, string title, float inhale, float exhale, float duration, Color accent, Transform parent) {
            var config = ScriptableObject.CreateInstance<BreathLevelSettings>();
            config.displayName = title; config.inhaleSeconds = inhale; config.exhaleSeconds = exhale; config.durationSeconds = duration;
            AssetDatabase.CreateAsset(config, $"Assets/Settings/Levels/{id}.asset");
            var level = new GameObject(id).AddComponent<BreathLevel>(); level.settings = config;
            level.spawnPoint = new GameObject("SpawnPoint").transform; level.spawnPoint.SetParent(level.transform, false);
            level.guide = new GameObject("Guide").transform; level.guide.SetParent(level.transform, false);
            Shape("LightAura", level.guide, Vector3.zero, Vector2.one * 2, new Color(accent.r, accent.g, accent.b, 0.6f), true, 7);
            Shape("LightCore", level.guide, Vector3.zero, Vector2.one * 0.25f, new Color(2, 2, 2), false, 10);
            Shape("Orbit", level.guide, new Vector3(-0.25f, 0.1f, 0), Vector2.one * 0.1f, accent * 1.8f, false, 9);
            float end = (duration + config.guideLeadSeconds) * config.moveSpeed + 12;
            level.routeGlow = Line("GlowingRoute", level.transform, 0.24f, new Color(accent.r, accent.g, accent.b, 0.17f), 3);
            level.routeCore = Line("StandardRoute", level.transform, 0.065f, new Color(accent.r * 1.7f, accent.g * 1.7f, accent.b * 1.7f), 4);
            level.guide.localPosition = new Vector3(0, (float)config.CreatePattern().HeightAt(0) * config.laneSpacing, -0.2f);
            var random = new System.Random(42);
            for (int i = 0; i < 220; i++) {
                float x = (float)random.NextDouble() * (end + 12) - 12;
                float y = (float)random.NextDouble() * 10 - 5;
                float size = 0.025f + (float)random.NextDouble() * 0.04f;
                Shape("Star", level.transform, new Vector3(x, y, 2), Vector2.one * size,
                    new Color(0.45f, 0.6f, 0.8f, 0.3f + (float)random.NextDouble() * 0.4f), false, -5);
            }
            for (float x = 0; x < end; x += 16) {
                Shape("Nebula", level.transform, new Vector3(x, -3.9f, 3), new Vector2(25, 7), new Color(0.12f, 0.3f, 0.45f, 0.28f), true, -8);
                Shape("Nebula", level.transform, new Vector3(x + 8, 3.8f, 3), new Vector2(20, 6), new Color(0.3f, 0.15f, 0.4f, 0.24f), true, -8);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(level.gameObject, $"Assets/Prefabs/Levels/{id}.prefab");
            UnityEngine.Object.DestroyImmediate(level.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.SetActive(false); return instance.GetComponent<BreathLevel>();
        }

        static LineRenderer Line(string name, Transform parent, float width, Color color, int order) {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.useWorldSpace = false; line.sharedMaterial = lineMaterial; line.widthMultiplier = width;
            line.startColor = line.endColor = color; line.sortingOrder = order;
            line.numCapVertices = 6; line.numCornerVertices = 6; line.positionCount = 0;
            return line;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max) {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = min; rect.offsetMax = max; return rect;
        }

        static TMP_Text Label(string name, Transform parent, string text, float size, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left) {
            var rect = Rect(name, parent, anchorMin, anchorMax, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
            label.fontSize = size; label.color = color; label.alignment = alignment; label.raycastTarget = false;
            label.enableWordWrapping = false; return label;
        }

        static Image Panel(RectTransform rect, Color color) { var image = rect.gameObject.AddComponent<Image>(); image.color = color; return image; }

        static Button Button(string name, Transform parent, string label, Vector2 anchor, Vector2 min, Vector2 max) {
            var rect = Rect(name, parent, anchor, anchor, min, max);
            var image = Panel(rect, new Color(0.12f, 0.22f, 0.31f, 0.95f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label("Label", rect, label, 18, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            return button;
        }

        static BreathHud CreateHud() {
            var canvas = new GameObject("Flight HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var hud = canvas.AddComponent<BreathHud>();
            var top = Rect("Header", canvas.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -135), Vector2.zero);
            Panel(top, new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            Label("Brand", top, "W I N G S I N G E R", 25, Cyan, Vector2.zero, Vector2.one, new Vector2(42, 72), new Vector2(-400, -20));
            hud.levelLabel = Label("Level", top, "01  /  FIRST FLIGHT", 16, new Color(0.65f, 0.76f, 0.85f), Vector2.zero, Vector2.one, new Vector2(44, 28), new Vector2(-450, -70));
            Label("ScoreCaption", top, "SCORE", 12, Cyan, new Vector2(0.52f, 0), new Vector2(0.65f, 1), new Vector2(0, 85), new Vector2(0, -10));
            hud.scoreLabel = Label("Score", top, "0000", 34, Color.white, new Vector2(0.52f, 0), new Vector2(0.65f, 1), new Vector2(0, 30), new Vector2(0, -40));
            hud.timerLabel = Label("Time", top, "45s", 30, Color.white, new Vector2(0.68f, 0), new Vector2(0.77f, 1), new Vector2(0, 35), new Vector2(0, -25));
            hud.restartButton = Button("Restart", top, "R  RESTART", Vector2.one, new Vector2(-320, -86), new Vector2(-178, -36));
            hud.nextButton = Button("Next", top, "N  NEXT", Vector2.one, new Vector2(-160, -86), new Vector2(-30, -36));
            var progress = Rect("Progress", top, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
            Panel(progress, new Color(0.12f, 0.19f, 0.27f));
            hud.progressFill = Rect("Fill", progress, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero); Panel(hud.progressFill, Cyan);

            var bottom = Rect("Breathing HUD", canvas.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 150));
            Panel(bottom, new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            hud.cueLabel = Label("Cue", bottom, "FOLLOW THE LIGHT", 30, Cyan, Vector2.zero, Vector2.one, new Vector2(40, 78), new Vector2(-40, -18), TextAlignmentOptions.Center);
            hud.feedbackLabel = Label("Feedback", bottom, "Get ready to breathe.", 18, Color.white, Vector2.zero, Vector2.one, new Vector2(40, 44), new Vector2(-40, -65), TextAlignmentOptions.Center);
            Label("Keys", bottom, "HOLD W  ·  UPPER / INHALE       RELEASE  ·  MIDDLE / REST       HOLD S  ·  LOWER / EXHALE", 14, new Color(0.55f, 0.68f, 0.8f), Vector2.zero, Vector2.one, new Vector2(30, 12), new Vector2(-30, -108), TextAlignmentOptions.Center);
            Label("Upper", canvas.transform, "INHALE  /  W", 14, Cyan, new Vector2(0, 0.69f), new Vector2(0, 0.69f), new Vector2(24, -12), new Vector2(185, 12));
            Label("Lower", canvas.transform, "EXHALE  /  S", 14, Purple, new Vector2(0, 0.30f), new Vector2(0, 0.30f), new Vector2(24, -12), new Vector2(185, 12));

            var overlay = Rect("Results", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Panel(overlay, new Color(0.01f, 0.025f, 0.065f, 0.8f)); hud.resultPanel = overlay.gameObject;
            var card = Rect("ResultCard", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280, -230), new Vector2(280, 230));
            Panel(card, new Color(0.045f, 0.095f, 0.16f));
            hud.resultLabel = Label("Summary", card, "FLIGHT COMPLETE", 22, Color.white, Vector2.zero, Vector2.one, new Vector2(20, 95), new Vector2(-20, -30), TextAlignmentOptions.Center);
            hud.resultRestartButton = Button("FlyAgain", card, "R  FLY AGAIN", new Vector2(0.5f, 0), new Vector2(-230, 28), new Vector2(-10, 83));
            hud.resultNextButton = Button("NextFlight", card, "N  NEXT FLIGHT", new Vector2(0.5f, 0), new Vector2(10, 28), new Vector2(230, 83));
            overlay.gameObject.SetActive(false);
            return hud;
        }
    }
}
