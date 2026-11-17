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
                AddAction(clip + "-" + direction, atlas, frames, durations, mirror,
                    frames.Contains(-1) ? "idle-" + direction : null);
            void Play(string clip, string atlas, int[] frames, int milliseconds) =>
                Add(clip, atlas, frames, EvenDurations(milliseconds, frames.Length));

            Play("takeoff", "jump", [-1, ..Range(1, 8)], 260);
            Play("air", "jump", Range(8, 32), 320);
            Play("fall", "jump", Range(26, 31), 200);
            // Timing follows the drawn poses, with no dissolve between silhouettes.
            Add("fall-roll", "fall-roll", Range(0, 13), [51, 58, 68, 67, 62, 71, 66, 69, 69, 63, 73, 81, 91, 111]);
            Add("fall-ready", "jump", [31], [1000]);
            Play("land", "jump", Range(32, 40), 280);

            Add("crouch", "hunt", [0], [1000]);
            var stalk = Range(0, 16).Concat(Range(1, 15).Reverse()).ToArray();
            Play("stalk", "hunt", stalk, 800);
            Add("pounce", "hunt", Range(16, 40),
                EvenDurations(100, 5).Concat(EvenDurations(280, 20)).ToArray());
            Play("catch", "hunt", Range(37, 40), 180);
            var pickup = new[] { -1 }.Concat(Range(0, 16)).ToArray();
            AddAction("pickup-" + direction, "pickup-" + direction, pickup,
                EvenDurations(300, pickup.Length), standingClip: "idle-" + direction);
            Play("held", "held", Range(0, 38), 2600);

            Play("stretch", "stretch", [-1, ..Range(1, 39), -1], 2800);
            Play("lie", "sleep", [-1, ..Range(1, 40)], 1500);
            Add("doze", "sleep", [40], [1000]);
            Play("rise", "sleep", [..Range(1, 40).Reverse(), -1], 1100);
            Play("window-lie", "ledge", [-1, ..Range(1, 24)], 1050);
            Add("window-rest", "ledge", [24], [1000]);
            Play("window-rise", "ledge", [..Range(1, 24).Reverse(), -1], 950);

            foreach (var gesture in new[] { "shift", "ledge", "reach", "miss", "knead" })
                Play("gesture-" + gesture, "gesture-" + gesture, Range(0, 36), 1000);

            AddAction("gesture-yawn-" + direction, "gesture-yawn", [-1, 0, 1, 2, 3, 4, 5, 0, -1],
                [72, 50, 93, 116, 235, 110, 100, 75, 149], mirror, "idle-" + direction);
            AddAction("gesture-roll-" + direction, "gesture-roll", [-1, 0, 1, 6, 7, 2, 3, 4, 5, 4, 3, 2, 7, 6, 1, 0, -1],
                [25, 32, 52, 31, 26, 30, 47, 62, 405, 52, 38, 27, 26, 27, 44, 37, 39], mirror, "doze-" + direction);
            AddAction("gesture-curl-" + direction, "gesture-curl", [-1, 0, 1, 2, 3, 4, 5, 4, 3, 2, 1, 0, -1],
                [30, 33, 50, 62, 48, 59, 475, 44, 34, 40, 44, 38, 43], mirror, "doze-" + direction);
        }

        var blink = EvenDurations(1500, 17);
        blink[0] += 260;
        blink[^1] += 340;
        AddAction("pet", "pet", Range(0, 16), blink);
        AddAction("perch", "perch", Range(0, 38), EvenDurations(2600, 39));
        AddAction("peek", "pet", [0], [1000]);
        AddAction("gesture-groom", "gesture-groom", [-1, 0, 6, 7, 1, 2, 3, 2, 3, 2, 4, 5, 7, 6, 0, -1],
            [50, 42, 45, 37, 55, 50, 66, 40, 70, 50, 203, 110, 44, 42, 40, 56], standingClip: "peek");
    }

    private static void AddAction(string name, string atlas, int[] frames, int[] durations, bool mirror = false, string? standingClip = null)
    {
        var file = $"actions/{atlas}.png";
        if (!File.Exists(Path.Combine(Output, file)))
            throw new FileNotFoundException("Approved action atlas is missing.", file);
        Clips[name] = new { file, columns = 6, frames, durations, blendMilliseconds = 0, scale = 2, mirror, standingClip };
    }

    private static int[] Range(int first, int last) => Enumerable.Range(first, last - first + 1).ToArray();

    private static int[] EvenDurations(int milliseconds, int frames) =>
        Enumerable.Range(0, frames).Select(i => (i + 1) * milliseconds / frames - i * milliseconds / frames).ToArray();
}
