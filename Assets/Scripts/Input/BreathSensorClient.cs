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
        public readonly double FilteredValue, PressureKPa;
        public readonly string FirmwareState;
        public readonly long SampleSequence;
        public bool Connected => Status == BreathSensorStatus.Connected;
        public bool IsFresh(double timeout) => Connected && HasSample && SampleAgeSeconds <= timeout;
        public BreathSensorSnapshot(BreathSensorStatus status, string port, string message, bool hasSample, double value, double age, long sampleSequence = 0,
            double? filteredValue = null, double pressureKPa = 0, string firmwareState = "") {
            Status = status; Port = port; Message = message; HasSample = hasSample; Value = value; SampleAgeSeconds = age;
            SampleSequence = sampleSequence;
            FilteredValue = filteredValue ?? value; PressureKPa = pressureKPa; FirmwareState = firmwareState;
        }
    }

    public sealed class BreathSensorConnectionOptions {
        public readonly string PortOverride;
        public readonly double HandshakeTimeout, ReconnectDelay, InactivityTimeout;
        public readonly bool CsvNotifications;
        public readonly int FilterSamples;
        public BreathSensorConnectionOptions(string portOverride = "", double handshakeTimeout = 4,
            double reconnectDelay = 2, double inactivityTimeout = 2, bool csvNotifications = false, int filterSamples = 5) {
            if (!Positive(handshakeTimeout) || !Positive(reconnectDelay) || !Positive(inactivityTimeout))
                throw new ArgumentException("Sensor timeouts must be positive and finite.");
            PortOverride = (portOverride ?? "").Trim();
            if (PortOverride.Length > 0 && !WindowsSerialTransport.IsPortName(PortOverride))
                throw new ArgumentException("A serial port override must be a COM port name, for example COM5.");
            HandshakeTimeout = handshakeTimeout; ReconnectDelay = reconnectDelay; InactivityTimeout = inactivityTimeout;
            if (filterSamples < 1 || filterSamples > 32) throw new ArgumentException("Filter samples must be between 1 and 32.");
            CsvNotifications = csvNotifications; FilterSamples = filterSamples;
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
        double filteredValue, pressureKPa;
        string firmwareState = "";
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
            lock (gate) return new BreathSensorSnapshot(status, port, message, hasSample, value, hasSample ? Now - sampleTime : double.PositiveInfinity,
                sampleSequence, filteredValue, pressureKPa, firmwareState);
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
                SetStatus(BreathSensorStatus.Identifying, candidate, options.CsvNotifications ? "Waiting for a valid BLE CSV notification" : "Checking device identity");
                transport = open(candidate, BreathSensorProtocol.BaudRate);
                lock (gate) active = transport;
                if (stopping) return;
                bool identified = false;
                double opened = Now, lastHello = double.NegativeInfinity, lastValid = opened;
                var decoder = new BreathSensorLineDecoder();
                var buffer = new byte[256];
                var average = new double[options.FilterSamples];
                int averageIndex = 0, averageCount = 0;
                double sum = 0;
                Action<double, double, string> sample = (raw, pressure, state) => {
                    if (!identified) {
                        identified = true; lastGoodPort = candidate;
                        SetStatus(BreathSensorStatus.Connected, candidate, "BLE CSV recognized");
                    }
                    lastValid = Now;
                    sum -= average[averageIndex]; average[averageIndex] = raw; sum += raw;
                    averageIndex = (averageIndex + 1) % average.Length; averageCount = Math.Min(averageCount + 1, average.Length);
                    lock (gate) {
                        value = raw; filteredValue = options.CsvNotifications ? sum / averageCount : raw;
                        pressureKPa = pressure; firmwareState = state;
                        sampleTime = lastValid; hasSample = true; sampleSequence++; message = "Receiving";
                    }
                };
                Action<string> receive = line => {
                    if (!identified) {
                        if (line != BreathSensorProtocol.Identity) return;
                        identified = true; lastValid = Now; lastGoodPort = candidate;
                        SetStatus(BreathSensorStatus.Connected, candidate, "Device recognized; waiting for samples");
                    } else if (BreathSensorProtocol.TryParseSample(line, out double raw)) {
                        sample(raw, 0, "");
                    }
                };
                while (!stopping) {
                    double now = Now;
                    if (!identified) {
                        if (now - opened >= options.HandshakeTimeout) break;
                        // Repeated requests tolerate the reset/boot delay caused by opening an Arduino port.
                        if (!options.CsvNotifications && now - lastHello >= 0.5) { transport.WriteLine(BreathSensorProtocol.Hello); lastHello = now; }
                    } else if (now - lastValid >= options.InactivityTimeout) break;
                    int count = transport.Read(buffer);
                    if (count > 0) {
                        if (options.CsvNotifications) {
                            // The BLE transport returns one complete notification per Read, including no delimiters.
                            if (BreathSensorProtocol.TryParseNotification(buffer, count, out double raw, out double pressure, out string state)) sample(raw, pressure, state);
                        } else decoder.Push(buffer, count, receive);
                    }
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
