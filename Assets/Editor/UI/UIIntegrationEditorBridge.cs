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
            var compiledAt = File.GetLastWriteTimeUtc(typeof(UIIntegrationEditorBridge).Assembly.Location);
            if (Directory.EnumerateFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles("Assets/Editor", "*.cs", SearchOption.AllDirectories))
                .Any(path => File.GetLastWriteTimeUtc(path) > compiledAt)) return;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            var result = new State { action = action, playing = EditorApplication.isPlayingOrWillChangePlaymode,
                compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
                scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).path).ToArray(),
                dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray() };
            try {
                switch (action) {
                    case "state": break;
                    case "setup": UIIntegrationSetup.Apply(); break;
                    case "validate": GameplayNavigationValidation.Begin(); break;
                    case "validate-preview": ReferenceUIPlayValidation.Begin(); break;
                    case "setup-viewport": UIViewportSetup.Apply(); break;
                    case "validate-viewport": UIViewportValidation.Begin(); break;
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
