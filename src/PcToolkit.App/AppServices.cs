using CommunityToolkit.Mvvm.ComponentModel;
using PcToolkit.Core.Export;
using PcToolkit.Core.Inventory;
using PcToolkit.Core.Model;
using PcToolkit.Core.Monitoring;
using PcToolkit.Core.Privacy;
using PcToolkit.Core.Storage;
using PcToolkit.Windows;

namespace PcToolkit.App;

/// <summary>Servicios compartidos y estado actual. Se crean una vez al iniciar.</summary>
public sealed partial class AppServices : ObservableObject
{
    public AppServices(AppPaths paths, SettingsStore settingsStore, AppSettings settings, PrivacyScrubber scrubber)
    {
        Paths = paths;
        SettingsStore = settingsStore;
        Settings = settings;
        History = new HistoryStore(paths.HistoryDirectory);
        Inventory = new InventoryService(new WindowsInventorySources());
        Monitoring = new MonitoringSession(new PdhSamplerFactory(), new ProcessSnapshotSource());
        Reports = new ReportBuilder(scrubber);
    }

    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public AppSettings Settings { get; set; }
    public HistoryStore History { get; }
    public InventoryService Inventory { get; }
    public MonitoringSession Monitoring { get; }
    public ReportBuilder Reports { get; }

    /// <summary>Inventario mostrado (el último capturado o uno cargado del historial).</summary>
    [ObservableProperty]
    public partial SystemInventory? CurrentInventory { get; set; }

    /// <summary>Sesión de muestreo mostrada.</summary>
    [ObservableProperty]
    public partial SessionResult? CurrentSession { get; set; }

    /// <summary>Registro del historial que se está mostrando, si procede.</summary>
    [ObservableProperty]
    public partial AnalysisRecord? CurrentRecord { get; set; }

    /// <summary>Hay una operación de lectura en curso. Impide ejecutar dos análisis a la vez.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void Show(AnalysisRecord record)
    {
        CurrentRecord = record;
        CurrentInventory = record.Inventory;
        CurrentSession = record.Session;
    }
}
