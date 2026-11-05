using System.Globalization;
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

    public EffectWindow(double dpi,bool toy=false)
    {
        _dpi=dpi; _canvas.Toy=toy;
        WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=null;
        ResizeMode=ResizeMode.NoResize; ShowActivated=false; ShowInTaskbar=false; Focusable=false;
        Width=toy?120:270; Height=toy?90:105; Content=_canvas;
        SourceInitialized+=(_,_)=> { _handle=new WindowInteropHelper(this).Handle; Win32.ConfigureOverlay(_handle,true); };
    }

    public void Update(PetEngine engine,UserSettings settings,bool running)
    {
        var show=running && (_canvas.Toy?engine.ToyActive:engine.EffectRemaining>0);
        if(!show) { if(IsVisible) Hide(); return; }
        if(!IsVisible) Show();
        Topmost=settings.Topmost;
        var scale=_dpi/VisualTreeHelper.GetDpi(this).DpiScaleX;
        _canvas.LayoutTransform=new ScaleTransform(scale,scale);
        _canvas.Text=engine.Bubble; _canvas.Hearts=engine.Hearts; _canvas.Age=engine.Age;
        var x=_canvas.Toy?engine.ToyPosition.X-24:engine.CenterX-135;
        var y=_canvas.Toy?engine.ToyPosition.Y-52:engine.FeetY-engine.BodyHeight-95;
        if (!_canvas.Toy)
        {
            var area=engine.WorkArea;
            x=Math.Clamp(x,area.Left+4,Math.Max(area.Left+4,area.Right-274));
            y=Math.Max(area.Top+4,y);
        }
        Win32.Place(_handle,x*_dpi,y*_dpi,(_canvas.Toy?120:270)*_dpi,(_canvas.Toy?90:105)*_dpi);
        _canvas.InvalidateVisual();
    }

    private sealed class EffectCanvas : FrameworkElement
    {
        public bool Toy,Hearts;
        public string? Text;
        public double Age;
        protected override void OnRender(DrawingContext dc)
        {
            if(Toy)
            {
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(137,110,87)),3),new Point(110,6),new Point(74,30));
                var curve=new StreamGeometry(); using(var c=curve.Open()) { c.BeginFigure(new Point(74,30),false,false); c.QuadraticBezierTo(new Point(48,12),new Point(24,52),true,false); }
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(185,163,149)),1.3),curve);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(216,132,149)),null,new Point(24,56),5,10);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(223,178,115)),null,new Point(30,59),4,8);
                return;
            }
            if(Text is not null)
            {
                var text=new FormattedText(Text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),13,new SolidColorBrush(Color.FromRgb(67,57,47)),VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth=230 };
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255,249,240)),new Pen(new SolidColorBrush(Color.FromRgb(226,197,172)),1),new Rect(4,12,260,text.Height+28),12,12);
                dc.DrawText(text,new Point(19,25));
            }
            else if(Hearts)
            {
                var brush=new SolidColorBrush(Color.FromRgb(221,141,154));
                for(var i=0;i<3;i++)
                {
                    var x=105+i*25; var y=72-((Age*24+i*15)%55);
                    var heart=Geometry.Parse("M 0,4 C -9,-4 -12,8 0,15 C 12,8 9,-4 0,4 Z");
                    dc.PushTransform(new TranslateTransform(x,y)); dc.DrawGeometry(brush,null,heart); dc.Pop();
                }
            }
        }
    }
}
