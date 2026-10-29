using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger.UI {
    public sealed class HardwareSettingsPanel : MonoBehaviour {
        public TMP_Text hardwareInfo, connectionInfo, readings, message;
        public TMP_InputField deviceName, address, center, deviation, filterSamples, freshness, reconnectDelay,
            inactivity, inhale, exhale, port, handshake, connectTimeout;
        public TMP_Dropdown transport, protocol;
        public Toggle debugLogging, keyboardFallback;
        public Button apply, reconnect, restoreDefaults;
        BreathUIFlowController flow;
        float refreshAt;
        ArduinoBreathInput Sensor => flow != null ? flow.sensorInput : null;

        void OnEnable() {
            flow = GetComponentInParent<BreathUIFlowController>();
            apply.onClick.AddListener(Apply); reconnect.onClick.AddListener(Reconnect); restoreDefaults.onClick.AddListener(RestoreDefaults);
            Populate(); Present();
        }
        void OnDisable() {
            apply.onClick.RemoveListener(Apply); reconnect.onClick.RemoveListener(Reconnect); restoreDefaults.onClick.RemoveListener(RestoreDefaults);
        }
        void Update() { if (Time.unscaledTime >= refreshAt) { Present(); refreshAt = Time.unscaledTime + .1f; } }
        static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        public void Populate() {
            var c = Sensor != null ? Sensor.Configuration : new BreathSensorConfiguration();
            deviceName.text = c.bluetoothDeviceName; address.text = c.bluetoothAddress;
            center.text = Number(c.bluetoothCenterMv); deviation.text = Number(c.bluetoothDeviationMv);
            filterSamples.text = c.bluetoothFilterSamples.ToString(CultureInfo.InvariantCulture);
            freshness.text = Number(c.sampleTimeoutSeconds); reconnectDelay.text = Number(c.reconnectDelaySeconds);
            inactivity.text = Number(c.inactivityTimeoutSeconds); inhale.text = Number(c.inhaleMaximum); exhale.text = Number(c.exhaleMinimum);
            port.text = c.portOverride; handshake.text = Number(c.handshakeTimeoutSeconds); connectTimeout.text = Number(c.bluetoothConnectTimeoutSeconds);
            transport.SetValueWithoutNotify((int)c.connectionKind); protocol.SetValueWithoutNotify((int)c.bluetoothProtocol);
            debugLogging.SetIsOnWithoutNotify(c.debugLogging); keyboardFallback.SetIsOnWithoutNotify(c.keyboardWhenDisconnected);
            message.text = "Edit parameters, then Apply & save. Center / deviation changes keep the current connection.";
        }
        public void Present() {
            var sensor = Sensor;
            apply.interactable = reconnect.interactable = restoreDefaults.interactable = sensor != null;
            if (sensor == null) { connectionInfo.text = "Preview - open BreathDemo for live hardware."; return; }
            var c = sensor.Configuration; var s = sensor.ReceiverSnapshot;
            hardwareInfo.text = c.bluetoothDeviceName + "\n" + sensor.ConnectionLabel + " / " + (sensor.UsesCsv ? "CSV notifications" : "Handshake UART")
                + "\nAddress: " + (string.IsNullOrEmpty(c.bluetoothAddress) ? "Automatic" : c.bluetoothAddress)
                + "\n\nService UUID\n" + c.bluetoothServiceUuid + "\nNotify UUID\n" + c.bluetoothNotifyUuid
                + (sensor.UsesCsv ? "\nNotify only - no write / handshake" : "\nWrite UUID\n" + c.bluetoothWriteUuid);
            connectionInfo.text = s.Status + "\n" + (s.Message ?? "Stopped") + "\n" + (s.Port ?? "")
                + "\n" + (s.IsFresh(c.sampleTimeoutSeconds) ? "Receiving fresh samples" : s.Connected ? "Sample stale - neutral input" : "Waiting for controller");
            readings.text = s.IsFresh(c.sampleTimeoutSeconds)
                ? FormattableString.Invariant($"ADC {s.Value:0.###} mV\nAverage {s.FilteredValue:0.###} mV\nJudgement: {sensor.DecisionLabel}\nPressure {s.PressureKPa:0.###} kPa\nFirmware: {s.FirmwareState}")
                : "ADC -- mV\nAverage -- mV\nJudgement: 默认\nNo fresh hardware sample";
        }
        static float Parse(TMP_InputField field, string label) {
            if (!float.TryParse(field.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException(label + " must be a finite number.");
            return value;
        }
        public void Apply() {
            if (Sensor == null) return;
            try {
                var c = Sensor.Configuration.Copy();
                c.connectionKind = (BreathSensorConnectionKind)transport.value; c.bluetoothProtocol = (BreathBluetoothProtocol)protocol.value;
                c.bluetoothDeviceName = deviceName.text.Trim(); c.bluetoothAddress = address.text.Trim();
                c.bluetoothCenterMv = Parse(center, "Center"); c.bluetoothDeviationMv = Parse(deviation, "Deviation");
                if (!int.TryParse(filterSamples.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out c.bluetoothFilterSamples))
                    throw new ArgumentException("Filter samples must be a whole number from 1 to 32.");
                c.sampleTimeoutSeconds = Parse(freshness, "Freshness"); c.reconnectDelaySeconds = Parse(reconnectDelay, "Reconnect delay");
                c.inactivityTimeoutSeconds = Parse(inactivity, "Inactivity"); c.bluetoothConnectTimeoutSeconds = Parse(connectTimeout, "BLE wait");
                c.inhaleMaximum = Parse(inhale, "USB inhale"); c.exhaleMinimum = Parse(exhale, "USB exhale");
                c.portOverride = port.text.Trim(); c.handshakeTimeoutSeconds = Parse(handshake, "USB handshake");
                c.debugLogging = debugLogging.isOn; c.keyboardWhenDisconnected = keyboardFallback.isOn;
                if (!Sensor.TryApplyConfiguration(c, true, out string error)) throw new ArgumentException(error);
                message.text = "Saved locally. Connection parameters reconnect once; mapping parameters apply immediately.";
                Present();
            } catch (ArgumentException error) { message.text = error.Message; }
        }
        public void Reconnect() {
            if (flow == null) return;
            flow.router.Route(UIRouteAction.CheckConnection);
            message.text = "Reconnecting with saved parameters. Apply pending edits first."; Present();
        }
        public void RestoreDefaults() {
            if (Sensor == null) return;
            if (!Sensor.TryApplyConfiguration(Sensor.DefaultConfiguration(), true, out string error)) { message.text = error; return; }
            Populate(); message.text = "Project defaults restored and saved locally."; Present();
        }
    }
}
