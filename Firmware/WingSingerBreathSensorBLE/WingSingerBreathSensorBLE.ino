// ESP32-C3 BLE variant. Reuses the proven ADC/calibration/USB diagnostics unchanged.
// Arduino-ESP32 3.x; ESP32C3 Dev Module, USB CDC On Boot = Enabled.
#include <atomic>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

#define setup setupUsbSensor
#define loop loopUsbSensor
#include "../WingSingerBreathSensor/WingSingerBreathSensor.ino"
#undef setup
#undef loop

constexpr char BLE_NAME[] = "WingSinger Breath";
constexpr char BLE_SERVICE[] = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
constexpr char BLE_RX[] = "6e400002-b5a3-f393-e0a9-e50e24dcca9e"; // Unity writes commands.
constexpr char BLE_TX[] = "6e400003-b5a3-f393-e0a9-e50e24dcca9e"; // Unity subscribes to notifications.
std::atomic<bool> bleConnected{false}, bleHello{false}, bleReset{false};
BLECharacteristic* bleTx = nullptr;
bool bleStreaming = false, bleWasConnected = false;
unsigned long lastBleSend = 0, bleDisconnectedAt = 0;

class BreathBleServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer*) override {
    bleReset = true;
    bleConnected = true;
  }
  void onDisconnect(BLEServer*) override {
    bleConnected = false;
    bleHello = false;
    bleReset = true;
  }
};

class BreathBleCommands : public BLECharacteristicCallbacks {
  char line[MAX_COMMAND_LENGTH + 1]{};
  size_t length = 0;
  bool discard = false;
  void onWrite(BLECharacteristic* characteristic) override {
    const String bytes = characteristic->getValue();
    for (size_t i = 0; i < bytes.length(); ++i) {
      const unsigned char c = bytes[i];
      if (c == '\n') {
        if (!discard) {
          if (length && line[length - 1] == '\r') --length;
          line[length] = '\0';
          if (strcmp(line, HELLO) == 0 && bleConnected) bleHello = true;
        }
        length = 0; discard = false;
      } else if (!discard) {
        if (length >= MAX_COMMAND_LENGTH || c > 126 || (c < 32 && c != '\r')) {
          length = 0; discard = true;
        } else line[length++] = static_cast<char>(c);
      }
    }
  }
};

void notifyBle(const char* line) {
  // Default ATT MTU 23 leaves 20 payload bytes. The 21-byte identity spans two notifications.
  // Unity's shared line decoder rejoins arbitrary fragments before interpreting them.
  size_t remaining = strlen(line);
  while (remaining && bleConnected) {
    const size_t count = remaining > 20 ? 20 : remaining;
    bleTx->setValue(reinterpret_cast<const uint8_t*>(line), count);
    bleTx->notify();
    line += count; remaining -= count;
  }
}

void setup() {
  setupUsbSensor();
  BLEDevice::init(BLE_NAME);
  auto* server = BLEDevice::createServer();
  server->setCallbacks(new BreathBleServerCallbacks());
  auto* service = server->createService(BLE_SERVICE);
  bleTx = service->createCharacteristic(BLE_TX, BLECharacteristic::PROPERTY_NOTIFY);
  bleTx->addDescriptor(new BLE2902());
  auto* rx = service->createCharacteristic(BLE_RX, BLECharacteristic::PROPERTY_WRITE);
  rx->setCallbacks(new BreathBleCommands());
  service->start();
  auto* advertising = BLEDevice::getAdvertising();
  advertising->addServiceUUID(BLE_SERVICE);
  // Put the name in the scan response; the 128-bit service UUID occupies the advertisement.
  advertising->setScanResponse(true);
  advertising->start();
}

void loop() {
  loopUsbSensor();
  const unsigned long now = millis();
  if (bleReset.exchange(false)) bleStreaming = false;
  const bool connected = bleConnected.load();
  if (connected) {
    bleWasConnected = true;
    if (bleHello.exchange(false)) {
      // Do not depend on MTU negotiation; notifyBle fragments the identity if necessary.
      notifyBle("WINGSINGER_BREATH_V1\n");
      bleStreaming = true;
      lastBleSend = now;
    }
    if (bleStreaming && calibrated && now - lastBleSend >= SEND_INTERVAL_MS) {
      lastBleSend = now;
      char payload[16];
      snprintf(payload, sizeof(payload), "%lu\n", static_cast<unsigned long>(adcMv));
      notifyBle(payload);
    }
  } else {
    bleStreaming = false;
    if (bleWasConnected) { bleWasConnected = false; bleDisconnectedAt = now; }
    if (bleDisconnectedAt && now - bleDisconnectedAt >= 200) {
      BLEDevice::startAdvertising();
      bleDisconnectedAt = 0;
    }
  }
}
