using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jellybean.Engine;

namespace Jellybean.UI;

public sealed class PetVisual : FrameworkElement
{
    private readonly PetMotionEffect _motion = new();
    private readonly ImageBrush _previousBrush = new() { Stretch = Stretch.Fill };
    private BitmapSource? _current, _transitionFrom;
    private string _clip = "";
    private double _blendStart, _blendDuration, _time, _breathWeight = 1, _sway;
    public BitmapSource? CurrentFrame => _current;
    public BitmapSource? PreviousFrame => _previousBrush.ImageSource as BitmapSource;

    public PetVisual()
    {
        Width = SpriteCatalog.Width; Height = SpriteCatalog.Height;
        _motion.Previous = _previousBrush; Effect = _motion;
    }

    public void Update(SpriteCatalog sprites, PetEngine engine)
    {
        var dt=Math.Clamp(engine.AliveTime-_time,0,.1);
        Present(sprites.Sample(engine.Animation, engine.RenderAge, engine.LoopAnimation), engine.Animation, engine.AliveTime, engine.State);
        var target=engine.State==PetState.Drag?engine.DragSway+Math.Sin(engine.AliveTime*2.4)*.6:0;
        _sway+=(target-_sway)*(1-Math.Exp(-dt*18));
        _motion.Sway=_sway*Math.PI/180;
    }

    public void Present(BitmapSource frame, string clip, double time, PetState state) =>
        Present(new SpriteSample(frame,frame,0),clip,time,state);

    public void Present(SpriteSample sample, string clip, double time, PetState state)
    {
        var dt = Math.Clamp(time - _time, 0, .1); _time = time;
        var targetBreath = state == PetState.Doze ? 1.35 : state is PetState.Walk or PetState.Stalk or PetState.ApproachJump or PetState.Pounce or PetState.Fall or PetState.Jump ? .25 : state==PetState.Drag?.45:1;
        _breathWeight += (targetBreath - _breathWeight) * (1 - Math.Exp(-dt * 8));
        if (clip != _clip)
        {
            _transitionFrom = Capture();
            _clip=clip; _blendStart=time;
            _blendDuration=state is PetState.Pounce or PetState.Jump or PetState.Catch?.07:state==PetState.Drag?.16:.14;
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
        _motion.Breath=(Math.Sin(time*Math.PI*2/3.4)+1)*.7*_breathWeight;
        _motion.Ear=Math.Pow(Math.Max(0,Math.Sin(time*.65)),24)*.65;
        InvalidateVisual();
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
