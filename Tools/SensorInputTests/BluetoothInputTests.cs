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
        var sensor = new ArduinoBreathInput { sampleTimeoutSeconds = .05f, bluetoothProtocol = BreathBluetoothProtocol.HandshakeUart };
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
        checks += CsvTests();
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

    static int CsvTests() {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        void Until(Func<bool> condition, string message) {
            double until = BreathSensorClient.Now + 3;
            while (!condition() && BreathSensorClient.Now < until) Thread.Sleep(5);
            Check(condition(), message);
        }
        bool Parse(string text) { var data = System.Text.Encoding.UTF8.GetBytes(text); return BreathSensorProtocol.TryParseNotification(data, data.Length, out _, out _, out _); }
        foreach (string valid in new[] { "1300,0.000,REST", "1199,-0.080,INHALE", "1500,0.150,EXHALE" }) Check(Parse(valid), "Hardware CSV accepted without newline");
        foreach (string invalid in new[] { "", "1300", "1300,0.0", "NaN,0,REST", "1300,Infinity,REST", "1300,0,OTHER",
            "-1,0,REST", "4096,0,REST", "1300,0,REST\n1500,0,EXHALE", "1300,0,REST1500,0,EXHALE", "1300,0,RES", "T", "1300,0,REST\0", "1300,0,呼气", new string('1', 65) })
            Check(!Parse(invalid), "Malformed/truncated/combined CSV notifications rejected");
        Check(BreathSensorProtocol.ResolveCentered(1199,1300,100) == BreathLane.Upper, "Below centered threshold inhales");
        Check(BreathSensorProtocol.ResolveCentered(1401,1300,100) == BreathLane.Lower, "Above centered threshold exhales");
        foreach (double value in new[] {1200d,1300,1400}) Check(BreathSensorProtocol.ResolveCentered(value,1300,100) == BreathLane.Middle, "Web tester dead-zone boundaries remain neutral");
        foreach (double invalid in new[] {0d,-1,double.NaN,double.PositiveInfinity}) Check(!BreathSensorProtocol.AreCenteredThresholdsValid(1300,invalid), "Invalid centered threshold rejected");
        WindowsBleTransport.ValidateProfile(WindowsBleTransport.ServiceUuid, WindowsBleTransport.NotifyUuid, "");
        var first = new PacketPort(); var next = new PacketPort(); int opens = 0;
        var sensor = new ArduinoBreathInput { sampleTimeoutSeconds = .05f, keyboardWhenDisconnected = false };
        var client = new BreathSensorClient(new BreathSensorConnectionOptions("", .5, .02, .25, csvNotifications:true),
            () => new[] {"BLE: ESP32_C3_Mini"}, (_,__) => Interlocked.Increment(ref opens) == 1 ? first : next);
        try {
            sensor.Connect(client);
            first.Feed("1300,0.000,REST"); Until(() => sensor.ReceiverSnapshot.HasSample, "First valid CSV connects without identity or writes");
            Check(first.Writes == 0 && sensor.ReadLane() == BreathLane.Middle, "Notify-only firmware receives no HELLO");
            first.Feed("1000,-0.100,INHALE"); first.Feed("1100,-0.100,INHALE"); first.Feed("1200,0,REST"); first.Feed("1300,0,REST");
            Until(() => sensor.ReceiverSnapshot.SampleSequence == 5, "Each notification counted exactly once");
            Check(sensor.ReceiverSnapshot.FilteredValue == 1180 && sensor.ReadLane() == BreathLane.Upper, "Five valid samples use moving average, not latest ADC");
            first.Feed("1400,0.150,EXHALE"); Until(() => sensor.ReceiverSnapshot.SampleSequence == 6, "Sixth notification received");
            Check(sensor.ReceiverSnapshot.FilteredValue == 1200 && sensor.ReadLane() == BreathLane.Middle, "Sliding window evicts oldest sample and neutral includes boundary");
            Check(sensor.ReceiverSnapshot.Value == 1400 && sensor.ReceiverSnapshot.PressureKPa == .15 && sensor.ReceiverSnapshot.FirmwareState == "EXHALE",
                "Raw ADC and firmware pressure/state retained independently from Unity decision");
            sensor.bluetoothCenterMv = 1000; Check(sensor.ReadLane() == BreathLane.Lower, "Changing center remaps existing average immediately");
            sensor.bluetoothDeviationMv = -1; Check(!sensor.ThresholdsValid && sensor.ReadLane() == BreathLane.Middle, "Invalid deviation blocks input");
            sensor.bluetoothCenterMv = 1300; sensor.bluetoothDeviationMv = 100;
            var update = typeof(ArduinoBreathInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            sensor.debugLogging = true; UnityEngine.Debug.Messages.Clear(); update.Invoke(sensor,null);
            Check(UnityEngine.Debug.Messages.Count == 1 && UnityEngine.Debug.Messages[0].Contains("均值=1200") && UnityEngine.Debug.Messages[0].Contains("固件=EXHALE"), "Console reports averaged value and firmware diagnostic");
            update.Invoke(sensor,null); Check(UnityEngine.Debug.Messages.Count == 1, "Debug never replays old notification each frame");
            first.Feed("1400,NaN,REST"); Thread.Sleep(80);
            Check(sensor.ReceiverSnapshot.SampleSequence == 6 && sensor.ReadLane() == BreathLane.Middle && !sensor.ReceiverSnapshot.IsFresh(.05), "Invalid data cannot keep input fresh");
            Until(() => opens >= 2 && first.Disposed, "Inactivity reconnects notify-only receiver");
            Check(!sensor.ReceiverSnapshot.HasSample, "Reconnect clears previous reading");
            next.Feed("1600,0.300,EXHALE"); Until(() => sensor.ReceiverSnapshot.SampleSequence == 7, "Replacement notification received");
            Check(sensor.ReceiverSnapshot.FilteredValue == 1600 && sensor.ReadLane() == BreathLane.Lower && next.Writes == 0, "Reconnect clears smoothing window and still sends no command");
        } finally { sensor.Disconnect(); }
        Check(next.Disposed && !client.IsRunning, "Notify receiver fully stops");
        using (var native = WindowsBleTransport.Open("WingSinger Native Lifecycle Test - no match", "", WindowsBleTransport.ServiceUuid, WindowsBleTransport.NotifyUuid, "")) {
            Thread.Sleep(50);
            try { Check(native.Read(new byte[256]) == 0, "Native packet read export loads without invented data"); }
            catch (IOException error) { Check(error.Message.Length > 0, "Native radio error remains actionable"); }
            bool rejected = false; try { native.WriteLine(BreathSensorProtocol.Hello); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Notify-only transport forbids write commands"); native.CancelRead();
        }
        return checks;
    }

    sealed class PacketPort : IBreathSerialTransport {
        readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> packets = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
        public volatile bool Disposed;
        public int Writes;
        public void Feed(string text) => packets.Enqueue(System.Text.Encoding.ASCII.GetBytes(text));
        public int Read(byte[] buffer) { if (!packets.TryDequeue(out var data)) return 0; Array.Copy(data,buffer,data.Length); return data.Length; }
        public void WriteLine(string line) => Interlocked.Increment(ref Writes);
        public void CancelRead() { }
        public void Dispose() => Disposed = true;
    }
}
