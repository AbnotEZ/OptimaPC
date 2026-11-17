using CommunityToolkit.Mvvm.ComponentModel;
using PcToolkit.Core;
using PcToolkit.Core.Localization;

namespace PcToolkit.App.ViewModels;

public sealed record NavItem(string Title, object Page, bool IsPending)
{
    public string AccessibleName => IsPending ? $"{Title} ({Loc.T("Nav.PendingBadge")})" : Title;
}

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(AppServices services)
    {
        Services = services;
        var home = new HomeViewModel(services);
        Items =
        [
            new NavItem(Loc.T("Nav.Home"), home, false),
            new NavItem(Loc.T("Nav.MyPc"), new MyPcViewModel(services), false),
            new NavItem(Loc.T("Nav.Diagnostics"), new DiagnosticsViewModel(services), false),
            new NavItem(Loc.T("Nav.Recommendations"), new PendingViewModel("Nav.Recommendations", "Pending.Recommendations", 2), true),
            new NavItem(Loc.T("Nav.Optimizations"), new PendingViewModel("Nav.Optimizations", "Pending.Optimizations", 3), true),
            new NavItem(Loc.T("Nav.Measurements"), new PendingViewModel("Nav.Measurements", "Pending.Measurements", 4), true),
            new NavItem(Loc.T("Nav.History"), new HistoryViewModel(services), false),
            new NavItem(Loc.T("Nav.Settings"), new SettingsViewModel(services), false),
        ];
        Selected = Items[0];
        home.NavigateRequested += page => Selected = Items.First(i => ReferenceEquals(i.Page.GetType(), page));
    }

    public AppServices Services { get; }
    public string Title => $"{AppInfo.ProductName} {AppInfo.Version}";
    public IReadOnlyList<NavItem> Items { get; }

    [ObservableProperty]
    public partial NavItem Selected { get; set; }

    partial void OnSelectedChanged(NavItem value)
    {
        if (value.Page is IActivatable a)
        {
            a.OnActivated();
        }
    }
}

/// <summary>Página que refresca su contenido al mostrarse.</summary>
public interface IActivatable
{
    void OnActivated();
}

public sealed class PendingViewModel(string titleKey, string descriptionKey, int phase)
{
    public string Title { get; } = Loc.T(titleKey);
    public string Description { get; } = Loc.T(descriptionKey);
    public string PhaseText { get; } = Loc.F("Pending.Phase", phase);
}
