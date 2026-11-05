// Frozen atlas slicing adapted from PixelPaws SpriteAnimator.cs (MIT).
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Jellybean.Engine;

public sealed class SpriteCatalog
{
    private sealed record Clip(BitmapSource[] Frames, int[] Durations)
    {
        public int Duration { get; } = Durations.Sum();
    }

    private readonly Dictionary<string, Clip> _clips = new();
    private readonly Dictionary<BitmapSource, byte[]> _hitMasks = new();
    public const int Width = 240, Height = 240, Anchor = 120, Foot = 164;
    public double WalkStrideLeft { get; }
    public double WalkStrideRight { get; }

    public SpriteCatalog()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"animations.json")));
        WalkStrideLeft=manifest.RootElement.GetProperty("walkStrideLeft").GetDouble();
        WalkStrideRight=manifest.RootElement.GetProperty("walkStrideRight").GetDouble();
        var atlases = new Dictionary<string, BitmapImage>();
        var frameCache = new Dictionary<(string,int),BitmapSource>();
        foreach (var entry in manifest.RootElement.GetProperty("clips").EnumerateObject())
        {
            var name = entry.Value.GetProperty("file").GetString()!;
            if (!atlases.TryGetValue(name, out var atlas))
            {
                atlas = new BitmapImage();
                atlas.BeginInit(); atlas.CacheOption = BitmapCacheOption.OnLoad;
                atlas.UriSource = new Uri(Path.Combine(directory,name)); atlas.EndInit(); atlas.Freeze();
                atlases.Add(name,atlas);
            }
            var frames = entry.Value.GetProperty("frames").EnumerateArray().Select(index =>
            {
                if(frameCache.TryGetValue((name,index.GetInt32()),out var cached)) return cached;
                var frame = new CroppedBitmap(atlas,new Int32Rect(index.GetInt32()*Width,0,Width,Height));
                frame.Freeze(); frameCache.Add((name,index.GetInt32()),frame); return (BitmapSource)frame;
            }).ToArray();
            _clips[entry.Name] = new Clip(frames,entry.Value.GetProperty("durations").EnumerateArray().Select(d=>d.GetInt32()).ToArray());
        }
    }

    public BitmapSource Frame(string animation,double age,bool loop=true)
    {
        var clip = _clips[animation];
        var milliseconds = (int)(age*1000);
        milliseconds = loop ? milliseconds%clip.Duration : Math.Min(milliseconds,clip.Duration-1);
        for (var i=0;i<clip.Durations.Length;i++)
        {
            if(milliseconds < clip.Durations[i]) return clip.Frames[i];
            milliseconds -= clip.Durations[i];
        }
        return clip.Frames[^1];
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
