using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WingSinger.UI;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class BluetoothInputValidation {
        const string Key = "WingSinger.BluetoothValidation";
        const string Report = "Docs/Validation/BluetoothInputValidation.json";
        static IEnumerator routine;
        static BreathUIFlowController flow;
        static readonly List<string> checks = new List<string>();
        [Serializable] sealed class Result {
            public bool success;
            public string date, error;
            public string[] checks;
            public string scope = "Unity Mono native DLL scan/cancel plus simulated BLE UART in the real scene; no physical BLE or ESP32 target build.";
        }
        static BluetoothInputValidation() { EditorApplication.update += Tick; }
        [MenuItem("WingSinger/Validate Bluetooth Input")]
        public static void Begin() {
            if (!WindowsBleTransport.IsSupported) throw new PlatformNotSupportedException("Validate BLE on Windows x64.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the scene first.");
            EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
            var sensor = UnityEngine.Object.FindObjectOfType<ArduinoBreathInput>();
            if (sensor == null || sensor.connectionKind != BreathSensorConnectionKind.BluetoothLE)
                throw new InvalidOperationException("BreathDemo must use the BLE input connection.");
            WindowsBleTransport.ValidateProfile(sensor.bluetoothServiceUuid, sensor.bluetoothNotifyUuid, sensor.bluetoothWriteUuid);
            WindowsBleTransport.ParseAddress(sensor.bluetoothAddress);
            Directory.CreateDirectory("Logs");
            var watch = Stopwatch.StartNew();
            using (var native = WindowsBleTransport.Open("WingSinger Native Lifecycle Test - no match", "", sensor.bluetoothServiceUuid,
                sensor.bluetoothNotifyUuid, sensor.bluetoothWriteUuid)) {
                native.WriteLine(BreathSensorProtocol.Hello);
                Thread.Sleep(100);
                try {
                    if (native.Read(new byte[256]) != 0) throw new InvalidOperationException("A non-matching scan produced data.");
                } catch (IOException error) { UnityEngine.Debug.Log("BLE radio diagnostic (no matching sensor requested): " + error.Message); }
                native.CancelRead();
            }
            if (watch.Elapsed.TotalSeconds > 2) throw new InvalidOperationException("BLE cancellation was unexpectedly slow.");
            File.WriteAllText("Logs/BluetoothNativeValidation.txt", "PASS: Unity Mono loads Windows x64 BLE DLL, starts/cancels discovery and disposes it.\n"
                + "Physical BLE notification delivery and ESP32 firmware build/upload remain unverified.\n");
            var sceneFlow = UnityEngine.Object.FindObjectOfType<BreathUIFlowController>();
            sceneFlow.profileKey = ""; sensor.enabled = false; // Unsaved test overrides; never change the user's save.
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + "Stage", 0);
            SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            EditorApplication.isPlaying = true;
        }

        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetInt(Key + "Stage", 0) == 1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key, false);
                EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1));
                return;
            }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 60) { Finish(false, "BLE play verification timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "Stage", 0) != 0) return;
            try {
                if (routine == null) { checks.Clear(); routine = Run(); }
                if (!routine.MoveNext()) Finish(true, null);
            } catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks.Add(description); }
        static IEnumerator Run() {
            Application.runInBackground = true;
            for (int i = 0; i < 5; i++) yield return null;
            flow = UnityEngine.Object.FindObjectOfType<BreathUIFlowController>();
            var sensor = flow.sensorInput; var manager = flow.levelManager;
            Check(flow.Progress != null && flow.profileKey == "" && manager.Session == null, "Real scene starts at home with isolated progress and no active session");
            Check(sensor.connectionKind == BreathSensorConnectionKind.BluetoothLE && manager.input == sensor, "Saved scene uses shared BLE input");
            sensor.sampleTimeoutSeconds = .1f; sensor.keyboardWhenDisconnected = false;
            var port = new SimulatedBle();
            var replacement = new SimulatedBle();
            int opens = 0;
            var client = new BreathSensorClient(new BreathSensorConnectionOptions("", 1, .05, .4),
                () => new[] { "BLE: simulated" }, (_, __) => Interlocked.Increment(ref opens) == 1 ? port : replacement);
            sensor.enabled = true; sensor.Connect(client);
            double until = EditorApplication.timeSinceStartup + 3;
            while (!flow.SensorFresh && EditorApplication.timeSinceStartup < until) yield return null;
            Check(flow.SensorFresh && sensor.ReadLane() == BreathLane.Upper, "Fragmented BLE identity and 400 sample control the upper lane");
            Check(flow.Text(UIValueKind.Connection) == "BLE connected" && flow.Text(UIValueKind.Signal) == "BLE"
                && flow.Text(UIValueKind.Battery) == "--", "UI shows real BLE transport without fabricated battery telemetry");
            port.Value = 1000; until = EditorApplication.timeSinceStartup + 2;
            while (sensor.ReadLane() != BreathLane.Lower && EditorApplication.timeSinceStartup < until) yield return null;
            Check(sensor.ReadLane() == BreathLane.Lower, "1000 BLE sample controls lower lane");
            port.Value = 700; until = EditorApplication.timeSinceStartup + 2;
            while (sensor.ReadLane() != BreathLane.Middle && EditorApplication.timeSinceStartup < until) yield return null;
            Check(flow.SensorFresh && sensor.ReadLane() == BreathLane.Middle, "700 BLE sample controls middle lane");
            int reconnects = 0; flow.ReconnectOverride = () => reconnects++;
            flow.router.Route(UIRouteAction.CheckConnection);
            Check(reconnects == 1, "Existing controller check requests exactly one reconnection");
            flow.ReconnectOverride = null;
            int[] ids = manager.levels.Select(level => level.GetInstanceID()).ToArray();
            flow.router.Route(UIRouteAction.SelectLevel, level: 1); flow.router.Route(UIRouteAction.StartFlight);
            Check(manager.Session != null && manager.levels.Count(level => level.gameObject.activeSelf) == 1, "BLE-ready takeoff activates exactly one level");
            flow.router.Route(UIRouteAction.Pause);
            var session = manager.Session; double elapsed = session.Elapsed; long sequence = sensor.ReceiverSnapshot.SampleSequence;
            until = EditorApplication.timeSinceStartup + .2;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(Time.timeScale == 0 && session.Elapsed == elapsed && sensor.ReceiverSnapshot.SampleSequence > sequence,
                "Pause freezes gameplay while BLE notifications continue");
            flow.router.Route(UIRouteAction.Resume); manager.SwitchLevel(1); manager.Restart();
            Check(manager.CurrentIndex == 1 && manager.levels.Count(level => level.gameObject.activeSelf) == 1 && sensor.Receiver == client
                && manager.levels.Select(level => level.GetInstanceID()).SequenceEqual(ids), "Switch and restart reuse level instances and the same BLE receiver");
            flow.ExitSession();
            Check(manager.levels.All(level => !level.gameObject.activeSelf) && sensor.Receiver == client,
                "Leaving gameplay disables level roots while shared BLE input remains available");
            port.Value = 400; until = EditorApplication.timeSinceStartup + 2;
            while (sensor.ReadLane() != BreathLane.Upper && EditorApplication.timeSinceStartup < until) yield return null;
            port.Withhold = true; until = EditorApplication.timeSinceStartup + .3;
            while (sensor.ReceiverSnapshot.IsFresh(.1) && EditorApplication.timeSinceStartup < until) yield return null;
            Check(sensor.ReceiverSnapshot.Connected && sensor.ReadLane() == BreathLane.Middle, "Stale BLE sample returns to middle before disconnect");
            until = EditorApplication.timeSinceStartup + 3;
            while (!(opens >= 2 && flow.SensorFresh) && EditorApplication.timeSinceStartup < until) yield return null;
            Check(opens >= 2 && port.Disposed && flow.SensorFresh, "Inactivity closes old transport and re-identifies replacement");
            sensor.enabled = false;
            Check(sensor.Receiver == null && !client.IsRunning && replacement.Disposed, "Component disable stops receiver and releases notifications");
            Check(UnityEngine.SceneManagement.SceneManager.sceneCount == 1, "BLE gameplay uses one scene throughout");
        }
        static void Finish(bool success, string error) {
            if (flow != null) { flow.ExitSession(); flow.sensorInput.Disconnect(); }
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllText(Report, JsonUtility.ToJson(new Result { success = success, date = DateTimeOffset.Now.ToString("O"),
                error = error, checks = checks.ToArray() }, true));
            if (success) UnityEngine.Debug.Log("WingSinger BLE Play validation passed: " + checks.Count);
            else UnityEngine.Debug.LogError(error);
            routine = null; SessionState.SetInt(Key + "Stage", 1); SessionState.SetInt(Key + "Exit", success ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        sealed class SimulatedBle : IBreathSerialTransport {
            readonly ConcurrentQueue<byte> bytes = new ConcurrentQueue<byte>();
            public volatile int Value = 400;
            public volatile bool Withhold, Disposed;
            bool identified;
            void Feed(string text) { foreach (byte value in Encoding.ASCII.GetBytes(text)) bytes.Enqueue(value); }
            public void WriteLine(string line) { if (line == BreathSensorProtocol.Hello) { Feed(BreathSensorProtocol.Identity + "\n"); identified = true; } }
            public int Read(byte[] buffer) {
                if (Disposed) throw new IOException("BLE disconnected");
                if (bytes.IsEmpty) { Thread.Sleep(10); if (!Withhold && identified) Feed(Value + "\n"); }
                int count = 0;
                while (count < Math.Min(7, buffer.Length) && bytes.TryDequeue(out byte value)) buffer[count++] = value;
                return count;
            }
            public void CancelRead() { Withhold = true; }
            public void Dispose() { Disposed = true; }
        }
    }
}
