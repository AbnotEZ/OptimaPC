using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcToolkit.Core;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;
using PcToolkit.Core.Presentation;
using PcToolkit.Core.Storage;

namespace PcToolkit.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject, IActivatable
{
    private readonly AppServices _services;
    private CancellationTokenSource? _cts;
    private bool _loadedLatest;

    public HomeViewModel(AppServices services)
    {
        _services = services;
        services.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppServices.CurrentInventory) or nameof(AppServices.CurrentSession))
            {
                Refresh();
            }
            if (e.PropertyName == nameof(AppServices.IsBusy))
            {
                AnalyzeCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public event Action<Type>? NavigateRequested;

    public string Welcome => Loc.F("Home.Welcome", AppInfo.ProductName);
    public ObservableCollection<SummaryLine> Summary { get; } = [];
    public ObservableCollection<string> Facts { get; } = [];

    [ObservableProperty]
    public partial string LastAnalysis { get; set; } = Loc.T("Home.NoAnalysis");

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool HasData { get; set; }

    public string AnalyzeHint => Loc.F("Home.AnalyzeHint", _services.Settings.QuickAnalysisSeconds);

    public void OnActivated()
    {
        if (_loadedLatest)
        {
            return;
        }
        _loadedLatest = true;
        try
        {
            if (_services.CurrentInventory is null && _services.History.LoadLatest() is { } latest)
            {
                _services.Show(latest);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Could not load latest analysis: {ex.Message}");
        }
        Refresh();
    }

    private void Refresh()
    {
        var inv = _services.CurrentInventory;
        var session = _services.CurrentSession;
        Summary.Clear();
        Facts.Clear();
        HasData = inv is not null;
        if (inv is not null)
        {
            foreach (var line in InventoryPresenter.Summary(inv))
            {
                Summary.Add(line);
            }
        }
        foreach (var f in ObservationPresenter.Facts(inv, session))
        {
            Facts.Add(f);
        }

        // El último análisis guardado, aunque ahora se muestre otro inventario.
        AnalysisSummary? latest = null;
        try
        {
            latest = _services.History.List().Items.FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"Could not list history: {ex.Message}");
        }
        LastAnalysis = latest is null
            ? Loc.T("Home.NoAnalysis")
            : Loc.F("Home.LastAnalysis",
                Fmt.DateTime(latest.CreatedAt),
                latest.InventoryDuration is { } d ? Fmt.Duration(d) : "—",
                latest.SessionDuration is { } s ? Fmt.Duration(s) : "—");
    }

    private bool CanAnalyze() => !_services.IsBusy;

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        _services.IsBusy = true;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        var started = DateTimeOffset.Now;
        try
        {
            Status = Loc.T("Home.Step.Inventory");
            Progress = 0;
            var inv = await _services.Inventory.CollectAsync(_cts.Token);

            var duration = TimeSpan.FromSeconds(_services.Settings.QuickAnalysisSeconds);
            var options = new SessionOptions(duration, TimeSpan.FromSeconds(1), SessionContext.Unspecified);
            var ctx = new SamplerContext(inv.Cpu.MaxClockMHz.HasValue ? inv.Cpu.MaxClockMHz.Value : null, inv.Gpus.Value ?? []);
            var progress = new Progress<Core.Monitoring.SessionProgress>(p =>
            {
                Progress = Math.Clamp(p.Elapsed / p.Total, 0, 1);
                Status = Loc.F("Home.Step.Sampling", (int)p.Elapsed.TotalSeconds, (int)p.Total.TotalSeconds);
            });
            var token = _cts.Token;
            var session = await Task.Run(() => _services.Monitoring.RunAsync(options, ctx, progress, token), token);

            var record = new AnalysisRecord
            {
                Id = HistoryStore.NewId(started),
                AppVersion = AppInfo.Version,
                CreatedAt = started,
                Inventory = inv,
                Session = session,
            };
            await Task.Run(() => _services.History.Save(record));
            _services.Show(record);
            Status = session.Cancelled ? Loc.T("Home.Done.Cancelled") : Loc.T("Home.Done");
        }
        catch (OperationCanceledException)
        {
            Status = Loc.T("Home.Cancelled");
        }
        catch (Exception ex)
        {
            AppLog.Error("Analysis failed", ex);
            Status = Loc.F("Home.Failed", ex.Message);
        }
        finally
        {
            Progress = 0;
            IsRunning = false;
            _services.IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _cts?.Cancel();

    private bool CanExport() => !_services.IsBusy && _services.CurrentInventory is not null;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export() => ExportDialog.Show(_services);

    [RelayCommand]
    private void GoToMyPc() => NavigateRequested?.Invoke(typeof(MyPcViewModel));

    [RelayCommand]
    private void GoToDiagnostics() => NavigateRequested?.Invoke(typeof(DiagnosticsViewModel));
}
