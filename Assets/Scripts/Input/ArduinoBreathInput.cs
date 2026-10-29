using System;
using UnityEngine;

namespace WingSinger {
    public sealed class ArduinoBreathInput : BreathInput {
        [Header("独立硬件配置")]
        public BreathSensorSettings settings;
        [Tooltip("加载本机 HardwareInput.json；游戏设置不会修改工程中的默认资源。")]
        public bool loadSavedConfiguration = true;
        [NonSerialized] public string ConfigurationFileOverride;
        BreathSensorConfiguration configuration;
        public string ConfigurationPath => string.IsNullOrEmpty(ConfigurationFileOverride)
            ? System.IO.Path.Combine(Application.persistentDataPath, "HardwareInput.json") : ConfigurationFileOverride;
        public BreathSensorConfiguration Configuration {
            get {
                if (configuration == null) {
                    configuration = DefaultConfiguration();
                    if (loadSavedConfiguration && Application.isPlaying) {
                        if (BreathSensorConfigFile.TryLoad(ConfigurationPath, out var saved, out string error)) configuration = saved;
                        else if (error != null) Debug.LogWarning("WingSinger hardware config: " + error, this);
                    }
                }
                return configuration;
            }
        }
        public BreathSensorConfiguration DefaultConfiguration() => settings != null && settings.defaults != null ? settings.defaults.Copy() : new BreathSensorConfiguration();
        public bool TryApplyConfiguration(BreathSensorConfiguration next, bool save, out string error) {
            if (next == null) { error = "Hardware configuration is missing."; return false; }
            if (!next.TryValidate(out error)) return false;
            if (save && !BreathSensorConfigFile.TrySave(ConfigurationPath, next, out error)) return false;
            bool reconnect = receiver != null && !Configuration.SameConnection(next);
            configuration = next.Copy();
            if (reconnect) Connect();
            return true;
        }
        // Compatibility properties for gameplay/setup callers; serialized data lives in the settings resource.
        public bool debugLogging { get => Configuration.debugLogging; set => Configuration.debugLogging = value; }
        public BreathSensorConnectionKind connectionKind { get => Configuration.connectionKind; set => Configuration.connectionKind = value; }
        public string bluetoothDeviceName { get => Configuration.bluetoothDeviceName; set => Configuration.bluetoothDeviceName = value; }
        public BreathBluetoothProtocol bluetoothProtocol { get => Configuration.bluetoothProtocol; set => Configuration.bluetoothProtocol = value; }
        public string bluetoothAddress { get => Configuration.bluetoothAddress; set => Configuration.bluetoothAddress = value; }
        public string bluetoothServiceUuid { get => Configuration.bluetoothServiceUuid; set => Configuration.bluetoothServiceUuid = value; }
        public string bluetoothNotifyUuid { get => Configuration.bluetoothNotifyUuid; set => Configuration.bluetoothNotifyUuid = value; }
        public string bluetoothWriteUuid { get => Configuration.bluetoothWriteUuid; set => Configuration.bluetoothWriteUuid = value; }
        public float bluetoothCenterMv { get => Configuration.bluetoothCenterMv; set => Configuration.bluetoothCenterMv = value; }
        public float bluetoothDeviationMv { get => Configuration.bluetoothDeviationMv; set => Configuration.bluetoothDeviationMv = value; }
        public int bluetoothFilterSamples { get => Configuration.bluetoothFilterSamples; set => Configuration.bluetoothFilterSamples = value; }
        public float bluetoothConnectTimeoutSeconds { get => Configuration.bluetoothConnectTimeoutSeconds; set => Configuration.bluetoothConnectTimeoutSeconds = value; }
        public string portOverride { get => Configuration.portOverride; set => Configuration.portOverride = value; }
        public float inhaleMaximum { get => Configuration.inhaleMaximum; set => Configuration.inhaleMaximum = value; }
        public float exhaleMinimum { get => Configuration.exhaleMinimum; set => Configuration.exhaleMinimum = value; }
        public float sampleTimeoutSeconds { get => Configuration.sampleTimeoutSeconds; set => Configuration.sampleTimeoutSeconds = value; }
        public float handshakeTimeoutSeconds { get => Configuration.handshakeTimeoutSeconds; set => Configuration.handshakeTimeoutSeconds = value; }
        public float reconnectDelaySeconds { get => Configuration.reconnectDelaySeconds; set => Configuration.reconnectDelaySeconds = value; }
        public float inactivityTimeoutSeconds { get => Configuration.inactivityTimeoutSeconds; set => Configuration.inactivityTimeoutSeconds = value; }
        public bool keyboardWhenDisconnected { get => Configuration.keyboardWhenDisconnected; set => Configuration.keyboardWhenDisconnected = value; }
        public KeyboardBreathInput keyboardFallback;
        [Header("Live receiver status (Play Mode)")]
        [SerializeField] string connectionStatus = "Stopped";
        [SerializeField] string connectedPort = "";
        [SerializeField] double latestValue;
        [SerializeField] double filteredValue;
        [SerializeField] double pressureKPa;
        [SerializeField] string firmwareState = "";
        [SerializeField] bool freshSample;
        [SerializeField, InspectorName("当前呼吸状态")] string breathState = "未连接";
        BreathSensorClient receiver;
        BreathSensorConnectionKind activeConnectionKind;
        bool activeCsv;
        long lastLoggedSampleSequence = -1;
        string lastDebugState;
        public BreathSensorClient Receiver => receiver;
        public BreathSensorSnapshot ReceiverSnapshot => receiver == null ? default : receiver.Snapshot();
        public string ConnectionLabel => (receiver == null ? connectionKind : activeConnectionKind) == BreathSensorConnectionKind.BluetoothLE ? "BLE" : "USB";
        public bool UsesCsv => receiver == null ? connectionKind == BreathSensorConnectionKind.BluetoothLE
            && bluetoothProtocol == BreathBluetoothProtocol.CsvNotifications : activeCsv;
        public bool ThresholdsValid => UsesCsv ? BreathSensorProtocol.AreCenteredThresholdsValid(bluetoothCenterMv, bluetoothDeviationMv)
            : BreathSensorProtocol.AreThresholdsValid(inhaleMaximum, exhaleMinimum);
        public string DecisionLabel {
            get {
                var state = ReceiverSnapshot;
                var lane = state.IsFresh(sampleTimeoutSeconds) && ThresholdsValid ? Resolve(state) : BreathLane.Middle;
                return lane == BreathLane.Upper ? "吸气" : lane == BreathLane.Lower ? "呼气" : "默认";
            }
        }
        BreathLane Resolve(BreathSensorSnapshot state) => UsesCsv
            ? BreathSensorProtocol.ResolveCentered(state.FilteredValue, bluetoothCenterMv, bluetoothDeviationMv)
            : BreathSensorProtocol.Resolve(state.Value, inhaleMaximum, exhaleMinimum);

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
                activeCsv = bluetooth && bluetoothProtocol == BreathBluetoothProtocol.CsvNotifications;
                if (suppliedClient != null) receiver = suppliedClient;
                else if (bluetooth) {
                    WindowsBleTransport.ValidateProfile(bluetoothServiceUuid, bluetoothNotifyUuid, activeCsv ? "" : bluetoothWriteUuid);
                    WindowsBleTransport.ParseAddress(bluetoothAddress);
                    // Freeze Inspector configuration for this connection; worker threads never read Unity fields.
                    string name = bluetoothDeviceName, address = bluetoothAddress, service = bluetoothServiceUuid,
                        notify = bluetoothNotifyUuid, write = activeCsv ? "" : bluetoothWriteUuid;
                    receiver = new BreathSensorClient(new BreathSensorConnectionOptions("", bluetoothConnectTimeoutSeconds,
                        reconnectDelaySeconds, inactivityTimeoutSeconds, activeCsv, bluetoothFilterSamples), () => new[] { "BLE: " + name },
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
                return Resolve(state);
            // A recognized sensor with stale data is neutral, even while a keyboard key is held.
            if (receiver != null && state.Connected) return BreathLane.Middle;
            return keyboardWhenDisconnected && keyboardFallback != null ? keyboardFallback.ReadLane() : BreathLane.Middle;
        }

        void Update() {
            if (receiver == null) return;
            var state = receiver.Snapshot();
            connectionStatus = state.Status + ": " + state.Message; connectedPort = state.Port;
            latestValue = state.Value; freshSample = state.IsFresh(sampleTimeoutSeconds);
            filteredValue = state.FilteredValue; pressureKPa = state.PressureKPa; firmwareState = state.FirmwareState;
            bool validThresholds = ThresholdsValid;
            BreathLane lane = Resolve(state);
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
                string details = UsesCsv ? FormattableString.Invariant($"均值={state.FilteredValue:0.###} mV | 压差={state.PressureKPa:0.###} kPa | 固件={state.FirmwareState} | 吸气<{bluetoothCenterMv - bluetoothDeviationMv:0.###}，呼气>{bluetoothCenterMv + bluetoothDeviationMv:0.###}")
                    : FormattableString.Invariant($"吸气≤{inhaleMaximum:0.###}，呼气≥{exhaleMinimum:0.###}");
                Debug.Log(FormattableString.Invariant($"[WingSinger Arduino] {ConnectionLabel} {state.Port} | 数值={state.Value:0.###} mV | 状态={breathState} | {details}"), this);
                lastLoggedSampleSequence = state.SampleSequence;
            } else if (!freshSample && breathState != lastDebugState) {
                Debug.Log($"[WingSinger Arduino] 状态={breathState} | {connectionStatus}", this);
            }
            lastDebugState = breathState;
        }

        public void Disconnect() {
            receiver?.Dispose(); receiver = null;
            connectionStatus = "Stopped"; connectedPort = ""; latestValue = 0; freshSample = false;
            filteredValue = pressureKPa = 0; firmwareState = "";
            breathState = "未连接"; lastLoggedSampleSequence = -1; lastDebugState = null;
        }
        void OnDisable() => Disconnect();
        void OnApplicationQuit() => Disconnect();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
