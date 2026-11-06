using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Jellybean.Engine;
using Jellybean.Native;
using Jellybean.Services;

namespace Jellybean.UI;

public sealed class EffectWindow : Window
{
    private readonly EffectCanvas _canvas = new();
    private readonly double _dpi;
    private nint _handle;
    private Rect _placed;

    public EffectWindow(double dpi,bool toy=false)
    {
        _dpi=dpi; _canvas.Toy=toy;
        WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=null;
        ResizeMode=ResizeMode.NoResize; ShowActivated=false; ShowInTaskbar=false; Focusable=false;
        Width=toy?120:270; Height=toy?90:105; Content=_canvas;
        SourceInitialized+=(_,_)=> { _handle=new WindowInteropHelper(this).Handle; Win32.ConfigureOverlay(_handle,true); };
    }

    public void Update(PetEngine engine,UserSettings settings,bool running,double? headHeight=null)
    {
        var show=running && (_canvas.Toy?engine.ToyActive:engine.EffectRemaining>0);
        if(!show) { if(IsVisible) Hide(); return; }
        if(!IsVisible) Show();
        Topmost=settings.Topmost;
        var scale=_dpi/VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (_canvas.LayoutTransform is not ScaleTransform old || Math.Abs(old.ScaleX-scale)>.0001)
            _canvas.LayoutTransform=new ScaleTransform(scale,scale);
        var redraw=_canvas.Toy || engine.Hearts || _canvas.Text!=engine.Bubble;
        _canvas.Text=engine.Bubble; _canvas.Hearts=engine.Hearts; _canvas.Age=engine.AliveTime; _canvas.Caught=engine.ToyCaught;
        _canvas.MeasureContent();
        var size=_canvas.ContentSize;
        var x=_canvas.Toy?engine.ToyPosition.X-24:engine.CenterX-size.Width/2;
        var y=_canvas.Toy?engine.ToyPosition.Y-52:engine.FeetY-(headHeight??engine.BodyHeight)-size.Height-8;
        if (!_canvas.Toy)
        {
            var area=engine.WorkArea;
            x=Math.Clamp(x,area.Left+4,Math.Max(area.Left+4,area.Right-size.Width-4));
            y=Math.Max(area.Top+4,y);
            var tail=Math.Clamp(engine.CenterX-x,18,size.Width-18);
            redraw|=Math.Abs(tail-_canvas.TailX)>.01; _canvas.TailX=tail;
        }
        var bounds=new Rect(Math.Round(x*_dpi),Math.Round(y*_dpi),Math.Ceiling(size.Width*_dpi),Math.Ceiling(size.Height*_dpi));
        if(bounds!=_placed) { Win32.Place(_handle,bounds.X,bounds.Y,bounds.Width,bounds.Height); _placed=bounds; }
        if(redraw) _canvas.InvalidateVisual();
    }

    public void RaiseAboveApps() { if (IsVisible) Win32.RaiseOverlay(_handle); }

    private sealed class EffectCanvas : FrameworkElement
    {
        public bool Toy,Hearts,Caught;
        public string? Text;
        public double Age, TailX=135;
        private readonly SpeechBubble _bubble=new();
        public Size ContentSize => Toy?new Size(120,90):Text is null?new Size(160,95):_bubble.Size;
        public void MeasureContent() { if(Text is not null) _bubble.SetText(Text,VisualTreeHelper.GetDpi(this).PixelsPerDip); }
        protected override void OnRender(DrawingContext dc)
        {
            if(Toy)
            {
                if(Caught) dc.PushTransform(new RotateTransform(Math.Sin(Age*70)*9,24,52));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(137,110,87)),3),new Point(110,6),new Point(74,30));
                var curve=new StreamGeometry(); using(var c=curve.Open()) { c.BeginFigure(new Point(74,30),false,false); c.QuadraticBezierTo(new Point(48,12),new Point(24,52),true,false); }
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(185,163,149)),1.3),curve);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(216,132,149)),null,new Point(24,56),5,10);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(223,178,115)),null,new Point(30,59),4,8);
                if(Caught) dc.Pop();
                return;
            }
            if(Text is not null)
            {
                _bubble.Draw(dc,TailX);
            }
            else if(Hearts)
            {
                var brush=new SolidColorBrush(Color.FromRgb(221,141,154));
                for(var i=0;i<3;i++)
                {
                    var x=55+i*25; var y=72-((Age*24+i*15)%55);
                    var heart=Geometry.Parse("M 0,4 C -9,-4 -12,8 0,15 C 12,8 9,-4 0,4 Z");
                    dc.PushTransform(new TranslateTransform(x,y)); dc.DrawGeometry(brush,null,heart); dc.Pop();
                }
            }
        }
    }
}
