#include "Arduino.h"
#include <cmath>
#include <iostream>
#include <limits>
#include <stdexcept>

unsigned long testMillis = 0;
uint32_t testAdcMv = 700;
unsigned int testAdcReads = 0;
TestSerial Serial;

#include "../../Firmware/WingSingerBreathSensor/WingSingerBreathSensor.ino"

int checks = 0;
void check(bool ok, const char* message) {
  if (!ok) throw std::runtime_error(message);
  ++checks;
}
void step(unsigned long elapsed = 10) { testMillis += elapsed; loop(); }
void run(unsigned long duration) { for (unsigned long t = 0; t < duration; t += 10) step(); }
bool stateIs(const char* expected) { return strcmp(currentState, expected) == 0; }

int main() {
  try {
    setup();
    check(Serial.baud == 115200 && Serial.output.empty(), "Startup must be quiet at 115200");
    Serial.feed(std::string(65, 'x') + "WINGSINGER_HELLO\n");
    run(100);
    check(!streaming && Serial.output.empty(), "Oversized commands must not leak a valid suffix");
    Serial.feed("WINGSINGER_HE"); step(0);
    check(!streaming, "A partial HELLO must not enable streaming");
    Serial.feed("LLO\r\n"); step(0);
    check(streaming && Serial.output == "WINGSINGER_BREATH_V1\r\n", "HELLO works while startup calibration is pending");
    Serial.output.clear();
    while (!calibrated) step();
    check(calibrationCount == 10 && std::fabs(baselineMv - 1400.0f) < 0.001f, "Baseline averages ten sensor voltages after the divider correction");
    check(filteredMv == baselineMv && deltaKPa == 0 && stateIs("REST"), "Calibration seeds the filter and neutral state");
    check(Serial.output.empty(), "No uncalibrated numeric samples or startup CSV are emitted");
    unsigned int readsBefore = testAdcReads;
    run(100);
    check(testAdcReads - readsBefore == 10, "ADC continues sampling every 10ms independently of serial printing");
    check(Serial.output == "700\r\n", "Unity receives one raw ADC value every 100ms, without doubling or filtering");
    Serial.output.clear();
    testAdcMv = 900; step();
    check(std::fabs(filteredMv - 1500.0f) < 0.001f && std::fabs(deltaKPa - 0.1f) < 0.00001f,
          "Sensor divider ratio, alpha=0.25 and nominal kPa conversion are restored");
    check(stateIs("EXHALE"), "Filtered positive pressure enters exhale");
    Serial.feed("WINGSINGER_STATUS\n"); step(0);
    check(Serial.output.find("adc_mV=900,sensor_mV=1800.0,baseline_mV=1400.0,filtered_mV=1500.0,delta_kPa=0.100,state=EXHALE") != std::string::npos,
          "Status query exposes the original processing values and state");
    currentState = "REST";
    check(strcmp(classifyBreathing(0.079f), "REST") == 0, "Below entry threshold remains neutral");
    check(strcmp(classifyBreathing(0.08f), "EXHALE") == 0, "Exhale includes the entry boundary");
    check(strcmp(classifyBreathing(0.04f), "EXHALE") == 0, "Hysteresis holds exhale above the exit threshold");
    check(strcmp(classifyBreathing(0.03f), "REST") == 0, "Exit threshold includes its boundary");
    check(strcmp(classifyBreathing(-0.08f), "INHALE") == 0, "Negative pressure enters inhale");
    check(strcmp(classifyBreathing(-0.04f), "INHALE") == 0, "Hysteresis also holds inhale");
    check(strcmp(classifyBreathing(0), "REST") == 0, "Zero pressure returns to rest");
    Serial.output.clear();
    Serial.feed("WINGSINGER_CALIBRATE\n"); step(0);
    check(!calibrated && Serial.output == "WINGSINGER_CALIBRATING\r\n", "Manual recalibration is acknowledged without blocking");
    Serial.output.clear();
    Serial.feed("WINGSINGER_HELLO\n"); step(0);
    check(Serial.output == "WINGSINGER_BREATH_V1\r\n" && !calibrated, "Handshake remains responsive during recalibration");
    Serial.output.clear();
    testAdcMv = 800;
    run(190);
    check(!calibrated && Serial.output.empty(), "Numeric output waits for all recalibration samples");
    step();
    check(calibrated && baselineMv == 1600 && filteredMv == 1600 && stateIs("REST"), "Recalibration resets baseline, filter and state together");
    run(100);
    check(Serial.output == "800\r\n", "Numeric streaming resumes automatically after calibration");
    Serial.output.clear();
    Serial.feed("WINGSINGER_HELLO\n"); step(0);
    check(Serial.output == "WINGSINGER_BREATH_V1\r\n" && baselineMv == 1600, "Reconnect HELLO does not silently recalibrate");
    Serial.output.clear();
    testMillis = std::numeric_limits<unsigned long>::max() - 50;
    lastSample = lastSend = testMillis;
    run(100);
    check(Serial.output == "800\r\n", "Sampling and output survive the millis rollover");
    std::cout << "PASS: " << checks << " integrated firmware assertions.\n";
    return 0;
  } catch (const std::exception& error) {
    std::cerr << "FAIL: " << error.what() << "\n";
    return 1;
  }
}
