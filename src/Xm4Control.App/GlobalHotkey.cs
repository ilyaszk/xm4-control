using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Xm4Control.App;

/// <summary>Raccourci clavier global Ctrl+Alt+N, reçu par une fenêtre Win32 invisible.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    public const string Label = "Ctrl + Alt + N";

    private const int WmHotkey = 0x0312;
    private const int Id = 0x584D;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint KeyN = 0x4E;

    private readonly HwndSource _window = new(new HwndSourceParameters("Xm4ControlHotkey") { WindowStyle = 0 });
    private bool _registered;

    public GlobalHotkey() => _window.AddHook(WndProc);

    public event Action? Pressed;

    /// <returns>false si une autre application utilise déjà ce raccourci.</returns>
    public bool Register()
    {
        if (!_registered) _registered = RegisterHotKey(_window.Handle, Id, ModControl | ModAlt | ModNoRepeat, KeyN);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) UnregisterHotKey(_window.Handle, Id);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam == Id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
