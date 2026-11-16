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
    private static readonly Breath Hanging = new(115, 102, 39, 46, 1.4, 2.8);
    private Breath _breathing;
    private double _phase;

    public void Apply(PetMotionEffect effect, string clip, PetState state, double progress, double time, double elapsed, Vector sway,
        PetGesture? gesture = null, double gestureProgress = 0, int gestureDirection = 0)
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

        Point4D Region(double x, double y, double width, double height) =>
            new(X(x) / size, y / size, Math.Sqrt(2) * size / width, Math.Sqrt(2) * size / height);
        Point4D Joint(double x, double y, double angle, Vector shift = default, bool hinge = false)
        {
            var radians = angle * horizontal * Math.PI / 180;
            var sin = Math.Sin(radians);
            var cos = hinge ? 1 : Math.Cos(radians);
            // Pack the inverse rigid transform as a displacement from the output pixel.
            var pivotX = (X(x) + shift.X * horizontal) / size;
            var pivotY = (y + shift.Y) / size;
            return new(cos - 1, sin,
                X(x) / size - cos * pivotX - sin * pivotY,
                y / size + sin * pivotX - cos * pivotY);
        }

        Point4D head = default, tail = default, accent = default;
        Point4D headMotion = default, tailMotion = default, accentMotion = default;
        var eyes = new Point();
        var eyeHeight = 0.0;
        var sleeping = clip.StartsWith("doze-", StringComparison.Ordinal);
        var onLedge = clip.StartsWith("window-rest-", StringComparison.Ordinal);
        var side = clip is "idle-left" or "idle-right" or "left-view" or "right-view";
        var seated = clip == "perch";
        var listening = Wave(time, 6.7, 1.2, .45);
        var glance = Wave(time, 7.9, 3.5, 1.35);
        var tailTwitch = Wave(time, onLedge ? 4.8 : 5.9, .6, .93);

        if (side)
        {
            tail = Region(48, 143, 35, 28);
            tailMotion = Joint(66, 122, 5.0 * tailTwitch, hinge: true);
            eyes = new Point(X(191) / size, X(191) / size);
            eyeHeight = 66.7;
        }
        else if (onLedge)
        {
            tail = Region(52, 188, 31, 44);
            tailMotion = Joint(57, 154, -5.5 * tailTwitch, hinge: true);
            eyes = new Point(X(178) / size, X(178) / size);
            eyeHeight = 99.5;
        }
        else if (clip == "front")
        {
            eyes = new Point(112 / size, 128 / size);
            eyeHeight = 75;
        }
        else if (clip == "front-three-quarter")
        {
            eyes = new Point(75 / size, 90 / size);
            eyeHeight = 74.5;
        }

        var turn = gestureDirection == 0 ? -1 : Math.Sign(gestureDirection);
        var skull = clip switch
        {
            "idle-left" or "idle-right" or "left-view" or "right-view" => new Point(183, 61),
            "front" or "rear" => new Point(120, 66),
            "front-three-quarter" => new Point(82, 67),
            "rear-three-quarter" => new Point(152, 59),
            "perch" => new Point(115, 63),
            _ => default
        };
        if (skull.X != 0)
        {
            var pivotX = side ? 167 : skull.X;
            var pivotY = side ? 85 : 91;
            var angle = 2.0 * glance;
            var shift = new Vector();
            if (gesture == PetGesture.Listen)
            {
                var lean = Envelope(gestureProgress, .15, .41, .55, 1);
                var ear = Envelope(gestureProgress, 0, .10, .17, .40);
                var earX = clip switch
                {
                    "front" or "rear" => 120 + 14 * turn,
                    "front-three-quarter" => 71,
                    "rear-three-quarter" => 162,
                    "perch" => 108,
                    _ => 181
                };
                var earY = clip == "perch" ? 51 : 48;
                var towardSound = side || seated ? -1 : turn;
                angle += (side || seated ? -8.0 : 10.0 * towardSound) * lean;
                shift = new Vector(towardSound * (side ? 3.0 : 1.6), -.7) * lean;
                accent = Region(earX, earY, side ? 13 : 12, 20);
                accentMotion = Joint(earX, earY + 11, towardSound * 16 * ear, hinge: true);
            }
            else
            {
                var earX = side ? 181 : skull.X - 10;
                var earY = clip == "perch" ? 51 : 48;
                accent = Region(earX, earY, 12, 20);
                accentMotion = Joint(earX, earY + 11, -8 * listening, hinge: true);
                if (gesture == PetGesture.Sniff && (side || seated))
                {
                    var reach = Envelope(gestureProgress, 0, .24, .74, 1);
                    var sniff = Pulse(gestureProgress, .25, .46) + Pulse(gestureProgress, .48, .70);
                    angle -= 4 * reach + 5 * sniff;
                    shift = new Vector(3.4 * reach, .6 * reach - 1.1 * sniff);
                }
            }

            // The head moves as a unit. Across the lower margin the movement falls off into the neck.
            head = Region(skull.X, skull.Y, side ? 55 : 43, 51);
            headMotion = Joint(pivotX, pivotY, angle, shift);
        }
        else if (sleeping || onLedge)
        {
            var earX = onLedge ? 168 : 172;
            var earY = onLedge ? 81 : 111;
            accent = Region(earX, earY, onLedge ? 18 : 32, 23);
            var ear = gesture == PetGesture.DreamTwitch
                ? Pulse(gestureProgress, .10, .32) - .40 * Pulse(gestureProgress, .32, .46)
                : .38 * listening;
            accentMotion = Joint(earX - 2, earY + 12, 14 * ear, hinge: true);
            if (gesture == PetGesture.DreamTwitch)
            {
                var paw = Pulse(gestureProgress, .31, .56) - .24 * Pulse(gestureProgress, .56, .71);
                head = Region(onLedge ? 157 : 169, 155, 23, 9);
                headMotion = Joint(onLedge ? 145 : 154, 156, -4 * paw, new Vector(1.5, -.6) * paw);
            }
        }
        else if (gesture == PetGesture.Nuzzle && clip == "pet")
        {
            var lean = Envelope(gestureProgress, 0, .32, .65, 1);
            var rub = Pulse(gestureProgress, .32, .73);
            head = Region(106, 65, 48, 54);
            headMotion = Joint(110, 94, turn * (9.5 * lean + 2.3 * rub),
                new Vector(turn * 2.6 * lean, -1.2 * rub));
        }

        effect.HeadRegion = head;
        effect.TailRegion = tail;
        effect.HeadMotion = headMotion;
        effect.TailMotion = tailMotion;
        effect.GestureRegion = accent;
        effect.GestureMotion = accentMotion;
        effect.Eyes = eyes;
        effect.EyeHeight = eyeHeight / size;
        var blinking = (time + .54 * Math.Sin(time * .89)) % (onLedge ? 5.2 : 4.2) - 1.8;
        var blink = eyeHeight == 0 ? 0 : 3.7 * Smooth(blinking / .08) * (1 - Smooth((blinking - .11) / .14));
        effect.Blink = blink;
    }

    private static double Envelope(double progress, double start, double open, double close, double end) =>
        Smooth((progress - start) / (open - start)) * Smooth((end - progress) / (end - close));

    private static double Pulse(double progress, double start, double end)
    {
        var t = Math.Clamp((progress - start) / (end - start), 0, 1);
        var pulse = Math.Sin(Math.PI * t);
        return pulse * pulse;
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
            PetState.Drag => Breath.Blend(Standing, Hanging, progress),
            PetState.HuntAim or PetState.Catch => new(135, 119, 51, 34, 1.6, 2.5),
            PetState.Stalk or PetState.Pounce => new(135, 116, 55, 38, .9, 2.4),
            PetState.Stretch => Breath.Blend(Standing, new(117, 101, 68, 43, 1.5, 3.0), progress),
            PetState.Walk or PetState.ApproachJump or PetState.Jump or PetState.Fall or PetState.Land => Standing with { Depth = .72, Period = 2.5 },
            _ when clip is "front" or "front-three-quarter" or "rear" or "rear-three-quarter" => new(120, 101, 38, 49, 1.8, 2.9),
            _ => Standing
        };
    }
}
