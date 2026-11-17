using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcToolkit.Core;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Presentation;
using PcToolkit.Core.Storage;

namespace PcToolkit.App.ViewModels;

public sealed record HistoryRow(string Id, string When, string Details, AnalysisSummary Summary);

public sealed partial class HistoryViewModel(AppServices services) : ObservableObject, IActivatable
{
    public ObservableCollection<HistoryRow> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand), nameof(DeleteCommand))]
    public partial HistoryRow? Selected { get; set; }

    [ObservableProperty] public partial string Status { get; set; } = "";

    public string Limits => Loc.F("History.Limits", services.History.MaxRecords, Fmt.Bytes((ulong)services.History.MaxTotalBytes));

    public void OnActivated() => Reload();

    private void Reload()
    {
        Items.Clear();
        try
        {
            var listing = services.History.List();
            foreach (var s in listing.Items)
            {
                var parts = new List<string>();
                if (s.InventoryDuration is { } inv)
                {
                    parts.Add(Loc.F("History.Inventory", Fmt.Duration(inv)));
                }
                if (s.SessionDuration is { } ses)
                {
                    parts.Add(Loc.F("History.Session", Fmt.Duration(ses), s.Context is { } c ? Fmt.Context(c) : "—"));
                }
                if (s.Cancelled)
                {
                    parts.Add(Loc.T("History.Cancelled"));
                }
                Items.Add(new HistoryRow(s.Id, Fmt.DateTime(s.CreatedAt), string.Join(" · ", parts), s));
            }
            Status = listing.UnreadableFiles > 0 ? Loc.F("History.Unreadable", listing.UnreadableFiles) : (Items.Count == 0 ? Loc.T("History.Empty") : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn($"History listing failed: {ex.Message}");
            Status = Loc.F("Home.Failed", ex.Message);
        }
    }

    private bool HasSelection() => Selected is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Open()
    {
        if (Selected is null)
        {
            return;
        }
        var record = services.History.Load(Selected.Id);
        if (record is null)
        {
            Status = Loc.T("History.LoadFailed");
            return;
        }
        services.Show(record);
        Status = Loc.F("History.Opened", Selected.When);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is null)
        {
            return;
        }
        if (MessageBox.Show(Loc.F("History.ConfirmDelete", Selected.When), AppInfo.ProductName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }
        services.History.Delete(Selected.Id);
        Reload();
    }

    [RelayCommand]
    private void DeleteAll()
    {
        if (MessageBox.Show(Loc.T("History.ConfirmDeleteAll"), AppInfo.ProductName, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }
        var n = services.History.DeleteAll();
        Reload();
        Status = Loc.F("History.Deleted", n);
    }
}
