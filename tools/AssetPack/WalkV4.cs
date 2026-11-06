using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private const int ReviewWidth=320, ReviewHeight=240, ReviewAnchor=160, ReviewFoot=184;
    private static readonly Color ReviewPaper=Color.FromRgb(255,250,240);

    private static void ReviewPng(DrawingVisual visual,int width,int height,string path)
    {
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path); encoder.Save(file);
    }

    private static void PrepareWalkV4()
    {
        var input=Path.Combine(Root,"artifacts/walk-v4/input"); Directory.CreateDirectory(input);
        var folder=Path.Combine(Root,"design/walk-v4"); Directory.CreateDirectory(folder);
        var transparent=Path.Combine(folder,"keys"); Directory.CreateDirectory(transparent);
        // One uniform transform per complete drawing: ring aligns the torso, soles align the floor.
        double[] ringX={656,555,557,516,640,545,551,539};
        var calibration=new object[8];
        var keys=new BitmapSource[8];
        for(var i=0;i<8;i++)
        {
            var cat=Isolate(Load($"assets/animations/walk-v4/keys/{i:00}.png"));
            var bounds=Bounds(cat); var scale=124/bounds.Height;
            var visual=new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
            using(var dc=visual.RenderOpen()) dc.DrawImage(cat,new Rect(ReviewAnchor-ringX[i]*scale,ReviewFoot-bounds.Bottom*scale,cat.PixelWidth*scale,cat.PixelHeight*scale));
            ReviewPng(visual,ReviewWidth,ReviewHeight,Path.Combine(transparent,$"{i:00}.png"));
            keys[i]=Load($"design/walk-v4/keys/{i:00}.png");
            calibration[i]=new{key=i,bounds,ringX=ringX[i],scale};
        }
        for(var i=0;i<9;i++)
        {
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(ReviewPaper),null,new Rect(0,0,ReviewWidth,ReviewHeight));
                dc.DrawImage(keys[i%8],new Rect(0,0,ReviewWidth,ReviewHeight));
            }
            ReviewPng(visual,ReviewWidth,ReviewHeight,Path.Combine(input,$"{i:00000000}.png"));
        }
        var contact=new DrawingVisual(); using(var dc=contact.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(ReviewPaper),null,new Rect(0,0,1280,480));
            for(var i=0;i<8;i++)
            {
                var x=i%4*ReviewWidth; var y=i/4*ReviewHeight;
                dc.DrawImage(keys[i],new Rect(x,y,ReviewWidth,ReviewHeight));
                dc.DrawLine(new Pen(Brushes.Wheat,1),new Point(x+12,y+ReviewFoot),new Point(x+308,y+ReviewFoot));
                dc.DrawText(new FormattedText($"{i+1:00}",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.SaddleBrown,1),new Point(x+18,y+204));
            }
        }
        ReviewPng(contact,1280,480,Path.Combine(folder,"key-poses.png"));
        File.WriteAllText(Path.Combine(Root,"artifacts/walk-v4/calibration.json"),JsonSerializer.Serialize(calibration,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Prepared 8 whole-cat keys + repeated first key for the loop.");
    }

    private static void PreviewWalkV4()
    {
        const int count=64;
        var folder=Path.Combine(Root,"design/walk-v4");
        var frames=Directory.GetFiles(Path.Combine(Root,"artifacts/walk-v4/interpolated"),"*.png").OrderBy(p=>p,StringComparer.Ordinal).Take(count).Select(Load).ToArray();
        if(frames.Length!=count) throw new InvalidOperationException("Prepare 65 interpolated frames, including the repeated first frame.");
        var atlas=new DrawingVisual(); using(var dc=atlas.RenderOpen())
            for(var i=0;i<count;i++) dc.DrawImage(frames[i],new Rect(i%8*ReviewWidth,i/8*ReviewHeight,ReviewWidth,ReviewHeight));
        var atlasPath=Path.Combine(folder,"atlas.png"); ReviewPng(atlas,2560,1920,atlasPath);
        var stills=Path.Combine(Root,"artifacts/walk-v4/preview"); Directory.CreateDirectory(stills);
        var travel=Path.Combine(Root,"artifacts/walk-v4/travel"); Directory.CreateDirectory(travel);
        void Stage(BitmapSource frame,string path,int width,int height,double anchor,double scale,bool ticks)
        {
            const double floor=280;
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(ReviewPaper),null,new Rect(0,0,width,height));
                dc.DrawImage(frame,new Rect(anchor-ReviewAnchor*scale,floor-ReviewFoot*scale,ReviewWidth*scale,ReviewHeight*scale));
                dc.DrawLine(new Pen(Brushes.Wheat,1),new Point(0,floor),new Point(width,floor));
                if(ticks) for(var x=24;x<width;x+=64) dc.DrawLine(new Pen(Brushes.Wheat,1),new Point(x,floor),new Point(x,floor+6));
            }
            ReviewPng(visual,width,height,path);
        }
        for(var i=0;i<count;i++) Stage(frames[i],Path.Combine(stills,$"{i:0000}.png"),600,340,280,1.65,false);
        for(var i=0;i<count*6;i++) Stage(frames[i%count],Path.Combine(travel,$"{i:0000}.png"),820,340,960-i*128.0/count*1.5,1.5,true);
        var embedded=Convert.ToBase64String(File.ReadAllBytes(atlasPath));
        File.WriteAllText(Path.Combine(folder,"index.html"),$$"""
        <!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>糖豆 · 行走重画</title><style>*{box-sizing:border-box}body{background:#f4efdf;color:#4b3a2a;font:16px "Microsoft YaHei UI",sans-serif;max-width:960px;margin:30px auto;padding:0 20px}h1{font-size:24px}p{line-height:1.7}small{color:#796a55}canvas{display:block;width:100%;height:auto;background:#fffaf0;border-radius:16px}button{font:inherit;color:inherit;background:#fff8e8;border:1px solid #cbbd9d;border-radius:9px;padding:8px 14px;margin:12px 8px 0 0;cursor:pointer}button:focus-visible{outline:3px solid #a96d45;outline-offset:3px}input{width:min(100%,420px);margin:16px 10px 0 0;accent-color:#b97850}label{display:inline-flex;align-items:center;gap:8px}</style>
        <h1>糖豆 · 行走重画</h1><p>按实拍研究接地、蹬地、前摆。整只猫重画，保留细长耳朵、细尾巴和粉色背带。<br><small>8 个完整关键姿势，整帧补间至 64 帧；待确认的小样。</small></p>
        <canvas id="view" width="920" height="380" aria-label="糖豆行走动画"></canvas>
        <div><button id="play">暂停</button><button id="slow">慢放</button><button id="travel">原地查看</button><button id="next">下一帧</button></div>
        <label>进度 <input id="seek" type="range" min="0" max="63" value="0" aria-label="动画帧"><span id="frame"></span></label>
        <p><small>检查脚掌是否滑动时，用「向前走」；检查腿部衔接时，用慢放或逐帧。<a href="SOURCES.md">制作依据</a></small></p>
        <script>
        const art=new Image();art.src='data:image/png;base64,{{embedded}}';
        const canvas=document.getElementById('view'),ctx=canvas.getContext('2d'),play=document.getElementById('play'),slow=document.getElementById('slow'),travel=document.getElementById('travel'),seek=document.getElementById('seek');
        const cycle=64/60*1000,scale=1.65,stride=128;let elapsed=cycle*2,last=0,playing=!matchMedia('(prefers-reduced-motion: reduce)').matches,speed=1,moving=true;ctx.imageSmoothingEnabled=false;
        function syncPlay(){play.textContent=playing?'暂停':'播放'}syncPlay();
        play.onclick=()=>{playing=!playing;syncPlay()};slow.onclick=()=>{speed=speed===1?.35:1;slow.textContent=speed===1?'慢放':'正常速度'};
        travel.onclick=()=>{moving=!moving;elapsed%=cycle;travel.textContent=moving?'原地查看':'向前走'};
        document.getElementById('next').onclick=()=>{playing=false;syncPlay();elapsed=(Math.floor(elapsed/cycle*64)+1)*cycle/64};
        seek.oninput=()=>{playing=false;syncPlay();elapsed=Number(seek.value)*cycle/64};
        function tick(t){if(last&&playing)elapsed+=Math.min(t-last,100)*speed;last=t;const i=Math.floor(elapsed/cycle*64)%64,floor=300,anchor=moving?1069-(elapsed/cycle%7)*stride*scale:440;
        ctx.fillStyle='#fffaf0';ctx.fillRect(0,0,920,380);
        if(art.complete&&art.naturalWidth)ctx.drawImage(art,i%8*320,Math.floor(i/8)*240,320,240,anchor-160*scale,floor-184*scale,320*scale,240*scale);
        ctx.strokeStyle='#e7d6b5';ctx.beginPath();ctx.moveTo(0,floor);ctx.lineTo(920,floor);for(let x=24;x<920;x+=64){ctx.moveTo(x,floor);ctx.lineTo(x,floor+6)}ctx.stroke();
        seek.value=i;document.getElementById('frame').textContent=(i+1)+' / 64';requestAnimationFrame(tick)}requestAnimationFrame(tick);
        </script></html>
        """);
        Console.WriteLine("Walk review exported: 64 frames, 60 fps, stride 128 native pixels per cycle.");
    }
}
