using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using PcToolkit.Core;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Privacy;
using PcToolkit.Core.Storage;
using PcToolkit.App.ViewModels;

namespace PcToolkit.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Default();
        var scrubber = new PrivacyScrubber(PrivacyIdentity.FromEnvironment());
        AppLog.Initialize(paths.LogsDirectory, scrubber);

        var settingsStore = new SettingsStore(paths.SettingsFile);
        var settings = settingsStore.Load();
        Loc.SetLanguage(settings.Language);
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Loc.Language);
        CultureInfo.CurrentUICulture = new CultureInfo(Loc.Language);

        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled (AppDomain)", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        AppLog.Info($"{AppInfo.ProductName} {AppInfo.Version} start; lang={Loc.Language}");

        var services = new AppServices(paths, settingsStore, settings, scrubber);
        var window = new MainWindow { DataContext = new MainViewModel(services) };
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled (UI)", e.Exception);
        MessageBox.Show(Loc.F("Error.Unexpected", e.Exception.Message), AppInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
