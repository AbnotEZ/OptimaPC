using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcToolkit.Core;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Model;
using PcToolkit.Core.Monitoring;
using PcToolkit.Core.Ports;
using PcToolkit.Core.Presentation;
using PcToolkit.Core.Storage;

namespace PcToolkit.App.ViewModels;

public sealed record Option<T>(string Label, T Value);

public sealed record LiveRow(string Name, string Value);

public sealed partial class DiagnosticsViewModel : ObservableObject, IActivatable
{
    private readonly AppServices _services;
    private CancellationTokenSource? _cts;

    public DiagnosticsViewModel(AppServices services)
    {
        _services = services;
        Durations = new[] { 15, 30, 60, 120, 300, 600, 1800 }
            .Select(s => new Option<TimeSpan>(Fmt.Duration(TimeSpan.FromSeconds(s)), TimeSpan.FromSeconds(s))).ToList();
        Intervals = new[] { 500, 1000, 2000, 5000 }
            .Select(ms => new Option<TimeSpan>(Fmt.Duration(TimeSpan.FromMilliseconds(ms)), TimeSpan.FromMilliseconds(ms))).ToList();
        Contexts = Enum.GetValues<SessionContext>().Select(c => new Option<SessionContext>(Fmt.Context(c), c)).ToList();

        SelectedDuration = Durations.FirstOrDefault(d => d.Value.TotalSeconds == services.Settings.DefaultSessionSeconds) ?? Durations[2];
        SelectedInterval = Intervals.FirstOrDefault(i => i.Value.TotalMilliseconds == services.Settings.DefaultIntervalMilliseconds) ?? Intervals[1];
        SelectedContext = Contexts[0];

        services.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppServices.CurrentSession))
            {
                ShowResult(services.CurrentSession);
            }
            if (e.PropertyName == nameof(AppServices.IsBusy))
            {
                StartCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public IReadOnlyList<Option<TimeSpan>> Durations { get; }
    public IReadOnlyList<Option<TimeSpan>> Intervals { get; }
    public IReadOnlyList<Option<SessionContext>> Contexts { get; }

    [ObservableProperty] public partial Option<TimeSpan> SelectedDuration { get; set; }
    [ObservableProperty] public partial Option<TimeSpan> SelectedInterval { get; set; }
    [ObservableProperty] public partial Option<SessionContext> SelectedContext { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = Loc.T("Diag.NoSession");
    [ObservableProperty] public partial bool HasResult { get; set; }

    public ObservableCollection<LiveRow> Live { get; } = [];
    public ObservableCollection<MetricRow> Metrics { get; } = [];
    public ObservableCollection<string> Unsupported { get; } = [];
    public ObservableCollection<ProcessRow> Processes { get; } = [];

    public void OnActivated() => ShowResult(_services.CurrentSession);

    private void ShowResult(SessionResult? session)
    {
        Metrics.Clear();
        Unsupported.Clear();
        Processes.Clear();
        HasResult = session is not null;
        if (session is null)
        {
            Description = Loc.T("Diag.NoSession");
            return;
        }
        Description = SessionPresenter.Description(session);
        foreach (var m in SessionPresenter.Metrics(session))
        {
            Metrics.Add(m);
        }
        foreach (var u in SessionPresenter.Unsupported(session))
        {
            Unsupported.Add(u);
        }
        foreach (var p in SessionPresenter.Processes(session))
        {
            Processes.Add(p);
        }
        if (!session.Processes.HasValue)
        {
            Unsupported.Add($"{Loc.T("Diag.Processes")}: {Fmt.Status(session.Processes.Status)}");
        }
    }

    private bool CanStart() => !_services.IsBusy;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        SessionOptions options;
        try
        {
            options = new SessionOptions(SelectedDuration.Value, SelectedInterval.Value, SelectedContext.Value);
            options.Validate();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Status = ex.Message;
            return;
        }

        _services.IsBusy = true;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        var started = DateTimeOffset.Now;
        try
        {
            // Inventario nuevo en cada sesión: aporta las GPU (sus LUID cambian tras reiniciar)
            // y la frecuencia base de la CPU, y deja el registro guardado coherente.
            Status = Loc.T("Home.Step.Inventory");
            var inv = await _services.Inventory.CollectAsync(_cts.Token);
            var ctx = new SamplerContext(inv.Cpu.MaxClockMHz.HasValue ? inv.Cpu.MaxClockMHz.Value : null, inv.Gpus.Value ?? []);

            var progress = new Progress<SessionProgress>(p =>
            {
                Progress = Math.Clamp(p.Elapsed / p.Total, 0, 1);
                Status = Loc.F("Home.Step.Sampling", (int)p.Elapsed.TotalSeconds, (int)p.Total.TotalSeconds);
                UpdateLive(p);
            });
            Live.Clear();
            var token = _cts.Token;
            var session = await Task.Run(() => _services.Monitoring.RunAsync(options, ctx, progress, token), token);

            var record = new AnalysisRecord { Id = HistoryStore.NewId(started), AppVersion = AppInfo.Version, CreatedAt = started, Inventory = inv, Session = session };
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
            AppLog.Error("Session failed", ex);
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

    private void UpdateLive(SessionProgress p)
    {
        // Valores instantáneos solo como indicación del progreso; el resumen usa la serie completa.
        Live.Clear();
        foreach (var m in p.Metrics)
        {
            var value = p.Latest.TryGetValue(m.Id, out var s) && s.Status == SampleStatus.Valid
                ? $"{Fmt.Number(s.Value!.Value, 1)} {m.Unit}"
                : Loc.T($"SampleStatus.{(p.Latest.TryGetValue(m.Id, out var s2) ? s2.Status : SampleStatus.Missing)}");
            Live.Add(new LiveRow(SessionPresenter.MetricName(m), value));
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _cts?.Cancel();

    private bool CanExport() => !_services.IsBusy && _services.CurrentInventory is not null;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export() => ExportDialog.Show(_services);
}
