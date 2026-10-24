#include <Arduino.h>
#include <math.h>
#include <string.h>

// ESP32-C3 SuperMini: enable Tools > USB CDC On Boot in Arduino IDE.
// Send the GPIO0 ADC voltage in mV, without undoing the voltage divider.
constexpr uint8_t SENSOR_PIN = 0;
constexpr unsigned long BAUD_RATE = 115200;
constexpr unsigned long SAMPLE_INTERVAL_MS = 10;
constexpr unsigned long SEND_INTERVAL_MS = 100;
constexpr float DIVIDER_RATIO = 2.0f;
constexpr float MV_PER_KPA = 1000.0f;  // Nominal relative estimate, not calibrated.
constexpr float FILTER_ALPHA = 0.25f;
constexpr float ENTER_THRESHOLD_KPA = 0.08f;
constexpr float EXIT_THRESHOLD_KPA = 0.03f;
constexpr bool INVERT_DIRECTION = false;
constexpr unsigned long STARTUP_SETTLE_MS = 1500;
constexpr unsigned long CALIBRATION_INTERVAL_MS = 20;
constexpr uint8_t CALIBRATION_SAMPLES = 10;
constexpr char HELLO[] = "WINGSINGER_HELLO";
constexpr char IDENTITY[] = "WINGSINGER_BREATH_V1";
constexpr size_t MAX_COMMAND_LENGTH = 64;

char command[MAX_COMMAND_LENGTH + 1];
size_t commandLength = 0;
bool discardCommand = false;
bool streaming = false;
uint32_t adcMv = 0;
unsigned long lastSample = 0;
unsigned long lastSend = 0;
float sensorMv = 0.0f;
float baselineMv = 0.0f;
float filteredMv = 0.0f;
float deltaKPa = 0.0f;
const char* currentState = "REST";
bool calibrated = false;
uint8_t calibrationCount = 0;
float calibrationSum = 0.0f;
unsigned long calibrationStarted = 0;
unsigned long calibrationSettleMs = 0;
unsigned long lastCalibrationSample = 0;

void startCalibration(unsigned long settleMs) {
  calibrated = false;
  calibrationCount = 0;
  calibrationSum = 0.0f;
  baselineMv = filteredMv = deltaKPa = 0.0f;
  currentState = "REST";
  calibrationStarted = lastCalibrationSample = millis();
  calibrationSettleMs = settleMs;
}

void updateCalibration(unsigned long now) {
  if (calibrated || now - calibrationStarted < calibrationSettleMs ||
      now - lastCalibrationSample < CALIBRATION_INTERVAL_MS) return;
  lastCalibrationSample = now;
  calibrationSum += sensorMv;
  if (++calibrationCount == CALIBRATION_SAMPLES) {
    baselineMv = calibrationSum / CALIBRATION_SAMPLES;
    filteredMv = baselineMv;
    deltaKPa = 0.0f;
    currentState = "REST";
    calibrated = true;
    lastSend = now;
  }
}

const char* classifyBreathing(float dpKPa) {
  // Preserve the original enter/exit hysteresis and direction mapping.
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

void printStatus() {
  // Explicit diagnostic reply; Unity's numeric parser ignores this text line.
  Serial.print("WINGSINGER_STATUS,time_ms="); Serial.print(millis());
  Serial.print(",adc_mV="); Serial.print(adcMv);
  Serial.print(",sensor_mV="); Serial.print(sensorMv, 1);
  Serial.print(",baseline_mV="); Serial.print(baselineMv, 1);
  Serial.print(",filtered_mV="); Serial.print(filteredMv, 1);
  Serial.print(",delta_kPa="); Serial.print(deltaKPa, 3);
  Serial.print(",state="); Serial.println(calibrated ? currentState : "CALIBRATING");
}

void receiveCommands() {
  // Bound the work so serial noise cannot starve ADC sampling.
  for (size_t count = 0; count < 64 && Serial.available() > 0; ++count) {
    const int value = Serial.read();
    if (value < 0) break;

    if (value == '\n') {
      if (!discardCommand) {
        // Accept both LF and CRLF. Only a final CR is removed.
        if (commandLength > 0 && command[commandLength - 1] == '\r') {
          --commandLength;
        }
        command[commandLength] = '\0';
        if (strcmp(command, HELLO) == 0) {
          // Reply on every HELLO so Unity can reconnect without a board reset.
          Serial.println(IDENTITY);
          streaming = true;
          lastSend = millis();
        } else if (strcmp(command, "WINGSINGER_STATUS") == 0) {
          printStatus();
        } else if (strcmp(command, "WINGSINGER_CALIBRATE") == 0) {
          startCalibration(0);
          Serial.println("WINGSINGER_CALIBRATING");
        }
      }
      commandLength = 0;
      discardCommand = false;
    } else if (!discardCommand) {
      if (commandLength >= MAX_COMMAND_LENGTH || value > 126 ||
          (value < 32 && value != '\r')) {
        discardCommand = true;
        commandLength = 0;
      } else {
        command[commandLength++] = static_cast<char>(value);
      }
    }
  }
}

void setup() {
  Serial.begin(BAUD_RATE);
  analogReadResolution(12);
  analogSetPinAttenuation(SENSOR_PIN, ADC_11db);
  adcMv = analogReadMilliVolts(SENSOR_PIN);
  sensorMv = static_cast<float>(adcMv) * DIVIDER_RATIO;
  lastSample = millis();
  lastSend = lastSample;
  startCalibration(STARTUP_SETTLE_MS);
  // Calibration is scheduled without delay(), so HELLO can be answered now.
  // No titles or CSV are mixed into the default Unity numeric stream.
}

void loop() {
  receiveCommands();
  const unsigned long now = millis();

  // Unsigned subtraction also works across millis() wraparound.
  if (now - lastSample >= SAMPLE_INTERVAL_MS) {
    lastSample = now;
    adcMv = analogReadMilliVolts(SENSOR_PIN);
    sensorMv = static_cast<float>(adcMv) * DIVIDER_RATIO;
    if (calibrated) {
      filteredMv += FILTER_ALPHA * (sensorMv - filteredMv);
      deltaKPa = (filteredMv - baselineMv) / MV_PER_KPA;
      classifyBreathing(deltaKPa);
    }
  }
  updateCalibration(now);

  if (streaming && calibrated && now - lastSend >= SEND_INTERVAL_MS) {
    lastSend = now;
    // Always transmit, including when the value has not changed.
    // Keep the original raw adcMv output. Firmware pressure classification and
    // Unity's configurable ADC thresholds are distinct; do not substitute one.
    Serial.println(adcMv);
  }
}
