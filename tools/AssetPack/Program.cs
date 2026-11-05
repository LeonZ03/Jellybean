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
    private const int Width=240, Height=240, Foot=164, Anchor=120;
    private static readonly Dictionary<string,object> Clips=new();
    private static readonly List<object> Calibration=new();
    private static readonly List<double> Strides=new();
    private static string Root="", Output="";

    [STAThread]
    private static void Main(string[] args)
    {
        Root=Path.GetFullPath(args.Length>0?args[0]:"."); Output=Path.Combine(Root,"src/Jellybean/Assets"); Directory.CreateDirectory(Output);
        var body=Load("assets/animations/v2/turnaround.png");
        var regions=new[]{new Int32Rect(130,0,230,486),new Int32Rect(460,0,443,486),new Int32Rect(903,0,633,486),new Int32Rect(12,488,540,536),new Int32Rect(620,488,240,536),new Int32Rect(965,488,500,536)};
        var views=regions.Select(r=>(BitmapSource)new CroppedBitmap(body,r)).ToArray();
        var names=new[]{"front","front-three-quarter","left-view","right-view","rear","rear-three-quarter"};
        for(var i=0;i<views.Length;i++)
        {
            var b=Bounds(views[i]); var s=124/b.Height;
            Save(names[i],new[]{Draw(views[i],(b.Left+b.Right)/2,b.Bottom,s)},new[]{1000});
        }
        Pack("walk-left",4,3,Enumerable.Repeat(100,12).ToArray());
        Pack("walk-right",4,3,Enumerable.Repeat(100,12).ToArray());
        Pack("pounce-left",3,2,new[]{200,160,190,450,170,200});
        Pack("pounce-right",3,2,new[]{200,160,190,450,170,200});
        Pack("pet",3,2,new[]{380,300,260,280,300,580});
        Pack("perch",3,2,new[]{600,650,300,450,700,700});
        Pack("stretch",3,2,new[]{200,400,500,700,500,500});
        Pack("rest-left",3,2,new[]{100,220,240,300,250,1000},sourceName:"doze");
        Pack("rest-window",3,2,new[]{100,220,240,300,900,900},new[]{405,405,398,324,326,325},"window-rest");
        foreach(var direction in new[]{"left","right"})
        {
            var source="pounce-"+direction;
            Alias("idle-"+direction,"walk-"+direction,new[]{0},new[]{1000});
            Alias("takeoff-"+direction,source,new[]{0,1,2},new[]{70,90,100});
            Alias("fall-"+direction,source,new[]{3},new[]{1000});
            Alias("land-"+direction,source,new[]{4,2,1,0},new[]{90,80,150,180});
            Alias("jump-"+direction,source,new[]{0,1,2,3,4,0},new[]{70,90,100,600,150,150});
        }
        Alias("lie-left","rest-left",new[]{0,1,2,3},new[]{100,220,240,300});
        Alias("doze","rest-left",new[]{5,4,5},new[]{2400,220,1800});
        Alias("rise-left","rest-left",new[]{3,2,1,0},new[]{120,200,230,230});
        Alias("window-lie","rest-window",new[]{0,1,2,3},new[]{100,220,240,300});
        Alias("window-rest","rest-window",new[]{3,4,5,4},new[]{700,900,700,900});
        Alias("window-rise","rest-window",new[]{3,2,1,0},new[]{120,200,230,230});
        var options=new JsonSerializerOptions{WriteIndented=true};
        File.WriteAllText(Path.Combine(Output,"animations.json"),JsonSerializer.Serialize(new{width=Width,height=Height,anchor=Anchor,foot=Foot,walkStrideLeft=Math.Round(Strides[0],1),walkStrideRight=Math.Round(Strides[1],1),clips=Clips},options));
        Directory.CreateDirectory(Path.Combine(Root,"artifacts"));
        File.WriteAllText(Path.Combine(Root,"artifacts/animation-calibration.json"),JsonSerializer.Serialize(Calibration,options));
        ShaderCompiler.Compile(Path.Combine(Root,"src/Jellybean/Shaders/PetMotion.hlsl"),Path.Combine(Output,"pet-motion.ps"));
        MakeIcon(Load("src/Jellybean/Assets/front.png"));
        Console.WriteLine($"Packed {Clips.Count} clips; one anatomical scale per sheet, feet fixed at {Foot}.");
    }

    private static BitmapSource Load(string path)
    {
        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.UriSource=new Uri(Path.Combine(Root,path)); image.EndInit();
        var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
        var pixels=new byte[converted.PixelWidth*converted.PixelHeight*4]; converted.CopyPixels(pixels,converted.PixelWidth*4,0);
        for(var i=0;i<pixels.Length;i+=4) if(pixels[i+3]<8) pixels[i]=pixels[i+1]=pixels[i+2]=pixels[i+3]=0;
        var clean=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,converted.PixelWidth*4); clean.Freeze(); return clean;
    }

    private static void Pack(string name,int columns,int rows,int[] durations,int[]? baselines=null,string? sourceName=null)
    {
        var source=Load("assets/animations/v2/"+(sourceName??name)+".png");
        var cells=Enumerable.Range(0,columns*rows).Select(i=>
        {
            var x=(int)Math.Round(i%columns*source.PixelWidth/(double)columns);
            var y=(int)Math.Round(i/columns*source.PixelHeight/(double)rows);
            var right=(int)Math.Round((i%columns+1)*source.PixelWidth/(double)columns);
            var bottom=(int)Math.Round((i/columns+1)*source.PixelHeight/(double)rows);
            return (BitmapSource)new CroppedBitmap(source,new Int32Rect(x,y,right-x,bottom-y));
        }).ToArray();
        var first=Bounds(cells[0]);
        // Standing reference frames share a height; compact resting poses keep that anatomical unit.
        var scale=(name is "pet" or "perch"?112.0:124.0)/first.Height;
        var anchor=MassCenterX(cells[0]);
        var frames=new List<BitmapSource>();
        for(var i=0;i<cells.Length;i++)
        {
            var b=Bounds(cells[i]);
            var baseline=baselines?[i]??b.Bottom;
            frames.Add(Draw(cells[i],anchor,baseline,scale));
        }
        Save(name,frames,durations);
        double? stride=null;
        if(name.StartsWith("walk-")) { stride=MeasureStride(cells,scale,name.EndsWith("left")); Strides.Add(stride.Value); }
        Calibration.Add(new{name,scale,headGap=HeadGap(cells[0])*scale,referenceHeight=first.Height*scale,stride});
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
        var pixels=new byte[bgra.PixelWidth*bgra.PixelHeight*4]; bgra.CopyPixels(pixels,bgra.PixelWidth*4,0); return pixels;
    }
    private static Rect Bounds(BitmapSource bitmap)
    {
        var pixels=Pixels(bitmap); var left=bitmap.PixelWidth; var top=bitmap.PixelHeight; var right=0; var bottom=0;
        for(var y=0;y<bitmap.PixelHeight;y++) for(var x=0;x<bitmap.PixelWidth;x++)
            if(pixels[(y*bitmap.PixelWidth+x)*4+3]>=40) { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x+1); bottom=Math.Max(bottom,y+1); }
        return new Rect(left,top,right-left,bottom-top);
    }
    private static double HeadGap(BitmapSource bitmap)
    {
        var b=Bounds(bitmap); var pixels=Pixels(bitmap);
        for(var y=(int)(b.Top+b.Height*.15);y<b.Top+b.Height*.6;y++)
        {
            var count=0;
            for(var x=(int)b.Left;x<b.Right;x++)
            {
                var p=(y*bitmap.PixelWidth+x)*4;
                if(pixels[p+3]>128 && pixels[p+2]>pixels[p+1]*1.13 && pixels[p]>pixels[p+1]*1.03 && pixels[p]>90) count++;
            }
            if(count>=4) return y-b.Top;
        }
        return b.Height*.36;
    }
    private static double MassCenterX(BitmapSource bitmap)
    {
        var pixels=Pixels(bitmap); double sum=0,total=0;
        for(var y=0;y<bitmap.PixelHeight;y++) for(var x=0;x<bitmap.PixelWidth;x++)
        { var a=pixels[(y*bitmap.PixelWidth+x)*4+3]; if(a>40) {sum+=x*a;total+=a;} }
        return sum/total;
    }
    private static double MeasureStride(BitmapSource[] cells,double scale,bool left)
    {
        double[] Toes(BitmapSource image)
        {
            var p=Pixels(image); var b=Bounds(image); var center=MassCenterX(image);
            var toes=new List<double>(); int start=-1;
            for(var x=0;x<image.PixelWidth;x++)
            {
                var hit=false;
                if(left?x<center:x>center)
                    for(var y=(int)b.Bottom-7;y<b.Bottom;y++) if(p[(y*image.PixelWidth+x)*4+3]>100) { hit=true; break; }
                if(hit && start<0) start=x;
                if(!hit && start>=0) { if(x-start>=3) toes.Add((start+x)/2.0); start=-1; }
            }
            return toes.ToArray();
        }
        var positions=cells.Select(Toes).ToArray(); var deltas=new List<double>();
        for(var i=0;i<cells.Length;i++)
        {
            var candidates=positions[i].SelectMany(a=>positions[(i+1)%cells.Length].Select(b=>(left?b-a:a-b)))
                .Where(d=>d>2 && d<cells[i].PixelWidth*.15).Order().ToArray();
            if(candidates.Length>0) deltas.Add(candidates[0]);
        }
        return deltas.Count>=4?Math.Clamp(deltas.Order().ElementAt(deltas.Count/2)*scale*cells.Length,48,140):90;
    }
    private static BitmapSource Draw(BitmapSource source,double anchor,double baseline,double scale)
    {
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen()) dc.DrawImage(source,new Rect(Anchor-anchor*scale,Foot-baseline*scale,source.PixelWidth*scale,source.PixelHeight*scale));
        var bitmap=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static void Save(string name,IReadOnlyList<BitmapSource> frames,int[] durations)
    {
        var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) for(var i=0;i<frames.Count;i++) dc.DrawImage(frames[i],new Rect(i*Width,0,Width,Height));
        var atlas=new RenderTargetBitmap(Width*frames.Count,Height,96,96,PixelFormats.Pbgra32); atlas.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(atlas)); using(var file=File.Create(Path.Combine(Output,name+".png"))) encoder.Save(file);
        Clips[name]=new{file=name+".png",frames=Enumerable.Range(0,frames.Count).ToArray(),durations};
    }
    private static void Alias(string name,string source,int[] frames,int[] durations)
    { var json=JsonSerializer.SerializeToElement(Clips[source]); Clips[name]=new{file=json.GetProperty("file").GetString(),frames,durations}; }
    private static void MakeIcon(BitmapSource frame)
    {
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame)); using var stream=new MemoryStream(); encoder.Save(stream); stream.Position=0;
        using var bitmap=new System.Drawing.Bitmap(stream); using var small=new System.Drawing.Bitmap(64,64);
        using(var graphics=System.Drawing.Graphics.FromImage(small)) graphics.DrawImage(bitmap,new System.Drawing.Rectangle(0,0,64,64),new System.Drawing.Rectangle(88,32,64,78),System.Drawing.GraphicsUnit.Pixel);
        var handle=small.GetHicon(); using(var icon=System.Drawing.Icon.FromHandle(handle)) using(var file=File.Create(Path.Combine(Output,"jellybean.ico"))) icon.Save(file); DestroyIcon(handle);
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
