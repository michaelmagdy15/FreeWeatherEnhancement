using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SkyWeave.App.Models;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.App.ViewModels;

public partial class RadarViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly SynopticMapGenerator _synopticGenerator = new();

    [ObservableProperty]
    private string _radarTimestamp = "---";

    [ObservableProperty]
    private bool _radarHasData;

    [ObservableProperty]
    private double _radarFade = 1.0;

    [ObservableProperty]
    private double _selectedRangeNm = 100;

    [ObservableProperty]
    private bool _showRadar = true;

    [ObservableProperty]
    private bool _showMap = true;

    [ObservableProperty]
    private bool _showTraffic = true;

    [ObservableProperty]
    private bool _showVatsim = true;

    [ObservableProperty]
    private bool _showIvao = true;

    [ObservableProperty]
    private bool _showIsobars = true;

    [ObservableProperty]
    private bool _showWindBarbs = true;

    [ObservableProperty]
    private bool _showStations = true;

    public string RangeText => $"Range: {SelectedRangeNm:F0} nm · center at aircraft";

    public ObservableCollection<RadarTileViewModel> MapTiles { get; } = new();
    public ObservableCollection<RadarTileViewModel> RadarTiles { get; } = new();
    public ObservableCollection<OnlineFlightViewModel> OnlineTraffic { get; } = new();
    public ObservableCollection<IsobarLine> Isobars { get; } = new();
    public ObservableCollection<PressureCenter> PressureCenters { get; } = new();
    public ObservableCollection<WindBarb> WindBarbs { get; } = new();

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

    [RelayCommand]
    private void ToggleLayer(string layerName)
    {
        switch (layerName?.ToUpperInvariant())
        {
            case "MAP":
                ShowMap = !ShowMap;
                break;
            case "RADAR":
                ShowRadar = !ShowRadar;
                break;
            case "TRAFFIC":
                ShowTraffic = !ShowTraffic;
                break;
            case "VATSIM":
                ShowVatsim = !ShowVatsim;
                break;
            case "IVAO":
                ShowIvao = !ShowIvao;
                break;
            case "ISOBARS":
                ShowIsobars = !ShowIsobars;
                break;
            case "WINDS":
                ShowWindBarbs = !ShowWindBarbs;
                break;
            case "STATIONS":
                ShowStations = !ShowStations;
                break;
        }
    }

    public void UpdateSynoptic(WeatherState state, IReadOnlyList<AirportData>? nearbyStations)
    {
        var data = _synopticGenerator.Generate(
            state.Latitude,
            state.Longitude,
            state.AltimeterHpa,
            state.WindDirectionDegrees,
            state.WindSpeedKnots,
            nearbyStations,
            SelectedRangeNm,
            768.0,
            768.0);

        Isobars.Clear();
        foreach (var iso in data.Isobars)
        {
            Isobars.Add(iso);
        }

        PressureCenters.Clear();
        foreach (var c in data.Centers)
        {
            PressureCenters.Add(c);
        }

        WindBarbs.Clear();
        foreach (var barb in data.Barbs)
        {
            WindBarbs.Add(barb);
        }
    }
}
