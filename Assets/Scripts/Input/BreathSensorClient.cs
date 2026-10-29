using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WingSinger {
    public interface IBreathSerialTransport : IDisposable {
        int Read(byte[] buffer);
        void WriteLine(string line);
        void CancelRead();
    }

    public enum BreathSensorStatus { Stopped, Searching, Identifying, Connected }

    public readonly struct BreathSensorSnapshot {
        public readonly BreathSensorStatus Status;
        public readonly string Port, Message;
        public readonly bool HasSample;
        public readonly double Value, SampleAgeSeconds;
        public readonly long SampleSequence;
        public bool Connected => Status == BreathSensorStatus.Connected;
        public bool IsFresh(double timeout) => Connected && HasSample && SampleAgeSeconds <= timeout;
        public BreathSensorSnapshot(BreathSensorStatus status, string port, string message, bool hasSample, double value, double age, long sampleSequence = 0) {
            Status = status; Port = port; Message = message; HasSample = hasSample; Value = value; SampleAgeSeconds = age;
            SampleSequence = sampleSequence;
        }
    }

    public sealed class BreathSensorConnectionOptions {
        public readonly string PortOverride;
        public readonly double HandshakeTimeout, ReconnectDelay, InactivityTimeout;
        public BreathSensorConnectionOptions(string portOverride = "", double handshakeTimeout = 4,
            double reconnectDelay = 2, double inactivityTimeout = 2) {
            if (!Positive(handshakeTimeout) || !Positive(reconnectDelay) || !Positive(inactivityTimeout))
                throw new ArgumentException("Sensor timeouts must be positive and finite.");
            PortOverride = (portOverride ?? "").Trim();
            if (PortOverride.Length > 0 && !WindowsSerialTransport.IsPortName(PortOverride))
                throw new ArgumentException("A serial port override must be a COM port name, for example COM5.");
            HandshakeTimeout = handshakeTimeout; ReconnectDelay = reconnectDelay; InactivityTimeout = inactivityTimeout;
        }
        static bool Positive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }

    // No Unity objects or callbacks are accessed by this worker. Only the latest sample is retained.
    public sealed class BreathSensorClient : IDisposable {
        readonly BreathSensorConnectionOptions options;
        readonly Func<string[]> enumerate;
        readonly Func<string, int, IBreathSerialTransport> open;
        readonly object gate = new object();
        Thread worker;
        volatile bool stopping;
        IBreathSerialTransport active;
        BreathSensorStatus status = BreathSensorStatus.Stopped;
        string port = "", message = "Stopped", lastGoodPort;
        bool hasSample;
        double value, sampleTime;
        long sampleSequence;
        public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        public bool IsRunning => worker != null && worker.IsAlive;

        public BreathSensorClient(BreathSensorConnectionOptions options, Func<string[]> enumerate = null,
            Func<string, int, IBreathSerialTransport> open = null) {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.enumerate = enumerate ?? WindowsSerialTransport.GetCandidatePorts;
            this.open = open ?? WindowsSerialTransport.Open;
        }

        public void Start() {
            if (worker != null) throw new InvalidOperationException("Create a new sensor client to restart a stopped receiver.");
            worker = new Thread(Run) { IsBackground = true, Name = "WingSinger Arduino" };
            worker.Start();
        }

        public BreathSensorSnapshot Snapshot() {
            lock (gate) return new BreathSensorSnapshot(status, port, message, hasSample, value, hasSample ? Now - sampleTime : double.PositiveInfinity, sampleSequence);
        }

        void SetStatus(BreathSensorStatus next, string name, string detail) {
            lock (gate) { status = next; port = name; message = detail; hasSample = false; }
        }

        void Run() {
            try {
                while (!stopping) {
                    SetStatus(BreathSensorStatus.Searching, "", "Searching for the breath sensor");
                    try {
                        var ports = options.PortOverride.Length > 0 ? new[] { options.PortOverride } : enumerate();
                        foreach (string candidate in ports.Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(p => p == lastGoodPort ? 0 : 1).ThenBy(p => p, StringComparer.OrdinalIgnoreCase)) {
                            if (stopping) break;
                            ProbeAndRead(candidate);
                        }
                    } catch (Exception error) {
                        if (!stopping) SetStatus(BreathSensorStatus.Searching, "", error.Message);
                    }
                    Wait(options.ReconnectDelay);
                }
            } finally { SetStatus(BreathSensorStatus.Stopped, "", "Stopped"); }
        }

        void ProbeAndRead(string candidate) {
            IBreathSerialTransport transport = null;
            try {
                SetStatus(BreathSensorStatus.Identifying, candidate, "Checking device identity");
                transport = open(candidate, BreathSensorProtocol.BaudRate);
                lock (gate) active = transport;
                if (stopping) return;
                bool identified = false;
                double opened = Now, lastHello = double.NegativeInfinity, lastValid = opened;
                var decoder = new BreathSensorLineDecoder();
                var buffer = new byte[256];
                Action<string> receive = line => {
                    if (!identified) {
                        if (line != BreathSensorProtocol.Identity) return;
                        identified = true; lastValid = Now; lastGoodPort = candidate;
                        SetStatus(BreathSensorStatus.Connected, candidate, "Device recognized; waiting for samples");
                    } else if (BreathSensorProtocol.TryParseSample(line, out double raw)) {
                        lastValid = Now;
                        lock (gate) { value = raw; sampleTime = lastValid; hasSample = true; sampleSequence++; message = "Receiving"; }
                    }
                };
                while (!stopping) {
                    double now = Now;
                    if (!identified) {
                        if (now - opened >= options.HandshakeTimeout) break;
                        // Repeated requests tolerate the reset/boot delay caused by opening an Arduino port.
                        if (now - lastHello >= 0.5) { transport.WriteLine(BreathSensorProtocol.Hello); lastHello = now; }
                    } else if (now - lastValid >= options.InactivityTimeout) break;
                    int count = transport.Read(buffer);
                    if (count > 0) decoder.Push(buffer, count, receive);
                    else Thread.Sleep(5);
                }
            } catch (Exception error) {
                if (!stopping) SetStatus(BreathSensorStatus.Searching, "", candidate + ": " + error.Message);
            } finally {
                lock (gate) active = null;
                transport?.Dispose();
                if (!stopping && Snapshot().Status != BreathSensorStatus.Searching)
                    SetStatus(BreathSensorStatus.Searching, "", "Disconnected or handshake/data timeout");
            }
        }

        void Wait(double seconds) {
            double until = Now + seconds;
            while (!stopping && Now < until) Thread.Sleep(25);
        }

        public void Dispose() {
            stopping = true;
            lock (gate) {
                try { active?.CancelRead(); } catch (ObjectDisposedException) { }
                hasSample = false; status = BreathSensorStatus.Stopped; port = ""; message = "Stopped";
            }
            if (worker != null && worker != Thread.CurrentThread) worker.Join(500);
        }
    }
}
