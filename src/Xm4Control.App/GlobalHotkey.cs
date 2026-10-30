using System.Runtime.InteropServices;

namespace Xm4Control.App;

/// <summary>
/// Raccourci clavier global Ctrl+Alt+N, reçu par une fenêtre Win32 "message only" créée sur le thread UI :
/// la boucle de messages de WinUI la dessert comme les autres fenêtres du thread.
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    public const string Label = "Ctrl + Alt + N";

    private const string ClassName = "Xm4ControlHotkey";
    private const uint WmHotkey = 0x0312;
    private const int Id = 0x584D;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint KeyN = 0x4E;
    private static readonly IntPtr HwndMessage = new(-3);

    // Gardé en champ : le délégué passé à Windows ne doit pas être collecté.
    private readonly WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private bool _registered;

    public GlobalHotkey()
    {
        _wndProc = WindowProc;
        var windowClass = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            Instance = GetModuleHandle(null),
            ClassName = ClassName,
        };
        RegisterClassEx(ref windowClass);
        _hwnd = CreateWindowEx(0, ClassName, "", 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, windowClass.Instance, IntPtr.Zero);
    }

    public event Action? Pressed;

    /// <returns>false si une autre application utilise déjà ce raccourci.</returns>
    public bool Register()
    {
        if (!_registered) _registered = RegisterHotKey(_hwnd, Id, ModControl | ModAlt | ModNoRepeat, KeyN);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered) UnregisterHotKey(_hwnd, Id);
        _registered = false;
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmHotkey && wParam == Id)
        {
            Pressed?.Invoke();
            return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
        DestroyWindow(_hwnd);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public IntPtr WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
