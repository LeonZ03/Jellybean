// Frozen atlas slicing adapted from PixelPaws SpriteAnimator.cs (MIT).
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Jellybean.Engine;

public readonly record struct SpriteSample(BitmapSource From, BitmapSource To, double Blend);

public sealed class SpriteCatalog
{
    private sealed record Clip(string File,int Columns,int[] Frames, int[] Durations)
    {
        public int Duration { get; } = Durations.Sum();
    }

    private readonly Dictionary<string, Clip> _clips = new();
    private readonly Dictionary<BitmapSource, byte[]> _hitMasks = new();
    private readonly Dictionary<(string File,int Frame),BitmapSource> _frames = new();
    private readonly Dictionary<string,(BitmapImage Image,long Used)> _atlases = new();
    private long _used;
    private const int AtlasCacheSize=3;
    public const int Width = 240, Height = 240, Anchor = 120, Foot = 164, HeldNapeY = 48;
    public double WalkStrideLeft { get; }
    public double WalkStrideRight { get; }

    public SpriteCatalog()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"animations.json")));
        WalkStrideLeft=manifest.RootElement.GetProperty("walkStrideLeft").GetDouble();
        WalkStrideRight=manifest.RootElement.GetProperty("walkStrideRight").GetDouble();
        foreach (var entry in manifest.RootElement.GetProperty("clips").EnumerateObject())
        {
            var name = entry.Value.GetProperty("file").GetString()!;
            var columns=entry.Value.TryGetProperty("columns",out var columnCount)?columnCount.GetInt32():1;
            _clips[entry.Name] = new Clip(name,columns,entry.Value.GetProperty("frames").EnumerateArray().Select(i=>i.GetInt32()).ToArray(),
                entry.Value.GetProperty("durations").EnumerateArray().Select(d=>d.GetInt32()).ToArray());
        }
    }

    public BitmapSource Frame(string animation,double age,bool loop=true)
        => Sample(animation,age,loop).From;

    public SpriteSample Sample(string animation,double age,bool loop=true)
    {
        var clip = _clips[animation];
        var milliseconds = Math.Max(0,age*1000);
        milliseconds = loop ? milliseconds%clip.Duration : Math.Min(milliseconds,clip.Duration-.001);
        for (var i=0;i<clip.Durations.Length;i++)
        {
            if(milliseconds < clip.Durations[i])
            {
                var next=loop?(i+1)%clip.Frames.Length:Math.Min(i+1,clip.Frames.Length-1);
                var span=Math.Min(45,clip.Durations[i]);
                var blend=Math.Clamp((milliseconds-(clip.Durations[i]-span))/span,0,1);
                return new SpriteSample(LoadFrame(clip,clip.Frames[i]),LoadFrame(clip,clip.Frames[next]),blend);
            }
            milliseconds -= clip.Durations[i];
        }
        var last=LoadFrame(clip,clip.Frames[^1]); return new SpriteSample(last,last,0);
    }

    private BitmapSource LoadFrame(Clip clip,int index)
    {
        if (!_atlases.TryGetValue(clip.File,out var atlas))
        {
            if (_atlases.Count==AtlasCacheSize)
            {
                var oldest=_atlases.MinBy(a=>a.Value.Used).Key;
                _atlases.Remove(oldest);
                foreach(var key in _frames.Keys.Where(k=>k.File==oldest).ToArray())
                { _hitMasks.Remove(_frames[key]); _frames.Remove(key); }
            }
            var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
            image.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"Assets",clip.File)); image.EndInit(); image.Freeze();
            atlas=(image,0);
        }
        _atlases[clip.File]=(atlas.Image,++_used);
        if (_frames.TryGetValue((clip.File,index),out var cached)) return cached;
        var frame=new CroppedBitmap(atlas.Image,new Int32Rect(index%clip.Columns*Width,index/clip.Columns*Height,Width,Height));
        frame.Freeze(); _frames[(clip.File,index)]=frame; return frame;
    }

    public byte[] HitMask(BitmapSource frame)
    {
        if (_hitMasks.TryGetValue(frame,out var mask)) return mask;
        var bgra = new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);
        var bytes = new byte[Width*Height*4];
        bgra.CopyPixels(bytes,Width*4,0);
        mask = new byte[Width*Height];
        for(var i=0;i<mask.Length;i++) mask[i]=bytes[i*4+3];
        // A grabbed pose is a temporary snapshot; atlas masks are the reusable ones.
        if(frame is CroppedBitmap) _hitMasks.Add(frame,mask);
        return mask;
    }
}
