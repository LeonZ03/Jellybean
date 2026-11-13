using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class CoatPalette
{
    private static readonly double[] OldLightness = [40, 90, 115, 145, 169, 188, 208, 233, 255];
    private static readonly double[] NewLightness = [40, 96, 121, 139, 151, 164, 182, 220, 255];

    public static BitmapSource MatchWalk(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var b = (double)pixels[i];
            var g = (double)pixels[i + 1];
            var r = (double)pixels[i + 2];
            var red = r - g;
            var yellow = g - b;
            var lightness = .2126 * r + .7152 * g + .0722 * b;
            // Pink hardware has very little yellow; pale whiskers and dark eyes
            // are at the ends of the fur range and fade out of the adjustment.
            var orange = SmoothStep((red - 14) / 34) * SmoothStep((yellow - 7) / 18);
            var mask = orange * SmoothStep((lightness - 37) / 62) * (1 - SmoothStep((lightness - 213) / 37));
            if (mask == 0) continue;

            var target = MatchLightness(lightness);
            var newRed = red * .62;
            var newYellow = yellow * .86;
            var newGreen = target - .2126 * newRed + .0722 * newYellow;
            pixels[i] = Blend(b, newGreen - newYellow, mask);
            pixels[i + 1] = Blend(g, newGreen, mask);
            pixels[i + 2] = Blend(r, newGreen + newRed, mask);
        }

        var matched = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96,
            PixelFormats.Bgra32, null, pixels, stride);
        matched.Freeze();
        return matched;
    }

    private static double MatchLightness(double value)
    {
        for (var i = 1; i < OldLightness.Length; i++)
        {
            if (value > OldLightness[i]) continue;
            if (i == 1 && value < OldLightness[0]) return value;
            var t = (value - OldLightness[i - 1]) / (OldLightness[i] - OldLightness[i - 1]);
            return NewLightness[i - 1] + t * (NewLightness[i] - NewLightness[i - 1]);
        }
        return value;
    }

    private static double SmoothStep(double value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static byte Blend(double from, double to, double t) =>
        (byte)Math.Clamp(Math.Round(from + (to - from) * t), 0, 255);
}
