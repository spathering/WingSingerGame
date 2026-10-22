using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WingSinger {
    // Windows native serial I/O keeps the project's existing .NET Standard profile and assembly references.
    public sealed class WindowsSerialTransport : IBreathSerialTransport {
        public static bool IsPortName(string name) {
            if (string.IsNullOrEmpty(name) || name.Length < 4 || !name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 3; i < name.Length; i++) if (name[i] < '0' || name[i] > '9') return false;
            return int.TryParse(name.Substring(3), out int number) && number > 0;
        }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || WINGSINGER_SERIAL_TESTS
        public static bool IsSupported => true;
        readonly SafeFileHandle handle;
        WindowsSerialTransport(SafeFileHandle handle) { this.handle = handle; }

        public static string[] GetCandidatePorts() {
            var ports = new List<string>();
            char[] names = null;
            uint count = 0;
            for (int length = 16384; length <= 1048576; length *= 2) {
                names = new char[length];
                count = QueryDosDevice(null, names, (uint)names.Length);
                if (count != 0) break;
                if (Marshal.GetLastWin32Error() != 122) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (count == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            foreach (string name in new string(names, 0, (int)count).Split('\0')) {
                if (!IsPortName(name)) continue;
                var target = new char[1024];
                uint used = QueryDosDevice(name, target, (uint)target.Length);
                // Ignore Bluetooth virtual COM ports; probing them can block on a remote device.
                if (used == 0 || new string(target, 0, (int)used).IndexOf("Bth", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                ports.Add(name);
            }
            ports.Sort(StringComparer.OrdinalIgnoreCase);
            return ports.ToArray();
        }

        public static IBreathSerialTransport Open(string port, int baudRate) {
            if (!IsPortName(port)) throw new ArgumentException("Invalid COM port name.");
            var file = CreateFile("\\\\.\\" + port, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (file.IsInvalid) { int error = Marshal.GetLastWin32Error(); file.Dispose(); throw new Win32Exception(error); }
            try {
                var dcb = new Dcb { Length = (uint)Marshal.SizeOf(typeof(Dcb)) };
                Check(GetCommState(file, ref dcb));
                dcb.BaudRate = (uint)baudRate; dcb.Flags = 0x11; // Binary, DTR enabled, no flow control.
                dcb.ByteSize = 8; dcb.Parity = 0; dcb.StopBits = 0; // 8N1
                Check(SetCommState(file, ref dcb));
                var timeouts = new CommTimeouts { ReadInterval = uint.MaxValue, ReadConstant = 100, WriteConstant = 100 };
                Check(SetCommTimeouts(file, ref timeouts));
                Check(PurgeComm(file, 0x0C));
                return new WindowsSerialTransport(file);
            } catch { file.Dispose(); throw; }
        }

        public int Read(byte[] buffer) {
            Check(ReadFile(handle, buffer, (uint)buffer.Length, out uint count, IntPtr.Zero));
            return (int)count;
        }
        public void WriteLine(string line) {
            byte[] bytes = Encoding.ASCII.GetBytes(line + "\n");
            Check(WriteFile(handle, bytes, (uint)bytes.Length, out uint written, IntPtr.Zero));
            if (written != bytes.Length) throw new System.IO.IOException("Serial handshake write timed out.");
        }
        public void CancelRead() { if (!handle.IsClosed && !handle.IsInvalid) CancelIoEx(handle, IntPtr.Zero); }
        public void Dispose() => handle.Dispose();
        static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }

        [StructLayout(LayoutKind.Sequential)]
        struct Dcb {
            public uint Length, BaudRate, Flags;
            public ushort Reserved, XonLimit, XoffLimit;
            public byte ByteSize, Parity, StopBits;
            public byte XonChar, XoffChar, ErrorChar, EofChar, EventChar;
            public ushort Reserved1;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct CommTimeouts { public uint ReadInterval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant; }
        [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint QueryDosDevice(string name, [Out] char[] target, uint length);
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetCommState(SafeFileHandle file, ref Dcb settings);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommState(SafeFileHandle file, ref Dcb settings);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommTimeouts(SafeFileHandle file, ref CommTimeouts timeouts);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool PurgeComm(SafeFileHandle file, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle file, [Out] byte[] buffer, uint length, out uint read, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteFile(SafeFileHandle file, byte[] buffer, uint length, out uint written, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
#else
        public static bool IsSupported => false;
        public static string[] GetCandidatePorts() => Array.Empty<string>();
        public static IBreathSerialTransport Open(string port, int baudRate) => throw new PlatformNotSupportedException("Arduino serial input currently supports Windows.");
        public int Read(byte[] buffer) => 0;
        public void WriteLine(string line) { }
        public void CancelRead() { }
        public void Dispose() { }
#endif
    }
}
