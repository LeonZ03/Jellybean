using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jellybean.Engine;
using Jellybean.Services;
using Jellybean.UI;

internal static class MotionPreview
{
    public static void Export(string output)
    {
        var sprites=new SpriteCatalog();
        var directory=Path.Combine(output,"motion-frames"); Directory.CreateDirectory(directory);
        var surfaces=new SurfaceProvider(1,0,_=>false);
        var area=surfaces.Desktop.WorkAreas[0];
        var engines=Enumerable.Range(0,4).Select(_=>new PetEngine(surfaces,new UserSettings{Autonomous=false},sprites.WalkStrideLeft,sprites.WalkStrideRight)).ToArray();
        foreach(var engine in engines) { engine.BeginDrag(); engine.DragTo(area.Left+area.Width/2,area.Bottom); engine.EndDrag(); engine.Idle(); }
        var starts=engines.Select(e=>e.CenterX).ToArray();
        var views=Enumerable.Range(0,6).Select(_=>new PetVisual()).ToArray();
        foreach(var view in views) { view.Measure(new Size(240,240)); view.Arrange(new Rect(0,0,240,240)); view.UpdateLayout(); }
        var labels=new[]{"持续呼吸","脚步与位移同步","保持姿势拖动 → 落地缓冲","趴窗沿，尾巴垂入窗口","动作衔接与体型","更瘦 · 长耳朵 · 委屈眼神"};
        bool walking=false,petted=false,dragging=false,released=false,stretch=false;
        const int fps=24;
        for(var n=0;n<fps*8;n++)
        {
            var t=n/(double)fps;
            if(t>=.7&&!walking) { engines[1].Walk(); walking=true; }
            if(t>=.4&&!petted) { engines[2].Doze(); engines[3].Pet(); petted=true; }
            if(t>=1.7&&!dragging) { views[2].Freeze(); engines[2].BeginDrag(); dragging=true; }
            if(dragging&&!released) engines[2].DragTo(starts[2]+Math.Sin(t*2)*55,area.Bottom-125);
            if(t>=3.2&&!released) { engines[2].EndDrag(); released=true; }
            if(t>=3.1&&!stretch) { engines[3].Stretch(); stretch=true; }
            foreach(var engine in engines) engine.Tick(1.0/fps);
            views[0].Update(sprites,engines[0]); views[1].Update(sprites,engines[1]); views[2].Update(sprites,engines[2]);
            var windowClip=t<.86?"window-lie":"window-rest";
            views[3].Present(sprites.Frame(windowClip,t<.86?t:t-.86,t>=.86),windowClip,t,t<.86?PetState.LieDown:PetState.Doze);
            views[4].Update(sprites,engines[3]);
            views[5].Present(sprites.Frame("pet",t),"pet",t,PetState.Pet);
            foreach(var view in views) view.UpdateLayout();
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Beige,null,new Rect(0,0,960,840));
                for(var i=0;i<6;i++)
                {
                    var x=i%2*480; var y=i/2*280;
                    dc.PushClip(new RectangleGeometry(new Rect(x+6,y+6,468,268)));
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255,250,242)),null,new Rect(x+6,y+6,468,268),12,12);
                    dc.DrawText(new FormattedText(labels[i],CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),15,new SolidColorBrush(Color.FromRgb(67,57,47)),1),new Point(x+24,y+20));
                    var floor=i==3?y+173:y+223;
                    if(i==3)
                    {
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(230,236,228)),new Pen(new SolidColorBrush(Color.FromRgb(197,207,193)),1),new Rect(x+28,floor,424,115),7,7);
                        dc.DrawText(new FormattedText("窗口内容",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,Brushes.Gray,1),new Point(x+45,floor+18));
                    }
                    else dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(215,202,186)),1),new Point(x+20,floor),new Point(x+460,floor));
                    double center=x+240,foot=floor;
                    if(i==1) center=x+(engines[1].Facing<0?365:110)+(engines[1].CenterX-starts[1])*.85;
                    if(i==2) { center=x+240+(engines[2].CenterX-starts[2])*.85; foot+= (engines[2].FeetY-area.Bottom)*.85; }
                    dc.DrawRectangle(new VisualBrush(views[i]){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,240,240)},null,new Rect(center-120*.85,foot-164*.85,240*.85,240*.85));
                    if(i==5)
                    {
                        dc.PushTransform(new TranslateTransform(x+110,y+43));
                        var shape=new StreamGeometry(); using(var c=shape.Open())
                        { c.BeginFigure(new Point(16,12),true,true); c.LineTo(new Point(244,12),true,false); c.QuadraticBezierTo(new Point(256,12),new Point(256,24),true,false); c.LineTo(new Point(256,48),true,false); c.QuadraticBezierTo(new Point(256,60),new Point(244,60),true,false); c.LineTo(new Point(140,60),true,false); c.LineTo(new Point(130,71),true,false); c.LineTo(new Point(122,60),true,false); c.LineTo(new Point(16,60),true,false); c.QuadraticBezierTo(new Point(4,60),new Point(4,48),true,false); c.LineTo(new Point(4,24),true,false); c.QuadraticBezierTo(new Point(4,12),new Point(16,12),true,false); }
                        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255,249,240)),new Pen(new SolidColorBrush(Color.FromRgb(226,197,172)),1),shape);
                        dc.DrawText(new FormattedText("陪我玩一会儿，好不好？",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),13,new SolidColorBrush(Color.FromRgb(67,57,47)),1),new Point(19,26)); dc.Pop();
                    }
                    dc.Pop();
                }
            }
            var bitmap=new RenderTargetBitmap(960,840,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(directory,$"{n:0000}.png")); encoder.Save(file);
        }
        Console.WriteLine("Motion preview frames exported.");
    }

    public static void CheckRenderer(SpriteCatalog sprites)
    {
        var view=new PetVisual(); view.Measure(new Size(240,240)); view.Arrange(new Rect(0,0,240,240));
        view.Present(sprites.Frame("idle-left",0),"idle-left",1,PetState.Idle); view.UpdateLayout();
        var a=Bytes(view.Capture()!);
        view.Present(sprites.Frame("idle-left",0),"idle-left",2.5,PetState.Idle); view.UpdateLayout();
        var b=Bytes(view.Capture()!);
        if(a.SequenceEqual(b)) throw new InvalidOperationException("Idle breathing did not change the rendered pixels.");
        view.Present(sprites.Frame("pet",0),"pet",2.51,PetState.Pet); view.UpdateLayout();
        view.Present(sprites.Frame("pet",0),"pet",2.62,PetState.Pet); view.UpdateLayout();
        var blend=Bytes(view.Capture()!);
        if(!Enumerable.Range(0,240*240).Any(i=>blend[i*4+3]>=253)) throw new InvalidOperationException("Pose blending reduced the solid body alpha.");
    }
    private static byte[] Bytes(BitmapSource frame)
    { var pixels=new byte[240*240*4]; new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0).CopyPixels(pixels,240*4,0); return pixels; }
}
