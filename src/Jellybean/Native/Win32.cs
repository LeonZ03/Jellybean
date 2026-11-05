// Monitor/DPI placement adapted from PixelPaws (MIT). See THIRD-PARTY-NOTICES.md.
using System.Runtime.InteropServices;
using System.Text;

namespace Jellybean.Native;

internal static class Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int Size;
        public RECT Monitor, Work;
        public uint Flags;
    }

    public delegate bool EnumWindowsProc(nint window, nint parameter);
    private delegate bool MonitorEnumProc(nint monitor, nint dc, ref RECT rect, nint parameter);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint window, out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] public static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] public static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(nint window, int index, int value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(POINT point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowRect(nint window, int attribute, out RECT value, int size);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(nint icon);

    public const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WM_MOUSEACTIVATE = 0x21, WM_NCHITTEST = 0x84;

    public static bool IsCloaked(nint window) =>
        DwmGetWindowAttribute(window, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    public static bool GetVisibleRect(nint window, out RECT rect)
    {
        if (DwmGetWindowRect(window, 9, out rect, Marshal.SizeOf<RECT>()) == 0) return true;
        return GetWindowRect(window, out rect);
    }

    public static List<(RECT Monitor, RECT Work)> GetMonitors()
    {
        var result = new List<(RECT, RECT)>();
        EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref RECT rect, nint parameter) =>
        {
            var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info)) result.Add((info.Monitor, info.Work));
            return true;
        }, 0);
        return result;
    }

    // A fixed scale keeps movement continuous when crossing monitors with different DPI.
    public static double PrimaryMonitorScale()
    {
        var monitor = MonitorFromPoint(new POINT(), 1);
        return GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
    }

    public static void Place(nint window, double x, double y, double width, double height) =>
        SetWindowPos(window, 0, (int)Math.Round(x), (int)Math.Round(y),
            (int)Math.Ceiling(width), (int)Math.Ceiling(height), 0x0010 | 0x0004);

    public static void ConfigureOverlay(nint window, bool clickThrough = false)
    {
        var flags = GetWindowLong(window, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        if (clickThrough) flags |= WS_EX_TRANSPARENT;
        SetWindowLong(window, GWL_EXSTYLE, flags);
    }

    public static void RaiseOverlay(nint window) => SetWindowPos(window, -1, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);

    public static bool IsForegroundFullscreen(uint ownProcessId)
        => IsWindowFullscreen(GetForegroundWindow(),ownProcessId);

    internal static bool IsWindowFullscreen(nint window,uint ownProcessId)
    {
        GetWindowThreadProcessId(window, out var processId);
        if (window == 0 || processId == ownProcessId) return false;
        var name = new StringBuilder(80);
        GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        if (!GetWindowRect(window, out var rect)) return false;
        return GetMonitors().Any(m => rect.Left <= m.Monitor.Left + 2 && rect.Right >= m.Monitor.Right - 2 &&
            rect.Top <= m.Monitor.Top + 2 && rect.Bottom >= m.Monitor.Bottom - 2);
    }
}
