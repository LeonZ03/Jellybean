// Adapted from PixelPaws DesktopGeometry.cs (MIT).
using System.Windows;
using Jellybean.Native;

namespace Jellybean.Engine;

public sealed class DesktopGeometry
{
    public IReadOnlyList<Rect> WorkAreas { get; }
    private readonly IReadOnlyList<Rect> _monitors;

    public DesktopGeometry(IReadOnlyList<Rect> workAreas, IReadOnlyList<Rect> monitors)
    { WorkAreas = workAreas; _monitors = monitors; }

    public static DesktopGeometry Capture(double scale)
    {
        var monitors = Win32.GetMonitors();
        Rect Logical(Win32.RECT r) => new(r.Left / scale, r.Top / scale, r.Width / scale, r.Height / scale);
        return new(monitors.Select(m => Logical(m.Work)).ToArray(), monitors.Select(m => Logical(m.Monitor)).ToArray());
    }

    public Rect MonitorAt(double x, double y) => _monitors.MinBy(a => DistanceSquared(a, x, y));

    public Rect WorkAreaAt(double x, double y)
    {
        return WorkAreas.MinBy(a => DistanceSquared(a, x, y));
    }

    private static double DistanceSquared(Rect area, double x, double y)
    {
        var dx = Math.Max(area.Left - x, Math.Max(0, x - area.Right));
        var dy = Math.Max(area.Top - y, Math.Max(0, y - area.Bottom));
        return dx * dx + dy * dy;
    }
}
