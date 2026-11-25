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

            var walkAtlas = mirror ? "photo-walk-left" : "photo-walk";
            AddAction("walk-" + direction, walkAtlas, Range(0, 11), EvenDurations(1200, 12));
            AddAction("idle-" + direction, walkAtlas, [0], [1000]);

            Add("takeoff", "jump", [-1, 0, 1, 2, 3, 4], [28, 41, 48, 55, 46, 42]);
            Add("air", "jump", [4, 5, 6, 7, 8], [43, 66, 65, 68, 78]);
            Add("fall", "jump", [6, 7, 8], [42, 57, 101]);
            Add("fall-roll", "fall-roll", Range(0, 13), [51, 58, 68, 67, 62, 71, 66, 69, 69, 63, 73, 81, 91, 111]);
            Add("fall-ready", "fall-roll", [13], [1000]);
            Add("land", "jump", [9, 10, 11, 12, -1], [44, 54, 65, 60, 57]);

            Add("crouch", "hunt", [0], [1000]);
            Play("stalk", "hunt", [0, 1, 2, 3, 2, 1], 800);
            Add("pounce", "hunt", [0, 1, 4, 5, 6, 7], [50, 50, 48, 80, 82, 70]);
            Add("catch", "hunt", [7], [180]);
            Add("pickup", "pickup-right", [-1, 0, 1, 2, 3, 4], [35, 50, 56, 56, 58, 45]);
            Add("held", "held", [0, 1, 0], [1180, 240, 1180]);

            Add("stretch", "stretch", [-1, 2, 0, 3, 1, 3, 0, 2, -1], [380, 170, 180, 180, 850, 180, 180, 180, 500]);
            Add("lie", "sleep", [-1, 4, 5, 0, 6, 1, 7, 2, 3], [190, 128, 166, 192, 155, 162, 172, 182, 153]);
            Add("doze", "sleep", [3], [1000]);
            Add("rise", "sleep", [3, 2, 7, 1, 6, 0, 5, 4, -1], [85, 95, 133, 145, 143, 138, 123, 109, 129]);
            Add("window-lie", "ledge", [-1, 3, 4, 0, 5, 1, 2], [140, 145, 155, 150, 155, 165, 140]);
            Add("window-rest", "ledge", [2], [1000]);
            Add("window-rise", "ledge", [2, 1, 5, 0, 4, 3, -1], [105, 135, 135, 135, 155, 155, 130]);

            Add("gesture-shift", "gesture-shift", [-1, 0, 1, 2, 3, 0, -1], [140, 125, 130, 155, 155, 140, 155]);
            Add("gesture-ledge", "gesture-ledge", [-1, 0, 1, 2, 1, 0, -1], [100, 145, 130, 250, 130, 145, 100]);
            AddAction("gesture-reach-" + direction, "gesture-reach", [-1, 0, 1, 2, 1, 0, -1],
                [120, 140, 150, 200, 150, 120, 120], mirror, "crouch-" + direction);
            Add("gesture-miss", "gesture-miss", [0, 1, 2, 3, 2, 4, 0], [85, 85, 125, 350, 125, 125, 105]);
            Add("gesture-yawn", "gesture-yawn", [-1, 0, 1, 0, -1], [115, 185, 350, 185, 165]);
            AddAction("gesture-roll-" + direction, "gesture-roll", [-1, 0, 1, 2, 3, 2, 1, 0, -1],
                [45, 60, 70, 80, 495, 80, 65, 55, 50], mirror, "doze-" + direction);
            AddAction("gesture-curl-" + direction, "gesture-curl", [-1, 0, 1, 2, 1, 0, -1],
                [60, 110, 110, 455, 105, 100, 60], mirror, "doze-" + direction);
            AddAction("gesture-knead-" + direction, "gesture-knead", [-1, 0, 1, 0, 1, 0, -1],
                [100, 160, 160, 160, 160, 160, 100], mirror, "doze-" + direction);
        }

        AddAction("pet", "pet", [0, 1, 2, 3, 1, 0], [530, 330, 120, 350, 290, 480]);
        AddAction("perch", "perch", [0, 1, 0, 2, 0], [915, 350, 510, 115, 710]);
        AddAction("peek", "pet", [0], [1000]);
        AddAction("gesture-groom", "gesture-groom", [-1, 0, 1, 0, 1, 2, -1],
            [125, 150, 145, 130, 180, 140, 130], standingClip: "peek");
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
