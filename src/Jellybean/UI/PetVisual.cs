using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jellybean.Engine;

namespace Jellybean.UI;

public sealed class PetVisual : FrameworkElement
{
    private readonly record struct Breathing(double X, double Y, double Width, double Height, double Depth)
    {
        public static Breathing Blend(Breathing from, Breathing to, double amount) => new(
            Lerp(from.X, to.X, amount), Lerp(from.Y, to.Y, amount),
            Lerp(from.Width, to.Width, amount), Lerp(from.Height, to.Height, amount),
            Lerp(from.Depth, to.Depth, amount));

        private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    }

    private static readonly Breathing Standing = new(136, 87, 58, 49, 1.5);
    private static readonly Breathing Sleeping = new(112, 114, 59, 37, 3.3);
    private static readonly Breathing Ledge = new(103, 121, 55, 34, 2.9);
    private readonly PetMotionEffect _motion = new();
    private readonly ImageBrush _previousBrush = new() { Stretch = Stretch.Fill };
    private BitmapSource? _current, _transitionFrom;
    private string _clip = "";
    private double _blendStart, _blendDuration, _time;
    private Breathing _breathing;
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
        var progress = state switch
        {
            PetState.LieDown => engine.LieProgress,
            PetState.Rise => 1 - engine.RiseProgress,
            PetState.Stretch => Math.Sin(Math.PI * Math.Clamp(engine.Age / 2.8, 0, 1)),
            _ => 1.0
        };
        var sway = state == PetState.Drag ? engine.DragSway :
            state == PetState.Fall && clip.StartsWith("held-", StringComparison.Ordinal)
                ? engine.DragSway * Math.Max(0, 1 - engine.Age / .08) : default;
        Present(sprites.Sample(clip, engine.RenderAge, engine.LoopAnimation), clip, engine.AliveTime, state, progress, sway);
    }

    public void Present(BitmapSource frame, string clip, double time, PetState state) =>
        Present(new SpriteSample(frame,frame,0),clip,time,state);

    public void Present(SpriteSample sample, string clip, double time, PetState state) =>
        Present(sample, clip, time, state, 1, default);

    private void Present(SpriteSample sample, string clip, double time, PetState state, double progress, Vector sway)
    {
        var dt = Math.Clamp(time - _time, 0, .1); _time = time;
        RenderOptions.SetBitmapScalingMode(this,sample.From.PixelWidth>SpriteCatalog.Width
            ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
        var target = BreathingFor(clip, state, progress);
        _breathing = _current is null ? target : Breathing.Blend(_breathing, target, 1 - Math.Exp(-dt * 14));
        if (clip != _clip)
        {
            _transitionFrom = Capture();
            _clip=clip; _blendStart=time;
            _blendDuration=state is PetState.Pounce or PetState.Jump or PetState.Catch or PetState.Land?.045:state==PetState.Drag?.16:.14;
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
        _motion.Breath = (.5 - .5 * Math.Cos(time * Math.Tau / 3.8)) * _breathing.Depth;
        _motion.BreathCenter = new Point(_breathing.X / Width, _breathing.Y / Height);
        _motion.BreathInverseExtent = new Point(Width / _breathing.Width, Height / _breathing.Height);
        _motion.Ear = state == PetState.Idle ? Math.Pow(Math.Max(0, Math.Sin(time * .65)), 24) * .45 : 0;
        _motion.Sway = new Point(sway.X, sway.Y);
        InvalidateVisual();
    }

    private static Breathing BreathingFor(string clip, PetState state, double progress)
    {
        var rest = clip.StartsWith("window-", StringComparison.Ordinal) ? Ledge : Sleeping;
        var pose = state switch
        {
            PetState.Doze => rest,
            PetState.LieDown or PetState.Rise => Breathing.Blend(Standing, rest, progress),
            PetState.Pet or PetState.Perch or PetState.Peek => new(120, 110, 31, 46, 1.5),
            PetState.Drag => new(116, 91, 32, 42, 1.2),
            PetState.HuntAim or PetState.Catch => new(136, 119, 51, 34, 1.4),
            PetState.Stalk or PetState.Pounce => new(136, 116, 55, 38, .7),
            PetState.Stretch => Breathing.Blend(Standing, new(117, 101, 68, 43, 1.3), progress),
            PetState.Walk or PetState.ApproachJump or PetState.Jump or PetState.Fall or PetState.Land => Standing with { Depth = .65 },
            _ when clip is "front" or "front-three-quarter" or "rear" or "rear-three-quarter" => new(120, 101, 38, 49, 1.5),
            _ => Standing
        };
        return clip.EndsWith("-left", StringComparison.Ordinal) || clip == "left-view"
            ? pose with { X = SpriteCatalog.Width - pose.X } : pose;
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
