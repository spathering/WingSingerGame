using System;
using UnityEngine;

namespace WingSinger {
    public sealed class ArduinoBreathInput : BreathInput {
        [Header("传感器连接（修改后重新连接）")]
        public BreathSensorConnectionKind connectionKind = BreathSensorConnectionKind.BluetoothLE;
        public string bluetoothDeviceName = WindowsBleTransport.DefaultDeviceName;
        [Tooltip("可留空自动选择。多台同名设备时填写指定设备的蓝牙地址，例如 AA:BB:CC:DD:EE:FF。")]
        public string bluetoothAddress = "";
        public string bluetoothServiceUuid = WindowsBleTransport.ServiceUuid;
        public string bluetoothNotifyUuid = WindowsBleTransport.NotifyUuid;
        public string bluetoothWriteUuid = WindowsBleTransport.WriteUuid;
        [Min(5)] public float bluetoothConnectTimeoutSeconds = 30;
        [Header("USB 调试后备：115200 / 8N1")]
        [Tooltip("Leave empty to scan serial ports and identify the sensor with the WingSinger handshake.")]
        public string portOverride = "";
        [Header("呼吸阈值与调试（实时生效）")]
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
        public KeyboardBreathInput keyboardFallback;
        [Header("Live receiver status (Play Mode)")]
        [SerializeField] string connectionStatus = "Stopped";
        [SerializeField] string connectedPort = "";
        [SerializeField] double latestValue;
        [SerializeField] bool freshSample;
        [SerializeField, InspectorName("当前呼吸状态")] string breathState = "未连接";
        BreathSensorClient receiver;
        BreathSensorConnectionKind activeConnectionKind;
        long lastLoggedSampleSequence = -1;
        string lastDebugState;
        public BreathSensorClient Receiver => receiver;
        public BreathSensorSnapshot ReceiverSnapshot => receiver == null ? default : receiver.Snapshot();
        public string ConnectionLabel => (receiver == null ? connectionKind : activeConnectionKind) == BreathSensorConnectionKind.BluetoothLE ? "BLE" : "USB";

        void OnEnable() { if (Application.isPlaying) Connect(); }

        // A supplied client also allows deterministic receiver/scene tests without a physical board.
        public void Connect(BreathSensorClient suppliedClient = null) {
            Disconnect();
            if (!Application.isPlaying) return;
            try {
                if (!Finite(sampleTimeoutSeconds) || sampleTimeoutSeconds <= 0 || inactivityTimeoutSeconds < sampleTimeoutSeconds)
                    throw new ArgumentException("Sensor timeouts are invalid.");
                bool bluetooth = connectionKind == BreathSensorConnectionKind.BluetoothLE;
                if (suppliedClient == null && !(bluetooth ? WindowsBleTransport.IsSupported : WindowsSerialTransport.IsSupported)) {
                    connectionStatus = "Sensor input supports Windows x64; keyboard fallback is available.";
                    return;
                }
                activeConnectionKind = connectionKind;
                if (suppliedClient != null) receiver = suppliedClient;
                else if (bluetooth) {
                    WindowsBleTransport.ValidateProfile(bluetoothServiceUuid, bluetoothNotifyUuid, bluetoothWriteUuid);
                    WindowsBleTransport.ParseAddress(bluetoothAddress);
                    // Freeze Inspector configuration for this connection; worker threads never read Unity fields.
                    string name = bluetoothDeviceName, address = bluetoothAddress, service = bluetoothServiceUuid,
                        notify = bluetoothNotifyUuid, write = bluetoothWriteUuid;
                    receiver = new BreathSensorClient(new BreathSensorConnectionOptions("", bluetoothConnectTimeoutSeconds,
                        reconnectDelaySeconds, inactivityTimeoutSeconds), () => new[] { "BLE: " + name },
                        (_, __) => WindowsBleTransport.Open(name, address, service, notify, write));
                } else receiver = new BreathSensorClient(new BreathSensorConnectionOptions(portOverride,
                    handshakeTimeoutSeconds, reconnectDelaySeconds, inactivityTimeoutSeconds));
                receiver.Start();
            } catch (Exception error) {
                Disconnect(); connectionStatus = error.Message;
                Debug.LogError("WingSinger sensor: " + error.Message, this);
            }
        }

        public override BreathLane ReadLane() {
            var state = ReceiverSnapshot;
            if (receiver != null && state.IsFresh(sampleTimeoutSeconds))
                return BreathSensorProtocol.Resolve(state.Value, inhaleMaximum, exhaleMinimum);
            // A recognized sensor with stale data is neutral, even while a keyboard key is held.
            if (receiver != null && state.Connected) return BreathLane.Middle;
            return keyboardWhenDisconnected && keyboardFallback != null ? keyboardFallback.ReadLane() : BreathLane.Middle;
        }

        void Update() {
            if (receiver == null) return;
            var state = receiver.Snapshot();
            connectionStatus = state.Status + ": " + state.Message; connectedPort = state.Port;
            latestValue = state.Value; freshSample = state.IsFresh(sampleTimeoutSeconds);
            bool validThresholds = BreathSensorProtocol.AreThresholdsValid(inhaleMaximum, exhaleMinimum);
            BreathLane lane = BreathSensorProtocol.Resolve(state.Value, inhaleMaximum, exhaleMinimum);
            breathState = !validThresholds ? "阈值无效（保持中轨）"
                : freshSample ? lane == BreathLane.Upper ? "吸气 / 上轨" : lane == BreathLane.Lower ? "呼气 / 下轨" : "静息 / 中轨"
                : state.Connected ? "等待有效数据 / 中轨" : "未连接";

            if (!debugLogging) {
                lastLoggedSampleSequence = -1; lastDebugState = null;
                return;
            }
            // Log on the main thread, even during the level's opening preview.
            // The receiver still stores only the newest sample, without a log queue.
            if (freshSample && (state.SampleSequence != lastLoggedSampleSequence || breathState != lastDebugState)) {
                Debug.Log(FormattableString.Invariant($"[WingSinger Arduino] {ConnectionLabel} {state.Port} | 数值={state.Value:0.###} mV | 状态={breathState} | 吸气≤{inhaleMaximum:0.###}，呼气≥{exhaleMinimum:0.###}"), this);
                lastLoggedSampleSequence = state.SampleSequence;
            } else if (!freshSample && breathState != lastDebugState) {
                Debug.Log($"[WingSinger Arduino] 状态={breathState} | {connectionStatus}", this);
            }
            lastDebugState = breathState;
        }

        public void Disconnect() {
            receiver?.Dispose(); receiver = null;
            connectionStatus = "Stopped"; connectedPort = ""; latestValue = 0; freshSample = false;
            breathState = "未连接"; lastLoggedSampleSequence = -1; lastDebugState = null;
        }
        void OnDisable() => Disconnect();
        void OnApplicationQuit() => Disconnect();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
