using System;
using System.Globalization;
using System.Text;

namespace WingSinger {
    public static class BreathSensorProtocol {
        public const int BaudRate = 115200;
        public const string Hello = "WINGSINGER_HELLO";
        public const string Identity = "WINGSINGER_BREATH_V1";

        public static bool TryParseSample(string line, out double value) =>
            double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);

        public static BreathLane Resolve(double value, double inhaleMaximum = 400, double exhaleMinimum = 1000) {
            if (double.IsNaN(value) || double.IsInfinity(value)) return BreathLane.Middle;
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
