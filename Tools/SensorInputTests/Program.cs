using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WingSinger;

int checks = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
void Until(Func<bool> done, string message) {
    double until = BreathSensorClient.Now + 3;
    while (!done() && BreathSensorClient.Now < until) Thread.Sleep(5);
    Check(done(), message);
}
foreach (double v in new[] { -50d, 0, 399, 400 }) Check(BreathSensorProtocol.Resolve(v) == BreathLane.Upper, "Inhale includes 400");
foreach (double v in new[] { 400.01, 600, 999.99 }) Check(BreathSensorProtocol.Resolve(v) == BreathLane.Middle, "Open interval is neutral");
foreach (double v in new[] { 1000d, 1001, 4095 }) Check(BreathSensorProtocol.Resolve(v) == BreathLane.Lower, "Exhale includes 1000");
foreach (double v in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Check(BreathSensorProtocol.Resolve(v) == BreathLane.Middle, "Invalid values are neutral");
foreach (string line in new[] { "400", " 1000.5\r", "6e2", "-1" }) Check(BreathSensorProtocol.TryParseSample(line, out _), "Invariant numeric format");
foreach (string line in new[] { "", "NaN", "Infinity", "1,000", "value=350", "400 garbage", BreathSensorProtocol.Identity })
    Check(!BreathSensorProtocol.TryParseSample(line, out _), "Reject malformed numeric input");
foreach (string name in new[] { "COM1", "com10", "COM999" }) Check(WindowsSerialTransport.IsPortName(name), "COM name accepted");
foreach (string name in new[] { "COM0", "COM", "COM-1", "COM1/../x", "C:\\foo", "\\\\.\\COM1" }) Check(!WindowsSerialTransport.IsPortName(name), "Only COM devices accepted");
var decoder = new BreathSensorLineDecoder();
var lines = new List<string>();
void Feed(string text) { byte[] bytes = Encoding.UTF8.GetBytes(text); decoder.Push(bytes, bytes.Length, lines.Add); }
Feed("WINGSINGER_BREA"); Feed("TH_V1\r\n4"); Feed("00\n1000\r\n");
Check(lines.SequenceEqual(new[] { BreathSensorProtocol.Identity, "400", "1000" }), "Fragmented handshake, CRLF and multiple samples decode correctly");
lines.Clear(); Feed(new string('9', 65) + "400\n600\n");
Check(lines.SequenceEqual(new[] { "600" }), "Oversized lines cannot leak a valid-looking suffix");
lines.Clear(); Feed("\u00ff400\n700\n"); Check(lines.SequenceEqual(new[] { "700" }), "Corrupt bytes discard the entire line");
var options = new BreathSensorConnectionOptions(handshakeTimeout: 0.3, reconnectDelay: 0.03, inactivityTimeout: 0.3);
var fake = new FakePort();
int opens = 0, baud = 0;
using (var client = new BreathSensorClient(options, () => new[] { "COM8" }, (p, b) => { baud = b; Interlocked.Increment(ref opens); return fake; })) {
    client.Start();
    Until(() => Volatile.Read(ref fake.Writes) > 0, "Receiver sends a handshake request");
    Check(fake.LastWrite == BreathSensorProtocol.Hello && baud == 115200, "Exact handshake and fixed 115200 baud");
    fake.Feed("200\nWINGSINGER_BREATH_V2\n"); Thread.Sleep(35);
    Check(!client.Snapshot().Connected && !client.Snapshot().HasSample, "Numbers and incorrect identities cannot identify a device");
    fake.Feed("WINGSINGER_BREA"); fake.Feed("TH_V1\r\n1000\n");
    Until(() => client.Snapshot().IsFresh(0.2) && client.Snapshot().Value == 1000, "Handshake unlocks live samples");
    fake.Feed(string.Concat(Enumerable.Repeat("400\n1000\n", 500)) + "700\n");
    Until(() => client.Snapshot().Value == 700, "A burst retains the newest value without a main-thread queue backlog");
    fake.Feed("NaN\nnoise\n"); Thread.Sleep(65);
    Check(client.Snapshot().Connected && !client.Snapshot().IsFresh(0.05), "Invalid packets do not refresh a stale reading");
    fake.FailRead = true;
    Until(() => fake.Disposed, "Transport errors release the old port");
    client.Dispose();
    Check(!client.IsRunning && !client.Snapshot().HasSample && !client.Snapshot().Connected, "Stop cancels reads and leaves no worker or input");
}
int created = 0;
var first = new FakePort { AutoIdentify = true }; first.InitialSample = "400\n";
var second = new FakePort { AutoIdentify = true };
using (var client = new BreathSensorClient(options, () => new[] { "COM7" }, (p, b) => Interlocked.Increment(ref created) == 1 ? first : second)) {
    client.Start(); Until(() => client.Snapshot().IsFresh(0.2), "First connection receives a sample");
    Until(() => Volatile.Read(ref created) >= 2 && client.Snapshot().Connected && first.Disposed, "Inactivity closes and reconnects automatically");
    Check(!client.Snapshot().HasSample, "Reconnect cannot reuse a previous connection's reading");
    second.Feed("1000\n"); Until(() => client.Snapshot().HasSample && client.Snapshot().Value == 1000, "Reconnected stream becomes usable");
    client.Dispose(); Check(second.Disposed && !client.IsRunning, "Second connection also closes on shutdown");
}
var wrong = new FakePort(); var right = new FakePort { AutoIdentify = true, InitialSample = "600\n" };
using (var client = new BreathSensorClient(options, () => new[] { "COM1", "COM2" }, (p, b) => p == "COM1" ? wrong : right)) {
    client.Start(); Until(() => client.Snapshot().Port == "COM2" && client.Snapshot().IsFresh(0.2), "Scanner skips a device that fails identity and finds the matching sensor");
    Check(wrong.Disposed, "Rejected candidate is closed");
}
int attempts = 0;
var manual = new FakePort { AutoIdentify = true, InitialSample = "600\n" };
using (var client = new BreathSensorClient(new BreathSensorConnectionOptions("COM12", 0.3, 0.03, 0.3),
    () => throw new Exception("Enumeration should not run for a manual port"), (p, b) => {
        if (Interlocked.Increment(ref attempts) == 1) throw new UnauthorizedAccessException("Busy port");
        return manual;
    })) {
    client.Start(); Until(() => client.Snapshot().IsFresh(0.2), "Busy/manual port retries without crashing");
    Check(attempts >= 2 && client.Snapshot().Port == "COM12", "Manual override is honored");
}
using (var client = new BreathSensorClient(options, () => Array.Empty<string>(), (p, b) => throw new Exception("No device should be opened"))) {
    client.Start(); Until(() => client.Snapshot().Status == BreathSensorStatus.Searching, "No-device state remains usable");
    client.Dispose(); Check(!client.IsRunning, "Scanning stops promptly");
}
Check(Marshal.SizeOf(typeof(WindowsSerialTransport).GetNestedType("Dcb", BindingFlags.NonPublic)) == 28, "Windows DCB layout matches the native ABI");
string[] candidates = WindowsSerialTransport.GetCandidatePorts();
Check(candidates.All(WindowsSerialTransport.IsPortName), "Native enumeration returns only serial names and excludes Bluetooth devices");
Console.WriteLine($"PASS: {checks} sensor assertions. Native candidates: [{string.Join(", ", candidates)}]. Physical Arduino not required for simulated transport tests.");

sealed class FakePort : IBreathSerialTransport {
    readonly ConcurrentQueue<byte> bytes = new ConcurrentQueue<byte>();
    public volatile bool Disposed, FailRead;
    public bool AutoIdentify;
    public string InitialSample = "", LastWrite;
    public int Writes;
    public void Feed(string text) { foreach (byte b in Encoding.ASCII.GetBytes(text)) bytes.Enqueue(b); }
    public int Read(byte[] buffer) {
        if (Disposed) throw new ObjectDisposedException(nameof(FakePort));
        if (FailRead) throw new IOException("Simulated unplug");
        int n = 0;
        while (n < buffer.Length && bytes.TryDequeue(out byte b)) buffer[n++] = b;
        if (n == 0) Thread.Sleep(5);
        return n;
    }
    public void WriteLine(string line) {
        LastWrite = line; Interlocked.Increment(ref Writes);
        if (AutoIdentify) Feed(BreathSensorProtocol.Identity + "\n" + InitialSample);
    }
    public void CancelRead() { }
    public void Dispose() { Disposed = true; }
}
