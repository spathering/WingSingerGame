#include "Arduino.h"
#include <iostream>
#include <stdexcept>
unsigned long testMillis = 0;
uint32_t testAdcMv = 700;
unsigned int testAdcReads = 0;
TestSerial Serial;
#include "../../Firmware/WingSingerBreathSensorBLE/WingSingerBreathSensorBLE.ino"
int checks = 0;
void check(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); ++checks; }
void step(unsigned long elapsed = 10) { testMillis += elapsed; loop(); }
void run(unsigned long duration) { for (unsigned long t = 0; t < duration; t += 10) step(); }
int main() {
  try {
    setup();
    auto& server = BLEDevice::server;
    auto& tx = server.service.tx;
    auto& rx = server.service.rx;
    check(BLEDevice::name == "WingSinger Breath" && BLEDevice::advertising.uuid == BLE_SERVICE, "Advertise agreed name/service");
    check(BLEDevice::advertising.scanResponse && server.service.started, "Scan response and service started");
    run(2000);
    check(calibrated && baselineMv == 1400 && tx.notifications.empty(), "Original calibration runs but BLE waits for identity request");
    server.callbacks->onConnect(&server); step();
    rx.write("WINGSINGER_HE"); step();
    check(!bleStreaming, "Fragmented command cannot start stream early");
    rx.write("LLO\r\n"); step();
    check(tx.notifications.size() == 2 && tx.notifications[0] + tx.notifications[1] == "WINGSINGER_BREATH_V1\n"
          && tx.notifications[0].size() <= 20 && tx.notifications[1].size() <= 20, "Identity fragments fit the default BLE MTU");
    size_t count = tx.notifications.size(); run(100);
    check(tx.notifications.size() == count + 1 && tx.notifications.back() == "700\n", "BLE sends raw ADC, not divider corrected or filtered data");
    run(100); check(tx.notifications.back() == "700\n" && tx.notifications.size() == count + 2, "Unchanged ADC still produces fresh notifications");
    testAdcMv = 400; run(100); check(tx.notifications.back() == "400\n", "Inhale boundary sent unchanged");
    testAdcMv = 1000; run(100); check(tx.notifications.back() == "1000\n", "Exhale boundary sent unchanged");
    check(!streaming && Serial.output.empty(), "BLE handshake does not enable or pollute USB stream");
    server.callbacks->onDisconnect(&server); step(); count = tx.notifications.size(); run(250);
    check(!bleStreaming && tx.notifications.size() == count && BLEDevice::advertising.starts == 2, "Disconnect stops notifications and restarts advertising");
    server.callbacks->onConnect(&server); step();
    rx.write(std::string(65, 'x') + "WINGSINGER_HELLO\n"); step();
    check(!bleStreaming, "Oversized BLE command cannot leak a valid suffix");
    rx.write("WINGSINGER_HELLO\n"); step();
    check(bleStreaming && baselineMv == 1400, "Reconnect requires handshake and preserves calibration");
    Serial.feed("WINGSINGER_HELLO\n"); step();
    check(streaming && Serial.output == "WINGSINGER_BREATH_V1\r\n", "USB diagnostic protocol remains available");
    std::cout << "PASS: " << checks << " BLE firmware host assertions (mock radio; not ESP32 build or physical test).\n";
    return 0;
  } catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << '\n'; return 1; }
}
