using System.Runtime.InteropServices;

namespace Xm4Control.Core.Bluetooth;

public sealed record PairedDevice(ulong Address, string Name, bool Connected)
{
    public string AddressText => string.Join(":", BitConverter.GetBytes(Address).Take(6).Reverse().Select(b => b.ToString("X2")));

    public override string ToString() => $"{Name} ({AddressText}){(Connected ? " - connecté" : "")}";
}

public static class PairedDevices
{
    public static List<PairedDevice> List()
    {
        using var radio = BluetoothNative.OpenRadio();
        var parameters = new BluetoothNative.DeviceSearchParams
        {
            Size = (uint)Marshal.SizeOf<BluetoothNative.DeviceSearchParams>(),
            ReturnAuthenticated = 1,
            ReturnRemembered = 1,
            ReturnConnected = 1,
            Radio = radio.Handle,
        };
        var info = BluetoothNative.DeviceInfo.Create();
        var devices = new List<PairedDevice>();

        var find = BluetoothNative.BluetoothFindFirstDevice(ref parameters, ref info);
        if (find == IntPtr.Zero) return devices;
        try
        {
            do
            {
                devices.Add(new PairedDevice(info.Address, info.Name, info.Connected != 0));
                info = BluetoothNative.DeviceInfo.Create();
            } while (BluetoothNative.BluetoothFindNextDevice(find, ref info));
        }
        finally
        {
            BluetoothNative.BluetoothFindDeviceClose(find);
        }
        return devices;
    }

    /// <summary>Relit l'état d'un appareil (connecté ou non).</summary>
    public static PairedDevice? Get(ulong address)
    {
        using var radio = BluetoothNative.OpenRadio();
        var info = BluetoothNative.DeviceInfo.Create(address);
        return BluetoothNative.BluetoothGetDeviceInfo(radio.Handle, ref info) == 0
            ? new PairedDevice(info.Address, info.Name, info.Connected != 0)
            : null;
    }

    /// <summary>Premier WH-1000XM4 appairé, en privilégiant celui qui est connecté.</summary>
    public static PairedDevice? FindXm4() =>
        List()
            .Where(d => d.Name.Contains("WH-1000XM4", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Connected)
            .FirstOrDefault();
}
