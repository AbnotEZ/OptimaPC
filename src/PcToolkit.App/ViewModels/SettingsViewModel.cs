using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcToolkit.Core;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Storage;

namespace PcToolkit.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        Languages =
        [
            new Option<string?>(Loc.T("Settings.Language.System"), null),
            new Option<string?>("Español", "es"),
            new Option<string?>("English", "en"),
        ];
        QuickDurations = new[] { 10, 20, 30, 60, 120 }.Select(s => new Option<int>($"{s} s", s)).ToList();
        SelectedLanguage = Languages.First(l => l.Value == services.Settings.Language);
        SelectedQuickDuration = QuickDurations.FirstOrDefault(q => q.Value == services.Settings.QuickAnalysisSeconds) ?? QuickDurations[1];
    }

    public string About => Loc.F("Settings.About", AppInfo.ProductName, AppInfo.Version);
    public string DataFolder => _services.Paths.Root;
    public IReadOnlyList<Option<string?>> Languages { get; }
    public IReadOnlyList<Option<int>> QuickDurations { get; }

    [ObservableProperty] public partial Option<string?> SelectedLanguage { get; set; }
    [ObservableProperty] public partial Option<int> SelectedQuickDuration { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";

    [RelayCommand]
    private void Save()
    {
        var updated = _services.Settings with
        {
            Language = SelectedLanguage.Value,
            QuickAnalysisSeconds = SelectedQuickDuration.Value,
        };
        var languageChanged = updated.Language != _services.Settings.Language;
        try
        {
            _services.SettingsStore.Save(updated);
            _services.Settings = updated.Normalized();
            Status = languageChanged ? Loc.T("Settings.SavedRestart") : Loc.T("Settings.Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Settings save failed", ex);
            Status = Loc.F("Home.Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_services.Paths.Root);
        // Abre la carpeta con el Explorador. La ruta es la de la propia aplicación (no proviene del usuario).
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_services.Paths.Root}\"") { UseShellExecute = false });
    }

    /// <summary>
    /// Borra historial y registros. En esta versión no existen respaldos de restauración
    /// (la aplicación no modifica el sistema); cuando existan, se tratarán por separado.
    /// </summary>
    [RelayCommand]
    private void DeleteLocalData()
    {
        if (MessageBox.Show(Loc.T("Settings.ConfirmDeleteData"), AppInfo.ProductName, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }
        var history = _services.History.DeleteAll();
        var logs = 0;
        try
        {
            if (Directory.Exists(_services.Paths.LogsDirectory))
            {
                foreach (var f in Directory.EnumerateFiles(_services.Paths.LogsDirectory, "app.log*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(f);
                        logs++;
                    }
                    catch (IOException)
                    {
                        // El archivo de registro actual puede estar en uso; se informa en el recuento.
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Warn($"Log deletion failed: {ex.Message}");
        }
        _services.CurrentRecord = null;
        _services.CurrentSession = null;
        _services.CurrentInventory = null;
        Status = Loc.F("Settings.DataDeleted", history, logs);
    }
}
