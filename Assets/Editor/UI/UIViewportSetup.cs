using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    public static class UIViewportSetup {
        [MenuItem("WingSinger/UI/Apply Responsive Viewports")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            var original = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Temp/NavigationBackup");
            foreach (string path in new[] { GameplayNavigationSetup.ScenePath, "Assets/Scenes/UI/UIFlowPreview.unity" }) {
                string backup = "Temp/NavigationBackup/" + Path.GetFileNameWithoutExtension(path) + ".before-viewport.unity";
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI/Screens" })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { ConfigurePage(root.GetComponent<UIPageView>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            const string flowPath = "Assets/Prefabs/UI/UIFlowRoot.prefab";
            var flow = PrefabUtility.LoadPrefabContents(flowPath);
            try {
                ConfigureCanvas(flow.GetComponent<Canvas>());
                foreach (var page in flow.GetComponentsInChildren<UIPageView>(true)) ConfigurePage(page);
                PrefabUtility.SaveAsPrefabAsset(flow, flowPath);
            } finally { PrefabUtility.UnloadPrefabContents(flow); }
            foreach (string path in new[] { GameplayNavigationSetup.ScenePath, "Assets/Scenes/UI/UIFlowPreview.unity" }) {
                var scene = EditorSceneManager.OpenScene(path);
                var router = Object.FindObjectOfType<UIScreenRouter>();
                if (router == null) throw new InvalidOperationException("Missing UI router: " + path);
                ConfigureCanvas(router.GetComponent<Canvas>());
                foreach (var page in router.pages) ConfigurePage(page);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? GameplayNavigationSetup.ScenePath : original);
            Debug.Log("WingSinger: responsive 1920x1080 Canvas and aspect-preserving page layouts applied to all registered pages.");
        }
        public static void ConfigureCanvas(Canvas canvas) {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            EditorUtility.SetDirty(scaler); PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
        }
        public static void ConfigurePage(UIPageView page) {
            var layout = page.GetComponent<UIPageViewport>() ?? page.gameObject.AddComponent<UIPageViewport>();
            layout.designSize = new Vector2(2420, 1668); layout.background = page.transform.Find("Background") as RectTransform;
            layout.respectSafeArea = true;
            EditorUtility.SetDirty(layout); PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
            layout.Fit();
        }
    }
}
