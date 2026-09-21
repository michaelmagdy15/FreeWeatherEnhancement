using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SkyWeave.App.Models;

namespace SkyWeave.App.ViewModels;

public partial class RadarViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private string _radarTimestamp = "---";

    [ObservableProperty]
    private bool _radarHasData;

    [ObservableProperty]
    private double _radarFade = 1.0;

    [ObservableProperty]
    private double _selectedRangeNm = 100;

    public string RangeText => $"Range: {SelectedRangeNm:F0} nm · center at aircraft";

    public ObservableCollection<RadarTileViewModel> RadarTiles { get; } = new();

    public double Ring25Diameter { get; set; }
    public double Ring25Left { get; set; }
    public double Ring50Diameter { get; set; }
    public double Ring50Left { get; set; }
    public double Ring100Diameter { get; set; }
    public double Ring100Left { get; set; }
    public double Ring250Diameter { get; set; }
    public double Ring250Left { get; set; }

    public RadarViewModel(MainViewModel main)
    {
        _main = main;
    }

    partial void OnSelectedRangeNmChanged(double value)
    {
        OnPropertyChanged(nameof(RangeText));
    }

    [RelayCommand]
    private void SetRange(object? param)
    {
        if (param is double d)
        {
            SelectedRangeNm = d;
        }
        else if (param is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            SelectedRangeNm = parsed;
        }
    }
}
