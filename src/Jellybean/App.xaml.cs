using System.Threading;
using System.Windows;
using Jellybean.Services;

namespace Jellybean;

public partial class App : Application
{
    private Mutex? _instance;
    private EventWaitHandle? _showSignal;
    private DesktopSession? _session;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\Jellybean.Show");
        _instance=new Mutex(true,@"Local\Jellybean.Desktop",out var first);
        if(!first) { _showSignal.Set(); Shutdown(); return; }
        DispatcherUnhandledException+=(_,error)=>
        {
            Directory.CreateDirectory(UserSettings.DataDirectory);
            File.WriteAllText(Path.Combine(UserSettings.DataDirectory,"last-error.txt"),error.Exception.ToString());
            MessageBox.Show("糖豆遇到了一点问题，请重新打开。\n错误详情已保存到：\n"+UserSettings.DataDirectory,"糖豆",MessageBoxButton.OK,MessageBoxImage.Information);
            error.Handled=true; Shutdown(1);
        };
        _session=new DesktopSession(_showSignal,e.Args.Contains("--inspect"));
        if(e.Args.Contains("--settings")) _session.OpenSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _session?.Dispose(); _showSignal?.Dispose(); _instance?.Dispose(); base.OnExit(e);
    }
}
