using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Runtime.InteropServices;
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
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    [STAThread]
    private static int Main(string[] args)
    {
        Output=Path.GetFullPath(args.Length>0?args[0]:"artifacts"); Directory.CreateDirectory(Output);
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/Jellybean;component/UI/Theme.xaml") });
        Window? fixture=null;
        try
        {
            if(args.Contains("--render-only")) { ExportPreview(new SpriteCatalog(),new UserSettings()); Console.WriteLine("Preview rendered."); return 0; }
            if(args.Contains("--motion-preview")) { MotionPreview.Export(Output); return 0; }
            if(args.Contains("--renderer-only")) { MotionPreview.CheckRenderer(new SpriteCatalog()); ExportPreview(new SpriteCatalog(),new UserSettings()); Console.WriteLine("Renderer checks passed."); return 0; }
            var platforms=new[]{new Surface(0,300,180),new Surface(0,300,600)};
            Check(Platforms.TryLand(platforms,100,100,650,out var landing)&&landing==180,"swept fall lands on highest crossed edge");
            Check(!Platforms.TryLand(platforms,400,100,650,out _),"fall outside a window does not snap to it");
            var dpi=Win32.PrimaryMonitorScale();
            var testHandles=new HashSet<nint>();
            var surfaces=new SurfaceProvider(dpi,0,window=>testHandles.Contains(window));
            var area=surfaces.Desktop.WorkAreas[0];
            fixture=new Window { Title="Jellybean verification surface",Width=460,Height=220,Topmost=true,ShowActivated=false,Background=Brushes.White };
            fixture.Show(); var handle=new WindowInteropHelper(fixture).Handle;
            testHandles.Add(handle);
            void Place(double left,double top,double width,double height)
            { Win32.Place(handle,left*dpi,top*dpi,width*dpi,height*dpi); Pump(); DwmFlush(); surfaces.Refresh(); }
            Place(area.Left+180,area.Top+250,460,220);
            if(!surfaces.Surfaces.Any(s=>s.Window==handle))
            {
                Win32.GetWindowRect(handle,out var actual); Win32.GetVisibleRect(handle,out var visible);
                throw new InvalidOperationException($"Fixture not enumerated: visible={Win32.IsWindowVisible(handle)} iconic={Win32.IsIconic(handle)} cloaked={Win32.IsCloaked(handle)} titleLength={Win32.GetWindowTextLength(handle)} ex={Win32.GetWindowLong(handle,Win32.GWL_EXSTYLE):X} rect={actual.Left},{actual.Top},{actual.Right},{actual.Bottom} dwm={visible.Left},{visible.Top},{visible.Right},{visible.Bottom} scale={dpi} area={area} handle={handle}");
            }
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
            Check(engine.State==PetState.Land && engine.Support.HasValue,"a released pet cushions its landing on a remaining surface");
            engine.Tick(.6);
            Check(engine.State==PetState.Idle,"landing recovers into idle");
            fixture.WindowState=WindowState.Normal; Place(area.Left+180,area.Bottom-300,460,220);
            edge=surfaces.Surfaces.First(s=>s.Window==handle);
            engine.BeginDrag(); engine.DragTo(edge.Left+210,area.Bottom); engine.EndDrag();
            Check(engine.JumpToWindow() && engine.State==PetState.Jump,"nearby exposed window can be selected for a jump");
            for(var i=0;i<25 && engine.State==PetState.Jump;i++) engine.Tick(.06);
            Check(engine.Support?.Window==handle,"jump finishes attached to the selected live window");
            fixture.WindowStyle=WindowStyle.None;
            var monitor=Win32.GetMonitors()[0].Monitor;
            Win32.Place(handle,monitor.Left,monitor.Top,monitor.Width,monitor.Height); Pump(); DwmFlush();
            Check(Win32.IsWindowFullscreen(handle,0),"fullscreen window bounds are detected without changing user focus");
            Check(GameDetector.IsProductivityApp("wps") && GameDetector.IsProductivityApp("wpp") && GameDetector.IsProductivityApp("et"),"WPS full-screen document modes are excluded from game hiding");
            fixture.WindowStyle=WindowStyle.SingleBorderWindow; Place(area.Left+180,area.Top+250,460,220);
            edge=surfaces.Surfaces.First(s=>s.Window==handle);
            engine.BeginDrag(); engine.DragTo(edge.Left+210,edge.Top); engine.EndDrag();
            fixture.Close(); fixture=null; engine.Tick(.06);
            Check(engine.Support?.Window!=handle,"closing a support window removes the attachment");

            var sprites=new SpriteCatalog();
            MotionPreview.CheckRenderer(sprites);
            Check(true,"breathing changes rendered pixels and pose blending retains solid alpha");
            foreach(var clip in new[]{"front","walk-left","walk-right","pounce-left","pounce-right","doze","pet","perch","stretch","rear"})
            {
                var frame=sprites.Frame(clip,.8);
                Check(frame.PixelWidth==SpriteCatalog.Width && frame.PixelHeight==SpriteCatalog.Height && frame.IsFrozen,$"{clip} atlas decodes at runtime size");
                var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
                var pixels=new byte[SpriteCatalog.Width*SpriteCatalog.Height*4]; converted.CopyPixels(pixels,SpriteCatalog.Width*4,0);
                Check(pixels[3]==0 && pixels[^1]==0,$"{clip} corners are truly transparent");
            }
            engine.Remind();
            for(var tick=0;tick<100 && engine.Bubble is null;tick++) engine.Tick(.03);
            Check(engine.Bubble is not null && engine.State==PetState.Stretch,"reminder waits for landing then combines stretch and speech");
            CheckMotionBehavior(surfaces,sprites,area);
            CheckRandomWindows(dpi,area);
            var panel=new SettingsWindow(new UserSettings(),sprites); double previewScale=0;
            panel.ScalePreview+=scale=>previewScale=scale;
            ((System.Windows.Controls.Slider)panel.FindName("SizeSlider")).Value=130;
            Check(Math.Abs(previewScale-1.3)<.001,"size slider emits its new scale before saving"); panel.Close();
            ExportPreview(sprites,new UserSettings());
            File.WriteAllText(Path.Combine(Output,"smoke-results.json"),JsonSerializer.Serialize(new{passed=Passed.Count,checks=Passed},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"PASS: {Passed.Count} checks; preview exported to {Output}");
            return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { fixture?.Close(); app.Shutdown(); }
    }

    private static void CheckMotionBehavior(SurfaceProvider surfaces,SpriteCatalog sprites,Rect area)
    {
        var engine=new PetEngine(surfaces,new UserSettings{Autonomous=false},sprites.WalkStrideLeft,sprites.WalkStrideRight);
        engine.BeginDrag(); engine.DragTo(area.Left+area.Width/2,area.Bottom); engine.EndDrag(); engine.Idle();
        engine.Walk(); var x=engine.CenterX;
        for(var i=0;i<8;i++) engine.Tick(.03);
        Check(Math.Abs(Math.Abs(engine.CenterX-x)-engine.RenderAge/PetEngine.WalkCycleSeconds*engine.WalkStride)<.01,"walk phase follows real distance through acceleration");
        var pose=engine.Animation; var age=engine.RenderAge;
        engine.BeginDrag(); engine.Tick(.2);
        Check(engine.Animation==pose && Math.Abs(engine.RenderAge-age)<.001,"drag holds the captured pose and frame");
        engine.EndDrag(); engine.Tick(.6); engine.Doze(); engine.Tick(.9);
        Check(engine.State==PetState.Doze,"lying down reaches a breathing rest pose");
        engine.Pet(); Check(engine.State==PetState.Rise,"interaction wakes a resting cat before petting");
        engine.Tick(.8); Check(engine.State==PetState.Pet,"wake transition completes the requested interaction");
        engine.Idle(); engine.Tick(181);
        Check(engine.Bubble?.Contains("陪我玩")==true,"long inactivity requests a little play");
        engine.Interact(); engine.Idle(); engine.Tick(20);
        Check(engine.Bubble is null,"interaction resets the attention request");
        var tail=sprites.HitMask(sprites.Frame("window-rest",1.5));
        Check(tail.Skip(SpriteCatalog.Width*(SpriteCatalog.Foot+5)).Any(a=>a>100),"window resting pose extends its tail below the support line");
    }

    private static void CheckRandomWindows(double dpi,Rect area)
    {
        var handles=new HashSet<nint>(); var windows=new List<Window>();
        try
        {
            for(var i=0;i<3;i++)
            {
                var window=new Window {Title="Jellybean jump surface "+i,Width=260,Height=170,Topmost=true,ShowActivated=false};
                window.Show(); var handle=new WindowInteropHelper(window).Handle; handles.Add(handle); windows.Add(window);
                Win32.Place(handle,(area.Left+70+i*300)*dpi,(area.Top+220+i*100)*dpi,260*dpi,170*dpi);
            }
            Pump(); DwmFlush();
            var provider=new SurfaceProvider(dpi,0,handles.Contains);
            var actor=new PetEngine(provider,new UserSettings{Autonomous=false});
            actor.BeginDrag(); actor.DragTo(area.Left+400,area.Bottom); actor.EndDrag(); actor.Idle();
            var visited=new HashSet<nint>();
            for(var n=0;n<8;n++)
            {
                if(!actor.JumpToWindow(false)) throw new InvalidOperationException("No jump target in three-window scene.");
                for(var tick=0;tick<100 && actor.State==PetState.Jump;tick++) actor.Tick(.03);
                if(actor.Support is { } support) visited.Add(support.Window);
                actor.Tick(.6);
            }
            Check(visited.Count==3,"autonomous target selection reaches different open windows");
        }
        finally { foreach(var window in windows) window.Close(); }
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
            DrawText(dc,"糖豆 Jellybean · v0.2 角色与动作",36,24,24);
            DrawText(dc,"实际运行素材，按 1.6 倍大小展示",36,60,13);
            var clips=new[]{"walk-left","walk-right","pet","doze","stretch","perch"};
            var labels=new[]{"向左走","向右走","摸摸","打盹","伸懒腰","背身坐坐"};
            for(var i=0;i<clips.Length;i++)
            {
                var x=25+i%3*392; var y=90+i/3*270;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255,250,242)),null,new Rect(x,y,370,245),14,14);
                DrawText(dc,labels[i],x+18,y+14,14);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(213,200,184)),1),new Point(x+15,y+228),new Point(x+355,y+228));
                dc.DrawImage(sprites.Frame(clips[i],i==4?1.2:.5),new Rect(x-7,y-34,384,384));
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
