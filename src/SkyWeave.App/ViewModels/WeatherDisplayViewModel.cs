using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using SkyWeave.Core.Models;
using System.Linq;
using System;
using SkyWeave.App.Models;

namespace SkyWeave.App.ViewModels;

public partial class WeatherDisplayViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _stationId = "---";

    [ObservableProperty]
    private string _temperature = "--°C";

    [ObservableProperty]
    private string _dewpoint = "--°C";

    [ObservableProperty]
    private string _wind = "---";

    [ObservableProperty]
    private string _visibility = "---";

    [ObservableProperty]
    private string _altimeter = "---";

    [ObservableProperty]
    private string _flightCategory = "---";

    [ObservableProperty]
    private string _freezingLevel = "---";

    [ObservableProperty]
    private string _ceiling = "---";

    [ObservableProperty]
    private string _icingIndex = "---";

    [ObservableProperty]
    private string _turbulenceIndex = "---";

    [ObservableProperty]
    private string _thunderstormIntensity = "---";

    [ObservableProperty]
    private string _precipitation = "---";

    [ObservableProperty]
    private string _humidity = "---";

    [ObservableProperty]
    private string _aerosolDensity = "---";

    [ObservableProperty]
    private string _lightningCount = "---";

    [ObservableProperty]
    private string _closestStrike = "---";

    [ObservableProperty]
    private string _stormCellCount = "---";

    [ObservableProperty]
    private string _rawMetar = "---";

    [ObservableProperty]
    private string _sourceModel = "---";

    [ObservableProperty]
    private string _dataAge = "---";

    [ObservableProperty]
    private string _capeValue = "---";

    [ObservableProperty]
    private string _liftedIndexValue = "---";

    public ObservableCollection<CloudLayerViewModel> CloudLayers { get; } = new();
    public ObservableCollection<WindLayerViewModel> WindLayers { get; } = new();
    public ObservableCollection<LightningViewModel> LightningStrikes { get; } = new();
    public ObservableCollection<StormCellViewModel> StormCells { get; } = new();
    public ObservableCollection<IcingLayerViewModel> IcingLayers { get; } = new();
    public ObservableCollection<TurbulenceLayerViewModel> TurbulenceLayers { get; } = new();
    public ObservableCollection<HazardViewModel> Hazards { get; } = new();

    public void Update(WeatherState state, int lightningCount, string closestStrike, int stormCellCount)
    {
        StationId = state.StationId;
        Temperature = $"{state.TemperatureCelsius:F1}°C";
        Dewpoint = $"{state.DewpointCelsius:F1}°C";
        Wind = $"{state.WindDirectionDegrees:F0}° @ {state.WindSpeedKnots:F0} kt";
        Visibility = $"{state.VisibilityMeters / 1609.344:F1} SM";
        Altimeter = $"{state.AltimeterHpa:F1} hPa";
        FlightCategory = state.FlightCategory;
        FreezingLevel = $"{state.FreezingLevelFeet:F0} ft";
        Ceiling = $"{state.CeilingFeet:F0} ft";
        IcingIndex = $"{state.IcingIndex:P0}";
        TurbulenceIndex = $"{state.TurbulenceIndex:P0}";
        ThunderstormIntensity = $"{state.ThunderstormIntensity:P0}";
        Precipitation = $"{state.PrecipitationRate:F1} mm/hr";
        Humidity = $"{state.HumidityPercent:F0}%";
        AerosolDensity = $"{state.AerosolDensity:P0}";
        RawMetar = $"Station: {state.StationId} | Temp: {state.TemperatureCelsius:F1}°C | Dew: {state.DewpointCelsius:F1}°C | Wind: {state.WindDirectionDegrees:F0}@{state.WindSpeedKnots:F0}";
        SourceModel = state.SourceModelName;
        DataAge = $"{(int)state.DataAgeMinutes} min";
        CapeValue = state.ConvectiveAvailablePotentialEnergy is double c ? $"{c:F0} J/kg" : "---";
        LiftedIndexValue = state.LiftedIndex is double li ? $"{li:F1}" : "---";
        
        LightningCount = lightningCount.ToString();
        ClosestStrike = closestStrike;
        StormCellCount = stormCellCount.ToString();

        CloudLayers.Clear();
        foreach (var layer in state.CloudLayers)
        {
            CloudLayers.Add(new CloudLayerViewModel
            {
                Base = $"{layer.BaseFeetAgl:F0} ft AGL",
                Top = $"{layer.TopFeetAgl:F0} ft AGL",
                Coverage = layer.CoveragePercent.ToString("P0"),
                Type = layer.Type.ToString(),
                Density = layer.Density.ToString("P0")
            });
        }

        WindLayers.Clear();
        foreach (var layer in state.WindsAloft)
        {
            WindLayers.Add(new WindLayerViewModel
            {
                Altitude = $"{layer.AltitudeFeet:F0} ft",
                Wind = $"{layer.DirectionDegrees:F0}° / {layer.SpeedKnots:F0} kt",
                Temperature = $"{layer.TemperatureCelsius:F1}°C",
                Turbulence = layer.TurbulenceIntensity?.ToString("F2") ?? "0.00"
            });
        }

        IcingLayers.Clear();
        foreach (var layer in state.IcingLayers)
        {
            IcingLayers.Add(new IcingLayerViewModel
            {
                Base = $"{layer.BaseFeet:F0} ft",
                Top = $"{layer.TopFeet:F0} ft",
                Severity = layer.Severity.ToString(),
                Type = layer.IcingType.ToString()
            });
        }

        TurbulenceLayers.Clear();
        foreach (var layer in state.TurbulenceLayers)
        {
            TurbulenceLayers.Add(new TurbulenceLayerViewModel
            {
                Base = $"{layer.BaseFeet:F0} ft",
                Top = $"{layer.TopFeet:F0} ft",
                Severity = layer.Intensity.ToString(),
                Type = layer.Type.ToString()
            });
        }

        Hazards.Clear();
        foreach (var hazard in state.Hazards)
        {
            Hazards.Add(new HazardViewModel
            {
                Id = hazard.Id,
                Type = hazard.Type.ToString(),
                Severity = hazard.Severity.ToString("F1"),
                ValidTo = $"{hazard.ValidFrom:HHmm}Z - {hazard.ValidTo:HHmm}Z",
                Description = hazard.Description ?? "No description"
            });
        }
    }
}
