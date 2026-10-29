using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    public static class HardwareSettingsValidation {
        public static IEnumerator Run(BreathUIFlowController flow, Action<int> feed, Action<bool, string> check) {
            var sensor = flow.sensorInput; var router = flow.router; var manager = flow.levelManager;
            string defaults = JsonUtility.ToJson(sensor.settings);
            var receiver = sensor.Receiver;
            check(sensor.settings != null && router.pages.Length == 14, "Independent hardware SO and 14th page are installed");
            flow.router.Route(UIRouteAction.Navigate, UIScreenId.Settings);
            var entry = router.Page(UIScreenId.Settings).transform.Find("Content/SettingsPanel/SupportSection/Language").GetComponent<UIRouteButton>();
            entry.Trigger(); yield return null;
            var panel = router.Page(UIScreenId.HardwareSettings).GetComponent<HardwareSettingsPanel>();
            check(router.CurrentScreen == UIScreenId.HardwareSettings && panel.gameObject.activeInHierarchy, "Settings hardware entry opens editable in-game page");
            panel.center.text = "1250"; panel.deviation.text = "80"; panel.apply.onClick.Invoke();
            check(sensor.bluetoothCenterMv == 1250 && sensor.bluetoothDeviationMv == 80 && sensor.Receiver == receiver,
                "Applying mapping edits changes runtime parameters without restarting receiver");
            check(BreathSensorConfigFile.TryLoad(sensor.ConfigurationPath, out var saved, out _) && saved.bluetoothCenterMv == 1250,
                "In-game settings persist to an independent JSON file");
            var readbackRoot = new GameObject("Config readback"); readbackRoot.SetActive(false);
            try {
                var readback = readbackRoot.AddComponent<ArduinoBreathInput>(); readback.settings = sensor.settings;
                readback.ConfigurationFileOverride = sensor.ConfigurationPath;
                check(readback.Configuration.bluetoothCenterMv == 1250, "A fresh input component restores saved settings");
            } finally { Object.DestroyImmediate(readbackRoot); }
            string file = File.ReadAllText(sensor.ConfigurationPath);
            panel.deviation.text = "NaN"; panel.apply.onClick.Invoke();
            check(sensor.bluetoothDeviationMv == 80 && File.ReadAllText(sensor.ConfigurationPath) == file,
                "Invalid edits preserve active configuration and saved file");
            panel.restoreDefaults.onClick.Invoke();
            check(sensor.bluetoothCenterMv == sensor.settings.defaults.bluetoothCenterMv && JsonUtility.ToJson(sensor.settings) == defaults,
                "Restore defaults uses project SO without modifying its asset");
            int requests = 0; flow.ReconnectOverride = () => requests++;
            panel.reconnect.onClick.Invoke(); flow.ReconnectOverride = null;
            check(requests == 1, "Hardware reconnect button sends exactly one reconnect request");
            feed(1300); double until = UnityEditor.EditorApplication.timeSinceStartup + 2;
            while (sensor.DecisionLabel != "默认" && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            check(sensor.DecisionLabel == "默认" && panel.hardwareInfo.text.Contains("ESP32_C3_Mini"), "Live hardware page displays device information and neutral decision");
            panel.Present();
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080), new Vector2Int(640,360) }) {
                Capture(flow, "HardwareSettings_" + size.x + "x" + size.y, size.x, size.y, true, check);
                yield return null;
            }
            router.Route(UIRouteAction.Back); check(router.CurrentScreen == UIScreenId.Settings, "Hardware Back returns to Settings");
            router.Route(UIRouteAction.SelectLevel, level: 1); router.Route(UIRouteAction.StartFlight); yield return null;
            check(manager.Session != null && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "Configured hardware starts one level");
            feed(900); until = UnityEditor.EditorApplication.timeSinceStartup + 2;
            while (sensor.DecisionLabel != "吸气" && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            flow.Refresh();
            check(flow.Text(UIValueKind.SensorReading).Contains("ADC 900") && flow.Text(UIValueKind.SensorReading).Contains("吸气"), "Gameplay HUD displays raw ADC and inhale decision");
            var header = router.Page(UIScreenId.PracticeFollow).transform.Find("Header/PracticeHUD");
            var hud = (header.Find("ConnectionBadge/Label") ?? header.Find("Connection/Label")).GetComponent<TMP_Text>();
            check(hud.text.Contains("ADC 900") && hud.text.Contains("吸气") && !hud.text.Contains("connected"), "Actual rendered HUD binding shows ADC and decision, with no connection-label override");
            Capture(flow, "LiveHardwareInhale", 1280, 720, false, check);
            feed(1700); until = UnityEditor.EditorApplication.timeSinceStartup + 2;
            while (sensor.DecisionLabel != "呼气" && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            check(flow.Text(UIValueKind.SensorReading).Contains("呼气"), "Gameplay HUD displays exhale decision");
            var session = manager.Session;
            router.Route(UIRouteAction.Pause); router.Route(UIRouteAction.Navigate, UIScreenId.Settings); entry.Trigger();
            double elapsed = session.Elapsed; long sequence = sensor.ReceiverSnapshot.SampleSequence;
            until = UnityEditor.EditorApplication.timeSinceStartup + .2;
            while (UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            check(Time.timeScale == 0 && session.Elapsed == elapsed && sensor.ReceiverSnapshot.SampleSequence > sequence,
                "Hardware settings preserves paused session while BLE reception continues");
            router.Route(UIRouteAction.Back); router.Route(UIRouteAction.Back);
            check(router.CurrentScreen == UIScreenId.FlightPaused && manager.Session == session && manager.IsPaused,
                "Hardware-to-Settings-to-Pause navigation keeps the same paused session");
            router.Route(UIRouteAction.Resume); check(!manager.IsPaused && Time.timeScale == 1, "Resume after hardware settings restores gameplay time");
            flow.ExitSession();
            check(JsonUtility.ToJson(sensor.settings) == defaults, "Settings operations never mutate shared project defaults");
        }

        static void Capture(BreathUIFlowController flow, string name, int width, int height, bool validatePage, Action<bool,string> check) {
            const string directory = "Docs/Design/UI/RuntimePreviews"; Directory.CreateDirectory(directory);
            var canvas = flow.GetComponent<Canvas>(); var camera = Camera.main;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; var cameraTarget = camera.targetTexture;
            var mode = canvas.renderMode; var canvasCamera = canvas.worldCamera; float distance = canvas.planeDistance, aspect = camera.aspect;
            int order = canvas.sortingOrder; Texture2D image = null;
            var panoramas = Object.FindObjectsOfType<LoopingPanorama>();
            try {
                target.Create(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f; canvas.sortingOrder = 32000;
                camera.targetTexture = target; camera.aspect = (float)width / height;
                foreach (var panorama in panoramas) panorama.Sync();
                Canvas.ForceUpdateCanvases();
                foreach (var text in flow.router.Page(flow.router.CurrentScreen).GetComponentsInChildren<TMP_Text>()) {
                    text.ForceMeshUpdate();
                    if (text.text.Contains("吸气") || text.text.Contains("默认") || text.text.Contains("呼气"))
                        check(text.textInfo.characterInfo.Take(text.textInfo.characterCount).All(c => c.character != '\u25a1'), "Chinese hardware status renders without missing glyphs");
                }
                if (validatePage) foreach (var control in flow.router.Page(UIScreenId.HardwareSettings).GetComponentsInChildren<Selectable>()) {
                    var corners = new Vector3[4]; control.GetComponent<RectTransform>().GetWorldCorners(corners);
                    check(corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera,c)).All(p => p.x >= -1 && p.x <= width+1 && p.y >= -1 && p.y <= height+1),
                        "Hardware control fits " + width + "x" + height + ": " + control.name);
                }
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                File.WriteAllBytes(directory + "/" + name + ".png", image.EncodeToPNG());
            } finally {
                canvas.renderMode = mode; canvas.worldCamera = canvasCamera; canvas.planeDistance = distance; canvas.sortingOrder = order;
                camera.targetTexture = cameraTarget; camera.aspect = aspect; RenderTexture.active = previous;
                foreach (var panorama in panoramas) panorama.Sync();
                if (image != null) Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
