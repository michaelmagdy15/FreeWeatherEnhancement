using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using SkyWeave.App.Models;

namespace SkyWeave.App.ViewModels;

public partial class TafViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _tafText = "---";

    [ObservableProperty]
    private string _tafStation = "---";

    [ObservableProperty]
    private string _tafValidity = "---";

    [ObservableProperty]
    private string _tafFlightCategory = "---";

    [ObservableProperty]
    private string _tafWind = "---";

    [ObservableProperty]
    private string _tafVisibility = "---";

    [ObservableProperty]
    private bool _tafDivergence;

    public ObservableCollection<TafGroupViewModel> TafGroups { get; } = new();
}
