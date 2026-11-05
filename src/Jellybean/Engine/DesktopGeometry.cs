// Adapted from PixelPaws DesktopGeometry.cs (MIT).
using System.Windows;
using Jellybean.Native;

namespace Jellybean.Engine;

public sealed class DesktopGeometry
{
    public IReadOnlyList<Rect> WorkAreas { get; }

    public DesktopGeometry(IReadOnlyList<Rect> workAreas) => WorkAreas = workAreas;

    public static DesktopGeometry Capture(double scale) => new(Win32.GetMonitors()
        .Select(m => new Rect(m.Work.Left / scale, m.Work.Top / scale, m.Work.Width / scale, m.Work.Height / scale)).ToArray());

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
