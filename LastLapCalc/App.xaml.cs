using System.Windows;
using LastLapCalc.Configuration;

namespace LastLapCalc;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = AppSettingsLoader.Load();
    }
}

