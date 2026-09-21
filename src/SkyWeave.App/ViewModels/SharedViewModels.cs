using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media.Imaging;

namespace SkyWeave.App.ViewModels;

public class CloudLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Density { get; set; } = string.Empty;
    public string Scattering { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
}

public class WindLayerViewModel
{
    public string Altitude { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string Turbulence { get; set; } = string.Empty;
    public string Wind { get; set; } = string.Empty;
}

public class LightningViewModel
{
    public string Latitude { get; set; } = string.Empty;
    public string Longitude { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class StormCellViewModel
{
    public string Latitude { get; set; } = string.Empty;
    public string Longitude { get; set; } = string.Empty;
    public string Intensity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Strikes { get; set; } = string.Empty;
}

public class IcingLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class TurbulenceLayerViewModel
{
    public string Base { get; set; } = string.Empty;
    public string Top { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class HazardViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ValidTo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AirportViewModel
{
    public string IcaoId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IataId { get; set; } = string.Empty;
}

public class TafGroupViewModel
{
    public string Type { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string FlightCategory { get; set; } = string.Empty;
    public string Wind { get; set; } = string.Empty;
    public string CloudSummary { get; set; } = string.Empty;
    public double DurationHours => (ValidTo - ValidFrom)?.TotalHours ?? 2;
    public bool IsTempo => Type == "TEMPO";
    public double BlockOpacity => IsTempo ? 0.6 : 1.0;
}

public partial class RadarTileViewModel : ObservableObject
{
    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private BitmapSource? _image;
}

public partial class MapStationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _icaoId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _distanceNm;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _flightCategory = "VFR";

    [ObservableProperty]
    private string _categoryColor = "#2ed573";

    [ObservableProperty]
    private string _borderColor = "#332ed573";

    [ObservableProperty]
    private string _tooltipText = string.Empty;

    public IRelayCommand? SelectCommand { get; set; }
}
