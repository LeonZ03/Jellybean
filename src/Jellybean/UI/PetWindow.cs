// Physical placement and DPI compensation adapted from PixelPaws PetWindow (MIT).
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Jellybean.Engine;
using Jellybean.Native;
using Jellybean.Services;

namespace Jellybean.UI;

public sealed class PetWindow : Window
{
    private readonly PetEngine _engine;
    private readonly UserSettings _settings;
    private readonly SpriteCatalog _sprites;
    private readonly Canvas _root = new() { Width = SpriteCatalog.Width, Height = SpriteCatalog.Height };
    private readonly Image _image = new() { Width = SpriteCatalog.Width, Height = SpriteCatalog.Height };
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input);
    private readonly DispatcherTimer _clickTimer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTick;
    private bool _pressing, _dragging, _doubleClick;
    private Point _down, _dragOffset;
    private BitmapSource? _frame;
    private byte[] _alpha = Array.Empty<byte>();
    private nint _handle;
    private int _lastX = int.MinValue, _lastY = int.MinValue, _lastW, _lastH;
    private readonly double _dpi;
    public event Action? MenuRequested;
    public event Action? FrameUpdated;
    public event Action<double>? ScaleRequested;

    public PetWindow(PetEngine engine, UserSettings settings, SpriteCatalog sprites, double dpi)
    {
        _engine = engine; _settings = settings; _sprites = sprites; _dpi = dpi;
        Title = "糖豆 Jellybean";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = null;
        ShowInTaskbar = false; ShowActivated = false; Focusable = false;
        Topmost = settings.Topmost;
        Width = SpriteCatalog.Width * settings.Scale; Height = SpriteCatalog.Height * settings.Scale;
        Content = _root; _root.Children.Add(_image);
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        _timer.Interval = TimeSpan.FromMilliseconds(80);
        _timer.Tick += (_, _) => Tick();
        _clickTimer.Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime);
        _clickTimer.Tick += (_, _) => { _clickTimer.Stop(); _engine.Pet(); Render(); };
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            Win32.ConfigureOverlay(_handle);
            HwndSource.FromHwnd(_handle).AddHook(WindowProc);
            SyncScale(); Render();
        };
        Closed += (_, _) => { _timer.Stop(); _clickTimer.Stop(); };
    }

    private nint WindowProc(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == Win32.WM_MOUSEACTIVATE) { handled = true; return 3; }
        if (message == Win32.WM_NCHITTEST && !IsMouseCaptured)
        {
            Win32.GetCursorPos(out var cursor);
            var p = PointFromScreen(new Point(cursor.X,cursor.Y));
            var factor = _settings.Scale * _dpi / VisualTreeHelper.GetDpi(this).DpiScaleX;
            var x = (int)(p.X/factor); var y = (int)(p.Y/factor);
            if (x < 0 || y < 0 || x >= SpriteCatalog.Width || y >= SpriteCatalog.Height ||
                _alpha.Length == 0 || _alpha[y*SpriteCatalog.Width+x] < 12 ||
                (_engine.State == PetState.Peek && y > SpriteCatalog.Foot - _engine.BodyHeight * .5 / _settings.Scale * _engine.PeekFraction))
            { handled = true; return -1; }
        }
        return 0;
    }

    public void SetRunning(bool running)
    {
        if (running)
        {
            if (!IsVisible) Show();
            _lastTick = _clock.Elapsed.TotalSeconds;
            _timer.Start(); Render();
        }
        else { _timer.Stop(); _clickTimer.Stop(); Hide(); }
    }

    private void Tick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Min(now - _lastTick, .1);
        _lastTick = now;
        if (_dragging && (Win32.GetAsyncKeyState(1) & 0x8000) == 0) EndDrag();
        if (_dragging)
        {
            Win32.GetCursorPos(out var cursor);
            _engine.DragTo(cursor.X/_dpi-_dragOffset.X,cursor.Y/_dpi-_dragOffset.Y);
        }
        _engine.Tick(dt);
        _timer.Interval = TimeSpan.FromMilliseconds(_engine.Moving ? 33 : 100);
        Render(); FrameUpdated?.Invoke();
    }

    public void Render()
    {
        if (_handle == 0) return;
        Topmost = _settings.Topmost;
        SyncScale();
        var loop = _engine.State is not (PetState.Pounce or PetState.Jump or PetState.Stretch);
        var image = _sprites.Frame(_engine.Animation,_engine.Age,loop);
        if (!ReferenceEquals(image,_frame))
        {
            _frame = image; _image.Source = image;
            _alpha = _sprites.HitMask(image);
        }
        // The body sinks behind the window; only the part above its edge remains visible.
        _root.Clip = _engine.State == PetState.Peek
            ? new RectangleGeometry(new Rect(0,0,SpriteCatalog.Width,SpriteCatalog.Foot-64*_engine.PeekFraction))
            : null;
        var s = _settings.Scale * _dpi;
        var x = (int)Math.Round((_engine.CenterX-SpriteCatalog.Anchor*_settings.Scale)*_dpi);
        var y = (int)Math.Round((_engine.FeetY-SpriteCatalog.Foot*_settings.Scale)*_dpi);
        var w = (int)Math.Ceiling(SpriteCatalog.Width*s); var h=(int)Math.Ceiling(SpriteCatalog.Height*s);
        if (x!=_lastX || y!=_lastY || w!=_lastW || h!=_lastH)
        {
            Win32.Place(_handle,x,y,w,h);
            _lastX=x; _lastY=y; _lastW=w; _lastH=h;
        }
    }

    private void SyncScale()
    {
        var factor = _settings.Scale*_dpi/VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (_root.LayoutTransform is ScaleTransform old && Math.Abs(old.ScaleX-factor)<.0001) return;
        _root.LayoutTransform = new ScaleTransform(factor,factor);
    }

    protected override void OnDpiChanged(DpiScale oldDpi,DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi,newDpi);
        _lastX = int.MinValue; SyncScale(); Render();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _clickTimer.Stop();
        if (e.ClickCount >= 2) { _doubleClick=true; _engine.ToggleToy(); e.Handled=true; return; }
        Win32.GetCursorPos(out var cursor);
        _down = new Point(cursor.X/_dpi,cursor.Y/_dpi);
        _dragOffset = new Point(_down.X-_engine.CenterX,_down.Y-_engine.FeetY);
        _pressing=true; _dragging=false; _doubleClick=false; CaptureMouse(); e.Handled=true;
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_pressing) return;
        Win32.GetCursorPos(out var cursor);
        var point = new Point(cursor.X/_dpi,cursor.Y/_dpi);
        if (!_dragging && (point-_down).Length>6) { _dragging=true; _engine.BeginDrag(); }
        if (_dragging) { _engine.DragTo(point.X-_dragOffset.X,point.Y-_dragOffset.Y); Render(); }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_doubleClick) { _doubleClick=false; return; }
        if (!_pressing) return;
        var wasDragging = _dragging;
        EndDrag();
        if (!wasDragging) _clickTimer.Start();
        e.Handled=true;
    }

    private void EndDrag()
    {
        var wasDragging=_dragging; _pressing=false; _dragging=false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (wasDragging) { _engine.BringIntoView(); _engine.EndDrag(); }
    }

    protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e) { base.OnLostMouseCapture(e); if (_pressing) EndDrag(); }
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) { _clickTimer.Stop(); MenuRequested?.Invoke(); e.Handled=true; }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { ScaleRequested?.Invoke(e.Delta>0?.1:-.1); e.Handled=true; }
}
