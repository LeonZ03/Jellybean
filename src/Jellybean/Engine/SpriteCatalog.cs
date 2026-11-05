// Frozen atlas slicing adapted from PixelPaws SpriteAnimator.cs (MIT).
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Jellybean.Engine;

public sealed class SpriteCatalog
{
    private sealed record Clip(BitmapSource[] Frames, int[] Durations)
    {
        public int Duration { get; } = Durations.Sum();
    }

    private readonly Dictionary<string, Clip> _clips = new();
    public const int Width = 192, Height = 176, Anchor = 96, Foot = 164;

    public SpriteCatalog()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"animations.json")));
        var atlases = new Dictionary<string, BitmapImage>();
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
                var frame = new CroppedBitmap(atlas,new Int32Rect(index.GetInt32()*Width,0,Width,Height));
                frame.Freeze(); return (BitmapSource)frame;
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
}
