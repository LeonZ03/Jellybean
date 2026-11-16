using System.Windows;
using System.Windows.Media.Media3D;
using Jellybean.Engine;

namespace Jellybean.UI;

internal sealed class PetLifeMotion
{
    private readonly record struct Breath(double X, double Y, double Width, double Height, double Depth, double Period)
    {
        public static Breath Blend(Breath from, Breath to, double amount) => new(
            Lerp(from.X, to.X, amount), Lerp(from.Y, to.Y, amount),
            Lerp(from.Width, to.Width, amount), Lerp(from.Height, to.Height, amount),
            Lerp(from.Depth, to.Depth, amount), Lerp(from.Period, to.Period, amount));

        private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
    }

    private static readonly Breath Standing = new(136, 90, 58, 50, 1.8, 2.9);
    private static readonly Breath Sleeping = new(111, 111, 57, 38, 4.1, 4.1);
    private static readonly Breath Ledge = new(105, 119, 54, 37, 3.5, 3.5);
    private Breath _breathing;
    private double _phase;

    public void Apply(PetMotionEffect effect, string clip, PetState state, double progress, double time, double elapsed, Vector sway)
    {
        const double size = SpriteCatalog.Width;
        var mirrored = clip.EndsWith("-left", StringComparison.Ordinal) || clip == "left-view";
        double X(double x) => mirrored ? size - x : x;
        var horizontal = mirrored ? -1 : 1;
        var target = BreathingFor(clip, state, progress);
        target = target with { X = X(target.X) };
        var easing = 1 - Math.Exp(-Math.Min(elapsed, .1) * 14);
        _breathing = _breathing.Period == 0 ? target : Breath.Blend(_breathing, target, easing);
        _phase = (_phase + Math.Tau * elapsed / _breathing.Period) % Math.Tau;
        var pulse = (.5 - .5 * Math.Cos(_phase)) * (.94 + .06 * Math.Cos(time * .91));
        effect.Breath = pulse * _breathing.Depth;
        effect.BreathCenter = new Point(_breathing.X / size, _breathing.Y / size);
        effect.BreathInverseExtent = new Point(size / _breathing.Width, size / _breathing.Height);
        effect.Sway = new Point(sway.X, sway.Y);

        Point4D Region(double x, double y, double width, double height) => new(X(x) / size, y / size, size / width, size / height);
        Point4D head = default, tail = default;
        var eyes = new Point();
        var eyeHeight = 0.0;
        var sleeping = clip.StartsWith("doze-", StringComparison.Ordinal);
        var onLedge = clip.StartsWith("window-rest-", StringComparison.Ordinal);
        var side = clip is "idle-left" or "idle-right" or "left-view" or "right-view";
        if (side)
        {
            head = Region(177, 53, 34, 36); tail = Region(45, 145, 33, 23);
            eyes = new Point(X(191) / size, X(191) / size); eyeHeight = 66.7;
        }
        else if (onLedge)
        {
            head = Region(171, 85, 28, 28); tail = Region(54, 203, 24, 36);
            eyes = new Point(X(178) / size, X(178) / size); eyeHeight = 99.5;
        }
        else if (sleeping) head = Region(169, 111, 27, 24);
        else if (clip is "front" or "front-three-quarter" or "rear" or "rear-three-quarter")
        {
            var faceX = clip == "front-three-quarter" ? 82 : clip == "rear-three-quarter" ? 144 : 120;
            head = Region(faceX, 54, 35, 35);
            if (clip == "front") { eyes = new Point(112 / size, 128 / size); eyeHeight = 75; }
            if (clip == "front-three-quarter") { eyes = new Point(75 / size, 90 / size); eyeHeight = 74.5; }
        }

        var listening = Wave(time, 6.7, 1.2, .45);
        var glance = Wave(time, 7.9, 3.5, 1.35);
        var twitch = Wave(time, onLedge ? 4.8 : 5.9, .6, .93);
        var headAmount = head.Z == 0 ? 0 : sleeping ? .5 : 1;
        var tailAmount = tail.Z == 0 ? 0 : onLedge ? 3.5 : 2.8;
        effect.HeadRegion = head;
        effect.TailRegion = tail;
        effect.DetailShift = new Point4D(horizontal * headAmount * (.85 * listening + 1.35 * glance) / size,
            headAmount * (1.2 * listening - .6 * glance) / size,
            horizontal * tailAmount * twitch / size, -.64 * tailAmount * twitch / size);
        effect.Eyes = eyes;
        effect.EyeHeight = eyeHeight / size;
        var blinking = (time + .54 * Math.Sin(time * .89)) % (onLedge ? 5.2 : 4.2) - 1.8;
        effect.Blink = eyeHeight == 0 ? 0 : 3.7 * Smooth(blinking / .08) * (1 - Smooth((blinking - .11) / .14));
    }

    private static double Wave(double time, double period, double offset, double span)
    {
        var progress = Math.Clamp(((time + .34 * Math.Sin(time * .73)) % period - offset) / span, 0, 1);
        return Math.Pow(Math.Sin(Math.PI * progress), 2);
    }

    private static double Smooth(double value)
    {
        var t = Math.Clamp(value, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static Breath BreathingFor(string clip, PetState state, double progress)
    {
        var rest = clip.StartsWith("window-", StringComparison.Ordinal) ? Ledge : Sleeping;
        return state switch
        {
            PetState.Doze => rest,
            PetState.LieDown or PetState.Rise => Breath.Blend(Standing, rest, progress),
            PetState.Pet or PetState.Perch or PetState.Peek => new(120, 111, 31, 47, 1.8, 3.0),
            PetState.Drag => new(115, 102, 39, 46, 1.4, 2.8),
            PetState.HuntAim or PetState.Catch => new(135, 119, 51, 34, 1.6, 2.5),
            PetState.Stalk or PetState.Pounce => new(135, 116, 55, 38, .9, 2.4),
            PetState.Stretch => Breath.Blend(Standing, new(117, 101, 68, 43, 1.5, 3.0), progress),
            PetState.Walk or PetState.ApproachJump or PetState.Jump or PetState.Fall or PetState.Land => Standing with { Depth = .72, Period = 2.5 },
            _ when clip is "front" or "front-three-quarter" or "rear" or "rear-three-quarter" => new(120, 101, 38, 49, 1.8, 2.9),
            _ => Standing
        };
    }
}
