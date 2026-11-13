using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    private static void AddApprovedActions()
    {
        foreach (var direction in new[] { "left", "right" })
        {
            var mirror = direction == "left";
            void Add(string clip, string atlas, int[] frames, int[] durations) =>
                AddAction(clip + "-" + direction, atlas, frames, durations, mirror);
            void Play(string clip, string atlas, int[] frames, int milliseconds) =>
                Add(clip, atlas, frames, EvenDurations(milliseconds, frames.Length));

            Play("takeoff", "jump", Range(0, 8), 260);
            Play("air", "jump", Range(8, 32), 320);
            Play("fall", "jump", Range(26, 31), 200);
            Play("land", "jump", Range(32, 40), 280);

            Add("crouch", "hunt", [0], [1000]);
            var stalk = Range(0, 16).Concat(Range(1, 15).Reverse()).ToArray();
            Play("stalk", "hunt", stalk, 800);
            Add("pounce", "hunt", Range(16, 40),
                EvenDurations(100, 5).Concat(EvenDurations(280, 20)).ToArray());
            Play("catch", "hunt", Range(37, 40), 180);
            Play("held", "held", Range(0, 38), 2600);

            Play("stretch", "stretch", Range(0, 40), 2800);
            Play("lie", "sleep", Range(0, 40), 1500);
            Add("doze", "sleep", [40], [1000]);
            Play("rise", "sleep", Range(0, 40).Reverse().ToArray(), 1100);
            Play("window-lie", "ledge", Range(0, 24), 1050);
            Play("window-rest", "ledge", Range(24, 40), 2300);
            Play("window-rise", "ledge", Range(0, 24).Reverse().ToArray(), 950);
        }

        var blink = EvenDurations(1500, 17);
        blink[0] += 260;
        blink[^1] += 340;
        AddAction("pet", "pet", Range(0, 16), blink);
        AddAction("perch", "perch", Range(0, 38), EvenDurations(2600, 39));
        AddAction("peek", "pet", [0], [1000]);
    }

    private static void AddAction(string name, string atlas, int[] frames, int[] durations, bool mirror = false)
    {
        var file = $"actions/{atlas}.png";
        if (!File.Exists(Path.Combine(Output, file)))
            throw new FileNotFoundException("Approved action atlas is missing.", file);
        Clips[name] = new { file, columns = 6, frames, durations, blendMilliseconds = 0, scale = 2, mirror };
    }

    private static int[] Range(int first, int last) => Enumerable.Range(first, last - first + 1).ToArray();

    private static int[] EvenDurations(int milliseconds, int frames) =>
        Enumerable.Range(0, frames).Select(i => (i + 1) * milliseconds / frames - i * milliseconds / frames).ToArray();
}
