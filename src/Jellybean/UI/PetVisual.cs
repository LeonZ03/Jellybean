using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jellybean.Engine;

namespace Jellybean.UI;

public sealed class PetVisual : FrameworkElement
{
    private readonly PetMotionEffect _motion = new();
    private readonly PetLifeMotion _life = new();
    private readonly ImageBrush _previousBrush = new() { Stretch = Stretch.Fill };
    private BitmapSource? _current, _transitionFrom;
    private string _clip = "";
    private double _blendStart, _blendDuration, _time;
    private static readonly Point HeldNape = new(SpriteCatalog.Anchor, SpriteCatalog.HeldNapeY);
    private Point _nape, _pickupOffset;
    public BitmapSource? CurrentFrame => _current;
    public BitmapSource? PreviousFrame => _previousBrush.ImageSource as BitmapSource;

    public PetVisual()
    {
        Width = SpriteCatalog.Width; Height = SpriteCatalog.Height;
        _motion.Previous = _previousBrush; Effect = _motion;
    }

    public void Update(SpriteCatalog sprites, PetEngine engine)
    {
        var clip = engine.Animation;
        var state = engine.State;
        var lifting = clip.StartsWith("pickup-", StringComparison.Ordinal);
        var progress = state switch
        {
            PetState.LieDown => engine.LieProgress,
            PetState.Rise => 1 - engine.RiseProgress,
            PetState.Stretch => Math.Sin(Math.PI * Math.Clamp(engine.Age / 2.8, 0, 1)),
            PetState.Drag when lifting => Math.Clamp(engine.Age / PetEngine.PickupSeconds, 0, 1),
            _ => 1.0
        };
        var sway = state == PetState.Drag ? engine.DragSway :
            state == PetState.Fall && clip.StartsWith("held-", StringComparison.Ordinal)
                ? engine.DragSway * Math.Max(0, 1 - engine.Age / .08) : default;
        Present(sprites.Sample(clip, engine.RenderAge, engine.LoopAnimation), clip, engine.AliveTime, state, progress, lifting ? sway * progress : sway);
    }

    public void Present(BitmapSource frame, string clip, double time, PetState state) =>
        Present(new SpriteSample(frame,frame,0),clip,time,state);

    public void Present(SpriteSample sample, string clip, double time, PetState state) =>
        Present(sample, clip, time, state, 1, default);

    private void Present(SpriteSample sample, string clip, double time, PetState state, double progress, Vector sway)
    {
        var elapsed = _current is null ? 0 : Math.Max(0, time - _time);
        _time = time;
        var previous = _current;
        if (clip != _clip)
        {
            var samePose = _transitionFrom is null && ReferenceEquals(_current, sample.From) && sample.Blend == 0 &&
                (_motion.Mix == 1 || ReferenceEquals(_previousBrush.ImageSource, _current));
            _transitionFrom = state == PetState.Drag || samePose ? null : Capture();
            _pickupOffset = state == PetState.Drag && _current is not null && clip.StartsWith("held-", StringComparison.Ordinal) &&
                !_clip.StartsWith("pickup-", StringComparison.Ordinal)
                ? new Point((_nape.X - HeldNape.X) / Width, (_nape.Y - HeldNape.Y) / Height) : default;
            _clip=clip; _blendStart=time;
            _blendDuration=state is PetState.Pounce or PetState.Jump or PetState.Catch or PetState.Land ? .045 :
                state == PetState.Drag ? .16 : .095;
        }
        var phase=Math.Clamp((time-_blendStart)/Math.Max(.001,_blendDuration),0,1);
        if (_transitionFrom is not null && phase<1)
        {
            _current=sample.From; _previousBrush.ImageSource=_transitionFrom;
            _motion.Mix=phase*phase*(3-2*phase); _motion.PreviousFrozen=1;
        }
        else
        {
            _transitionFrom=null;
            _current=sample.To; _previousBrush.ImageSource=sample.From;
            _motion.Mix=sample.Blend; _motion.PreviousFrozen=0;
        }
        var offset = state == PetState.Drag ? 1 - phase * phase * (3 - 2 * phase) : 0;
        _motion.PickupOffset = new Point(_pickupOffset.X * offset, _pickupOffset.Y * offset);
        _nape = NapeFor(clip, state, progress);
        _life.Apply(_motion, clip, state, progress, time, elapsed, sway);
        if (!ReferenceEquals(previous, _current))
        {
            if (previous?.PixelWidth != _current.PixelWidth)
                RenderOptions.SetBitmapScalingMode(this, _current.PixelWidth > SpriteCatalog.Width
                    ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            InvalidateVisual();
        }
    }

    private static Point NapeFor(string clip, PetState state, double progress)
    {
        if (clip.StartsWith("held-", StringComparison.Ordinal)) return HeldNape;
        var standing = new Point(166, 72);
        var resting = clip.StartsWith("window-", StringComparison.Ordinal) ? new Point(155, 102) : new Point(164, 130);
        var nape = state switch
        {
            PetState.Doze => resting,
            PetState.LieDown or PetState.Rise => standing + (resting - standing) * progress,
            PetState.Stretch => standing + new Vector(3, 30) * progress,
            PetState.Drag => standing + (HeldNape - standing) * progress,
            PetState.Pet or PetState.Peek => new(111, 87),
            PetState.Perch => new(116, 88),
            PetState.HuntAim or PetState.Stalk or PetState.Pounce or PetState.Catch => new(169, 119),
            _ when clip is "front" or "rear" => new(120, 84),
            _ when clip == "front-three-quarter" => new(89, 83),
            _ when clip == "rear-three-quarter" => new(145, 79),
            _ => standing
        };
        if (clip.EndsWith("-left", StringComparison.Ordinal) || clip == "left-view") nape.X = SpriteCatalog.Width - nape.X;
        return nape;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_current is null) return;
        dc.DrawImage(_current,new Rect(0,0,Width,Height));
    }

    public BitmapSource? Capture()
    {
        if (_current is null) return null;
        var bitmap=new RenderTargetBitmap(SpriteCatalog.Width,SpriteCatalog.Height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(this); bitmap.Freeze(); return bitmap;
    }
    protected override HitTestResult HitTestCore(PointHitTestParameters parameters) => new PointHitTestResult(this, parameters.HitPoint);
}
