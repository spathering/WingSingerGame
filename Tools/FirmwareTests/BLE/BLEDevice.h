#pragma once
#include "Arduino.h"
#include <vector>
using String = std::string;
class BLEServer;
class BLECharacteristic;
class BLEServerCallbacks {
public:
  virtual ~BLEServerCallbacks() = default;
  virtual void onConnect(BLEServer*) {}
  virtual void onDisconnect(BLEServer*) {}
};
class BLECharacteristicCallbacks {
public:
  virtual ~BLECharacteristicCallbacks() = default;
  virtual void onWrite(BLECharacteristic*) {}
};
class BLE2902 {};
class BLECharacteristic {
public:
  static constexpr uint32_t PROPERTY_NOTIFY = 1, PROPERTY_WRITE = 2;
  String value;
  std::vector<String> notifications;
  BLECharacteristicCallbacks* callbacks = nullptr;
  void addDescriptor(BLE2902*) {}
  void setCallbacks(BLECharacteristicCallbacks* callback) { callbacks = callback; }
  String getValue() const { return value; }
  void setValue(const uint8_t* bytes, size_t length) { value.assign(reinterpret_cast<const char*>(bytes), length); }
  void notify() { notifications.push_back(value); }
  void write(String bytes) { value = bytes; callbacks->onWrite(this); }
};
class BLEService {
public:
  BLECharacteristic tx, rx;
  bool started = false;
  BLECharacteristic* createCharacteristic(const char*, uint32_t properties) { return properties == BLECharacteristic::PROPERTY_NOTIFY ? &tx : &rx; }
  void start() { started = true; }
};
class BLEServer {
public:
  BLEService service;
  BLEServerCallbacks* callbacks = nullptr;
  void setCallbacks(BLEServerCallbacks* callback) { callbacks = callback; }
  BLEService* createService(const char*) { return &service; }
};
class BLEAdvertising {
public:
  int starts = 0;
  bool scanResponse = false;
  String uuid;
  void addServiceUUID(const char* value) { uuid = value; }
  void setScanResponse(bool enabled) { scanResponse = enabled; }
  void start() { starts++; }
};
class BLEDevice {
public:
  inline static BLEServer server;
  inline static BLEAdvertising advertising;
  inline static String name;
  static void init(const char* value) { name = value; }
  static BLEServer* createServer() { return &server; }
  static BLEAdvertising* getAdvertising() { return &advertising; }
  static void startAdvertising() { advertising.start(); }
};
