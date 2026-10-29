using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WingSinger;

static class BluetoothInputTests {
    public static int Run() {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        void Until(Func<bool> condition, string message) {
            double until = BreathSensorClient.Now + 3;
            while (!condition() && BreathSensorClient.Now < until) Thread.Sleep(5);
            Check(condition(), message);
        }
        Check(WindowsBleTransport.ParseAddress("") == 0, "Empty BLE address enables discovery");
        Check(WindowsBleTransport.ParseAddress("AA:BB:CC:DD:EE:FF") == 0xaabbccddeeffUL, "BLE MAC address filter parses correctly");
        Check(WindowsBleTransport.ParseAddress("aa-bb-cc-dd-ee-ff") == 0xaabbccddeeffUL, "Hyphenated lower-case BLE address accepted");
        foreach (string invalid in new[] { "COM7", "0", "GG:BB:CC:DD:EE:FF", "00:00:00:00:00:00" }) {
            bool rejected = false;
            try { WindowsBleTransport.ParseAddress(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid BLE address rejected before starting receiver");
        }
        WindowsBleTransport.ValidateProfile(WindowsBleTransport.ServiceUuid, WindowsBleTransport.NotifyUuid, WindowsBleTransport.WriteUuid);
        bool badProfile = false;
        try { WindowsBleTransport.ValidateProfile("bad", WindowsBleTransport.NotifyUuid, WindowsBleTransport.WriteUuid); }
        catch (ArgumentException) { badProfile = true; }
        Check(badProfile, "Invalid UUIDs rejected before invoking native DLL");
        var sensor = new ArduinoBreathInput { sampleTimeoutSeconds = .05f };
        Check(sensor.connectionKind == BreathSensorConnectionKind.BluetoothLE && sensor.ConnectionLabel == "BLE", "New input defaults to BLE");
        var port = new FakePort { AutoIdentify = true, InitialSample = "400\n" };
        var client = new BreathSensorClient(new BreathSensorConnectionOptions("", 1, .05, 2),
            () => new[] { "BLE: WingSinger Breath" }, (_, _) => port);
        try {
            sensor.Connect(client);
            Until(() => sensor.ReceiverSnapshot.HasSample, "BLE endpoint uses common handshake without COM validation");
            Check(sensor.ReadLane() == BreathLane.Upper, "BLE 400 maps to inhale/upper");
            port.Feed("1000\n"); Until(() => sensor.ReceiverSnapshot.Value == 1000, "BLE exhale sample received");
            Check(sensor.ReadLane() == BreathLane.Lower, "BLE 1000 maps to exhale/lower");
            port.Feed("700\n"); Until(() => sensor.ReceiverSnapshot.Value == 700, "BLE rest sample received");
            Check(sensor.ReadLane() == BreathLane.Middle, "BLE 700 maps to rest/middle");
            port.Feed("400\n"); Until(() => sensor.ReceiverSnapshot.Value == 400, "Final BLE sample received");
            Thread.Sleep(80);
            Check(sensor.ReadLane() == BreathLane.Middle && client.Snapshot().Connected, "Stale BLE connection returns to middle");
            sensor.connectionKind = BreathSensorConnectionKind.UsbSerial;
            Check(sensor.ConnectionLabel == "BLE", "UI describes active transport until explicitly reconnected");
        } finally { sensor.Disconnect(); }
        Check(!client.IsRunning && port.Disposed, "BLE receiver disable releases worker/transport");
        // Run the actual C++ DLL, not a mock, while no physical sensor is required.
        // A deliberately non-matching advertised name prevents attaching to real hardware.
        for (int i = 0; i < 3; i++) {
            var native = WindowsBleTransport.Open("WingSinger Native Lifecycle Test - no match", "",
                WindowsBleTransport.ServiceUuid, WindowsBleTransport.NotifyUuid, WindowsBleTransport.WriteUuid);
            native.WriteLine(BreathSensorProtocol.Hello);
            Thread.Sleep(100);
            try { Check(native.Read(new byte[256]) == 0, "Native scan does not invent sample/identity bytes"); }
            catch (IOException error) { Check(!string.IsNullOrEmpty(error.Message), "Unavailable adapter reports actionable error"); }
            var watch = Stopwatch.StartNew();
            native.CancelRead(); native.Dispose(); native.Dispose();
            Check(watch.Elapsed.TotalSeconds < 1, "Native scanning stops promptly and repeated Dispose is safe");
        }
        return checks;
    }
}
