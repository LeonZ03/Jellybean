using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private const int Width=240, Height=240, Foot=164, Anchor=120;
    private const double WalkStride=53.16; // 44.3 logical pixels/second for the 1.2-second source cycle.
    private static readonly int[] WalkDurations=Enumerable.Repeat(100,12).ToArray();
    private static readonly Dictionary<string,object> Clips=new();
    private static readonly List<object> Calibration=new();
    private static string Root="", Output="";

    [STAThread]
    private static void Main(string[] args)
    {
        Root=Path.GetFullPath(args.Length>0?args[0]:"."); Output=Path.Combine(Root,"src/Jellybean/Assets"); Directory.CreateDirectory(Output);
        if(args.Contains("--walk-v4-prepare")) { PrepareWalkV4(); return; }
        if(args.Contains("--walk-v4-preview")) { PreviewWalkV4(); return; }
        if(args.Contains("--walk-review")) { ExportWalkReview(); return; }
        var walkLeft=PackApprovedWalk("walk-left","design/walk-transfer/atlas-fur-50-left.png");
        var walkRight=PackApprovedWalk("walk-right","design/walk-transfer/atlas-fur-50.png");
        var body=Load("assets/animations/v2/turnaround.png");
        var regions=new[]{new Int32Rect(130,0,230,486),new Int32Rect(460,0,443,486),new Int32Rect(903,0,633,486),new Int32Rect(12,488,540,536),new Int32Rect(620,488,240,536),new Int32Rect(965,488,500,536)};
        var views=regions.Select(r=>Isolate(new CroppedBitmap(body,r))).ToArray();
        var names=new[]{"front","front-three-quarter","left-view","right-view","rear","rear-three-quarter"};
        for(var i=0;i<views.Length;i++)
        {
            var b=Bounds(views[i]); var s=124/b.Height;
            var side=i==2 || i==3;
            var xOffset=i==2?-4:i==3?4:0;
            Save(names[i],new[]{Draw(CoatPalette.MatchWalk(views[i]),(b.Left+b.Right)/2,b.Bottom,s,side?1.06:1,xOffset)},new[]{1000});
        }
        Pack("leap-left",3,2,Enumerable.Repeat(100,6).ToArray(),sourceName:"pounce-left",
            standing:walkLeft,horizontalScale:1.08,xOffset:-7,endsStanding:true);
        Pack("leap-right",3,2,Enumerable.Repeat(100,6).ToArray(),sourceName:"pounce-right",
            standing:walkRight,horizontalScale:1.08,xOffset:8,endsStanding:true);
        Pack("pet",3,2,new[]{380,300,260,280,300,580});
        Pack("perch",3,2,new[]{600,650,300,450,700,700});
        Pack("stretch",3,2,new[]{200,400,500,700,500,500},standing:walkLeft,endsStanding:true);
        Pack("rest-left",3,2,new[]{100,220,240,300,250,1000},sourceName:"doze",
            standing:walkLeft,horizontalScale:1.06,xOffset:-6);
        Pack("rest-window",3,2,new[]{100,220,240,300,900,900},new[]{405,405,398,324,326,325},"window-rest",
            standing:walkLeft,horizontalScale:1.04,xOffset:-4);
        var held=CoatPalette.MatchWalk(Isolate(Load("assets/animations/v3/held.png")));
        foreach(var direction in new[]{"left","right"})
        {
            var source="leap-"+direction;
            Alias("idle-"+direction,"walk-"+direction,new[]{0},new[]{1000});
            Alias("stalk-"+direction,"walk-"+direction,Enumerable.Range(0,12).ToArray(),WalkDurations);
            Alias("crouch-"+direction,source,new[]{2},new[]{1000});
            Alias("takeoff-"+direction,source,new[]{0,1,2},new[]{40,45,45});
            Alias("air-"+direction,source,new[]{3},new[]{320});
            Alias("fall-"+direction,source,new[]{3},new[]{1000});
            Alias("land-"+direction,source,new[]{4,2,1,0},new[]{45,45,80,110});
            Alias("catch-"+direction,source,new[]{2},new[]{180});
            Alias("pounce-"+direction,source,new[]{2,3},new[]{100,280});
            Save("held-"+direction,new[]{Held(held,direction=="right")},new[]{1000});
        }
        Alias("lie-left","rest-left",new[]{0,1,2,3},new[]{100,220,240,300});
        Alias("doze","rest-left",new[]{5,4,5},new[]{2400,220,1800});
        Alias("rise-left","rest-left",new[]{3,2,1,0},new[]{120,200,230,230});
        Alias("window-lie","rest-window",new[]{0,1,2,3},new[]{100,220,240,300});
        Alias("window-rest","rest-window",new[]{3,4,5,4},new[]{700,900,700,900});
        Alias("window-rise","rest-window",new[]{3,2,1,0},new[]{120,200,230,230});
        var options=new JsonSerializerOptions{WriteIndented=true};
        File.WriteAllText(Path.Combine(Output,"animations.json"),JsonSerializer.Serialize(new{width=Width,height=Height,anchor=Anchor,foot=Foot,walkStrideLeft=WalkStride,walkStrideRight=WalkStride,clips=Clips},options));
        Directory.CreateDirectory(Path.Combine(Root,"artifacts"));
        File.WriteAllText(Path.Combine(Root,"artifacts/animation-calibration.json"),JsonSerializer.Serialize(Calibration,options));
        ShaderCompiler.Compile(Path.Combine(Root,"src/Jellybean/Shaders/PetMotion.hlsl"),Path.Combine(Output,"pet-motion.ps"));
        MakeIcon(Load("src/Jellybean/Assets/front.png"));
        Console.WriteLine($"Packed {Clips.Count} clips; one anatomical scale per sheet, feet fixed at {Foot}.");
    }

    private static BitmapSource PackApprovedWalk(string name,string path)
    {
        const int columns=4, cellWidth=480, cellHeight=352;
        var source=Load(path);
        if(source.PixelWidth!=columns*cellWidth || source.PixelHeight!=3*cellHeight)
            throw new InvalidDataException($"Unexpected approved walk atlas size: {path}");
        var frames=Enumerable.Range(0,WalkDurations.Length).Select(i =>
        {
            var rect=new Int32Rect(i%columns*cellWidth,i/columns*cellHeight,cellWidth,cellHeight);
            // Use the same anchor and floor for every frame so the planted paws keep their motion.
            return Draw(new CroppedBitmap(source,rect),240,314,.5);
        }).ToArray();
        Save(name,frames,WalkDurations,blendMilliseconds:0);
        Calibration.Add(new { name, scale=.5, referenceHeight=124 });
        return frames[0];
    }

    private static BitmapSource Load(string path)
    {
        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.UriSource=new Uri(Path.Combine(Root,path)); image.EndInit();
        var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
        var pixels=new byte[converted.PixelWidth*converted.PixelHeight*4]; converted.CopyPixels(pixels,converted.PixelWidth*4,0);
        for(var i=0;i<pixels.Length;i+=4) if(pixels[i+3]<8) pixels[i]=pixels[i+1]=pixels[i+2]=pixels[i+3]=0;
        var clean=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,converted.PixelWidth*4); clean.Freeze(); return clean;
    }

    private static void Pack(string name,int columns,int rows,int[] durations,int[]? baselines=null,string? sourceName=null,
        BitmapSource? standing=null,double horizontalScale=1,double xOffset=0,bool endsStanding=false)
    {
        var source=Load("assets/animations/v2/"+(sourceName??name)+".png");
        var padding=new (int Left,int Top)[columns*rows];
        var cells=Enumerable.Range(0,columns*rows).Select(i=>
        {
            var x=(int)Math.Round(i%columns*source.PixelWidth/(double)columns);
            var y=(int)Math.Round(i/columns*source.PixelHeight/(double)rows);
            var right=(int)Math.Round((i%columns+1)*source.PixelWidth/(double)columns);
            var bottom=(int)Math.Round((i/columns+1)*source.PixelHeight/(double)rows);
            var left=Math.Max(0,x-24); var top=Math.Max(0,y-24);
            padding[i]=(x-left,y-top);
            right=Math.Min(source.PixelWidth,right+24); bottom=Math.Min(source.PixelHeight,bottom+24);
            return Isolate(new CroppedBitmap(source,new Int32Rect(left,top,right-left,bottom-top)));
        }).ToArray();
        var first=Bounds(cells[0]);
        // Standing reference frames share a height; compact resting poses keep that anatomical unit.
        var scale=(name is "pet" or "perch"?112.0:124.0)/first.Height;
        var anchor=MassCenterX(cells[0]);
        var frames=new List<BitmapSource>();
        for(var i=0;i<cells.Length;i++)
        {
            var b=Bounds(cells[i]);
            var baseline=baselines is null?b.Bottom:baselines[i]+padding[i].Top;
            if(standing is not null && (i==0 || endsStanding && i==cells.Length-1))
                frames.Add(standing);
            else
                frames.Add(Draw(CoatPalette.MatchWalk(cells[i]),anchor+padding[i].Left-padding[0].Left,
                    baseline,scale,horizontalScale,xOffset));
        }
        Save(name,frames,durations);
        Calibration.Add(new{name,scale,headGap=HeadGap(cells[0])*scale,referenceHeight=first.Height*scale});
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var bgra=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
        var pixels=new byte[bgra.PixelWidth*bgra.PixelHeight*4]; bgra.CopyPixels(pixels,bgra.PixelWidth*4,0); return pixels;
    }
    // A padded cell recovers paws/tails crossing grid lines; connected components reject its neighbours.
    private static BitmapSource Isolate(BitmapSource bitmap)
    {
        var width=bitmap.PixelWidth; var height=bitmap.PixelHeight; var pixels=Pixels(bitmap);
        var labels=new int[width*height]; var queue=new int[labels.Length];
        var sizes=new List<int>{0}; var biggest=0;
        for(var start=0;start<labels.Length;start++)
        {
            if(labels[start]!=0 || pixels[start*4+3]<40) continue;
            var id=sizes.Count; var read=0; var write=1; queue[0]=start; labels[start]=id;
            while(read<write)
            {
                var point=queue[read++]; var x=point%width; var y=point/width;
                for(var yy=Math.Max(0,y-1);yy<=Math.Min(height-1,y+1);yy++)
                    for(var xx=Math.Max(0,x-1);xx<=Math.Min(width-1,x+1);xx++)
                    { var p=yy*width+xx; if(labels[p]==0 && pixels[p*4+3]>=40) { labels[p]=id; queue[write++]=p; } }
            }
            sizes.Add(write); if(biggest==0 || write>sizes[biggest]) biggest=id;
        }
        var keep=new bool[sizes.Count]; keep[biggest]=true;
        for(var p=0;p<labels.Length;p++)
        {
            if(labels[p]!=biggest) continue;
            var x=p%width; var y=p/width;
            for(var yy=Math.Max(0,y-6);yy<=Math.Min(height-1,y+6);yy++)
                for(var xx=Math.Max(0,x-6);xx<=Math.Min(width-1,x+6);xx++) keep[labels[yy*width+xx]]=true;
        }
        keep[0]=false;
        for(var p=0;p<labels.Length;p++)
        {
            var preserve=keep[labels[p]];
            if(!preserve && labels[p]==0 && pixels[p*4+3]>=8)
            {
                var x=p%width; var y=p/width;
                for(var yy=Math.Max(0,y-1);yy<=Math.Min(height-1,y+1) && !preserve;yy++)
                    for(var xx=Math.Max(0,x-1);xx<=Math.Min(width-1,x+1);xx++) if(keep[labels[yy*width+xx]]) { preserve=true; break; }
            }
            if(!preserve) pixels[p*4]=pixels[p*4+1]=pixels[p*4+2]=pixels[p*4+3]=0;
        }
        var result=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4); result.Freeze(); return result;
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
    private static BitmapSource Draw(BitmapSource source,double anchor,double baseline,double scale,double horizontalScale=1,double xOffset=0)
    {
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        var xScale=scale*horizontalScale;
        using(var dc=visual.RenderOpen()) dc.DrawImage(source,new Rect(Anchor+xOffset-anchor*xScale,Foot-baseline*scale,source.PixelWidth*xScale,source.PixelHeight*scale));
        var bitmap=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static BitmapSource Held(BitmapSource source,bool right)
    {
        var visual=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())
        {
            if(right) dc.PushTransform(new ScaleTransform(-1,1,Anchor,0));
            const double scale=.127;
            dc.DrawImage(source,new Rect(Anchor-510*scale,48-365*scale,source.PixelWidth*scale,source.PixelHeight*scale));
            if(right) dc.Pop();
        }
        var bitmap=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static void ExportWalkReview()
    {
        var source=Load("assets/animations/walk-review/whole-cat-left.png");
        var cells=Enumerable.Range(0,16).Select(i=>
        {
            var x=(int)Math.Round(i%4*source.PixelWidth/4.0); var y=(int)Math.Round(i/4*source.PixelHeight/4.0);
            var right=(int)Math.Round((i%4+1)*source.PixelWidth/4.0); var bottom=(int)Math.Round((i/4+1)*source.PixelHeight/4.0);
            return Isolate(new CroppedBitmap(source,new Int32Rect(x,y,right-x,bottom-y)));
        }).ToArray();
        var first=Bounds(cells[0]); var scale=124/first.Height; var anchor=MassCenterX(cells[0]);
        var frames=cells.Select(cell=>Draw(cell,anchor,Bounds(cell).Bottom,scale)).ToArray();
        var folder=Path.Combine(Root,"design/walk-review"); Directory.CreateDirectory(folder);
        var frameFolder=Path.Combine(Root,"artifacts/walk-review/frames"); Directory.CreateDirectory(frameFolder);
        void Png(DrawingVisual visual,int width,int height,string path)
        {
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file=File.Create(path); encoder.Save(file);
        }
        var atlas=new DrawingVisual(); using(var dc=atlas.RenderOpen())
            for(var i=0;i<16;i++) dc.DrawImage(frames[i],new Rect(i%4*Width,i/4*Height,Width,Height));
        var atlasPath=Path.Combine(folder,"atlas-left.png"); Png(atlas,960,960,atlasPath);
        for(var i=0;i<16;i++)
        {
            var stage=new DrawingVisual(); using(var dc=stage.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255,250,240)),null,new Rect(0,0,520,300));
                var label=new FormattedText("整图逐帧 · 行走小样",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),17,Brushes.SaddleBrown,1);
                dc.DrawText(label,new Point(22,18));
                dc.DrawLine(new Pen(Brushes.Wheat,1),new Point(24,242),new Point(496,242));
                dc.DrawImage(frames[i],new Rect(260-Anchor*1.25,242-Foot*1.25,Width*1.25,Height*1.25));
            }
            Png(stage,520,300,Path.Combine(frameFolder,$"{i:0000}.png"));
        }
        var embedded=Convert.ToBase64String(File.ReadAllBytes(atlasPath));
        File.WriteAllText(Path.Combine(folder,"index.html"),$$"""
        <!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>糖豆 · 整图行走小样</title><style>body{background:#f4efdf;color:#4b3a2a;font:16px "Microsoft YaHei UI",sans-serif;margin:32px auto;padding:0 20px;max-width:700px}h1{font-size:24px}canvas{width:100%;height:auto;background:#fffaf0;border-radius:18px;image-rendering:pixelated}button{font:inherit;color:inherit;background:#fff8e8;border:1px solid #cbbd9d;border-radius:9px;padding:8px 16px;margin:12px 8px 0 0;cursor:pointer}p{line-height:1.7}small{color:#796a55}</style>
        <h1>糖豆 · 整图行走小样</h1><p>每帧均为完整猫图，检查体型、四肢和背带是否连贯。<br><small>16 张动作图；待确认，未替换桌宠。</small></p>
        <canvas id="view" width="700" height="360"></canvas><div><button id="play">暂停</button><button id="slow">慢放</button><span id="frame"></span></div>
        <script>const art=new Image();art.src='data:image/png;base64,{{embedded}}';const canvas=document.getElementById('view'),ctx=canvas.getContext('2d');let elapsed=0,last=0,playing=true,speed=1;ctx.imageSmoothingEnabled=false;document.getElementById('play').onclick=e=>{playing=!playing;e.target.textContent=playing?'暂停':'播放'};document.getElementById('slow').onclick=e=>{speed=speed===1?.5:1;e.target.textContent=speed===1?'慢放':'正常速度'};function tick(t){if(last&&playing)elapsed+=Math.min(t-last,100)*speed;last=t;let i=Math.floor(elapsed/60)%16;ctx.clearRect(0,0,700,360);ctx.strokeStyle='#e7d6b5';ctx.beginPath();ctx.moveTo(28,290);ctx.lineTo(672,290);ctx.stroke();if(art.complete&&art.naturalWidth)ctx.drawImage(art,i%4*240,Math.floor(i/4)*240,240,240,350-120*1.55,290-164*1.55,372,372);document.getElementById('frame').textContent=(i+1)+' / 16';requestAnimationFrame(tick)}requestAnimationFrame(tick);</script></html>
        """);
        Console.WriteLine("Whole-cat walk review exported; runtime assets unchanged.");
    }
    private static void Save(string name,IReadOnlyList<BitmapSource> frames,int[] durations,int? blendMilliseconds=null)
    {
        var columns=Math.Min(8,frames.Count); var rows=(frames.Count+columns-1)/columns;
        var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) for(var i=0;i<frames.Count;i++) dc.DrawImage(frames[i],new Rect(i%columns*Width,i/columns*Height,Width,Height));
        var atlas=new RenderTargetBitmap(Width*columns,Height*rows,96,96,PixelFormats.Pbgra32); atlas.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(atlas)); using(var file=File.Create(Path.Combine(Output,name+".png"))) encoder.Save(file);
        var indices=Enumerable.Range(0,frames.Count).ToArray();
        Clips[name]=blendMilliseconds is int blend
            ? new {file=name+".png",columns,frames=indices,durations,blendMilliseconds=blend}
            : new {file=name+".png",columns,frames=indices,durations};
    }
    private static void Alias(string name,string source,int[] frames,int[] durations)
    {
        var json=JsonSerializer.SerializeToElement(Clips[source]);
        var file=json.GetProperty("file").GetString();
        var columns=json.GetProperty("columns").GetInt32();
        Clips[name]=json.TryGetProperty("blendMilliseconds",out var blend)
            ? new {file,columns,frames,durations,blendMilliseconds=blend.GetInt32()}
            : new {file,columns,frames,durations};
    }
    private static void MakeIcon(BitmapSource frame)
    {
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame)); using var stream=new MemoryStream(); encoder.Save(stream); stream.Position=0;
        using var bitmap=new System.Drawing.Bitmap(stream); using var small=new System.Drawing.Bitmap(64,64);
        using(var graphics=System.Drawing.Graphics.FromImage(small)) graphics.DrawImage(bitmap,new System.Drawing.Rectangle(0,0,64,64),new System.Drawing.Rectangle(88,32,64,78),System.Drawing.GraphicsUnit.Pixel);
        var handle=small.GetHicon(); using(var icon=System.Drawing.Icon.FromHandle(handle)) using(var file=File.Create(Path.Combine(Output,"jellybean.ico"))) icon.Save(file); DestroyIcon(handle);
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
