using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Jellybean.Engine;
using Jellybean.Native;
using Jellybean.UI;

namespace Jellybean.Services;

public sealed class DesktopSession : IDisposable
{
    public UserSettings Settings { get; }
    public PetEngine Engine { get; }
    public bool Hidden { get; private set; }
    private readonly SurfaceProvider _surfaces;
    private readonly SpriteCatalog _sprites = new();
    private readonly PetWindow _pet;
    private readonly EffectWindow _effects, _toy;
    private readonly TrayService _tray;
    private readonly double _dpi = Win32.PrimaryMonitorScale();
    private readonly DispatcherTimer _maintenance = new() { Interval=TimeSpan.FromSeconds(1) };
    private readonly EventWaitHandle _showSignal;
    private SettingsWindow? _settingsWindow;
    private bool _running;
    private DateTime? _intervalDue, _dailyDue;
    private bool _pendingReminder;

    public DesktopSession(EventWaitHandle showSignal)
    {
        _showSignal=showSignal;
        Settings=UserSettings.Load();
        _surfaces=new SurfaceProvider(_dpi) { MinimumClearance=136*Settings.Scale };
        Engine=new PetEngine(_surfaces,Settings,_sprites.WalkStrideLeft,_sprites.WalkStrideRight);
        _pet=new PetWindow(Engine,Settings,_sprites,_dpi);
        _effects=new EffectWindow(_dpi); _toy=new EffectWindow(_dpi,true);
        _tray=new TrayService(this);
        _pet.MenuRequested+=_tray.ShowMenu;
        _pet.FrameUpdated+=UpdateEffects;
        _pet.ScaleRequested+=delta=> { Settings.Scale=Math.Round(Math.Clamp(Settings.Scale+delta,.7,1.8),1); SaveSettings(); };
        _maintenance.Tick+=(_,_)=>Maintain();
        ScheduleReminders();
        UpdateVisibility();
        _maintenance.Start();
        if(Settings.CenterX is null) Engine.Say("单击摸摸我，双击陪我玩。右键有更多动作。" );
    }

    private void Maintain()
    {
        if(_showSignal.WaitOne(0)) ResetPosition();
        UpdateVisibility();
        if(_running && Settings.Topmost && Win32.IsForegroundFullscreen((uint)Environment.ProcessId))
        { _pet.RaiseAboveApps(); _effects.RaiseAboveApps(); _toy.RaiseAboveApps(); }
        var now=DateTime.Now;
        if(_intervalDue is { } interval && now>=interval)
        { _pendingReminder=true; _intervalDue=now.AddMinutes(Settings.ReminderMinutes); }
        if(_dailyDue is { } daily && now>=daily)
        { _pendingReminder=true; _dailyDue=daily.AddDays(1); }
        if(_pendingReminder && _running && Engine.State is not (PetState.Drag or PetState.Jump or PetState.Fall))
        { _pendingReminder=false; PreviewReminder(); }
    }

    private void UpdateVisibility()
    {
        var shouldRun=!Hidden && !(Settings.HideInFullscreen && GameDetector.ShouldHideForGame((uint)Environment.ProcessId));
        if(shouldRun==_running) return;
        _running=shouldRun;
        if(!shouldRun) Engine.StopToy();
        _pet.SetRunning(shouldRun);
        UpdateEffects();
    }

    private void UpdateEffects()
    {
        if(Engine.ToyActive)
        {
            Win32.GetCursorPos(out var point); Engine.ToyPosition=new Point(point.X/_dpi,point.Y/_dpi);
            if((Win32.GetAsyncKeyState(0x1B)&0x8000)!=0) Engine.StopToy();
        }
        _effects.Update(Engine,Settings,_running,_pet.HeadHeight); _toy.Update(Engine,Settings,_running);
    }

    public void Perform(Action action)
    {
        Engine.Interact(); Hidden=false; UpdateVisibility(); action(); _pet.Render(); UpdateEffects();
    }

    public void ShowAngle(string clip) => Perform(()=>Engine.ShowAngle(clip));
    public void SetHidden(bool hidden) { Hidden=hidden; UpdateVisibility(); }
    public void ResetPosition() => Perform(Engine.ResetPosition);

    public void SaveSettings()
    {
        _surfaces.MinimumClearance=136*Settings.Scale;
        Engine.BringIntoView();
        Settings.CenterX=Engine.CenterX; Settings.FeetY=Engine.FeetY;
        Settings.Save(); _pet.Render(); UpdateVisibility();
    }

    private void ScheduleReminders()
    {
        _intervalDue=Settings.ReminderMinutes>0?DateTime.Now.AddMinutes(Settings.ReminderMinutes):null;
        _dailyDue=null;
        if(TimeOnly.TryParseExact(Settings.DailyReminder,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))
        {
            var due=DateTime.Today.Add(time.ToTimeSpan());
            _dailyDue=due>DateTime.Now?due:due.AddDays(1);
        }
    }

    public void PreviewReminder()
    {
        Engine.Remind();
        if(!Settings.Silent) System.Media.SystemSounds.Asterisk.Play();
        UpdateEffects();
    }

    public void OpenSettings()
    {
        Engine.Interact();
        if(_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        _settingsWindow=new SettingsWindow(Settings,_sprites);
        _settingsWindow.ScalePreview+=scale=>
        {
            Engine.Interact(); Settings.Scale=scale; _surfaces.MinimumClearance=136*scale;
            _pet.Render(); UpdateEffects();
        };
        _settingsWindow.SettingsSaved+=()=> { SaveSettings(); ScheduleReminders(); };
        _settingsWindow.PreviewReminder+=PreviewReminder;
        _settingsWindow.Closed+=(_,_)=> { _settingsWindow=null; Settings.Save(); };
        _settingsWindow.Show(); _settingsWindow.Activate();
    }

    public void Exit() => Application.Current.Shutdown();

    public void Dispose()
    {
        _maintenance.Stop();
        Settings.CenterX=Engine.CenterX;
        Settings.FeetY=Engine.State==PetState.Peek?Engine.Support?.Top:Engine.FeetY;
        Settings.Save();
        _tray.Dispose(); _settingsWindow?.Close();
        _pet.Close(); _effects.Close(); _toy.Close();
    }
}
