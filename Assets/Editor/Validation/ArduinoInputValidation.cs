using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class ArduinoInputValidation {
        const string Key = "WingSingerArduinoValidation";
        const string Report = "Logs/ArduinoInputValidation.txt";
        static BreathLevelManager manager;
        static ArduinoBreathInput sensor;
        static BreathSensorClient client;
        static SimulatedPort port;
        static int stage, openCount, lastBaud;
        static double started;

        static ArduinoInputValidation() {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) {
                    SessionState.SetBool(Key + "Background", Application.runInBackground);
                    Application.runInBackground = true;
                    manager = null; stage = 0; started = EditorApplication.timeSinceStartup;
                }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key, false)) {
                    Application.runInBackground = SessionState.GetBool(Key + "Background", false);
                    SessionState.SetBool(Key, false);
                }
            };
        }

        [MenuItem("WingSinger/Validate Arduino Input")]
        public static void Begin() {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != BreathDemoBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved BreathDemo scene outside Play mode first.");
            if (!(UnityEngine.Object.FindObjectOfType<BreathLevelManager>().input is ArduinoBreathInput))
                throw new InvalidOperationException("Apply WingSinger/Connect Arduino Input first.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, "Arduino input Play Mode verification (simulated serial transport, real scene)\n");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void Check(bool ok, string message) {
            if (!ok) throw new Exception(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }

        static BreathSensorClient SimulatedClient(string name, SimulatedPort transport) {
            return new BreathSensorClient(new BreathSensorConnectionOptions(inactivityTimeout: 3), () => new[] { name }, (p, baud) => {
                Interlocked.Increment(ref openCount); lastBaud = baud; return transport;
            });
        }

        static bool AtLane(BreathLane lane) => Mathf.Abs(manager.player.body.localPosition.y - (int)lane * manager.CurrentLevel.settings.laneSpacing) < 0.001f;
        static void NextStage(int next) { stage = next; started = EditorApplication.timeSinceStartup; }

        static void Tick() {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                if (manager == null) manager = UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                if (manager == null || manager.Session == null) return;
                if (EditorApplication.timeSinceStartup - started > 12) throw new Exception("Arduino input validation timed out in stage " + stage);
                switch (stage) {
                    case 0:
                        sensor = manager.input as ArduinoBreathInput;
                        Check(sensor != null && sensor.keyboardFallback != null && sensor.keyboardWhenDisconnected,
                            "Saved scene uses sensor input with the existing W/S fallback");
                        Check(sensor.inhaleMaximum == 400 && sensor.exhaleMinimum == 1000 && sensor.portOverride == "",
                            "Saved thresholds and automatic port selection are correct");
                        sensor.keyboardWhenDisconnected = false;
                        openCount = 0; port = new SimulatedPort { RepeatingValue = "400" };
                        client = SimulatedClient("COM99", port); sensor.Connect(client);
                        manager.Restart(); manager.Session.Advance(manager.Session.Pattern.Lead + 0.1, BreathLane.Middle);
                        NextStage(1); break;
                    case 1:
                        if (!sensor.ReceiverSnapshot.IsFresh(sensor.sampleTimeoutSeconds) || !AtLane(BreathLane.Upper)) return;
                        Check(lastBaud == 115200 && port.LastHello == BreathSensorProtocol.Hello, "Receiver uses the agreed baud and handshake");
                        Check(sensor.ReceiverSnapshot.Port == "COM99" && sensor.ReadLane() == BreathLane.Upper,
                            "Value 400 maps to inhale through the actual receiver");
                        Check(AtLane(BreathLane.Upper), "Sensor inhale moves the actual player to the upper lane");
                        port.RepeatingValue = "1000"; NextStage(2); break;
                    case 2:
                        if (sensor.ReceiverSnapshot.Value != 1000 || !AtLane(BreathLane.Lower)) return;
                        Check(sensor.ReadLane() == BreathLane.Lower, "Value 1000 maps to exhale, including the boundary");
                        Check(AtLane(BreathLane.Lower), "Sensor exhale moves the actual player to the lower lane");
                        port.RepeatingValue = "700"; NextStage(3); break;
                    case 3:
                        if (sensor.ReceiverSnapshot.Value != 700 || !AtLane(BreathLane.Middle)) return;
                        Check(sensor.ReadLane() == BreathLane.Middle && AtLane(BreathLane.Middle), "An intermediate value returns the player to the middle lane");
                        manager.Restart();
                        Check(ReferenceEquals(sensor.Receiver, client) && openCount == 1 && manager.Session.Score == 0,
                            "Restart resets gameplay while preserving the serial connection");
                        var old = manager.CurrentLevel; manager.Next();
                        Check(ReferenceEquals(sensor.Receiver, client) && openCount == 1 && !old.gameObject.activeSelf
                            && manager.levels.Count(l => l.gameObject.activeSelf) == 1,
                            "Prefab switching preserves one active level and the same receiver");
                        Check(!sensor.transform.IsChildOf(old.transform), "Receiver belongs to shared systems");
                        port.RepeatingValue = null; port.Feed("garbage\nNaN\nInfinity\n"); NextStage(4); break;
                    case 4:
                        if (EditorApplication.timeSinceStartup - started < sensor.sampleTimeoutSeconds + 0.15) return;
                        Check(sensor.ReceiverSnapshot.Connected && !sensor.ReceiverSnapshot.IsFresh(sensor.sampleTimeoutSeconds),
                            "Malformed packets cannot keep a stale signal alive");
                        Check(sensor.ReadLane() == BreathLane.Middle, "Stale sensor input is neutral");
                        port.FailRead = true; NextStage(5); break;
                    case 5:
                        if (!port.Disposed || sensor.ReceiverSnapshot.Connected) return;
                        Check(sensor.ReadLane() == BreathLane.Middle, "Simulated unplug clears input");
                        sensor.enabled = false;
                        Check(!client.IsRunning && port.Disposed && sensor.ReadLane() == BreathLane.Middle,
                            "Disabling input stops the worker and closes the transport");
                        sensor.enabled = true;
                        var second = new SimulatedPort { RepeatingValue = "1200" };
                        var replacement = SimulatedClient("COM100", second); sensor.Connect(replacement);
                        Check(!client.IsRunning, "Re-enabling cannot reactivate the old worker");
                        client = replacement; port = second;
                        manager.Restart(); manager.Session.Advance(manager.Session.Pattern.Lead + 0.1, BreathLane.Middle);
                        NextStage(6); break;
                    case 6:
                        if (!sensor.ReceiverSnapshot.IsFresh(sensor.sampleTimeoutSeconds) || !AtLane(BreathLane.Lower)) return;
                        Check(sensor.ReceiverSnapshot.Port == "COM100" && sensor.ReadLane() == BreathLane.Lower,
                            "A replacement receiver can use a changed COM number");
                        sensor.Disconnect();
                        Check(!client.IsRunning && port.Disposed && sensor.ReadLane() == BreathLane.Middle,
                            "Shutdown releases the replacement transport and resets the lane");
                        Check(UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length == 0, "No gameplay UI was added");
                        File.AppendAllText(Report, "SUCCESS: Physical Arduino is not connected; hardware end-to-end validation is pending.\n");
                        Finish(); break;
                }
            } catch (Exception error) {
                File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); Finish();
            }
        }

        static void Finish() {
            if (sensor != null) sensor.Disconnect();
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            SessionState.SetBool(Key, false); EditorApplication.isPlaying = false;
        }

        sealed class SimulatedPort : IBreathSerialTransport {
            readonly ConcurrentQueue<byte> bytes = new ConcurrentQueue<byte>();
            public volatile string RepeatingValue;
            public volatile bool Disposed, FailRead;
            public string LastHello;
            public void Feed(string text) { foreach (byte b in Encoding.ASCII.GetBytes(text)) bytes.Enqueue(b); }
            public void WriteLine(string line) { LastHello = line; if (line == BreathSensorProtocol.Hello) Feed(BreathSensorProtocol.Identity + "\r\n"); }
            public int Read(byte[] buffer) {
                if (Disposed || FailRead) throw new IOException("Simulated sensor disconnect");
                if (bytes.IsEmpty) { Thread.Sleep(10); string value = RepeatingValue; if (value != null) Feed(value + "\r\n"); }
                int count = 0;
                while (count < buffer.Length && bytes.TryDequeue(out byte b)) buffer[count++] = b;
                return count;
            }
            public void CancelRead() { }
            public void Dispose() { Disposed = true; }
        }
    }
}
