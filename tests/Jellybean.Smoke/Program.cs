using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Jellybean;
using Jellybean.Engine;
using Jellybean.Native;
using Jellybean.Services;
using Jellybean.UI;

internal static class Program
{
    private static readonly List<string> Passed = new();
    private static string Output = "";

    [STAThread]
    private static int Main(string[] args)
    {
        Output=Path.GetFullPath(args.Length>0?args[0]:"artifacts"); Directory.CreateDirectory(Output);
        var app=new App(); app.InitializeComponent();
        Window? fixture=null;
        try
        {
            if(args.Contains("--render-only")) { ExportPreview(new SpriteCatalog(),new UserSettings()); Console.WriteLine("Preview rendered."); return 0; }
            var platforms=new[]{new Surface(0,300,180),new Surface(0,300,600)};
            Check(Platforms.TryLand(platforms,100,100,650,out var landing)&&landing==180,"swept fall lands on highest crossed edge");
            Check(!Platforms.TryLand(platforms,400,100,650,out _),"fall outside a window does not snap to it");
            var dpi=Win32.PrimaryMonitorScale();
            var surfaces=new SurfaceProvider(dpi,0);
            var area=surfaces.Desktop.WorkAreas[0];
            fixture=new Window { Title="Jellybean verification surface",Width=460,Height=220,Topmost=true,Background=Brushes.White };
            fixture.Show(); var handle=new WindowInteropHelper(fixture).Handle;
            void Place(double left,double top,double width,double height)
            { Win32.Place(handle,left*dpi,top*dpi,width*dpi,height*dpi); Pump(); surfaces.Refresh(); }
            Place(area.Left+180,area.Top+250,460,220);
            var edge=surfaces.Surfaces.First(s=>s.Window==handle);
            Check(edge.Width>400,"visible native window exposes a walkable top edge");
            var settings=new UserSettings { Autonomous=false };
            var engine=new PetEngine(surfaces,settings);
            engine.BeginDrag(); engine.DragTo(edge.Left+210,edge.Top+14); engine.EndDrag();
            Check(engine.Support?.Window==handle && Math.Abs(engine.FeetY-edge.Top)<1,"drag release snaps to the window top");
            var oldX=engine.CenterX; var oldY=engine.FeetY;
            Place(area.Left+270,area.Top+300,460,220); engine.Tick(.06);
            Check(Math.Abs(engine.CenterX-oldX-90)<2 && Math.Abs(engine.FeetY-oldY-50)<2,"pet follows a moved window without drifting");
            engine.Peek(); engine.Tick(.5);
            Check(engine.State==PetState.Peek && engine.FeetY>oldY+50,"peek lowers the body behind the attached window");
            engine.Idle(); Check(Math.Abs(engine.FeetY-oldY-50)<2,"leaving peek restores its original foothold");
            fixture.WindowState=WindowState.Minimized; Pump(); engine.Tick(.06);
            Check(engine.Support?.Window!=handle && engine.State==PetState.Fall,"minimizing the support releases the pet into a fall");
            for(var i=0;i<120 && engine.State==PetState.Fall;i++) engine.Tick(.06);
            Check(engine.State==PetState.Idle && engine.Support.HasValue,"a released pet lands on a remaining surface");
            fixture.WindowState=WindowState.Normal; Place(area.Left+180,area.Bottom-300,460,220);
            edge=surfaces.Surfaces.First(s=>s.Window==handle);
            engine.BeginDrag(); engine.DragTo(edge.Left+210,area.Bottom); engine.EndDrag();
            Check(engine.JumpToWindow() && engine.State==PetState.Jump,"nearby exposed window can be selected for a jump");
            for(var i=0;i<25 && engine.State==PetState.Jump;i++) engine.Tick(.06);
            Check(engine.Support?.Window==handle,"jump finishes attached to the selected live window");
            fixture.WindowStyle=WindowStyle.None;
            var monitor=Win32.GetMonitors()[0].Monitor;
            Win32.Place(handle,monitor.Left,monitor.Top,monitor.Width,monitor.Height); fixture.Activate(); Pump();
            Check(Win32.IsForegroundFullscreen(0),"foreground fullscreen window activates the quiet-mode detector");
            fixture.WindowStyle=WindowStyle.SingleBorderWindow; Place(area.Left+180,area.Top+250,460,220);
            edge=surfaces.Surfaces.First(s=>s.Window==handle);
            engine.BeginDrag(); engine.DragTo(edge.Left+210,edge.Top); engine.EndDrag();
            fixture.Close(); fixture=null; engine.Tick(.06);
            Check(engine.Support?.Window!=handle,"closing a support window removes the attachment");

            var sprites=new SpriteCatalog();
            foreach(var clip in new[]{"front","walk-left","walk-right","pounce-left","pounce-right","doze","pet","perch","stretch","rear"})
            {
                var frame=sprites.Frame(clip,.8);
                Check(frame.PixelWidth==SpriteCatalog.Width && frame.PixelHeight==SpriteCatalog.Height && frame.IsFrozen,$"{clip} atlas decodes at runtime size");
                var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
                var pixels=new byte[SpriteCatalog.Width*SpriteCatalog.Height*4]; converted.CopyPixels(pixels,SpriteCatalog.Width*4,0);
                Check(pixels[3]==0 && pixels[^1]==0,$"{clip} corners are truly transparent");
            }
            engine.Remind(); Check(engine.Bubble is not null && engine.State==PetState.Stretch,"reminder combines a stretch with a quiet speech bubble");
            ExportPreview(sprites,new UserSettings());
            File.WriteAllText(Path.Combine(Output,"smoke-results.json"),JsonSerializer.Serialize(new{passed=Passed.Count,checks=Passed},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS: {Passed.Count} checks; preview exported to {Output}");
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { fixture?.Close(); app.Shutdown(); }
    }

    private static void Check(bool condition,string name)
    {
        if(!condition) throw new InvalidOperationException("FAIL: "+name);
        Passed.Add(name);
    }

    private static void Pump()
    {
        var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }

    private static void ExportPreview(SpriteCatalog sprites,UserSettings settings)
    {
        var visual=new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
        using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(238,239,227)),null,new Rect(0,0,1200,650));
            DrawText(dc,"糖豆 Jellybean · 首版角色与动作",36,24,24);
            DrawText(dc,"实际运行素材，按 1.6 倍大小展示",36,60,13);
            var clips=new[]{"walk-left","walk-right","pet","doze","stretch","perch"};
            var labels=new[]{"向左走","向右走","摸摸","打盹","伸懒腰","背身坐坐"};
            for(var i=0;i<clips.Length;i++)
            {
                var x=25+i%3*392; var y=90+i/3*270;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255,250,242)),null,new Rect(x,y,370,245),14,14);
                DrawText(dc,labels[i],x+18,y+14,14);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(213,200,184)),1),new Point(x+15,y+228),new Point(x+355,y+228));
                dc.DrawImage(sprites.Frame(clips[i],i==4?1.2:.5),new Rect(x-7,y-34,384,281.6));
            }
        }
        Save(visual,1200,650,"first-build-preview.png");
        var window=new SettingsWindow(settings,sprites);
        var content=(FrameworkElement)window.Content;
        content.Measure(new Size(424,641)); content.Arrange(new Rect(0,0,424,641)); content.UpdateLayout();
        ((System.Windows.Controls.ScrollViewer)content).Background=new SolidColorBrush(Color.FromRgb(255,249,241));
        Save(content,424,641,"settings-preview.png");
        window.Close();
    }

    private static void DrawText(DrawingContext dc,string text,double x,double y,int size)
    {
        dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,new SolidColorBrush(Color.FromRgb(67,57,47)),1),new Point(x,y));
    }

    private static void Save(Visual visual,int width,int height,string name)
    {
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var dc=background.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255,249,241)),null,new Rect(0,0,width,height));
        bitmap.Render(background); bitmap.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(Path.Combine(Output,name)); encoder.Save(stream);
    }
}
