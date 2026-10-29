using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WingSinger.Editor {
    public static class ArduinoInputSetup {
        [MenuItem("WingSinger/Connect Arduino Input")]
        [MenuItem("WingSinger/Connect Bluetooth Input")]
        public static void Apply() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != BreathDemoBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved BreathDemo scene outside Play mode first.");
            ApplyToOpenDemo();
            EditorSceneManager.SaveScene(scene);
        }

        public static void ApplyToOpenDemo() {
            var manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
            if (manager == null) throw new InvalidOperationException("BreathLevelManager is missing.");
            var sensor = manager.GetComponent<ArduinoBreathInput>();
            if (sensor == null) sensor = manager.gameObject.AddComponent<ArduinoBreathInput>();
            sensor.settings = AssetDatabase.LoadAssetAtPath<BreathSensorSettings>(HardwareSettingsSetup.SettingsPath);
            if (sensor.settings == null) throw new InvalidOperationException("Hardware settings asset is missing.");
            sensor.keyboardFallback = manager.GetComponent<KeyboardBreathInput>();
            manager.input = sensor;
            EditorUtility.SetDirty(sensor); EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("WingSinger BLE CSV: ESP32_C3_Mini, Notify only, no handshake; filtered ADC uses center +/- deviation.");
        }
    }
}
