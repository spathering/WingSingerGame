using System;
using System.IO;
using WingSinger;

static class SensorConfigurationTests {
    public static int Run() {
        int checks = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks++; }
        var defaults = new BreathSensorSettings();
        var sensor = new ArduinoBreathInput { settings = defaults, loadSavedConfiguration = false };
        sensor.bluetoothCenterMv = 1200;
        Check(defaults.defaults.bluetoothCenterMv == 1300 && sensor.Configuration.bluetoothCenterMv == 1200,
            "Runtime edits never mutate shared SO defaults");
        var next = sensor.Configuration.Copy(); next.bluetoothCenterMv = 1250;
        Check(sensor.TryApplyConfiguration(next, false, out _), "Valid runtime configuration applies");
        next.bluetoothCenterMv = 900;
        Check(sensor.Configuration.bluetoothCenterMv == 1250, "Applied configuration owns a copy");
        string path = Path.Combine(Path.GetTempPath(), "WingSingerConfigTest-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            sensor.ConfigurationFileOverride = path;
            Check(sensor.TryApplyConfiguration(sensor.Configuration.Copy(), true, out _), "Hardware settings save as standalone JSON");
            Check(BreathSensorConfigFile.TryLoad(path, out var restored, out _) && restored.bluetoothCenterMv == 1250,
                "Standalone JSON restores numeric mapping configuration");
            var fresh = new ArduinoBreathInput { settings = defaults, ConfigurationFileOverride = path };
            Check(fresh.Configuration.bluetoothCenterMv == 1250, "New component reloads saved hardware settings");
            var invalid = restored.Copy(); invalid.bluetoothDeviationMv = -1;
            string previous = File.ReadAllText(path);
            Check(!sensor.TryApplyConfiguration(invalid, true, out _) && File.ReadAllText(path) == previous && sensor.bluetoothDeviationMv == 100,
                "Invalid edits cannot overwrite live settings or saved file");
            invalid = restored.Copy(); invalid.bluetoothFilterSamples = 0;
            Check(!invalid.TryValidate(out _), "Invalid smoothing window rejected");
            invalid = restored.Copy(); invalid.inactivityTimeoutSeconds = .1f;
            Check(!invalid.TryValidate(out _), "Inactivity cannot be shorter than freshness");
            invalid = restored.Copy(); invalid.bluetoothDeviceName = "";
            Check(!invalid.TryValidate(out _), "Notify-only settings require a name or address filter");
            Check(sensor.TryApplyConfiguration(sensor.DefaultConfiguration(), true, out _) && sensor.bluetoothCenterMv == 1300,
                "Restoring defaults persists the original mapping");
            File.WriteAllText(path, "{ broken json");
            Check(!BreathSensorConfigFile.TryLoad(path, out _, out _), "Corrupt settings file rejected safely");
            var fallback = new ArduinoBreathInput { settings = defaults, ConfigurationFileOverride = path };
            Check(fallback.Configuration.bluetoothCenterMv == 1300, "Corrupt file falls back to project defaults");
            File.WriteAllText(path, "{\"version\":99,\"configuration\":{}}");
            Check(!BreathSensorConfigFile.TryLoad(path, out _, out _), "Unknown configuration versions rejected");
        } finally { File.Delete(path); File.Delete(path + ".tmp"); }
        return checks;
    }
}
