using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Jellybean.Engine;
using Jellybean.Services;

namespace Jellybean.UI;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    public event Action? SettingsSaved;
    public event Action? PreviewReminder;

    public SettingsWindow(UserSettings settings,SpriteCatalog sprites)
    {
        _settings=settings; InitializeComponent();
        Portrait.Source=sprites.Frame("front",0);
        SizeSlider.Value=settings.Scale*100;
        Autonomous.IsChecked=settings.Autonomous; AutoJump.IsChecked=settings.AutoJump;
        AlwaysTop.IsChecked=settings.Topmost; Fullscreen.IsChecked=settings.HideInFullscreen;
        StartAtLogin.IsChecked=AutoStart.Enabled; Silent.IsChecked=settings.Silent;
        ReminderInterval.SelectedItem=ReminderInterval.Items.Cast<ComboBoxItem>().First(i=>i.Tag.ToString()==settings.ReminderMinutes.ToString());
        DailyTime.Text=settings.DailyReminder;
    }

    private void PetSizeChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(SizeLabel is not null) SizeLabel.Text=$"{e.NewValue:0}%";
    }

    private void PreviewClicked(object sender,RoutedEventArgs e) => PreviewReminder?.Invoke();

    private void SaveClicked(object sender,RoutedEventArgs e)
    {
        var daily=DailyTime.Text.Trim();
        if(daily.Length>0 && !TimeOnly.TryParseExact(daily,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
        { Error.Text="时间请填写为 08:30 或 15:30。"; DailyTime.Focus(); return; }
        _settings.Scale=SizeSlider.Value/100;
        _settings.Autonomous=Autonomous.IsChecked==true; _settings.AutoJump=AutoJump.IsChecked==true;
        _settings.Topmost=AlwaysTop.IsChecked==true; _settings.HideInFullscreen=Fullscreen.IsChecked==true;
        _settings.Silent=Silent.IsChecked==true; _settings.DailyReminder=daily;
        _settings.ReminderMinutes=int.Parse(((ComboBoxItem)ReminderInterval.SelectedItem).Tag.ToString()!);
        if(AutoStart.Enabled!=(StartAtLogin.IsChecked==true)) AutoStart.Enabled=StartAtLogin.IsChecked==true;
        SettingsSaved?.Invoke(); Close();
    }
}
