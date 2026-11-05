using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Jellybean.Native;

internal static class GameDetector
{
    private const int RunningD3dFullScreen = 3;

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHQueryUserNotificationState(out int state);

    public static bool ShouldHideForGame(uint ownProcessId)
    {
        if (!Win32.IsForegroundFullscreen(ownProcessId)) return false;

        var window = Win32.GetForegroundWindow();
        if (window == 0) return false;
        Win32.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0 || processId == ownProcessId) return false;

        string? processName = null;
        string? executablePath = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = Path.GetFileNameWithoutExtension(process.ProcessName);
            try { executablePath = process.MainModule?.FileName; }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }

        if (IsProductivityApp(processName)) return false;

        try
        {
            if (SHQueryUserNotificationState(out var state) == 0 && state == RunningD3dFullScreen)
                return true;
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        return IsGameInstallPath(executablePath);
    }

    internal static bool IsProductivityApp(string? name) => name is not null &&
        (name.Equals("wps", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("wpp", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("et", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("WINWORD", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("EXCEL", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("POWERPNT", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("AcroRd32", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("Acrobat", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("FoxitPDFReader", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase));

    private static bool IsGameInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('/', '\\');
        return normalized.Contains("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\\Epic Games\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\\GOG Games\\", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("\\Riot Games\\", StringComparison.OrdinalIgnoreCase);
    }
}
