using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Export;
using PcToolkit.Core.Localization;

namespace PcToolkit.App;

/// <summary>
/// Exportación voluntaria con vista previa. Lo que se ve en la vista previa es exactamente lo que se guarda.
/// Nada se envía a Internet.
/// </summary>
public sealed partial class ExportViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly DateTimeOffset _now = DateTimeOffset.Now;

    public ExportViewModel(AppServices services)
    {
        _services = services;
        HasSession = services.CurrentSession is not null;
        Rebuild();
    }

    public bool HasSession { get; }

    [ObservableProperty] public partial bool AsJson { get; set; } = true;
    [ObservableProperty] public partial bool IncludeStartupCommands { get; set; }
    [ObservableProperty] public partial bool IncludeProcesses { get; set; } = true;
    [ObservableProperty] public partial string Preview { get; set; } = "";
    [ObservableProperty] public partial string Status { get; set; } = "";

    partial void OnAsJsonChanged(bool value) => Rebuild();
    partial void OnIncludeStartupCommandsChanged(bool value) => Rebuild();
    partial void OnIncludeProcessesChanged(bool value) => Rebuild();

    private void Rebuild()
    {
        var options = new ExportOptions(AsJson ? ReportFormat.Json : ReportFormat.Text, IncludeStartupCommands, IncludeProcesses);
        Preview = _services.Reports.Build(_services.CurrentInventory, _services.CurrentSession, options, _now);
    }

    [RelayCommand]
    private void Save()
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"optimapc-{_now:yyyyMMdd-HHmm}.optimapc-report.{(AsJson ? "json" : "txt")}",
            Filter = AsJson ? "JSON (*.json)|*.json" : "Text (*.txt)|*.txt",
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, Preview, new System.Text.UTF8Encoding(false));
            Status = Loc.F("Export.Saved", Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Export failed", ex);
            Status = Loc.F("Home.Failed", ex.Message);
        }
    }
}

public static class ExportDialog
{
    public static void Show(AppServices services)
    {
        var window = new Views.ExportWindow
        {
            DataContext = new ExportViewModel(services),
            Owner = Application.Current.MainWindow,
        };
        window.ShowDialog();
    }
}
