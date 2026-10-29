using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WingSinger {
    public enum BreathSensorConnectionKind { UsbSerial = 0, BluetoothLE = 1 }

    // The BLE UART profile uses the existing newline decoder and identity handshake.
    // Its native worker owns discovery/GATT; Unity never calls a radio operation on its main thread.
    public sealed class WindowsBleTransport : IBreathSerialTransport {
        public const string DefaultDeviceName = "WingSinger Breath";
        public const string ServiceUuid = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
        public const string WriteUuid = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";
        public const string NotifyUuid = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";
        public static ulong ParseAddress(string address) {
            if (string.IsNullOrWhiteSpace(address)) return 0;
            string hex = address.Trim().Replace(":", "").Replace("-", "");
            if (hex.Length != 12 || !ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value) || value == 0)
                throw new ArgumentException("BLE address must contain 12 hexadecimal digits, for example AA:BB:CC:DD:EE:FF, or be empty.");
            return value;
        }
        public static void ValidateProfile(string service, string notify, string write) {
            if (!Guid.TryParse(service, out Guid s) || s == Guid.Empty || !Guid.TryParse(notify, out Guid n) || n == Guid.Empty
                || !Guid.TryParse(write, out Guid w) || w == Guid.Empty || s == n || s == w || n == w)
                throw new ArgumentException("BLE requires three distinct, non-empty service/notify/write UUIDs.");
        }
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || WINGSINGER_SERIAL_TESTS) && !UNITY_32
        public static bool IsSupported => IntPtr.Size == 8;
        readonly object gate = new object();
        IntPtr handle;
        WindowsBleTransport(IntPtr handle) { this.handle = handle; }
        public static IBreathSerialTransport Open(string name, string address, string service, string notify, string write) {
            if (!IsSupported) throw new PlatformNotSupportedException("BLE currently supports Windows x64.");
            ValidateProfile(service, notify, write);
            var instance = WS_BleCreate(name ?? "", ParseAddress(address), service, notify, write);
            if (instance == IntPtr.Zero) throw new IOException("Cannot start Windows BLE backend.");
            return new WindowsBleTransport(instance);
        }
        public int Read(byte[] buffer) {
            lock (gate) {
                RequireHandle();
                int count = WS_BleRead(handle, buffer, buffer.Length);
                if (count < 0) throw RadioError();
                return count;
            }
        }
        public void WriteLine(string line) {
            if (line != BreathSensorProtocol.Hello) throw new ArgumentException("The BLE input transport only sends the sensor identity request.");
            lock (gate) {
                RequireHandle();
                if (WS_BleWrite(handle, line + "\n") < 0) throw RadioError();
            }
        }
        IOException RadioError() {
            var text = new StringBuilder(512); WS_BleError(handle, text, text.Capacity);
            return new IOException(text.ToString());
        }
        void RequireHandle() { if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(WindowsBleTransport)); }
        public void CancelRead() { lock (gate) { if (handle != IntPtr.Zero) WS_BleStop(handle); } }
        public void Dispose() {
            IntPtr old;
            lock (gate) { old = handle; handle = IntPtr.Zero; }
            if (old != IntPtr.Zero) WS_BleDestroy(old);
        }
        const string Library = "WingSingerBle";
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr WS_BleCreate([MarshalAs(UnmanagedType.LPUTF8Str)] string name, ulong address,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string service, [MarshalAs(UnmanagedType.LPUTF8Str)] string notify, [MarshalAs(UnmanagedType.LPUTF8Str)] string write);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern int WS_BleRead(IntPtr handle, [Out] byte[] bytes, int capacity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] static extern int WS_BleWrite(IntPtr handle, string command);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] static extern void WS_BleError(IntPtr handle, StringBuilder error, int capacity);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void WS_BleStop(IntPtr handle);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void WS_BleDestroy(IntPtr handle);
#else
        public static bool IsSupported => false;
        public static IBreathSerialTransport Open(string name, string address, string service, string notify, string write) =>
            throw new PlatformNotSupportedException("BLE currently supports Windows x64.");
        public int Read(byte[] buffer) => 0;
        public void WriteLine(string line) { }
        public void CancelRead() { }
        public void Dispose() { }
#endif
    }
}
