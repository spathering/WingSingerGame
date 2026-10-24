#include <Arduino.h>
#include <math.h>
#include <string.h>

// ===== Hardware =====
const int SENSOR_PIN = 0;          // ESP32-C3 GPIO0
const float DIVIDER_RATIO = 2.0f;  // Two 10k resistors
const float MV_PER_KPA = 1000.0f; // Nominal estimate, not calibrated

// ===== Sampling and display =====
const unsigned long SAMPLE_INTERVAL_MS = 10; // Sample every 10 ms
const unsigned long PRINT_INTERVAL_MS = 100; // Print every 100 ms
const float FILTER_ALPHA = 0.25f;             // Smoother readings

// ===== Breathing thresholds =====
const float ENTER_THRESHOLD_KPA = 0.08f;
const float EXIT_THRESHOLD_KPA = 0.03f;

// Change to true if inhale/exhale labels are reversed
const bool INVERT_DIRECTION = false;

// ===== State =====
float baselineMv = 0.0f;
float filteredMv = 0.0f;

unsigned long lastSample = 0;
unsigned long lastPrint = 0;

const char* currentState = "REST";

// Read sensor voltage, estimated before the divider
float readSensorMv() {
  return (float)analogReadMilliVolts(SENSOR_PIN)
         * DIVIDER_RATIO;
}

// Calibrate while both ports have equal pressure.
// Do not blow during calibration.
void calibrateBaseline() {
  const int samples = 10;
  float sum = 0.0f;

  Serial.println();
  Serial.println("Calibration: equal pressure, do not blow.");

  for (int i = 0; i < samples; i++) {
    sum += readSensorMv();
    delay(20);
  }

  baselineMv = sum / samples;
  filteredMv = baselineMv;

  Serial.print("Baseline_mV: ");
  Serial.println(baselineMv, 1);
}

// Breathing state with separate enter/exit thresholds
const char* classifyBreathing(float dpKPa) {
  if (strcmp(currentState, "REST") == 0) {
    if (dpKPa >= ENTER_THRESHOLD_KPA) {
      currentState = INVERT_DIRECTION ? "INHALE" : "EXHALE";
    } else if (dpKPa <= -ENTER_THRESHOLD_KPA) {
      currentState = INVERT_DIRECTION ? "EXHALE" : "INHALE";
    }
  } else if (fabsf(dpKPa) <= EXIT_THRESHOLD_KPA) {
    currentState = "REST";
  }

  return currentState;
}

void setup() {
  Serial.begin(115200);
  delay(1500);

  analogReadResolution(12);
  analogSetPinAttenuation(SENSOR_PIN, ADC_11db);

  Serial.println("Breathing sensor test");

  calibrateBaseline();

  Serial.println();
  Serial.println("time_ms,adc_mV,sensor_mV,delta_kPa,state");
}

void loop() {
  unsigned long now = millis();

  // Sample frequently for responsive detection
  if (now - lastSample >= SAMPLE_INTERVAL_MS) {
    lastSample = now;

    uint32_t adcMv = analogReadMilliVolts(SENSOR_PIN);
    float sensorMv = (float)adcMv * DIVIDER_RATIO;

    // Smooth readings
    filteredMv += FILTER_ALPHA * (sensorMv - filteredMv);

    // Estimate pressure change from baseline
    float deltaKPa =
      (filteredMv - baselineMv) / MV_PER_KPA;

    const char* state = classifyBreathing(deltaKPa);

    // Print less frequently for easier reading
    if (now - lastPrint >= PRINT_INTERVAL_MS) {
      lastPrint = now;

     // Serial.print(now);
      //Serial.print(",");
      Serial.print(adcMv);
      Serial.println();
      //Serial.print(filteredMv, 1);
      //Serial.print(",");
      //Serial.print(deltaKPa, 3);
      //Serial.print(",");
      //Serial.println(state);
      delay(100);
    }
  }
}