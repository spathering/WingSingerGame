using System;
using System.IO;
using UnityEngine;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Breath Sensor Settings")]
    public sealed class BreathSensorSettings : ScriptableObject {
        public BreathSensorConfiguration defaults = new BreathSensorConfiguration();
    }

    [Serializable]
    public sealed class BreathSensorConfiguration {
        [Header("传感器连接（修改后重新连接）")]
        public BreathSensorConnectionKind connectionKind = BreathSensorConnectionKind.BluetoothLE;
        public string bluetoothDeviceName = WindowsBleTransport.DefaultDeviceName;
        [Tooltip("Csv Notifications 对应 sketch_oct27b：仅订阅通知，无握手。Handshake Uart 用于旧参考固件。")]
        public BreathBluetoothProtocol bluetoothProtocol = BreathBluetoothProtocol.CsvNotifications;
        [Tooltip("可留空自动选择。多台同名设备时填写指定设备的蓝牙地址，例如 AA:BB:CC:DD:EE:FF。")]
        public string bluetoothAddress = "";
        public string bluetoothServiceUuid = WindowsBleTransport.ServiceUuid;
        public string bluetoothNotifyUuid = WindowsBleTransport.NotifyUuid;
        public string bluetoothWriteUuid = WindowsBleTransport.WriteUuid;
        [Header("硬件 BLE CSV：与网页测试端一致")]
        [InspectorName("中心基准（ADC mV）")] public float bluetoothCenterMv = 1300;
        [InspectorName("触发偏差（mV）")] public float bluetoothDeviationMv = 100;
        [Range(1, 32), Tooltip("最近 N 个有效通知的均值；修改后重新连接。")]
        public int bluetoothFilterSamples = 5;
        [Min(5)] public float bluetoothConnectTimeoutSeconds = 30;
        [Header("USB 调试后备：115200 / 8N1")]
        [Tooltip("Leave empty to scan serial ports and identify the sensor with the WingSinger handshake.")]
        public string portOverride = "";
        [Header("USB / 旧 BLE UART 阈值（实时生效）")]
        [InspectorName("吸气阈值（≤，上轨）")]
        [Tooltip("ADC 毫伏值小于或等于此值时判定吸气。必须小于呼气阈值；运行时调整立即生效，无需重新烧录。")]
        public float inhaleMaximum = 400;
        [InspectorName("呼气阈值（≥，下轨）")]
        [Tooltip("ADC 毫伏值大于或等于此值时判定呼气。两阈值之间为静息/中轨；无效阈值配置保持中轨。")]
        public float exhaleMinimum = 1000;
        [InspectorName("开启 Console 调试")]
        [Tooltip("收到新样本或判定状态变化时，输出数值、吸气/呼气/静息状态和当前阈值；不在每帧重复输出旧值。")]
        public bool debugLogging;
        [Header("连接超时（秒）")]
        [Min(0.05f)] public float sampleTimeoutSeconds = 0.5f;
        [Min(0.1f)] public float handshakeTimeoutSeconds = 4;
        [Min(0.1f)] public float reconnectDelaySeconds = 2;
        [Min(0.1f)] public float inactivityTimeoutSeconds = 2;
        [Header("Development fallback")]
        public bool keyboardWhenDisconnected = true;

        public BreathSensorConfiguration Copy() => (BreathSensorConfiguration)MemberwiseClone();
        public bool TryValidate(out string error) {
            error = null;
            try {
                if (!Enum.IsDefined(typeof(BreathSensorConnectionKind), connectionKind) || !Enum.IsDefined(typeof(BreathBluetoothProtocol), bluetoothProtocol))
                    throw new ArgumentException("Unknown connection or protocol mode.");
                if (!(sampleTimeoutSeconds > 0) || float.IsInfinity(sampleTimeoutSeconds) || !(inactivityTimeoutSeconds >= sampleTimeoutSeconds))
                    throw new ArgumentException("Freshness must be positive; inactivity must be at least freshness.");
                bool csv = connectionKind == BreathSensorConnectionKind.BluetoothLE && bluetoothProtocol == BreathBluetoothProtocol.CsvNotifications;
                if (csv ? !BreathSensorProtocol.AreCenteredThresholdsValid(bluetoothCenterMv, bluetoothDeviationMv)
                    : !BreathSensorProtocol.AreThresholdsValid(inhaleMaximum, exhaleMinimum))
                    throw new ArgumentException("Invalid breathing thresholds.");
                new BreathSensorConnectionOptions(connectionKind == BreathSensorConnectionKind.UsbSerial ? portOverride : "",
                    connectionKind == BreathSensorConnectionKind.BluetoothLE ? bluetoothConnectTimeoutSeconds : handshakeTimeoutSeconds,
                    reconnectDelaySeconds, inactivityTimeoutSeconds, csv, bluetoothFilterSamples);
                if (connectionKind == BreathSensorConnectionKind.BluetoothLE) {
                    if (!csv && string.IsNullOrWhiteSpace(bluetoothWriteUuid)) throw new ArgumentException("Legacy UART needs a Write UUID.");
                    WindowsBleTransport.ValidateProfile(bluetoothServiceUuid, bluetoothNotifyUuid, csv ? "" : bluetoothWriteUuid);
                    ulong address = WindowsBleTransport.ParseAddress(bluetoothAddress);
                    if (csv && string.IsNullOrWhiteSpace(bluetoothDeviceName) && address == 0) throw new ArgumentException("Specify a BLE device name or address.");
                }
                return true;
            } catch (ArgumentException invalid) { error = invalid.Message; return false; }
        }
        public bool SameConnection(BreathSensorConfiguration other) => connectionKind == other.connectionKind && bluetoothProtocol == other.bluetoothProtocol
            && bluetoothDeviceName == other.bluetoothDeviceName && bluetoothAddress == other.bluetoothAddress
            && bluetoothServiceUuid == other.bluetoothServiceUuid && bluetoothNotifyUuid == other.bluetoothNotifyUuid && bluetoothWriteUuid == other.bluetoothWriteUuid
            && bluetoothFilterSamples == other.bluetoothFilterSamples && bluetoothConnectTimeoutSeconds == other.bluetoothConnectTimeoutSeconds
            && portOverride == other.portOverride && handshakeTimeoutSeconds == other.handshakeTimeoutSeconds
            && reconnectDelaySeconds == other.reconnectDelaySeconds && inactivityTimeoutSeconds == other.inactivityTimeoutSeconds;
    }

    public static class BreathSensorConfigFile {
        [Serializable] sealed class Document { public Document() { } public int version = 1; public BreathSensorConfiguration configuration; }
        public static bool TryLoad(string path, out BreathSensorConfiguration configuration, out string error) {
            configuration = null; error = null;
            if (!File.Exists(path)) return false;
            try {
                var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
                if (document == null || document.version != 1 || document.configuration == null) throw new InvalidDataException("Unsupported hardware configuration file.");
                if (!document.configuration.TryValidate(out error)) return false;
                configuration = document.configuration; return true;
            } catch (Exception failure) { error = failure.Message; return false; }
        }
        public static bool TrySave(string path, BreathSensorConfiguration configuration, out string error) {
            if (!configuration.TryValidate(out error)) return false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                string pending = path + ".tmp";
                File.WriteAllText(pending, JsonUtility.ToJson(new Document { configuration = configuration }, true));
                if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path);
                return true;
            } catch (Exception failure) { error = failure.Message; return false; }
        }
    }
}
