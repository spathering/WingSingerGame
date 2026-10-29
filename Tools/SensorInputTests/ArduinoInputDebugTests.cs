using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using WingSinger;

// Exercise the production component with the real receiver and a simulated port.
// The minimal Unity API shim below does not replace Editor/Play Mode validation.
static class ArduinoInputDebugTests {
    public static int Run() {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        void Until(Func<bool> done, string message) {
            double until = BreathSensorClient.Now + 3;
            while (!done() && BreathSensorClient.Now < until) Thread.Sleep(5);
            Check(done(), message);
        }
        var update = typeof(ArduinoBreathInput).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        var port = new FakePort { AutoIdentify = true, InitialSample = "400\n" };
        var client = new BreathSensorClient(new BreathSensorConnectionOptions("COM99", 1, 0.1, 2),
            () => new[] { "COM99" }, (_, _) => port);
        var sensor = new ArduinoBreathInput { connectionKind = BreathSensorConnectionKind.UsbSerial, sampleTimeoutSeconds = 1, inactivityTimeoutSeconds = 2 };
        UnityEngine.Debug.Messages.Clear();
        try {
            sensor.Connect(client);
            Until(() => sensor.ReceiverSnapshot.HasSample, "Component receives its first debug sample");
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == 0, "Debug logging defaults to off");
            sensor.debugLogging = true;
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == 1 && UnityEngine.Debug.Messages[0].Contains("数值=400 mV")
                && UnityEngine.Debug.Messages[0].Contains("吸气 / 上轨"), "Enabling debug logs the received value and inhale state");
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == 1, "Repeated Unity frames do not repeat an old sample");
            long sequence = sensor.ReceiverSnapshot.SampleSequence;
            port.Feed("400\n");
            Until(() => sensor.ReceiverSnapshot.SampleSequence > sequence, "A repeated numeric value arrives as a new sample");
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == 2, "Equal consecutive values are still logged when newly received");
            sensor.inhaleMaximum = 300;
            sensor.exhaleMinimum = 900;
            update.Invoke(sensor, null);
            Check(sensor.ReadLane() == BreathLane.Middle && UnityEngine.Debug.Messages[^1].Contains("静息 / 中轨"),
                "Editing thresholds immediately updates both gameplay and debug state");
            sensor.exhaleMinimum = 350;
            update.Invoke(sensor, null);
            Check(sensor.ReadLane() == BreathLane.Lower && UnityEngine.Debug.Messages[^1].Contains("呼气 / 下轨"),
                "Custom exhale threshold and logged state agree");
            sensor.inhaleMaximum = 400;
            update.Invoke(sensor, null);
            Check(sensor.ReadLane() == BreathLane.Middle && sensor.ReceiverSnapshot.Connected
                && UnityEngine.Debug.Messages[^1].Contains("阈值无效"), "Overlapping thresholds stay neutral without losing the connection");
            sensor.debugLogging = false;
            int logged = UnityEngine.Debug.Messages.Count;
            sensor.inhaleMaximum = 300;
            sensor.exhaleMinimum = 900;
            sequence = sensor.ReceiverSnapshot.SampleSequence;
            port.Feed("1000\n");
            Until(() => sensor.ReceiverSnapshot.SampleSequence > sequence, "Exhale data arrives with debug disabled");
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == logged && sensor.ReadLane() == BreathLane.Lower,
                "Disabling debug stops logs without affecting input");
            sensor.debugLogging = true;
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == logged + 1 && UnityEngine.Debug.Messages[^1].Contains("数值=1000 mV"),
                "Re-enabling debug reports the current sample");
            sensor.sampleTimeoutSeconds = 0.01f;
            Thread.Sleep(30);
            update.Invoke(sensor, null);
            Check(sensor.ReadLane() == BreathLane.Middle && UnityEngine.Debug.Messages[^1].Contains("等待有效数据")
                && !UnityEngine.Debug.Messages[^1].Contains("数值="), "Stale input logs its state without repeating an old value");
            logged = UnityEngine.Debug.Messages.Count;
            update.Invoke(sensor, null);
            Check(UnityEngine.Debug.Messages.Count == logged, "Stale state does not spam the Console every frame");
            sensor.Disconnect();
            Check(!client.IsRunning && port.Disposed, "Debug validation releases its simulated receiver");
        } finally { sensor.Disconnect(); }
        return checks;
    }
}

namespace UnityEngine {
    public class Object { }
    public class MonoBehaviour : Object { }
    public class ScriptableObject : Object { }
    public sealed class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public static class JsonUtility {
        static readonly System.Text.Json.JsonSerializerOptions Options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public static T FromJson<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text, Options);
        public static string ToJson(object value, bool pretty = false) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), Options);
    }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string _) { } }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string _) { } }
    public sealed class InspectorNameAttribute : Attribute { public InspectorNameAttribute(string _) { } }
    public sealed class MinAttribute : Attribute { public MinAttribute(float _) { } }
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float _, float __) { } }
    public sealed class SerializeField : Attribute { }
    public static class Application { public static bool isPlaying = true; public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WingSingerSensorTests"); }
    public enum KeyCode { W, S }
    public static class Input { public static bool GetKey(KeyCode _) => false; }
    public static class Debug {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(object message, Object context) => Messages.Add(message.ToString());
        public static void LogWarning(object message, Object context) => Messages.Add(message.ToString());
        public static void LogError(object message, Object context) => throw new Exception(message.ToString());
    }
}
