using System.ComponentModel;

namespace Xm4Control.Core.Bluetooth;

/// <summary>
/// Profil "mains libres" (HFP) côté Windows. Tant qu'il est actif, une appli qui ouvre le micro
/// fait basculer le casque en mode téléphone (mono, son dégradé). Le désactiver laisse le casque
/// en A2DP stéréo en permanence, sans micro. C'est la case "Téléphonie mains libres" de l'onglet
/// Services du Panneau de configuration.
/// </summary>
public static class HandsFreeProfile
{
    public static readonly Guid HandsFree = new("0000111E-0000-1000-8000-00805F9B34FB");
    public static readonly Guid Headset = new("00001108-0000-1000-8000-00805F9B34FB");

    public static bool IsEnabled(ulong address)
    {
        using var radio = BluetoothNative.OpenRadio();
        var info = BluetoothNative.DeviceInfo.Create(address);
        var services = new Guid[64];
        var count = (uint)services.Length;
        var result = BluetoothNative.BluetoothEnumerateInstalledServices(radio.Handle, ref info, ref count, services);
        if (result != 0) throw new Win32Exception((int)result);
        return services.Take((int)count).Any(g => g == HandsFree || g == Headset);
    }

    /// <exception cref="UnauthorizedAccessException">Windows exige les droits administrateur.</exception>
    public static void SetEnabled(ulong address, bool enabled)
    {
        using var radio = BluetoothNative.OpenRadio();
        var info = BluetoothNative.DeviceInfo.Create(address);
        var result = BluetoothNative.BluetoothGetDeviceInfo(radio.Handle, ref info);
        if (result != 0) throw new Win32Exception((int)result);

        // HSP est coupé aussi au cas où, mais seul HFP est réactivé : le XM4 n'expose pas HSP.
        var services = enabled ? [HandsFree] : new[] { HandsFree, Headset };
        foreach (var service in services)
        {
            var guid = service;
            var flags = enabled ? BluetoothNative.ServiceEnable : BluetoothNative.ServiceDisable;
            result = BluetoothNative.BluetoothSetServiceState(radio.Handle, ref info, ref guid, flags);
            switch (result)
            {
                case 0:
                case BluetoothNative.EInvalidArg: // déjà dans l'état demandé
                    continue;
                case BluetoothNative.ErrorAccessDenied:
                    throw new UnauthorizedAccessException("Droits administrateur requis pour changer le profil mains libres.");
                default:
                    // Pour HSP, "service inexistant" est normal (le XM4 n'expose que HFP).
                    if (service == Headset) continue;
                    throw new Win32Exception((int)result);
            }
        }
    }
}
