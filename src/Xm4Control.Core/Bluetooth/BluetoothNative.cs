using System.Runtime.InteropServices;

namespace Xm4Control.Core.Bluetooth;

/// <summary>API Bluetooth Win32 (bthprops.cpl).</summary>
internal static class BluetoothNative
{
    private const string Dll = "bthprops.cpl";

    public const uint ServiceDisable = 0x00;
    public const uint ServiceEnable = 0x01;
    public const uint ErrorAccessDenied = 5;
    public const uint ErrorMoreData = 234;
    public const uint EInvalidArg = 0x80070057;

    [StructLayout(LayoutKind.Sequential)]
    public struct FindRadioParams
    {
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceSearchParams
    {
        public uint Size;
        public int ReturnAuthenticated;
        public int ReturnRemembered;
        public int ReturnUnknown;
        public int ReturnConnected;
        public int IssueInquiry;
        public byte TimeoutMultiplier;
        public IntPtr Radio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DeviceInfo
    {
        public uint Size;
        public ulong Address;
        public uint ClassOfDevice;
        public int Connected;
        public int Remembered;
        public int Authenticated;
        public SystemTime LastSeen;
        public SystemTime LastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string Name;

        public static DeviceInfo Create(ulong address = 0) => new()
        {
            Size = (uint)Marshal.SizeOf<DeviceInfo>(),
            Address = address,
            Name = "",
        };
    }

    [DllImport(Dll, SetLastError = true)]
    public static extern IntPtr BluetoothFindFirstRadio(ref FindRadioParams parameters, out IntPtr radio);

    [DllImport(Dll, SetLastError = true)]
    public static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport(Dll, SetLastError = true)]
    public static extern IntPtr BluetoothFindFirstDevice(ref DeviceSearchParams parameters, ref DeviceInfo info);

    [DllImport(Dll, SetLastError = true)]
    public static extern bool BluetoothFindNextDevice(IntPtr find, ref DeviceInfo info);

    [DllImport(Dll, SetLastError = true)]
    public static extern bool BluetoothFindDeviceClose(IntPtr find);

    [DllImport(Dll)]
    public static extern uint BluetoothGetDeviceInfo(IntPtr radio, ref DeviceInfo info);

    [DllImport(Dll)]
    public static extern uint BluetoothEnumerateInstalledServices(
        IntPtr radio, ref DeviceInfo info, ref uint serviceCount, [Out] Guid[]? services);

    [DllImport(Dll)]
    public static extern uint BluetoothSetServiceState(IntPtr radio, ref DeviceInfo info, ref Guid service, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    /// <summary>Ouvre la première radio Bluetooth locale. Le handle doit être fermé par l'appelant.</summary>
    public static SafeRadioHandle OpenRadio()
    {
        var parameters = new FindRadioParams { Size = (uint)Marshal.SizeOf<FindRadioParams>() };
        var find = BluetoothFindFirstRadio(ref parameters, out var radio);
        if (find == IntPtr.Zero) throw new InvalidOperationException("Aucun adaptateur Bluetooth trouvé.");
        BluetoothFindRadioClose(find);
        return new SafeRadioHandle(radio);
    }
}

internal sealed class SafeRadioHandle(IntPtr handle) : IDisposable
{
    public IntPtr Handle { get; } = handle;

    public void Dispose() => BluetoothNative.CloseHandle(Handle);
}
