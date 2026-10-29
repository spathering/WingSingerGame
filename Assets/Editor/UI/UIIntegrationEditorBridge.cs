using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WingSinger.Editor {
    // Small, local request queue for reusing the open editor without a second Unity process.
    [InitializeOnLoad]
    public static class UIIntegrationEditorBridge {
        const string Request = "Temp/WingSingerUIIntegration.request";
        const string Response = "Temp/WingSingerUIIntegration.response.json";
        [Serializable] sealed class State {
            public bool success, playing, compiling, updating;
            public string action, error;
            public string[] scenes;
            public bool[] dirty;
        }
        static UIIntegrationEditorBridge() { EditorApplication.update += Poll; }
        static void Poll() {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(Request).Trim();
            var compiledAt = File.GetLastWriteTimeUtc(typeof(UIIntegrationEditorBridge).Assembly.Location);
            var pendingScripts = Directory.EnumerateFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles("Assets/Editor", "*.cs", SearchOption.AllDirectories))
                .Where(path => File.GetLastWriteTimeUtc(path) > compiledAt).ToArray();
            if (pendingScripts.Length > 0 && action != "import-pending") return;
            File.Delete(Request);
            var result = new State { action = action, playing = EditorApplication.isPlayingOrWillChangePlaymode,
                compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
                scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray(),
                dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray() };
            try {
                switch (action) {
                    case "state": break;
                    case "setup-hardware-ui": HardwareSettingsSetup.Apply(); break;
                    case "import-bluetooth-plugin":
                        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before importing the BLE plugin.");
                        AssetDatabase.ImportAsset("Assets/Plugins/WingSinger/Bluetooth/x86_64/WingSingerBle.dll");
                        break;
                    case "import-pending":
                        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before importing scripts.");
                        foreach (string path in pendingScripts) AssetDatabase.ImportAsset(path.Replace('\\', '/'));
                        break;
                    case "setup": UIIntegrationSetup.Apply(); break;
                    case "validate": GameplayNavigationValidation.Begin(); break;
                    case "validate-preview": ReferenceUIPlayValidation.Begin(); break;
                    case "setup-viewport": UIViewportSetup.Apply(); break;
                    case "validate-viewport": UIViewportValidation.Begin(); break;
                    case "validate-bluetooth": BluetoothInputValidation.Begin(); break;
                    case "validate-parallax": ParallaxBackgroundSetup.ValidateExisting(); break;
                    case "open-gameplay":
                        UIIntegrationSetup.RequireSavedScenes(); EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath); break;
                    default: throw new InvalidOperationException("Unsupported local UI integration request: " + action);
                }
                result.success = true;
            } catch (Exception e) { result.error = e.ToString(); Debug.LogException(e); }
            File.WriteAllText(Response, JsonUtility.ToJson(result, true));
        }
    }
}
