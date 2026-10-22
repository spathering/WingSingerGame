using System;
using UnityEngine;

namespace WingSinger {
    public sealed class ArduinoBreathInput : BreathInput {
        [Header("115200 baud / 8N1 / newline-delimited numbers")]
        [Tooltip("Leave empty to scan serial ports and identify the sensor with the WingSinger handshake.")]
        public string portOverride = "";
        public float inhaleMaximum = 400;
        public float exhaleMinimum = 1000;
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
        BreathSensorClient receiver;
        public BreathSensorClient Receiver => receiver;
        public BreathSensorSnapshot ReceiverSnapshot => receiver == null ? default : receiver.Snapshot();

        void OnEnable() { if (Application.isPlaying) Connect(); }

        // A supplied client also allows deterministic receiver/scene tests without a physical board.
        public void Connect(BreathSensorClient suppliedClient = null) {
            Disconnect();
            if (!Application.isPlaying) return;
            try {
                if (!Finite(inhaleMaximum) || !Finite(exhaleMinimum) || inhaleMaximum >= exhaleMinimum
                    || !Finite(sampleTimeoutSeconds) || sampleTimeoutSeconds <= 0 || inactivityTimeoutSeconds < sampleTimeoutSeconds)
                    throw new ArgumentException("Sensor thresholds/timeouts are invalid.");
                if (suppliedClient == null && !WindowsSerialTransport.IsSupported) {
                    connectionStatus = "Serial input supports Windows; keyboard fallback is available.";
                    return;
                }
                receiver = suppliedClient ?? new BreathSensorClient(new BreathSensorConnectionOptions(portOverride,
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
        }

        public void Disconnect() {
            receiver?.Dispose(); receiver = null;
            connectionStatus = "Stopped"; connectedPort = ""; latestValue = 0; freshSample = false;
        }
        void OnDisable() => Disconnect();
        void OnApplicationQuit() => Disconnect();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
