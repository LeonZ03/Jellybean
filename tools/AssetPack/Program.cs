using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private const int Width = 192, Height = 176, Foot = 164, Anchor = 96;
    private static readonly Dictionary<string, object> Clips = new();
    private static string Root = "", Output = "";

    [STAThread]
    private static void Main(string[] args)
    {
        Root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        Output = Path.Combine(Root, "src/Jellybean/Assets");
        Directory.CreateDirectory(Output);
        Pack("walk-left", "demo/walk-clean.png", new[] {399,399,399,398,399,399}, null, .38, new[] {140,140,140,140,140,140});
        Pack("walk-right", "assets/animations/walk-right.png", null, null, null, new[] {140,140,140,140,140,140});
        Pack("doze", "demo/doze.png", Enumerable.Repeat(406,6).ToArray(), null, .38, new[] {550,460,220,240,520,650});
        Pack("pet", "demo/pet-refined.png", new[] {473,474,474,474,474,474}, new[] {276,262,249,278,263,250}, .28, new[] {300,180,200,270,180,340});
        Pack("perch", "demo/perch.png", Enumerable.Repeat(452,6).ToArray(), new[] {419,418,404,403,418,417}, .30, new[] {460,220,280,360,250,480});
        Pack("stretch", "assets/animations/stretch.png", null, null, null, new[] {450,450,650,600,450,500});
        Pack("pounce-left", "demo/pounce.png", new[] {420,420,415,420,384,383}, null, .38, new[] {370,175,220,180,145,390});
        Pack("pounce-right", "assets/animations/pounce-right.png", null, null, null, new[] {370,175,220,180,145,390});

        var body = Load("assets/character/jellybean-turnaround.png");
        var boxes = new[] {new Int32Rect(162,31,169,441),new Int32Rect(479,32,434,440),new Int32Rect(911,33,563,439),new Int32Rect(22,520,506,449),new Int32Rect(651,520,173,472),new Int32Rect(985,520,405,455)};
        var names = new[] {"front", "front-three-quarter", "idle-left", "idle-right", "rear", "rear-three-quarter"};
        for (var i=0;i<boxes.Length;i++)
        {
            var crop = new CroppedBitmap(body, boxes[i]);
            SaveFrames(names[i], new[] {Draw(crop, boxes[i].Width/2.0, boxes[i].Height, .28)}, new[] {1000});
        }
        Alias("jump-left", "pounce-left", new[] {70,70,80,400,80,80}, new[] {0,1,2,3,4,5});
        Alias("jump-right", "pounce-right", new[] {70,70,80,400,80,80}, new[] {0,1,2,3,4,5});
        File.WriteAllText(Path.Combine(Output, "animations.json"), JsonSerializer.Serialize(new {width=Width,height=Height,anchor=Anchor,foot=Foot,clips=Clips}, new JsonSerializerOptions{WriteIndented=true}));
        using var bitmapStream = new MemoryStream();
        var face = Draw(new CroppedBitmap(body, boxes[0]), boxes[0].Width/2.0, boxes[0].Height, .28);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(face)); encoder.Save(bitmapStream);
        bitmapStream.Position=0;
        using var bitmap = new System.Drawing.Bitmap(bitmapStream);
        using var small = new System.Drawing.Bitmap(64,64);
        using (var graphics = System.Drawing.Graphics.FromImage(small))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            // Crop the face from the front view for a readable tray icon.
            graphics.DrawImage(bitmap,new System.Drawing.Rectangle(0,0,64,64),new System.Drawing.Rectangle(68,38,56,66),System.Drawing.GraphicsUnit.Pixel);
        }
        var handle = small.GetHicon();
        using (var icon = System.Drawing.Icon.FromHandle(handle))
        using (var stream=File.Create(Path.Combine(Output,"jellybean.ico"))) icon.Save(stream);
        DestroyIcon(handle);
        Console.WriteLine($"Packed {Clips.Count} clips into {Output}");
    }

    private static BitmapSource Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.UriSource=new Uri(Path.Combine(Root,path)); image.EndInit();
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth*converted.PixelHeight*4];
        converted.CopyPixels(pixels,converted.PixelWidth*4,0);
        for (var i=0;i<pixels.Length;i+=4)
            if(pixels[i+3]<8) { pixels[i]=pixels[i+1]=pixels[i+2]=pixels[i+3]=0; }
        var clean=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,converted.PixelWidth*4);
        clean.Freeze(); return clean;
    }

    private static void Pack(string name,string path,int[]? baselines,int[]? anchors,double? scale,int[] durations)
    {
        var source=Load(path);
        var cells=Enumerable.Range(0,6).Select(i=>(BitmapSource)new CroppedBitmap(source,new Int32Rect(i%3*512,i/3*512,512,512))).ToArray();
        var bounds=cells.Select(OpaqueBounds).ToArray();
        var commonScale=scale ?? Math.Min(122.0/bounds[0].Height,176.0/bounds.Max(b=>b.Width));
        var frames=new List<BitmapSource>();
        for(var i=0;i<6;i++)
            frames.Add(Draw(cells[i],anchors?[i] ?? (bounds[i].Left+bounds[i].Right)/2.0,baselines?[i] ?? bounds[i].Bottom,commonScale));
        SaveFrames(name,frames,durations);
    }

    private static Rect OpaqueBounds(BitmapSource bitmap)
    {
        var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
        var pixels=new byte[bgra.PixelWidth*bgra.PixelHeight*4]; bgra.CopyPixels(pixels,bgra.PixelWidth*4,0);
        var left=bgra.PixelWidth; var top=bgra.PixelHeight; var right=0; var bottom=0;
        for(var y=0;y<bgra.PixelHeight;y++) for(var x=0;x<bgra.PixelWidth;x++)
            if(pixels[(y*bgra.PixelWidth+x)*4+3]>=40) { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x+1); bottom=Math.Max(bottom,y+1); }
        return new Rect(left,top,right-left,bottom-top);
    }

    private static BitmapSource Draw(BitmapSource source,double anchor,double baseline,double scale)
    {
        var visual=new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
        using(var dc=visual.RenderOpen()) dc.DrawImage(source,new Rect(Math.Round(Anchor-anchor*scale),Math.Round(Foot-baseline*scale),source.PixelWidth*scale,source.PixelHeight*scale));
        var bitmap=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }

    private static void SaveFrames(string name,IReadOnlyList<BitmapSource> frames,int[] durations)
    {
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()) for(var i=0;i<frames.Count;i++) dc.DrawImage(frames[i],new Rect(i*Width,0,Width,Height));
        var atlas=new RenderTargetBitmap(Width*frames.Count,Height,96,96,PixelFormats.Pbgra32); atlas.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(atlas));
        using(var file=File.Create(Path.Combine(Output,name+".png"))) encoder.Save(file);
        Clips[name]=new {file=name+".png",frames=Enumerable.Range(0,frames.Count).ToArray(),durations};
    }

    private static void Alias(string name,string source,int[] durations,int[]? frames=null)
    {
        var json=JsonSerializer.SerializeToElement(Clips[source]);
        Clips[name]=new {file=json.GetProperty("file").GetString(),frames=frames ?? Enumerable.Repeat(0,durations.Length).ToArray(),durations};
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
