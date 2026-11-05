using System.Text.Json;
using Microsoft.Win32;

namespace Jellybean.Services;

public sealed class UserSettings
{
    public double Scale { get; set; } = 1;
    public bool Topmost { get; set; } = true;
    public bool Autonomous { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool AutoJump { get; set; } = true;
    public bool Silent { get; set; } = true;
    public int ReminderMinutes { get; set; }
    public string DailyReminder { get; set; } = "";
    public double? CenterX { get; set; }
    public double? FeetY { get; set; }

    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jellybean");
    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static UserSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new();
            settings.Scale = Math.Clamp(settings.Scale, 0.7, 1.8);
            if (settings.ReminderMinutes is not (0 or 15 or 30 or 60)) settings.ReminderMinutes = 0;
            return settings;
        }
        catch (JsonException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public static class AutoStart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("Jellybean") is string; }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key);
            if (value) key.SetValue("Jellybean", $"\"{Environment.ProcessPath}\" --autostart");
            else key.DeleteValue("Jellybean", false);
        }
    }
}
