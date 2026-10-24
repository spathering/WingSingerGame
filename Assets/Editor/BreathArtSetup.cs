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
    // Explicit, repeatable art migration. Does not rebuild the gameplay scene or its configuration.
    public static class BreathArtSetup {
        const string CharacterFolder = "Assets/Art/Character/";
        const string PanoramaPath = "Assets/Art/Background/ForestPanorama.png";
        [Serializable] public class Manifest { public ClipInfo[] clips; }
        [Serializable] public class ClipInfo {
            public string name, sourceFile, sourceSha256, atlas;
            public int frameWidth, frameHeight, padding, columns, rows;
            public int[] durationsMs;
        }

        [MenuItem("WingSinger/Apply Forest Art")]
        public static void Apply() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != BreathDemoBuilder.ScenePath)
                throw new InvalidOperationException("Open BreathDemo.unity outside Play mode first.");
            if (scene.isDirty) throw new InvalidOperationException("Save the current scene before applying art.");
            ApplyToOpenDemo();
            EditorSceneManager.SaveScene(scene);
        }

        public static void ApplyToOpenDemo() {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(CharacterFolder + "CharacterManifest.json"));
            if (manifest == null || manifest.clips == null || manifest.clips.Length != 6)
                throw new InvalidOperationException("Convert all six source GIFs first.");
            var clips = manifest.clips.Select(CreateClip).ToArray();
            var player = UnityEngine.Object.FindObjectOfType<BreathPlayer>();
            if (player == null || player.body == null) throw new InvalidOperationException("Player is missing.");
            var material = EnsureMaterial("Assets/Art/Character/Airship.mat", "WingSinger/URP Airship Chroma Key");
            foreach (Transform child in player.body) {
                if (new[] { "Body", "Wing", "Tail", "Beak", "Eye" }.Contains(child.name)) child.gameObject.SetActive(false);
            }
            var airship = player.body.Find("Airship");
            if (airship == null) {
                airship = new GameObject("Airship", typeof(SpriteRenderer)).transform;
                airship.SetParent(player.body, false);
            }
            var sprite = airship.GetComponent<SpriteRenderer>();
            sprite.sharedMaterial = material; sprite.sortingOrder = 12; sprite.color = Color.white;
            var animator = airship.GetComponent<BreathCharacterAnimator>();
            if (animator == null) animator = airship.gameObject.AddComponent<BreathCharacterAnimator>();
            animator.target = sprite;
            animator.cruise = clips[0]; animator.rise = clips[1]; animator.descend = clips[2];
            animator.cruiseToDescend = clips[3]; animator.cruiseToRise = clips[4];
            // Clip 06 exists in the asset browser solely for preview, with no runtime reference.
            player.characterAnimation = animator;
            animator.ResetAnimation();
            EditorUtility.SetDirty(player);

            foreach (string path in new[] { "Assets/Prefabs/Levels/Level_001_FirstFlight.prefab", "Assets/Prefabs/Levels/Level_002_SlowBreath.prefab" }) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var legacy = root.GetComponentsInChildren<Transform>(true)
                        .Where(t => t != root.transform && (t.name.StartsWith("Track_", StringComparison.Ordinal)
                            || t.name.StartsWith("Star", StringComparison.Ordinal)
                            || t.name.StartsWith("Nebula", StringComparison.Ordinal))).ToArray();
                    if (legacy.Length == 0) continue;
                    foreach (var child in legacy) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            ConfigureTexture(PanoramaPath, true);
            var panorama = EnsureMaterial("Assets/Art/Background/ForestPanorama.mat", "WingSinger/URP Looping Panorama");
            panorama.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PanoramaPath);
            EditorUtility.SetDirty(panorama);
            var background = UnityEngine.Object.FindObjectOfType<LoopingPanorama>();
            if (background == null) {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Looping Background";
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                background = quad.AddComponent<LoopingPanorama>();
            }
            background.targetCamera = Camera.main;
            background.backgroundRenderer = background.GetComponent<MeshRenderer>();
            background.backgroundRenderer.sharedMaterial = panorama;
            background.backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            background.backgroundRenderer.receiveShadows = false;
            background.originWorldX = Camera.main.transform.position.x - (Camera.main.orthographicSize * 2 + 0.02f) * Camera.main.aspect / 2;
            var manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
            var initialLevel = manager == null || manager.levels == null ? null : manager.levels.ElementAtOrDefault(manager.initialLevel);
            if (initialLevel != null && initialLevel.settings.playerEntranceSeconds > 0)
                background.originWorldX -= (float)initialLevel.settings.CreatePattern().Lead * initialLevel.settings.moveSpeed;
            background.Sync();
            if (File.Exists("Assets/Art/Background/SkyPanorama.png") && File.Exists("Assets/Art/Background/ForestForeground.png"))
                ParallaxBackgroundSetup.ApplyToOpenDemo();
            var volume = UnityEngine.Object.FindObjectOfType<Volume>();
            if (volume != null && volume.sharedProfile != null) {
                Bloom bloom; Vignette vignette;
                if (volume.sharedProfile.TryGet(out bloom)) { bloom.intensity.Override(0.18f); bloom.threshold.Override(1.05f); }
                if (volume.sharedProfile.TryGet(out vignette)) vignette.intensity.Override(0.10f);
                EditorUtility.SetDirty(volume.sharedProfile);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("WingSinger forest art applied: five gameplay clips, preview-only clip 06, seamless world-space panorama.");
        }

        static Material EnsureMaterial(string path, string shaderName) {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            return material;
        }

        static void ConfigureTexture(string path, bool panorama) {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Texture not found: " + path);
            // Full-resolution panorama retains its 8192:902 aspect ratio; atlases keep exact pixel rectangles.
            int max = panorama ? 8192 : 4096;
            var compression = panorama ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            if (importer.textureType == TextureImporterType.Default && importer.npotScale == TextureImporterNPOTScale.None
                && importer.maxTextureSize == max && importer.wrapModeU == (panorama ? TextureWrapMode.Repeat : TextureWrapMode.Clamp)
                && importer.wrapModeV == TextureWrapMode.Clamp && !importer.mipmapEnabled
                && importer.textureCompression == compression) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = max;
            importer.wrapModeU = panorama ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        static CharacterSpriteClip CreateClip(ClipInfo info) {
            return CreateSpriteClip(info, CharacterFolder);
        }

        public static CharacterSpriteClip CreateSpriteClip(ClipInfo info, string folder) {
            string path = folder + info.name + ".asset";
            var clip = AssetDatabase.LoadAssetAtPath<CharacterSpriteClip>(path);
            if (clip != null && clip.sourceSha256 == info.sourceSha256 && clip.frames != null
                && clip.frames.Length == info.durationsMs.Length && clip.frames.All(f => f != null)) return clip;
            ConfigureTexture(folder + info.atlas, false);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + info.atlas);
            if (clip == null) {
                clip = ScriptableObject.CreateInstance<CharacterSpriteClip>();
                AssetDatabase.CreateAsset(clip, path);
            } else {
                foreach (var child in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) UnityEngine.Object.DestroyImmediate(child, true);
            }
            clip.frames = new Sprite[info.durationsMs.Length];
            clip.frameSeconds = info.durationsMs.Select(ms => ms / 1000f).ToArray();
            clip.sourceFile = info.sourceFile; clip.sourceSha256 = info.sourceSha256;
            for (int i = 0; i < clip.frames.Length; i++) {
                int x = (i % info.columns) * (info.frameWidth + 2 * info.padding) + info.padding;
                int top = (i / info.columns) * (info.frameHeight + 2 * info.padding) + info.padding;
                var rect = new Rect(x, texture.height - top - info.frameHeight, info.frameWidth, info.frameHeight);
                var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), info.frameHeight / 2f, 0, SpriteMeshType.FullRect);
                sprite.name = info.name + "_" + i.ToString("D3");
                AssetDatabase.AddObjectToAsset(sprite, clip); clip.frames[i] = sprite;
            }
            clip.RebuildTiming(); EditorUtility.SetDirty(clip);
            return clip;
        }

        public static void Capture(string path) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var camera = Camera.main;
            var hud = UnityEngine.Object.FindObjectOfType<BreathHud>();
            var canvas = hud == null ? null : hud.GetComponent<Canvas>();
            var previousMode = canvas == null ? RenderMode.ScreenSpaceOverlay : canvas.renderMode;
            var previousCamera = canvas == null ? null : canvas.worldCamera;
            var previousDistance = canvas == null ? 0 : canvas.planeDistance;
            var backgrounds = UnityEngine.Object.FindObjectsOfType<LoopingPanorama>();
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var previousAspect = camera.aspect;
            try {
                camera.aspect = 1600f / 900;
                foreach (var background in backgrounds) background.Sync();
                if (canvas != null) {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = renderTexture;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            } finally {
                RenderTexture.active = previous; camera.aspect = previousAspect;
                if (canvas != null) {
                    canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
                    Canvas.ForceUpdateCanvases();
                }
                foreach (var background in backgrounds) background.Sync();
                renderTexture.Release(); UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
