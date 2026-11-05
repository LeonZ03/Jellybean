using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jellybean.Engine;

namespace Jellybean.UI;

public sealed class PetVisual : FrameworkElement
{
    private readonly PetMotionEffect _motion = new();
    private readonly ImageBrush _previousBrush = new() { Stretch = Stretch.Fill };
    private BitmapSource? _current, _previous, _frozen;
    private string _clip = "";
    private double _blendStart, _blendDuration = .2, _time, _breathWeight = 1;
    private bool _clipTransition, _dragging;
    public BitmapSource? CurrentFrame => _frozen ?? _current;
    public BitmapSource? PreviousFrame => _previous;

    public PetVisual()
    {
        Width = SpriteCatalog.Width; Height = SpriteCatalog.Height;
        _motion.Previous = _previousBrush; Effect = _motion;
    }
    public void Update(SpriteCatalog sprites, PetEngine engine) =>
        Present(sprites.Frame(engine.Animation, engine.RenderAge, engine.LoopAnimation), engine.Animation, engine.AliveTime, engine.State);

    public void Present(BitmapSource frame, string clip, double time, PetState state)
    {
        var dt = Math.Clamp(time - _time, 0, .1); _time = time;
        var targetBreath = state == PetState.Doze ? 1.35 : state is PetState.Walk or PetState.Fall or PetState.Jump ? .5 : 1;
        _breathWeight += (targetBreath - _breathWeight) * (1 - Math.Exp(-dt * 8));
        if (state == PetState.Drag)
        {
            if (!_dragging) Freeze();
            _dragging = true; _motion.Mix = 1; _motion.Breath = _motion.Ear = 0; InvalidateVisual(); return;
        }
        if (_dragging)
        {
            _previous = _frozen; _frozen = null; _dragging = false;
            _current = frame; _clip = clip; StartBlend(time, .22, true, true);
        }
        else if (clip != _clip)
        {
            var interrupted = _current is not null && Blend < 1;
            _previous = interrupted ? Capture() : _current;
            _current = frame; _clip = clip; StartBlend(time, .22, true, interrupted);
        }
        else if (!ReferenceEquals(frame, _current))
        {
            if (!_clipTransition || Blend >= 1)
            { _previous = _current; StartBlend(time, .065, false, false); }
            _current = frame;
        }
        if (Blend >= 1) _previous = null;
        _previousBrush.ImageSource = _previous ?? _current;
        var t = Blend; _motion.Mix = _previous is null ? 1 : t * t * (3 - 2 * t);
        _motion.Breath = (Math.Sin(time * Math.PI * 2 / 3.4) + 1) * .7 * _breathWeight;
        _motion.Ear = Math.Pow(Math.Max(0, Math.Sin(time * .65)), 24) * .65;
        InvalidateVisual();
    }
    private void StartBlend(double time, double duration, bool clip, bool snapshot)
    { _blendStart = time; _blendDuration = duration; _clipTransition = clip; _motion.PreviousFrozen = snapshot ? 1 : 0; }
    private double Blend => Math.Clamp((_time - _blendStart) / _blendDuration, 0, 1);
    public void Freeze() => _frozen = Capture() ?? _current;
    protected override void OnRender(DrawingContext dc)
    {
        var image = _dragging ? _frozen : _current;
        if (image is not null) dc.DrawImage(image, new Rect(0, 0, Width, Height));
    }
    public BitmapSource? Capture()
    {
        if (_current is null) return null;
        var bitmap = new RenderTargetBitmap(SpriteCatalog.Width, SpriteCatalog.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this); bitmap.Freeze(); return bitmap;
    }
    protected override HitTestResult HitTestCore(PointHitTestParameters parameters) => new PointHitTestResult(this, parameters.HitPoint);
}
