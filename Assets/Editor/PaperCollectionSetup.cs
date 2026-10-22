using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WingSinger.Editor {
    public static class PaperCollectionSetup {
        const string Folder = "Assets/Art/Paper/";
        const string PrefabPath = "Assets/Prefabs/Gameplay/PaperCollectible.prefab";

        // Entry point for the existing project/editor cache, also usable without an MCP connection.
        public static void ApplyAndValidateBatch() {
            try {
                EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                Apply();
                PaperCollectionValidation.BeginBatch();
            } catch (Exception error) {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PaperCollectionValidation.txt", "FAIL\n" + error);
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("WingSinger/Apply Paper Collection Art")]
        public static void Apply() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != BreathDemoBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open and save BreathDemo.unity outside Play mode first.");
            ApplyToOpenDemo();
            EditorSceneManager.SaveScene(scene);
        }

        public static void ApplyToOpenDemo() {
            var manifest = JsonUtility.FromJson<BreathArtSetup.Manifest>(File.ReadAllText(Folder + "PaperManifest.json"));
            var paperInfo = manifest.clips.Single(c => c.sourceFile == "star.gif");
            var guideInfo = manifest.clips.Single(c => c.sourceFile == "craneFlat (1).gif");
            var paperClip = BreathArtSetup.CreateSpriteClip(paperInfo, Folder);
            var guideClip = BreathArtSetup.CreateSpriteClip(guideInfo, Folder);
            var shader = Shader.Find("WingSinger/URP Glow Sprite");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("URP paper shader unavailable.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PaperSprites.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "PaperSprites.mat"); }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Gameplay")) AssetDatabase.CreateFolder("Assets/Prefabs", "Gameplay");
            var paperAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var paperRoot = paperAsset == null ? new GameObject("PaperCollectible") : PrefabUtility.LoadPrefabContents(PrefabPath);
            try {
                var paper = paperRoot.GetComponent<PaperCollectible>();
                if (paper == null) paper = paperRoot.AddComponent<PaperCollectible>();
                paper.visual = EnsureVisual(paperRoot.transform, "Visual", material, paperClip, 0.36f, 8);
                paper.clip = paperClip;
                PrefabUtility.SaveAsPrefabAsset(paperRoot, PrefabPath);
            } finally {
                if (paperAsset == null) UnityEngine.Object.DestroyImmediate(paperRoot);
                else PrefabUtility.UnloadPrefabContents(paperRoot);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<PaperCollectible>();
            foreach (string path in new[] { "Assets/Prefabs/Levels/Level_001_FirstFlight.prefab", "Assets/Prefabs/Levels/Level_002_SlowBreath.prefab" }) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var level = root.GetComponent<BreathLevel>();
                    foreach (Transform child in level.guide.Cast<Transform>().ToArray()) {
                        if (new[] { "LightAura", "LightCore", "Orbit" }.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                    level.guideClip = guideClip;
                    level.guideVisual = EnsureVisual(level.guide, "Paper Guide", material, guideClip, 0.5f, 15);
                    level.guideVisual.color = new Color(1.3f, 1.3f, 1.3f);
                    level.collectiblePrefab = prefab;
                    level.routeCore.enabled = level.routeGlow.enabled = level.settings.showGuideLine;
                    EditorUtility.SetDirty(level.settings); // Persist renamed per-object score with its original value.
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static SpriteRenderer EnsureVisual(Transform parent, string name, Material material, CharacterSpriteClip clip, float scale, int order) {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(SpriteRenderer)).transform; child.SetParent(parent, false); }
            child.localPosition = Vector3.zero; child.localScale = Vector3.one * scale;
            var renderer = child.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = material; renderer.sprite = clip.frames[0]; renderer.sortingOrder = order;
            return renderer;
        }
    }
}
