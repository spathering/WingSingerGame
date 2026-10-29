// First-party Windows x64 BLE backend. No managed callbacks cross a Unity domain reload.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Devices.Bluetooth.h>
#include <winrt/Windows.Devices.Bluetooth.Advertisement.h>
#include <winrt/Windows.Devices.Bluetooth.GenericAttributeProfile.h>
#include <winrt/Windows.Storage.Streams.h>
#include <atomic>
#include <chrono>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Devices::Bluetooth;
using namespace Windows::Devices::Bluetooth::Advertisement;
using namespace Windows::Devices::Bluetooth::GenericAttributeProfile;
using namespace Windows::Storage::Streams;
using namespace std::chrono_literals;

namespace {
struct Session {
    std::atomic<bool> stopping{false};
    std::mutex gate;
    std::thread worker;
    std::deque<std::vector<uint8_t>> packets;
    std::string command, error;
    size_t packetOffset = 0;
    std::wstring name;
    uint64_t addressFilter = 0;
    guid serviceUuid, notifyUuid, writeUuid;

    void Fail(std::string text) { std::lock_guard lock(gate); if (error.empty()) error = std::move(text); }
    void Receive(IBuffer const& buffer) {
        std::vector<uint8_t> bytes(buffer.Length());
        if (bytes.empty()) return;
        if (bytes.size() > 512) { Fail("BLE notification exceeds 512 bytes"); return; }
        DataReader::FromBuffer(buffer).ReadBytes(bytes);
        std::lock_guard lock(gate);
        // Never drop part of a line and accidentally turn its suffix into a valid sample.
        if (packets.size() >= 32) { if (error.empty()) error = "BLE receive overflow"; return; }
        packets.push_back(std::move(bytes));
    }
    template<class Operation> auto Await(Operation const& operation) {
        const auto until = std::chrono::steady_clock::now() + 8s;
        while (operation.Status() == AsyncStatus::Started) {
            if (stopping || std::chrono::steady_clock::now() >= until) {
                operation.Cancel();
                throw std::runtime_error(stopping ? "BLE stopped" : "BLE operation timed out");
            }
            std::this_thread::sleep_for(20ms);
        }
        return operation.GetResults();
    }
};
struct ScanState {
    std::mutex gate;
    struct Seen { bool service = false; std::wstring name; };
    std::map<uint64_t, Seen> devices;
    uint64_t selected = 0;
    BluetoothAddressType type = BluetoothAddressType::Unspecified;
    bool aborted = false;
};

void Run(std::shared_ptr<Session> const& session) {
    BluetoothLEAdvertisementWatcher watcher{nullptr};
    BluetoothLEDevice device{nullptr};
    GattDeviceService service{nullptr};
    GattCharacteristic notify{nullptr};
    event_token changed{};
    bool subscribed = false;
    bool apartment = false;
    try {
        init_apartment(apartment_type::multi_threaded);
        apartment = true;
        const auto scan = std::make_shared<ScanState>();
        const auto wantedService = session->serviceUuid;
        const auto wantedName = session->name;
        const auto wantedAddress = session->addressFilter;
        watcher = BluetoothLEAdvertisementWatcher();
        watcher.ScanningMode(BluetoothLEScanningMode::Active);
        watcher.Received([scan, wantedService, wantedName, wantedAddress](auto const&, BluetoothLEAdvertisementReceivedEventArgs const& args) {
            if (wantedAddress && args.BluetoothAddress() != wantedAddress) return;
            std::lock_guard lock(scan->gate);
            if (scan->devices.size() >= 256 && !scan->devices.count(args.BluetoothAddress())) return;
            auto& seen = scan->devices[args.BluetoothAddress()];
            for (auto const& uuid : args.Advertisement().ServiceUuids()) if (uuid == wantedService) seen.service = true;
            auto const localName = args.Advertisement().LocalName();
            if (!localName.empty()) seen.name = std::wstring(localName);
            if (!scan->selected && seen.service && (wantedName.empty() || seen.name == wantedName)) {
                scan->selected = args.BluetoothAddress(); scan->type = args.BluetoothAddressType();
            }
        });
        watcher.Stopped([scan](auto const&, BluetoothLEAdvertisementWatcherStoppedEventArgs const& args) {
            if (args.Error() != BluetoothError::Success) { std::lock_guard lock(scan->gate); scan->aborted = true; }
        });
        watcher.Start();
        uint64_t address = 0;
        BluetoothAddressType addressType{};
        const auto scanUntil = std::chrono::steady_clock::now() + 8s;
        while (!session->stopping && std::chrono::steady_clock::now() < scanUntil) {
            { std::lock_guard lock(scan->gate);
              if (scan->aborted) throw std::runtime_error("BLE scanner unavailable: turn on Bluetooth and check the adapter");
              address = scan->selected; addressType = scan->type; }
            if (address) break;
            std::this_thread::sleep_for(20ms);
        }
        watcher.Stop(); watcher = nullptr;
        if (!address) throw std::runtime_error("BLE sensor not found: check power, advertised name/service, and other connected apps");
        if (session->stopping) throw std::runtime_error("BLE stopped");
        device = session->Await(BluetoothLEDevice::FromBluetoothAddressAsync(address, addressType));
        if (!device) throw std::runtime_error("BLE device access denied or unavailable");
        auto services = session->Await(device.GetGattServicesForUuidAsync(session->serviceUuid, BluetoothCacheMode::Uncached));
        if (services.Status() != GattCommunicationStatus::Success || services.Services().Size() != 1)
            throw std::runtime_error("BLE service unavailable: verify firmware and Service UUID");
        service = services.Services().GetAt(0);
        auto characteristics = session->Await(service.GetCharacteristicsAsync(BluetoothCacheMode::Uncached));
        if (characteristics.Status() != GattCommunicationStatus::Success) throw std::runtime_error("BLE characteristic discovery failed");
        GattCharacteristic write{nullptr};
        for (auto const& characteristic : characteristics.Characteristics()) {
            if (characteristic.Uuid() == session->notifyUuid) notify = characteristic;
            if (characteristic.Uuid() == session->writeUuid) write = characteristic;
        }
        if (!notify || !write || (notify.CharacteristicProperties() & GattCharacteristicProperties::Notify) == GattCharacteristicProperties::None
            || (write.CharacteristicProperties() & GattCharacteristicProperties::Write) == GattCharacteristicProperties::None)
            throw std::runtime_error("BLE needs a Notify characteristic and a Write-with-response characteristic");
        std::weak_ptr<Session> weak = session;
        changed = notify.ValueChanged([weak](auto const&, GattValueChangedEventArgs const& args) {
            if (auto state = weak.lock(); state && !state->stopping) {
                try { state->Receive(args.CharacteristicValue()); }
                catch (...) { state->Fail("BLE notification decode failed"); }
            }
        });
        subscribed = true;
        if (session->Await(notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue::Notify))
            != GattCommunicationStatus::Success) throw std::runtime_error("BLE notification subscription failed");
        while (!session->stopping) {
            std::string command;
            { std::lock_guard lock(session->gate); if (!session->error.empty()) break; command.swap(session->command); }
            if (!command.empty()) {
                DataWriter writer;
                writer.WriteBytes(array_view<uint8_t const>(reinterpret_cast<uint8_t const*>(command.data()),
                    reinterpret_cast<uint8_t const*>(command.data()) + command.size()));
                auto result = session->Await(write.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption::WriteWithResponse));
                if (result.Status() != GattCommunicationStatus::Success) throw std::runtime_error("BLE command write failed");
            }
            if (device.ConnectionStatus() == BluetoothConnectionStatus::Disconnected) throw std::runtime_error("BLE sensor disconnected");
            std::this_thread::sleep_for(20ms);
        }
    } catch (hresult_error const& error) { session->Fail("Windows BLE: " + to_string(error.message())); }
      catch (std::exception const& error) { session->Fail(error.what()); }
      catch (...) { session->Fail("Unexpected Windows BLE error"); }
    try { if (watcher) watcher.Stop(); } catch (...) {}
    try { if (subscribed) notify.ValueChanged(changed); } catch (...) {}
    // Close the service/device instead of awaiting another radio operation during shutdown.
    try { if (service) service.Close(); } catch (...) {}
    try { if (device) device.Close(); } catch (...) {}
    notify = nullptr; service = nullptr; device = nullptr; watcher = nullptr;
    if (apartment) uninit_apartment();
}
using Handle = std::shared_ptr<Session>;
}

