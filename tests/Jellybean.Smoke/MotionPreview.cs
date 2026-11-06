using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Jellybean.Engine;
using Jellybean.Native;
using Jellybean.Services;
using Jellybean.UI;

internal static class MotionPreview
{
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    public static void Export(string output)
    {
        var sprites=new SpriteCatalog(); var dpi=Win32.PrimaryMonitorScale();
        var directory=Path.Combine(output,"motion-frames"); Directory.CreateDirectory(directory);
        var plane=new SurfaceProvider(dpi,0,_=>false); var area=plane.Desktop.WorkAreas[0];
        PetEngine Actor(SurfaceProvider surfaces)=>new(surfaces,new UserSettings{Autonomous=false},sprites.WalkStrideLeft,sprites.WalkStrideRight);
        void PlaceActor(PetEngine actor,double x,double y) { actor.BeginDrag(); actor.DragTo(x,y); actor.EndDrag(); actor.Idle(); }
        var actors=Enumerable.Range(0,4).Select(_=>Actor(plane)).ToArray();
        foreach(var actor in actors) PlaceActor(actor,area.Left+area.Width/2,area.Bottom);
        var starts=actors.Select(e=>e.CenterX).ToArray();
        var handles=new HashSet<nint>(); var fixtures=new List<Window>();
        try
        {
            for(var i=0;i<2;i++)
            {
                var window=new Window { Title="Jellybean motion preview surface",Width=260,Height=190,ShowActivated=false };
                window.Show(); var handle=new WindowInteropHelper(window).Handle; handles.Add(handle); fixtures.Add(window);
                Win32.Place(handle,(area.Left+100+i*420)*dpi,(area.Top+310-i*65)*dpi,260*dpi,190*dpi);
            }
            var dispatcherFrame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>dispatcherFrame.Continue=false)); Dispatcher.PushFrame(dispatcherFrame); DwmFlush();
            var windows=new SurfaceProvider(dpi,0,handles.Contains);
            var support=windows.Surfaces.First(s=>s.Window==handles.First());
            var jumper=Actor(windows); PlaceActor(jumper,support.Left+support.Width/2,support.Top);
            var views=Enumerable.Range(0,6).Select(_=>new PetVisual()).ToArray();
            foreach(var view in views) { view.Measure(new Size(240,240)); view.Arrange(new Rect(0,0,240,240)); view.UpdateLayout(); }
            var labels=new[]{"重做步态 · 连续 60 帧循环","走到边缘 → 提速 → 跳窗","小落差正常站稳 · 高落差缓冲","拎起后自然下垂 · 跟随惯性轻摆","观察 → 压低潜行 → 快速扑击","淡黄色气泡 · 按文本调整大小"};
            var bubble=new SpeechBubble(); bool walk=false,jump=false,returnJump=false,low=false,high=false,held=false,released=false,hunt=false;
            const int fps=60;
            for(var n=0;n<fps*9;n++)
            {
                var t=n/(double)fps;
                if(t>=.4&&!walk) { actors[0].Walk(); walk=true; }
                if(t>=.4&&!jump) { jumper.JumpToWindow(false); jump=true; }
                if(t>=5.2&&!returnJump) { jumper.JumpToWindow(false); returnJump=true; }
                if(t>=.5&&!low) { actors[1].BeginDrag(); actors[1].DragTo(starts[1],area.Bottom-60); actors[1].EndDrag(); low=true; }
                if(t>=3.5&&!high) { actors[1].BeginDrag(); actors[1].DragTo(starts[1],area.Bottom-180); actors[1].EndDrag(); high=true; }
                if(t>=.4&&!held) { actors[2].BeginDrag(); held=true; }
                if(held&&!released) actors[2].DragTo(starts[2]+Math.Sin(t*1.7)*48,area.Bottom-55+Math.Sin(t*2)*7);
                if(t>=6.5&&!released) { actors[2].EndDrag(); released=true; }
                actors[3].ToyPosition=new Point(starts[3]+(t<4.6?240:75)+Math.Sin(t*2.5)*22,area.Bottom-10-Math.Sin(t*3)*8);
                if(t>=.4&&!hunt) { actors[3].ToggleToy(); hunt=true; }
                foreach(var actor in actors) actor.Tick(1.0/fps); jumper.Tick(1.0/fps);
                views[0].Update(sprites,actors[0]); views[1].Update(sprites,jumper); views[2].Update(sprites,actors[1]); views[3].Update(sprites,actors[2]); views[4].Update(sprites,actors[3]);
                views[5].Present(sprites.Sample("pet",t,true),"pet",t,PetState.Pet);
                foreach(var view in views) view.UpdateLayout();
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239,238,220)),null,new Rect(0,0,1440,600));
                    for(var i=0;i<6;i++)
                    {
                        var x=i%3*480.0; var y=i/3*300.0;
                        dc.PushClip(new RectangleGeometry(new Rect(x+6,y+6,468,288)));
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(255,251,243)),null,new Rect(x+6,y+6,468,288),12,12);
                        Text(dc,labels[i],x+22,y+20,14);
                        var floor=y+260; var center=x+240; var foot=floor; var scale=1.0;
                        if(i!=1) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(217,204,185)),1),new Point(x+20,floor),new Point(x+460,floor));
                        if(i==0) center=x+(actors[0].Facing<0?370:100)+(actors[0].CenterX-starts[0]);
                        if(i==1)
                        {
                            scale=.61;
                            foreach(var edge in windows.Surfaces.Where(s=>s.Window!=0))
                            {
                                var left=x+32+(edge.Left-area.Left-100)*scale; var top=y+227+(edge.Top-area.Top-310)*scale;
                                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(231,237,231)),new Pen(new SolidColorBrush(Color.FromRgb(193,204,192)),1),new Rect(left,top,edge.Width*scale,100),5,5);
                            }
                            center=x+32+(jumper.CenterX-area.Left-100)*scale; foot=y+227+(jumper.FeetY-area.Top-310)*scale;
                        }
                        if(i==2) { scale=.68; foot+= (actors[1].FeetY-area.Bottom)*scale; Text(dc,t<3.5?"低处放下":"高处落下",x+25,y+55,12); }
                        if(i==3) { center+=(actors[2].CenterX-starts[2]); foot+=(actors[2].FeetY-area.Bottom); }
                        if(i==4)
                        {
                            scale=.94; center=x+84+(actors[3].CenterX-starts[3])*scale;
                            foot+=(actors[3].FeetY-area.Bottom)*scale;
                            var toyX=x+84+(actors[3].ToyPosition.X-starts[3])*scale; var toyY=floor+(actors[3].ToyPosition.Y-area.Bottom)*scale;
                            dc.DrawLine(new Pen(Brushes.Tan,1),new Point(toyX+26,toyY-50),new Point(toyX,toyY));
                            dc.DrawEllipse(Brushes.PaleVioletRed,null,new Point(toyX,toyY),5,9);
                            Text(dc,actors[3].State switch {PetState.HuntAim=>"瞄准",PetState.Stalk=>"潜行",PetState.Pounce=>"扑！",PetState.Catch=>"抓住",_=>""},x+25,y+54,12);
                        }
                        DrawCat(dc,views[i],center,foot,scale);
                        if(i==5)
                        {
                            bubble.SetText(t<3?"喵？":t<6?"陪我玩一会儿，好不好？":"糖豆想陪你休息一会儿。\n放下工作，陪我玩一下，好不好？",1);
                            dc.PushTransform(new TranslateTransform(x+240-bubble.Size.Width/2,y+54)); bubble.Draw(dc,bubble.Size.Width/2); dc.Pop();
                        }
                        dc.Pop();
                    }
                }
                var bitmap=new RenderTargetBitmap(1440,600,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=File.Create(Path.Combine(directory,$"{n:0000}.png")); encoder.Save(file);
            }
            Console.WriteLine("60 fps motion preview exported.");
        }
        finally { foreach(var window in fixtures) window.Close(); }
    }

    private static void Text(DrawingContext dc,string text,double x,double y,double size) => dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,new SolidColorBrush(Color.FromRgb(67,57,47)),1),new Point(x,y));
    private static void DrawCat(DrawingContext dc,PetVisual view,double center,double foot,double scale) => dc.DrawRectangle(new VisualBrush(view){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,240,240)},null,new Rect(center-120*scale,foot-164*scale,240*scale,240*scale));

    public static void CheckRenderer(SpriteCatalog sprites)
    {
        var view=new PetVisual(); view.Measure(new Size(240,240)); view.Arrange(new Rect(0,0,240,240));
        view.Present(sprites.Frame("idle-left",0),"idle-left",1,PetState.Idle); view.UpdateLayout();
        var a=Bytes(view.Capture()!);
        view.Present(sprites.Frame("idle-left",0),"idle-left",2.5,PetState.Idle); view.UpdateLayout();
        var b=Bytes(view.Capture()!);
        if(a.SequenceEqual(b)) throw new InvalidOperationException("Idle breathing did not change the rendered pixels.");
        view.Present(sprites.Frame("pet",0),"pet",2.51,PetState.Pet); view.UpdateLayout();
        view.Present(sprites.Frame("pet",0),"pet",2.58,PetState.Pet); view.UpdateLayout();
        var blend=Bytes(view.Capture()!);
        if(!Enumerable.Range(0,240*240).Any(i=>blend[i*4+3]>=253)) throw new InvalidOperationException("Pose blending reduced the solid body alpha.");
    }
    private static byte[] Bytes(BitmapSource frame)
    { var pixels=new byte[240*240*4]; new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0).CopyPixels(pixels,240*4,0); return pixels; }
}
