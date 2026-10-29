using System;
using System.Globalization;
using System.Text;

namespace WingSinger {
    public enum BreathBluetoothProtocol { CsvNotifications = 0, HandshakeUart = 1 }

    public static class BreathSensorProtocol {
        public const int BaudRate = 115200;
        public const string Hello = "WINGSINGER_HELLO";
        public const string Identity = "WINGSINGER_BREATH_V1";

        public static bool TryParseSample(string line, out double value) =>
            double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);

        // sketch_oct27b sends exactly one CSV sample per notification, without a newline.
        public static bool TryParseNotification(byte[] bytes, int count, out double adc, out double pressure, out string state) {
            adc = pressure = 0; state = "";
            if (count <= 0 || count > BreathSensorLineDecoder.MaximumLineLength) return false;
            for (int i = 0; i < count; i++) if (bytes[i] < 32 || bytes[i] > 126) return false;
            string[] fields = Encoding.ASCII.GetString(bytes, 0, count).Split(',');
            if (fields.Length != 3 || !TryParseSample(fields[0], out adc) || adc < 0 || adc > 4095
                || !TryParseSample(fields[1], out pressure)) return false;
            state = fields[2];
            return state == "REST" || state == "INHALE" || state == "EXHALE";
        }

        public static bool AreCenteredThresholdsValid(double center, double deviation) =>
            AreThresholdsValid(center - deviation, center + deviation) && deviation > 0;

        public static BreathLane ResolveCentered(double value, double center, double deviation) {
            if (double.IsNaN(value) || double.IsInfinity(value) || !AreCenteredThresholdsValid(center, deviation)) return BreathLane.Middle;
            // Match the supplied web tester: equality remains in the neutral dead zone.
            return value < center - deviation ? BreathLane.Upper : value > center + deviation ? BreathLane.Lower : BreathLane.Middle;
        }

        public static bool AreThresholdsValid(double inhaleMaximum, double exhaleMinimum) =>
            !double.IsNaN(inhaleMaximum) && !double.IsInfinity(inhaleMaximum)
            && !double.IsNaN(exhaleMinimum) && !double.IsInfinity(exhaleMinimum)
            && inhaleMaximum < exhaleMinimum;

        public static BreathLane Resolve(double value, double inhaleMaximum = 400, double exhaleMinimum = 1000) {
            if (double.IsNaN(value) || double.IsInfinity(value) || !AreThresholdsValid(inhaleMaximum, exhaleMinimum))
                return BreathLane.Middle;
            return value <= inhaleMaximum ? BreathLane.Upper : value >= exhaleMinimum ? BreathLane.Lower : BreathLane.Middle;
        }
    }

    // A serial Read is an arbitrary byte fragment, not necessarily a complete line.
    public sealed class BreathSensorLineDecoder {
        public const int MaximumLineLength = 64;
        readonly StringBuilder line = new StringBuilder(MaximumLineLength);
        bool discard;

        public void Push(byte[] bytes, int count, Action<string> receive) {
            for (int i = 0; i < count; i++) {
                char c = (char)bytes[i];
                if (c == '\n') {
                    if (!discard && line.Length > 0) receive(line.ToString().Trim());
                    line.Clear(); discard = false;
                } else if (!discard) {
                    if (line.Length >= MaximumLineLength || c > 127 || (char.IsControl(c) && c != '\r' && c != '\t')) {
                        line.Clear(); discard = true;
                    } else line.Append(c);
                }
            }
        }
    }
}
