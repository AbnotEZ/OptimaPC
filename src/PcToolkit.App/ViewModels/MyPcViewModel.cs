using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcToolkit.Core.Diagnostics;
using PcToolkit.Core.Localization;
using PcToolkit.Core.Presentation;

namespace PcToolkit.App.ViewModels;

public sealed partial class MyPcViewModel : ObservableObject, IActivatable
{
    private readonly AppServices _services;

    public MyPcViewModel(AppServices services)
    {
        _services = services;
        services.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppServices.CurrentInventory))
            {
                Refresh();
            }
            if (e.PropertyName == nameof(AppServices.IsBusy))
            {
                ReadCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public ObservableCollection<InfoGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial string Header { get; set; } = Loc.T("MyPc.Empty");

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public void OnActivated() => Refresh();

    private void Refresh()
    {
        Groups.Clear();
        var inv = _services.CurrentInventory;
        if (inv is null)
        {
            Header = Loc.T("MyPc.Empty");
            return;
        }
        Header = Loc.F("MyPc.CapturedAt", Fmt.DateTime(inv.CapturedAt), Fmt.Duration(inv.Duration));
        foreach (var g in InventoryPresenter.Groups(inv))
        {
            Groups.Add(g);
        }
    }

    private bool CanRead() => !_services.IsBusy;

    /// <summary>Vuelve a leer solo el inventario (sin muestreo). No se guarda en el historial hasta un análisis completo.</summary>
    [RelayCommand(CanExecute = nameof(CanRead))]
    private async Task ReadAsync()
    {
        _services.IsBusy = true;
        Status = Loc.T("Home.Step.Inventory");
        try
        {
            var inv = await _services.Inventory.CollectAsync();
            _services.CurrentRecord = null;
            _services.CurrentSession = null;
            _services.CurrentInventory = inv;
            Status = "";
        }
        catch (Exception ex)
        {
            AppLog.Error("Inventory failed", ex);
            Status = Loc.F("Home.Failed", ex.Message);
        }
        finally
        {
            _services.IsBusy = false;
        }
    }
}