extern "C" __declspec(dllexport) void* __cdecl WS_BleCreate(char const* name, uint64_t address,
    char const* service, char const* notify, char const* write) noexcept {
    try {
        auto state = std::make_shared<Session>();
        state->name = std::wstring(to_hstring(name)); state->addressFilter = address;
        state->serviceUuid = guid(to_hstring(service)); state->notifyUuid = guid(to_hstring(notify)); state->writeUuid = guid(to_hstring(write));
        auto handle = std::make_unique<Handle>(state);
        state->worker = std::thread([state] { Run(state); });
        return handle.release();
    } catch (...) { return nullptr; }
}
extern "C" __declspec(dllexport) int __cdecl WS_BleRead(void* handle, uint8_t* buffer, int capacity) noexcept {
    auto const& state = *static_cast<Handle*>(handle);
    std::lock_guard lock(state->gate);
    if (!state->error.empty()) return -1;
    int count = 0;
    while (count < capacity && !state->packets.empty()) {
        auto const& packet = state->packets.front();
        while (count < capacity && state->packetOffset < packet.size()) buffer[count++] = packet[state->packetOffset++];
        if (state->packetOffset == packet.size()) { state->packets.pop_front(); state->packetOffset = 0; }
    }
    return count;
}
extern "C" __declspec(dllexport) int __cdecl WS_BleWrite(void* handle, char const* command) noexcept {
    auto const& state = *static_cast<Handle*>(handle);
    std::lock_guard lock(state->gate);
    if (!state->error.empty() || state->stopping) return -1;
    state->command = command; // Only the latest repeated HELLO is needed before discovery finishes.
    return 0;
}
extern "C" __declspec(dllexport) void __cdecl WS_BleError(void* handle, char* text, int capacity) noexcept {
    auto const& state = *static_cast<Handle*>(handle);
    std::lock_guard lock(state->gate);
    if (capacity > 0) strncpy_s(text, capacity, state->error.c_str(), _TRUNCATE);
}
extern "C" __declspec(dllexport) void __cdecl WS_BleStop(void* handle) noexcept {
    (*static_cast<Handle*>(handle))->stopping = true;
}
extern "C" __declspec(dllexport) void __cdecl WS_BleDestroy(void* handle) noexcept {
    std::unique_ptr<Handle> holder(static_cast<Handle*>(handle));
    (*holder)->stopping = true;
    if ((*holder)->worker.joinable()) (*holder)->worker.join();
}
