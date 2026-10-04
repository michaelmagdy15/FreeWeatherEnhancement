using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.App.ViewModels;

public partial class SoundingViewModel : ViewModelBase
{
    private readonly SoundingGenerator _generator = new();

    [ObservableProperty]
    private bool _isGraphicMode = true;

    [ObservableProperty]
    private string _temperatureSvg = string.Empty;

    [ObservableProperty]
    private string _dewpointSvg = string.Empty;

    [ObservableProperty]
    private double _freezingLevelY = 160.0;

    [ObservableProperty]
    private string _freezingLevelLabel = "0°C FREEZING LEVEL";

    [ObservableProperty]
    private double _aircraftLevelY;

    [ObservableProperty]
    private bool _hasAircraftAltitude;

    public ObservableCollection<SoundingLevel> StandardLevels { get; } = new();
    public ObservableCollection<SoundingCloudBlock> CloudBlocks { get; } = new();
    public ObservableCollection<SoundingHazardBand> HazardBands { get; } = new();

    [RelayCommand]
    private void ToggleMode()
    {
        IsGraphicMode = !IsGraphicMode;
    }

    public void Update(WeatherState state, double? aircraftAltFeet = null)
    {
        var data = _generator.Generate(state, aircraftAltFeet, 360.0, 240.0);

        TemperatureSvg = data.TemperaturePath;
        DewpointSvg = data.DewpointPath;
        FreezingLevelY = data.FreezingLevelY;
        FreezingLevelLabel = data.FreezingLevelLabel;

        if (data.AircraftY.HasValue)
        {
            AircraftLevelY = data.AircraftY.Value;
            HasAircraftAltitude = true;
        }
        else
        {
            HasAircraftAltitude = false;
        }

        StandardLevels.Clear();
        foreach (var lvl in data.StandardLevels)
        {
            StandardLevels.Add(lvl);
        }

        CloudBlocks.Clear();
        foreach (var cb in data.CloudBlocks)
        {
            CloudBlocks.Add(cb);
        }

        HazardBands.Clear();
        foreach (var hb in data.HazardBands)
        {
            HazardBands.Add(hb);
        }
    }
}
