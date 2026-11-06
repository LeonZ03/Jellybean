using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Jellybean.UI;

internal sealed class SpeechBubble
{
    private static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(67,57,47));
    private static readonly Brush Paper=new SolidColorBrush(Color.FromRgb(255,246,214));
    private static readonly Pen Outline=new(new SolidColorBrush(Color.FromRgb(220,198,151)),1);
    private FormattedText? _layout;
    private string _text="";
    private double _dpi;
    public Size Size { get; private set; }

    public void SetText(string text,double pixelsPerDip)
    {
        if (_layout is not null && text==_text && pixelsPerDip==_dpi) return;
        _text=text; _dpi=pixelsPerDip;
        _layout=new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"),13,Ink,pixelsPerDip);
        if (_layout.WidthIncludingTrailingWhitespace>230) _layout.MaxTextWidth=230;
        Size=new Size(Math.Max(66,Math.Ceiling(_layout.WidthIncludingTrailingWhitespace)+30),Math.Ceiling(_layout.Height)+40);
    }

    public void Draw(DrawingContext dc,double tailX)
    {
        if (_layout is null) return;
        var right=Size.Width-2; var bottom=Size.Height-12;
        tailX=Math.Clamp(tailX,18,right-16);
        var bubble=new StreamGeometry();
        using(var c=bubble.Open())
        {
            c.BeginFigure(new Point(13,2),true,true); c.LineTo(new Point(right-11,2),true,false);
            c.QuadraticBezierTo(new Point(right,2),new Point(right,13),true,false);
            c.LineTo(new Point(right,bottom-11),true,false); c.QuadraticBezierTo(new Point(right,bottom),new Point(right-11,bottom),true,false);
            c.LineTo(new Point(tailX+8,bottom),true,false); c.LineTo(new Point(tailX,bottom+10),true,false); c.LineTo(new Point(tailX-8,bottom),true,false);
            c.LineTo(new Point(13,bottom),true,false); c.QuadraticBezierTo(new Point(2,bottom),new Point(2,bottom-11),true,false);
            c.LineTo(new Point(2,13),true,false); c.QuadraticBezierTo(new Point(2,2),new Point(13,2),true,false);
        }
        dc.DrawGeometry(Paper,Outline,bubble);
        dc.DrawText(_layout,new Point(15,13));
    }
}
