#pragma once
#include <cstddef>
#include <cstdint>
#include <iomanip>
#include <sstream>
#include <string>

// Host-only adapter for executing the actual .ino in deterministic tests.
extern unsigned long testMillis;
extern uint32_t testAdcMv;
extern unsigned int testAdcReads;
constexpr int ADC_11db = 3;
inline unsigned long millis() { return testMillis; }
inline uint32_t analogReadMilliVolts(uint8_t) { ++testAdcReads; return testAdcMv; }
inline void analogReadResolution(int) { }
inline void analogSetPinAttenuation(uint8_t, int) { }

struct TestSerial {
  std::string input, output;
  size_t cursor = 0;
  unsigned long baud = 0;
  void begin(unsigned long value) { baud = value; }
  int available() const { return static_cast<int>(input.size() - cursor); }
  int read() { return available() ? static_cast<unsigned char>(input[cursor++]) : -1; }
  template<class T> void print(T value) { std::ostringstream text; text << value; output += text.str(); }
  void print(float value, int digits) { std::ostringstream text; text << std::fixed << std::setprecision(digits) << value; output += text.str(); }
  template<class T> void println(T value) { print(value); output += "\r\n"; }
  void feed(const std::string& text) { input += text; }
};
extern TestSerial Serial;
