// Window enumeration adapted from PixelPaws SurfaceProvider.cs (MIT).
// Jellybean adds window identities, exposed-edge clipping and moving-window attachment.
using System.Windows;
using Jellybean.Native;

namespace Jellybean.Engine;

public readonly record struct Surface(double Left, double Right, double Top, nint Window = 0)
{
    public bool ContainsX(double x) => x >= Left && x <= Right;
    public double Width => Right - Left;
}

public sealed class SurfaceProvider
{
    private readonly uint _processId;
    private readonly double _scale;
    public DesktopGeometry Desktop { get; private set; }
    public IReadOnlyList<Surface> Surfaces { get; private set; } = Array.Empty<Surface>();
    public double MinimumClearance { get; set; } = 136;

    public SurfaceProvider(double scale,uint? ownProcessId=null)
    {
        _scale = scale;
        _processId = ownProcessId ?? (uint)Environment.ProcessId;
        Desktop = DesktopGeometry.Capture(scale);
        Refresh();
    }

    public void Refresh()
    {
        Desktop = DesktopGeometry.Capture(_scale);
        var surfaces = Desktop.WorkAreas.Select(a => new Surface(a.Left, a.Right, a.Bottom)).ToList();
        var occluders = new List<Rect>();
        Win32.EnumWindows((window, _) =>
        {
            if (!IsEligible(window) || !Win32.GetVisibleRect(window, out var r)) return true;
            var rect = new Rect(r.Left / _scale, r.Top / _scale, r.Width / _scale, r.Height / _scale);
            if (rect.Width < 80 || rect.Height < 32) return true;
            var area = Desktop.WorkAreaAt(rect.Left + rect.Width / 2, rect.Top);
            if (rect.Top >= area.Top + MinimumClearance && rect.Top < area.Bottom)
            {
                var segments = new List<(double Left, double Right)> { (Math.Max(rect.Left, area.Left), Math.Min(rect.Right, area.Right)) };
                foreach (var cover in occluders)
                {
                    if (cover.Top > rect.Top + 2 || cover.Bottom <= rect.Top) continue;
                    var next = new List<(double, double)>();
                    foreach (var segment in segments)
                    {
                        if (cover.Right <= segment.Left || cover.Left >= segment.Right) next.Add(segment);
                        else
                        {
                            if (cover.Left > segment.Left) next.Add((segment.Left, cover.Left));
                            if (cover.Right < segment.Right) next.Add((cover.Right, segment.Right));
                        }
                    }
                    segments = next;
                }
                foreach (var segment in segments)
                    if (segment.Right - segment.Left >= 60)
                        surfaces.Add(new Surface(segment.Left, segment.Right, rect.Top, window));
            }
            occluders.Add(rect);
            return true;
        }, 0);
        Surfaces = surfaces;
    }

    private bool IsEligible(nint window)
    {
        Win32.GetWindowThreadProcessId(window, out var processId);
        return processId != _processId && Win32.IsWindowVisible(window) && !Win32.IsIconic(window) &&
            !Win32.IsCloaked(window) && Win32.GetWindowTextLength(window) > 0 &&
            (Win32.GetWindowLong(window, Win32.GWL_EXSTYLE) & Win32.WS_EX_TOOLWINDOW) == 0;
    }

    public bool TryGetWindowOrigin(nint window, out Point origin)
    {
        if (Win32.IsWindowVisible(window) && !Win32.IsIconic(window) && !Win32.IsCloaked(window) &&
            Win32.GetVisibleRect(window, out var rect))
        {
            origin = new Point(rect.Left / _scale, rect.Top / _scale);
            return true;
        }
        origin = default;
        return false;
    }
}
