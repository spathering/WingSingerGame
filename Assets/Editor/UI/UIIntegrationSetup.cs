using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    public static class UIIntegrationSetup {
        const string DirectoryPath = "Assets/Settings/Levels";
        public const string CatalogPath = DirectoryPath + "/WingSingerLevelCatalog.asset";
        [MenuItem("WingSinger/UI/Apply Button Effects and Level Catalog")]
        public static void Apply() {
            RequireSavedScenes();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayNavigationSetup.ScenePath) scene = EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var flow = Object.FindObjectOfType<BreathUIFlowController>();
            if (flow == null) throw new InvalidOperationException("Connect gameplay navigation before applying the level catalog.");
            Directory.CreateDirectory("Temp/NavigationBackup");
            string backup = "Temp/NavigationBackup/BreathDemo.before-level-catalog.unity";
            if (!File.Exists(backup)) File.Copy(GameplayNavigationSetup.ScenePath, backup);
            InstallInteractions();
            var first = Definition("Level_001_FirstFlight", "first-flight", "First Flight", "Find your rhythm: inhale 3 s, exhale 4 s. A 45-second forest flight.");
            var second = Definition("Level_002_SlowBreath", "slow-breath", "Slow Breath", "A slower rhythm: inhale 4 s, exhale 6 s. A 60-second forest flight.");
            var catalog = AssetDatabase.LoadAssetAtPath<BreathLevelCatalog>(CatalogPath);
            if (catalog == null) {
                catalog = ScriptableObject.CreateInstance<BreathLevelCatalog>(); catalog.levels = new[] { first, second };
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            BindCatalog(flow, catalog);
            // Preserve the existing UI and its scene overrides; add only missing bindings/effects.
            foreach (var button in flow.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<UIInteractionTween>() == null) button.gameObject.AddComponent<UIInteractionTween>();
            var detail = flow.router.pages.Single(p => p.screenId == UIScreenId.LevelSelect).transform.Find("Content/LevelsPanel/SelectedLevel/Description");
            var label = detail.GetComponent<UIRuntimeLabel>() ?? detail.gameObject.AddComponent<UIRuntimeLabel>();
            label.value = UIValueKind.LevelDescription;
            var text = detail.GetComponent<TMP_Text>(); text.enableAutoSizing = true; text.fontSizeMax = text.fontSize; text.fontSizeMin = 18;
            var readyTitle = flow.router.pages.Single(p => p.screenId == UIScreenId.ReadyForTakeoff).transform.Find("Content/ReadyPanel/LevelTitle/Label");
            var titleLabel = readyTitle.GetComponent<UIRuntimeLabel>() ?? readyTitle.gameObject.AddComponent<UIRuntimeLabel>();
            titleLabel.value = UIValueKind.LevelTitle;
            var titleText = readyTitle.GetComponent<TMP_Text>(); titleText.enableAutoSizing = true; titleText.fontSizeMax = titleText.fontSize; titleText.fontSizeMin = 18;
            foreach (var item in new Object[] { flow.levelManager, label, text, titleLabel, titleText }) {
                EditorUtility.SetDirty(item); if (item is Component component) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WingSinger: PrimeTween interactions installed; existing two gameplay Prefabs registered by level definition SOs.");
        }
        public static void RequireSavedScenes() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before updating UI assets.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Open scene has unsaved changes. Save it before applying the UI integration.");
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.isDirty) throw new InvalidOperationException("Save the open Prefab stage before applying UI integration.");
        }
        public static void BindCatalog(BreathUIFlowController flow, BreathLevelCatalog catalog) {
            catalog.Validate();
            var manager = flow.levelManager;
            var all = manager.levels;
            var ordered = new BreathLevel[catalog.Count];
            for (int i = 0; i < ordered.Length; i++) {
                var definition = catalog.At(i);
                ordered[i] = all.SingleOrDefault(level => level != null &&
                    PrefabUtility.GetCorrespondingObjectFromSource(level) == definition.prefab);
                if (ordered[i] == null) throw new InvalidOperationException("The main scene has no registered instance of " + definition.prefab.name);
                if (ordered[i].settings != definition.settings) throw new InvalidOperationException("Scene level settings differ from definition: " + definition.levelId);
            }
            manager.catalog = catalog; manager.levels = ordered; manager.ValidateCatalog();
            EditorUtility.SetDirty(manager);
        }
        static BreathLevelDefinition Definition(string fileName, string id, string title, string description) {
            string path = DirectoryPath + "/" + fileName + "_Definition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BreathLevelDefinition>(path);
            if (definition != null) return definition;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/" + fileName + ".prefab").GetComponent<BreathLevel>();
            definition = ScriptableObject.CreateInstance<BreathLevelDefinition>();
            definition.levelId = id; definition.title = title; definition.description = description;
            definition.prefab = prefab; definition.settings = prefab.settings; definition.availableFromStart = true;
            definition.sourceScenePath = GameplayNavigationSetup.ScenePath;
            definition.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Backgrounds/ForestValley.png");
            definition.Validate(); AssetDatabase.CreateAsset(definition, path); return definition;
        }
        public static void InstallInteractions() {
            string[] folders = { "Assets/Prefabs/UI/Shared", "Assets/Prefabs/UI/Screens" };
            foreach (string folder in folders)
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
                    InstallPrefab(AssetDatabase.GUIDToAssetPath(guid));
            InstallPrefab("Assets/Prefabs/UI/UIFlowRoot.prefab");
        }
        static void InstallPrefab(string path) {
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                bool changed = false;
                foreach (var button in root.GetComponentsInChildren<Button>(true)) {
                    if (button.GetComponent<UIInteractionTween>() != null) continue;
                    button.gameObject.AddComponent<UIInteractionTween>(); changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
